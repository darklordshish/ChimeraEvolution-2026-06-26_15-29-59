"""ЛОСЬ МЕШЕМ, 14.10: сырой образец Hunyuan3D-2mv (кандидат C — «у которого нет лишних ушей», выбор геймдизайнера 14.10;
вход — вычищенные вырезки листа витрины `Референсы/работа/модельная/2026-10-13-proba-los/chisto/`) → кандидат игрового
меша до рига. Тот же тракт, что у волка (`mesh_volka.py` + `dovodka_volka.py`) и человека (`mesh_cheloveka.py`).

Шаги:
  1. один объект, морда в −Y Blender (+Z Unity), низ на земле;
  2. ПЛАСТИНА ПОД КОПЫТАМИ: тень на листе генератор принял за подставку. Снимается всё в нижних `--plast` метрах образца,
     что лежит в плане дальше `--okolo` от «колонны» копыта (вершин чуть выше пластины), дыры закрываются;
  3. МАСШТАБ ПО СКЕЛЕТУ ЛОСЯ (`Docs/models/rig/los-skelet.json`): длина от кончика носа до конца хвоста — как у скелета
     (место `нос` y −2.459, конец кости `хвост` y 1.520 + `--hvost` на кожу), холка печатается для сверки;
  4. СИММЕТРИЯ по X (`--half` +1/−1 — какая половина остаётся): веса рига зеркалятся по паре вершин;
  5. ПЛОТНОСТЬ: равномерно до `--budget` треугольников, плоское затенение.

  blender -b --factory-startup -P mesh_losya.py -- вход.glb выход.obj [--budget 7000] [--half 1] [--plast 0.035] [--okolo 0.012]
Оси OBJ на выходе — как у остальных проб (вперёд −Z, вверх Y).
"""
import sys, math
import bpy, bmesh
from mathutils import Vector, Matrix, kdtree

a = sys.argv[sys.argv.index('--') + 1:]
src, out = a[0], a[1]
opt = dict(budget=7000.0, half=1.0, plast=0.035, okolo=0.012, nos=-2.4585, hvost=0.06, tail_end=1.5199)
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
REP = []

# 1. морда в −Y: у лося голова — тот конец, где выше верх (рога)
V = [v.co for v in me.vertices]
y0, y1 = min(v.y for v in V), max(v.y for v in V); L = y1 - y0
if max(v.z for v in V if v.y > y1 - 0.25 * L) > max(v.z for v in V if v.y < y0 + 0.25 * L):
    me.transform(Matrix.Rotation(math.pi, 4, 'Z'))

# 2. пластина под копытами
bm = bmesh.new(); bm.from_mesh(me)
z0 = min(v.co.z for v in bm.verts)
col = [v.co for v in bm.verts if z0 + opt['plast'] < v.co.z < z0 + opt['plast'] + 0.03]
kd = kdtree.KDTree(len(col))
for k_, p in enumerate(col):
    kd.insert(Vector((p.x, p.y, 0)), k_)
kd.balance()
bad = [v for v in bm.verts if v.co.z <= z0 + opt['plast'] and kd.find(Vector((v.co.x, v.co.y, 0)))[2] > opt['okolo']]
bmesh.ops.delete(bm, geom=bad, context='VERTS')
# обрывки, отвалившиеся от тела, — снять: остаётся самый большой кусок
bm.verts.ensure_lookup_table()
seen, comps = set(), []
for v0 in bm.verts:
    if v0 in seen:
        continue
    st, c = [v0], []
    while st:
        u = st.pop()
        if u in seen:
            continue
        seen.add(u); c.append(u); st.extend(e.other_vert(u) for e in u.link_edges)
    comps.append(c)
comps.sort(key=len, reverse=True)
bmesh.ops.delete(bm, geom=[u for c in comps[1:] for u in c], context='VERTS')
nb = sum(1 for e in bm.edges if e.is_boundary)
bmesh.ops.holes_fill(bm, edges=[e for e in bm.edges if e.is_boundary], sides=0)
bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 3])
REP.append('пластина: снято %d вершин, обрывков %d, открытых рёбер до заплаты %d, после %d' % (
    len(bad), len(comps) - 1, nb, sum(1 for e in bm.edges if e.is_boundary)))
bm.to_mesh(me); bm.free()

# 3. масштаб по длине скелета: кончик носа → конец хвоста
V = [v.co for v in me.vertices]
y0, y1 = min(v.y for v in V), max(v.y for v in V); z0 = min(v.z for v in V)
cx = (min(v.x for v in V) + max(v.x for v in V)) / 2
k = (opt['tail_end'] + opt['hvost'] - opt['nos']) / (y1 - y0)
me.transform(Matrix.Translation((-cx, -y0, -z0)))
me.transform(Matrix.Scale(k, 4))
me.transform(Matrix.Translation((0, opt['nos'], 0)))
V = [v.co for v in me.vertices]
withers = max(v.z for v in V if -0.9 < v.y < -0.3 and abs(v.x) < 0.15)
REP.append('масштаб ×%.3f; длина нос→хвост %.3f м; холка (y −0.9…−0.3) %.3f м (кость `горб` до 2.600); верх рогов %.3f м' % (
    k, max(v.y for v in V) - min(v.y for v in V), withers, max(v.z for v in V)))

# 4. симметрия по X
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.symmetrize(direction='POSITIVE_X' if opt['half'] > 0 else 'NEGATIVE_X', threshold=0.002)
bpy.ops.mesh.remove_doubles(threshold=0.0005)
bpy.ops.object.mode_set(mode='OBJECT')

# 5. плотность
n0 = len(me.polygons)
m = ob.modifiers.new('dec', 'DECIMATE'); m.ratio = opt['budget'] / max(1, n0); m.use_symmetry = True; m.symmetry_axis = 'X'
bpy.ops.object.modifier_apply(modifier=m.name)
bm = bmesh.new(); bm.from_mesh(me); bmesh.ops.triangulate(bm, faces=bm.faces[:])
nonman = sum(1 for e in bm.edges if not e.is_manifold); bnd = sum(1 for e in bm.edges if e.is_boundary)
bm.to_mesh(me); bm.free()
for p in me.polygons:
    p.use_smooth = False
V = [v.co for v in me.vertices]
REP.append('%d → %d тр; неманифолд %d, открытых %d; габарит X %.3f Y %.3f Z %.3f' % (
    n0, len(me.polygons), nonman, bnd, max(v.x for v in V) - min(v.x for v in V), max(v.y for v in V) - min(v.y for v in V), max(v.z for v in V)))
bpy.ops.wm.obj_export(filepath=out, export_selected_objects=False, forward_axis='NEGATIVE_Z', up_axis='Y',
                      export_materials=False, export_normals=False, export_uv=False)
print('ЛОСЬ: ' + '\nЛОСЬ: '.join(REP))
