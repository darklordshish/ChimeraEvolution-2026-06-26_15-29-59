"""Standalone contiguous skin sample, 16 cells / 96 triangles total."""
import bpy,math,json
from pathlib import Path
from mathutils import Vector
P=Path(__file__).parent
bpy.ops.wm.read_factory_settings(use_empty=True)
S=math.sin(.62);C=math.cos(.62);step=.065
points=[];faces=[];indices={};heights=[]
def vertex(u,v,h):
    key=(u,v,h)
    if key in indices:return indices[key]
    x=(u-v)*step/(2*S);z=(u+v-4)*step/(2*C)
    y=-x*x/.7
    n=Vector((2*x/.7,1,0)).normalized()
    p=Vector((x,y,z))+n*h
    indices[key]=len(points);points.append(p);heights.append(h);return indices[key]
for i in range(4):
    for j in range(4):
        a=vertex(i,j,0);b=vertex(i+1,j,0);c=vertex(i+1,j+1,0);d=vertex(i,j+1,0)
        # Broad, long, low keel. Shared outer corners; no overlapping plates.
        r0=vertex(i+.18,j+.18,.0025);r1=vertex(i+.82,j+.82,.0025)
        for f in [(a,b,r0),(b,r1,r0),(b,c,r1),(c,d,r1),(d,r0,r1),(d,a,r0)]:
            q,w,e=[points[k] for k in f]
            if (w-q).cross(e-q).y<0:f=tuple(reversed(f))
            faces.append(f)
me=bpy.data.meshes.new('непрерывная-кожа');me.from_pydata(points,[],faces);me.update()
ob=bpy.data.objects.new('Участок кожи · 16 ячеек',me);bpy.context.collection.objects.link(ob)
mat=bpy.data.materials.new('Кожа без контуров');mat.diffuse_color=(.43,.42,.36,1);me.materials.append(mat)
for f in me.polygons:f.use_smooth=False
sc=bpy.context.scene;sc.render.engine='BLENDER_WORKBENCH';sh=sc.display.shading
sh.light='STUDIO';sh.studio_light='paint.sl';sh.color_type='MATERIAL';sh.show_shadows=False;sh.show_cavity=False;sh.show_specular_highlight=False;sh.show_backface_culling=False;sh.background_type='WORLD'
sc.world=bpy.data.worlds.new('Фон');sc.world.color=(.82,.83,.84);sc.view_settings.view_transform='Standard'
sc.render.resolution_x=1000;sc.render.resolution_y=820;sc.render.resolution_percentage=100;sc.render.image_settings.file_format='PNG'
cam=bpy.data.objects.new('Camera',bpy.data.cameras.new('Camera'));bpy.context.collection.objects.link(cam);sc.camera=cam;cam.data.type='ORTHO';cam.data.ortho_scale=.58
def render(label,pos):
    cam.location=pos;cam.rotation_euler=(-cam.location).to_track_quat('-Z','Y').to_euler()
    sc.render.filepath=str(P/'renders'/f'patch-{label}.png');bpy.ops.render.render(write_still=True)
render('oblique',Vector((.34,1,.30)))
render('front',Vector((0,1,0)))
assert len(faces)==96 and len(points)==57
edges={}
for f in faces:
    for a,b in zip(f,f[1:]+f[:1]):
        edge=tuple(sorted((a,b)));edges[edge]=edges.get(edge,0)+1
assert max(edges.values())==2
report=dict(cells=16,vertices=len(points),triangles=len(faces),height_mm=2.5,grid_step_m=step,angle_rad=.62,boundary_edges=sum(n==1 for n in edges.values()),internal_edges=sum(n==2 for n in edges.values()),nonmanifold_edges=sum(n>2 for n in edges.values()),standalone_sample=True,limitations=['Not attached to body or rig.','96 triangles are this patch only, not the whole body budget.','No overlap lips or ground truth biological measurements.'])
(P/'patch.json').write_text(json.dumps(report,indent=2),encoding='utf8')
bpy.ops.wm.save_as_mainfile(filepath=str(P/'continuous-patch.blend'));print(report)
