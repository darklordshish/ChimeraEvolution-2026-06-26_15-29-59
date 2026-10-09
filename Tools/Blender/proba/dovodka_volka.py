"""ПРОБА (б), ДОВОДКА (09.10, «да» геймдизайнера; план критика `a3cd4d3c85e5485f5`): сырой образец Hunyuan3D-2mv в метрах
(`mesh_volka.py`, 300 тыс. тр) → игровой меш. Шаги по весу критика; каждый — параметр, чтобы проход воспроизводился.

  1. МАСШТАБ по линии спины: середина спины листа 1.135 м (ортопара; холка 1.170). Привязка по уху раздувала тело на 4 %:
     уши у образца ниже листа.
  2. СИММЕТРИЯ по X: половина `--half` (+1 / −1) отзеркаливается — сзади у образца щель у хвоста с одной стороны и хвост,
     слитый с телом, с другой.
  3. ПОСАДКА ГОЛОВЫ И ШЕИ: поворот вверх вокруг основания шеи на `--neck` градусов, плавно от Z0 до Z1 вдоль тела.
  4. ПЛОТНОСТЬ: равномерно до `--mid` (6000: голова и лапы читаются), затем только тело — до `--budget`.

  blender -b --factory-startup -P dovodka_volka.py -- вход.obj выход.obj [--half 1] [--neck 11] [--mid 6000] [--budget 3500]
Оси OBJ на входе и выходе — как у `blender_pyat_kamer.py` (вперёд −Z, вверх Y); в Blender морда в +Y? — проверяется по
верху (уши) и разворачивается в −Y.
"""
import sys, math
import bpy, bmesh
from mathutils import Vector, Matrix

a = sys.argv[sys.argv.index('--') + 1:]
src, out = a[0], a[1]
opt = dict(half=1.0, neck=10.0, budget=2500.0, mid=6000.0, back=1.135)
i = 2
while i < len(a):
    opt[a[i].lstrip('-')] = float(a[i + 1]); i += 2

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.wm.obj_import(filepath=src, forward_axis='NEGATIVE_Z', up_axis='Y')
ob = [o for o in bpy.data.objects if o.type == 'MESH'][0]
bpy.context.view_layer.objects.active = ob; ob.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
me = ob.data

# морда — в −Y Blender (как у `mesh_volka.py`); голова там, где выше верх
V = [v.co for v in me.vertices]
y0, y1 = min(v.y for v in V), max(v.y for v in V); L = y1 - y0
hi_lo = max(v.z for v in V if v.y < y0 + 0.25 * L); hi_hi = max(v.z for v in V if v.y > y1 - 0.25 * L)
if hi_hi > hi_lo:
    me.transform(Matrix.Rotation(math.pi, 4, 'Z'))
FWD = -1.0                                 # морда в −Y: «вперёд» = −Y

# 1. масштаб по линии спины — за гривой (середина длины задевает гриву: 1.25 вместо 1.19)
V = [v.co for v in me.vertices]
y0, y1 = min(v.y for v in V), max(v.y for v in V); ym = (y0 + y1) / 2; L = y1 - y0
back = max(v.z for v in V if y1 - 0.45 * L < v.y < y1 - 0.30 * L)   # спина за гривой: 30–45 % длины от хвоста
k = opt['back'] / back
me.transform(Matrix.Scale(k, 4))
print('масштаб по спине: спина %.3f → %.3f (×%.3f)' % (back, opt['back'], k))

# 2. симметрия: оставить половину знака `half` и отзеркалить
bm = bmesh.new(); bm.from_mesh(me)
bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], dist=1e-5, plane_co=(0, 0, 0),
                       plane_no=(1, 0, 0), clear_inner=opt['half'] > 0, clear_outer=opt['half'] < 0)
for v in bm.verts:
    if abs(v.co.x) < 1e-4:
        v.co.x = 0.0
bm.to_mesh(me); bm.free()
mm = ob.modifiers.new('mir', 'MIRROR'); mm.use_axis[0] = True; mm.use_clip = True; mm.merge_threshold = 0.001
bpy.ops.object.modifier_apply(modifier=mm.name)

# 3. посадка головы и шеи: поворот вверх вокруг основания шеи
V = [v.co for v in me.vertices]
y0, y1 = min(v.y for v in V), max(v.y for v in V); L = y1 - y0
yf = y0                                     # перёд (морда) — минимальный Y
# основание шеи: на 0.33 длины от морды, на высоте 0.88 спины
piv = Vector((0.0, yf + 0.33 * L, 0.88 * opt['back']))
Z0, Z1 = 0.28 * L, 0.45 * L                 # от морды: полная сила ближе Z0, ноль дальше Z1
ang = math.radians(opt['neck'])
for v in me.vertices:
    d = v.co.y - yf                          # расстояние от морды вдоль тела
    if d >= Z1:
        continue
    t = 1.0 if d <= Z0 else (Z1 - d) / (Z1 - Z0)
    h = min(1.0, max(0.0, (v.co.z - 0.40 * opt['back']) / (0.30 * opt['back'])))   # по высоте тоже плавно: без ступеньки на плече
    t = t * t * (3 - 2 * t) * h * h * (3 - 2 * h)
    r = v.co - piv
    th = ang * t                             # нос вверх: вокруг X, перёд (−Y) поднимается
    c, s = math.cos(th), math.sin(th)
    ny = r.y * c + r.z * s
    nz = -r.y * s + r.z * c
    v.co = piv + Vector((r.x, ny, nz))
zs = [v.co.z for v in me.vertices]
print('после посадки: верх %.3f' % max(zs))

# 5–7. ЗОННЫЕ ПРАВКИ (критик r(б)1): плавные, по зонам вдоль тела (доля длины от морды) и по высоте
V = [v.co for v in me.vertices]
y0 = min(v.y for v in V); L = max(v.y for v in V) - y0


def sm(t):
    t = min(1.0, max(0.0, t)); return t * t * (3 - 2 * t)


def side_centres(sel):
    """Центр по X каждой стороны (ноги парные) в зоне — ось, вокруг которой сужать."""
    l = [v.co.x for v in me.vertices if sel(v) and v.co.x > 0]
    return (sum(l) / len(l)) if l else 0.0


# 7. ПЕРЕДНИЕ НОГИ ОВАЛОМ: в анфас уже на `fore` (глубина в профиль не трогается); ниже локтя (0.62 спины)
fore = opt.get('fore', 0.85)
selF = lambda v: v.co.z < 0.55 * opt['back'] and 0.20 * L < (v.co.y - y0) < 0.45 * L
cF = side_centres(selF)
for v in me.vertices:
    if selF(v) and abs(v.co.x) > 0.02:
        w = sm((0.55 * opt['back'] - v.co.z) / 0.08)
        c = cF if v.co.x > 0 else -cF
        v.co.x = c + (v.co.x - c) * (1 + (fore - 1) * w)
# ...и ПЕРЁД КОРПУСА в анфас на 13 % шире листа (0.385 против 0.34): плечи и грудь уже на `chest`
chest = opt.get('chest', 0.90)
for v in me.vertices:
    d = (v.co.y - y0) / L
    if 0.18 < d < 0.55 and v.co.z > 0.45 * opt['back']:
        w = sm((d - 0.18) / 0.06) * sm((0.55 - d) / 0.10) * sm((v.co.z - 0.45 * opt['back']) / 0.10)
        v.co.x *= 1 + (chest - 1) * w
# 6. ПЛЮСНА СЗАДИ: уже на `hind` ниже скакательного (0.31 холки = 0.36 м)
hind = opt.get('hind', 0.80)
selH = lambda v: v.co.z < 0.36 and (v.co.y - y0) > 0.62 * L
cH = side_centres(selH)
for v in me.vertices:
    if selH(v) and abs(v.co.x) > 0.02:
        w = sm((0.36 - v.co.z) / 0.06) * sm((v.co.z - 0.05) / 0.04)      # лапу не трогать
        c = cH if v.co.x > 0 else -cH
        v.co.x = c + (v.co.x - c) * (1 + (hind - 1) * w)
# 5. ХВОСТ ОТ БЕДРА: всё позади линии седалищных бугров у средней линии — назад на `tail` м, сильнее к кончику
tail = opt.get('tail', 0.04)
for v in me.vertices:
    d = (v.co.y - y0) / L
    if d > 0.80 and abs(v.co.x) < 0.14 and 0.15 < v.co.z < 1.05:
        w = sm((d - 0.80) / 0.08) * sm((0.14 - abs(v.co.x)) / 0.04)
        v.co.y += tail * w

# 4. плотность: сначала равномерно до `mid` (уровень, на котором голова и лапы читаются — критик: 6000), потом ТОЛЬКО
# тело (без головы и лап) — до `budget`. Вес группы в Decimate: 0 — вершину не трогать. Планарное растворение по углу
# дало веера на весь бок (проба 09.10) — грани по формам ставятся руками, не им
n0 = len(me.polygons)
m = ob.modifiers.new('dec', 'DECIMATE'); m.ratio = opt['mid'] / max(1, n0); m.use_symmetry = True; m.symmetry_axis = 'X'
bpy.ops.object.modifier_apply(modifier=m.name)
n1 = len(me.polygons)
if opt['budget'] < n1:
    vg = ob.vertex_groups.new(name='тело')
    V = [v.co for v in me.vertices]
    y0 = min(v.y for v in V); L = max(v.y for v in V) - y0
    body = [v.index for v in me.vertices if (v.co.y - y0) > 0.24 * L and v.co.z > 0.12]
    vg.add(body, 1.0, 'REPLACE')
    m = ob.modifiers.new('dec2', 'DECIMATE'); m.ratio = opt['budget'] / n1; m.vertex_group = 'тело'
    m.vertex_group_factor = 1.0; m.use_symmetry = True; m.symmetry_axis = 'X'
    bpy.ops.object.modifier_apply(modifier=m.name)
bm = bmesh.new(); bm.from_mesh(me); bmesh.ops.triangulate(bm, faces=bm.faces[:]); bm.to_mesh(me); bm.free()
for p in me.polygons:
    p.use_smooth = False
V = [v.co for v in me.vertices]
print('ДОВОДКА: %d → %d тр; габарит X %.3f Y %.3f Z %.3f' % (n0, len(me.polygons), max(v.x for v in V) - min(v.x for v in V),
      max(v.y for v in V) - min(v.y for v in V), max(v.z for v in V)))
bpy.ops.wm.obj_export(filepath=out, export_selected_objects=False, forward_axis='NEGATIVE_Z', up_axis='Y',
                      export_materials=False, export_normals=False, export_uv=False)
