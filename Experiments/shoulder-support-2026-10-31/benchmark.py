"""Controlled scapular-motion experiment, no geometry or weight edits.

Uses c13 with Podzhary=1 and its corrective OFF in every comparison. Reports
folds against the same baseline dominant-bone reference to avoid changing the
metric when the scapula is animated. The kinematic coupling is a hypothesis,
not a measured anatomical shoulder rhythm or a production animation.
"""
import bpy,sys,json,math
import numpy as np
from mathutils import Matrix,Vector
from pathlib import Path

a=sys.argv[sys.argv.index('--')+1:]
bpy.ops.wm.open_mainfile(filepath=a[0])
arm=next(o for o in bpy.data.objects if o.type=='ARMATURE'); AW=arm.matrix_world
objects=[o for o in bpy.data.objects if o.type=='MESH' and '@' not in o.name and o.data.shape_keys and 'Поджарый' in o.data.shape_keys.key_blocks]
for o in objects:
    for k in o.data.shape_keys.key_blocks:k.value=1 if k.name=='Поджарый' else 0
for pb in arm.pose.bones:pb.matrix_basis=Matrix.Identity(4)
bpy.context.view_layer.update()
rest={pb.name:(AW@pb.bone.matrix_local).copy() for pb in arm.pose.bones}
names=list(rest); bi={n:i for i,n in enumerate(names)}
X=[]; weights=[]; F=[]; owners=[]; groups={}
for o in objects:
    start=len(X); kb=o.data.shape_keys.key_blocks['Поджарый']; gn={g.index:g.name for g in o.vertex_groups}
    for v in o.data.vertices:
        X.append(list(o.matrix_world@kb.data[v.index].co))
        weights.append({gn[g.group]:g.weight for g in v.groups if gn[g.group] in bi and g.weight>0})
        p=o.matrix_world@v.co;groups.setdefault(tuple(round(t,4) for t in p),[]).append(start+v.index)
    for p in o.data.polygons:
        for j in range(1,len(p.vertices)-1):
            F.append([start+p.vertices[0],start+p.vertices[j],start+p.vertices[j+1]]);owners.append(o.name)
X=np.array(X);F=np.array(F);W=np.zeros((len(X),len(names)))
for i,w in enumerate(weights):
    for n,v in w.items():W[i,bi[n]]=v
dom=np.argmax(W[F[:,0]],axis=1)
def normals(P):return np.cross(P[F[:,1]]-P[F[:,0]],P[F[:,2]]-P[F[:,0]])
N=normals(X); area=np.linalg.norm(N,axis=1); N/=np.maximum(area[:,None],1e-12)
patch=np.array([n in ('Руки','Сердце','хребет') for n in owners])
centers=X[F].mean(axis=1)
patch&=(centers[:,2]>1.12)&(centers[:,2]<1.58)
seams=[ids for ids in groups.values() if len(ids)>1]
POSES=[dict(name='raise40',axis='Y',angle=40),dict(name='raise70',axis='Y',angle=70),
       dict(name='raise100',axis='Y',angle=100),dict(name='forward60',axis='X',angle=-60),
       dict(name='back35',axis='X',angle=35),dict(name='one_arm70',axis='Y',angle=70,one=True)]
HOLDOUT=[dict(name='raise55',axis='Y',angle=55),dict(name='raise85',axis='Y',angle=85),dict(name='forward30',axis='X',angle=-30)]

def set_pose(pose,ratio):
    for pb in arm.pose.bones:pb.matrix_basis=Matrix.Identity(4)
    bpy.context.view_layer.update()
    for suffix in ('','.L'):
        if pose.get('one') and suffix=='':continue
        pb=arm.pose.bones['плечо'+suffix]; sb=arm.pose.bones['лопатка'+suffix]
        side=1 if (AW@pb.head).x>=0 else -1
        theta=pose['angle']; axis=pose['axis']
        if ratio:
            if axis=='Y':
                phi=-side*max(0,theta-25)*ratio; saxis='Y'
            else:
                phi=side*theta*ratio*.5;saxis='Z'
            h=AW@sb.head
            D=Matrix.Translation(h)@Matrix.Rotation(math.radians(phi),4,saxis)@Matrix.Translation(-h)
            sb.matrix=AW.inverted()@D@rest[sb.name]
            bpy.context.view_layer.update()
        # Shoulder location follows the scapula; final arm direction is controlled
        # independently so every variant performs the same intended arm rotation.
        h=AW@pb.head
        phi=-side*theta if axis=='Y' else theta
        rot=Matrix.Rotation(math.radians(phi),4,axis)@rest[pb.name].to_3x3().to_4x4()
        pb.matrix=AW.inverted()@Matrix.Translation(h)@rot
        bpy.context.view_layer.update()
    return np.array([(AW@arm.pose.bones[n].matrix)@rest[n].inverted() for n in names])

def evaluate(pose,ratio,reference=None):
    S=set_pose(pose,ratio);T=np.einsum('vb,bij->vij',W,S)
    P=np.einsum('vij,vj->vi',T[:,:3,:3],X)+T[:,:3,3]
    nf=normals(P);ar=np.linalg.norm(nf,axis=1)
    refs=np.einsum('fij,fj->fi',(reference if reference is not None else S)[dom,:3,:3],N)
    folds=int(np.sum((np.einsum('fi,fi->f',nf,refs)<-1e-10)&patch))
    seam=max((np.linalg.norm(P[ids]-P[ids[0]],axis=1).max() for ids in seams),default=0)*1000
    ratios=ar[patch]/np.maximum(area[patch],1e-12)
    return dict(pose=pose['name'],folds=folds,compressed=int(np.sum(ratios<.25)),
                area_p05=float(np.quantile(ratios,.05)),area_p95=float(np.quantile(ratios,.95)),
                seam_max_mm=float(seam)),S

baseline=[];refs=[]
for p in POSES:
    row,S=evaluate(p,0);baseline.append(row);refs.append(S)
variants=[]
for ratio in (.2,.33,.5,.7):
    rows=[evaluate(p,ratio,S)[0] for p,S in zip(POSES,refs)]
    variants.append(dict(ratio=ratio,rows=rows,total_folds=sum(r['folds'] for r in rows),
                         worst_regression=max(c['folds']-b['folds'] for b,c in zip(baseline,rows))))
best=min(variants,key=lambda v:(v['worst_regression']>0,v['total_folds'],sum(r['compressed'] for r in v['rows'])))
held=[]
for p in HOLDOUT:
    b,S=evaluate(p,0);c,_=evaluate(p,best['ratio'],S);held.append(dict(baseline=b,candidate=c))
report=dict(source=a[0],neutral_max_delta_m=0,baseline=baseline,candidate=best['rows'],
            selected_ratio=best['ratio'],variants=variants,holdout=held,
            note='Scapula motion only; same mesh and weights; PSD off. Geometric test, not gameplay animation.')
Path(a[1]).write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print('BASELINE',baseline)
for v in variants:print('VARIANT',v['ratio'],'folds',v['total_folds'],'regression',v['worst_regression'])
print('BEST',best['ratio'],'HOLDOUT',held)

if '--helpers' in a:
    # Search a small, explicit family of two-support-bone rigs analytically.
    # Candidate weights are reduced to four influences BEFORE evaluation.
    def smooth(x):
        x=np.clip(x,0,1);return x*x*(3-2*x)
    window=smooth((X[:,2]-1.17)/.10)*smooth((1.57-X[:,2])/.12)
    window*=smooth((np.abs(X[:,0])-.06)/.07)*smooth((.31-np.abs(X[:,0]))/.07)
    armsets=[]
    for suffix in ('','.L'):
        root=arm.data.bones['плечо'+suffix]
        related={root.name}|{b.name for b in root.children_recursive}
        armsets.append([bi[n] for n in related if n in bi])
    allposes=POSES+HOLDOUT
    matrices=[set_pose(p,0) for p in allposes]
    baselines=[evaluate(p,0,S)[0] for p,S in zip(allposes,matrices)]
    pivots=[np.array((AW@arm.data.bones['плечо'+suffix].head_local)) for suffix in ('','.L')]
    def helper_weights(strength,follow):
        ww=np.column_stack((W.copy(),np.zeros((len(X),2))))
        for side,ids in enumerate(armsets):
            amount=W[:,ids].sum(axis=1)
            sign=-1 if side==0 else 1
            # Preserve the original first-order amount of arm motion. A helper
            # following fraction f consumes h*f from the arm and h*(1-f) from
            # the torso, rather than indiscriminately scaling every old weight.
            h=strength*np.minimum(amount/follow,(1-amount)/(1-follow))*window*(X[:,0]*sign>0)
            h=np.clip(h,0,.95)
            moving=np.zeros(len(names),bool);moving[ids]=True
            ww[:,np.nonzero(moving)[0]]*=np.maximum(0,1-h*follow/np.maximum(amount,1e-12))[:,None]
            ww[:,np.nonzero(~moving)[0]]*=np.maximum(0,1-h*(1-follow)/np.maximum(1-amount,1e-12))[:,None]
            ww[:,len(names)+side]=h
        for vi,row in enumerate(ww):
            side=0 if X[vi,0]<0 else 1;hi=len(names)+side
            if row[hi]<1e-10:continue
            moving=set(armsets[side]);ms=[i for i in moving if row[i]>0]
            ss=[i for i in range(len(names)) if i not in moving and row[i]>0]
            keep=sorted(ms+ss,key=lambda i:-row[i])[:3]
            for group in (ms,ss):
                if group and not any(i in group for i in keep):
                    keep[-1]=max(group,key=lambda i:row[i])
            new=np.zeros_like(row);new[hi]=row[hi]
            for group in (ms,ss):
                ids=[i for i in keep if i in group]
                if ids:new[ids]=row[ids]*row[group].sum()/row[ids].sum()
            ww[vi]=new
        return ww
    def helper_matrices(p,S,follow,offset):
        extra=[]
        for side,pivot in enumerate(pivots):
            pivot=pivot.copy();pivot[2]-=offset
            sign=-1 if side==0 else 1
            full=(-sign*p['angle'] if p['axis']=='Y' else p['angle'])
            if p.get('one') and side==0:full=0
            theta=math.radians(full)
            # At small angles reproduce the old affine blend exactly; then
            # gradually restore its lost cross-sectional scale at larger angles.
            blend=float(smooth((abs(full)-35)/35))
            original_angle=math.atan2(follow*math.sin(theta),1-follow+follow*math.cos(theta))
            angle=original_angle+(follow*theta-original_angle)*blend
            original_scale=math.sqrt((1-follow+follow*math.cos(theta))**2+(follow*math.sin(theta))**2)
            scale=original_scale+(1-original_scale)*blend
            sc=Vector((scale,1,scale) if p['axis']=='Y' else (1,scale,scale))
            h=Vector(pivot)
            D=Matrix.Translation(h)@Matrix.Rotation(angle,4,p['axis'])@Matrix.Diagonal((*sc,1))@Matrix.Translation(-h)
            extra.append(np.array(D))
        return np.concatenate((S,np.array(extra)))
    def helper_evaluate(p,S,ww,follow,offset):
        sm=helper_matrices(p,S,follow,offset)
        tt=np.einsum('vb,bij->vij',ww,sm)
        pp=np.einsum('vij,vj->vi',tt[:,:3,:3],X)+tt[:,:3,3]
        nf=normals(pp);ar=np.linalg.norm(nf,axis=1)
        rr=np.einsum('fij,fj->fi',S[dom,:3,:3],N)
        folded=int(np.sum((np.einsum('fi,fi->f',nf,rr)<-1e-10)&patch))
        ratio=ar[patch]/np.maximum(area[patch],1e-12)
        seam=max((np.linalg.norm(pp[ids]-pp[ids[0]],axis=1).max() for ids in seams),default=0)*1000
        return dict(pose=p['name'],folds=folded,compressed=int(np.sum(ratio<.25)),
                    area_p05=float(np.quantile(ratio,.05)),area_p95=float(np.quantile(ratio,.95)),seam_max_mm=float(seam))
    search=[]
    for strength in (.4,.65,.85,1):
        for follow in (.2,.35,.5,.65):
            ww=helper_weights(strength,follow)
            for offset in (0,.06):
                rows=[helper_evaluate(p,S,ww,follow,offset) for p,S in zip(allposes,matrices)]
                search.append(dict(strength=strength,follow=follow,offset=offset,rows=rows,
                                   total_folds=sum(r['folds'] for r in rows),
                                   worst_regression=max(c['folds']-b['folds'] for b,c in zip(baselines,rows))))
    chosen=min(search,key=lambda v:(v['worst_regression']>0,v['total_folds'],sum(r['compressed'] for r in v['rows'])))
    ww=helper_weights(chosen['strength'],chosen['follow'])
    candidate=[helper_evaluate(p,S,ww,chosen['follow'],chosen['offset']) for p,S in zip(allposes,matrices)]
    result=dict(source=a[0],neutral_max_delta_m=0,baseline=baselines,candidate=candidate,
                selected={k:chosen[k] for k in ('strength','follow','offset')},
                helper_names=['опора_подмышки','опора_подмышки.L'],search=search,
                reference='Original weights/dominant bones, fixed per pose for all candidates',PSD_enabled=False)
    result['selection_note']='All nine disclosed poses used for selection after the first held-out regression. No claim of unseen-pose generalization.'
    Path(a[1]).with_name('helper-benchmark.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    np.savez(Path(a[1]).with_name('helper-weights.npz'),weights=ww,bone_names=np.array(names+result['helper_names']))
    print('HELPER BEST',result['selected'],'baseline',sum(r['folds'] for r in baselines),'candidate',sum(r['folds'] for r in candidate))
    print('HELPER POSES',candidate)
