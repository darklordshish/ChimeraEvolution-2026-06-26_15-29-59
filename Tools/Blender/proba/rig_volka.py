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

  blender -b --factory-startup -P rig_volka.py -- меш.obj скелет.json выход.fbx [--muzzle градусы — как в доводке] [--shvy 1 — швы кусков, 2 — и голова по органам] [--pasporta папка] [--blend файл.blend] [--shots префикс]
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
# борозда рта образца (до поворота морды в доводке): под носом, 0.02 м от носа, на 1.083 м; у угла рта (0.225 м от
# носа, под задним краем глаза) — 1.106 м. Морда в доводке опущена на `--muzzle` градусов вокруг переносицы — точки
# борозды, угол рта и сустав пересчитываются тем же поворотом (`--muzzle` здесь обязан совпадать с доводкой)
MZ = math.radians(float(opt.get('muzzle', 0.0)))


def muzzle(n, z):
    """Точка «м от носа, высота» исходной головы → после поворота морды (доводка, шаг «стоп»)."""
    t = sm((0.23 - n) / 0.11)
    c, s_ = math.cos(-MZ * t), math.sin(-MZ * t)
    ry, rz = n - 0.20, z - 1.20
    return 0.20 + ry * c + rz * s_, 1.20 - ry * s_ + rz * c


N0 = muzzle(0.0, 1.135)[0]                 # куда уехал кончик носа: от него теперь меряется «м от носа»
(nA, zA), (nB, zB) = muzzle(0.02, 1.083), muzzle(0.225, 1.106)
yA, yB = yn + nA - N0, yn + nB - N0
SL = (zB - zA) / (yB - yA)
P0 = Vector((0, yA, zA)); PN = Vector((0, -SL, 1)).normalized()
NC = yn + 0.22 - N0                 # угол рта
JOINT = Vector((0, yn + 0.325 - N0, 1.155))   # челюстной сустав: под передним краем основания уха (критик 10.10: выше на 2 см)
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
PART = bm.faces.layers.int.new('part')           # 0 — кожа, 1 — полость рта и язык, 2 — зубы: свой цвет — свой объект
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
# нижняя челюсть уже верхней дуги (критик 10.10: передняя треть шире морды, «лопата»): всё ниже разреза перед углом рта
# сужается на 17 % у подбородка, к 13 см от носа — ноль; нижняя губа и клыки уходят под верхнюю
lo_set = set(LO[1:-1])
for v in bm.verts:
    if v in lo_set or (v.co.y < NC and sd(v.co) < -1e-5):
        v.co.x *= 1 - 0.17 * (1 - sm((v.co.y - yn - 0.05) / 0.08))
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


for k_, f in enumerate(bm.faces):
    f[PART] = 1 if POCK[k_] else 0


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
        for f in fs:
            f[PART] = 2


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
for f in tf:
    f[PART] = 1
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

# ---------- 3а. ШВЫ КУСКОВ (пилот Б, спека `2026-10-01-konstruktor-detaley.md` §3, §7; условия механик 10.10) ----------
# Голова и хвост уезжают на чужое тело КУСКАМИ, поэтому их край — шов стандартного типа: плоское кольцо поперёк кости,
# одинаковое число вершин у всех видов (12 у шеи, 8 у хвоста), вершины считаются от «верха» кольца в сторону +X.
# `--shvy 1`:
#   · «основание черепа»: кольцо 12 на конце кости `шея`, ПОД гривой. Грива остаётся куску головы юбкой: кожа режется по
#     нижней кромке гривы (плоскость холка → низ манишки, по рёбрам меша — кромка рваная, как шерсть). Под юбкой у тела
#     встаёт скрытая шея — конус от кромки к кольцу; у куска головы — такая же изнанка, чтобы юбка не была листом без
#     толщины. Юбка с шеей носителя не сшивается: общие у куска и тела только 12 вершин кольца;
#   · «корень хвоста»: кольцо 8 поперёк кости `хвост` там, где хвост отходит от крупа; рваная граница стягивается в 8 точек.
SHVY = int(float(opt.get('shvy', 0)))
SEAMS = {}
if SHVY:
    bm = bmesh.new(); bm.from_mesh(me)
    EXF = bm.faces.layers.int['extra']
    VP = bm.verts.layers.int.new('piece')        # 0 — тело, 1 — кусок головы, 2 — кусок хвоста
    RG = bm.verts.layers.int.new('ring')         # 1+k — кольцо шеи, 51+k — кольцо у основания шеи (скрытое), 101+k — кольцо хвоста
    FC = bm.faces.layers.int.new('forced')       # 3 — скрытая шея тела
    for f in bm.faces:
        f[FC] = 0
    for v in bm.verts:
        v[VP] = 0; v[RG] = 0
    bm.faces.ensure_lookup_table(); bm.verts.ensure_lookup_table()
    XU = Vector((1, 0, 0))

    def region(seed_face, ok):
        """Связная область граней от затравки по условию; затем — без зубцов (большинство соседей)."""
        reg, st = set(), [seed_face]
        while st:
            f = st.pop()
            if f in reg or f[EXF] or not ok(f):
                continue
            reg.add(f)
            st.extend(g for e in f.edges for g in e.link_faces if g is not f)
        for _ in range(4):
            for f in bm.faces:
                if f[EXF]:
                    continue
                nbs = [g for e in f.edges for g in e.link_faces if g is not f]
                inn = sum(1 for g in nbs if g in reg)
                if f in reg and inn <= 1: reg.discard(f)
                elif f not in reg and inn >= 2 and ok(f): reg.add(f)
        return reg

    def cut_loop(reg, what):
        """Граница области — одна замкнутая петля; разрезать по ней. Возвращает (петля куска, петля тела) по порядку."""
        E = [e for e in bm.edges if len(e.link_faces) == 2 and (e.link_faces[0] in reg) != (e.link_faces[1] in reg)]
        deg = {}
        for e in E:
            for v in e.verts:
                deg[v] = deg.get(v, 0) + 1
        assert all(d == 2 for d in deg.values()), '%s: граница не простая петля (узлов с ветвлением %d)' % (what, sum(1 for d in deg.values() if d != 2))
        where = [v.co.copy() for v in deg]
        bmesh.ops.split_edges(bm, edges=E)
        res = []
        for inside in (True, False):
            es = [e for e in bm.edges if e.is_boundary and (e.link_faces[0] in reg) == inside
                  and all(any((v.co - q).length < 1e-7 for q in where) for v in e.verts)]
            adj = {}
            for e in es:
                for v in e.verts:
                    adj.setdefault(v, []).append(e)
            v0 = next(iter(adj)); loop, prev = [v0], None
            while True:
                e = [e for e in adj[loop[-1]] if e is not prev][0]
                nx = e.other_vert(loop[-1]); prev = e
                if nx is v0:
                    break
                loop.append(nx)
            assert len(loop) == len(adj), '%s: петель больше одной (%d из %d вершин)' % (what, len(loop), len(adj))
            res.append(loop)
        return res

    def ang(co, c, w):
        d = co - c
        return math.atan2(d.dot(XU), d.dot(w)) % (2 * math.pi)       # 0 — «верх», растёт к +X

    def ordered(loop, c, w):
        """Петля от «верха» в сторону роста угла; углы — неубывающие."""
        a = [ang(v.co, c, w) for v in loop]
        k0 = min(range(len(loop)), key=lambda k_: min(a[k_], 2 * math.pi - a[k_]))
        loop = loop[k0:] + loop[:k0]; a = a[k0:] + a[:k0]
        if a[0] > math.pi: a[0] -= 2 * math.pi
        q = max(2, len(a) // 4)
        if sum(1 for k_ in range(1, q) if a[k_] > math.pi) > q // 2:      # идёт в сторону убывания
            loop = [loop[0]] + loop[:0:-1]; a = [a[0]] + a[:0:-1]
        out = [a[0]]
        for x in a[1:]:
            out.append(max(out[-1], x))
        return loop, out

    def ring(c, w, rx, rw, n, code, piece):
        vs = []
        for k_ in range(n):
            th = 2 * math.pi * k_ / n
            v = bm.verts.new(c + XU * (rx * math.sin(th)) + w * (rw * math.cos(th)))
            v[RG] = code + k_; v[VP] = piece; vs.append(v)
        return vs, [2 * math.pi * k_ / n for k_ in range(n)]

    def bridge(A, aA, B, aB, fc):
        """Полоса между двумя петлями разной длины: шаг по той, чей следующий угол меньше."""
        m, n = len(A), len(B); i_ = j_ = 0
        aA = aA + [aA[0] + 2 * math.pi]; aB = aB + [aB[0] + 2 * math.pi]
        fs = []
        while i_ < m or j_ < n:
            if j_ >= n or (i_ < m and aA[i_ + 1] <= aB[j_ + 1]):
                tri = (A[i_], A[(i_ + 1) % m], B[j_ % n]); i_ += 1
            else:
                tri = (A[i_ % m], B[(j_ + 1) % n], B[j_ % n]); j_ += 1
            if len(set(tri)) == 3:
                f = bm.faces.new(tri); f[FC] = fc; fs.append(f)
        return fs

    # --- основание черепа ---
    nh, nt = Vector(BN['шея']['head']), Vector(BN['шея']['tail'])
    AX = (nt - nh).normalized()
    WN = Vector((0, AX.z, -AX.y)); WN = WN if WN.z > 0 else -WN        # «верх» кольца
    QT, QB = Vector((0, yn + float(opt.get('qt', 0.67)), 1.30)), Vector((0, yn + float(opt.get('qb', 0.44)), 0.78))   # холка и низ манишки
    QN = Vector((0, (QT - QB).z, -(QT - QB).y)).normalized()           # к телу
    nose_f = min((f for f in bm.faces if not f[EXF]), key=lambda f: f.calc_center_median().y)
    head = region(nose_f, lambda f: (f.calc_center_median() - QT).dot(QN) < 0)
    Ls, Lb = cut_loop(head, 'шов шеи')
    CA = nh + AX * float(opt.get('ta', 0.25)); CR = nt
    Ls, aS = ordered(Ls, CA, WN); Lb, aB = ordered(Lb, CA, WN)
    new_f = []
    for piece, L, aL, fc in ((0, Lb, aB, 3), (1, Ls, aS, 4)):
        rA, a12 = ring(CA, WN, 0.12, 0.14, 12, 51, piece)
        rR, _ = ring(CR, WN, 0.085, 0.10, 12, 1, piece)
        if piece == 1: HEAD_RING = rR
        new_f += bridge(L, aL, rA, a12, fc) + bridge(rA, a12, rR, a12, fc)
    over = max((CR - v.co).dot(AX) for v in Ls)
    SEAMS['основание черепа'] = dict(n=12, bone='шея', c=CR, ax=AX, up=WN, r=(0.085, 0.10), at=(CR - nh).length, edge=len(Ls), over=over)

    # --- корень хвоста ---
    th_, tt_ = Vector(BN['хвост']['head']), Vector(BN['хвост']['tail'])
    AT = (tt_ - th_).normalized()
    WT = Vector((0, -AT.z, AT.y)); WT = WT if WT.z > 0 else -WT
    PC = Vector((0, float(opt.get('ty', 0.63)), float(opt.get('tz', 1.00))))
    tip_f = min((f for f in bm.faces if not f[EXF] and abs(f.calc_center_median().x) < 0.03 and f.calc_center_median().y > 0.75),
                key=lambda f: f.calc_center_median().z)
    # хвост — только у средней линии и позади бёдер: ниже плоскости шва лежат и ляжки, по ним область утекла бы в ноги
    def in_tail(f):
        c_ = f.calc_center_median()
        return (c_ - PC).dot(AT) > 0 and abs(c_.x) < 0.12 and c_.y - yn > 1.76
    tail = region(tip_f, in_tail)
    Lt, Lk = cut_loop(tail, 'шов хвоста')
    # У образца хвост не труба от корня: спереди он сращён с крупом от корня до скакательных (петля среза 60 × 28 см).
    # Стянуть такую петлю в кольцо нельзя — вышел бы эллипс во весь круп. Кольцо 8 ставится у настоящего корня, а длинный
    # срез закрывается стенкой: у хвоста — его передняя сторона, у тела — задняя сторона крупа; общие — только 8 вершин
    top = max((v.co for v in Lt), key=lambda q: q.z)
    cc = Vector((0, top.y, top.z)) + WT * (-float(opt.get('tr', 0.055))) + AT * 0.01
    rx, rw = 0.055, 0.05
    Lt, aT = ordered(Lt, cc, WT); Lk, aK = ordered(Lk, cc, WT)
    for piece, L, aL in ((2, Lt, aT), (0, Lk, aK)):
        r8, a8 = ring(cc, WT, rx, rw, 8, 101, piece)
        new_f += bridge(L, aL, r8, a8, 5)
    SEAMS['корень хвоста'] = dict(n=8, bone='хвост', c=cc, ax=AT, up=WT, r=(rx, rw), at=(cc - th_).dot(AT), edge=len(Lt),
                                  over=max((v.co - cc).dot(AT) for v in Lt))


    # ---------- НАРЕЗКА ГОЛОВЫ ПО ОРГАНАМ (`--shvy 2`; решение геймдизайнера 13.10, спека `2026-10-08-vid-na-chuzhom-plane.md` §2 п.3) ----------
    #   морда с челюстями, зубами, языком и полостью — орган Пасть: шов «основание морды», кольцо 12 за углом рта;
    #   уши и мочка — орган Нюх (слот Чутьё): шов «основание уха», кольцо 8 на ухо; мочка — отдельный островок на конце морды;
    #   грива — черта шасси: воротник с тем же кольцом 12 «основание черепа», отделён от черепа вторым швом;
    #   череп с глазницами остаётся куском общей формы вида (целая голова = череп + морда) со своим кольцом 12.
    RINGS = {}
    if SHVY >= 2:
        PARTF = bm.faces.layers.int['part']
        head_set = lambda f: f.is_valid and f in head

        def ring_seam(Lp, Lb_, c, w, n, code, piece, u=None, k=0.92, cap=False):
            """Шов куска: новое кольцо из n точек эллипса чуть уже кромки и полоса «кромка → кольцо» у куска и у тела.
            Стягивать кромку по секторам нельзя: у редкой кромки (основание уха — 9–10 вершин) сектора остаются пустыми."""
            global XU
            keep_u = XU
            if u is not None:
                XU = u                                    # кольцо не в плоскости X тела: углы и точки — в своём базисе
            rx_ = k * max(abs((v.co - c).dot(XU)) for v in Lp); rw_ = k * max(abs((v.co - c).dot(w)) for v in Lp)
            for pc_, L in ((piece, Lp), (1, Lb_)):
                L, aL = ordered(L, c, w)
                r_, an = ring(c, w, rx_, rw_, n, code, pc_)
                fs_ = bridge(L, aL, r_, an, 8)
                if cap and pc_ == 1:
                    # основание уха на черепе — СПЛОШНАЯ кожа (механики 13.10: голова-черта на теле с чужим Чутьём иначе
                    # безухая с дырами): кольцо черепа закрыто веером, кольцо остаётся посадочным местом
                    cv = bm.verts.new(c); cv[VP] = 1; cv[RG] = code + 50
                    fs_ += [bm.faces.new((cv, r_[k_], r_[(k_ + 1) % n])) for k_ in range(n)]
                for f_ in fs_:
                    f_[PARTF] = 0; f_[EXF] = 0; f_[FC] = 8
                    new_f.append(f_)
                if pc_ == 1:
                    head.update(fs_)                  # полоса и заглушка у черепа — часть головы: иначе при делении «череп ↔ грива» уходили воротнику
            XU = keep_u
            return rx_, rw_

        def tidy(codes):
            """После стягивания: снять висячие лоскуты, закрыть треугольные дырки у кольца."""
            flap = [f for f in bm.faces if all(e.is_boundary for e in f.edges)]
            if flap:
                bmesh.ops.delete(bm, geom=flap, context='FACES')
                bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
            for _ in range(8):
                stray = [v for v in bm.verts if not v[RG] and sum(1 for e in v.link_edges if e.is_boundary) == 2]
                if not stray:
                    break
                v = stray[0]; nbv = [e.other_vert(v) for e in v.link_edges if e.is_boundary]
                if bm.faces.get((v, nbv[0], nbv[1])) is None:
                    f_ = bm.faces.new((v, nbv[0], nbv[1])); f_[FC] = 0; f_[PARTF] = 0; f_[EXF] = 0
                else:
                    break

        # --- уши ---
        for sgn, code in ((1, 201), (-1, 301)):
            tip = max((f for f in bm.faces if head_set(f) and f.calc_center_median().x * sgn > 0.08), key=lambda f: f.calc_center_median().z)

            def in_ear(f, sgn=sgn):
                c_ = f.calc_center_median()
                return head_set(f) and c_.x * sgn > 0.085 and c_.z > float(opt.get('ez', 1.40)) and c_.y - yn < 0.42
            ear = region(tip, in_ear)
            Le, Lh = cut_loop(ear, 'шов уха')
            ce = sum((v.co for v in Le), Vector()) / len(Le)
            tipc = tip.calc_center_median()
            axe = (tipc - ce).normalized()
            we = (Vector((0, -1, 0)) - axe * axe.dot(Vector((0, -1, 0)))).normalized()      # «верх» кольца уха — вперёд, к морде
            ue = axe.cross(we).normalized(); ue = ue if ue.x > 0 else -ue      # кольцо уха лежит не в плоскости X: свой базис
            rr = ring_seam(Le, Lh, ce, we, 8, code, 4, ue, cap=True)
            if sgn == 1:
                SEAMS['основание уха'] = dict(n=8, bone='голова', c=ce, ax=axe, up=we, u=ue, r=rr, at=(ce - Vector(BN['голова']['head'])).length,
                                              edge=len(Le), over=0.0)
            head.difference_update(ear)
        tidy(None)

        # --- морда (Пасть) ---
        MT, MB = Vector((0, yn + float(opt.get('mt', 0.17)), 1.27)), Vector((0, yn + float(opt.get('mb', 0.25)), 1.04))    # стоп → под челюстью за углом рта
        MN = Vector((0, (MT - MB).z, -(MT - MB).y)).normalized(); MN = MN if MN.y > 0 else -MN       # к черепу
        nose_f2 = min((f for f in bm.faces if head_set(f)), key=lambda f: f.calc_center_median().y)
        # ниже челюсти плоскость шва уходит назад под гриву — без нижней границы область утекала в манишку (кольцо 41 см)
        muzzle = region(nose_f2, lambda f: head_set(f) and (f.calc_center_median() - MT).dot(MN) < 0 and f.calc_center_median().z > 1.0)
        Lm, Lsk0 = cut_loop(muzzle, 'шов морды')
        cm = sum((v.co for v in Lm), Vector()) / len(Lm); cm = Vector((0, cm.y, cm.z))
        cm = cm - MN * (cm - MT).dot(MN)
        wm = Vector((0, MN.z, -MN.y)); wm = wm if wm.z > 0 else -wm
        rr = ring_seam(Lm, Lsk0, cm, wm, 12, 401, 3)
        SEAMS['основание морды'] = dict(n=12, bone='голова', c=cm, ax=-MN, up=wm, r=rr, at=(cm - Vector(BN['голова']['head'])).length,
                                         edge=len(Lm), over=0.0)
        head.difference_update(muzzle)
        tidy(None)

        # --- череп ↔ грива ---
        ST, SB = Vector((0, yn + float(opt.get('st', 0.46)), 1.44)), Vector((0, yn + float(opt.get('sb', 0.27)), 1.00))    # за ушами → под челюстью
        SN = Vector((0, (ST - SB).z, -(ST - SB).y)).normalized(); SN = SN if SN.y > 0 else -SN
        seed_p = Vector((0, yn + 0.27, 1.36))
        skull_seed = min((f for f in bm.faces if head_set(f) and not f[FC]), key=lambda f: (f.calc_center_median() - seed_p).length)

        def in_skull(f):
            c_ = f.calc_center_median()
            return head_set(f) and f[FC] in (0, 8) and (c_ - ST).dot(SN) < 0 and (abs(c_.x) < float(opt.get('sx', 0.15)) or c_.z > 1.33)
        skull = region(skull_seed, in_skull)
        Lsk, Lco = cut_loop(skull, 'шов гривы')
        collar_seed = Lco[0]
        Lsk, aSk = ordered(Lsk, CR, WN); Lco, aCo = ordered(Lco, CR, WN)
        rS, a12 = ring(CR, WN, 0.085, 0.10, 12, 1, 1)                       # своё кольцо «основание черепа» у черепа
        new_f += bridge(Lsk, aSk, rS, a12, 6)
        new_f += bridge(Lco, aCo, HEAD_RING, a12, 7)                         # передняя изнанка воротника — к его же кольцу
        SEAMS['грива'] = dict(n=12, bone='шея', c=CR, ax=AX, up=WN, r=(0.085, 0.10), at=(CR - nh).length, edge=len(Lco),
                              over=SEAMS['основание черепа']['over'])

        # --- мочка носа: у образца её нет (критик 10.10) — островок на конце морды ---
        tipv = min((v for v in bm.verts if v.link_faces and abs(v.co.x) < 0.02), key=lambda v: v.co.y).co.copy()
        lob = []
        for dy, hw, z0_, z1_ in ((-0.010, 0.016, -0.004, 0.018), (0.012, 0.024, -0.012, 0.024), (0.034, 0.020, -0.008, 0.020)):
            lob.append([bm.verts.new((tipv.x + sx * hw, tipv.y + dy, tipv.z + z)) for sx, z in ((-1, z0_), (1, z0_), (1, z1_), (-1, z1_))])
        lf = [bm.faces.new((lob[0][0], lob[0][1], lob[0][2], lob[0][3])), bm.faces.new((lob[2][3], lob[2][2], lob[2][1], lob[2][0]))]
        for r0, r1 in zip(lob, lob[1:]):
            lf += [bm.faces.new((r0[k_], r1[k_], r1[(k_ + 1) % 4], r0[(k_ + 1) % 4])) for k_ in range(4)]
        bmesh.ops.recalc_face_normals(bm, faces=lf)
        lf = bmesh.ops.triangulate(bm, faces=lf)['faces']
        for f in lf:
            f[EXF] = 1; f[PARTF] = 3; f[FC] = 0
        TAGL = bm.verts.layers.int['jawtag']
        for r in lob:
            for v in r:
                v[TAGL] = 2; v[VP] = 7; v[RG] = 0
        SEAMS['мочка'] = dict(n=8, bone='голова', c=tipv + Vector((0, 0.012, 0.006)), ax=Vector((0, -1, 0)), up=Vector((0, 0, 1)), r=(0.024, 0.018),
                              at=0.0, edge=0, over=0.0)

    # куски — связные области: от носа и от кончика хвоста
    bm.verts.ensure_lookup_table(); bm.faces.ensure_lookup_table()
    for piece, seed in ((1, min(bm.verts, key=lambda v: v.co.y)),
                        (2, min((v for v in bm.verts if abs(v.co.x) < 0.03 and v.co.y > 0.75 and v.link_faces), key=lambda v: v.co.z))):
        seen, st = set(), [seed]
        while st:
            u = st.pop()
            if u in seen:
                continue
            seen.add(u); u[VP] = piece
            st.extend(e.other_vert(u) for e in u.link_edges)
    if SHVY >= 2:                                        # 1 череп, 3 морда, 4 уши, 6 воротник, 7 мочка
        bm.verts.ensure_lookup_table()
        lob_v = {v for r in lob for v in r}
        seeds = [(6, collar_seed), (1, skull_seed.verts[0]), (3, min((v for v in bm.verts if v.link_faces and v not in lob_v), key=lambda v: v.co.y))]
        seeds += [(4, max((v for v in bm.verts if v.co.x * sg_ > 0.08 and v.link_faces), key=lambda v: v.co.z)) for sg_ in (1, -1)]
        for v in bm.verts:                               # прежняя разметка «всё от носа — голова» здесь не действует
            if v[VP] == 1: v[VP] = 0
        for piece, seed in seeds:
            seen, st = set(), [seed]
            while st:
                u = st.pop()
                if u in seen:
                    continue
                seen.add(u); u[VP] = piece
                st.extend(e.other_vert(u) for e in u.link_edges)
        for v in lob_v:
            v[VP] = 7
    for f in bm.faces:                                   # зубы и язык — с пастью (в старой нарезке — с головой); мочка — сама
        if f[EXF] and not (SHVY >= 2 and f[bm.faces.layers.int['part']] == 3):
            for v in f.verts:
                v[VP] = 3 if SHVY >= 2 else 1
    bmesh.ops.recalc_face_normals(bm, faces=[f for f in bm.faces if not f[EXF]])
    # ОРИЕНТАЦИЯ — ПО ЗНАКУ ОБЪЁМА КУСКА: у замкнутого воротника с двумя изнанками пересчёт нормалей выворачивал часть
    # граней (в кадре с отсечением задних граней грива рассыпалась на клочки). Кусок с отрицательным объёмом — целиком наизнанку
    seenf = set()
    for f0 in bm.faces:
        if f0 in seenf:
            continue
        comp, st = [], [f0]
        while st:
            f = st.pop()
            if f in seenf:
                continue
            seenf.add(f); comp.append(f)
            st.extend(g for e in f.edges for g in e.link_faces if g is not f)
        # объём — от центра самого куска: у незамкнутого лоскута (лицо, ухо) объём от начала координат знака не держит
        cen_ = sum((f.calc_center_median() for f in comp), Vector()) / len(comp)
        vol = 0.0
        for f in comp:
            vs_ = [l.vert.co - cen_ for l in f.loops]
            for k_ in range(1, len(vs_) - 1):
                vol += vs_[0].dot(vs_[k_].cross(vs_[k_ + 1]))
        if vol < 0:
            bmesh.ops.reverse_faces(bm, faces=comp)
    bnd_n = sum(1 for e in bm.edges if e.is_boundary)
    bm.to_mesh(me); bm.free()
    for p_ in me.polygons:
        p_.use_smooth = False
    for nm, d in SEAMS.items():
        REP.append('шов «%s»: кольцо %d вершин на кости `%s`, %.3f м от её начала; центр (%.3f %.3f %.3f), ось наружу (%.2f %.2f %.2f), '
                   '«верх» (%.2f %.2f %.2f), полуоси вбок %.3f × по «верху» %.3f м; кромка куска %d вершин; кусок свисает за шов на %.3f м вдоль оси'
                   % (nm, d['n'], d['bone'], d['at'], *d['c'], *d['ax'], *d['up'], *d['r'], d['edge'], d['over']))
    REP.append('швы: граничных рёбер %d (ждём %d)' % (bnd_n, 40 if SHVY < 2 else 24 + 16 + 16 + 24))     # шея и череп, хвост, уши (у черепа закрыты), морда; воротник замкнут

tagv = [d.value for d in me.attributes['jawtag'].data]
vpv = [d.value for d in me.attributes['piece'].data] if SHVY else [0] * len(me.vertices)
rgv = [d.value for d in me.attributes['ring'].data] if SHVY else [0] * len(me.vertices)
W = []
NJ = yn + 0.32 - N0
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
    # куски несут только кости, которые есть у любого носителя шва (условие механик): голова — `голова`, `шея`, `челюсть`;
    # хвост — `хвост`, `хвост_кисть`. Кольцо шва у куска и у тела весит одинаково, иначе шов разойдётся в движении
    if SHVY:
        r_ = rgv[v.index]
        pv_ = vpv[v.index]
        if 1 <= r_ < 100: w = {'шея': 1.0}
        elif 100 < r_ < 200: w = {'хвост': 1.0}
        elif r_ > 200: w = {'голова': 1.0}                 # кольца ушей и морды — на черепе
        elif pv_ in (4, 7): w = {'голова': 1.0}            # уши и мочка жёсткие
        elif pv_ == 3: w = {k_: x for k_, x in w.items() if k_ in ('голова', 'челюсть')} or {'голова': 1.0}
        elif pv_ == 6: w = {k_: x for k_, x in w.items() if k_ in ('голова', 'шея')} or {'шея': 1.0}
        elif vpv[v.index] == 1:
            w = {k_: x for k_, x in w.items() if k_ in ('голова', 'шея', 'челюсть')} or {'шея': 1.0}
        elif vpv[v.index] == 2:
            w = {k_: x for k_, x in w.items() if k_ in ('хвост', 'хвост_кисть')} or {'хвост': 1.0}
        else:
            for k_ in ('голова', 'челюсть'):
                if k_ in w: w['шея'] = w.get('шея', 0) + w.pop(k_)
        s_ = sum(w.values()); w = {k_: x / s_ for k_, x in w.items()}
    W.append(w)


def swap(w):
    return {(k_[:-2] if k_.endswith('.L') else (k_ + '.L' if k_ in LIMB else k_)): x for k_, x in w.items()}


RCL = lambda r_: 0 if r_ == 0 else 1 + (r_ > 50) + (r_ > 100) + (r_ > 200) + (r_ > 400)      # класс кольца: зеркальная вершина кольца носит другой номер
kdm = kdtree.KDTree(len(me.vertices))
for v in me.vertices:
    kdm.insert(v.co, v.index)
kdm.balance()
asym = 0
for v in me.vertices:
    if v.co.x > 1e-5:
        # у губ верхняя и нижняя вершины стоят в одной точке — пара ищется среди своих по метке челюсти
        hit = [j for _, j, d in kdm.find_n((-v.co.x, v.co.y, v.co.z), 4) if d < 1e-4 and tagv[j] == tagv[v.index] and vpv[j] == vpv[v.index] and RCL(rgv[j]) == RCL(rgv[v.index])]
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
# СВОЙ ЦВЕТ — ОТДЕЛЬНЫМ ОБЪЕКТОМ (механики, `c04c3a7`): игра красит рендерер целиком, поэтому полость рта с языком и зубы —
# объекты `слот.что` с материалом своего цвета; рендерер в игре получит имя слота, цвет уйдёт в паспорт детали
# ИМЯ ОБЪЕКТА = СЛОТ, точка — ТОЛЬКО у кусков со своим цветом: игра читает цвет материала у любого объекта `слот.что`, а
# без материала Unity подставляет серый по умолчанию — уши и грива покрасились бы им вместо цвета состава. Поэтому уши —
# `Чутьё`, грива — `Шкура` (покров), а с точкой — полость, зубы и мочка, у которых цвет свой
MAW = 'Пасть' if SHVY >= 2 else 'голова'             # чьи полость и зубы: при нарезке по органам — Пасти
OWN = {MAW + '.полость': (0.035, 0.006, 0.007, 1.0), MAW + '.зубы': (0.90, 0.88, 0.80, 1.0)}   # линейные: в игре полость ≈ (0.21 0.07 0.08)
if SHVY >= 2:
    OWN['Чутьё.мочка'] = (0.012, 0.011, 0.012, 1.0)
ORDER = ['голова', 'шея', 'хребет', 'Руки', 'Ноги', 'Хвост'] + (['Пасть', 'Чутьё', 'Шкура'] if SHVY >= 2 else []) + list(OWN)
part = [d.value for d in me.attributes['part'].data]
extra = [d.value for d in me.attributes['extra'].data]
forced = [d.value for d in me.attributes['forced'].data] if SHVY else [0] * len(me.polygons)
F = [tuple(p.vertices) for p in me.polygons]
fslot = []
for k_, f in enumerate(F):
    sc = {}
    for vi in f:
        for nm, x in W[vi].items():
            sc[SLOT[nm]] = sc.get(SLOT[nm], 0) + x
    if SHVY:                                             # кусок — целиком свой объект; у тела граней головы и хвоста нет
        pc = vpv[f[0]]
        for bad_ in ('голова', 'Хвост'):
            sc.pop(bad_, None)
        sl_ = ({1: 'голова', 2: 'Хвост', 3: 'Пасть', 4: 'Чутьё', 6: 'Шкура'}.get(pc)
               or ('шея' if forced[k_] == 3 else (max(sc, key=sc.get) if sc else 'хребет')))
        fslot.append(MAW + '.полость' if part[k_] == 1 else MAW + '.зубы' if part[k_] == 2 else 'Чутьё.мочка' if part[k_] == 3 else sl_)
        if pc or forced[k_]:
            part[k_] = part[k_] or 9                     # дальше границу не приглаживать
        continue
    fslot.append(MAW + '.полость' if part[k_] == 1 else MAW + '.зубы' if part[k_] == 2
                 else 'голова' if all(me.vertices[vi].co.y - yn < 0.27 for vi in f) else max(sc, key=sc.get))
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
        if part[k_]:
            continue
        o = [fslot[j] for j in nb[k_] if fslot[j] != fslot[k_] and not part[j]]
        if len(o) >= 2 and o.count(o[0]) >= 2:
            fslot[k_] = o[0]
done = set()                                         # островки: кусок слота меньше 12 граней уходит к соседу
for k_ in range(len(F)):
    if k_ in done or part[k_]:
        continue
    comp, st = [], [k_]
    while st:
        u = st.pop()
        if u in done:
            continue
        done.add(u); comp.append(u)
        st.extend(j for j in nb[u] if fslot[j] == fslot[k_] and j not in done)
    if len(comp) < 12:
        o = [fslot[j] for u in comp for j in nb[u] if fslot[j] != fslot[k_] and not part[j]]
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
    if sl in OWN:
        mt = bpy.data.materials.new(sl); mt.diffuse_color = OWN[sl]; mt.use_nodes = True
        mt.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = OWN[sl]
        m.materials.append(mt)
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

# ФОРМА-КЛЮЧ «двуногий» У ВОРОТНИКА (13.10, первый оборотень из кусков): у волка масса гривы вентральная — манишка на
# 0.35 м ниже оси шеи при 0.20 над ней. На вертикальной шее человека это борода-нагрудник, а загривок голый (кадры
# механик `Оборотень-пилот-2026-10-10/черты2-*`, тот же вид даёт `oboroten_predprosmotr.py`). Ключ в кадре шва
# (x вбок, h — дорсальная сторона, t вдоль оси, t < 0 — к телу): вентральная сторона короче на 45 % и ближе к оси на
# 40 %, дорсальная длиннее на 90 %, к низу воротник шире на 20 % — грива ложится на плечи и спину. Чистому волку ключ
# не нужен (вес 0); на двуногом носителе его включает посадка
if SHVY >= 2 and 'Шкура' in parts:
    go = parts['Шкура'][0]
    cK, upK, axK = SEAMS['грива']['c'], SEAMS['грива']['up'], SEAMS['грива']['ax']
    RK = Matrix((upK.cross(axK).normalized(), upK, axK)).transposed()
    go.shape_key_add(name='Basis')
    kb = go.shape_key_add(name='двуногий')
    for v in go.data.vertices:
        x_, h_, t_ = (v.co - cK) @ RK
        dors, vent = sm(h_ / 0.12), sm(-h_ / 0.12)
        kb.data[v.index].co = cK + RK @ Vector((x_ * (1 + 0.20 * sm(-t_ / 0.25)), h_ * (1 - 0.40 * vent), t_ * (1 + 0.9 * dors - 0.45 * vent)))
    REP.append('форма-ключ «двуногий» у объекта `Шкура`: вершин %d' % len(go.data.vertices))

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
    REP.append('  %-14s %5d тр, %4d вершин' % (sl, n, len(parts[sl][1])))
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

# ---------- паспорта кусков ----------
# `--pasporta папка`: паспорт детали на каждый кусок (формат `Docs/models/handoff/parts/*.json`; поля `fbx` и `fit` — по
# письму механик 10.10: меш берётся объектом из модели вида, `fit: ring` — кусок встаёт в кадр кости шва целиком).
# Числа — из тех же переменных, что построили кольцо: паспорт с FBX разойтись не может. Оси Unity: (−x, z, −y) Blender
if SHVY and 'pasporta' in opt:
    def U(q):
        return [round(-q.x, 5), round(q.z, 5), round(-q.y, 5)]
    NOTE = ('метры тела волка, оси Unity; кольцо плоское; вершина 0 — на «верху», дальше в сторону `side` (у осевых швов — к −X Unity); '
            'ellipse_m — вбок × по «верху»; overhang_m — на сколько кусок свисает за шов вдоль оси к телу носителя')
    if SHVY >= 2:
        # (файл, шов, слот или None для черты, объекты, кости, fit, зеркальная пара, доп. поля)
        PIECES = (
            ('volk-morda.json', 'основание морды', 'Пасть', dict(main='Пасть', polost='Пасть.полость', zuby='Пасть.зубы'), ['голова', 'челюсть'], 'ring', False,
             dict(jaw_joint_m=U(JOINT), jaw_bone='челюсть')),
            ('volk-ushi.json', 'основание уха', 'Чутьё', dict(main='Чутьё'), ['голова'], 'ring', True, {}),
            ('volk-mochka.json', 'мочка', 'Чутьё', dict(main='Чутьё.мочка'), ['голова'], None, False, dict(seat='нос')),
            ('volk-golova-kusok.json', 'основание черепа', None, dict(main='голова', morda='Пасть', polost='Пасть.полость', zuby='Пасть.зубы'),
             ['голова', 'шея', 'челюсть'], 'ring', False, dict(trait='голова', step=2, also=['Пасть', 'Пасть.полость', 'Пасть.зубы'],
                                                              center_biped_m=U(Vector((0, yn + 0.37, 1.12))))),
            ('volk-griva.json', 'грива', None, dict(main='Шкура'), ['голова', 'шея'], 'ring', False, dict(trait='грива', step=1, keys=['двуногий'])),
            ('volk-hvost-kusok.json', 'корень хвоста', None, dict(main='Хвост'), ['хвост', 'хвост_кисть'], None, False, dict(trait='хвост', step=1)),
        )
    else:
        PIECES = (
            ('volk-golova-kusok.json', 'основание черепа', 'Пасть', dict(main='голова', polost='голова.полость', zuby='голова.зубы'),
             ['голова', 'шея', 'челюсть'], 'ring', False, {}),
            ('volk-hvost-kusok.json', 'корень хвоста', 'Хвост', dict(main='Хвост'), ['хвост', 'хвост_кисть'], None, False, {}),
        )
    for fname, seam_name, slot, objs, bones_, fit, mirror, extra_f in PIECES:
        d = SEAMS[seam_name]
        uu = d.get('u', Vector((1, 0, 0)))
        ring_pts = [d['c'] + uu * (d['r'][0] * math.sin(2 * math.pi * k_ / d['n'])) + d['up'] * (d['r'][1] * math.cos(2 * math.pi * k_ / d['n']))
                    for k_ in range(d['n'])]
        seam_type = 'основание черепа' if seam_name == 'грива' else seam_name
        pas = {
            'species': 'Волк', 'plan': 'четвероногий',
            'seam': {'type': seam_type, 'ring': list(range(d['n'])), 'ring_m': [U(q) for q in ring_pts], 'center_m': U(d['c']),
                     'ellipse_m': [round(d['r'][0], 4), round(d['r'][1], 4)], 'axis_out': U(d['ax']), 'up': U(d['up']), 'side': U(uu), 'bone': d['bone'],
                     'on_bone_m': round(d['at'], 4), 'overhang_m': round(d['over'], 4), 'note': NOTE},
            'objects': objs, 'fbx': 'Assets/_Chimera/Models/volk_mesh.fbx', 'mirror': mirror,
            'bones': bones_, 'armature': 'все узлы графа волка + челюсть', 'keys': [],
            'tris': sum(len(parts[o_][0].data.polygons) for o_ in objs.values()), 'tris_main': len(parts[objs['main']][0].data.polygons),
            'generator': 'Tools/Blender/proba/rig_volka.py --muzzle 4.5 --shvy %d --pasporta …' % SHVY,
            'source': 'Референсы/работа/модельная/2026-10-09-proba-b/volk_b9.obj',
        }
        if slot:
            pas['slot'] = slot
        if fit:
            pas['fit'] = fit
        pas.update(extra_f)                                   # в том числе `keys` — формы-ключи адаптации к плану тела
        if mirror:
            pas['seam']['note'] += '; кольцо дано для стороны −X Unity (+X Blender), вторая сторона — зеркалом по X'
        sub = 'parts' if slot else 'cherty'           # черта шасси — не деталь органа: читатель деталей её не берёт
        dst = os.path.join(os.path.abspath(opt['pasporta']), '..', sub) if SHVY >= 2 else os.path.abspath(opt['pasporta'])
        os.makedirs(dst, exist_ok=True)
        with open(os.path.join(dst, fname), 'w', encoding='utf-8') as fh:
            json.dump(pas, fh, ensure_ascii=False, indent=2)
        REP.append('паспорт: %s/%s (%s, %s, %d тр)' % (sub if SHVY >= 2 else 'parts', fname, seam_name, 'слот ' + slot if slot else 'черта', pas['tris']))

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

    COL = {'Пасть': (0.95, 0.55, 0.75, 1), 'Чутьё': (1.0, 0.95, 0.3, 1), 'Шкура': (0.55, 0.35, 0.2, 1), 'голова': (0.85, 0.3, 0.3, 1), 'шея': (0.9, 0.75, 0.3, 1), 'хребет': (0.4, 0.7, 0.4, 1), 'Руки': (0.3, 0.55, 0.9, 1),
           'Ноги': (0.65, 0.4, 0.85, 1), 'Хвост': (0.3, 0.8, 0.8, 1)}
    for sl in ORDER:
        parts[sl][0].color = COL.get(sl, OWN.get(sl, (0.7, 0.7, 0.7, 1)))
    sh.color_type = 'OBJECT'
    shot('narezka', (0, 0, 0.8), 2.8, ['side', '34f', '34b', 'front', 'back'])
    for sl in ORDER:
        parts[sl][0].color = OWN.get(sl, (0.72, 0.72, 0.72, 1))
    hc = (0, yn + 0.20, 1.20)
    shot('zakryta', hc, 0.55, ['side', '34f', 'low'])
    pose({'челюсть': 35})
    shot('past35', hc, 0.55, ['side', '34f', 'front', 'low', 'hi'])
    pose({'голова': -20, 'шея': -15, 'плечо': 25, 'предплечье': -35, 'плечо.L': -20, 'бедро': -25, 'голень': 30,
          'бедро.L': 20, 'хвост': -40, 'хвост_кисть': -25, 'челюсть': 20})
    shot('poza', (0, 0, 0.8), 2.8, ['side', '34f', '34b'])
    pose({})
    shot('pokoj', (0, 0, 0.8), 2.8, ['side', '34f'])
