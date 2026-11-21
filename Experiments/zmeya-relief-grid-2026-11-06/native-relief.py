"""Static budget probe: displace existing native vertices, zero added faces."""
import bpy,math,json
from pathlib import Path
from mathutils import Vector
from mathutils.kdtree import KDTree
P=Path(__file__).parent
bpy.ops.wm.open_mainfile(filepath=str(P/'optimized-preview.blend'))
body=[o for o in bpy.data.objects if o.type=='MESH' and '@' not in o.name and o.name!='Cube']
for o in bpy.data.objects:
    if o.type=='MESH':o.hide_render=o not in body
    if o.type=='MESH' and o.data.shape_keys:
        for k in o.data.shape_keys.key_blocks:k.value=0
bpy.context.view_layer.update()
verts=[];refs=[];faces=[]
for o in body:
    off=len(verts);verts += [o.matrix_world@v.co for v in o.data.vertices];refs += [(o,v.index) for v in o.data.vertices]
    o.data.calc_loop_triangles();faces += [tuple(off+k for k in t.vertices) for t in o.data.loop_triangles]
kd=KDTree(len(verts))
for i,p in enumerate(verts):kd.insert(p,i)
kd.balance();groups=[min(j for _,j,_ in kd.find_range(p,1e-5)) for p in verts];normals={}
for f in faces:
    a,b,c=[verts[k] for k in f];n=(b-a).cross(c-a)
    for k in f:normals[groups[k]]=normals.get(groups[k],Vector())+n
S=math.sin(.62);C=math.cos(.62);step=.065
heightmax=0;changed=0
for i,(o,k) in enumerate(refs):
    p=verts[i];n=normals[groups[i]].normalized()
    u=(S*p.x+C*p.z)/step;v=(-S*p.x+C*p.z)/step
    u-=math.floor(u);v-=math.floor(v)
    # Same phase; a simple peaked cell sampled only at native vertices.
    tent=max(0,min(u,1-u,v,1-v)*2)
    fade=min(1,max(0,(p.z-1.06)/.04),max(0,(1.66-p.z)/.04))*min(1,max(0,(p.y+.01)/.06))
    h=.0125*tent*fade;heightmax=max(heightmax,h)
    delta=o.matrix_world.to_3x3().inverted()@(n*h)
    if h>1e-8:changed+=1
    if o.data.shape_keys:
        for key in o.data.shape_keys.key_blocks:key.data[k].co+=delta
    else:o.data.vertices[k].co+=delta
base=(.58,.55,.50)
for o in body:
    o.data.materials.clear()
    for t in [1,.74]:
        mat=bpy.data.materials.new('native-tone');mat.diffuse_color=(*(c*t for c in base),1);o.data.materials.append(mat)
    for f in o.data.polygons:
        p=o.matrix_world@f.center
        # Face centers recomputed below, prior center is valid for phase only.
        p=sum((o.matrix_world@o.data.vertices[j].co for j in f.vertices),Vector())/len(f.vertices)
        active=1.06<p.z<1.66 and p.y>-.01
        f.material_index=(math.floor((S*p.x+C*p.z)/step)+math.floor((-S*p.x+C*p.z)/step))%2 if active else 0
        f.use_smooth=False
    o.data.update()
sc=bpy.context.scene;sc.render.resolution_x=800;sc.render.resolution_y=720
bpy.context.view_layer.update();sc.render.filepath=str(P/'renders/native-relief.png');bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=str(P/'native-relief.blend'))
report=dict(total_body_triangles=len(faces),added_triangles=0,changed_vertices=changed,max_height_mm=heightmax*1000,limitations=['Static artistic probe; no movement acceptance.','Native topology cannot represent each rhombus keel accurately.','Colors sampled per existing face; pattern detail is lost.'])
(P/'native-relief.json').write_text(json.dumps(report,indent=2),encoding='utf8');print(report)
