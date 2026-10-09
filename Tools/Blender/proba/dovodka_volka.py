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

# 1а. КОРЕНЬ ХВОСТА (критик, проход 3: «отодвинуть, не заполнив, — половина правки»; до рига обязательно — иначе на махе
# порвётся): между хвостом и ляжками у образца щель от крупа почти до скакательного. Заполнитель — эллипсоид круп→ляжки у
# корня хвоста; меш с ним сплавляется вокселями `--voxel`, хвост выходит из цельного крупа
if opt.get('fill', 0) > 0:
    V = [v.co for v in me.vertices]
    yh = min(v.y for v in V); L = max(v.y for v in V) - yh
    bpy.ops.mesh.primitive_uv_sphere_add(segments=32, ring_count=16, radius=1.0,
                                         location=(0, yh + 0.845 * L, 0.78 * opt['back']))
    fl = bpy.context.active_object
    fl.scale = (0.13, 0.07, 0.22)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    # БУЛЕВО ОБЪЕДИНЕНИЕ, а не воксели всего тела: вокселизация заглаживала поверхность, и прореживание складывало бок в
    # картон (проход 4, проба). Объединение трогает меш только там, где заполнитель
    bpy.context.view_layer.objects.active = ob
    bo = ob.modifiers.new('fill', 'BOOLEAN'); bo.operation = 'UNION'; bo.object = fl; bo.solver = 'EXACT'
    bpy.ops.object.modifier_apply(modifier=bo.name)
    bpy.data.objects.remove(fl, do_unlink=True)
    me = ob.data
    print('корень хвоста: заполнитель объединён, вершин %d' % len(me.vertices))

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
# ...и ГОЛОВА ОБРАТНО ВНИЗ в затылочном суставе на `head` градусов (критик проход 2: шею поднять, а спинку носа держать
# ~22° вниз, как ref_02 — при общем повороте морда перекрыла глаза в анфас). Сустав — у основания черепа: 0.20 длины от
# морды, на уровне основания уха; полная сила до 0.15, ноль за 0.24
V = [v.co for v in me.vertices]
yf = min(v.y for v in V); L = max(v.y for v in V) - yf
head = math.radians(opt.get('head', 0.0))
if head:
    top = max(v.co.z for v in me.vertices if v.co.y - yf < 0.25 * L)
    piv2 = Vector((0.0, yf + 0.20 * L, top - 0.22))
    for v in me.vertices:
        d = (v.co.y - yf) / L
        if d >= 0.24:
            continue
        t = 1.0 if d <= 0.15 else (0.24 - d) / 0.09
        t = t * t * (3 - 2 * t)
        r = v.co - piv2
        th = -head * t                       # нос вниз
        c, s_ = math.cos(th), math.sin(th)
        v.co = piv2 + Vector((r.x, r.y * c + r.z * s_, -r.y * s_ + r.z * c))
zs = [v.co.z for v in me.vertices]
print('после посадки: верх %.3f' % max(zs))

# ГЛАЗНИЦА И НАДБРОВЬЕ (критик, проход 3: глаза в анфас не вернулись — глазница и надбровье мелкие): глаз — на 0.115 м выше
# кончика носа и на 0.21 позади (ортопара), на поверхности головы. Глазница внутрь на `--eye` м, надбровье — полкой над ней
eye = opt.get('eye', 0.0)
if eye > 0:
    nose = min(me.vertices, key=lambda v: v.co.y).co.copy()
    ye, ze = nose.y + 0.21, nose.z + 0.115
    near = [v for v in me.vertices if abs(v.co.y - ye) < 0.02 and abs(v.co.z - ze) < 0.02]
    xe = max(abs(v.co.x) for v in near)
    E = Vector((xe, ye, ze)); Bw = Vector((xe, ye - 0.005, ze + 0.035))
    for v in me.vertices:
        q = Vector((abs(v.co.x), v.co.y, v.co.z)); sg = 1 if v.co.x >= 0 else -1
        de = (q - E).length
        if de < 0.035:
            v.co.x -= sg * eye * (1 - de / 0.035) ** 2
        db = (q - Bw).length
        if db < 0.03:
            k = (1 - db / 0.03) ** 2
            v.co.x += sg * 0.5 * eye * k; v.co.y -= 0.6 * eye * k
    print('глазница: глаз у (%.3f %.3f %.3f), вглубь %.3f' % (xe, ye, ze, eye))

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
# ЛАПЫ ×`paw` в ширину (критик проход 2: ноги сузили, лапы нет — сзади лапа 1.6 ширины плюсны против 1.3 у образца)
paw = opt.get('paw', 1.0)
for lo, hi in ((0.15, 0.45), (0.62, 1.0)):
    selP = lambda v: v.co.z < 0.09 and lo * L < (v.co.y - y0) < hi * L
    cP = side_centres(selP)
    for v in me.vertices:
        if selP(v) and abs(v.co.x) > 0.02:
            w = sm((0.09 - v.co.z) / 0.03)
            c = cP if v.co.x > 0 else -cP
            v.co.x = c + (v.co.x - c) * (1 + (paw - 1) * w)
# 5. ХВОСТ ОТ БЕДРА: всё позади линии седалищных бугров у средней линии — назад на `tail` м, сильнее к кончику
tail = opt.get('tail', 0.04)
for v in me.vertices:
    d = (v.co.y - y0) / L
    if d > 0.80 and abs(v.co.x) < 0.14 and 0.15 < v.co.z < 1.05:
        w = sm((d - 0.80) / 0.08) * sm((0.14 - abs(v.co.x)) / 0.04)
        v.co.y += tail * w

# 8. РВАНЫЙ КОНТУР ГРИВЫ (критик, проход 2: в тумане остаётся контур, его делает рваный край). Не клинья поверх меша —
# они торчали иглами (проба 09.10), — а зубцы самой кромки: вершины у контура сдвигаются наружу пилой по ходу шерсти
# (зубец растёт к хвосту и обрывается). Зоны: гребень затылок → холка (вверх), щёки и воротник (вбок). Край кисти хвоста — пока нет
saw_a = opt.get('saw', 0.0)
if saw_a > 0:
    import random
    rnd = random.Random(7)
    V = [v.co for v in me.vertices]
    y0 = min(v.y for v in V); L = max(v.y for v in V) - y0
    NB = 120
    top = [-1.0] * NB; side = [0.0] * NB; front = {}
    for v in me.vertices:
        b = min(NB - 1, int((v.co.y - y0) / L * NB))
        if abs(v.co.x) < 0.08:
            top[b] = max(top[b], v.co.z)
        side[b] = max(side[b], abs(v.co.x))

    def teeth(a, b):
        """Зубцы НЕРАВНОГО шага 3–8 см (критик, проход 3: равный шаг торчком — «гребень дракона»)."""
        t = [a]
        while t[-1] < b:
            t.append(t[-1] + rnd.uniform(0.03, 0.08))
        return t
    CREST = teeth(y0 + 0.17 * L, y0 + 0.50 * L)          # гребень — до плеча, гаснет
    BIB = teeth(0.55 * opt['back'], 1.00 * opt['back'])  # манишка — по высоте
    CHEEK = teeth(0.80 * opt['back'], 1.30 * opt['back'])

    def phase(x, T):
        for a, b in zip(T, T[1:]):
            if a <= x < b:
                f = (x - a) / (b - a)
                return (f if f < 0.8 else (1 - f) * 4.0) * min(1.0, (b - a) / 0.06)
        return 0.0
    for v in me.vertices:                                  # передняя кромка груди по высоте
        d = (v.co.y - y0) / L
        if 0.20 < d < 0.40 and abs(v.co.x) < 0.06:
            zb = int(v.co.z / 0.02)
            front[zb] = min(front.get(zb, 9.0), v.co.y)
    TAN = math.tan(math.radians(25))                       # кончик назад, 20–30° к поверхности
    for v in me.vertices:
        d = (v.co.y - y0) / L
        b = min(NB - 1, int(d * NB))
        if 0.17 < d < 0.50 and top[b] > 0:                 # гребень: затылок → холка → гаснет на плечо
            w = sm((0.05 - (top[b] - v.co.z)) / 0.05) * sm((0.10 - abs(v.co.x)) / 0.05) * sm((d - 0.17) / 0.03) * sm((0.50 - d) / 0.12)
            f = saw_a * w * phase(v.co.y, CREST)
            v.co.y += f; v.co.z += f * TAN
        if 0.15 < d < 0.30 and v.co.z > 0.80 * opt['back']:  # щёки и воротник: вбок-назад
            w = sm((0.04 - (side[b] - abs(v.co.x))) / 0.04) * sm((d - 0.15) / 0.03) * sm((0.30 - d) / 0.04)
            f = saw_a * w * phase(v.co.z, CHEEK)
            v.co.x += (1 if v.co.x > 0 else -1) * f * TAN; v.co.y += f
        zb = int(v.co.z / 0.02)                            # МАНИШКА остриём вниз — главный признак в анфас после ушей
        if opt.get('bib', 0) > 0 and 0.20 < d < 0.40 and zb in front and abs(v.co.x) < 0.07 and 0.55 * opt['back'] < v.co.z < 1.0 * opt['back']:
            w = sm((0.025 - (v.co.y - front[zb])) / 0.025) * sm((0.07 - abs(v.co.x)) / 0.03)
            f = 0.6 * saw_a * w * phase(v.co.z, BIB)          # глубже и шире — «медальон» на груди (проход 4)
            v.co.z -= f; v.co.y -= f * TAN
    print('рваный контур: зубец %.3f м' % saw_a)

B_ = opt['back']


def joint(c, y0, L):
    """КОЛЬЦА НА СУСТАВАХ (критик, проход 3: «важнее всего по деформации»): полосы сгибов держат плотность прохода `mid` —
    несколько колец на сгиб — и не растворяются в плоскости. Локоть, запястье, колено, скакательный, основание шеи, корень
    хвоста; доля длины от морды, высота в метрах."""
    d = (c.y - y0) / L; z = c.z
    return ((0.28 < d < 0.46 and abs(z - 0.62 * B_) < 0.05) or (0.25 < d < 0.46 and 0.14 < z < 0.26)
            or (0.62 < d < 0.84 and 0.48 < z < 0.64 and abs(c.x) > 0.05) or (0.72 < d < 0.96 and 0.30 < z < 0.45)
            or (0.24 < d < 0.32 and z > 0.75 * B_) or (0.80 < d < 0.89 and z > 0.72 * B_))


# 4. плотность: сначала равномерно до `mid` (уровень, на котором голова и лапы читаются — критик: 6000), потом ТОЛЬКО
# тело (без головы и лап) — до `budget`. Вес группы в Decimate: 0 — вершину не трогать. Планарное растворение по углу
# дало веера на весь бок (проба 09.10) — грани по формам ставятся руками, не им
n0 = sum(len(p.vertices) - 2 for p in me.polygons)   # в треугольниках: после вокселей грани — четырёхугольники
m = ob.modifiers.new('dec', 'DECIMATE'); m.ratio = opt['mid'] / max(1, n0); m.use_symmetry = True; m.symmetry_axis = 'X'
bpy.ops.object.modifier_apply(modifier=m.name)
n1 = len(me.polygons)
print('  равномерно: %d тр' % n1)
if opt['budget'] < n1:
    vg = ob.vertex_groups.new(name='тело')
    V = [v.co for v in me.vertices]
    y0 = min(v.y for v in V); L = max(v.y for v in V) - y0
    body = [v.index for v in me.vertices if (v.co.y - y0) > 0.24 * L and v.co.z > 0.12 and not joint(v.co, y0, L)]
    vg.add(body, 1.0, 'REPLACE')
    m = ob.modifiers.new('dec2', 'DECIMATE'); m.ratio = opt['budget'] / n1; m.vertex_group = 'тело'
    m.vertex_group_factor = 1.0; m.use_symmetry = True; m.symmetry_axis = 'X'
    bpy.ops.object.modifier_apply(modifier=m.name)
    print('  тело: %d тр (вершин тела %d)' % (len(me.polygons), len(body)))
# 9. ПЛОСКОСТИ ПО МЫШЕЧНЫМ ГРУППАМ (критик, проход 2: дешёвый путь без вееров): грань относится к группе карты мышц
# (`витрина/волк/мышцы.png`) по месту на теле; ограниченное растворение по углу `--groups` собирает многоугольники ТОЛЬКО внутри
# группы (граница групп — материал), затем «красивая» триангуляция. Голова и лапы не трогаются
ga = opt.get('groups', 0.0)
if ga > 0:
    V = [v.co for v in me.vertices]
    y0 = min(v.y for v in V); L = max(v.y for v in V) - y0
    B_ = opt['back']

    def group(c):
        d = (c.y - y0) / L; z = c.z
        if d < 0.20 or z < 0.10 or joint(c, y0, L): return 0   # голова, лапы, суставы
        if d < 0.30: return 1 if z > 0.75 * B_ else 2          # шея / манишка
        if d < 0.45: return 3 if z > 0.62 * B_ else 4          # лопатка+плечо / предплечье
        if d < 0.62: return 5 if z > 0.62 * B_ else (6 if z > 0.45 * B_ else 4)   # рёбра / низ груди
        if d < 0.70: return 7 if z > 0.62 * B_ else 8          # поясница / пах
        if d < 0.90: return 9 if z > 0.45 * B_ else 10         # бедро / голень
        return 11                                               # хвост
    for gi in range(12):
        me.materials.append(bpy.data.materials.new('г%d' % gi))
    for f in me.polygons:
        f.material_index = group(f.center)
    bm = bmesh.new(); bm.from_mesh(me)
    edges = [e for e in bm.edges if all(f.material_index != 0 for f in e.link_faces)]
    verts = list({v for e in edges for v in e.verts})
    bmesh.ops.dissolve_limit(bm, angle_limit=math.radians(ga), verts=verts, edges=edges, delimit={'MATERIAL'})
    bmesh.ops.triangulate(bm, faces=bm.faces[:], quad_method='BEAUTY', ngon_method='BEAUTY')
    bm.to_mesh(me); bm.free()
    # ДЛИННЫЕ ТОНКИЕ ГРАНИ (критик, проход 3: грань поперёк рёбер от локтя к паху) — рёбра длиннее `--long` делятся
    bm = bmesh.new(); bm.from_mesh(me)
    longe = [e for e in bm.edges if e.calc_length() > opt.get('long', 0.28)]
    bmesh.ops.subdivide_edges(bm, edges=longe, cuts=1)
    bmesh.ops.triangulate(bm, faces=bm.faces[:], quad_method='BEAUTY', ngon_method='BEAUTY')
    bm.to_mesh(me); bm.free()
    print('плоскости по группам: угол %.0f°, тр %d' % (ga, len(me.polygons)))
    print('  длинных рёбер разделено: %d' % len(longe))
bm = bmesh.new(); bm.from_mesh(me); bmesh.ops.triangulate(bm, faces=bm.faces[:]); bm.to_mesh(me); bm.free()
for p in me.polygons:
    p.use_smooth = False
V = [v.co for v in me.vertices]
print('ДОВОДКА: %d → %d тр; габарит X %.3f Y %.3f Z %.3f' % (n0, len(me.polygons), max(v.x for v in V) - min(v.x for v in V),
      max(v.y for v in V) - min(v.y for v in V), max(v.z for v in V)))
bpy.ops.wm.obj_export(filepath=out, export_selected_objects=False, forward_axis='NEGATIVE_Z', up_axis='Y',
                      export_materials=False, export_normals=False, export_uv=False)
