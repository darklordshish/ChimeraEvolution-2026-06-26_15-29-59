import bpy, math, sys, json
from pathlib import Path
from mathutils import Vector
P=Path(__file__).parent
OUT=P/'renders'; OUT.mkdir(exist_ok=True)
cfg=json.loads((P/'verification.json').read_text(encoding='utf8'))
mode=sys.argv[sys.argv.index('--')+1] if '--' in sys.argv else 'proposal'
bpy.ops.wm.open_mainfile(filepath=str(P/('candidate.blend' if mode=='proposal' else 'source.blend')))
sc=bpy.context.scene; sc.render.engine='BLENDER_WORKBENCH'
w=sc.display.shading; w.light='STUDIO'; w.studio_light='paint.sl'; w.color_type='OBJECT'
w.show_shadows=False; w.show_cavity=False; w.show_specular_highlight=False
w.show_backface_culling=True; w.background_type='WORLD'
sc.world=sc.world or bpy.data.worlds.new('World'); sc.world.color=(.82,.83,.84)
sc.view_settings.view_transform='Standard'
sc.render.image_settings.file_format='PNG'; sc.render.film_transparent=False
sc.render.resolution_x=520; sc.render.resolution_y=720; sc.render.resolution_percentage=100
cam=bpy.data.objects.new('Camera_probe',bpy.data.cameras.new('Camera_probe')); sc.collection.objects.link(cam)
cam.data.type='ORTHO'; cam.data.ortho_scale=1.28; cam.data.clip_end=20; sc.camera=cam
base=(.58,.55,.50)
for vid,en in [('Змея','snake'),('Волк','wolf')]:
    tone_names={o['name'] for o in cfg['variants'][vid]['objects']}
    for o in bpy.data.objects:
        if o.type!='MESH': continue
        o.hide_render=True
        if '@' not in o.name and o.name!='Cube': o.hide_render=o.name in (['хребет','шея','Сердце','Руки'] if mode=='proposal' else ['хребет','шея','Сердце'])
        if o.name in tone_names: o.hide_render=False
        o.color=(*base,1)
        if o.name in tone_names:
            factor=o.data.materials[0].diffuse_color[0]
            o.color=(*(x*factor for x in base),1)
        for p in o.data.polygons: p.use_smooth=False
        if o.data.shape_keys:
            for k in o.data.shape_keys.key_blocks: k.value=0
    for az,label in [(180,'back'),(145,'rear34'),(35,'front34')]:
        a=math.radians(az); d=Vector((math.sin(a),-math.cos(a),.05)).normalized()
        target=Vector((0,0,1.30)); cam.location=target+d*4
        cam.rotation_euler=(-d).to_track_quat('-Z','Y').to_euler()
        sc.render.filepath=str(OUT/f'{en}-{mode}-{label}.png'); bpy.ops.render.render(write_still=True)
print('RENDER DONE',mode)
