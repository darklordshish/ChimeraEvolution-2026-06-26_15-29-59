"""Bounded shape adjustment against signed-area constraints in two poses.

Does not edit rig/weights. Shared vertices are optimized as a single variable.
Checks are geometric constraints, not an anatomical correctness score.
"""
import bpy,sys,json,math
import numpy as np
from mathutils import Matrix,Vector
from mathutils.kdtree import KDTree
a=sys.argv[sys.argv.index('--')+1:]
bpy.ops.wm.open_mainfile(filepath=a[0])
arm=next(o for o in bpy.data.objects if o.type=='ARMATURE'); AW=arm.matrix_world
objs=[o for o in bpy.data.objects if o.type=='MESH' and '@' not in o.name and o.data.shape_keys and 'Поджарый' in o.data.shape_keys.key_blocks]
for pb in arm.pose.bones:pb.matrix_basis=Matrix.Identity(4)
bpy.context.view_layer.update()
rest={pb.name:AW@pb.bone.matrix_local for pb in arm.pose.bones}
for bn in ('плечо','плечо.L'):
    pb=arm.pose.bones.get(bn)
    if pb is None:continue
    h=AW@pb.head; sign=1 if h.x>=0 else -1
    D=Matrix.Translation(h)@Matrix.Rotation(math.radians(-sign*70),4,'Y')@Matrix.Translation(-h)
    pb.matrix=AW.inverted()@D@AW@pb.matrix
    bpy.context.view_layer.update()
skin={pb.name:np.array((AW@pb.matrix)@rest[pb.name].inverted()) for pb in arm.pose.bones}
skins=[skin]
if '--multi' in a:
    for angle in (40,100):
        for pb in arm.pose.bones:pb.matrix_basis=Matrix.Identity(4)
        bpy.context.view_layer.update()
        for bn in ('плечо','плечо.L'):
            pb=arm.pose.bones.get(bn)
            if pb is None:continue
            h=AW@pb.head;sign=1 if h.x>=0 else -1
            D=Matrix.Translation(h)@Matrix.Rotation(math.radians(-sign*angle),4,'Y')@Matrix.Translation(-h)
            pb.matrix=AW.inverted()@D@AW@pb.matrix
            bpy.context.view_layer.update()
        skins.append({pb.name:np.array((AW@pb.matrix)@rest[pb.name].inverted()) for pb in arm.pose.bones})
X=[]; B=[]; maps={}; weights=[]; refs={}; faces=[]; dominant=[]; owner=[]
lookup={}
for o in objs:
    kb=o.data.shape_keys.key_blocks['Поджарый']; gn={g.index:g.name for g in o.vertex_groups}
    for v in o.data.vertices:
        b=o.matrix_world@v.co; key=tuple(round(c,4) for c in b)
        if key not in lookup:
            i=len(X); lookup[key]=i; B.append(list(b)); X.append(list(o.matrix_world@kb.data[v.index].co))
            ws={gn[g.group]:g.weight for g in v.groups if gn[g.group] in skin and g.weight>0}
            weights.append(ws); refs[i]=[]
        i=lookup[key]; maps[o.name,v.index]=i; refs[i].append((o,v.index))
    for p in o.data.polygons:
        ids=[maps[o.name,int(i)] for i in p.vertices]
        for j in range(1,len(ids)-1):
            f=[ids[0],ids[j],ids[j+1]]; faces.append(f); owner.append(o.name)
            dominant.append(max(weights[f[0]],key=weights[f[0]].get,default=None))
X=np.array(X,dtype=float); B=np.array(B); F=np.array(faces); X0=X.copy()
T=np.array([sum((w*skin[n] for n,w in ws.items()),np.eye(4)*max(0,1-sum(ws.values()))) for ws in weights])
R=T[:,:3,:3]; t=T[:,:3,3]
def normals(P):return np.cross(P[F[:,1]]-P[F[:,0]],P[F[:,2]]-P[F[:,0]])
N0=normals(B); ref0=N0/np.maximum(np.linalg.norm(N0,axis=1)[:,None],1e-12)
Rf=np.array([skin[n][:3,:3] if n else np.eye(3) for n in dominant])
ref1=np.einsum('fij,fj->fi',Rf,ref0); ref1/=np.maximum(np.linalg.norm(ref1,axis=1)[:,None],1e-12)
pose_models=[(R,t,ref1)]
for sk in skins[1:]:
    tt=np.array([sum((w*sk[n] for n,w in ws.items()),np.eye(4)*max(0,1-sum(ws.values()))) for ws in weights])
    rr=np.array([sk[n][:3,:3] if n else np.eye(3) for n in dominant])
    rf=np.einsum('fij,fj->fi',rr,ref0);rf/=np.maximum(np.linalg.norm(rf,axis=1)[:,None],1e-12)
    pose_models.append((tt[:,:3,:3],tt[:,:3,3],rf))
area=np.linalg.norm(normals(X),axis=1)
active=(np.abs(X[:,0])>.06)&(np.abs(X[:,0])<.28)&(X[:,2]>1.15)&(X[:,2]<1.52)
def stats():
    P=np.einsum('vij,vj->vi',R,X)+t
    result=dict(neutral_negative=int(np.sum(np.einsum('fi,fi->f',normals(X),ref0)<-1e-10)),
                raised_negative=int(np.sum(np.einsum('fi,fi->f',normals(P),ref1)<-1e-10)))
    for label,(r,t_,rf) in zip(('70','40','100'),pose_models):
        pp=np.einsum('vij,vj->vi',r,X)+t_
        result['raised'+label]=int(np.sum(np.einsum('fi,fi->f',normals(pp),rf)<-1e-10))
    return result
before=stats()
for iteration in range(600 if '--multi' in a else 300):
    updates=np.zeros_like(X); counts=np.zeros(len(X))
    for model in [None]+pose_models:
        r,t_,nr=model if model is not None else (None,None,ref0)
        P=np.einsum('vij,vj->vi',r,X)+t_ if model is not None else X
        signed=np.einsum('fi,fi->f',normals(P),nr)
        desired=np.maximum(1e-9,area*.08)
        for fi in np.nonzero(signed<desired)[0]:
            ids=F[fi]; pa,pb,pc=P[ids]; n=nr[fi]
            grads=np.array([np.cross(pb-pc,n),np.cross(pc-pa,n),np.cross(pa-pb,n)])
            if model is not None:grads=np.einsum('vji,vj->vi',r[ids],grads)
            grads*=active[ids,None]
            denom=float(np.sum(grads*grads))
            if denom<1e-14:continue
            step=.7*(desired[fi]-signed[fi])/denom
            for vi,g in zip(ids,grads):updates[vi]+=step*g; counts[vi]+=1
    X+=updates/np.maximum(counts[:,None],1)
    delta=X-X0; lengths=np.linalg.norm(delta,axis=1)
    cap=.03 if '--multi' in a else .018
    X=X0+delta*np.minimum(1,cap/np.maximum(lengths,1e-12))[:,None]
    if not np.any(counts):break
after=stats(); delta=np.linalg.norm(X-X0,axis=1)
report=dict(before=before,after=after,iterations=iteration+1,max_displacement_m=float(delta.max()),moved_vertices=int(np.sum(delta>1e-7)))
posed_points=np.einsum('vij,vj->vi',R,X)+t
neg=np.einsum('fi,fi->f',normals(posed_points),ref1)<-1e-10
report['remaining']=[dict(object=owner[i],center=X[F[i]].mean(0).tolist(),vertices=F[i].tolist()) for i in np.nonzero(neg)[0]]
print('POSE_GUARD',json.dumps(report))
for i,ls in refs.items():
    if delta[i]<1e-8:continue
    for o,vi in ls:o.data.shape_keys.key_blocks['Поджарый'].data[vi].co=o.matrix_world.inverted()@Vector(X[i])
for pb in arm.pose.bones:pb.matrix_basis=Matrix.Identity(4)
bpy.context.view_layer.update()
bpy.ops.wm.save_as_mainfile(filepath=a[1])
with open(a[2],'w',encoding='utf-8') as f:json.dump(report,f,indent=2)
