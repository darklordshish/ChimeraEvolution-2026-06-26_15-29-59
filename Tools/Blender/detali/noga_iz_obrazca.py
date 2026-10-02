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
    apex = bm.verts.new(to_b(tip + dir_t * 0.03 - up_t * 0.016))
    for j in range(4):
        bm.faces.new([claw_base[j], claw_base[(j + 1) % 4], apex])
    bm.faces.new(claw_base[::-1])
    toes.append((base, [v for r in rings_t for v in r] + claw_base + [apex], claw_base + [apex]))

bmesh.ops.triangulate(bm, faces=bm.faces[:])
bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
bm.verts.index_update()
seam_idx = [v.index for v in keep_v]
wrist_idx = [v.index for v in wr_keep]
pad_idx = set(v.index for v in pad1 + pad2 + [bot])
toe_sets = [(base, [v.index for v in vs], set(v.index for v in cl)) for base, vs, cl in toes]
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
for v in me.vertices:
    y = from_b(v.co)[1]
    w = float(np.clip((y - (E_[1] - 0.03)) / 0.06, 0, 1))         # 1 — плечо, 0 — предплечье
    if w > 0: vg_s.add([v.index], w, 'REPLACE')
    if w < 1: vg_f.add([v.index], 1 - w, 'REPLACE')

# ── форма-ключ «двуногий» ──
ob.shape_key_add(name='Basis', from_mix=False)
key = ob.shape_key_add(name='двуногий', from_mix=False)
seam_set = set(seam_idx)


PAW_TOP = wrist_y + 0.04       # выше — предплечье, ниже — лапа


def thick(y):
    """КЛЮЧ «двуногий», v5 (критик: v3 «колбаса» 3/10, v4 «колокол» 4.5/10). Множитель толщины по высоте на волке.
    На человеке (калибр Donor) волчья кость `плечо` короче человеческой: сустав E (0.66) ложится на 0.71 роста, локоть
    оборотня по листу — на ~0.65 роста ≈ 0.56 по высоте волка. Ритм листа: дельта 0.10 — локоть 0.065 — верх предплечья
    0.085 — запястье 0.05: предплечье КЛИНОМ, пик сразу под локтем, к запястью −45 %."""
    return float(np.interp(y, [PAW_TOP, 0.30, 0.42, 0.50, 0.56, 0.60, E_[1], (E_[1] + S[1]) / 2, S[1]],
                           [0.85, 1.10, 1.45, 1.70, 1.05, 1.15, 1.05, 1.10, 1.0]))


WR = np.array([W[0], wrist_y, W[2]])      # запястье волка на оси ноги — центр поворота кисти


def rot_x(rel, deg):
    t = math.radians(deg); c, s_ = math.cos(t), math.sin(t)
    return np.array([rel[0], c * rel[1] - s_ * rel[2], s_ * rel[1] + c * rel[2]])


def hand(p):
    """Кисть: волчья лапа (подошва вниз, пальцы вперёд) → кисть оборотня (пальцы вниз вдоль предплечья, ладонью назад).
    Форма, не поза: кости кисти в графе нет (письмо ГеймБосса 02.10). Ладонь не шире верха предплечья (критик v4)"""
    rel = rot_x(p - WR, 90.0)
    rel[0] *= 0.85; rel[1] *= 1.25; rel[2] *= 0.7
    return WR + rel


toe_of = {}
for t_i, (base, ids, claw) in enumerate(toe_sets):
    for i in ids:
        toe_of[i] = t_i
base_t = [np.mean([hand(from_b(me.vertices[i].co)) for i in ids[:6]], axis=0) for base, ids, claw in toe_sets]

for i, kv in enumerate(key.data):
    if i in seam_set:
        continue
    p = from_b(kv.co)
    y = p[1]
    if i in toe_of:
        # ПАЛЬЦЫ — пятерня: длиннее ×1.8 от основания, веером врозь, когти загнуты к бедру (внутрь, −X) и к ладони
        t_i = toe_of[i]; k_ = t_i - 1.5
        q = hand(p); d = q - base_t[t_i]
        d *= 1.8
        d[0] += k_ * 0.25 * abs(d[1])
        q = base_t[t_i] + d
        if i in toe_sets[t_i][2]:
            q += np.array([-0.018, 0.0, -0.012])
        p = q
    elif y > E_[1]:
        # КУЛЬТЯ → ПЛЕЧО ОБОРОТНЯ: волчья лопатка (масса назад) в профиль мельче, купол дельты — наружу и вверх
        ax = axis_o(y); off = p - ax; off[1] = 0
        k = thick(y)
        off[2] *= (0.4 if off[2] < 0 else 1.0) * k
        dome = float(np.interp(y, [E_[1], (E_[1] + S[1]) / 2, S[1]], [0.0, 1.0, 0.5]))
        off[0] *= k * (1.0 + 0.45 * dome) if off[0] > 0 else k
        p = ax + off + np.array([0, y - ax[1] + 0.02 * dome, 0])
    elif y > PAW_TOP:
        ax = axis_o(y); off = p - ax; off[1] = 0
        off *= thick(y)
        # ТОЧКА ЛОКТЯ (олекранон) — клин назад у локтя человека; 2.5 см тонули (критик v4), теперь 4.5 см
        if off[2] < 0:
            off[2] -= 0.045 * float(np.clip(1 - abs(y - 0.56) / 0.05, 0, 1))
        p = ax + off + np.array([0, y - ax[1], 0])
    else:
        p = hand(p)
    kv.co = to_b(p)

# переход кисть ↔ предплечье: у вершин чуть выше лапы — половина поворота, иначе запястье рвётся
for i, kv in enumerate(key.data):
    if i in seam_set:
        continue
    p0 = from_b(me.vertices[i].co)
    if PAW_TOP < p0[1] < PAW_TOP + 0.05:
        w = (PAW_TOP + 0.05 - p0[1]) / 0.05
        q = WR + rot_x(p0 - WR, 90.0 * 0.5 * w)
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

# ── КУЛЬТЯ — ОТДЕЛЬНЫМ ОБЪЕКТОМ (ГеймБосс 02.10): выше подмышки (высота локтя графа) — скиннед-объект `культя` с кольцом
# шва; на шасси-поле сборка её не рисует, верх ноги упирается в тушу. Веса и ключ переезжают вместе с гранями
bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True); bpy.context.view_layer.objects.active = ob
bpy.ops.object.mode_set(mode='EDIT')
bm = bmesh.from_edit_mesh(me)
cut_y = float(E_[1])
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
               bones=['плечо', 'предплечье'], armature='граф волка в позе покоя (все узлы)', keys=['двуногий'],
               tris=tris, tris_main=tris_main, tris_stump=tris_stump, source='Anatomy/species/wolf/ref/mv/volk_mv_A.glb (лист volk-prirodnyj_meshy), выровнен obrazec_v_obj.py',
               generator='Tools/Blender/detali/noga_iz_obrazca.py'),
          open(passport, 'w', encoding='utf-8'), ensure_ascii=False, indent=2)
print('ГОТОВО: %d тр' % tris)
