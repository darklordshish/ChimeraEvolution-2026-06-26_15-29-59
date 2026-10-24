"""Local piecewise-planar chest sample. Posed static geometry, NOT a skinned delivery.
Split input triangles along a small authored control grid; keep the patch border fixed.
"""
import bpy,sys,json
import numpy as np
from pathlib import Path
ROOT=Path(__file__).resolve().parent; PREV=ROOT.parent/'morph-weights-2026-10-23'
sys.path.insert(0,str(PREV))
from check_roundtrip import read_pose,skin_matrices,vertex_matrix,worldpoint
bpy.ops.wm.open_mainfile(filepath=str(PREV/'corrected.blend'))
arm=next(o for o in bpy.data.objects if o.type=='ARMATURE'); skin=skin_matrices(arm,read_pose(PREV/'pose.json'))
B=[];P=[];F=[]
for o in bpy.data.objects:
    if o.type!='MESH' or '@' in o.name or o.name in ('голова','Чутьё'): continue
    offset=len(B); key=o.data.shape_keys.key_blocks['Волк']
    for v in o.data.vertices:
        B.append(worldpoint(o,v.co)); P.append((vertex_matrix(o,v,skin)@np.r_[worldpoint(o,key.data[v.index].co),1])[:3])
    F.extend([[offset+i for i in p.vertices] for p in o.data.polygons])
B=np.array(B);P=np.array(P);F=np.array(F)
norm=np.cross(B[F[:,1]]-B[F[:,0]],B[F[:,2]]-B[F[:,0]])
front=(norm[:,1]<-1e-9)&(B[F,1].mean(1)<0)
xs=np.array([-.12,-.06,0,.06,.12]); zs=np.array([1.26,1.31,1.37,1.44])

def bary(uv,t):
    return np.linalg.solve(np.vstack([t.T,np.ones(3)]),np.r_[uv,1])

def sample(x,z):
    hits=[]
    for f in F[front]:
        t=B[f][:,[0,2]]
        if x<t[:,0].min()-1e-7 or x>t[:,0].max()+1e-7 or z<t[:,1].min()-1e-7 or z>t[:,1].max()+1e-7: continue
        if abs(np.linalg.det(np.vstack([t.T,np.ones(3)])))<1e-10: continue
        w=bary((x,z),t)
        if w.min()>-1e-6: hits.append((float(w@B[f,1]),w@P[f]))
    if not hits: raise RuntimeError(f'Control point outside front skin: {x}, {z}')
    return min(hits,key=lambda h:h[0])[1]

uv=np.array([(x,z) for z in zs for x in xs]); control=np.array([sample(x,z) for x,z in uv])
# Explicit symmetric target: maintain sternum, broad left/right pectoral planes.
# Average mirrored samples to remove incidental asymmetry of the fitted source.
for row in range(len(zs)):
    for col in range(2):
        i=row*5+col;j=row*5+(4-col)
        avg=(control[i]*np.array([-1,1,1])+control[j])/2
        control[j]=avg; control[i]=avg*np.array([-1,1,1])
grid=[]
for row in range(3):
    for col in range(4):
        a=row*5+col;b=a+1;c=a+5;d=c+1
        # diagonals follow mirrored pectoral fans, not a single global slash direction.
        grid.extend([(a,b,c),(b,d,c)] if col<2 else [(a,b,d),(a,d,c)])

def split(poly,n,c):
    """Split interpolated [rest xyz, posed xyz] by an affine rest-x/z halfspace."""
    yes=[];no=[]
    if not len(poly):return yes,no
    for i,a in enumerate(poly):
        b=poly[(i+1)%len(poly)]; da=n@a[[0,2]]-c;db=n@b[[0,2]]-c
        (yes if da>=-1e-10 else no).append(a)
        if (da>1e-10 and db<-1e-10) or (da<-1e-10 and db>1e-10):
            p=a+(b-a)*(da/(da-db));yes.append(p);no.append(p)
    return yes,no

out=[];faces=[];vertexmap={};patch_faces=[];deltas=[];border=[]
def emit(poly,patch=False):
    if len(poly)<3:return
    ids=[]
    for v in poly:
        p=v[3:];key=tuple(np.round(p,8));idx=vertexmap.setdefault(key,len(out))
        if idx==len(out):out.append(p.tolist())
        ids.append(idx)
    for i in range(1,len(ids)-1):
        tri=[ids[0],ids[i],ids[i+1]]
        p=np.array([out[j] for j in tri])
        if np.linalg.norm(np.cross(p[1]-p[0],p[2]-p[0]))>1e-12:
            faces.append(tri);patch_faces.append(patch)

for fi,f in enumerate(F):
    poly=[np.r_[B[v],P[v]] for v in f]
    if not front[fi]:emit(poly);continue
    inside=poly
    for n,c in [(np.array([1,0]),xs[0]),(np.array([-1,0]),-xs[-1]),(np.array([0,1]),zs[0]),(np.array([0,-1]),-zs[-1])]:
        inside,outside=split(inside,n,c);emit(outside)
    if len(inside)<3:continue
    for g in grid:
        t=uv[list(g)];clipped=inside
        for i in range(3):
            a=t[i];b=t[(i+1)%3];e=b-a;n=np.array([-e[1],e[0]])
            clipped,_=split(clipped,n,n@a)
        updated=[]
        for v in clipped:
            u=v[[0,2]];w=bary(u,t);q=w@control[list(g)]
            margin=min(u[0]-xs[0],xs[-1]-u[0],u[1]-zs[0],zs[-1]-u[1])
            blend=np.clip(margin/0.025,0,1)
            p=v[3:]*(1-blend)+q*blend
            deltas.append(float(np.linalg.norm(p-v[3:])))
            if margin<1e-8:border.append(float(np.linalg.norm(p-v[3:])))
            updated.append(np.r_[v[:3],p])
        emit(updated,True)

assert deltas and max(border,default=0)<1e-7
assert np.isfinite(out).all()
report=dict(input_triangles=len(F),output_triangles=len(faces),patch_triangles=sum(patch_faces),
            max_displacement_mm=max(deltas)*1000,mean_displacement_mm=float(np.mean(deltas))*1000,
            max_border_displacement_mm=max(border,default=0)*1000,control_planes=len(grid),
            note='Static posed study; 25mm transition belt. Not a production retopology, no rig/keys transferred.')
(ROOT/'metrics.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
(ROOT/'controls.json').write_text(json.dumps(dict(uv=uv.tolist(),posed=control.tolist(),triangles=grid),indent=2),encoding='utf-8')
for label,verts,tris in [('before',P.tolist(),F.tolist()),('patch',out,faces)]:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    me=bpy.data.meshes.new('Body');me.from_pydata(verts,[],tris);me.update()
    ob=bpy.data.objects.new('Body',me);bpy.context.scene.collection.objects.link(ob);ob.color=(.48,.50,.53,1)
    bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/(label+'.blend')))
print(json.dumps(report))
