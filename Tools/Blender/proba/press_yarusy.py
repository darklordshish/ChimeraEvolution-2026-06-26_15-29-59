"""ПРЕСС ЯРУСАМИ (03.11, критик r2: «брикет ~16×16 см с замкнутой рамкой и полкой внизу — почти запрет 03-no-armor»; просьба: «по бокам открыть в косую без кромки, низ
раскрыть к пупку, 3 ступени с кромкой только вдоль белой линии, верхние вперёд-вверх, нижние вперёд-вниз»). Сначала сглаживание области (Таубин) снимает рамку, затем
глубина задаётся формой: ярусы — выпуклый профиль по z в каждом ярусе (амплитуда `--a`), белая линия — канавка глубиной `--g` у оси; всё гаснет к боку (|x| `--x0` → `--x1`) и к
низу и верху полосы. Края объектов стоят, общие вершины едут вместе. Правится ключ и база (если база = ключ).

  blender -b вход.blend --factory-startup -P press_yarusy.py -- выход.blend [--z 1.13:1.33] [--yarusy 1.31,1.255,1.205,1.15] [--a 0.005] [--g 0.004] [--x0 0.055] [--x1 0.10] [--glad 8]
"""
import sys
import bpy, bmesh
import numpy as np
from mathutils import Vector, kdtree

a = sys.argv[sys.argv.index('--') + 1:]
out = a[0]
opt = dict(z='1.13:1.33', yarusy='1.31,1.255,1.205,1.15', a='0.005', g='0.004', x0='0.055', x1='0.10', glad='8', imya='Поджарый')
i = 1
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
z0, z1 = (float(x) for x in opt['z'].split(':')); Y = [float(x) for x in opt['yarusy'].split(',')]
AM, GR, X0, X1 = (float(opt[k]) for k in ('a', 'g', 'x0', 'x1'))
OBJ = [o for o in bpy.data.objects if o.type == 'MESH' and '@' not in o.name and o.data.shape_keys and opt['imya'] in o.data.shape_keys.key_blocks]
flat = [(o, vi, o.matrix_world @ o.data.shape_keys.key_blocks[opt['imya']].data[vi].co) for o in OBJ for vi in range(len(o.data.vertices))]
kd = kdtree.KDTree(len(flat))
for j, (_, _, p) in enumerate(flat): kd.insert(p, j)
kd.balance()
rep = [None] * len(flat); X = []
for j, (o, vi, p) in enumerate(flat):
    if rep[j] is not None: continue
    u = len(X); X.append(list(p))
    for (_, jj, _) in kd.find_range(p, 0.0003):
        if rep[jj] is None: rep[jj] = u
vm = {(o.name, vi): rep[j] for j, (o, vi, _) in enumerate(flat)}
X = np.array(X); N = len(X); nb = [set() for _ in range(N)]; kr = {}
for o in OBJ:
    bm = bmesh.new(); bm.from_mesh(o.data)
    for e in bm.edges:
        x, y = vm[(o.name, e.verts[0].index)], vm[(o.name, e.verts[1].index)]; nb[x].add(y); nb[y].add(x)
        if e.is_boundary: kr[tuple(sorted((x, y)))] = kr.get(tuple(sorted((x, y))), 0) + 1
    bm.free()
FIX = np.zeros(N, bool)
for (x, y), c in kr.items():
    if c == 1: FIX[x] = FIX[y] = True


def ss(t):
    q = np.clip(t, 0, 1); return q * q * (3 - 2 * q)


ax = np.abs(X[:, 0]); front = X[:, 1] < 0
Wb = ss((X1 - ax) / (X1 - X0)) * ss((X[:, 2] - z0) / 0.03) * ss((z1 - X[:, 2]) / 0.03) * front * ~FIX
NB = [list(s) for s in nb]; X0_ = X.copy()
for _ in range(int(opt['glad'])):
    for lam in (0.5, -0.53):
        L = np.array([X[n].mean(0) - X[k] if n else np.zeros(3) for k, n in enumerate(NB)]); X = X + lam * Wb[:, None] * L
Z = X[:, 2]; S = np.zeros(N)
for ya, yb in zip(Y, Y[1:]):
    m = (Z <= ya) & (Z > yb); t = (ya - Z[m]) / (ya - yb)      # 0 — верх яруса, 1 — низ
    S[m] = AM * (1 - (2 * t - 1) ** 2) - AM * 0.5            # середина яруса впереди, у кромки уходит назад
Sg = GR * ss(1 - ax / 0.018)
X[:, 1] += -(S * Wb) + Sg * Wb                              # перёд — к −y; канавка белой линии — назад
d = np.linalg.norm(X - X0_, axis=1)
print('ПРЕСС: вершин %d, сдвиг средн %.1f мм, макс %.1f мм' % (int((Wb > 0).sum()), d[Wb > 0].mean() * 1000, d.max() * 1000))
for o in OBJ:
    kb = o.data.shape_keys.key_blocks[opt['imya']]; iw = o.matrix_world.inverted()
    same = all((kb.data[k].co - o.data.vertices[k].co).length < 1e-6 for k in range(len(o.data.vertices)))
    for vi in range(len(o.data.vertices)):
        p = iw @ Vector(X[vm[(o.name, vi)]]); kb.data[vi].co = p
        if same: o.data.vertices[vi].co = p; o.data.shape_keys.key_blocks[0].data[vi].co = p
bpy.ops.wm.save_as_mainfile(filepath=out)
