"""РИГ И НАРЕЗКА ЧЕЛОВЕКА-МЕША (проба 2; тракт и правила — как у волка, `rig_volka.py` и ТЗ `Docs/models/TZ-2026-10-10-rig-volka.md`).

Вход — доведённый меш (`mesh_cheloveka.py`) и скелет графа (`Docs/models/rig/chelovek-skelet.json`, 49 костей). Выход — FBX:
арматура ровно по скелету и объект на слот поля: `хребет`, `шея`, `голова`, `Руки`, `Сердце` (грудная клетка), `Ноги`
(слот кости — поле `socket` узла графа `Docs/models/handoff/chelovek-graph.json`).

  1. ПОСАДКА: меш уже в метрах листа и стоит по оси; сдвиг вдоль тела — по голеням (середина сечения ноги на 0.30 м).
  2. ВЕСА: несущие кости — автоматом Blender, затем правила: голова жёсткая, кисть на `предплечье`, стопа на `голень`
     (ниже запястья и щиколотки костей в графе нет), у средней линии таза ноги не тянут, стороны не смешиваются, веса
     зеркальны, до четырёх костей на вершину. Мышечные кости без весов.
  3. ШОВ «основание черепа»: настоящий разрез — голова отдельным куском, плоское кольцо 12 на конце кости `шея`; у куска
     веса только `голова` и `шея`, кольцо у куска и у тела весит одинаково (`шея` = 1).
     Посадочное кольцо «корень хвоста» — на СПЛОШНОЙ коже крестца (решение механик 13.10: у человека без хвоста дырки
     нет) — только числом в отчёте и паспорте.
  4. НАРЕЗКА: грань идёт в слот, чьи кости весят в ней больше; зубцы и островки приглаживаются.

  blender -b --factory-startup -P rig_cheloveka.py -- меш.obj скелет.json граф.json выход.fbx [--blend файл] [--shots префикс]
"""
import sys, math, json, os
import bpy, bmesh
from mathutils import Vector, Matrix, kdtree

a = sys.argv[sys.argv.index('--') + 1:]
src, skp, grp, out = a[0], a[1], a[2], os.path.abspath(a[3])
opt = {}
i = 4
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.wm.obj_import(filepath=src, forward_axis='NEGATIVE_Z', up_axis='Y')
ob = [o for o in bpy.data.objects if o.type == 'MESH'][0]
bpy.context.view_layer.objects.active = ob; ob.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
me = ob.data
sk = json.load(open(skp, encoding='utf-8'))
BN = {b['name']: b for b in sk['bones']}
SOCKET = {n['name']: n.get('socket', '') for n in json.load(open(grp, encoding='utf-8'))['nodes']}
REP = []
XU = Vector((1, 0, 0))


def sm(t):
    t = min(1.0, max(0.0, t)); return t * t * (3 - 2 * t)


def sect_y(z, xmin, xmax):
    ys = []
    for e in me.edges:
        p, q = me.vertices[e.vertices[0]].co, me.vertices[e.vertices[1]].co
        if (p.z - z) * (q.z - z) < 0:
            c = p + (q - p) * ((z - p.z) / (q.z - p.z))
            if xmin < c.x < xmax:
                ys.append(c)
    return ys


def on_bone(name, z):
    h, t = Vector(BN[name + '.L']['head']), Vector(BN[name + '.L']['tail'])
    return h + (t - h) * ((z - h.z) / (t.z - h.z))


# ---------- 1. ПОСАДКА ----------
q = sect_y(0.30, 0.05, 0.30); my = (min(c.y for c in q) + max(c.y for c in q)) / 2
shift = on_bone('голень', 0.30).y - my
me.transform(Matrix.Translation((0, shift, 0)))
REP.append('посадка: сдвиг вдоль тела %+.3f м (по голени на 0.30 м)' % shift)
REP.append('расхождения меша со скелетом (середина сечения конечности стороны +X против оси кости, м):')
for nm, z, x0, x1 in (('плечо', 1.30, 0.21, 0.40), ('предплечье', 1.05, 0.21, 0.42), ('бедро', 0.70, 0.07, 0.30), ('голень', 0.30, 0.05, 0.30),
                      ('голень', 0.12, 0.05, 0.30)):
    q = sect_y(z, x0, x1)
    if q:
        mx = (min(c.x for c in q) + max(c.x for c in q)) / 2; myy = (min(c.y for c in q) + max(c.y for c in q)) / 2
        b = on_bone(nm, z)
        REP.append('  %-11s z %.2f: меш (%.3f %.3f)  кость (%.3f %.3f)  Δ вбок %+.3f, вдоль %+.3f' % (nm, z, mx, myy, b.x, b.y, mx - b.x, myy - b.y))
top = max(v.co.z for v in me.vertices)
REP.append('  макушка меша %.3f м; конец кости `голова` %.3f' % (top, BN['голова']['tail'][2]))

# ---------- арматура ----------
arm = bpy.data.armatures.new('Скелет'); ao = bpy.data.objects.new('Скелет', arm)
bpy.context.scene.collection.objects.link(ao)
bpy.ops.object.select_all(action='DESELECT')
bpy.context.view_layer.objects.active = ao; ao.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
for b in sk['bones']:
    eb = arm.edit_bones.new(b['name'])
    eb.head, eb.tail = Vector(b['head']), Vector(b['tail'])
    eb.align_roll(Vector(b['z_axis']))
    assert eb.name == b['name'], 'имя кости изменено: %s → %s' % (b['name'], eb.name)
for b in sk['bones']:
    if b['parent']:
        arm.edit_bones[b['name']].parent = arm.edit_bones[b['parent']]
bpy.ops.object.mode_set(mode='OBJECT')
MAIN = ['хребет', 'таз', 'живот', 'грудная клетка', 'шея', 'голова']
LIMB = ['лопатка', 'плечо', 'предплечье', 'бедро', 'голень']
DEF = MAIN + LIMB + [n + '.L' for n in LIMB]
for b in arm.bones:
    b.use_deform = b.name in DEF

# ---------- 2. автовеса на копии до разреза ----------
skin = ob.copy(); skin.data = me.copy(); bpy.context.scene.collection.objects.link(skin)
bpy.ops.object.select_all(action='DESELECT')
skin.select_set(True); ao.select_set(True); bpy.context.view_layer.objects.active = ao
bpy.ops.object.parent_set(type='ARMATURE_AUTO')
gname = {g.index: g.name for g in skin.vertex_groups}
kd = kdtree.KDTree(len(skin.data.vertices))
for v in skin.data.vertices:
    kd.insert(v.co, v.index)
kd.balance()
heat = [{gname[g.group]: g.weight for g in v.groups if g.weight > 1e-4} for v in skin.data.vertices]
empty = sum(1 for h in heat if not h)
assert empty < 0.2 * len(heat), 'автовеса не сошлись: без весов %d из %d вершин' % (empty, len(heat))

# ---------- 3. ШОВ «основание черепа» ----------
nh, nt = Vector(BN['шея']['head']), Vector(BN['шея']['tail'])
AX = (nt - nh).normalized()
WN = Vector((0, AX.z, -AX.y)); WN = WN if WN.y > 0 else -WN        # «верх» кольца — дорсальная сторона (затылок), как у волка
bm = bmesh.new(); bm.from_mesh(me)
VP = bm.verts.layers.int.new('piece'); RG = bm.verts.layers.int.new('ring')
for v in bm.verts:
    v[VP] = 0; v[RG] = 0
bm.faces.ensure_lookup_table(); bm.verts.ensure_lookup_table()
CR = nt + AX * float(opt.get('sz', 0.0))


def ang(co, c, w):
    d = co - c
    return math.atan2(d.dot(XU), d.dot(w)) % (2 * math.pi)


seed = max(bm.faces, key=lambda f: f.calc_center_median().z)
reg, st = set(), [seed]
while st:
    f = st.pop()
    if f in reg or (f.calc_center_median() - CR).dot(AX) <= 0 or f.calc_center_median().z < 1.45:
        continue
    reg.add(f); st.extend(g for e in f.edges for g in e.link_faces if g is not f)
for _ in range(4):
    for f in bm.faces:
        nbs = [g for e in f.edges for g in e.link_faces if g is not f]
        inn = sum(1 for g in nbs if g in reg)
        if f in reg and inn <= 1: reg.discard(f)
        elif f not in reg and inn >= 2 and (f.calc_center_median() - CR).dot(AX) > 0 and f.calc_center_median().z > 1.45: reg.add(f)
E = [e for e in bm.edges if len(e.link_faces) == 2 and (e.link_faces[0] in reg) != (e.link_faces[1] in reg)]
deg = {}
for e in E:
    for v in e.verts:
        deg[v] = deg.get(v, 0) + 1
assert all(d == 2 for d in deg.values()), 'шов шеи: граница не простая петля'
where = [v.co.copy() for v in deg]
n_edge = len(where)
cc = sum(where, Vector()) / len(where); cc = Vector((0, cc.y, cc.z))
cc = cc + AX * (CR - cc).dot(AX)                                     # центр — в плоскости шва
rx = max(abs((p - cc).dot(XU)) for p in where); rw = max(abs((p - cc).dot(WN)) for p in where)
bmesh.ops.split_edges(bm, edges=E)
for inside in (True, False):
    loop = {v for e in bm.edges if e.is_boundary and (e.link_faces[0] in reg) == inside for v in e.verts
            if any((v.co - p).length < 1e-7 for p in where)}
    sect = {}
    for v in loop:
        sect.setdefault(int(round(ang(v.co, cc, WN) / (math.pi / 6))) % 12, []).append(v)
    assert len(sect) == 12, 'шов шеи: в кольце %d секторов из 12' % len(sect)
    for k_, vs in sect.items():
        th = math.pi / 6 * k_
        co = cc + XU * (rx * math.sin(th)) + WN * (rw * math.cos(th))
        for v in vs:                                       # номер — всем: какая из слитых вершин выживет, заранее не известно
            v[RG] = 1 + k_
        if len(vs) > 1: bmesh.ops.pointmerge(bm, verts=vs, merge_co=co)
        else: vs[0].co = co
# стягивание рваной кромки в 12 точек оставляет у кольца мелкие дырки и узелки: закрыть всё открытое, кроме самих колец
for _ in range(3):
    small = [e for e in bm.edges if e.is_boundary and not (e.verts[0][RG] and e.verts[1][RG])]
    bad = [e for e in bm.edges if not e.is_manifold and not e.is_boundary]
    if not small and not bad:
        break
    if bad:
        vs = {v for e in bad for v in e.verts if not v[RG]}
        bmesh.ops.delete(bm, geom=list({f for v in vs for f in v.link_faces}), context='FACES')
        small = [e for e in bm.edges if e.is_boundary and not (e.verts[0][RG] and e.verts[1][RG])]
    if small:
        bmesh.ops.holes_fill(bm, edges=[e for e in bm.edges if e.is_boundary], sides=5)     # кольца по 12 рёбер не закрываются
    bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 3])
# ...и отчёт по каждой стороне шва: у куска и у тела должно остаться ровно по 12 открытых рёбер
def loops_report():
    es = [e for e in bm.edges if e.is_boundary]
    adj_ = {}
    for e in es:
        for v in e.verts:
            adj_.setdefault(v, []).append(e)
    return len(es), sorted(len(l) for l in adj_.values())[-3:], sum(1 for v in adj_ if not v[RG])


# лоскут: грань, у которой все рёбра открытые (висит на одной вершине кольца после стягивания), — снять
flap = [f for f in bm.faces if all(e.is_boundary for e in f.edges)]
if flap:
    bmesh.ops.delete(bm, geom=flap, context='FACES')
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
for _ in range(6):          # лишняя вершина на границе у кольца: закрыть треугольником с её соседями по границе
    stray = [v for v in bm.verts if not v[RG] and any(e.is_boundary for e in v.link_edges)]
    if not stray:
        break
    v = stray[0]
    nbv = [e.other_vert(v) for e in v.link_edges if e.is_boundary]
    if len(nbv) == 2 and bm.faces.get((v, nbv[0], nbv[1])) is None:
        bm.faces.new((v, nbv[0], nbv[1]))
    else:
        break
bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
nb_, degs_, stray_ = loops_report()
bm.verts.ensure_lookup_table()
seen, st = set(), [max(bm.verts, key=lambda v: v.co.z)]
while st:
    u = st.pop()
    if u in seen:
        continue
    seen.add(u); u[VP] = 1; st.extend(e.other_vert(u) for e in u.link_edges)
bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
bnd_n = sum(1 for e in bm.edges if e.is_boundary); nonman = sum(1 for e in bm.edges if not e.is_manifold and not e.is_boundary)
bm.to_mesh(me); bm.free()
for p_ in me.polygons:
    p_.use_smooth = False
REP.append('шов «основание черепа»: кольцо 12 на конце кости `шея` (%.3f м от её начала); центр (%.3f %.3f %.3f), ось наружу (%.2f %.2f %.2f), '
           '«верх» (%.2f %.2f %.2f), полуоси вбок %.3f × по «верху» %.3f м; кромка до кольца %d вершин; открытых рёбер %d (ждём 24), неманифолд %d'
           % ((CR - nh).length, *cc, *AX, *WN, rx, rw, n_edge, bnd_n, nonman))
# посадочное кольцо хвоста — на сплошной коже крестца: точка кожи на средней линии сзади на высоте начала кости `таз`
zt = BN['таз']['head'][2] + 0.03
back = max((v.co for v in me.vertices if abs(v.co.x) < 0.03 and abs(v.co.z - zt) < 0.03), key=lambda c: c.y)
TAX = Vector((0, 0.64, -0.76)); TW = Vector((0, 0.76, 0.64))
REP.append('посадочное кольцо «корень хвоста» (на сплошной коже, 8): центр (%.3f %.3f %.3f), ось наружу (%.2f %.2f %.2f), «верх» (%.2f %.2f %.2f), '
           'полуоси 0.045 × 0.040 м; кость `таз`' % (0.0, back.y, back.z, *TAX, *TW))
SEAM = dict(c=cc, ax=AX, up=WN, r=(rx, rw), at=(CR - nh).length, tail_c=Vector((0, back.y, back.z)), tail_ax=TAX, tail_up=TW)

# ---------- веса ----------
vpv = [d.value for d in me.attributes['piece'].data]; rgv = [d.value for d in me.attributes['ring'].data]

# РУКА ИЛИ НЕ РУКА — ПО СВЯЗНОСТИ: ниже 1.25 м рука соединена с телом только через плечо, значит в меше, обрезанном по
# этой высоте, руки — отдельные острова (те, что не доходят до средней линии). Расстояние до костей тут врёт: наружная
# сторона бедра к вытянутой кости кисти ближе, чем к оси бедра. Срослась кисть с бедром — острова сольются, и это ошибка меша
ZARM = 1.25
adj = {}
for e in me.edges:
    i_, j_ = e.vertices
    if me.vertices[i_].co.z < ZARM and me.vertices[j_].co.z < ZARM:
        adj.setdefault(i_, []).append(j_); adj.setdefault(j_, []).append(i_)
is_arm = [False] * len(me.vertices); seen_ = set(); n_arm_isl = 0
for v0 in adj:
    if v0 in seen_:
        continue
    comp, st = [], [v0]
    while st:
        u = st.pop()
        if u in seen_:
            continue
        seen_.add(u); comp.append(u); st.extend(adj[u])
    if all(abs(me.vertices[u].co.x) > 0.12 for u in comp) and max(abs(me.vertices[u].co.x) for u in comp) > 0.28:
        n_arm_isl += 1
        for u in comp:
            is_arm[u] = True
assert n_arm_isl == 2, 'ниже %.2f м рук-островов %d, а не 2: кисть срослась с бедром или рука с корпусом' % (ZARM, n_arm_isl)
W = []
for v in me.vertices:
    co = v.co
    w = dict(heat[kd.find(co)[1]])
    if not w:
        for _, j, _ in kd.find_n(co, 12):
            if heat[j]:
                w = dict(heat[j]); break
    # голова жёсткая выше подбородка
    hw = sm((co.z - 1.63) / 0.05)
    w = {k_: x * (1 - hw) for k_, x in w.items()}; w['голова'] = w.get('голова', 0) + hw
    # кисть — на предплечье, стопа — на голень: костей ниже запястья и щиколотки в графе нет
    sfx = '.L' if co.x > 0 else ''
    # РУКА И НОГА ВИСЯТ РЯДОМ: автовеса отдавали наружную сторону бедра костям руки (кисть в 2 см от ляжки) — в позе нога
    # тянулась за рукой занавесом. Ниже плеч у руки нет весов ноги и корпуса-таза, у остального тела — весов руки
    if co.z < ZARM:
        arm_side = is_arm[v.index]
        for nm in list(w):
            base = nm.split('.')[0]
            if (base in ('плечо', 'предплечье') and not arm_side) or (base in ('бедро', 'голень', 'таз', 'хребет', 'живот') and arm_side):
                w.pop(nm)
        w = {k_: x for k_, x in w.items() if x > 1e-6}
        if arm_side and co.z < 0.93:                      # кисть — жёстко на предплечье
            t = sm((0.93 - co.z) / 0.04); w = {k_: x * (1 - t) for k_, x in w.items()}; w['предплечье' + sfx] = w.get('предплечье' + sfx, 0) + t
        w = {k_: x for k_, x in w.items() if x > 1e-6}
        if not w:
            w = {(('плечо' if co.z > 1.16 else 'предплечье') + sfx if arm_side else 'таз' if co.z > 0.95 else 'бедро' + sfx if co.z > 0.5 else 'голень' + sfx): 1.0}
        s_ = sum(w.values()); w = {k_: x / s_ for k_, x in w.items()}
    if co.z < 0.10:
        t = sm((0.10 - co.z) / 0.03); w = {k_: x * (1 - t) for k_, x in w.items()}; w['голень' + sfx] = w.get('голень' + sfx, 0) + t
    # у средней линии таза и корпуса конечности не тянут
    f = max(sm((abs(co.x) - 0.02) / 0.05), sm((0.86 - co.z) / 0.05)) if co.z < 1.2 else sm((abs(co.x) - 0.04) / 0.08)
    if f < 1:
        mv = 0.0
        for nm in list(w):
            if nm.split('.')[0] in LIMB:
                mv += w[nm] * (1 - f); w[nm] *= f
        core = {nm: x for nm, x in w.items() if nm in MAIN and x > 0}
        if core:
            cs = sum(core.values())
            for nm, x in core.items():
                w[nm] += mv * x / cs
        elif mv:
            nm = 'таз' if co.z < 1.07 else 'грудная клетка'
            w[nm] = w.get(nm, 0) + mv
    if abs(co.x) > 0.02:
        for nm in LIMB:
            wrong, right = (nm, nm + '.L') if co.x > 0 else (nm + '.L', nm)
            if wrong in w:
                w[right] = w.get(right, 0) + w.pop(wrong)
    r_ = rgv[v.index]
    if r_: w = {'шея': 1.0}
    elif vpv[v.index] == 1: w = {k_: x for k_, x in w.items() if k_ in ('голова', 'шея')} or {'голова': 1.0}
    elif 'голова' in w: w['шея'] = w.get('шея', 0) + w.pop('голова')
    w = {k_: x for k_, x in w.items() if x > 0}
    s_ = sum(w.values()); W.append({k_: x / s_ for k_, x in w.items()})


def swap(w):
    return {(k_[:-2] if k_.endswith('.L') else (k_ + '.L' if k_ in LIMB else k_)): x for k_, x in w.items()}


kdm = kdtree.KDTree(len(me.vertices))
for v in me.vertices:
    kdm.insert(v.co, v.index)
kdm.balance()
asym = 0
for v in me.vertices:
    if v.co.x > 1e-5:
        hit = [j for _, j, d in kdm.find_n((-v.co.x, v.co.y, v.co.z), 4) if d < 1e-4 and vpv[j] == vpv[v.index] and bool(rgv[j]) == bool(rgv[v.index])]
        if hit: W[hit[0]] = swap(W[v.index])
        else: asym += 1
    elif abs(v.co.x) <= 1e-5:
        w, ws = W[v.index], swap(W[v.index])
        W[v.index] = {k_: (w.get(k_, 0) + ws.get(k_, 0)) / 2 for k_ in set(w) | set(ws)}
for k_, w in enumerate(W):
    topw = sorted(w.items(), key=lambda kv: -kv[1])[:4]
    s_ = sum(x for _, x in topw)
    W[k_] = {nm: x / s_ for nm, x in topw if x / s_ > 0.01}
    s_ = sum(W[k_].values()); W[k_] = {nm: x / s_ for nm, x in W[k_].items()}
bpy.data.objects.remove(skin, do_unlink=True)

# ---------- 4. НАРЕЗКА ----------
ORDER = ['голова', 'шея', 'хребет', 'Сердце', 'Руки', 'Ноги']
slot_of = lambda nm: SOCKET[nm[:-2] if nm.endswith('.L') else nm]
F = [tuple(p.vertices) for p in me.polygons]
fslot, fixed = [], []
for f in F:
    sc = {}
    for vi in f:
        for nm, x in W[vi].items():
            sc[slot_of(nm)] = sc.get(slot_of(nm), 0) + x
    pc = vpv[f[0]] == 1
    sc.pop('голова', None)
    fslot.append('голова' if pc else max(sc, key=sc.get)); fixed.append(pc)
edge_f = {}
for k_, f in enumerate(F):
    for x, y in ((f[0], f[1]), (f[1], f[2]), (f[2], f[0])):
        edge_f.setdefault((min(x, y), max(x, y)), []).append(k_)
nb = [[] for _ in F]
for fs in edge_f.values():
    if len(fs) == 2:
        nb[fs[0]].append(fs[1]); nb[fs[1]].append(fs[0])
for _ in range(3):
    for k_ in range(len(F)):
        if fixed[k_]:
            continue
        o = [fslot[j] for j in nb[k_] if fslot[j] != fslot[k_] and not fixed[j]]
        if len(o) >= 2 and o.count(o[0]) >= 2:
            fslot[k_] = o[0]
done = set()
for k_ in range(len(F)):
    if k_ in done or fixed[k_]:
        continue
    comp, st = [], [k_]
    while st:
        u = st.pop()
        if u in done:
            continue
        done.add(u); comp.append(u)
        st.extend(j for j in nb[u] if fslot[j] == fslot[k_] and j not in done)
    if len(comp) < 12:
        o = [fslot[j] for u in comp for j in nb[u] if fslot[j] != fslot[k_] and not fixed[j]]
        if o:
            to = max(set(o), key=o.count)
            for u in comp:
                fslot[u] = to

V = [v.co.copy() for v in me.vertices]
bpy.data.objects.remove(ob, do_unlink=True)
parts = {}
for sl in ORDER:
    fs = [F[k_] for k_ in range(len(F)) if fslot[k_] == sl]
    ids = sorted({vi for f in fs for vi in f}); re = {vi: k_ for k_, vi in enumerate(ids)}
    m = bpy.data.meshes.new(sl)
    m.from_pydata([V[vi] for vi in ids], [], [tuple(re[vi] for vi in f) for f in fs])
    m.update()
    for p in m.polygons:
        p.use_smooth = False
    o = bpy.data.objects.new(sl, m); bpy.context.scene.collection.objects.link(o)
    assert o.name == sl, 'имя объекта изменено: ' + o.name
    groups = {}
    for vi in ids:
        for nm, x in W[vi].items():
            if nm not in groups:
                groups[nm] = o.vertex_groups.new(name=nm)
            groups[nm].add([re[vi]], x, 'REPLACE')
    o.parent = ao
    md = o.modifiers.new('Скелет', 'ARMATURE'); md.object = ao
    parts[sl] = (o, ids)
for b in arm.bones:
    b.use_deform = True

tot = 0
REP.append('треугольники по объектам:')
for sl in ORDER:
    n = len(parts[sl][0].data.polygons); tot += n
    REP.append('  %-7s %5d тр, %4d вершин' % (sl, n, len(parts[sl][1])))
REP.append('  всего   %5d тр' % tot)
cnt = {}
for w in W:
    for nm in w:
        cnt[nm] = cnt.get(nm, 0) + 1
REP.append('кости с весами (%d из %d; вершин на кость): ' % (len(cnt), len(arm.bones))
           + ', '.join('%s %d' % (nm, cnt[nm]) for nm in [b.name for b in arm.bones] if nm in cnt))
REP.append('несимметричных вершин при зеркале весов: %d' % asym)

# ---------- паспорта ----------
if 'pasporta' in opt:
    def U(q):
        return [round(-q.x, 5), round(q.z, 5), round(-q.y, 5)]
    ring_pts = [SEAM['c'] + XU * (SEAM['r'][0] * math.sin(math.pi / 6 * k_)) + SEAM['up'] * (SEAM['r'][1] * math.cos(math.pi / 6 * k_)) for k_ in range(12)]
    tail_pts = [SEAM['tail_c'] + XU * (0.045 * math.sin(math.pi / 4 * k_)) + SEAM['tail_up'] * (0.040 * math.cos(math.pi / 4 * k_)) for k_ in range(8)]
    note = 'метры тела человека, оси Unity; кольцо плоское, поперёк кости; вершина 0 — на «верху» (дорсальная сторона), дальше к −X Unity; ellipse_m — вбок × по «верху»'
    common = {'species': 'Человек', 'plan': 'двуногий', 'fbx': 'Assets/_Chimera/Models/chelovek_mesh.fbx', 'mirror': False,
              'armature': 'все узлы графа человека', 'keys': [], 'generator': 'Tools/Blender/proba/rig_cheloveka.py --pasporta …',
              'source': 'Референсы/работа/модельная/2026-10-10-proba-chelovek/'}
    # голова человека — кусок с тем же швом, что волчья (формат и поля — как `volk-golova-kusok.json`, письмо механик 13.10)
    head = dict(common, slot='Пасть', fit='ring', objects={'main': 'голова'}, bones=['голова', 'шея'],
                tris=len(parts['голова'][0].data.polygons), tris_main=len(parts['голова'][0].data.polygons),
                seam={'type': 'основание черепа', 'ring': list(range(12)), 'ring_m': [U(q) for q in ring_pts], 'center_m': U(SEAM['c']),
                      'ellipse_m': [round(SEAM['r'][0], 4), round(SEAM['r'][1], 4)], 'axis_out': U(SEAM['ax']), 'up': U(SEAM['up']),
                      'bone': 'шея', 'on_bone_m': round(SEAM['at'], 4), 'overhang_m': 0.0, 'note': note})
    # посадочное кольцо хвоста — на СПЛОШНОЙ коже: не край куска, а место (центр, ось, эллипс, порядок вершин)
    tail = dict(common, kind='посадочное место на сплошной коже', bones=['таз'],
                seam={'type': 'корень хвоста', 'ring': list(range(8)), 'ring_m': [U(q) for q in tail_pts], 'center_m': U(SEAM['tail_c']),
                      'ellipse_m': [0.045, 0.040], 'axis_out': U(SEAM['tail_ax']), 'up': U(SEAM['tail_up']), 'bone': 'таз', 'note': note})
    for fname, pas in (('chelovek-golova-kusok.json', head), ('../chelovek-koren-hvosta.json', tail)):     # посадочное место — не деталь: рядом с графом, не в parts/
        with open(os.path.join(os.path.abspath(opt['pasporta']), fname), 'w', encoding='utf-8') as fh:
            json.dump(pas, fh, ensure_ascii=False, indent=2)
        REP.append('паспорт: ' + fname)

# ---------- экспорт ----------
bpy.ops.object.select_all(action='SELECT')
bpy.context.view_layer.objects.active = ao
bpy.ops.export_scene.fbx(filepath=out, use_selection=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z',
                         axis_up='Y', object_types={'ARMATURE', 'MESH'}, add_leaf_bones=False, bake_anim=False,
                         mesh_smooth_type='FACE', use_armature_deform_only=False)
if 'blend' in opt:
    bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(opt['blend']))
print('РИГ: ' + '\nРИГ: '.join(REP))

# ---------- проверочные кадры ----------
if 'shots' in opt:
    pre = os.path.abspath(opt['shots'])
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_WORKBENCH'
    sh = sc.display.shading
    sh.light = 'STUDIO'; sh.color_type = 'OBJECT'; sh.show_cavity = True
    sh.background_type = 'VIEWPORT'; sh.background_color = (0.15, 0.16, 0.18)
    sc.render.resolution_x = sc.render.resolution_y = 700
    ao.hide_render = True; ao.hide_viewport = True
    cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); sc.collection.objects.link(cam); sc.camera = cam
    cam.data.type = 'ORTHO'; cam.data.clip_end = 100; cam.data.ortho_scale = 2.3
    s35, c35 = math.sin(math.radians(35)), math.cos(math.radians(35))
    DIR = {'front': (0, -1, 0), '34f': (-s35, -c35, 0.15), 'side': (-1, 0, 0), 'back': (0, 1, 0), '34b': (-s35, c35, 0.2)}

    def shot(name, views):
        for v in views:
            e = Vector(DIR[v]).normalized(); c_ = Vector((0, 0, 0.95))
            cam.location = c_ + e * 10
            cam.rotation_euler = (c_ - cam.location).to_track_quat('-Z', 'Y').to_euler()
            sc.render.filepath = '%s_%s_%s.png' % (pre, name, v)
            bpy.ops.render.render(write_still=True)

    def pose(rot):
        """Повороты костей в позе: имя → (ось тела 'X'|'Y'|'Z', градусы); пусто — сброс."""
        for pb in ao.pose.bones:
            pb.matrix_basis = Matrix.Identity(4)
        bpy.context.view_layer.update()
        for nm in [b.name for b in arm.bones if b.name in rot]:
            pb = ao.pose.bones[nm]; ax_, dg = rot[nm]
            h = pb.matrix.to_translation()
            pb.matrix = Matrix.Translation(h) @ Matrix.Rotation(math.radians(dg), 4, ax_) @ Matrix.Translation(-h) @ pb.matrix
            bpy.context.view_layer.update()

    COL = {'голова': (0.85, 0.3, 0.3, 1), 'шея': (0.9, 0.75, 0.3, 1), 'хребет': (0.4, 0.7, 0.4, 1), 'Сердце': (0.9, 0.5, 0.2, 1),
           'Руки': (0.3, 0.55, 0.9, 1), 'Ноги': (0.65, 0.4, 0.85, 1)}
    for sl in ORDER:
        parts[sl][0].color = COL[sl]
    shot('narezka', ['front', '34f', 'side', 'back', '34b'])
    for sl in ORDER:
        parts[sl][0].color = (0.72, 0.72, 0.72, 1)
    # шаг и замах: бедро и голень, рука вперёд и в сторону, предплечье согнуто, корпус и голова повёрнуты
    pose({'бедро': ('X', -40), 'голень': ('X', 55), 'бедро.L': ('X', 25), 'плечо': ('X', -60), 'предплечье': ('X', -50),
          'плечо.L': ('Y', -70), 'грудная клетка': ('Z', 20), 'шея': ('X', 15), 'голова': ('Z', 35)})
    shot('poza', ['front', '34f', 'side', 'back', '34b'])
    pose({})
