"""Bounded abdominal patch replacement, preserving outside mesh and boundary.

First fixed-density spike could not express six broad planes. This candidate
explicitly changes local topology; other keys are barycentrically resampled,
not claimed identical or accepted for production.
"""
import bpy,bmesh,sys,json,numpy as np
from pathlib import Path
from collections import Counter,defaultdict
from mathutils import Vector
from mathutils.geometry import delaunay_2d_cdt
root=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(root.parent/'shoulder-local-2026-10-31/candidate.blend'))
o=bpy.data.objects['Сердце'];mesh=o.data;keys=mesh.shape_keys.key_blocks;keynames=[k.name for k in keys]
keyarrays={k.name:np.array([p.co[:] for p in k.data]) for k in keys}
X=np.array([list(o.matrix_world@p.co) for p in keys['Поджарый'].data]);P=X[:,[0,2]]
oldfaces=[tuple(p.vertices) for p in mesh.polygons];materials=[p.material_index for p in mesh.polygons]
selected=[i for i,f in enumerate(oldfaces) if all(abs(X[v,0])<.105 and X[v,1]<-.08 and 1.115<X[v,2]<1.32 for v in f)]
edges=Counter(tuple(sorted((f[j],f[(j+1)%len(f)]))) for i in selected for f in [oldfaces[i]] for j in range(len(f)))
boundary=[e for e,n in edges.items() if n==1];adj=defaultdict(list)
for a,b in boundary:adj[a].append(b);adj[b].append(a)
assert all(len(v)==2 for v in adj.values())
loop=[min(adj)];previous=None
while True:
    choices=[i for i in adj[loop[-1]] if i!=previous];nxt=choices[0]
    if nxt==loop[0]:break
    previous=loop[-1];loop.append(nxt)
assert len(loop)==len(adj),'Patch has disconnected boundaries'
tris=np.array([oldfaces[i] for i in selected]);assert tris.shape[1]==3
def bary(p,tri):
    a,b,c=P[tri];m=np.column_stack((b-a,c-a))
    if abs(np.linalg.det(m))<1e-12:return None
    uv=np.linalg.solve(m,p-a);return np.array([1-uv.sum(),uv[0],uv[1]])
def sample(p):
    for tri in tris:
        w=bary(p,tri)
        if w is not None and w.min()>=-1e-6:return tri,w
    return None
coords=[Vector(P[i]) for i in loop];samples=[None]*len(loop);depths=[None]*len(loop)
constraints=[(i,(i+1)%len(loop)) for i in range(len(loop))]
for row,(cz,w,h) in enumerate([(1.274,.025,.017),(1.217,.024,.017),(1.163,.023,.016)]):
    for side in (-1,1):
        cx=side*.038
        for ring in (False,True):
            ww=w+(.009 if ring else 0);hh=h+(.007 if ring else 0);cut=.007
            polygon=[(-ww+cut,-hh),(ww-cut,-hh),(ww,-hh+cut),(ww,hh-cut),(ww-cut,hh),(-ww+cut,hh),(-ww,hh-cut),(-ww,-hh+cut)]
            indices=[]
            for dx,dz in polygon:
                x,z=cx+dx,cz+dz;samp=sample(np.array([x,z]))
                if samp is None:continue
                idx=len(coords);coords.append(Vector((x,z)));samples.append(samp)
                # Sloping bevel into the existing abdominal surface, not a grid.
                old_y=float(np.einsum('i,i->',samp[1],X[samp[0],1]))
                yy=(.35*old_y+.65*(-.1265)) if ring else -.1300+.050*dz+.020*side*dx
                depths.append(yy);indices.append(idx)
            if len(indices)==8:constraints.extend(zip(indices,indices[1:]+indices[:1]))
        samp=sample(np.array([cx,cz]))
        if samp is not None:
            coords.append(Vector((cx,cz)));samples.append(samp);depths.append(-.1335)
verts,_,faces,orig,_,_=delaunay_2d_cdt(coords,constraints,[list(range(len(loop)))],1,1e-6,True)
outside=[f for i,f in enumerate(oldfaces) if i not in selected]
keep=sorted(set(v for f in outside for v in f)|set(loop));mapping={old:i for i,old in enumerate(keep)}
newkeys={name:[keyarrays[name][i].copy() for i in keep] for name in keynames}
gn={g.index:g.name for g in o.vertex_groups}
weights=[{gn[g.group]:g.weight for g in v.groups if g.weight>0} for v in mesh.vertices]
newweights=[weights[i].copy() for i in keep];outindices=[]
inv=o.matrix_world.inverted()
for j,p in enumerate(verts):
    original=next((i for i in orig[j] if i<len(loop)),None)
    if original is not None:outindices.append(mapping[loop[original]]);continue
    s=sample(np.array(p));assert s is not None,(j,list(p))
    tri,w=s;idx=len(newweights);outindices.append(idx)
    for name in keynames:newkeys[name].append(np.einsum('i,ij->j',w,keyarrays[name][tri]))
    input_id=next((i for i in orig[j] if i>=len(loop)),None)
    yy=depths[input_id] if input_id is not None else float(np.einsum('i,i->',w,X[tri,1]))
    wp=Vector((p.x,yy,p.y));newkeys['Поджарый'][-1]=np.array(inv@wp)
    ws=defaultdict(float)
    for v,amount in zip(tri,w):
        for name,value in weights[v].items():ws[name]+=float(amount)*value
    # Same maximum of four influences as the source; localized to new vertices.
    ws=dict(sorted(((n,v) for n,v in ws.items() if v>1e-8),key=lambda item:-item[1])[:4]);total=sum(ws.values())
    newweights.append({n:v/total for n,v in ws.items()})
newfaces=[tuple(mapping[v] for v in f) for f in outside]
newmaterials=[materials[i] for i in range(len(oldfaces)) if i not in selected]
for f in faces:
    ids=[outindices[i] for i in f];ps=np.array([newkeys['Поджарый'][i] for i in ids])
    n=np.cross(ps[1]-ps[0],ps[2]-ps[0])
    if n[1]>0:ids.reverse()
    newfaces.append(tuple(ids));newmaterials.append(0)
oldmat=list(mesh.materials);nm=bpy.data.meshes.new(mesh.name+'·Пресс');nm.from_pydata(newkeys[keynames[0]],[],newfaces);nm.update()
o.data=nm
for mat in oldmat:nm.materials.append(mat)
for f,mat in zip(nm.polygons,newmaterials):f.material_index=mat;f.use_smooth=False
for name in keynames:
    k=o.shape_key_add(name=name,from_mix=False)
    for p,co in zip(k.data,newkeys[name]):p.co=co
    k.value=0
for group in list(o.vertex_groups):o.vertex_groups.remove(group)
for ws in newweights:
    for name in ws:
        if name not in o.vertex_groups:o.vertex_groups.new(name=name)
for i,ws in enumerate(newweights):
    for name,value in ws.items():o.vertex_groups[name].add([i],value,'REPLACE')
# A 2D CDT can create a skinny triangle with opposed 3D normals after relief.
# Repair its diagonal, keeping all vertex positions and outside faces fixed.
bm=bmesh.new();bm.from_mesh(o.data);bm.faces.ensure_lookup_table()
def negatives(fs,bm_):
    sk=bm_.verts.layers.shape.get('Поджарый');bad=0
    for f in fs:
        if len(f.verts)!=3:continue
        a=np.array([v.co[:] for v in f.verts]);b=np.array([v[sk][:] for v in f.verts])
        bad+=int(np.cross(a[1]-a[0],a[2]-a[0])@np.cross(b[1]-b[0],b[2]-b[0])<0)
    return bad
for _ in range(3):
    changed=False
    for e in list(bm.edges):
        if not e.is_valid or len(e.link_faces)!=2 or any(f.index<len(outside) for f in e.link_faces):continue
        oldbad=negatives(e.link_faces,bm)
        if not oldbad:continue
        bm.edges.index_update();trial=bm.copy();trial.edges.ensure_lookup_table();res=bmesh.ops.rotate_edges(trial,edges=[trial.edges[e.index]],use_ccw=False)
        accept=bool(res['edges']) and negatives(res['edges'][0].link_faces,trial)<oldbad
        trial.free()
        if accept:bmesh.ops.rotate_edges(bm,edges=[e],use_ccw=False);changed=True
    if not changed:break
assert negatives(bm.faces,bm)==0,'Unresolved opposite normals'
bm.to_mesh(o.data);bm.free();o.data.update()
bpy.ops.wm.save_as_mainfile(filepath=str(root/'candidate.blend'))
report=dict(source='shoulder-local-2026-10-31/candidate.blend',source_patch_faces=len(selected),boundary_vertices=len(loop),old_heart_vertices=len(X),new_heart_vertices=len(nm.vertices),new_patch_faces=len(faces),heart_triangles_before=sum(len(f)-2 for f in oldfaces),heart_triangles_after=sum(len(f)-2 for f in newfaces),all_keys=list(keynames),note='Local topology changed. Outside vertices/keys/weights preserved. Interior keys interpolated, need revalidation. Previous fixed-count criterion not met; explicit local-density alternative.')
(root/'retopo.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8');print('RETOPO',report)
