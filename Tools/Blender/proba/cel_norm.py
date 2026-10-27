"""НОРМИРОВКА 3D-ЦЕЛИ (перенесено из черновиков 27.10): glb генератора → один меш, низ на земле, центр по x — середина габарита,
по y — по стопам (низ 5 %), рост `H` м; прорежено до 60 тыс. треугольников; OBJ в осях тракта. `[префикс кадров]` — анфас и
профиль с сеткой 0.1 м (по ним снимаются ориентиры цели, `orientiry-celi.json`).

  blender -b --factory-startup -P cel_norm.py -- cel_mv_A.glb cel_A_norm.obj 1.86 [префикс кадров]
"""
import bpy, sys, math, bmesh
from mathutils import Vector, Matrix
a = sys.argv[sys.argv.index('--') + 1:]
H = float(a[2])
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=a[0])
ms = [o for o in bpy.data.objects if o.type == 'MESH']
for o in bpy.data.objects: o.select_set(o in ms)
bpy.context.view_layer.objects.active = ms[0]
if len(ms) > 1: bpy.ops.object.join()
ob = bpy.context.view_layer.objects.active
bpy.ops.object.parent_clear(type='CLEAR_KEEP_TRANSFORM'); bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
me = ob.data
V = [v.co for v in me.vertices]
z0, z1 = min(v.z for v in V), max(v.z for v in V)
cx = (min(v.x for v in V) + max(v.x for v in V)) / 2
# центр по Y — по ступням (низ 5 %)
low = [v for v in V if v.z < z0 + 0.05 * (z1 - z0)]
cy = sum(v.y for v in low) / len(low)
k = H / (z1 - z0)
me.transform(Matrix.Translation((-cx, -cy, -z0))); me.transform(Matrix.Scale(k, 4))
# прореживание для работы: 60 тыс. тр
m = ob.modifiers.new('d', 'DECIMATE'); m.ratio = min(1, 60000 / sum(len(p.vertices) - 2 for p in me.polygons))
bpy.ops.object.modifier_apply(modifier=m.name)
bm = bmesh.new(); bm.from_mesh(me); bmesh.ops.triangulate(bm, faces=bm.faces[:]); bm.to_mesh(me); bm.free()
V = [v.co for v in me.vertices]
print('NORM k %.4f габарит x %.3f..%.3f y %.3f..%.3f z %.3f..%.3f, тр %d' % (k, min(v.x for v in V), max(v.x for v in V), min(v.y for v in V), max(v.y for v in V), min(v.z for v in V), max(v.z for v in V), len(me.polygons)))
bpy.ops.wm.obj_export(filepath=a[1], export_selected_objects=False, forward_axis='NEGATIVE_Z', up_axis='Y', export_materials=False, export_normals=False, export_uv=False)
if len(a) < 4: raise SystemExit(0)
# кадры с сеткой 0.1 м
ob.color = (0.75, 0.73, 0.70, 1)
for p in me.polygons: p.use_smooth = False
def bar(p, s):
    bpy.ops.mesh.primitive_cube_add(size=1, location=p); c = bpy.context.active_object; c.scale = s
    c.color = (0.9, 0.3, 0.2, 1) if abs(round(max(p) * 10) % 5) == 0 else (0.35, 0.35, 0.4, 1)
for i in range(0, 23):
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 0.6, i * 0.1)); c = bpy.context.active_object; c.scale = (3, 0.001, 0.002)
    c.color = (0.9, 0.3, 0.2, 1) if i % 5 == 0 else (0.3, 0.3, 0.35, 1)
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0.6, 0, i * 0.1)); c = bpy.context.active_object; c.scale = (0.001, 3, 0.002)
    c.color = (0.9, 0.3, 0.2, 1) if i % 5 == 0 else (0.3, 0.3, 0.35, 1)
for i in range(-8, 9):
    bpy.ops.mesh.primitive_cube_add(size=1, location=(i * 0.1, 0.6, 1.1)); c = bpy.context.active_object; c.scale = (0.002, 0.001, 2.4)
    c.color = (0.9, 0.3, 0.2, 1) if i % 5 == 0 else (0.3, 0.3, 0.35, 1)
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0.6, i * 0.1, 1.1)); c = bpy.context.active_object; c.scale = (0.001, 0.002, 2.4)
    c.color = (0.9, 0.3, 0.2, 1) if i % 5 == 0 else (0.3, 0.3, 0.35, 1)
sc = bpy.context.scene; sc.render.engine = 'BLENDER_WORKBENCH'
sh = sc.display.shading; sh.light = 'STUDIO'; sh.color_type = 'OBJECT'
sh.background_type = 'VIEWPORT'; sh.background_color = (0.15, 0.16, 0.18)
sc.render.resolution_x, sc.render.resolution_y = 1100, 1200
cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); sc.collection.objects.link(cam); sc.camera = cam
cam.data.type = 'ORTHO'; cam.data.ortho_scale = 2.4; cam.data.clip_end = 100
c0 = Vector((0, 0, 1.05))
for nm, d in (('front', (0, -1, 0)), ('side', (-1, 0, 0))):
    e = Vector(d); cam.location = c0 + e * 20; cam.rotation_euler = (c0 - cam.location).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = a[3] + '_' + nm + '.png'; bpy.ops.render.render(write_still=True)
