# -*- coding: utf-8 -*-
"""МЕТРИКИ ЭКСПЕРИМЕНТА КОНСИЛИУМА (геометр `07` §8.3) по оболочкам плеч — объекты `field*` OBJ стенда.

M1 решёточность L — доля площади граней, нормаль которых в 10° от одного из 26 направлений решётки (фон 0.197);
M2 плоскостность — участки ростом области (8° к средней нормали участка); N80 — число участков на 80 % площади, / T;
M3 шахматность Z — доля внутренних рёбер, у которых знак двугранного угла противоположен знаку ВСЕХ остальных рёбер
   обеих смежных граней (упрощение «соседних рёбер той же петли»);
M4 верность полю — двусторонние RMS и Хаусдорф до эталонной оболочки (мелкая клетка), в % роста; точки — равномерная
   выборка по площади, расстояние до облака эталона (cKDTree, 300 тыс. точек — приближение «до поверхности»);
M5 IoU силуэта против маски листа — по кадрам стенда (фигура светлее фона), масштаб ±15 % от высоты, низ на земле.

Запуск:  python metriki.py эталон.obj папка_кадров маска_профиля маска_анфаса A=arm-A.obj B=arm-B.obj …
  кадры в папке — `<плечо>-profile.png`, `<плечо>-nose.png`.
"""
import os
import sys

import numpy as np
from PIL import Image
from scipy import ndimage
from scipy.spatial import cKDTree


def load(path, only_field=True):
    V, F, cur, keep = [], [], None, True
    for l in open(path, encoding='utf-8'):
        if l.startswith('o '):
            keep = (not only_field) or l[2:].strip().startswith('field')
        elif l.startswith('v '):
            V.append([float(t) for t in l.split()[1:4]])
        elif l.startswith('f ') and keep:
            idx = [int(t.split('/')[0]) - 1 for t in l.split()[1:]]
            for k in range(1, len(idx) - 1):
                F.append([idx[0], idx[k], idx[k + 1]])
    V = np.array(V); F = np.array(F)
    used = np.unique(F); remap = -np.ones(len(V), int); remap[used] = np.arange(len(used))
    V = V[used]; F = remap[F]
    # склейка совпадающих вершин (плоские грани хранят вершины раздельно)
    key = np.round(V / 1e-5).astype(np.int64)
    _, inv = np.unique(key, axis=0, return_inverse=True)
    inv = inv.ravel()
    Vm = np.zeros((inv.max() + 1, 3)); Vm[inv] = V
    F = inv[F]
    F = F[(F[:, 0] != F[:, 1]) & (F[:, 1] != F[:, 2]) & (F[:, 0] != F[:, 2])]
    return Vm, F


def normals_areas(V, F):
    c = np.cross(V[F[:, 1]] - V[F[:, 0]], V[F[:, 2]] - V[F[:, 0]])
    a = np.linalg.norm(c, axis=1)
    return c / np.maximum(a, 1e-12)[:, None], a / 2


DIRS = np.array([(x, y, z) for x in (-1, 0, 1) for y in (-1, 0, 1) for z in (-1, 0, 1) if (x, y, z) != (0, 0, 0)], float)
DIRS /= np.linalg.norm(DIRS, axis=1)[:, None]


def m1(N, A):
    cos = N @ DIRS.T
    return float(A[(cos.max(1) > np.cos(np.radians(10)))].sum() / A.sum())


def edges(F):
    E = {}
    for fi, f in enumerate(F):
        for k in range(3):
            e = tuple(sorted((f[k], f[(k + 1) % 3])))
            E.setdefault(e, []).append(fi)
    return {e: fs for e, fs in E.items() if len(fs) == 2}


def m2(N, A, E):
    adj = [[] for _ in range(len(N))]
    for fs in E.values():
        adj[fs[0]].append(fs[1]); adj[fs[1]].append(fs[0])
    seen = np.zeros(len(N), bool); areas = []
    cos8 = np.cos(np.radians(8))
    order = np.argsort(-A)
    for s in order:
        if seen[s]:
            continue
        seen[s] = True; stack = [s]; n = N[s] * A[s]; area = A[s]
        while stack:
            f = stack.pop()
            mean = n / np.linalg.norm(n)
            for g in adj[f]:
                if not seen[g] and N[g] @ mean > cos8:
                    seen[g] = True; stack.append(g); n = n + N[g] * A[g]; area += A[g]
        areas.append(area)
    areas = np.sort(areas)[::-1]
    k = int(np.searchsorted(np.cumsum(areas), 0.8 * A.sum()) + 1)
    return k, k / len(N)


def m3(V, F, N, E):
    sign = {}
    cen = V[F].mean(1)
    for e, (f, g) in E.items():
        s = np.dot(N[f], cen[g] - cen[f])          # >0 — вогнуто, <0 — выпукло
        if abs(1 - N[f] @ N[g]) < 1e-6:
            continue
        sign[e] = np.sign(s)
    fedges = {}
    for e in sign:
        for f in E[e]:
            fedges.setdefault(f, []).append(e)
    bad = 0
    for e, s in sign.items():
        others = [sign[o] for f in E[e] for o in fedges[f] if o != e]
        if others and all(o == -s for o in others):
            bad += 1
    return bad / max(1, len(sign))


def sample(V, F, n, rng):
    _, A = normals_areas(V, F)
    fi = rng.choice(len(F), n, p=A / A.sum())
    u, v = rng.random(n), rng.random(n)
    m = u + v > 1; u[m], v[m] = 1 - u[m], 1 - v[m]
    T = V[F[fi]]
    return T[:, 0] + u[:, None] * (T[:, 1] - T[:, 0]) + v[:, None] * (T[:, 2] - T[:, 0])


def m4(V, F, ref, h, rng):
    Vr, Fr = ref
    pr, pm = sample(Vr, Fr, 300000, rng), sample(V, F, 60000, rng)
    d1 = cKDTree(pr).query(pm)[0]; d2 = cKDTree(pm).query(sample(Vr, Fr, 60000, rng))[0]
    d = np.concatenate([d1, d2])
    return float(np.sqrt((d ** 2).mean()) / h * 100), float(d.max() / h * 100)


def figure(path):
    g = np.asarray(Image.open(path).convert('L')).astype(int)
    bg = np.median(np.concatenate([g[0], g[-1], g[:, 0], g[:, -1]]))
    m = ndimage.binary_fill_holes(np.abs(g - bg) > 12)
    ys, xs = np.nonzero(m)
    return m[ys.min():ys.max() + 1, xs.min():xs.max() + 1]


def iou(frame, mask_path):
    ref = np.asarray(Image.open(mask_path).convert('L')) < 128
    ys, xs = np.nonzero(ref); ref = ref[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
    H = ref.shape[0]; best = 0.0
    for s in np.linspace(0.85, 1.15, 13):
        h = max(8, int(H * s)); w = max(8, int(frame.shape[1] * h / frame.shape[0]))
        f = np.asarray(Image.fromarray(frame.astype(np.uint8) * 255).resize((w, h))) > 127
        Wc = max(w, ref.shape[1]) + 2; Hc = max(h, H)
        R = np.zeros((Hc, Wc), bool); R[Hc - H:, :ref.shape[1]] = ref        # низ на земле
        for dx in range(0, Wc - w + 1, 2):
            Q = np.zeros((Hc, Wc), bool); Q[Hc - h:, dx:dx + w] = f
            best = max(best, (R & Q).sum() / (R | Q).sum())
    return best


def main():
    ref_path, frames, mprof, mfront = sys.argv[1:5]
    arms = [x.split('=') for x in sys.argv[5:]]
    rng = np.random.default_rng(1)
    Vr, Fr = load(ref_path)
    h = Vr[:, 1].max() - Vr[:, 1].min()
    print('| плечо | T | M1 L | M2 N80 | N80/T | M3 Z | M4 RMS % | M4 Хаусдорф % | M5 IoU профиль | M5 IoU анфас |')
    print('|---|---|---|---|---|---|---|---|---|---|')
    for name, path in arms:
        V, F = load(path)
        N, A = normals_areas(V, F); E = edges(F)
        k, kt = m2(N, A, E)
        rms, hd = m4(V, F, (Vr, Fr), h, rng) if name != 'C' else (float('nan'), float('nan'))
        ip = iou(figure(os.path.join(frames, name + '-profile.png')), mprof)
        ia = iou(figure(os.path.join(frames, name + '-nose.png')), mfront)
        print('| %s | %d | %.3f | %d | %.3f | %.3f | %.2f | %.2f | %.3f | %.3f |' % (name, len(F), m1(N, A), k, kt, m3(V, F, N, E), rms, hd, ip, ia))


if __name__ == '__main__':
    main()
