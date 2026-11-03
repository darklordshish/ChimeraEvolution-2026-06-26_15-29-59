"""ПРЕСС ГРАНЯМИ (03.11, критик r2: «3 ступени, грани 6–8 см, кромка только вдоль белой линии, без рамки»; сглаживание и ярусы по старой сетке дали кашу — граней там много и
направлены случайно). Область живота в `--ob` (все вершины грани: |x| < `--x`, z в `--z`, перёд) вырезается и заполняется заново: точки — белая линия и колонка ±`--kol` на рядах
`--ryady`, центр каждого кубика; ограниченная триангуляция Делоне (`mathutils.geometry.delaunay_2d_cdt`) по проекции (x, z), край области сохраняется. Глубина — с поверхности
тела лучом спереди; центр кубика вперёд на `--vpered`, белая линия назад на `--belaya`, ряды на колонке назад на `--ryad`. Снаружи колонки кромки нет: триангуляция уходит в край области.
Веса и прочие ключи — от ближайшей вершины. Ключ и база правятся вместе (база = ключ).

  blender -b вход.blend --factory-startup -P press_grani.py -- выход.blend [--ob Сердце] [--x 0.082] [--z 1.14:1.29] [--ryady 1.285,1.24,1.195,1.15] [--kol 0.042] [--vpered 0.004] [--belaya 0.004] [--ryad 0.0015]
"""
import sys
import bpy, bmesh
from mathutils import Vector, kdtree
from mathutils.bvhtree import BVHTree
from mathutils.geometry import delaunay_2d_cdt

a = sys.argv[sys.argv.index('--') + 1:]
out = a[0]
opt = dict(ob='Сердце', x='0.082', z='1.14:1.29', ryady='1.285,1.24,1.195,1.15', kol='0.042', vpered='0.004', belaya='0.004', ryad='0.0015', imya='Поджарый')
i = 1
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
X_, KOL, VP, BL, RD = (float(opt[k]) for k in ('x', 'kol', 'vpered', 'belaya', 'ryad'))
z0, z1 = (float(q) for q in opt['z'].split(':')); RY = [float(q) for q in opt['ryady'].split(',')]
o = bpy.data.objects[opt['ob']]; Mw = o.matrix_world; iw = Mw.inverted()
# поверхность тела спереди — все объекты в ключе
V, F = [], []
for b in bpy.data.objects:
    if b.type != 'MESH' or '@' in b.name or not b.data.shape_keys or opt['imya'] not in b.data.shape_keys.key_blocks: continue
    kb = b.data.shape_keys.key_blocks[opt['imya']]; n0 = len(V); V += [b.matrix_world @ k.co for k in kb.data]
    b.data.calc_loop_triangles(); F += [[n0 + q for q in t.vertices] for t in b.data.loop_triangles]
bvh = BVHTree.FromPolygons(V, F, all_triangles=True)


def glubina(x, z):
    h = bvh.ray_cast(Vector((x, -1.0, z)), Vector((0, 1, 0)))
    return h[0].y if h[0] is not None else None


bm = bmesh.new(); bm.from_mesh(o.data)
keys = [k.name for k in o.data.shape_keys.key_blocks]; sh = {k: bm.verts.layers.shape[k] for k in keys}; dv = bm.verts.layers.deform.active; KS = sh[opt['imya']]
W_ = lambda v: Mw @ v[KS]
vnut = lambda v: abs(W_(v).x) < X_ and z0 < W_(v).z < z1 and W_(v).y < 0
ubr = [f for f in bm.faces if all(vnut(v) for v in f.verts)]
# связная область через ось
seen, st = set(), [max(ubr, key=lambda f: -abs(f.calc_center_median().x))]
while st:
    f = st.pop()
    if f in seen: continue
    seen.add(f)
    for e in f.edges:
        for g in e.link_faces:
            if g in ubr and g not in seen: st.append(g)
ubr = list(seen)
kraj = {}
for f in ubr:
    for e in f.edges: kraj[e] = kraj.get(e, 0) + 1
gran = [e for e, c in kraj.items() if c == 1]
adj = {}
for e in gran:
    for v in e.verts: adj.setdefault(v, []).append(e)
if any(len(q) != 2 for q in adj.values()): sys.exit('ПРЕСС: край области ветвится: %s' % sorted({len(q) for q in adj.values()}))
start = gran[0].verts[0]; loop = [start]; prev = None; cur = start
while True:
    nx = next((e.other_vert(cur) for e in adj[cur] if e.other_vert(cur) is not prev), None)
    if nx is None or nx is start: break
    loop.append(nx); prev, cur = cur, nx
if len(loop) != len(gran): sys.exit('ПРЕСС: край области не один: %d из %d' % (len(loop), len(gran)))
n_ub = len(ubr); vnutr = {v for f in ubr for v in f.verts} - set(loop)
kd = kdtree.KDTree(len(loop))
for j, v in enumerate(loop): kd.insert(W_(v), j)
kd.balance()
# точки формы
P = []
for z in RY:
    for x, dy in ((0.0, BL), (-KOL, RD), (KOL, RD)): P.append((x, z, dy))
for za, zb in zip(RY, RY[1:]):
    for x in (-KOL / 2, KOL / 2): P.append((x, (za + zb) / 2, -VP))
L2 = [(W_(v).x, W_(v).z) for v in loop]
from mathutils import Vector as V2
coords = [V2(p) for p in L2] + [V2((x, z)) for x, z, _ in P]
edges = [(j, (j + 1) % len(loop)) for j in range(len(loop))]
res = delaunay_2d_cdt(coords, edges, [], 1, 1e-6)
vs, _, fs, ov, _, _ = res
mapv = {}
nov = []
for j, ol in enumerate(ov):
    if not ol: sys.exit('ПРЕСС: триангуляция добавила точку')
    q = ol[0]
    if q < len(loop): mapv[j] = loop[q]
    else: nov.append((j, P[q - len(loop)]))
bmesh.ops.delete(bm, geom=ubr, context='FACES_ONLY')
bmesh.ops.delete(bm, geom=[v for v in vnutr if v.is_valid], context='VERTS')
for j, (x, z, dy) in nov:
    y = glubina(x, z)
    if y is None: sys.exit('ПРЕСС: луч мимо тела в (%.3f, %.3f)' % (x, z))
    p = Vector((x, y + dy, z)); v = bm.verts.new(iw @ p); _, near, _ = kd.find(p); s = loop[near]
    for k in keys: v[sh[k]] = (iw @ p) + (s[sh[k]] - s[KS])
    for g, w in s[dv].items(): v[dv][g] = w
    mapv[j] = v
n_new = 0
for f in fs:
    try:
        nf = bm.faces.new([mapv[q] for q in f]); n_new += 1; nf.normal_update()
        if (Mw.to_3x3() @ nf.normal).y > 0: nf.normal_flip()      # перёд смотрит к −y; общий пересчёт нормалей выворачивал всю заднюю часть объекта
    except ValueError: pass
bm.to_mesh(o.data); bm.free(); o.data.update()
print('ПРЕСС: граней %d → %d, край области %d вершин, новых точек %d; граней объекта %d' % (n_ub, n_new, len(loop), len(nov), len(o.data.polygons)))
bpy.ops.wm.save_as_mainfile(filepath=out)
