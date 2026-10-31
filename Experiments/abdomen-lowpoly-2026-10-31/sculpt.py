"""Approved six-pack placement: bounded shape-key-only relief, no added faces."""
import bpy,bmesh,numpy as np,json
from pathlib import Path
from mathutils import Vector
root=Path(__file__).resolve().parent
source=root.parent/'shoulder-local-2026-10-31/candidate.blend'
bpy.ops.wm.open_mainfile(filepath=str(source))
def trapezoid(x,center,inner,outer):
    return max(0.,min(1.,(outer-abs(x-center))/(outer-inner)))
log=[];errors=[];flips=[]
for o in bpy.data.objects:
    if o.type!='MESH' or '@' in o.name or not o.data.shape_keys:continue
    k=o.data.shape_keys.key_blocks.get('Поджарый')
    if not k:continue
    old=np.array([p.co[:] for p in k.data]);moved=[]
    for i,v in enumerate(k.data):
        p=o.matrix_world@v.co;x,y,z=p
        if not (abs(x)<.080 and y<-.085 and 1.125<z<1.315):continue
        # Reposition the existing lower apex into its approved row; no added density.
        if .027<abs(x)<.045 and 1.163<z<1.179:
            p.z=1.158
        side=trapezoid(abs(x),.038,.023,.039)
        rows=max(trapezoid(p.z,c,.015,.029) for c in (1.275,1.210,1.155))
        envelope=trapezoid(z,1.220,.075,.095)*trapezoid(abs(x),.035,.030,.045)
        # Small front-facing plateau with sloped walls, never separate armor pieces.
        target=-.1265-.009*side*rows
        delta=max(-.010,min(.010,(target-y)*envelope))
        if abs(delta)<1e-7:continue
        p.y+=delta;v.co=o.matrix_world.inverted()@p;moved.append(dict(vertex=i,delta_y_m=delta,point=[x,y,z]))
    new=np.array([p.co[:] for p in k.data]);bad=[]
    for f in o.data.polygons:
        ids=list(f.vertices)
        for j in range(1,len(ids)-1):
            a,b,c=[ids[t] for t in (0,j,j+1)]
            n0=np.cross(old[b]-old[a],old[c]-old[a]);n1=np.cross(new[b]-new[a],new[c]-new[a])
            if n0@n1< -1e-12:bad.append(f.index)
    if bad:errors.append(dict(object=o.name,reversed_faces=bad))
    if moved:log.append(dict(object=o.name,moved=moved))
    if o.name=='Сердце':
        bm=bmesh.new();bm.from_mesh(o.data);bm.verts.ensure_lookup_table()
        sk=bm.verts.layers.shape.get('Поджарый');crease=bm.edges.layers.int.get('перелом_линия')
        def points(face,layer):return np.array([list(o.matrix_world@(v[layer] if layer else v.co)) for v in face.verts])
        def target(x,z):
            return -.1265-.009*trapezoid(abs(x),.038,.023,.039)*max(trapezoid(z,c,.015,.029) for c in (1.275,1.210,1.155))
        def score(faces,layer):
            result=0
            for f in faces:
                ps=points(f,layer);n=np.cross(ps[1]-ps[0],ps[2]-ps[0]);area=np.linalg.norm(n)
                c=ps.mean(0);q=2*np.sqrt(3)*area/max(sum(np.sum((ps[(i+1)%3]-ps[i])**2) for i in range(3)),1e-10)
                result+=area*((target(c[0],c[2])-c[1])**2+2e-7/max(q,.02))
            return result
        for _ in range(2):
            for e in list(bm.edges):
                if not e.is_valid or len(e.link_faces)!=2 or any(len(f.verts)!=3 for f in e.link_faces):continue
                if crease and e[crease]:continue
                vs=set(v for f in e.link_faces for v in f.verts)
                ps=np.array([list(o.matrix_world@v[sk]) for v in vs])
                if not np.all((abs(ps[:,0])<.085)&(ps[:,1]<-.09)&(ps[:,2]>1.125)&(ps[:,2]<1.310)):continue
                oldscore=score(e.link_faces,sk)
                ref=sum((f.normal for f in e.link_faces),Vector())
                oldn=sum((Vector(np.cross(points(f,sk)[1]-points(f,sk)[0],points(f,sk)[2]-points(f,sk)[0])) for f in e.link_faces),Vector())
                bm.edges.index_update();trial=bm.copy();trial.edges.ensure_lookup_table();res=bmesh.ops.rotate_edges(trial,edges=[trial.edges[e.index]],use_ccw=False)
                accept=False
                if res['edges']:
                    fs=res['edges'][0].link_faces;skt=trial.verts.layers.shape.get('Поджарый')
                    accept=(len(fs)==2 and score(fs,skt)<oldscore*.95 and all(Vector(np.cross(points(f,None)[1]-points(f,None)[0],points(f,None)[2]-points(f,None)[0])).dot(ref)>0 and Vector(np.cross(points(f,skt)[1]-points(f,skt)[0],points(f,skt)[2]-points(f,skt)[0])).dot(oldn)>0 for f in fs))
                trial.free()
                if accept:
                    flips.append([v.index for v in e.verts]);bmesh.ops.rotate_edges(bm,edges=[e],use_ccw=False)
        bm.to_mesh(o.data);bm.free();o.data.update()
    for key in o.data.shape_keys.key_blocks:key.value=0
assert not errors,errors
bpy.ops.wm.save_as_mainfile(filepath=str(root/'candidate.blend'))
(root/'sculpt.json').write_text(json.dumps(dict(source=str(source),rows_z_m=[1.275,1.210,1.155],nominal_relief_m=.009,depth_displacement_cap_m=.010,lower_apex_z=1.158,changes=log,edge_flips=flips,errors=errors,only_key='Поджарый',note='Placement approved; relief amount is a new visual candidate. Local diagonals rotated; weights, rest bones, base and other keys unchanged.'),ensure_ascii=False,indent=2),encoding='utf-8')
print('ABDOMEN',[(r['object'],len(r['moved'])) for r in log])
