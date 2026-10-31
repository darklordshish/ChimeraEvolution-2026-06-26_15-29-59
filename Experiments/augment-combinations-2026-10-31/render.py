import bpy,bmesh,json,math
from pathlib import Path
from mathutils import Vector
root=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(root/'combinations.blend'))
cases=[('00-base',[]),('01-wolf-hands-ears',['Руки@Волк','Чутьё@Волк']),('02-wolf-three',['Руки@Волк','Чутьё@Волк','Шкура@Волк']),('03-hedgehog',['Шкура@Ёж','Игломёт@Ёж']),('04-wolf-hedgehog',['Руки@Волк','Чутьё@Волк','Шкура@Ёж']),('05-mixed-stress',['Руки@Волк','Шкура@Ёж','Игломёт@Ёж','Рога@Лось'])]
organs=[o for o in bpy.data.objects if o.type=='MESH' and '@' in o.name];backs={}
# Runtime organs are two-sided; reproduce the same visibility rule as the stand.
for o in organs:
    dup=o.copy();dup.data=o.data.copy();bpy.context.scene.collection.objects.link(dup)
    bm=bmesh.new();bm.from_mesh(dup.data);bmesh.ops.reverse_faces(bm,faces=list(bm.faces));bm.to_mesh(dup.data);bm.free();backs[o.name]=dup
sc=bpy.context.scene;sc.render.engine='BLENDER_WORKBENCH';sc.render.resolution_x=700;sc.render.resolution_y=950;sc.render.resolution_percentage=100
sh=sc.display.shading;sh.light='STUDIO';sh.color_type='SINGLE';sh.single_color=(.72,.68,.62);sh.show_backface_culling=True;sh.show_object_outline=False
w=bpy.data.worlds.new('preview-white');w.color=(1,1,1);sc.world=w
cam=bpy.data.objects.new('preview-camera',bpy.data.cameras.new('preview-camera'));sc.collection.objects.link(cam);sc.camera=cam;cam.data.type='ORTHO';cam.data.ortho_scale=2.18
for o in bpy.data.objects:
    if o.type=='MESH':
        for p in o.data.polygons:p.use_smooth=False
for label,enabled in cases:
    for o in organs:
        o.hide_render=o.name not in enabled;backs[o.name].hide_render=o.hide_render
    # Existing project organ visibility rule: wolf senses replace human senses.
    bpy.data.objects['Чутьё'].hide_render='Чутьё@Волк' in enabled
    for view,angle in [('front',35),('back',145)]:
        r=math.radians(angle);direction=Vector((math.sin(r),-math.cos(r),0));center=Vector((0,0,1.00));cam.location=center+direction*10;cam.rotation_euler=(-direction).to_track_quat('-Z','Y').to_euler()
        sc.render.filepath=str(root/f'{label}_{view}.png');bpy.ops.render.render(write_still=True)
# Detail frames expose small organs that disappear in a full-height preview.
for label,enabled,center,scale in [('hands',['Руки@Волк'],(-.32,-.01,.89),.34),('head',['Чутьё@Волк','Рога@Лось'],(0,0,1.69),.85),('back',['Шкура@Ёж','Игломёт@Ёж'],(0,.03,1.43),.75)]:
    for o in organs:o.hide_render=o.name not in enabled;backs[o.name].hide_render=o.hide_render
    bpy.data.objects['Чутьё'].hide_render='Чутьё@Волк' in enabled
    angle=145 if label=='back' else 35;r=math.radians(angle);direction=Vector((math.sin(r),-math.cos(r),0));cam.data.ortho_scale=scale;cam.location=Vector(center)+direction*10;cam.rotation_euler=(-direction).to_track_quat('-Z','Y').to_euler();sc.render.filepath=str(root/f'detail-{label}.png');bpy.ops.render.render(write_still=True)
(root/'cases.json').write_text(json.dumps(dict(cases=[dict(id=n,organs=e) for n,e in cases],mode='Static full-growth endpoint test, no stage morph or gameplay legality claim',two_sided_organs=True),ensure_ascii=False,indent=2),encoding='utf-8')
