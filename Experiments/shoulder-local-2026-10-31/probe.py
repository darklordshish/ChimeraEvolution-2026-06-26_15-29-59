"""Single-side, fixed-geometry topology/weight spike; never touches Assets."""
import bpy,bmesh,numpy as np,sys,math,json
from pathlib import Path
from mathutils import Matrix
root=Path(__file__).resolve().parent
sys.path.insert(0,str(root.parent/'shoulder-support-2026-10-31'))
from support_motion import apply_support
source=root.parent/'shoulder-support-2026-10-31/support.blend'
bpy.ops.wm.open_mainfile(filepath=str(source))
arm=next(o for o in bpy.data.objects if o.type=='ARMATURE');aw=arm.matrix_world
objs=[o for o in bpy.data.objects if o.type=='MESH' and '@' not in o.name and o.data.shape_keys and 'Поджарый' in o.data.shape_keys.key_blocks]
names=[b.name for b in arm.data.bones];ni={n:i for i,n in enumerate(names)}
rest={n:aw@arm.data.bones[n].matrix_local for n in names}
cfg=json.loads((root.parent/'shoulder-support-2026-10-31/helper-benchmark.json').read_text(encoding='utf-8'))['selected']
poses=[('rest',0,'Y'),('raise45',45,'Y'),('raise70',70,'Y'),('raise90',90,'Y'),('raise100',100,'Y'),('forward60',-60,'X')]
skins=[]
for label,angle,axis in poses:
    for b in arm.pose.bones:b.matrix_basis=Matrix.Identity(4)
    bpy.context.view_layer.update()
    for suf in ('','.L'):
        pb=arm.pose.bones['плечо'+suf];h=aw@pb.head;side=1 if h.x>0 else -1
        d=Matrix.Translation(h)@Matrix.Rotation(math.radians(-side*angle if axis=='Y' else angle),4,axis)@Matrix.Translation(-h)
        pb.matrix=aw.inverted()@d@rest[pb.name]
        bpy.context.view_layer.update()
    apply_support(arm,angle,axis,cfg)
    skins.append(np.array([(aw@arm.pose.bones[n].matrix)@rest[n].inverted() for n in names]))
S=np.array(skins)
lookup={};X=[];W=[];copies=[];ids={}
for o in objs:
    gn={g.index:g.name for g in o.vertex_groups}
    for v in o.data.vertices:
        key=tuple(round(t,4) for t in o.matrix_world@v.co)
        if key not in lookup:
            lookup[key]=len(X);X.append(list(o.matrix_world@o.data.shape_keys.key_blocks['Поджарый'].data[v.index].co));copies.append([])
            row=np.zeros(len(names))
            for g in v.groups:
                if gn[g.group] in ni:row[ni[gn[g.group]]]=g.weight
            W.append(row)
        idx=lookup[key];ids[o.name,v.index]=idx;copies[idx].append((o,v.index))
X=np.array(X);W=np.array(W);W0=W.copy()
Q=np.einsum('pbij,vj->pvbi',S[:,:,:3,:3],X)+S[None,:,:,:3,3].transpose(1,0,2,3)
P=np.einsum('vb,pvbi->pvi',W,Q)
def normal(points,f):return np.cross(points[...,f[1],:]-points[...,f[0],:],points[...,f[2],:]-points[...,f[0],:])
def quality(points,f):
    n=normal(points,f);l=sum(np.sum((points[...,f[(i+1)%3],:]-points[...,f[i],:])**2,axis=-1) for i in range(3))
    return 2*math.sqrt(3)*np.linalg.norm(n,axis=-1)/np.maximum(l,1e-12)
def ff(o,f):return [ids[o.name,v.index] for v in f.verts]
flips=[]
for o in objs:
    if o.name not in ('Руки','Сердце','хребет'):continue
    bm=bmesh.new();bm.from_mesh(o.data);bm.verts.ensure_lookup_table()
    crease=bm.edges.layers.int.get('перелом_линия')
    for sweep in range(2):
        for e in list(bm.edges):
            if not e.is_valid or len(e.link_faces)!=2 or any(len(f.verts)!=3 for f in e.link_faces):continue
            if crease and e[crease]:continue
            old=[ff(o,f) for f in e.link_faces];center=X[list(set(sum(old,[])))].mean(0)
            if not (.07<center[0]<.32 and 1.18<center[2]<1.57):continue
            # Keep old patch orientation in every pose; reject any local fold regression.
            refs=sum(normal(P,f) for f in old);rn=np.linalg.norm(refs,axis=-1)
            if np.any(rn<1e-8):continue
            refs/=rn[:,None]
            def score(fs):
                signs=np.array([np.einsum('pi,pi->p',normal(P,f),refs) for f in fs])
                folds=(signs< -1e-10).sum(axis=0)
                q=np.array([quality(P,f) for f in fs])
                return folds,float(np.mean(1/np.maximum(q,.02))),q.min(axis=0)
            oldscore=score(old)
            bm.edges.index_update();trial=bm.copy();trial.edges.ensure_lookup_table()
            res=bmesh.ops.rotate_edges(trial,edges=[trial.edges[e.index]],use_ccw=False)
            accept=False
            if res['edges']:
                fs=[ff(o,f) for f in res['edges'][0].link_faces]
                newscore=score(fs)
                accept=(len(fs)==2 and np.all(newscore[0]<=oldscore[0]) and newscore[1]<oldscore[1]*.94 and np.all(newscore[2]>=oldscore[2]-.08))
            trial.free()
            if accept:
                bmesh.ops.rotate_edges(bm,edges=[e],use_ccw=False);flips.append(dict(object=o.name,center=center.tolist()))
    bm.to_mesh(o.data);bm.free();o.data.update()
F=[];owner=[]
for o in objs:
    for p in o.data.polygons:
        vs=[ids[o.name,i] for i in p.vertices]
        for j in range(1,len(vs)-1):F.append([vs[0],vs[j],vs[j+1]]);owner.append(o.name)
F=np.array(F);cent=X[F].mean(1)
patch=(cent[:,0]>.06)&(cent[:,0]<.34)&(cent[:,2]>1.12)&(cent[:,2]<1.6)
N=np.cross(X[F[:,1]]-X[F[:,0]],X[F[:,2]]-X[F[:,0]])
area=np.linalg.norm(N,axis=1);dom=W0[F[:,0]].argmax(1)
refs=np.einsum('pfij,fj->pfi',S[:,dom,:3,:3],N)/np.maximum(area[None,:,None],1e-12)
def signs(points):return np.einsum('pfi,pfi->pf',np.cross(points[:,F[:,1]]-points[:,F[:,0]],points[:,F[:,2]]-points[:,F[:,0]]),refs)
def stats(points):
    sig=signs(points);ar=np.linalg.norm(np.cross(points[:,F[:,1]]-points[:,F[:,0]],points[:,F[:,2]]-points[:,F[:,0]]),axis=-1)
    return [dict(pose=p[0],folds=int(((sig[k]<-1e-10)&patch).sum()),compressed=int(((ar[k]<area*.25)&patch).sum())) for k,p in enumerate(poses)]
before=stats(P);diagnostic=[]
for f in np.nonzero(patch & np.any(signs(P)<-1e-10,axis=0))[0]:
    diagnostic.append(dict(object=owner[f],center=cent[f].tolist(),vertices=F[f].tolist()))
(root/'diagnostic.json').write_text(json.dumps(dict(after_topology=before,fold_locations=diagnostic,flips=flips),ensure_ascii=False,indent=2),encoding='utf-8')
# Coordinate descent transfers at most .12 between two existing influences.
# Shared seam vertices are one variable; no influence is added or discarded.
armset={b.name for b in arm.data.bones['плечо.L'].children_recursive}|{'плечо.L'}
moving=[ni[n] for n in armset];fixed=[i for i,n in enumerate(names) if n not in armset and not n.startswith('опора_')]
changes=[]
for sweep in range(3):
    for vi,x in enumerate(X):
        if not (.07<x[0]<.32 and 1.18<x[2]<1.57):continue
        ma=max(moving,key=lambda i:W0[vi,i]);st=max(fixed,key=lambda i:W0[vi,i])
        if min(W0[vi,ma],W0[vi,st])<.015:continue
        faces=np.nonzero(np.any(F==vi,axis=1)&patch)[0]
        if not len(faces):continue
        fs=F[faces];r=refs[:,faces];a=area[faces]
        def local(points):
            n=np.cross(points[:,fs[:,1]]-points[:,fs[:,0]],points[:,fs[:,2]]-points[:,fs[:,0]])
            signed=np.einsum('pfi,pfi->pf',n,r)/np.maximum(a[None,:],1e-12)
            loss=np.mean(np.maximum(.25-signed,0)**2)+.02*np.mean(np.maximum(np.linalg.norm(n,axis=-1)/np.maximum(a[None,:],1e-12)-2,0)**2)
            return (signed< -1e-6).sum(axis=1),float(loss)
        current=local(P);best=(current[1],W[vi,st]-W0[vi,st],P[:,vi].copy())
        for delta in (-.12,-.08,-.04,0,.04,.08,.12):
            if W0[vi,st]+delta<0 or W0[vi,ma]-delta<0:continue
            previous=P[:,vi].copy();new=P[:,vi]+(delta-(W[vi,st]-W0[vi,st]))*(Q[:,vi,st]-Q[:,vi,ma]);P[:,vi]=new
            folds,loss=local(P);P[:,vi]=previous
            loss+=.005*delta*delta
            if np.all(folds<=current[0]) and loss<best[0]-1e-8:best=(loss,delta,new.copy())
        if abs(best[1]-(W[vi,st]-W0[vi,st]))>1e-7:
            W[vi]=W0[vi];W[vi,st]+=best[1];W[vi,ma]-=best[1];P[:,vi]=best[2]
after=stats(P)
assert all(a['folds']<=b['folds'] for a,b in zip(after,before)),(before,after)
for vi in np.nonzero(np.max(abs(W-W0),axis=1)>1e-7)[0]:
    for o,j in copies[vi]:
        for i,n in enumerate(names):
            if abs(W[vi,i]-W0[vi,i])>1e-7:o.vertex_groups[n].add([j],float(W[vi,i]),'REPLACE')
    changes.append(dict(point=X[vi].tolist(),max_weight_delta=float(np.max(abs(W[vi]-W0[vi])))))
for b in arm.pose.bones:b.matrix_basis=Matrix.Identity(4)
for o in objs:
    for k in o.data.shape_keys.key_blocks:k.value=0
bpy.context.view_layer.update()
bpy.ops.wm.save_as_mainfile(filepath=str(root/'candidate.blend'))
report=dict(source=str(source),side='+X only',topology_flips=flips,weight_changes=changes,before_weights=before,after_weights=after,max_weight_change=float(np.max(abs(W-W0))),triangles=len(F),note='Same vertex coordinates and keys; other species keys require pose revalidation after weight edits. No anatomy score.')
(root/'result.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print('LOCAL_RESULT',json.dumps({k:v for k,v in report.items() if k not in ('topology_flips','weight_changes')},ensure_ascii=False));print('FLIPS',len(flips),'WEIGHTS',len(changes))
