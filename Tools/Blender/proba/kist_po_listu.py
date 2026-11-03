"""КИСТЬ ПО ЛИСТУ (03.11, критик r2: «четыре пальца слиты в клин, большой висит брусом»; ГеймБосс: в первом лице кисть вплотную). У шаблона К2
сама форма — кулак-варежка, правками её не получить. Кисть строится заново в объекте `Руки`:
  1. рез горизонтальной плоскостью запястья (`--z`), нижняя часть удаляется; кольцо реза — край для ладони;
  2. ладонь — кольца из тех же N вершин: запястье → середина (бугор большого пальца с передне-внутренней стороны) → косточки → дно, тыл — одна
     плоскость, косточки — ребро;
  3. пальцы и большой — трубки 4 грани, 3 фаланги с изломом на суставах, основание утоплено в ладонь (закрыто, не видно), длины с листа;
  4. веса — среднее кольца реза (вся кисть едет с предплечьем); в каждую форму-ключ новые вершины идут с тем же смещением, что среднее кольца
     реза в этом ключе. Рамка: d — вниз вдоль кисти, w — поперёк ладони вперёд (к большому пальцу), n — наружу (тыл). Кисть симметрична.

  blender -b вход.blend --factory-startup -P kist_po_listu.py -- выход.blend [--z 0.96] [--imya Поджарый] [--mash 1.12 — масштаб от запястья] [--naruzhu 0.006 — сдвиг наружу, м]
"""
import sys, math
import bpy, bmesh
import numpy as np
from mathutils import Vector

a = sys.argv[sys.argv.index('--') + 1:]
out = a[0]
opt = dict(z='0.96', imya='Поджарый', mash='1', naruzhu='0')
i = 1
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
ZC = float(opt['z']); MS, NR = float(opt['mash']), float(opt['naruzhu'])
o = bpy.data.objects['Руки']; Mw = o.matrix_world; iw = Mw.inverted()
keys = [k.name for k in o.data.shape_keys.key_blocks]
n_v0, n_f0 = len(o.data.vertices), len(o.data.polygons)
# рез один на обе руки: горизонтальная плоскость по всему объекту (рез с наклоном по части геометрии снимал грани выше запястья — дыры на предплечье);
# ниже `--z` у `Руки` только кисти
bm = bmesh.new(); bm.from_mesh(o.data)
pl_co = iw @ Vector((0, 0, ZC)); pl_no = (iw.to_3x3() @ Vector((0, 0, -1))).normalized()
bmesh.ops.bisect_plane(bm, geom=list(bm.verts) + list(bm.edges) + list(bm.faces), plane_co=pl_co, plane_no=pl_no, clear_outer=True, clear_inner=False, dist=1e-5)
bm.to_mesh(o.data); bm.free(); o.data.update()
for side in (-1, 1):     # −1: правая (x < 0)
    bm = bmesh.new(); bm.from_mesh(o.data); bm.verts.ensure_lookup_table()
    sh = {k: bm.verts.layers.shape[k] for k in keys}; dv = bm.verts.layers.deform.active
    W_ = lambda v: Mw @ v.co
    d = Vector((side * -0.05, -0.10, -1.0)).normalized()
    bm.edges.ensure_lookup_table()
    # кольцо реза: открытые рёбра на плоскости, своя сторона
    on = [e for e in bm.edges if e.is_boundary and all(abs((v.co - pl_co).dot(pl_no)) < 1e-4 and W_(v).x * side > 0.18 for v in e.verts)]
    if not on: sys.exit('КИСТЬ: кольца реза нет')
    adj = {}
    for e in on:
        for v in e.verts: adj.setdefault(v, []).append(e)
    if any(len(q) != 2 for q in adj.values()): sys.exit('КИСТЬ: кольцо реза ветвится: %s' % sorted({len(q) for q in adj.values()}))
    start = on[0].verts[0]; ring = [start]; prev = None; cur = start
    while True:
        nxt = next((e.other_vert(cur) for e in adj[cur] if e.other_vert(cur) is not prev), None)
        if nxt is None or nxt is start: break
        ring.append(nxt); prev, cur = cur, nxt
    N = len(ring)
    if N != len(on): sys.exit('КИСТЬ: кольцо реза не одно: обход %d из %d рёбер' % (N, len(on)))
    # рамка
    c = sum((W_(v) for v in ring), Vector()) / N
    nn = Vector((side, 0, 0)); w = d.cross(nn).normalized()
    if w.y > 0: w = -w
    nn = w.cross(d).normalized()
    if nn.x * side < 0: nn = -nn
    # смещение ключей и веса кольца
    dk = {k: sum(((v[sh[k]] - v.co) for v in ring), Vector()) / N for k in keys}
    wt = {}
    for v in ring:
        for g, x in v[dv].items(): wt[g] = wt.get(g, 0) + x / N
    top = sorted(wt.items(), key=lambda t: -t[1])[:4]; sm = sum(x for _, x in top); wt = {g: x / sm for g, x in top}

    def nv(p):
        r_ = p - c; h_ = r_.dot(d); p = c + r_ * MS + nn * NR * min(1.0, max(h_, 0) / 0.06)
        q = bm.verts.new(iw @ p)
        for k in keys: q[sh[k]] = q.co + dk[k]
        for g, x in wt.items(): q[dv][g] = x
        return q

    def face(vs):
        try: return bm.faces.new(vs)
        except ValueError: return None

    # углы кольца вокруг оси
    def ug(v):
        r_ = W_(v) - c; return math.atan2(r_.dot(w), r_.dot(nn))
    # ладонь: (доля вниз м, полу-ширина по w, полу-толщина по n, сдвиг по w, сдвиг по n)
    RY = [(0.026, 0.033, 0.024, 0.002, 0.0), (0.060, 0.045, 0.025, 0.003, -0.003), (0.092, 0.046, 0.016, 0.0, 0.0)]
    prev_r = ring; rows = []
    for dz, hw, ht, sw, sn in RY:
        row = []
        for v in ring:
            t = ug(v); cs, sn_ = math.cos(t), math.sin(t)
            # суперэллипс: тыл (cos > 0) плоский, косточки ребром
            ex = 0.6; ca = math.copysign(abs(cs) ** ex, cs); sa = math.copysign(abs(sn_) ** ex, sn_)
            ofs = w * (sw + hw * sa) + nn * (sn + ht * ca)
            # бугор большого пальца: вперёд-внутрь в средних кольцах
            if dz < 0.08 and sa > 0.2 and ca < 0.2: ofs += (w * 0.008 - nn * 0.008)
            row.append(nv(c + d * dz + ofs))
        rows.append(row)
    all_rows = [ring] + rows
    for ra, rb in zip(all_rows, all_rows[1:]):
        for k in range(N):
            f = face([ra[k], ra[(k + 1) % N], rb[(k + 1) % N], rb[k]])
    # дно ладони
    cb = nv(c + d * 0.10 + nn * 0.002)
    for k in range(N): face([rows[-1][k], rows[-1][(k + 1) % N], cb])

    def truba(base, dirv, bend_ax, seg, tw, tn, ug_):
        """фаланги: base — центр основания (внутри ладони), seg — длины, ug_ — изгиб на суставах, tw/tn — полу-сечения от корня к кончику"""
        p = base.copy(); dv_ = dirv.normalized(); rings = []
        for j, L in enumerate([0.012] + seg):
            ax = bend_ax.normalized(); u2 = ax.cross(dv_).normalized()
            s_w, s_n = tw[min(j, len(tw) - 1)], tn[min(j, len(tn) - 1)]
            rings.append([nv(p + ax * sx * s_w + u2 * sy * s_n) for sx, sy in ((1, 1), (-1, 1), (-1, -1), (1, -1))])
            p = p + dv_ * L
            if j >= 1:
                ca_, sa_ = math.cos(ug_), math.sin(ug_); dv_ = (dv_ * ca_ + ax.cross(dv_) * sa_).normalized()
        for ra, rb in zip(rings, rings[1:]):
            for k in range(4): face([ra[k], ra[(k + 1) % 4], rb[(k + 1) % 4], rb[k]])
        # кончик скосом (критик r5: плоские торцы читались чёрными обрубками): кольцо меньше и дальше по оси, сдвинуто к ладони, потом торец
        k_r = rings[-1]; c_r = sum((Mw @ v.co for v in k_r), Vector()) / 4; ax2 = bend_ax.normalized().cross(dv_).normalized()
        tip = [bm.verts.new(iw @ (c_r + (Mw @ v.co - c_r) * 0.72 + dv_ * 0.005 + ax2 * 0.0015)) for v in k_r]
        for t_ in tip:
            for k in keys: t_[sh[k]] = t_.co + dk[k]
            for g, x in wt.items(): t_[dv][g] = x
        for k in range(4): face([k_r[k], k_r[(k + 1) % 4], tip[(k + 1) % 4], tip[k]])
        face(tip)
        face(rings[0][::-1])

    bend = w   # ось сгиба — поперёк ладони: пальцы сгибаются к ладони (−n)
    sgn = -1.0 if bend.cross(d).dot(nn) > 0 else 1.0
    # указательный (спереди) → мизинец: смещение по w, длины фаланг, с листа ~8.5 / 9.5 / 8.8 / 7 см
    PAL = [(0.036, (0.040, 0.026, 0.022), 0.0118), (0.012, (0.044, 0.029, 0.023), 0.0122), (-0.012, (0.041, 0.027, 0.022), 0.0118), (-0.035, (0.032, 0.022, 0.019), 0.0102)]
    for ofs_w, seg, th in PAL:
        base = c + d * 0.086 + w * ofs_w + nn * 0.002
        dr = (d + w * (ofs_w * 0.25) - nn * 0.12).normalized()
        truba(base, dr, bend * sgn, list(seg), (th, th, th * 0.96, th * 0.92), (th * 0.95, th * 0.95, th * 0.92, th * 0.85), math.radians(20))
    # большой: от бугра вперёд-вниз-внутрь, под углом к ладони
    bb = c + d * 0.045 + w * 0.040 - nn * 0.016
    dr = (d * 0.86 + w * 0.42 - nn * 0.28).normalized()
    truba(bb, dr, (dr.cross(nn)).normalized(), [0.034, 0.028, 0.024], (0.0175, 0.0175, 0.0140, 0.0120), (0.0170, 0.0170, 0.0135, 0.0110), math.radians(12))
    bm.normal_update(); bm.to_mesh(o.data); bm.free(); o.data.update()
bm = bmesh.new(); bm.from_mesh(o.data); bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 3]); nt = len(bm.faces); bmesh.ops.recalc_face_normals(bm, faces=bm.faces); bm.to_mesh(o.data); bm.free(); o.data.update()
bm = bmesh.new(); bm.from_mesh(o.data); kr = [e for e in bm.edges if e.is_boundary and abs((Mw @ e.verts[0].co).x) > 0.2 and (Mw @ e.verts[0].co).z < 1.2]; bm.free()
print('КИСТЬ: Руки — вершин %d → %d, граней %d → %d, открытых рёбер на руке %d' % (n_v0, len(o.data.vertices), n_f0, nt, len(kr)))
bpy.ops.wm.save_as_mainfile(filepath=out)
