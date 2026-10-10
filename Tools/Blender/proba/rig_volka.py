"""РИГ И НАРЕЗКА ВОЛКА-МЕША (ТЗ `Docs/models/TZ-2026-10-10-rig-volka.md`, спека `2026-10-09-chistyj-vid-meshem.md`).

Вход — доведённый меш (`dovodka_volka.py`, проход 6: `volk_b7.obj`) и скелет графа (`Docs/models/rig/volk-skelet.json`).
Выход — FBX: арматура ровно по скелету (57 костей + `челюсть`) и шесть объектов по слотам.

  1. ПОСАДКА: меш сдвигается вдоль тела так, чтобы ноги легли на кости (перед — по `пясть`, зад — по гнезду `Ноги`).
     Масштаб не трогается: меш уже в метрах листа, длина выправлена в доводке.
  2. ПАСТЬ: морда режется плоскостью по борозде рта от носа до угла рта; в разрез встаёт полость (нёбо — вверх, дно —
     вниз, задняя стенка между ними), зубы — закрытые пирамидки в дёснах, язык на дне. Поза привязки — пасть закрыта.
  3. ВЕСА: несущие кости — автоматом Blender (bone heat), затем правила: голова жёсткая, нижняя челюсть на `челюсть`
     (щека за углом рта делится полосой), плюсна и лапа — на `голень` (ниже скакательного в графе кости нет), стороны
     не смешиваются, веса симметричны, до четырёх костей на вершину.
  4. НАРЕЗКА: грань идёт в слот, чьи кости весят в ней больше; мелкие островки и зубцы границы приглаживаются. Вершины
     шва у соседних объектов совпадают по месту и весам; затенение плоское — нормаль у грани своя, ступени на шве нет.

  blender -b --factory-startup -P rig_volka.py -- меш.obj скелет.json выход.fbx [--blend файл.blend] [--shots префикс]
Печатает отчёт «РИГ: …»: посадка, расхождения меша со скелетом, треугольники по объектам, вершины по костям.
"""
import sys, math, json, os
import bpy, bmesh
from mathutils import Vector, Matrix, kdtree

a = sys.argv[sys.argv.index('--') + 1:]
src, skp, out = a[0], a[1], os.path.abspath(a[2])
opt = {}
i = 3
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.wm.obj_import(filepath=src, forward_axis='NEGATIVE_Z', up_axis='Y')
ob = [o for o in bpy.data.objects if o.type == 'MESH'][0]
bpy.context.view_layer.objects.active = ob; ob.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
me = ob.data
n_orig = len(me.polygons)
sk = json.load(open(skp, encoding='utf-8'))
BN = {b['name']: b for b in sk['bones']}
SOCK = {}
for s_ in sk['sockets']:
    SOCK.setdefault(s_['name'], []).append(Vector(s_['at']))


def sm(t):
    t = min(1.0, max(0.0, t)); return t * t * (3 - 2 * t)


def leg_centre(z, ylo, yhi, xmin=0.085):
    """Середина сечения ноги стороны +X на высоте z (по рёбрам, пересекающим высоту)."""
    ys = []
    for e in me.edges:
        p, q = me.vertices[e.vertices[0]].co, me.vertices[e.vertices[1]].co
        if (p.z - z) * (q.z - z) < 0:
            c = p + (q - p) * ((z - p.z) / (q.z - p.z))
            if c.x > xmin and ylo < c.y < yhi:
                ys.append(c.y)
    return (min(ys) + max(ys)) / 2


def on_bone(name, z):
    h, t = Vector(BN[name]['head']), Vector(BN[name]['tail'])
    return (h + (t - h) * ((z - h.z) / (t.z - h.z))).y


# ---------- 1. ПОСАДКА ----------
yn = min(v.co.y for v in me.vertices)
mf, mh = leg_centre(0.30, yn + 0.45, yn + 0.95), leg_centre(0.25, yn + 1.50, yn + 2.0)
bf, bh = on_bone('пясть', 0.30), sorted(SOCK['Ноги'], key=lambda p: abs(p.z - 0.25))[0].y
shift = ((bf - mf) + (bh - mh)) / 2
me.transform(Matrix.Translation((0, shift, 0)))
yn += shift
REP = ['посадка: сдвиг вдоль тела %+.3f м (нос на Y %.3f); после сдвига перед %+.3f, зад %+.3f м от кости'
       % (shift, yn, mf + shift - bf, mh + shift - bh)]

# ---------- 2. ПАСТЬ ----------
# борозда рта образца: под носом на 1.083 м, у угла рта (0.22 м от носа, под задним краем глаза) — 1.106 м
yA, zA, yB, zB = yn + 0.02, 1.083, yn + 0.225, 1.106
SL = (zB - zA) / (yB - yA)
P0 = Vector((0, yA, zA)); PN = Vector((0, -SL, 1)).normalized()
NC = yn + 0.22                      # угол рта
JOINT = Vector((0, yn + 0.325, 1.155))   # челюстной сустав: под передним краем основания уха (критик 10.10: выше на 2 см)
H_UP, H_LO, INSET = 0.016, 0.012, 0.03
TT, FF = (0.0, 0.4, 1.0), (0.0, 0.7, 1.0)   # кольца полости: доля пути от губы к оси и доля подъёма


def sd(co):
    return (co - P0).dot(PN)


def zpl(y):
    return zA + (y - yA) * SL


# нижняя челюсть образца — пластина 2.7–3.5 см (пасть у него закрыта, челюсть не лепилась): в открытой пасти читается
# палкой. Низ челюсти опускается на 0.5 см у подбородка и до 1.2 см к середине; губа и угол рта на месте
for v in me.vertices:
    n, s_ = v.co.y - yn, sd(v.co)
    if n < 0.30 and -0.07 < s_ < -0.008:
        v.co -= PN * ((0.005 + 0.007 * sm(n / 0.08)) * sm((-s_ - 0.008) / 0.012) * (1 - sm((n - 0.22) / 0.08)))

bm = bmesh.new(); bm.from_mesh(me)
TAG = bm.verts.layers.int.new('jawtag')          # 1 — на челюсть, 2 — на голову жёстко
faces = [f for f in bm.faces if f.calc_center_median().y < NC]
geom = set(faces)
for f in faces:
    geom.update(f.edges); geom.update(f.verts)
res = bmesh.ops.bisect_plane(bm, geom=list(geom), dist=1e-5, plane_co=P0, plane_no=PN)
cut = [e for e in res['geom_cut'] if isinstance(e, bmesh.types.BMEdge)]
bmesh.ops.split_edges(bm, edges=cut)
bnd = [e for e in bm.edges if e.is_boundary]


def chain(up):
    es = [e for e in bnd if (sd(e.link_faces[0].calc_center_median()) > 0) == up]
    adj = {}
    for e in es:
        for v in e.verts:
            adj.setdefault(v, []).append(e)
    ends = sorted([v for v, l in adj.items() if len(l) == 1], key=lambda v: v.co.x)
    assert len(ends) == 2, 'разрез рта — не одна цепь: концов %d' % len(ends)
    ch, prev = [ends[0]], None
    while ch[-1] is not ends[1]:
        e = [e for e in adj[ch[-1]] if e is not prev][0]
        ch.append(e.other_vert(ch[-1])); prev = e
    return ch


UP, LO = chain(True), chain(False)
assert UP[0] is LO[0] and UP[-1] is LO[-1], 'углы рта у губ не общие'
yF, yC = min(v.co.y for v in UP), (UP[0].co.y + UP[-1].co.y) / 2
new_verts = []


def pocket(ch, sign, H, tag):
    rows = []
    for k_, v in enumerate(ch):
        p = v.co.copy()
        yc = min(yC, max(yF + INSET, p.y))
        c = Vector((0, yc, zpl(yc)))
        row = [v]
        for t, f in zip(TT[1:], FF[1:]):
            nv = bm.verts.new(p.lerp(c, t) + PN * (sign * H * f)); nv[TAG] = tag
            row.append(nv); new_verts.append(nv)
        if 0 < k_ < len(ch) - 1:
            v[TAG] = tag
        rows.append(row)
    for r0, r1 in zip(rows, rows[1:]):
        for k_ in range(len(TT) - 1):
            bm.faces.new((r0[k_], r1[k_], r1[k_ + 1], r0[k_ + 1]))
    return rows


RU, RL = pocket(UP, 1, H_UP, 2), pocket(LO, -1, H_LO, 1)
for ru, rl in ((RU[0], RL[0]), (RU[-1], RL[-1])):      # задняя стенка: нёбо ↔ дно у углов рта
    bm.faces.new((ru[0], rl[1], ru[1]))
    for k_ in range(1, len(TT) - 1):
        bm.faces.new((ru[k_], rl[k_], rl[k_ + 1], ru[k_ + 1]))
bmesh.ops.remove_doubles(bm, verts=new_verts, dist=1e-5)
new_verts = [v for v in new_verts if v.is_valid]
# ось нёба и дна у задней стенки слилась попарно; стенка у оси — ребро «нёбо—дно» общее для двух половин
bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
# КОЖА ДЛЯ ПРОВЕРКИ ЗУБОВ (критик 10.10: в закрытой пасти зубы прокалывали губу белыми точками): зуб обязан сидеть под
# наружной кожей не мельче 2 мм; ближайшая грань полости не в счёт — в полости зубу и место
from mathutils.bvhtree import BVHTree
bm.faces.ensure_lookup_table()
SKIN_BVH = BVHTree.FromBMesh(bm)
POCK = [all(v[TAG] in (1, 2) or v is UP[0] or v is UP[-1] for v in f.verts) and any(v in set(new_verts) for v in f.verts) for f in bm.faces]


def hidden(p, margin=0.002):
    co, no, idx, d = SKIN_BVH.find_nearest(p)
    return POCK[idx] or ((p - co).dot(no) < 0 and d >= margin)




def lip_point(ch, s_arc, side):
    """Точка губы на дуге s_arc от середины переда в сторону side (±1) и единичное направление вдоль губы."""
    mid = min(range(len(ch)), key=lambda k_: (abs(ch[k_].co.x), ch[k_].co.y))
    k_, left = mid, s_arc
    step = 1 if ch[-1].co.x * side > 0 else -1
    while 0 <= k_ + step < len(ch):
        d = (ch[k_ + step].co - ch[k_].co).length
        if d >= left:
            dr = (ch[k_ + step].co - ch[k_].co).normalized()
            return ch[k_].co + dr * left, dr
        left -= d; k_ += step
    return ch[k_].co.copy(), Vector((0, 1, 0))


def surf(p, sign, H, inset):
    """Точка на своде полости в `inset` м от губы внутрь."""
    yc = min(yC, max(yF + INSET, p.y)); c = Vector((0, yc, zpl(yc)))
    t = min(0.5, inset / max(1e-6, (c - p).length))
    f = FF[1] * t / TT[1] if t <= TT[1] else FF[1] + (FF[2] - FF[1]) * (t - TT[1]) / (TT[2] - TT[1])
    return p.lerp(c, t) + PN * (sign * H * f), (c - p).normalized()


def tooth(ch, s_arc, sign, H, tag, length, along, across, inset=0.007):
    """Зуб — закрытая пирамида: основание утоплено в десну на 2 мм, вершина — к противоположной челюсти и чуть внутрь."""
    for side in (1, -1):
        p, dr = lip_point(ch, s_arc, side)
        ins, ln = inset, length
        for _ in range(40):                              # глубже от губы, затем короче — пока зуб не спрячется под кожу
            b, inw = surf(p, sign, H, ins)
            b = b + PN * (sign * 0.002)
            tip = b - PN * (sign * (ln + 0.002)) + inw * (0.12 * ln)
            cr = dr.cross(PN).normalized()
            q = [b + dr * (sx * along / 2) + cr * (sy * across / 2) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
            if all(hidden(x) for x in q + [tip, (b + tip) / 2]):
                break
            if ins < 0.016: ins += 0.0015
            else: ln *= 0.93
        if side == 1:
            TEETH.append((sign, s_arc, ins, ln))
        vs = [bm.verts.new(x) for x in q] + [bm.verts.new(tip)]
        for v in vs:
            v[TAG] = tag
        fs = [bm.faces.new((vs[k_], vs[(k_ + 1) % 4], vs[4])) for k_ in range(4)] + [bm.faces.new((vs[3], vs[2], vs[1], vs[0]))]
        bmesh.ops.recalc_face_normals(bm, faces=fs)


TEETH = []
#        дуга от середины, длина, вдоль губы, поперёк
UPPER = ((0.008, 0.007, 0.006, 0.005), (0.021, 0.008, 0.006, 0.005), (0.046, 0.036, 0.015, 0.013),
         (0.078, 0.008, 0.012, 0.006), (0.104, 0.009, 0.014, 0.007), (0.136, 0.011, 0.020, 0.008))
LOWER = ((0.007, 0.006, 0.006, 0.005), (0.019, 0.007, 0.006, 0.005), (0.031, 0.029, 0.013, 0.012),
         (0.064, 0.007, 0.011, 0.006), (0.090, 0.008, 0.013, 0.007), (0.120, 0.010, 0.018, 0.008))
for s_arc, ln, al, ac in UPPER:
    tooth(UP, s_arc, 1, H_UP, 2, ln, al, ac)
for s_arc, ln, al, ac in LOWER:
    tooth(LO, s_arc, -1, H_LO, 1, ln, al, ac)
# язык: клин на дне, от задней стенки почти до резцов; верх чуть ниже линии смыкания
y0t, y1t = yF + 0.035, yC - 0.008
tv = []
for y, hw in ((y0t, 0.006), ((y0t + y1t) / 2, 0.011), (y1t, 0.011)):   # уже и ниже зубного ряда (критик 10.10: плита во всю челюсть — «второе дно»)
    zt, zb = zpl(y) - 0.004, zpl(y) - (H_LO + 0.002)
    tv.append([bm.verts.new((sx * hw * kx, y, z)) for sx, kx, z in ((-1, 1, zt - 0.003), (1, 1, zt - 0.003), (1, 0.8, zb), (-1, 0.8, zb))])
tf = [bm.faces.new((tv[0][3], tv[0][2], tv[0][1], tv[0][0])), bm.faces.new((tv[2][0], tv[2][1], tv[2][2], tv[2][3]))]
for r0, r1 in zip(tv, tv[1:]):
    tf += [bm.faces.new((r0[k_], r0[(k_ + 1) % 4], r1[(k_ + 1) % 4], r1[k_])) for k_ in range(4)]
for r in tv:
    for v in r:
        v[TAG] = 1
bmesh.ops.recalc_face_normals(bm, faces=tf)
bmesh.ops.triangulate(bm, faces=bm.faces[:], quad_method='BEAUTY', ngon_method='BEAUTY')
bm.faces.ensure_lookup_table()
EXTRA = bm.faces.layers.int.new('extra')         # зубы и язык: отдельные острова, в автовеса не идут
bm.faces.ensure_lookup_table()
# острова: всё, что не связано с кожей
bm.verts.ensure_lookup_table()
seen, stack = set(), [min(bm.verts, key=lambda v: v.co.z)]
while stack:
    u = stack.pop()
    if u in seen:
        continue
    seen.add(u); stack.extend(e.other_vert(u) for e in u.link_edges)
for f in bm.faces:
    f[EXTRA] = 0 if f.verts[0] in seen else 1
n_extra = sum(f[EXTRA] for f in bm.faces)
n_lip = len(UP)
bm.to_mesh(me); bm.free()
for p in me.polygons:
    p.use_smooth = False
REP.append('зубы (сторона +X; дуга от середины губы → отступ от губы, длина): ' + ', '.join('%s %.3f → %.4f, %.3f' % ('верх' if sg > 0 else 'низ', a_, i_, l_) for sg, a_, i_, l_ in TEETH))
REP.append('пасть: губа %d вершин, разрез и полость +%d тр, зубы и язык %d тр; сустав челюсти (%.3f %.3f %.3f)'
           % (n_lip, len(me.polygons) - n_extra - n_orig, n_extra, *JOINT))

# проверка кожи (без зубов и языка): замкнута, без самопересечений
from mathutils.bvhtree import BVHTree
bmc = bmesh.new(); bmc.from_mesh(me)
layc = bmc.faces.layers.int['extra']
bmesh.ops.delete(bmc, geom=[f for f in bmc.faces if f[layc]], context='FACES')
bmesh.ops.delete(bmc, geom=[v for v in bmc.verts if not v.link_faces], context='VERTS')
bmc.faces.ensure_lookup_table()
bvh = BVHTree.FromBMesh(bmc)
inter = [(i_, j_) for i_, j_ in bvh.overlap(bvh) if i_ < j_
         and not set(v.index for v in bmc.faces[i_].verts) & set(v.index for v in bmc.faces[j_].verts)
         and not any((u.co - w_.co).length < 1e-6 for u in bmc.faces[i_].verts for w_ in bmc.faces[j_].verts)]
REP.append('кожа с полостью: граней %d, неманифолд %d, граничных %d, самопересечений %d%s'
           % (len(bmc.faces), sum(1 for e in bmc.edges if not e.is_manifold), sum(1 for e in bmc.edges if e.is_boundary), len(inter),
              ''.join(' (%.3f %.3f %.3f)' % tuple(bmc.faces[i_].calc_center_median()) for i_, _ in inter[:6])))
bmc.free()

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
eb = arm.edit_bones.new('челюсть')
eb.head, eb.tail = JOINT, Vector((0, yn + 0.03, 1.062))
eb.align_roll(Vector((0, 0, 1))); eb.parent = arm.edit_bones['голова']
bpy.ops.object.mode_set(mode='OBJECT')

# несущие — в автовеса; мышечные и покровные без весов (ТЗ §1 п.2), `челюсть` — правилом
MAIN = ['хребет', 'грудной', 'крестец', 'шея', 'голова', 'хвост', 'хвост_кисть']
LIMB = ['лопатка', 'плечо', 'предплечье', 'пясть', 'бедро', 'голень', 'пятка']
# помощники автовесов: без них грудину и брюхо забирали лопатки и бёдра (они ближе к поверхности, чем ось корпуса) —
# грудь между ногами уезжала бы за передней ногой. Их вес сворачивается в несущую кость-родителя
HELP = {'грудь': 'грудной', 'загривок': 'грудной', 'живот': 'хребет'}
DEF = MAIN + LIMB + [n + '.L' for n in LIMB] + list(HELP)
for b in arm.bones:
    b.use_deform = b.name in DEF

# ---------- 3. ВЕСА ----------
# автовеса — по коже без зубов и языка (закрытые острова внутри полости для них не видны)
skin = ob.copy(); skin.data = me.copy(); bpy.context.scene.collection.objects.link(skin)
bmk = bmesh.new(); bmk.from_mesh(skin.data)
lay = bmk.faces.layers.int['extra']
bmesh.ops.delete(bmk, geom=[f for f in bmk.faces if f[lay]], context='FACES')
bmesh.ops.delete(bmk, geom=[v for v in bmk.verts if not v.link_faces], context='VERTS')
bmk.to_mesh(skin.data); bmk.free()
bpy.ops.object.select_all(action='DESELECT')
skin.select_set(True); ao.select_set(True); bpy.context.view_layer.objects.active = ao
bpy.ops.object.parent_set(type='ARMATURE_AUTO')
gname = {g.index: g.name for g in skin.vertex_groups}
kd = kdtree.KDTree(len(skin.data.vertices))
for v in skin.data.vertices:
    kd.insert(v.co, v.index)
kd.balance()
heat = []
for v in skin.data.vertices:
    h = {}
    for g in v.groups:
        if g.weight > 1e-4:
            nm = HELP.get(gname[g.group], gname[g.group]); h[nm] = h.get(nm, 0) + g.weight
    heat.append(h)
empty = sum(1 for h in heat if not h)
assert empty < 0.2 * len(heat), 'автовеса не сошлись: без весов %d из %d вершин' % (empty, len(heat))
tagv = [d.value for d in me.attributes['jawtag'].data]
W = []
NJ = yn + 0.32
for v in me.vertices:
    co = v.co
    w = dict(heat[kd.find(co)[1]])
    if not w:                                      # вершина, не видимая костям (глубина полости): от ближайшей со своими весами
        for _, j, _ in kd.find_n(co, 12):
            if heat[j]:
                w = dict(heat[j]); break
    n = co.y - yn
    # голова жёсткая: морда, глаза, уши
    hw = max(1 - sm((n - 0.27) / 0.10), sm((co.z - 1.37) / 0.04) * (1 - sm((n - 0.37) / 0.05)))
    w = {k_: x * (1 - hw) for k_, x in w.items()}
    w['голова'] = w.get('голова', 0) + hw
    # нижняя челюсть
    s_ = sd(co)
    # у угла рта губы стянуты перепонкой (критик 10.10: при 35° виден сквозной клин до самого угла): на последних 4.5 см
    # перед углом верх и низ сходятся весами к половине — раствор растёт от угла плавно
    web = 0.5 * sm((NC - co.y) / 0.045)
    if tagv[v.index] == 1: jw = 0.5 + web
    elif tagv[v.index] == 2: jw = 0.5 - web
    elif co.y < NC: jw = 1.0 if s_ < -1e-4 else 0.0
    else: jw = sm((-s_ + 0.015) / 0.03) * (1 - sm((co.y - NC) / (NJ - NC))) * sm((s_ + 0.11) / 0.04)
    if tagv[v.index] in (1, 2): w = {}
    w = {k_: x * (1 - jw) for k_, x in w.items()}
    if jw > 0: w['челюсть'] = jw
    if tagv[v.index] in (1, 2) and jw < 1: w['голова'] = 1 - jw
    # плюсна и задняя лапа — жёстко на `голень`: ниже скакательного кости в графе нет, блоки поля висят там же
    if co.y > 0.2 and abs(co.x) > 0.04:
        sfx = '.L' if co.x > 0 else ''
        t = sm((0.44 - co.z) / 0.08)
        mv = w.get('пятка' + sfx, 0) * t
        if mv:
            w['пятка' + sfx] -= mv; w['голень' + sfx] = w.get('голень' + sfx, 0) + mv
    # у средней линии корпуса (грудина, холка, круп) конечности не тянут: вес ног гаснет к оси тела и уходит костям корпуса
    f = max(sm((abs(co.x) - 0.03) / 0.07), sm((0.64 - co.z) / 0.06))
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
        else:
            nm = 'грудной' if co.y < -0.05 else ('крестец' if co.y > 0.30 else 'хребет')
            w[nm] = w.get(nm, 0) + mv
    # стороны не смешиваются
    if abs(co.x) > 0.02:
        for nm in LIMB:
            wrong, right = (nm, nm + '.L') if co.x > 0 else (nm + '.L', nm)
            if wrong in w:
                w[right] = w.get(right, 0) + w.pop(wrong)
    W.append(w)


def swap(w):
    return {(k_[:-2] if k_.endswith('.L') else (k_ + '.L' if k_ in LIMB else k_)): x for k_, x in w.items()}


kdm = kdtree.KDTree(len(me.vertices))
for v in me.vertices:
    kdm.insert(v.co, v.index)
kdm.balance()
asym = 0
for v in me.vertices:
    if v.co.x > 1e-5:
        # у губ верхняя и нижняя вершины стоят в одной точке — пара ищется среди своих по метке челюсти
        hit = [j for _, j, d in kdm.find_n((-v.co.x, v.co.y, v.co.z), 4) if d < 1e-4 and tagv[j] == tagv[v.index]]
        if hit: W[hit[0]] = swap(W[v.index])
        else: asym += 1
    elif abs(v.co.x) <= 1e-5:
        w, ws = W[v.index], swap(W[v.index])
        W[v.index] = {k_: (w.get(k_, 0) + ws.get(k_, 0)) / 2 for k_ in set(w) | set(ws)}
for k_, w in enumerate(W):
    top = sorted(w.items(), key=lambda kv: -kv[1])[:4]
    s_ = sum(x for _, x in top)
    W[k_] = {nm: x / s_ for nm, x in top if x / s_ > 0.01}
    s_ = sum(W[k_].values()); W[k_] = {nm: x / s_ for nm, x in W[k_].items()}
bpy.data.objects.remove(skin, do_unlink=True)

# ---------- 4. НАРЕЗКА ----------
SLOT = {'голова': 'голова', 'челюсть': 'голова', 'шея': 'шея', 'хребет': 'хребет', 'грудной': 'хребет', 'крестец': 'хребет',
        'хвост': 'Хвост', 'хвост_кисть': 'Хвост'}
for nm in ('лопатка', 'плечо', 'предплечье', 'пясть'):
    SLOT[nm] = SLOT[nm + '.L'] = 'Руки'
for nm in ('бедро', 'голень', 'пятка'):
    SLOT[nm] = SLOT[nm + '.L'] = 'Ноги'
ORDER = ['голова', 'шея', 'хребет', 'Руки', 'Ноги', 'Хвост']
extra = [d.value for d in me.attributes['extra'].data]
F = [tuple(p.vertices) for p in me.polygons]
fslot = []
for k_, f in enumerate(F):
    sc = {}
    for vi in f:
        for nm, x in W[vi].items():
            sc[SLOT[nm]] = sc.get(SLOT[nm], 0) + x
    fslot.append('голова' if extra[k_] or all(me.vertices[vi].co.y - yn < 0.27 for vi in f) else max(sc, key=sc.get))
edge_f = {}
for k_, f in enumerate(F):
    for x, y in ((f[0], f[1]), (f[1], f[2]), (f[2], f[0])):
        edge_f.setdefault((min(x, y), max(x, y)), []).append(k_)
nb = [[] for _ in F]
for fs in edge_f.values():
    if len(fs) == 2:
        nb[fs[0]].append(fs[1]); nb[fs[1]].append(fs[0])
for _ in range(3):                                   # зубцы границы: грань, у которой двое из трёх соседей чужие, уходит к ним
    for k_ in range(len(F)):
        if extra[k_]:
            continue
        o = [fslot[j] for j in nb[k_] if fslot[j] != fslot[k_]]
        if len(o) >= 2 and o.count(o[0]) >= 2:
            fslot[k_] = o[0]
done = set()                                         # островки: кусок слота меньше 12 граней уходит к соседу
for k_ in range(len(F)):
    if k_ in done or extra[k_]:
        continue
    comp, st = [], [k_]
    while st:
        u = st.pop()
        if u in done:
            continue
        done.add(u); comp.append(u)
        st.extend(j for j in nb[u] if fslot[j] == fslot[k_] and j not in done)
    if len(comp) < 12:
        o = [fslot[j] for u in comp for j in nb[u] if fslot[j] != fslot[k_]]
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

# ---------- отчёт ----------
allv = [x for x in V]
tip_ear = max(allv, key=lambda p: p.z)
tail_end = min((p for p in allv if abs(p.x) < 0.08 and p.y > bh + 0.2), key=lambda p: p.z)
hock = max((p for p in allv if p.x > 0.13 and 0.28 < p.z < 0.50 and 0.3 < p.y < bh + 0.16), key=lambda p: p.y)
back = max(p.z for p in allv if abs(p.y - 0.0) < 0.15)


def row(what, meshp, bonep, note=''):
    d = Vector(meshp) - Vector(bonep)
    REP.append('  %-34s меш (%+.3f %+.3f %+.3f)  скелет (%+.3f %+.3f %+.3f)  Δ вдоль %+.3f, вверх %+.3f  %s'
               % (what, *meshp, *bonep, d.y, d.z, note))


REP.append('расхождения меша со скелетом (Δ = меш − скелет, м):')
row('нос ↔ гнездо `нос`', (0, yn, 1.135), SOCK['нос'][0], 'гнездо — центр мочки, не кончик')
row('глаз ↔ гнездо `глаза`', (0.089, yn + 0.21, 1.25), [abs(SOCK['глаза'][0].x)] + list(SOCK['глаза'][0])[1:])
row('кончик уха ↔ гнездо `уши`', tip_ear, [abs(SOCK['уши'][0].x)] + list(SOCK['уши'][0])[1:], 'гнездо — основание уха')
row('перёд, z 0.30 ↔ `пясть`', (0.145, mf + shift, 0.30), (0.145, bf, 0.30))
row('зад, z 0.25 ↔ гнездо `Ноги`', (0.13, mh + shift, 0.25), (0.13, bh, 0.25))
row('скакательный (зад. край) ↔ хвост `пятка`', hock, [abs(BN['пятка']['tail'][0])] + BN['пятка']['tail'][1:])
row('кончик хвоста ↔ хвост `хвост_кисть`', tail_end, BN['хвост_кисть']['tail'], 'кость — ось, без радиуса кисти')
REP.append('  верх спины над серединой `хребет`: %.3f м (кость на %.3f)' % (back, BN['хребет']['head'][2]))
tot = 0
REP.append('треугольники по объектам:')
for sl in ORDER:
    n = len(parts[sl][0].data.polygons); tot += n
    REP.append('  %-7s %5d тр, %4d вершин' % (sl, n, len(parts[sl][1])))
REP.append('  всего   %5d тр (зубы и язык %d)' % (tot, n_extra))
cnt = {}
for w in W:
    for nm in w:
        cnt[nm] = cnt.get(nm, 0) + 1
REP.append('кости с весами (%d из %d; вершин на кость): ' % (len(cnt), len(arm.bones))
           + ', '.join('%s %d' % (nm, cnt[nm]) for nm in [b.name for b in arm.bones] if nm in cnt))
REP.append('кости без весов: ' + ', '.join(b.name for b in arm.bones if b.name not in cnt))
REP.append('несимметричных вершин при зеркале весов: %d' % asym)
# швы: вершины соседних объектов на шве совпадают по месту и весам по построению (одна вершина исходного меша)
seam = {}
for sl in ORDER:
    for vi in parts[sl][1]:
        seam.setdefault(vi, []).append(sl)
REP.append('вершин на швах: %d (общих у двух и более объектов)' % sum(1 for l in seam.values() if len(l) > 1))

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
    sh.light = 'STUDIO'; sh.color_type = 'SINGLE'; sh.single_color = (0.72, 0.72, 0.72); sh.show_cavity = True
    sh.background_type = 'VIEWPORT'; sh.background_color = (0.15, 0.16, 0.18)
    sc.render.resolution_x = sc.render.resolution_y = 700
    ao.hide_render = True; ao.hide_viewport = True
    cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); sc.collection.objects.link(cam); sc.camera = cam
    cam.data.type = 'ORTHO'; cam.data.clip_end = 100
    s35, c35 = math.sin(math.radians(35)), math.cos(math.radians(35))
    DIR = {'side': (-1, 0, 0), '34f': (-s35, -c35, 0.15), 'front': (0, -1, 0), 'low': (-0.45, -0.8, -0.4), 'hi': (-0.45, -0.8, 0.45),
           '34b': (-s35, c35, 0.2), 'back': (0, 1, 0)}

    def shot(name, c, fr, views):
        cam.data.ortho_scale = fr
        for v in views:
            e = Vector(DIR[v]).normalized(); c_ = Vector(c)
            cam.location = c_ + e * 10
            cam.rotation_euler = (c_ - cam.location).to_track_quat('-Z', 'Y').to_euler()
            sc.render.filepath = '%s_%s_%s.png' % (pre, name, v)
            bpy.ops.render.render(write_still=True)

    def pose(rot):
        """Повороты костей вокруг оси X тела (градусы, + — конец кости вниз/назад) в позе; пусто — сброс."""
        for pb in ao.pose.bones:
            pb.matrix_basis = Matrix.Identity(4)
        bpy.context.view_layer.update()
        for nm in [b.name for b in arm.bones if b.name in rot]:      # от корня к листьям
            pb = ao.pose.bones[nm]
            h = pb.matrix.to_translation()
            pb.matrix = Matrix.Translation(h) @ Matrix.Rotation(math.radians(rot[nm]), 4, 'X') @ Matrix.Translation(-h) @ pb.matrix
            bpy.context.view_layer.update()

    COL = {'голова': (0.85, 0.3, 0.3, 1), 'шея': (0.9, 0.75, 0.3, 1), 'хребет': (0.4, 0.7, 0.4, 1), 'Руки': (0.3, 0.55, 0.9, 1),
           'Ноги': (0.65, 0.4, 0.85, 1), 'Хвост': (0.3, 0.8, 0.8, 1)}
    for sl in ORDER:
        parts[sl][0].color = COL[sl]
    sh.color_type = 'OBJECT'
    shot('narezka', (0, 0, 0.8), 2.8, ['side', '34f', '34b', 'front', 'back'])
    sh.color_type = 'SINGLE'
    hc = (0, yn + 0.20, 1.20)
    shot('zakryta', hc, 0.55, ['side', '34f', 'low'])
    pose({'челюсть': 35})
    shot('past35', hc, 0.55, ['side', '34f', 'front', 'low', 'hi'])
    pose({'голова': -20, 'шея': -15, 'плечо': 25, 'предплечье': -35, 'плечо.L': -20, 'бедро': -25, 'голень': 30,
          'бедро.L': 20, 'хвост': -40, 'хвост_кисть': -25, 'челюсть': 20})
    shot('poza', (0, 0, 0.8), 2.8, ['side', '34f', '34b'])
    pose({})
    shot('pokoj', (0, 0, 0.8), 2.8, ['side', '34f'])
