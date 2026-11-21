import bpy,math,sys
from pathlib import Path
from mathutils import Vector
P=Path(__file__).parent; O=P/'renders'; O.mkdir(exist_ok=True)
pattern='--pattern' in sys.argv
bpy.ops.wm.open_mainfile(filepath=str(P.parent/'shkura-ton-2026-11-06/candidate.blend' if pattern else P/'candidate.blend'))
if pattern:O=P/'pattern-renders';O.mkdir(exist_ok=True)
sc=bpy.context.scene;sc.render.engine='BLENDER_WORKBENCH';w=sc.display.shading
w.light='STUDIO';w.studio_light='paint.sl';w.color_type='MATERIAL'
w.show_shadows=False;w.show_cavity=False;w.show_specular_highlight=False;w.show_backface_culling=True;w.background_type='WORLD'
sc.world=sc.world or bpy.data.worlds.new('World');sc.world.color=(.82,.83,.84);sc.view_settings.view_transform='Standard'
base=(.58,.55,.50)
for o in bpy.data.objects:
    if o.type!='MESH':continue
    plate=(o.name.startswith('Шкура.тон') and o.name.endswith('@Змея')) if pattern else o.name=='Шкура.щитки-проба@Змея'
    o.hide_render='@' in o.name and not plate
    if pattern and o.name in ['хребет','шея','Сердце','Руки']:o.hide_render=True
    for i,mat in enumerate(o.data.materials):
        if mat:
            m=mat.copy();o.data.materials[i]=m;factor=mat.diffuse_color[0] if plate else 1.
            m.diffuse_color=(*(c*factor for c in base),1)
    for p in o.data.polygons:p.use_smooth=False
    if o.data.shape_keys:
        for k in o.data.shape_keys.key_blocks:k.value=0
cam=bpy.data.objects.new('Camera',bpy.data.cameras.new('Camera'));sc.collection.objects.link(cam);sc.camera=cam
cam.data.type='ORTHO';cam.data.clip_end=20;sc.render.image_settings.file_format='PNG'
for az,label in [(180,'back'),(145,'rear34'),(90,'side'),(35,'front34')]:
    a=math.radians(az);d=Vector((math.sin(a),-math.cos(a),.05)).normalized();target=Vector((0,0,1.30))
    cam.location=target+d*4;cam.rotation_euler=(-d).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=1.28
    sc.render.resolution_x=520;sc.render.resolution_y=720;sc.render.resolution_percentage=100
    sc.render.filepath=str(O/(label+'.png'));bpy.ops.render.render(write_still=True)
    if az==145:
        target=Vector((0,.03,1.38));cam.location=target+d*4;cam.rotation_euler=(-d).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=.60
        sc.render.resolution_x=sc.render.resolution_y=800;sc.render.filepath=str(O/'detail.png');bpy.ops.render.render(write_still=True)
