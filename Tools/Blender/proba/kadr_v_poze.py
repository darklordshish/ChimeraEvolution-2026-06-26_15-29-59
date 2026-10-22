# КАДР В ПОЗЕ СТУПЕНИ (22.10): ключ KEYN на 1, остальные на 0; кости — рамками из poza.json; анфас, профиль, 3/4, сзади, 3/4 сзади.
#   blender -b --factory-startup -P kadr_v_poze.py -- сборка.blend поза.json префикс [ключ]
# как покажет игра на ступени 2: ключ (вес 1) + поза из файла *-poza.json (повороты костей) + черта @; анфас, профиль, 3/4
import bpy, sys, json
from mathutils import Vector, Matrix, Quaternion
a = sys.argv[sys.argv.index('--') + 1:]; KEYN = a[3] if len(a) > 3 else 'Волк'; HIDE = ['голова', 'Чутьё']
bpy.ops.wm.open_mainfile(filepath=a[0])
PZ = {b['name']: b for b in json.load(open(a[1], encoding='utf-8'))['bones']}
for o in bpy.data.objects:
    if o.type == 'MESH' and o.data.shape_keys:
        for kb_ in o.data.shape_keys.key_blocks: kb_.value = 1.0 if kb_.name == KEYN else 0.0
    if o.type == 'MESH' and (o.name in HIDE or (o.name.startswith('@') and o.name != '@' + KEYN)): o.hide_render = True
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]; AW = arm.matrix_world
bpy.context.view_layer.objects.active = arm; bpy.ops.object.mode_set(mode='POSE')
order = []
def walk(b):
    order.append(b.name)
    for c in b.children: walk(c)
for b in arm.data.bones:
    if b.parent is None: walk(b)
for bn in order:
    if bn not in PZ: continue
    pb = arm.pose.bones[bn]; bpy.context.view_layer.update()
    h, t, z = Vector(PZ[bn]['head']), Vector(PZ[bn]['tail']), Vector(PZ[bn]['z_axis'])
    y = (t - h).normalized(); x = y.cross(z).normalized(); z2 = x.cross(y)
    R = Matrix((x, y, z2)).transposed().to_4x4()
    sc = (AW @ pb.matrix).to_scale()
    Mw = Matrix.Translation(h) @ R
    pb.matrix = AW.inverted() @ Mw
bpy.context.view_layer.update(); bpy.ops.object.mode_set(mode='OBJECT')
sc = bpy.context.scene; sc.render.engine = 'BLENDER_WORKBENCH'; sc.display.shading.light = 'STUDIO'
sc.render.resolution_x = 1000; sc.render.resolution_y = 1000
cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); sc.collection.objects.link(cam); sc.camera = cam
cam.data.type = 'ORTHO'; cam.data.ortho_scale = 2.6
for nm, d in (('front', Vector((0, -1, 0))), ('side', Vector((-1, 0, 0))), ('34', Vector((-0.7, -0.7, 0)).normalized()), ('back', Vector((0, 1, 0))), ('34back', Vector((-0.7, 0.7, 0)).normalized())):
    cam.location = Vector((0, 0, 1.0)) + d * 10; cam.rotation_euler = d.to_track_quat('Z', 'Y').to_euler()
    sc.render.filepath = a[2] + '_' + nm + '.png'; bpy.ops.render.render(write_still=True)
