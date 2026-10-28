"""ОРГАН НА ШАБЛОНЕ (22.10, CONSTRUCTOR_GUIDE «Орган на шаблоне», спека `2026-10-21-organ-prorastaet-klyuch-oblasti.md` §2.1): объект
`<слот>@<вид>` в меше шасси, привязан к его костям, виден только при привитом органе этого вида на этот слот. Растёт формой-ключом
`рост`: база — объект, прижатый к телу шасси (каждая вершина сдвинута к ближайшей точке тела в покое на `--sjatie`), `рост` на весе 1 —
полная форма. Код ставит вес `рост` = доля вида / 0.65.

  blender -b --factory-startup -P organ_objekt.py -- сборка.blend откуда.blend имя_там <слот>@<вид> выход.blend [--sjatie 0.85] [--srez 0.025 [--srez_ot Руки]] [--sherst 0.5:4] [--niz 1.45:1.58 [--shir 0.08]] [--prizhim 0.5] [--tolshchina 0.004]
"""
import sys
import bpy
from mathutils.bvhtree import BVHTree

a = sys.argv[sys.argv.index('--') + 1:]
base, src, nm_src, nm, out = a[:5]
opt = {}
i = 5
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
SJ = float(opt.get('sjatie', '0.85'))

bpy.ops.wm.open_mainfile(filepath=base)
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
old = bpy.data.objects.get(nm)
if old: bpy.data.objects.remove(old, do_unlink=True)
body = [o for o in bpy.data.objects if o.type == 'MESH' and '@' not in o.name]
V, F = [], []
for o in body:
    n0 = len(V); V += [o.matrix_world @ v.co for v in o.data.vertices]; F += [[n0 + k for k in p.vertices] for p in o.data.polygons]
bvh = BVHTree.FromPolygons(V, F)
with bpy.data.libraries.load(src) as (s_, d_): d_.objects = [nm_src]
o = d_.objects[0]; bpy.context.scene.collection.objects.link(o)
o.name = nm; o.data.name = nm; o.parent = arm
for m in o.modifiers:
    if m.type == 'ARMATURE': m.object = arm
if o.data.shape_keys: o.shape_key_clear()
# ИСХОДНАЯ ГЕОМЕТРИЯ ОРГАНА — ДО ЗАПИСИ БАЗЫ И РОСТА (27.10, ГеймБосс по ст.1: «когти — две палочки вбок у каждой кисти, гребень —
# пучок стружки над спиной»; правка одного «роста» их не брала — форма сидит и в базе):
#   `--srez N` — грани, у которых все вершины дальше N м от тела шасси, срезаются (палочки длиннее N); островки мельче 6 граней — тоже;
#   `--sherst доля:шаги` — шерсть массой: прореживание до доли и Таубин (λ 0.5, μ −0.53, края на месте), как griva_massa.py
import bmesh
import numpy as np
if opt.get('srez') or opt.get('sherst'):
    bpy.context.view_layer.objects.active = o; o.select_set(True)
    if opt.get('sherst'):
        dl, gl = opt['sherst'].split(':')
        m_ = o.modifiers.new('d', 'DECIMATE'); m_.ratio = float(dl); m_.use_collapse_triangulate = True
        bpy.ops.object.modifier_apply(modifier='d')
    bm = bmesh.new(); bm.from_mesh(o.data); bm.verts.ensure_lookup_table()
    Mw_ = o.matrix_world; n_s = 0
    if opt.get('srez'):
        N_ = float(opt['srez'])
        # `--srez_ot Руки` — мерить только от своих объектов шасси: палочка когтя смотрит в бедро, и ближайшим «телом» была нога (G8)
        bv_ = bvh
        if opt.get('srez_ot'):
            V2, F2 = [], []
            for b_ in [x for x in body if x.name in opt['srez_ot'].split(',')]:
                n2 = len(V2); V2 += [b_.matrix_world @ v.co for v in b_.data.vertices]; F2 += [[n2 + k for k in p_.vertices] for p_ in b_.data.polygons]
            bv_ = BVHTree.FromPolygons(V2, F2)
        dal = {v.index: bv_.find_nearest(Mw_ @ v.co)[3] > N_ for v in bm.verts}
        ub = [f for f in bm.faces if all(dal[v.index] for v in f.verts)]; n_s = len(ub)
        bmesh.ops.delete(bm, geom=ub, context='FACES')
        seen, mel = set(), []
        for f in bm.faces:
            if f in seen: continue
            st, isl = [f], []; seen.add(f)
            while st:
                g = st.pop(); isl.append(g)
                for e in g.edges:
                    for h in e.link_faces:
                        if h not in seen: seen.add(h); st.append(h)
            if len(isl) < 6: mel += isl
        bmesh.ops.delete(bm, geom=mel, context='FACES')
        bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    if opt.get('sherst'):
        bm.verts.ensure_lookup_table()
        X = np.array([list(v.co) for v in bm.verts]); NB = [[e.other_vert(v).index for e in v.link_edges] for v in bm.verts]
        kr = np.array([any(e.is_boundary for e in v.link_edges) for v in bm.verts])
        for _ in range(int(gl)):
            for lam in (0.5, -0.53):
                L = np.array([X[nb_].mean(0) - X[k] if nb_ else np.zeros(3) for k, nb_ in enumerate(NB)]); L[kr] = 0; X = X + lam * L
        for v, x in zip(bm.verts, X): v.co = x
    bm.to_mesh(o.data); bm.free(); o.data.update()
    print('ОРГАН: %s — срезано граней %d (дальше %s м), шерсть %s; осталось граней %d' % (nm, n_s, opt.get('srez', '—'), opt.get('sherst', '—'), len(o.data.polygons)))
# ЗАТУХАНИЕ КНИЗУ (`--niz z0:z1`, 28.10, дизайн-линия, повтор осмотра дыр п.1: гребень ст.1 — «длинная жёсткая пластина 1.3–1.6H
# до нижних углов лопаток», рисунок — компактный гребень с затуханием выше углов лопаток). Грани, задевшие низ z0 (покой),
# срезаются вместе с островками; от z0 до z1 вершина прижимается к телу шасси с долей (z1 − z)/(z1 − z0): гребень уходит в кожу
if opt.get('niz'):
    z0, z1 = (float(x) for x in opt['niz'].split(':'))
    bm = bmesh.new(); bm.from_mesh(o.data); Mw_ = o.matrix_world; iv_ = Mw_.inverted()
    # `--shir x` — и грани, задевшие |x| > x: гребень — срединная масса; боковая прядь, отогнутая за плечо, давала окно 94×84 мм
    SH_ = float(opt.get('shir', '9'))
    ub = [f for f in bm.faces if any((Mw_ @ v.co).z < z0 or abs((Mw_ @ v.co).x) > SH_ for v in f.verts)]; n_n = len(ub)   # любая вершина: длинные грани после Таубина тянули язык до 1.17
    bmesh.ops.delete(bm, geom=ub, context='FACES')
    seen, mel = set(), []
    for f in bm.faces:
        if f in seen: continue
        st, isl = [f], []; seen.add(f)
        while st:
            g = st.pop(); isl.append(g)
            for e in g.edges:
                for h in e.link_faces:
                    if h not in seen: seen.add(h); st.append(h)
        if len(isl) < 6: mel += isl
    bmesh.ops.delete(bm, geom=mel, context='FACES')
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    for v in bm.verts:
        w = Mw_ @ v.co
        if w.z >= z1: continue
        loc = bvh.find_nearest(w)[0]
        if loc is not None: v.co = iv_ @ (w + (loc - w) * min(1.0, (z1 - w.z) / (z1 - z0)))
    bm.to_mesh(o.data); bm.free(); o.data.update()
    print('ОРГАН: %s — ниже %.2f срезано граней %d, до %.2f затухает к телу; осталось граней %d' % (nm, z0, n_n, z1, len(o.data.polygons)))
# ОБЪЁМ ВМЕСТО ОБОЛОЧКИ (`--tolshchina м`, 28.10, детектор дыр стенда: «в модели дырки» — органы ст.1 рисуются ОДНОСТОРОННЕ
# (двусторонние в игре только черты `@`, BodyMesh.TwoSided), а когти, гребень и уши — тонкие оболочки: их изнанка видна насквозь
# пятнами по 20–50 см²). Оболочка закрывается толщиной с бортиком (Solidify) — замкнутый объём виден с любой стороны
if opt.get('tolshchina'):
    # выделен ТОЛЬКО орган: режим правки берёт все выделенные — нормали шасси переворачивались вместе с органом (проба 28.10)
    for x in bpy.context.view_layer.objects: x.select_set(False)
    bpy.context.view_layer.objects.active = o; o.select_set(True)
    # нормали оболочки из цели генератора разнонаправлены — толщина ложилась то наружу, то внутрь (пятен изнанки не убавилось):
    # сначала нормали наружу
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT'); bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode='OBJECT')
    m_ = o.modifiers.new('t', 'SOLIDIFY'); m_.thickness = float(opt['tolshchina']); m_.offset = -1.0; m_.use_rim = True; m_.use_even_offset = True
    bpy.ops.object.modifier_apply(modifier='t')
    print('ОРГАН: %s — оболочка закрыта толщиной %.1f мм, граней %d' % (nm, float(opt['tolshchina']) * 1000, len(o.data.polygons)))
# ПРИЖИМ ПОЛНОЙ ФОРМЫ (`--prizhim k`, 28.10, гребень ст.1): «рост» — тело + k·(форма − тело); пряди из цели стоят пластинами в
# 5–8 см от кожи, между ними и шеей фон (окна 3/4 сзади) — при k < 1 масса ложится ближе к телу, затухание и контур те же
if opt.get('prizhim'):
    k_ = float(opt['prizhim']); bv_p = bvh
    if opt.get('srez_ot'):   # когти: ближайшее «тело» кончика — бедро (G8); прижим — к своим объектам, как и срез
        V2, F2 = [], []
        for b_ in [x for x in body if x.name in opt['srez_ot'].split(',')]:
            n2 = len(V2); V2 += [b_.matrix_world @ v.co for v in b_.data.vertices]; F2 += [[n2 + k for k in p_.vertices] for p_ in b_.data.polygons]
        bv_p = BVHTree.FromPolygons(V2, F2)
    for v in o.data.vertices:
        w = o.matrix_world @ v.co; loc = bv_p.find_nearest(w)[0]
        if loc is not None: v.co = o.matrix_world.inverted() @ (loc + (w - loc) * k_)
full = [v.co.copy() for v in o.data.vertices]
for v in o.data.vertices:
    w = o.matrix_world @ v.co; loc = bvh.find_nearest(w)[0]
    if loc is not None: v.co = o.matrix_world.inverted() @ (w + (loc - w) * SJ)
o.shape_key_add(name='Basis', from_mix=False)
kb = o.shape_key_add(name='рост', from_mix=False)
for k, p in enumerate(full): kb.data[k].co = p
print('ОРГАН: %s — %d тр, база прижата к телу на %.0f %%, «рост» — полная форма' % (nm, len(o.data.polygons), SJ * 100))
bpy.ops.wm.save_as_mainfile(filepath=out)
