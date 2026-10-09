# -*- coding: utf-8 -*-
"""ДЕТАЛЬ-ПИЛОТ «Волк · Руки · четвероногий» (письмо механик `Docs/models/FEEDBACK-2026-10-02b-pilot-noga.md` с поправками
ГеймБосса 02.10: меш — В МЕТРАХ ТЕЛА ВОЛКА, оси Unity, там, где нога стоит в позе покоя графа; арматура — граф волка;
веса — `плечо`, `предплечье`, лапа к `предплечье`). Эксперимент консилиума: деталь — меш по форме листа.

Шаги:
  1. образец листа (выровненный `obrazec_v_obj.py`) → правая передняя нога: ось по следу от стопы, грани ниже брюха,
     связная область стопы; прореживание QEM до бюджета; срез верха плоскостью у подмышки;
  2. ПЕРЕСАДКА НА СУСТАВЫ ВОЛКА по высоте: земля → земля, запястье образца → та же высота (лапа своего размера), локоть →
     конец `плечо`, плечевой сустав → начало `плечо`; поперёк — смещение от оси образца переносится на ось костей волка
     без масштаба (толщина листа сохраняется);
  3. культя: петля подмышки → кольцо → КОЛЬЦО ШВА из 8 вершин на плечевом суставе (внутри туши, на поле уходит в тело);
     вершина 0 — перёд тела, обход против часовой при взгляде из детали к телу;
  4. арматура — все узлы графа волка в позе покоя (`DumpGraph`), имена — узлы; веса: выше локтя `плечо`, ниже —
     `предплечье`, переход ±3 см;
  5. форма-ключ `двуногий` (рука оборотня по листу `human/ref/chimera/oboroten-…png`): плечо и предплечье мощнее, пальцы
     длиннее; кольцо шва ключ не двигает (01d §1.4); поза — костями;
  6. FBX как у `chimera_rig.export_fbx` (без `bake_space_transform`: корень едет с поворотом −90°, в Unity — Bake Axis
     Conversion) и паспорт JSON.

Запуск:  blender -b -P noga_iz_obrazca.py -- образец.obj граф_мира.json выход.fbx паспорт.json [бюджет] [кадр.png]
"""
import json
import os
import math
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Vector

a = sys.argv[sys.argv.index('--') + 1:]
src, graph_path, fbx, passport = a[0], a[1], a[2], a[3]
BUDGET = int(a[4]) if len(a) > 4 else 450
SHOT = a[5] if len(a) > 5 else None
N_RING = 8


def to_b(p):            # Unity (x, y, z) → Blender: модель смотрит в −Y, правая сторона Unity +X = Blender −X
    return Vector((-p[0], -p[2], p[1]))


def from_b(v):
    return np.array([-v[0], v[2], -v[1]])


# ── граф волка (выгрузка DumpGraph в осях OBJ стенда: x зеркален) → Unity ──
G = {n['name']: dict(n, a=np.array([-n['a'][0], n['a'][1], n['a'][2]]), b=np.array([-n['b'][0], n['b'][1], n['b'][2]]))
     for n in json.load(open(graph_path, encoding='utf-8'))}
S, E_, W = G['плечо']['a'], G['плечо']['b'], G['предплечье']['b']
GROUND = np.array([W[0], 0.0, W[2]])

# ── образец в осях Unity ──
V, F = [], []
for l in open(src, encoding='utf-8'):
    if l.startswith('v '):
        x, y, z = (float(t) for t in l.split()[1:4]); V.append((-x, y, z))
    elif l.startswith('f '):
        F.append([int(t.split('/')[0]) - 1 for t in l.split()[1:]])
P = np.array(V)
zmin, zmax = P[:, 2].min(), P[:, 2].max(); L = zmax - zmin
# БРЮХО — только по срезам туши МЕЖДУ ногами (02.10, поправка v2): у образца передние ноги стоят почти на средней
# линии, и 5-й перцентиль по всей средней трети ловил их — брюхо выходило 0.42 вместо ~0.6, а нога резалась вдвое короче.
# Срез «без ног» — тот, где низ по бокам (|x| > 0.06) выше 0.3 м; брюхо — медиана нижней точки средней линии по таким срезам
mins = []
for z0 in np.linspace(zmin, zmax, 25)[1:-1]:
    sl = P[np.abs(P[:, 2] - z0) < 0.03]
    sd, md = sl[np.abs(sl[:, 0]) > 0.06], sl[np.abs(sl[:, 0]) < 0.04]
    if len(sd) and len(md) and sd[:, 1].min() > 0.3:
        mins.append(md[:, 1].min())
belly = float(np.median(mins))
legs = (P[:, 1] < belly - 0.03) & (np.abs(P[:, 0]) > 0.055)
zc = float(np.median(P[legs, 2]))
g = np.nonzero(legs & (P[:, 0] > 0) & (P[:, 2] > zc))[0]
c = np.median(P[g[P[g, 1] < 0.08]][:, [0, 2]], axis=0); axis = []
DY = 0.04
for y in np.arange(0.02, belly - 0.01, DY):
    s = g[(P[g, 1] >= y - DY / 2) & (P[g, 1] < y + DY / 2)]
    s = s[np.linalg.norm(P[s][:, [0, 2]] - c, axis=1) < 0.13]
    if len(s) > 20:
        c = P[s][:, [0, 2]].mean(0); axis.append((c[0], y, c[1]))
axis = np.array(axis)
cut = belly - 0.01
elbow_s = None


def axis_s(y):
    """Ось ноги образца на высоте y; выше подмышки — прямая к плечевому суставу образца."""
    if y > axis[-1, 1] and elbow_s is not None:
        t = (y - elbow_s[1]) / max(1e-6, shoulder_s[1] - elbow_s[1])
        return elbow_s + (shoulder_s - elbow_s) * np.clip(t, 0, 1.5)
    i = np.clip(np.searchsorted(axis[:, 1], y), 1, len(axis) - 1)
    t = (y - axis[i - 1, 1]) / (axis[i, 1] - axis[i - 1, 1])
    return axis[i - 1] + (axis[i] - axis[i - 1]) * np.clip(t, 0, 1)


# НОГА — БУЛЕВЫМ ПЕРЕСЕЧЕНИЕМ замкнутого образца с замкнутой трубой вдоль оси ноги: кусок, выбранный гранями, у
# образца рваный (открытые цепочки, дыры), и воксели его теряют. Труба: кольца по 16 вершин от земли до среза,
# радиус 0.13 у лапы и 0.10 у груди (грива груди висит между ногами)
bpy.ops.wm.read_factory_settings(use_empty=True)
bm = bmesh.new()
sv = [bm.verts.new(to_b(p)) for p in P]
for f in F:
    try:
        bm.faces.new([sv[i] for i in f])
    except ValueError:
        pass
me = bpy.data.meshes.new('Руки'); bm.to_mesh(me); bm.free()
ob = bpy.data.objects.new('Руки', me); bpy.context.collection.objects.link(ob)
bm = bmesh.new(); tube = []
for y in list(np.arange(-0.02, cut - 0.01, 0.03)) + [cut]:
    ax = axis_s(max(y, 0.02)); rad = 0.13 if y < 0.35 else (0.10 if y < 0.45 else 0.085)   # у груди уже: грива груди
    tube.append([bm.verts.new(to_b(np.array([ax[0] + rad * math.cos(t), y, ax[2] + rad * math.sin(t)])))
                 for t in np.linspace(0, 2 * math.pi, 16, endpoint=False)])
for r0, r1 in zip(tube, tube[1:]):
    for j in range(16):
        bm.faces.new([r0[j], r0[(j + 1) % 16], r1[(j + 1) % 16], r1[j]])
bm.faces.new(tube[0][::-1]); bm.faces.new(tube[-1])
bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
vol = bpy.data.objects.new('объём', bpy.data.meshes.new('объём')); bm.to_mesh(vol.data); bm.free()
bpy.context.collection.objects.link(vol)
bpy.context.view_layer.objects.active = ob; ob.select_set(True)
bo = ob.modifiers.new('bo', 'BOOLEAN'); bo.operation = 'INTERSECT'; bo.object = vol; bo.solver = 'EXACT'
bpy.ops.object.modifier_apply(modifier=bo.name)
bpy.data.objects.remove(vol, do_unlink=True)

n0 = sum(len(p.vertices) - 2 for p in me.polygons)

# суставы образца
plane_z = cut - 0.03
elbow_s = axis_s(plane_z)
wrist_y = 0.13
shoulder_s = elbow_s + (S - E_) * ((elbow_s[1]) / max(1e-6, E_[1]))  # плечевая кость образца — того же наклона, длина по росту локтя
print('образец: брюхо %.3f, локоть %s, плечо %s' % (belly, np.round(elbow_s, 3), np.round(shoulder_s, 3)))
dir_u = (shoulder_s - elbow_s) / np.linalg.norm(shoulder_s - elbow_s)        # к телу
fwd = np.array([0.0, 0.0, 1.0]); fwd = fwd - dir_u * fwd.dot(dir_u); fwd /= np.linalg.norm(fwd)
lat = np.cross(dir_u, fwd)

# сечение культи — по верху ноги (вершины в полосе 4 см под срезом): полуоси вдоль «перёд» и «вбок»
top = np.array([from_b(v.co) for v in me.vertices if abs(v.co.z - (plane_z - 0.02)) < 0.02])
cen = top.mean(0) if len(top) else elbow_s
ax_f = float(np.percentile(np.abs((top - cen) @ fwd), 85)); ax_l = float(np.percentile(np.abs((top - cen) @ lat), 85))

# КУЛЬТЯ — закрытая труба 8-угольником от середины верха ноги до чуть выше плечевого сустава; нога и культя
# сливаются ВОКСЕЛЯМИ в одну водонепроницаемую оболочку (верх образца рваный: открытые цепочки и дыры, сшивка петли с
# кольцом на нём ненадёжна, v1–v2), затем срез плоскостью шва даёт одну чистую петлю
bm = bmesh.new()
rings = []
K_SEAM = 0.55          # культя уже ноги: на шасси-поле туша у плеча узкая, культя должна прятаться внутри (v2: при 0.85 торчала из груди)
for c_, k in ((cen - dir_u * 0.04, 1.0), (shoulder_s, K_SEAM), (shoulder_s + dir_u * 0.03, K_SEAM)):
    rings.append([bm.verts.new(to_b(c_ + k * (fwd * ax_f * math.cos(2 * math.pi * j / N_RING)
                                                 - lat * ax_l * math.sin(2 * math.pi * j / N_RING)))) for j in range(N_RING)])
for r0, r1 in zip(rings, rings[1:]):
    for j in range(N_RING):
        bm.faces.new([r0[j], r0[(j + 1) % N_RING], r1[(j + 1) % N_RING], r1[j]])
bm.faces.new(rings[0][::-1]); bm.faces.new(rings[-1])
stub = bpy.data.objects.new('культя', bpy.data.meshes.new('культя')); bm.to_mesh(stub.data); bm.free()
bpy.context.collection.objects.link(stub)
bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True); stub.select_set(True)
bpy.context.view_layer.objects.active = ob; bpy.ops.object.join(); me = ob.data
rm = ob.modifiers.new('rm', 'REMESH'); rm.mode = 'VOXEL'; rm.voxel_size = 0.006
bpy.ops.object.modifier_apply(modifier=rm.name)

# срез плоскостью шва через плечевой сустав (граница = одна петля) и — v5 — по ЗАПЯСТЬЮ: лапа образца при прореживании
# сливала пальцы в «веер» (критик 4.5/10), поэтому лапа строится процедурно ниже, а от образца берётся нога до запястья
WRIST_CUT = 0.15
bm = bmesh.new(); bm.from_mesh(me)
bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], dist=1e-5,
                       plane_co=to_b(shoulder_s), plane_no=Vector(to_b(dir_u)) - Vector(to_b(np.zeros(3))), clear_outer=True)
bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], dist=1e-5,
                       plane_co=Vector((0, 0, WRIST_CUT)), plane_no=Vector((0, 0, 1)), clear_inner=True)
bm.to_mesh(me); bm.free()
m = ob.modifiers.new('dec', 'DECIMATE'); m.ratio = BUDGET / max(1, 2.0 * len(me.polygons))   # воксели — квады: треугольников вдвое
bpy.ops.object.modifier_apply(modifier=m.name)
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT'); bpy.ops.mesh.quads_convert_to_tris()
bpy.ops.object.mode_set(mode='OBJECT')

# КОЛЬЦА: петля шва и петля запястья → ровно по 8 вершин (ближайшие к углам кольца, остальные растворены). Запястье —
# задел под будущий шов «конец конечности» (письмо ГеймБосса 02.10): чистая петля из 8 вершин между предплечьем и лапой
bm = bmesh.new(); bm.from_mesh(me)
bnd = [v for v in bm.verts if v.is_boundary]
seam_bnd = [v for v in bnd if v.co.z > 0.4]
wr_bnd = [v for v in bnd if v.co.z <= 0.4]
print('петли: шва %d вершин, запястья %d' % (len(seam_bnd), len(wr_bnd)))
WRC = axis_s(WRIST_CUT)
fwd0, lat0 = np.array([0.0, 0.0, 1.0]), np.array([-1.0, 0.0, 0.0])     # кольцо запястья горизонтально: вершина 0 — перёд
wl = np.array([from_b(v.co) for v in wr_bnd]) - WRC
wr_f = float(np.percentile(np.abs(wl @ fwd0), 85)); wr_l = float(np.percentile(np.abs(wl @ lat0), 85))


def reduce_loop(loop, center, f_, l_, af, al, k):
    def ang(v):
        d = from_b(v.co) - center
        return math.atan2(-(d @ l_), d @ f_) % (2 * math.pi)
    keep = []
    for j in range(N_RING):
        a_ = 2 * math.pi * j / N_RING
        keep.append(min(loop, key=lambda v: abs((ang(v) - a_ + math.pi) % (2 * math.pi) - math.pi)))
    bmesh.ops.dissolve_verts(bm, verts=[v for v in loop if v not in keep])
    for j, v in enumerate(keep):
        th = 2 * math.pi * j / N_RING
        v.co = to_b(center + k * (f_ * af * math.cos(th) - l_ * al * math.sin(th)))
    return keep


keep_v = reduce_loop(seam_bnd, shoulder_s, fwd, lat, ax_f, ax_l, K_SEAM)
# ФЛАНЕЦ — запас культи внутрь туши за кольцом шва (ГеймБосс 02.10: при отводе руки до ~30° в подмышке открывалась щель):
# ещё одно кольцо на 4 см глубже по оси культи, чуть уже; кольцо шва остаётся петлёй из 8 вершин (уже не край)
# v6d: фланец вдоль оси волчьей плечевой кости у человека смотрел ВВЕРХ и торчал над линией плеча крышкой («обод»
# критика, кадр ГеймБосса) — теперь короче и уведён внутрь, к средней линии тела (−X у правой ноги), где у любого носителя туша
FLANGE = 0.02
MEDIAL = np.array([-1.0, 0.0, 0.0])
flange = []
for j in range(N_RING):
    th = 2 * math.pi * j / N_RING
    # v6f: фланец — выпуклым куполом наружу, чуть шире кольца, на 1.5 см глубже по оси, без втягивания к средней линии.
    # «Щербину» под плечом (v6d–v6f) давал НЕ фланец: со скрытой культёй клин был тот же. Это была нижняя кромка дельты
    # человека, висевшей на лопатке и потому не гаснувшей с рукой — исправлено в графе человека (дельта на плече, 02.10)
    flange.append(bm.verts.new(to_b(shoulder_s + dir_u * 0.015 + MEDIAL * 0.01
                                    + K_SEAM * 1.08 * (fwd * ax_f * math.cos(th) - lat * ax_l * math.sin(th)))))
for j in range(N_RING):
    bm.faces.new([keep_v[j], keep_v[(j + 1) % N_RING], flange[(j + 1) % N_RING], flange[j]])
# ТОРЕЦ ФЛАНЦА ЗАКРЫТ (v6f): оболочка детали замкнута — при отводе верх культи может выйти из поля плеча, и открытая труба
# показала бы изнанку. На шасси-поле крышка внутри туши и не видна
f_cap = bm.verts.new(sum((v.co for v in flange), Vector()) / N_RING)
for j in range(N_RING):
    bm.faces.new([flange[j], flange[(j + 1) % N_RING], f_cap])
wr_keep = reduce_loop(wr_bnd, WRC, fwd0, lat0, wr_f, wr_l, 1.0)

# ЛАПА ВОЛКА — процедурная: подушка (два кольца по 8 и низ веером) и 4 пальца трубками по 6 граней с когтями-конусами.
# Пальцы — отдельные оболочки, утопленные в подушку: ключ «двуногий» разводит их в пятерню (v5)


def ring8(center, af, al):
    return [bm.verts.new(to_b(center + fwd0 * af * math.cos(2 * math.pi * j / N_RING)
                              - lat0 * al * math.sin(2 * math.pi * j / N_RING))) for j in range(N_RING)]


def bridge(r0, r1):
    for j in range(N_RING):
        bm.faces.new([r0[j], r0[(j + 1) % N_RING], r1[(j + 1) % N_RING], r1[j]])


# размер подушки — по стопе образца (вершины правой передней ноги ниже 6 см), а не по тонкой петле запястья: иначе лапа
# волка выходила игрушечной
foot_v = P[g[P[g, 1] < 0.06]]
foot_c = np.array([WRC[0], 0.0, float(np.median(foot_v[:, 2]))])
f_hl = float(np.percentile(np.abs(foot_v[:, 0] - WRC[0]), 90))                    # полуширина
f_z0, f_z1 = float(np.percentile(foot_v[:, 2], 5)), float(np.percentile(foot_v[:, 2], 95))
f_hf = (f_z1 - f_z0) / 2
print('стопа образца: полуширина %.3f, длина %.3f' % (f_hl, f_z1 - f_z0))
pad1 = ring8(np.array([WRC[0], 0.08, (WRC[2] + (f_z0 + f_z1) / 2) / 2]), max(wr_f * 1.2, f_hf * 0.75), max(wr_l * 1.1, f_hl * 0.85))
pad2 = ring8(np.array([WRC[0], 0.035, (f_z0 + f_z1) / 2]), f_hf * 0.85, f_hl)
bridge(wr_keep, pad1); bridge(pad1, pad2)
bot = bm.verts.new(to_b(np.array([WRC[0], 0.005, (f_z0 + f_z1) / 2])))
for j in range(N_RING):
    bm.faces.new([pad2[j], pad2[(j + 1) % N_RING], bot])
toes = []                                                # [(основание, все вершины пальца, вершины когтя)]
front_z = (f_z0 + f_z1) / 2 + f_hf * 0.85
for i in range(4):
    k_ = i - 1.5
    base = np.array([WRC[0] + k_ * f_hl * 0.55, 0.03, front_z - 0.035])
    ln = 0.07 if abs(k_) < 1 else 0.058
    dir_t = np.array([k_ * 0.12, -0.25, 1.0]); dir_t /= np.linalg.norm(dir_t)
    up_t = np.array([0.0, 1.0, 0.0]); up_t -= dir_t * up_t.dot(dir_t); up_t /= np.linalg.norm(up_t)
    sd_t = np.cross(dir_t, up_t)
    rings_t = []
    for t, r in ((0.0, 0.021), (0.5, 0.019), (1.0, 0.014)):
        c_ = base + dir_t * ln * t
        rings_t.append([bm.verts.new(to_b(c_ + r * (up_t * math.cos(2 * math.pi * j / 6) + sd_t * math.sin(2 * math.pi * j / 6))))
                        for j in range(6)])
    for r0, r1 in zip(rings_t, rings_t[1:]):
        for j in range(6):
            bm.faces.new([r0[j], r0[(j + 1) % 6], r1[(j + 1) % 6], r1[j]])
    bm.faces.new(rings_t[0][::-1])
    tip = base + dir_t * ln
    claw_base = [bm.verts.new(to_b(tip + 0.011 * (up_t * math.cos(2 * math.pi * j / 4) + sd_t * math.sin(2 * math.pi * j / 4))))
                 for j in range(4)]
    apex = bm.verts.new(to_b(tip + dir_t * 0.022 - up_t * 0.012))   # v6: «меньше гротеска»
    for j in range(4):
        bm.faces.new([claw_base[j], claw_base[(j + 1) % 4], apex])
    bm.faces.new(claw_base[::-1])
    toes.append((base, [v for r in rings_t for v in r] + claw_base + [apex], claw_base + [apex]))

# номера колец, лапы и пальцев — ДО подразбиения: новые вершины добавляются в конец, старые номера сохраняются
bm.verts.index_update()
seam_idx = [v.index for v in keep_v]
flange_idx = [v.index for v in flange] + [f_cap.index]
wrist_idx = [v.index for v in wr_keep]
pad_idx = set(v.index for v in pad1 + pad2 + [bot])
toe_sets = [(base, [v.index for v in vs], set(v.index for v in cl)) for base, vs, cl in toes]
keep_set = set(keep_v) | set(flange) | {f_cap} | set(wr_keep)

# ПЕТЛИ ПОПЕРЁК ПОДМЫШКИ (v6e): внутренняя сторона от верха ноги до кольца шва — подразбить рёбра, чтобы при отводе было
# чем гнуться; кольцо шва, фланец и лапа не трогаются


def armpit(v):
    q = from_b(v.co)
    return elbow_s[1] - 0.06 < q[1] < shoulder_s[1] - 0.01 and q[0] < axis_s(q[1])[0] + 0.01


sub_e = [e for e in bm.edges if all(armpit(v) and v not in keep_set for v in e.verts)]
bmesh.ops.subdivide_edges(bm, edges=sub_e, cuts=1, use_grid_fill=True)
print('подмышка: подразбито рёбер %d' % len(sub_e))
# v6f: ЗУБЦЫ ПОД РУКОЙ при отводе — рваная геометрия образца в подмышке; сглаживание внутренней стороны (кольцо, фланец, лапа — нет)
arm_v = [v for v in bm.verts if armpit(v) and v not in keep_set]
for _ in range(6):
    bmesh.ops.smooth_vert(bm, verts=arm_v, factor=0.5, use_axis_x=True, use_axis_y=True, use_axis_z=True)
bmesh.ops.triangulate(bm, faces=bm.faces[:])
bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
bm.to_mesh(me); bm.free()

# ── пересадка на суставы волка ──
ys = [0.0, wrist_y, elbow_s[1], shoulder_s[1]]
yo = [0.0, wrist_y, E_[1], S[1]]


def axis_o(y):
    if y <= W[1]:
        return np.array([W[0], y, W[2]])
    if y <= E_[1]:
        t = (y - W[1]) / (E_[1] - W[1]); return W + (E_ - W) * t
    t = (y - E_[1]) / (S[1] - E_[1]); return E_ + (S - E_) * t


def retarget(p):
    y2 = float(np.interp(p[1], ys, yo, right=S[1] + (p[1] - shoulder_s[1])))
    off = p - axis_s(p[1]); off[1] = 0
    return axis_o(y2) + off + np.array([0, y2 - axis_o(y2)[1], 0])


for v in me.vertices:
    v.co = to_b(retarget(from_b(v.co)))

# СЕЧЕНИЕ ПРЕДПЛЕЧЬЯ ПО ЛИСТУ (09.10, критик r1 4/10; срез подтвердил): у образца Meshy оно круглое и книзу колоколом —
# на 0.50 м 14 × 16 см. У листа волка — овал: вбок 9–10 см по всей длине, в профиль 14 см у локтя → 9 у запястья.
# Поясами по 3 см между запястьем и локтем: полуоси пояса (90-й перцентиль) приводятся к целевым; кольца шва и запястья
# (контракт стыка) не трогаются, переход к ним — плавный
FA_LAT = 0.048
FA_FWD = (0.045, 0.070)            # у запястья, у локтя
ring_set = set(seam_idx) | set(flange_idx) | set(wrist_idx)
P_ = np.array([from_b(v.co) for v in me.vertices])
WR = G['пясть']['b'] if 'пясть' in G else W        # НАСТОЯЩЕЕ запястье: `W` здесь — конец поля предплечья (0.50)
y_lo, y_hi = WR[1] + 0.03, E_[1] + 0.03
leg_i = [i for i in range(len(P_)) if y_lo <= P_[i, 1] < y_hi and i not in ring_set and P_[i, 1] > 0.12]
D_ = {i: P_[i] - axis_o(P_[i, 1]) for i in leg_i}


def half_axes(yc):
    """Полуоси сечения у высоты yc: 90-й перцентиль по вершинам в поясе ±4 см (кольца у трубы редкие)."""
    b = [D_[i] for i in leg_i if abs(P_[i, 1] - yc) < 0.04]
    if len(b) < 4:
        return None
    b = np.array(b)
    return float(np.percentile(np.abs(b[:, 0]), 90)), float(np.percentile(np.abs(b[:, 2]), 90))


for i in leg_i:
    y = P_[i, 1]
    ha = half_axes(y)
    if ha is None:
        continue
    t = float(np.clip((y - WR[1]) / (E_[1] - WR[1]), 0, 1))
    kl = FA_LAT / max(ha[0], 1e-3); kf = (FA_FWD[0] + (FA_FWD[1] - FA_FWD[0]) * t) / max(ha[1], 1e-3)
    kl, kf = min(kl, 1.0), min(kf, 1.0)            # только сужать: колокол и круг, а не раздувать тонкое
    fade = max(0.0, min(1.0, (y - y_lo) / 0.03, (y_hi - y) / 0.04))     # к кольцам запястья и шва — плавно
    d = D_[i]
    q = axis_o(y) + np.array([d[0] * (1 + (kl - 1) * fade), d[1], d[2] * (1 + (kf - 1) * fade)])
    me.vertices[i].co = to_b(q)
for yc in np.arange(WR[1] + 0.05, E_[1], 0.08):
    ha = half_axes(yc)
    if ha:
        print('  было у %.2f: %.3f × %.3f' % (yc, 2 * ha[0], 2 * ha[1]))
print('предплечье по листу: вбок %.3f, в профиль %.3f → %.3f' % (2 * FA_LAT, 2 * FA_FWD[0], 2 * FA_FWD[1]))
seam_m = np.array([from_b(me.vertices[i].co) for i in seam_idx])
print('кольцо шва: центр %s (плечо графа %s), средний радиус %.4f м' % (np.round(seam_m.mean(0), 3), np.round(S, 3),
      np.mean(np.linalg.norm(seam_m - seam_m.mean(0), axis=1))))

# ── арматура — граф волка в позе покоя ──
arm_d = bpy.data.armatures.new('Волк'); arm = bpy.data.objects.new('Волк', arm_d)
bpy.context.collection.objects.link(arm); bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode='EDIT')
for n in G.values():
    b = arm_d.edit_bones.new(n['name']); b.head = to_b(n['a']); b.tail = to_b(n['b'])
for n in G.values():
    if n['parent']:
        arm_d.edit_bones[n['name']].parent = arm_d.edit_bones[n['parent']]
bpy.ops.object.mode_set(mode='OBJECT')
ob.parent = arm
mod = ob.modifiers.new('Armature', 'ARMATURE'); mod.object = arm
vg_s = ob.vertex_groups.new(name='плечо'); vg_f = ob.vertex_groups.new(name='предплечье')
# ПЯСТЬ — узел-риг графа волка до запястья (02.10, слой `Rig`): конец поля предплечья на 0.50 — не сустав. Ниже 0.50 вес
# переходит на `пясть`, лапа целиком на ней: кисть вращается вокруг настоящего запястья, а не конца поля
HAS_MC = 'пясть' in G
vg_m = ob.vertex_groups.new(name='пясть') if HAS_MC else None
vg_l = ob.vertex_groups.new(name='лопатка')
AX_U = (S - E_) / np.linalg.norm(S - E_)                          # ось кости `плечо` к телу
for v in me.vertices:
    pu = from_b(v.co)
    y = pu[1]
    w = float(np.clip((y - (E_[1] - 0.03)) / 0.06, 0, 1))         # 1 — плечо, 0 — предплечье
    # ПЕРЕХОД ЛОПАТКА → ПЛЕЧО по культе (ГеймБосс 02.10: лоскут в подмышке рвался при отводе): фланец и кольцо шва — на
    # лопатке, дальше от тела вес плавно переходит к плечу; полоса дельты и подмышки — около 50/50
    t = float((pu - S) @ AX_U)                                    # >0 — внутрь туши за суставом
    wl = float(np.clip((t + 0.09) / 0.12, 0, 1)) if y > E_[1] - 0.01 else 0.0
    # ПОДМЫШКА (v6d, ГеймБосс: при отводе +10° внутренняя нижняя часть культи висела лоскутом — была целиком на `плечо`):
    # внутренняя сторона (к средней линии тела) до 16 см ниже сустава получает до половины веса на лопатку — тянется от корпуса
    # v6e (ГеймБосс: при +30° внутренняя сторона на 50/50 на коротком участке складывалась — коллапс линейного смешения):
    # градиент длиннее и мягче — 0 → 40 % по всей внутренней стороне, до 30 см ниже сустава, края сглажены
    # 02.10: ГРАДИЕНТ КОНЧАЕТСЯ НА ЛОКТЕ. Вершины на лопатке сборка ставит кадром корня цепи (плеча); с костями носителя
    # (f6215ee) локоть человека прямой, а волчий — согнут, и внутренняя сторона локтя с весом лопатки оставалась в волчьем
    # изгибе — шипом внутрь (кадр 02.10). Выше локтя на 10 см — прежний градиент, к локтю гаснет до нуля
    if y > E_[1]:
        ax_ = axis_o(y)
        med = float(np.clip((ax_[0] - pu[0]) / 0.07, 0, 1))
        down = float(np.clip((t + 0.30) / 0.30, 0, 1))
        sm = lambda u: u * u * (3 - 2 * u)
        wl = max(wl, 0.4 * sm(med) * sm(down) * sm(float(np.clip((y - E_[1]) / 0.10, 0, 1))))
    ws, wf = w * (1 - wl), 1 - w
    if wl > 0: vg_l.add([v.index], wl, 'REPLACE')
    if ws > 0: vg_s.add([v.index], ws, 'REPLACE')
    if HAS_MC and wf > 0:
        wm = wf * float(np.clip((W[1] + 0.04 - y) / 0.08, 0, 1))      # 0.54 → 0.46: предплечье → пясть
        wf -= wm
        if wm > 0: vg_m.add([v.index], wm, 'REPLACE')
    if wf > 0: vg_f.add([v.index], wf, 'REPLACE')

# ── форма-ключ «двуногий» ──
ob.shape_key_add(name='Basis', from_mix=False)
key = ob.shape_key_add(name='двуногий', from_mix=False)
seam_set = set(seam_idx) | set(flange_idx)      # кольцо шва и фланец ключ не двигает (01d §1.4)


PAW_TOP = wrist_y + 0.04       # выше — предплечье, ниже — лапа


def thick(y):
    """КЛЮЧ «двуногий» — предплечье от верха кисти до локтя. Клином: пик под
    локтем, к запястью −45 % (v5, критик). v6: обхват на 17 % меньше — геймдизайнер: «предплечья крупноваты»."""
    # v6b (критик): «меньше» не значит «без формы» — снимать у запястья, плечелучевую под локтем оставить
    # y — в системе колоколов v6 (`OLD_ELBOW`), см. `to_old`. Множители пересчитаны так, чтобы ОБХВАТ в долях предплечья
    # остался обхватом v6: прежний пик ×1.6 стоял на середине волчьего предплечья (радиус образца ~0.065), а в долях он
    # садится под локоть, где образец и так 0.083 — ком в профиль (кадр 02.10). Обхват v6 по долям 0/⅓/0.64/0.85/1:
    # 0.035/0.049/0.078/0.104/0.080 м; радиус образца там: 0.054/0.060/0.072/0.083/0.079
    return float(np.interp(y, [PAW_TOP, 0.30, 0.42, 0.50, OLD_ELBOW], [0.65, 0.82, 1.08, 1.25, 1.00]))


def fa(y):
    """Доля предплечья: 0 — верх кисти, 1 — локоть"""
    return (y - PAW_TOP) / (E_[1] - PAW_TOP)


def bell(y, c, w):
    return float(np.clip(1 - abs(y - c) / w, 0, 1))


def ua(y):
    """Доля кости `плечо`: 0 — локоть, 1 — плечевой сустав"""
    return (y - E_[1]) / (S[1] - E_[1])


# КОЛОКОЛА КЛЮЧА — В ДОЛЯХ КОСТЕЙ (02.10). До v6f они стояли в метрах под «локоть оборотня 0.56» — на 10 см ниже волчьего:
# плечо ключа шло от 0.56 до сустава, предплечье — от кисти до 0.56, то есть ключ тайно переносил локоть. С костями носителя
# (f6215ee) волчий локоть встаёт на локоть человека, и ключ обязан держать ту же раскладку. Колокола и числа v6 (их оценивали
# критик и геймдизайнер) сохранены: точка переводится в их систему по доле кости (`to_old`), а множитель поправляется на
# толщины образца в двух раскладках разные — множители `thick` и `UB` пересчитаны на обхват v6 по замеру радиусов
OLD_ELBOW = 0.56
# ПЛЕЧО в долях — на треть тоньше v6: в прежней раскладке середину плеча ключа давал толстый верх волчьего предплечья
# (0.087 на 0.62), а в долях — сама плечевая (0.054 на 0.74). Обхват v6 возвращается этим множителем; у кольца шва — 1
UB = [1.0, 1.15, 1.15, 1.05, 1.0]      # полный возврат (1.3) давал «гориллу» в анфас — кадр 02.10


def no_flap(off):
    """ЛОСКУТ ГРУДИ (02.10): труба пересечения у локтя захватывает грудь волка между ног — на 0.58–0.63 вершины уходят
    внутрь на 10–11.5 см при полуширине предплечья ~6 см. У волка лоскут в груди, у двуногого — шип внутрь у локтя (кадр
    02.10, со скелетом носителя). Внутренняя сторона дальше 5.5 см от оси сжимается втрое"""
    if off[0] < -MED_CAP:
        off[0] = -MED_CAP - (-off[0] - MED_CAP) / 3.0
    return off


MED_CAP = 0.055


def to_old(y):
    if y > E_[1]:
        return OLD_ELBOW + ua(y) * (S[1] - OLD_ELBOW)
    return PAW_TOP + fa(y) * (OLD_ELBOW - PAW_TOP)


def upper_arm(off, y):
    """ПЛЕЧО ОБОРОТНЯ (v6, геймдизайнер: «плоское вдоль тела, нет бицепса и трицепса»). На человеке плечо — волчья высота
    локоть … плечевой сустав (доли `ua`). В кадре кости: +X наружу, +Z вперёд, −Z назад. Купол дельты — наружу, вперёд и назад у плечевого сустава;
    бицепс — спереди посередине; трицепс — сзади повыше; перехват к локтю. Волчья лопатка (масса назад в культе) — вдвое мельче."""
    d = bell(y, 0.77, 0.04)                     # дельта вперёд-назад — у сустава, к самому кольцу гаснет (v6d: «обод» сверху)
    dl = bell(y, 0.74, 0.08)                    # дельта наружу — ниже, клином к середине плеча (v6c: низ полкой, «наплечник»)
    bi = bell(y, 0.66, 0.06)                    # бицепс
    tri = bell(y, 0.71, 0.06)                   # трицепс
    nk = bell(y, 0.575, 0.03)                   # перехват к локтю
    k = 1.0 - 0.12 * nk
    if off[0] > 0:
        off[0] *= k * (1 + 0.55 * dl + 0.10 * bi)
    else:
        off[0] *= k
    if off[2] > 0:
        off[2] *= k * (1 + 0.35 * d + 0.45 * bi)
    else:
        off[2] *= k * (1 + 0.05 * d + 0.45 * tri)          # v6b: купол назад торчал «эполетом» за спину
    return off


WR = np.array([W[0], wrist_y, W[2]])      # запястье волка на оси ноги — центр поворота кисти


def rot_x(rel, deg):
    t = math.radians(deg); c, s_ = math.cos(t), math.sin(t)
    return np.array([rel[0], c * rel[1] - s_ * rel[2], s_ * rel[1] + c * rel[2]])


def rot_y(rel, deg):
    t = math.radians(deg); c, s_ = math.cos(t), math.sin(t)
    return np.array([c * rel[0] + s_ * rel[2], rel[1], -s_ * rel[0] + c * rel[2]])


def hand0(p):
    """Кисть в кадре «пальцы вниз, ладонь назад» (до доворота): волчья лапа повёрнута вокруг запястья на 90° по X;
    ладонь не шире верха предплечья. Форма, не поза: кости кисти в графе нет (письмо ГеймБосса 02.10)"""
    rel = rot_x(p - WR, 90.0)
    # v6c: у самого запястья кисть уже (×0.6), к пальцам — ×0.85: верх подушки был шире запястья — «манжета» (критик)
    f_ = float(np.clip(-rel[1] / 0.07, 0, 1))
    rel[0] *= 0.6 + 0.25 * f_; rel[1] *= 1.25; rel[2] *= 0.35 + 0.10 * f_
    return rel


def place_hand(rel):
    """v6: ЛАДОНЬЮ К БЕДРУ — доворот на 90° вокруг оси предплечья: ладонь (−Z) → внутрь (−X, к средней линии). В v5 ладонь
    смотрела назад и веер пальцев лежал во фронтальной плоскости (геймдизайнер: «кисти развёрнуты ладонями вперёд»)"""
    return WR + rot_y(rel, HAND_TURN)


HAND_TURN = 55.0     # v6b: строго ребром (90°) кисть пропадала в анфас; на листе ладонь к бедру и вперёд на 30–45°


toe_of = {}
for t_i, (base, ids, claw) in enumerate(toe_sets):
    for i in ids:
        toe_of[i] = t_i
base_t = [np.mean([hand0(from_b(me.vertices[i].co)) for i in ids[:6]], axis=0) for base, ids, claw in toe_sets]

for i, kv in enumerate(key.data):
    if i in seam_set or os.environ.get('NOKEY'):
        continue
    p = from_b(kv.co)
    y = p[1]
    if i in toe_of:
        # ПАЛЬЦЫ — хватка, а не грабли (критик v5, геймдизайнер «меньше гротеска»): средние длиннее (×1.6), крайние ×1.35;
        # веер узкий; пальцы согнуты к ладони (−Z) тем сильнее, чем дальше от основания; когти — к ладони
        t_i = toe_of[i]; k_ = t_i - 1.5
        q = hand0(p); d = q - base_t[t_i]
        b_ = base_t[t_i] * np.array([0.6, 1.0, 1.0])          # основания пальцев ближе друг к другу — кисть, а не веер
        d *= 1.6 if abs(k_) < 1 else 1.35
        L_ = abs(d[1])
        d[0] += k_ * 0.08 * L_
        d[2] -= 0.55 * max(0.0, L_ - 0.02)
        if i in toe_sets[t_i][2]:
            # КОГТИ — длина ~0.3 пальца и загиб к ладони (v6c: «стали ногтями» — «скромнее» было про веер, а не про когти)
            n_ = d / max(1e-6, np.linalg.norm(d))
            d += n_ * 0.016
            d[2] -= 0.018
        p = place_hand(b_ + d)
    elif y > E_[1]:
        ax = axis_o(y); off = p - ax; off[1] = 0
        off = no_flap(off)
        if off[2] < 0:
            off[2] *= 0.45                       # волчья лопатка в культе — масса назад вдвое мельче (v6, по настоящей высоте)
        yo = to_old(y)
        off = upper_arm(off * float(np.interp(ua(y), [0.0, 0.25, 0.5, 0.75, 1.0], UB)), yo)
        p = ax + off + np.array([0, y - ax[1], 0])
    elif y > PAW_TOP:
        ax = axis_o(y); off = p - ax; off[1] = 0
        yo = to_old(y)
        off = no_flap(off) * thick(yo)
        if off[2] < 0:                           # точка локтя
            off[2] -= 0.03 * bell(yo, OLD_ELBOW, 0.05)
        p = ax + off + np.array([0, y - ax[1], 0])
    else:
        p = place_hand(hand0(p))
    kv.co = to_b(p)

# переход кисть ↔ предплечье: у вершин чуть выше лапы — половина поворота, иначе запястье рвётся
for i, kv in enumerate(key.data):
    if i in seam_set or os.environ.get("NOKEY"):
        continue
    p0 = from_b(me.vertices[i].co)
    if PAW_TOP < p0[1] < PAW_TOP + 0.05:
        w = (PAW_TOP + 0.05 - p0[1]) / 0.05
        q = WR + rot_y(rot_x(p0 - WR, 90.0 * 0.5 * w), HAND_TURN * 0.5 * w)
        cur = from_b(kv.co)
        kv.co = to_b(cur * (1 - 0.5 * w) + q * 0.5 * w)

tris = sum(len(p.vertices) - 2 for p in me.polygons)
print('нога: %d → %d тр (с культёй)' % (n0, tris))

# ── кадр для проверки: профиль, оба ключа ──
if SHOT:
    sc = bpy.context.scene; sc.render.engine = 'BLENDER_WORKBENCH'
    sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'SINGLE'; sc.display.shading.single_color = (0.7, 0.7, 0.72)
    sc.world = bpy.data.worlds.new('w'); sc.render.resolution_x, sc.render.resolution_y = 900, 900
    for p in me.polygons: p.use_smooth = False
    cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); bpy.context.collection.objects.link(cam)
    cam.data.type = 'ORTHO'; cam.data.ortho_scale = 1.0; sc.camera = cam
    # камера по умолчанию смотрит вдоль −Z: поворот (90°, 0, 90°) — вдоль −X (бок), (90°, 0, 0) — вдоль +Y (на морду)
    for vw, rot, loc in (('bok', (math.radians(90), 0, math.radians(90)), Vector((5, -0.45, 0.45))),
                         ('pered', (math.radians(90), 0, 0), Vector((-0.09, -5, 0.45)))):
        cam.rotation_euler = rot; cam.location = loc
        for kk, val in (('basis', 0.0), ('dvunogij', 1.0)):
            key.value = val
            sc.render.filepath = SHOT.replace('.png', '-%s-%s.png' % (vw, kk)); bpy.ops.render.render(write_still=True)
    key.value = 0.0

# ── ПРОВЕРКА ОТВОДА (ГеймБосс 02.10): кость `плечо` наружу на 0/15/30° вокруг оси «вперёд» — нет ли разрыва в подмышке
if SHOT:
    from mathutils import Matrix
    bpy.context.view_layer.objects.active = arm; bpy.ops.object.mode_set(mode='POSE')
    pb = arm.pose.bones['плечо']
    head = pb.head.copy()
    key.value = 1.0
    cam.rotation_euler = (math.radians(90), 0, 0); cam.location = Vector((-0.09, -5, 0.6)); cam.data.ortho_scale = 0.8
    for deg in (0, 15, 30):
        pb.matrix_basis = Matrix.Identity(4)
        bpy.context.view_layer.update()
        # наружу для правой руки (Unity +X = Blender −X) — поворот вокруг оси «вперёд» (Blender −Y) через голову кости
        Rm = Matrix.Translation(head) @ Matrix.Rotation(math.radians(deg), 4, Vector((0, 1, 0))) @ Matrix.Translation(-head)
        pb.matrix = Rm @ pb.matrix
        bpy.context.view_layer.update()
        sc.render.filepath = SHOT.replace('.png', '-otvod-%02d.png' % deg); bpy.ops.render.render(write_still=True)
    pb.matrix_basis = Matrix.Identity(4); key.value = 0.0
    bpy.ops.object.mode_set(mode='OBJECT')

# ── КУЛЬТЯ — ОТДЕЛЬНЫМ ОБЪЕКТОМ (ГеймБосс 02.10): выше подмышки (высота локтя графа) — скиннед-объект `культя` с кольцом
# шва; на шасси-поле сборка её не рисует, верх ноги упирается в тушу. Веса и ключ переезжают вместе с гранями
bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True); bpy.context.view_layer.objects.active = ob
bpy.ops.object.mode_set(mode='EDIT')
bm = bmesh.from_edit_mesh(me)
# РЕЗ НА 5 СМ ВЫШЕ ЛОКТЯ (на 14 плечевая часть выходила из груди планкой, на 8 — светлой гранью над контуром, критик r3) (09.10, критик r2 «срезанные торцы над передними ногами»): у волка по листу дно груди у ноги на
# 0.70–0.72, а рез стоял на локте (0.64) — на своём поле культю не рисуют, поле плеча детали выключено, и между грудью и
# верхом ноги светились щель и срезанная труба; плечевой кости не было вовсе. Теперь нога уходит в тушу, торец внутри
cut_y = float(E_[1]) + 0.05
bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], dist=1e-5,
                       plane_co=Vector((0, 0, cut_y)), plane_no=Vector((0, 0, 1)))
for f in bm.faces:
    f.select = f.calc_center_median().z > cut_y
for v in bm.verts:
    v.select = any(f.select for f in v.link_faces)
bmesh.update_edit_mesh(me)
bpy.ops.mesh.separate(type='SELECTED')
bpy.ops.object.mode_set(mode='OBJECT')
stump = [o for o in bpy.context.selected_objects if o is not ob][0]
stump.name = 'культя'; stump.data.name = 'культя'
seam_b = [to_b(p) for p in seam_m]
seam_idx = [min(range(len(stump.data.vertices)), key=lambda i: (stump.data.vertices[i].co - q).length) for q in seam_b]
tris_main = sum(len(p.vertices) - 2 for p in ob.data.polygons)
tris_stump = sum(len(p.vertices) - 2 for p in stump.data.polygons)
print('разделено: Руки %d тр, культя %d тр (выше %.3f); кольцо в культе %s' % (tris_main, tris_stump, cut_y, seam_idx))

# ── экспорт ──
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=fbx, use_selection=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z',
                         axis_up='Y', object_types={'ARMATURE', 'MESH'}, add_leaf_bones=False, bake_anim=False,
                         mesh_smooth_type='OFF', use_armature_deform_only=False)
cen_m = seam_m.mean(0)
json.dump(dict(species='Волк', slot='Руки', plan='четвероногий',
               seam=dict(type='плечо', ring=seam_idx, ring_m=[[round(c, 4) for c in p] for p in seam_m],
                         center_m=[round(c, 4) for c in cen_m],
                         ellipse_m=[round(ax_f * K_SEAM, 4), round(ax_l * K_SEAM, 4)],
                         note='метры тела волка, оси Unity; вершина 0 — перёд тела, обход против часовой при взгляде из детали к телу; '
                              'индексы — порядок вершин в Blender (Unity при плоских гранях делит вершины — сверять по ring_m)'),
               objects=dict(main='Руки', stump='культя', stump_note='культя — от подмышки (высота локтя графа %.3f) до кольца шва; '
                                                                 'на шасси-поле не рисуется, кольцо и индексы ring — в ней' % cut_y),
               bones=['лопатка', 'плечо', 'предплечье'] + (['пясть'] if HAS_MC else []), armature='граф волка в позе покоя (все узлы)', keys=['двуногий'],
               tris=tris, tris_main=tris_main, tris_stump=tris_stump, source='Anatomy/species/wolf/ref/mv/volk_mv_A.glb (лист volk-prirodnyj_meshy), выровнен obrazec_v_obj.py',
               generator='Tools/Blender/detali/noga_iz_obrazca.py'),
          open(passport, 'w', encoding='utf-8'), ensure_ascii=False, indent=2)
print('ГОТОВО: %d тр' % tris)
