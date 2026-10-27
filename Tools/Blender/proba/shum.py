"""МЕРА «МЯТОЙ ФОЛЬГИ» (27.10, ГеймБосс по ряду F/S7b: «у витрины крупные чистые плоскости по 5–10 на массу, у нас мелкая мятая сетка
по всему телу — сильнее полки и V вместе»). Диагноз: на каком шаге тракта (цель → подгонка → огранка → итог) поверхность мнётся.

Мера не зависит от плотности сетки (цель — 60 тыс. треугольников, ключ — 12 тыс.):
  - РАЗБРОС НОРМАЛЕЙ: для грани — угол между её нормалью и средней (по площади) нормалью граней в радиусе `--r` (3 см); медиана и
    90-й процентиль по площади. Чистая крупная плоскость — ~0°, мятая фольга — 10–20°, перелом между массами виден только у кромок;
  - РЁБРА ПО ДВУГРАННОМУ УГЛУ (доля длины): ровно < 3°, фольга 3–20°, перелом > 20° — для сеток одной плотности (ключ к ключу).
Области: торс (|x| < 0.22, между тазом и плечами), руки, ноги — по положению в покое; `--z` (низ торса:верх торса) — в метрах.

  blender -b --factory-startup -P shum.py -- файл.blend|файл.obj [--kluch Волк] [--r 0.03] [--z 0.95:1.45] [--metka имя]
"""
import sys
import bpy, bmesh
import numpy as np
from mathutils import Vector
from mathutils import kdtree

a = sys.argv[sys.argv.index('--') + 1:]
src = a[0]
opt = dict(kluch='', r='0.03', z='0.95:1.45', metka='')
i = 1
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
R = float(opt['r']); Z0, Z1 = (float(x) for x in opt['z'].split(':'))

P, F = [], []
if src.lower().endswith('.obj'):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.wm.obj_import(filepath=src, forward_axis='NEGATIVE_Z', up_axis='Y')
    o = bpy.context.selected_objects[0]
    P = [list(o.matrix_world @ v.co) for v in o.data.vertices]; F = [list(p.vertices) for p in o.data.polygons]
else:
    bpy.ops.wm.open_mainfile(filepath=src); uid = {}
    for o in bpy.data.objects:
        if o.type != 'MESH' or '@' in o.name: continue
        kb = o.data.shape_keys.key_blocks.get(opt['kluch']) if (opt['kluch'] and o.data.shape_keys) else None
        if opt['kluch'] and kb is None: continue
        vm = {}
        for vi, v in enumerate(o.data.vertices):
            b = o.matrix_world @ v.co; k = (round(b.x, 5), round(b.y, 5), round(b.z, 5))
            if k not in uid: uid[k] = len(P); P.append(list(o.matrix_world @ (kb.data[vi].co if kb else v.co)))
            vm[vi] = uid[k]
        F += [[vm[x] for x in p.vertices] for p in o.data.polygons]
P = np.array(P)
T = [(f[0], f[j], f[j + 1]) for f in F for j in range(1, len(f) - 1)]
T = np.array(T); a_, b_, c_ = P[T[:, 0]], P[T[:, 1]], P[T[:, 2]]
Nn = np.cross(b_ - a_, c_ - a_); A = np.linalg.norm(Nn, axis=1) / 2; N = Nn / np.maximum(2 * A[:, None], 1e-12); C = (a_ + b_ + c_) / 3
ok = A > 1e-10; N, A, C, T = N[ok], A[ok], C[ok], T[ok]


def oblast(c):
    x, y, z = c
    if Z0 < z < Z1 and abs(x) < 0.22: return 'торс'
    if z >= Z1 - 0.15 and abs(x) >= 0.22 or (Z0 < z < Z1 and abs(x) >= 0.22): return 'руки'
    if z <= Z0: return 'ноги'
    return 'прочее'


OB = np.array([oblast(c) for c in C])
kd = kdtree.KDTree(len(C))
for k_, c in enumerate(C): kd.insert(Vector(c), k_)
kd.balance()
ang = np.zeros(len(C))
for k_, c in enumerate(C):
    ids = [j for (_, j, _) in kd.find_range(Vector(c), R)]
    m = (N[ids] * A[ids, None]).sum(0); m /= max(np.linalg.norm(m), 1e-12)
    ang[k_] = np.degrees(np.arccos(np.clip(N[k_] @ m, -1, 1)))
# двугранные углы по рёбрам (доля длины)
ed = {}
for ti, t in enumerate(T):
    for u, v in ((t[0], t[1]), (t[1], t[2]), (t[2], t[0])): ed.setdefault((min(u, v), max(u, v)), []).append(ti)
dh = {o_: [0.0, 0.0, 0.0] for o_ in ('торс', 'руки', 'ноги')}
for (u, v), ts in ed.items():
    if len(ts) != 2: continue
    o_ = OB[ts[0]]
    if o_ not in dh: continue
    d = np.degrees(np.arccos(np.clip(N[ts[0]] @ N[ts[1]], -1, 1))); L = float(np.linalg.norm(P[u] - P[v]))
    dh[o_][0 if d < 3 else 1 if d < 20 else 2] += L


def wq(x, w, q):
    s = np.argsort(x); cw = np.cumsum(w[s]); return float(x[s][np.searchsorted(cw, q * cw[-1])])


print('ШУМ %s: граней %d, радиус %.0f мм' % (opt['metka'] or src.split('/')[-1], len(C), R * 1000))
for o_ in ('торс', 'руки', 'ноги'):
    m = OB == o_
    if not m.any(): continue
    tot = sum(dh[o_]) or 1
    print('ШУМ %s %-5s: разброс нормалей медиана %4.1f°, p90 %4.1f° | рёбра ровно %2.0f%% фольга %2.0f%% перелом %2.0f%%' % (
        opt['metka'], o_, wq(ang[m], A[m], 0.5), wq(ang[m], A[m], 0.9), 100 * dh[o_][0] / tot, 100 * dh[o_][1] / tot, 100 * dh[o_][2] / tot))
