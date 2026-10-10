"""ПРОБА 2, 10.10: цельный меш человека из образца Hunyuan3D-2mv (вход — четыре ракурса листа `витрина/человек/вид.png`).
Тот же тракт, что у волка (`mesh_volka.py` → `dovodka_volka.py`), но ДО рига — только кандидаты на выбор геймдизайнеру:
его глаз стоит в начале (урок волка 10.10). В игру не идёт, граф и данные человека не трогает.

Шаги: импорт .glb → один объект → лицо в −Y Blender (+Z Unity), стопы на земле, середина по X → масштаб по росту
`--rost` (1.85 м) → по желанию симметрия (`--sym 1`: половина +X зеркалится) → прореживание:
`--mid` равномерно, затем только корпус, плечи, бёдра и голени до `--budget` (голова, кисти и стопы держат плотность
`--mid` — там читается лицо и пальцы) → плоское затенение → OBJ (оси как у `blender_pyat_kamer.py`: вперёд −Z, вверх Y).

  blender -b --factory-startup -P mesh_cheloveka.py -- вход.glb выход.obj [--rost 1.85] [--sym 1] [--mid 9000] [--budget 4500]
"""
import sys, math
import bpy, bmesh
from mathutils import Matrix

a = sys.argv[sys.argv.index('--') + 1:]
src, out = a[0], a[1]
opt = dict(rost=1.85, sym=0.0, mid=9000.0, budget=4500.0)
i = 2
while i < len(a):
    opt[a[i].lstrip('-')] = float(a[i + 1]); i += 2

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
me = ob.data


def V():
    return [v.co for v in me.vertices]


# ширина — длинная горизонталь (плечи и руки), лицо — туда, куда смотрят стопы
xs = [v.x for v in V()]; ys = [v.y for v in V()]
if (max(ys) - min(ys)) > (max(xs) - min(xs)):
    me.transform(Matrix.Rotation(math.radians(90), 4, 'Z'))
z0 = min(v.z for v in V()); z1 = max(v.z for v in V()); H = z1 - z0
feet = [v for v in V() if v.z < z0 + 0.04 * H]
torso = [v for v in V() if z0 + 0.5 * H < v.z < z0 + 0.7 * H]
cy = sum(v.y for v in torso) / len(torso)
if max(v.y for v in feet) - cy > cy - min(v.y for v in feet):      # носки в +Y — развернуть лицом в −Y
    me.transform(Matrix.Rotation(math.radians(180), 4, 'Z'))
k = opt['rost'] / H
torso = [v for v in V() if z0 + 0.5 * H < v.z < z0 + 0.7 * H]
cx = (min(v.x for v in torso) + max(v.x for v in torso)) / 2
cy = sum(v.y for v in torso) / len(torso)
me.transform(Matrix.Translation((-cx, -cy, -z0)))
me.transform(Matrix.Scale(k, 4))

if opt['sym'] > 0:
    bm = bmesh.new(); bm.from_mesh(me)
    bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], dist=1e-5, plane_co=(0, 0, 0),
                           plane_no=(1, 0, 0), clear_inner=True)
    for v in bm.verts:
        if abs(v.co.x) < 1e-4:
            v.co.x = 0.0
    bm.to_mesh(me); bm.free()
    mm = ob.modifiers.new('mir', 'MIRROR'); mm.use_axis[0] = True; mm.use_clip = True; mm.merge_threshold = 0.001
    bpy.ops.object.modifier_apply(modifier=mm.name)

R = opt['rost']
n0 = len(me.polygons)
m = ob.modifiers.new('dec', 'DECIMATE'); m.ratio = opt['mid'] / max(1, n0)
if opt['sym'] > 0:
    m.use_symmetry = True; m.symmetry_axis = 'X'
bpy.ops.object.modifier_apply(modifier=m.name)
n1 = len(me.polygons)
if opt['budget'] < n1:
    # плотность держат: голова с шеей (выше 0.80 роста), кисти (ниже 0.50 роста и дальше 0.14 роста от оси), стопы
    body = [v.index for v in me.vertices
            if v.co.z < 0.80 * R and v.co.z > 0.06 * R and not (v.co.z < 0.50 * R and abs(v.co.x) > 0.14 * R)]
    vg = ob.vertex_groups.new(name='тело'); vg.add(body, 1.0, 'REPLACE')
    m = ob.modifiers.new('dec2', 'DECIMATE'); m.ratio = opt['budget'] / n1; m.vertex_group = 'тело'; m.vertex_group_factor = 1.0
    if opt['sym'] > 0:
        m.use_symmetry = True; m.symmetry_axis = 'X'
    bpy.ops.object.modifier_apply(modifier=m.name)
bm = bmesh.new(); bm.from_mesh(me); bmesh.ops.triangulate(bm, faces=bm.faces[:]); bm.to_mesh(me); bm.free()
for p in me.polygons:
    p.use_smooth = False
vs = V()
print('ЧЕЛОВЕК: %d → %d тр; рост %.3f, размах X %.3f, глубина Y %.3f; масштаб ×%.4f; симметрия %d' % (
    n0, len(me.polygons), max(v.z for v in vs), max(v.x for v in vs) - min(v.x for v in vs),
    max(v.y for v in vs) - min(v.y for v in vs), k, opt['sym']))
bpy.ops.wm.obj_export(filepath=out, export_selected_objects=False, forward_axis='NEGATIVE_Z', up_axis='Y',
                      export_materials=False, export_normals=False, export_uv=False)
