"""Budget-first static probe: twelve four-face cells, native body untouched."""
import bpy,math,json
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
P=Path(__file__).parent
bpy.ops.wm.open_mainfile(filepath=str(P/'optimized-preview.blend'))
body=[o for o in bpy.data.objects if o.type=='MESH' and '@' not in o.name and o.name!='Cube']
for o in bpy.data.objects:
    if o.type=='MESH':o.hide_render=o not in body
    if o.type=='MESH' and o.data.shape_keys:
        for k in o.data.shape_keys.key_blocks:k.value=0
bpy.context.view_layer.update()
V=[];F=[]
for o in body:
    off=len(V);V += [o.matrix_world@v.co for v in o.data.vertices];o.data.calc_loop_triangles()
    F += [tuple(off+k for k in t.vertices) for t in o.data.loop_triangles]
tree=BVHTree.FromPolygons(V,F,all_triangles=True)
S=math.sin(.62);C=math.cos(.62);step=.065
def sample(u,v):
    x=(u-v)*step/(2*S);z=(u+v)*step/(2*C)
    p,n,fi,d=tree.ray_cast(Vector((x,2,z)),Vector((0,-1,0)),4)
    return p,n
candidates=[]
for i in range(8,24):
    for j in range(8,24):
        p,n=sample(i+.5,j+.5)
        if p is None or not (1.13<p.z<1.59 and abs(p.x)<.185 and n.y>.55):continue
        ring=[]
        for u,v in [(i+.09,j+.09),(i+.91,j+.09),(i+.91,j+.91),(i+.09,j+.91)]:
            q,qn=sample(u,v)
            if q is None or qn.y<.4:break
            ring.append(q+qn*.0008)
        if len(ring)==4:candidates.append((i,j,p,n,ring))
assert len(candidates)>=12,len(candidates)
chosen=[min(candidates,key=lambda c:abs(c[2].x)+abs(c[2].z-1.4))]
while len(chosen)<12:
    nxt=max((c for c in candidates if c not in chosen),key=lambda c:min((c[2]-a[2]).length for a in chosen));chosen.append(nxt)
verts=[];faces=[];tones=[]
for i,j,p,n,ring in chosen:
    off=len(verts);verts += ring+[p+n*.012]
    for k in range(4):
        f=(off+k,off+(k+1)%4,off+4)
        a,b,c=[verts[q] for q in f]
        if (b-a).cross(c-a).dot(n)<0:f=tuple(reversed(f))
        faces.append(f);tones.append((i+j)%2)
me=bpy.data.meshes.new('12 килей, 48 треугольников');me.from_pydata(verts,[],faces);me.update()
ob=bpy.data.objects.new('Шкура.минимальная-проба@Змея',me);bpy.context.scene.collection.objects.link(ob)
base=(.58,.55,.50)
for factor in [1,.74]:
    mat=bpy.data.materials.new('тон-киля');mat.diffuse_color=(*(c*factor for c in base),1);me.materials.append(mat)
for k,f in enumerate(me.polygons):f.material_index=tones[k];f.use_smooth=False
for o in body:
    for k,mat in enumerate(o.data.materials):
        if mat:
            mat=mat.copy();mat.diffuse_color=(*base,1);o.data.materials[k]=mat
sc=bpy.context.scene;sc.render.resolution_x=800;sc.render.resolution_y=720
sc.render.filepath=str(P/'renders/sparse-detail.png');bpy.ops.render.render(write_still=True)
sc.camera.data.ortho_scale=1.58
sc.render.filepath=str(P/'renders/sparse-body.png');bpy.ops.render.render(write_still=True)
report=dict(native_body_triangles=len(F),keels=12,added_triangles=len(faces),total_triangles=len(F)+len(faces),grid_step=step,grid_angle=.62,cells=[(a[0],a[1]) for a in chosen],peak_offset_mm=12,limitations=['Static shape probe, unrigged additions.','Native body is unchanged; full painted lattice is not reproduced in this preview.','Four-face caps approximate surface; contact and movement not accepted.','2068 exceeds strict 2000; native source itself is 2020.'])
(P/'sparse.json').write_text(json.dumps(report,indent=2),encoding='utf8')
bpy.ops.wm.save_as_mainfile(filepath=str(P/'sparse.blend'));print(report)
