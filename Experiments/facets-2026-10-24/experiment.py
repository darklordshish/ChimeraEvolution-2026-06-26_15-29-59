"""Controlled chest-plane experiment in posed space, with bounded displacements.
All variants use the same final weights, template, and endpoint pose.
"""
import bpy,sys,json
import numpy as np
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parent; PREV=ROOT.parent/'morph-weights-2026-10-23'
sys.path.insert(0,str(PREV))
from check_roundtrip import skin_matrices,read_pose,vertex_matrix,worldpoint

def collect(path):
    bpy.ops.wm.open_mainfile(filepath=str(path))
    arm=next(o for o in bpy.data.objects if o.type=='ARMATURE')
    skin=skin_matrices(arm,read_pose(PREV/'pose.json'))
    ids={}; base=[]; pos=[]; faces=[]; refs={}; mats={}
    for o in bpy.data.objects:
        if o.type!='MESH' or '@' in o.name: continue
        kb=o.data.shape_keys.key_blocks['Волк']; vi=[]
        for v in o.data.vertices:
            b=worldpoint(o,v.co); k=tuple(np.round(b,5))
            idx=ids.setdefault(k,len(ids)); m=vertex_matrix(o,v,skin)
            p=(m@np.r_[worldpoint(o,kb.data[v.index].co),1])[:3]
            if idx==len(base): base.append(b); pos.append(p)
            refs[(o.name,v.index)]=idx; mats[(o.name,v.index)]=m; vi.append(idx)
        for f in o.data.polygons:
            # All supplied faces are triangles; retain topology.
            assert len(f.vertices)==3
            faces.append([vi[i] for i in f.vertices])
    return np.array(base),np.array(pos),np.array(faces),refs,mats

B,P,F,refs,mats=collect(PREV/'corrected.blend')
_,OLD,_,_,_=collect(ROOT/'old-full.blend')
collect(PREV/'corrected.blend')
# Anatomical bands are an experimental manual layout in K2 rest coordinates.
# No inference from skinning weights; no merging across region labels.
active=(B[:,2]>1.00)&(B[:,2]<1.48)&(B[:,1]<0.025)&(np.abs(B[:,0])<0.24)
fc=B[F].mean(1); labels=[]
for x,y,z in fc:
    side='L' if x>0 else 'R'; ax=abs(x)
    if ax>0.145: region='ribs' if z>1.21 else 'oblique'
    elif z>1.27+0.4*ax: region='chest-upper' if z>1.38 else 'chest-lower'
    elif ax>0.08: region='rib-to-waist'
    else: region='abdomen-upper' if z>1.13 else 'abdomen-lower'
    labels.append(side+'/'+region)
eligible=np.all(active[F],axis=1)
# Lock transition perimeter, to avoid moving the seam between edited/unedited surface.
fixed=~active.copy()
for f in F[~eligible]: fixed[f]=True
# Hold sampled front/side contour extrema within horizontal posed-height bands.
for z in np.arange(P[active,2].min(),P[active,2].max()+0.02,0.02):
    ids=np.where(active&(abs(P[:,2]-z)<0.015))[0]
    if len(ids)<3: continue
    for axis in (0,1):
        val=P[ids,axis]; fixed[ids[(val<val.min()+0.003)|(val>val.max()-0.003)]]=True
norm=np.cross(P[F[:,1]]-P[F[:,0]],P[F[:,2]]-P[F[:,0]])
area=np.linalg.norm(norm,axis=1)/2
planes={}; groups={}
for label in sorted(set(labels)):
    fi=np.array([i for i,l in enumerate(labels) if l==label and eligible[i]])
    if len(fi)<4: continue
    verts=P[F[fi]].reshape(-1,3); w=np.repeat(area[fi],3); w/=w.sum()
    center=(verts*w[:,None]).sum(0)
    cov=(verts-center).T@((verts-center)*w[:,None])
    n=np.linalg.eigh(cov)[1][:,0]
    planes[label]=(n,center); groups[label]=fi
vf=[set() for _ in P]
for label,fi in groups.items():
    for v in np.unique(F[fi]): vf[v].add(label)
Q=P.copy()
for v in np.where(~fixed)[0]:
    labs=sorted(vf[v])
    if not labs: continue
    normals=np.array([planes[l][0] for l in labs])
    d=np.array([np.dot(*planes[l]) for l in labs])
    # Intersection of all incident region planes, with a positional regularizer.
    q=np.linalg.solve(normals.T@normals+0.06*np.eye(3),normals.T@d+0.06*P[v])
    delta=q-P[v]; delta*=min(1,0.008/max(np.linalg.norm(delta),1e-12))
    Q[v]+=delta
# Reject movements that turn a triangle against its INITIAL POSED normal, not the human rest normal.
# This is a local step guard, NOT a self-intersection detector.
for _ in range(12):
    nn=np.cross(Q[F[:,1]]-Q[F[:,0]],Q[F[:,2]]-Q[F[:,0]])
    bad=np.where((np.einsum('ij,ij->i',nn,norm)<=0)&(area>1e-10))[0]
    if not len(bad): break
    vv=np.unique(F[bad]); Q[vv]=(Q[vv]+P[vv])/2

def metrics(points):
    displacement=np.linalg.norm(points-P,axis=1)
    nn=np.cross(points[F[:,1]]-points[F[:,0]],points[F[:,2]]-points[F[:,0]])
    residual=[]
    for l,fi in groups.items():
        n,c=planes[l]; residual.extend(abs((points[F[fi]].reshape(-1,3)-c)@n))
    return dict(max_mm=float(displacement.max()*1000),mean_active_mm=float(displacement[active].mean()*1000),
                moved_vertices=int((displacement>1e-7).sum()),fixed_max_mm=float(displacement[fixed].max()*1000),
                initial_plane_mean_distance_mm=float(np.mean(residual)*1000),
                reversed_relative_to_posed_input=int(((np.einsum('ij,ij->i',nn,norm)<=0)&(area>1e-10)).sum()))

# Fair locality: use the OLD algorithm's result only on the same movable vertices.
oldlocal=P.copy(); oldlocal[~fixed]=OLD[~fixed]
report={'input':metrics(P),'old_local':metrics(oldlocal),'bounded_planes':metrics(Q),
        'regions':len(groups),'active_vertices':int(active.sum()),'movable_vertices':int((~fixed).sum())}
(ROOT/'metrics.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
assert report['bounded_planes']['max_mm']<=8.001
assert report['bounded_planes']['fixed_max_mm']<0.001
assert report['bounded_planes']['reversed_relative_to_posed_input']==0
assert report['bounded_planes']['initial_plane_mean_distance_mm']<report['input']['initial_plane_mean_distance_mm']

for variant,points in [('input',P),('old',oldlocal),('planes',Q)]:
    # Reload each time to guarantee identical weights and other keys.
    collect(PREV/'corrected.blend')
    for (name,vi),idx in refs.items():
        o=bpy.data.objects[name]; rest=np.linalg.solve(mats[(name,vi)],np.r_[points[idx],1])[:3]
        o.data.shape_keys.key_blocks['Волк'].data[vi].co=o.matrix_world.inverted()@Vector(rest)
    bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/(variant+'.blend')))
print(json.dumps(report))
