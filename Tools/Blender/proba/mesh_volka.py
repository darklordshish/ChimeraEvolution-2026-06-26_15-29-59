"""ПРОБА (б), 09.10: цельный меш чистого волка из образца Hunyuan3D-2mv (вход — ортопара витрины + спина листа).
Только проба: в игру не идёт, граф и данные волка не трогает (задача ГеймБосса 09.10, решение геймдизайнера).

Шаги: импорт .glb → один объект → морда в −Y Blender (+Z Unity), низ на земле → масштаб по кончику уха 1.533 м
(ортопара: земля 886 px, холка 322 px = 1.170 м, ухо 147 px) → прореживание до бюджета → плоское затенение → OBJ
(оси как у `blender_pyat_kamer.py`: вперёд −Z, вверх Y) и отчёт габарита.

  blender -b --factory-startup -P mesh_volka.py -- вход.glb выход.obj [бюджет_тр=2500]
"""
import sys, math
import bpy, bmesh
from mathutils import Vector, Matrix

a = sys.argv[sys.argv.index('--') + 1:]
src, out = a[0], a[1]
BUDGET = int(a[2]) if len(a) > 2 else 2500
EAR_TOP = 1.533

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
ms = [o for o in bpy.data.objects if o.type == 'MESH']
for o in bpy.data.objects:
    o.select_set(o in ms)
bpy.context.view_layer.objects.active = ms[0]
if len(ms) > 1:
    bpy.ops.object.join()
ob = bpy.context.view_layer.objects.active
bpy.ops.object.parent_clear(type='CLEAR_KEEP_TRANSFORM')
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)


def verts():
    return [ob.matrix_world @ v.co for v in ob.data.vertices]


# ось тела — длинная горизонталь; морда — тот конец, где выше верх (уши)
V = verts()
xs = [v.x for v in V]; ys = [v.y for v in V]
along_x = (max(xs) - min(xs)) > (max(ys) - min(ys))
if along_x:
    ob.data.transform(Matrix.Rotation(math.radians(90), 4, 'Z'))
V = verts(); ys = [v.y for v in V]; y0, y1 = min(ys), max(ys)
L = y1 - y0
top_lo = max((v.z for v in V if v.y < y0 + 0.25 * L), default=0)
top_hi = max((v.z for v in V if v.y > y1 - 0.25 * L), default=0)
if top_hi > top_lo:                     # голова в +Y — развернуть мордой в −Y
    ob.data.transform(Matrix.Rotation(math.radians(180), 4, 'Z'))
V = verts()
zmin = min(v.z for v in V); zmax = max(v.z for v in V)
k = EAR_TOP / (zmax - zmin)
cx = (min(v.x for v in V) + max(v.x for v in V)) / 2; cy = (min(v.y for v in V) + max(v.y for v in V)) / 2
ob.data.transform(Matrix.Translation((-cx, -cy, -zmin)))
ob.data.transform(Matrix.Scale(k, 4))
n0 = len(ob.data.polygons)
m = ob.modifiers.new('dec', 'DECIMATE'); m.ratio = BUDGET / max(1, n0)
bpy.ops.object.modifier_apply(modifier=m.name)
bm = bmesh.new(); bm.from_mesh(ob.data); bmesh.ops.triangulate(bm, faces=bm.faces[:]); bm.to_mesh(ob.data); bm.free()
for p in ob.data.polygons:
    p.use_smooth = False
V = verts()
print('ПРОБА: %d → %d тр; габарит X %.3f (ширина) Y %.3f (длина) Z %.3f (высота), масштаб ×%.4f' % (
    n0, len(ob.data.polygons), max(v.x for v in V) - min(v.x for v in V), max(v.y for v in V) - min(v.y for v in V),
    max(v.z for v in V), k))
bpy.ops.wm.obj_export(filepath=out, export_selected_objects=False, forward_axis='NEGATIVE_Z', up_axis='Y',
                      export_materials=False, export_normals=False, export_uv=False)
