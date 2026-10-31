"""ИГЛЫ ЕЖА НА ШАБЛОНЕ ЧЕЛОВЕКА (30.10, опись органов №2: Шкура и Игломёт ежа на человеке не видны). Раскладка — в координатах
ТЕЛА, а не вершин (файл `--raskladka`, JSON): игла — луч от оси корпуса (вертикаль x 0 через середину торса по глубине на своей
высоте) или от середины черепа под углом φ от спины (0 — назад, ±90 — вбок), точка — где луч вышел на кожу. Поэтому раскладка
переживает смену базы (поджарый ключ, ступени): перегнать инструмент — иглы сядут на новую кожу.

  Шкура@Ёж — поле игл-пирамидок (4 грани + дно, лоу-поли как лист `витрина/человек-ёж/ступени.png`) по спине, плечам и затылку:
    сетка по высоте и углу с шагом `shag`, ряды со сдвигом в полшага, разброс `razbros`; длина — `dlina` с затуханием к краям поля
    (`zatuh`), наклон от нормали к низу на `naklon`°;
  Игломёт@Ёж — трубы (6 граней, стенка, открытый торец) между лопатками, ось — нормаль кожи, повёрнутая вверх на `vverh`°.
Веса — барицентрически с треугольника кожи под основанием (игла едет с кожей). Выход — blend с объектами `иглы` и `игломёт`
(как источник для `organ_objekt.py … иглы Шкура@Ёж`).

  blender -b --factory-startup -P shipy_ezha.py -- шасси.blend раскладка.json выход.blend [--kluch Поджарый]
"""
import sys, json, math
import bpy, bmesh
import numpy as np
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

a = sys.argv[sys.argv.index('--') + 1:]
src, lay_p, out = a[:3]
opt = {}
i = 3
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
L = json.load(open(lay_p, encoding='utf-8'))
bpy.ops.wm.open_mainfile(filepath=src)
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]; KOSTI = {b.name for b in arm.data.bones}
BEZ = set(L.get('bez', ['Чутьё']))
T, TF, TW = [], [], []
for b in bpy.data.objects:
    if b.type != 'MESH' or '@' in b.name or b.name in BEZ: continue
    kb = b.data.shape_keys.key_blocks.get(opt['kluch']) if opt.get('kluch') and b.data.shape_keys else None
    src_v = kb.data if kb else b.data.vertices
    gb = {g.index: g.name for g in b.vertex_groups}; n0 = len(T)
    T += [b.matrix_world @ d.co for d in src_v]
    TW += [{gb[g.group]: g.weight for g in v.groups if g.weight > 0 and gb[g.group] in KOSTI} for v in b.data.vertices]
    for p in b.data.polygons:
        vs = list(p.vertices); TF += [[n0 + vs[0], n0 + vs[k], n0 + vs[k + 1]] for k in range(1, len(vs) - 1)]
bvh = BVHTree.FromPolygons(T, TF)
TA = np.array([list(t) for t in T])


def y_mid(z, x_max=0.12):
    s = TA[(np.abs(TA[:, 2] - z) < 0.01) & (np.abs(TA[:, 0]) < x_max)]
    return float((s[:, 1].min() + s[:, 1].max()) / 2) if len(s) else 0.0


def kozha(o_, d_):
    """Луч изнутри наружу: последняя кожа по лучу (ищем снаружи внутрь — первую)."""
    h = bvh.ray_cast(o_ + d_ * 0.6, -d_)
    if h[0] is None: return None
    n = h[1].normalized()
    if n.dot(d_) < 0: n = -n
    tri = TF[h[2]]
    # барицентрика точки в треугольнике → веса
    # над точкой по нормали есть ещё кожа — это внутренняя стенка (объекты шасси перекрываются на плече): игла села бы под кожей
    # дном наружу (тёмные квадраты на кадре 30.10) — такое попадание не в счёт
    if bvh.ray_cast(h[0] + n * 0.003, n, 0.15)[0] is not None: return None
    A, B, C = (T[k] for k in tri); v0, v1, v2 = B - A, C - A, h[0] - A
    d00, d01, d11, d20, d21 = v0.dot(v0), v0.dot(v1), v1.dot(v1), v2.dot(v0), v2.dot(v1)
    den = d00 * d11 - d01 * d01 or 1e-12
    bv = (d11 * d20 - d01 * d21) / den; bw = (d00 * d21 - d01 * d20) / den; bu = 1 - bv - bw
    w = {}
    for k, c in zip(tri, (bu, bv, bw)):
        for g, x in TW[k].items(): w[g] = w.get(g, 0) + x * max(c, 0)
    s = sum(w.values()) or 1
    return h[0], n, {g: x / s for g, x in w.items()}


def obj(name, V, F, Wv):
    me = bpy.data.meshes.new(name); me.from_pydata(V, [], F); me.update()
    o = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(o); o.parent = arm
    m = o.modifiers.new('Armature', 'ARMATURE'); m.object = arm
    # нормали наружу (дно пирамидки и торцы труб собирались разной намоткой — тёмные пятна изнанки)
    bm = bmesh.new(); bm.from_mesh(me); bmesh.ops.recalc_face_normals(bm, faces=bm.faces); bm.to_mesh(me); bm.free(); me.update()
    for vi, w in enumerate(Wv):
        for g, x in w.items():
            vg = o.vertex_groups.get(g) or o.vertex_groups.new(name=g); vg.add([vi], x, 'REPLACE')
    return o


rng = np.random.default_rng(L.get('sid', 30))
# ---------- Шкура: поле игл ----------
S = L['shkura']; V, F, Wv = [], [], []; n_ig = 0
HC = Vector(L.get('golova_c', [0, 0.0, 1.74]))
for zona in S['zony']:
    z0, z1 = zona['z']; sh = zona.get('shag', S['shag'])
    nz = max(1, int(round((z1 - z0) / sh)))
    for iz in range(nz + 1):
        z = z0 + (z1 - z0) * iz / nz
        t = (z - z0) / max(z1 - z0, 1e-6)
        fmax = zona['fi'][0] + (zona['fi'][1] - zona['fi'][0]) * t       # полуугол поля на этой высоте (линейно от низа к верху)
        r_ = 0.2 if zona.get('golova') else 0.15
        nf = max(1, int(round(math.radians(fmax) * r_ / sh)))
        for jf in range(-nf, nf + 1):
            f = fmax * (jf + 0.5 * (iz % 2)) / max(nf, 1)
            if abs(f) > fmax: continue
            f += rng.uniform(-1, 1) * S['razbros'] * math.degrees(sh / r_); zz = z + rng.uniform(-1, 1) * S['razbros'] * sh * 0.5
            fr = math.radians(f)
            d = Vector((math.sin(fr), math.cos(fr), 0))                    # 0 — назад (+y Blender), ±90 — вбок
            if zona.get('golova'):
                d = Vector((math.sin(fr), math.cos(fr), zona.get('vverh', 0.6))).normalized(); o_ = HC + Vector((0, 0, zz - HC.z))
            else:
                o_ = Vector((0, y_mid(zz), zz))
            k_ = kozha(o_, d)
            if k_ is None: continue
            p, n, w = k_
            # затухание: к краю по углу и к низу зоны
            e = min(1.0, (1 - abs(f) / max(fmax, 1e-6)) / S['zatuh'], (t + 0.05) / S['zatuh']) if not zona.get('golova') else min(1.0, (1 - abs(f) / max(fmax, 1e-6)) / S['zatuh'])
            h = zona.get('dlina', S['dlina']) * (0.35 + 0.65 * max(e, 0)) * rng.uniform(0.85, 1.15)
            if h < 0.02: continue
            down = Vector((0, 0, -1)); ax = (n * math.cos(math.radians(S['naklon'])) + (down - n * n.dot(down)).normalized() * math.sin(math.radians(S['naklon']))).normalized()
            u = (down - n * n.dot(down)).normalized(); s_ = n.cross(u).normalized()
            rb = S['osnova'] * (h / S['dlina']) ** 0.5
            base = [p + (u * math.cos(math.pi / 2 * q + 0.785) + s_ * math.sin(math.pi / 2 * q + 0.785)) * rb - n * (0.004 + 0.5 * rb) for q in range(4)]   # основание утоплено на полрадиуса: на выпуклом плече углы дна выходили из кожи
            tip = p + ax * h
            b0 = len(V); V += base + [tip]; Wv += [w] * 5
            F += [[b0 + q, b0 + (q + 1) % 4, b0 + 4] for q in range(4)] + [[b0 + 3, b0 + 2, b0 + 1, b0]]
            n_ig += 1
obj('иглы', V, F, Wv)
print('ИГЛЫ: Шкура — %d игл' % n_ig)
# ---------- Игломёт: трубы ----------
G = L['iglomet']; V, F, Wv = [], [], []
for x_ in G['x']:
    zt = G['z']; o_ = Vector((x_, y_mid(zt), zt)); k_ = kozha(o_, Vector((0, 1, 0)))
    if k_ is None: continue
    p, n, w = k_
    up = Vector((0, 0, 1)); ax = (n * math.cos(math.radians(G['vverh'])) + (up - n * n.dot(up)).normalized() * math.sin(math.radians(G['vverh']))).normalized()
    ax = (ax + Vector((x_, 0, 0)) * G.get('veer', 0)).normalized()
    r1 = ax.cross(Vector((1, 0, 0))).normalized(); r2 = ax.cross(r1).normalized()
    R_, Ri, Ln, ns = G['r'], G['r'] * 0.6, G['dlina'], 6
    b0 = len(V)
    for ring, (rad, off) in enumerate([(R_, -0.02), (R_, Ln), (Ri, Ln), (Ri, Ln - 0.04)]):
        for q in range(ns):
            ang = 2 * math.pi * q / ns
            V.append(p + ax * off + (r1 * math.cos(ang) + r2 * math.sin(ang)) * rad); Wv.append(w)
    for r_i in range(3):
        for q in range(ns):
            a0, a1 = b0 + r_i * ns + q, b0 + r_i * ns + (q + 1) % ns
            F.append([a0, a1, a1 + ns, a0 + ns])
    F.append([b0 + q for q in range(ns)][::-1]); F.append([b0 + 3 * ns + q for q in range(ns)])
obj('игломёт', V, F, Wv)
print('ИГЛЫ: Игломёт — %d труб' % len(G['x']))
bpy.ops.wm.save_as_mainfile(filepath=out)
