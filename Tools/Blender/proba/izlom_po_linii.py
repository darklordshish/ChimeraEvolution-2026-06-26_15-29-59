"""ИЗЛОМ ПО ЛИНИИ (30.10, критик поджарого c3: «грани крупные, но направлены случайно — рёбра не легли по границам масс»; ГеймБосс:
шаг «излом по линии», не выпрямление областей). Огранка проекцией на средние плоскости ставила излом, но срезала массу (IoU 0.903 →
0.85–0.89). Здесь двигаются только вершины линий и их ближайшие соседи:

  1. линия — цепочки рёбер с атрибутом `перелом` (`linii_k2.py`; `--linii 2` — только главные Г, 1 — и второстепенные В);
  2. у вершины линии соседи в радиусе `--radius` м (по поверхности, не через линию) делятся на две стороны знаком
     (p − v)·(t × n), t — направление линии в вершине, n — нормаль; по каждой стороне — плоскость (взвешенные наименьшие квадраты);
  3. вершина линии едет к ближайшей точке прямой пересечения двух плоскостей (если они не почти параллельны: угол < `--min_ugol`°
     — излома нет, вершина стоит), соседи — к своей плоскости с весом (1 − d/R)², сила `--sila`, сдвиг не больше `--maks` м;
  4. вклады нескольких вершин линии в одного соседа усредняются; края объектов (кольца швов) неподвижны.
Тело сшито по общим вершинам объектов. Правится форма-ключ `--imya` (и база, если база = ключ после `prorezhivanie.py`).

  blender -b вход.blend --factory-startup -P izlom_po_linii.py -- выход.blend [--imya Поджарый] [--linii 1] [--radius 0.05]
      [--sila 1] [--maks 0.015] [--min_ugol 4] [--prohody 2] [--bez шея — линии этих объектов не ломаются]
"""
import sys, math
import bpy, bmesh
import numpy as np
from mathutils import kdtree, Vector

a = sys.argv[sys.argv.index('--') + 1:]
out = a[0]
opt = dict(imya='Поджарый', linii='1', radius='0.05', sila='1', maks='0.015', min_ugol='4', prohody='2', bez='шея')
i = 1
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
R, S, MX, MINU, LIN = float(opt['radius']), float(opt['sila']), float(opt['maks']), float(opt['min_ugol']), int(opt['linii'])
OBJ = [o for o in bpy.data.objects if o.type == 'MESH' and '@' not in o.name and o.data.shape_keys and opt['imya'] in o.data.shape_keys.key_blocks]
# ---------- одна сетка ----------
flat = []
for o in OBJ:
    for vi, v in enumerate(o.data.vertices): flat.append((o, vi, o.matrix_world @ v.co))
kd = kdtree.KDTree(len(flat))
for j, (_, _, p) in enumerate(flat): kd.insert(p, j)
kd.balance()
rep = [None] * len(flat); X = []
for j, (o, vi, p) in enumerate(flat):
    if rep[j] is not None: continue
    u = len(X); X.append(list(o.matrix_world @ o.data.shape_keys.key_blocks[opt['imya']].data[vi].co))
    for (_, jj, _) in kd.find_range(p, 0.0005):
        if rep[jj] is None: rep[jj] = u
vm = {(o.name, vi): rep[j] for j, (o, vi, _) in enumerate(flat)}
X = np.array(X); N = len(X)
nb = [set() for _ in range(N)]; F = []; kraj_cnt = {}; LE = set(); BEZV = set()
for o in OBJ:
    bm = bmesh.new(); bm.from_mesh(o.data); al = bm.edges.layers.int.get('перелом')
    for e in bm.edges:
        x, y = vm[(o.name, e.verts[0].index)], vm[(o.name, e.verts[1].index)]; nb[x].add(y); nb[y].add(x)
        if e.is_boundary:
            key = tuple(sorted((x, y))); kraj_cnt[key] = kraj_cnt.get(key, 0) + 1
        if al is not None and e[al] >= (2 if LIN == 2 else 1): LE.add(tuple(sorted((x, y))))
    for f in bm.faces: F.append([vm[(o.name, v.index)] for v in f.verts])
    if o.name in set(opt['bez'].split(',')):
        for v in bm.verts: BEZV.add(vm[(o.name, v.index)])
    bm.free()
# открытый край тела — краевое ребро, которое не закрыто другим объектом
FIX = np.zeros(N, bool)
for (x, y), c in kraj_cnt.items():
    if c == 1: FIX[x] = FIX[y] = True
# у неподвижного края и его соседей излом не ставится: сосед ехал, край стоял — карманы у кольца на талии и на затылке
# и у швов МЕЖДУ объектами (кольцо «Сердце»–«хребет» на талии: карманы у его вершин)
SHOV = {k for (x, y), c in kraj_cnt.items() if c > 1 for k in (x, y)}
BLIZ = set(np.nonzero(FIX)[0]) | SHOV | {w for v in set(np.nonzero(FIX)[0]) | SHOV for w in nb[v]} | BEZV
LV = sorted({v for e in LE for v in e}); lnb = {v: [w for w in nb[v] if tuple(sorted((v, w))) in LE] for v in LV}
print('ИЗЛОМ: рёбер линий %d, вершин линий %d, неподвижных %d' % (len(LE), len(LV), int(FIX.sum())))


def normali(X):
    Nv = np.zeros_like(X)
    for f in F:
        for k in range(1, len(f) - 1):
            n = np.cross(X[f[k]] - X[f[0]], X[f[k + 1]] - X[f[0]]); Nv[f[0]] += n; Nv[f[k]] += n; Nv[f[k + 1]] += n
    return Nv / np.maximum(np.linalg.norm(Nv, axis=1, keepdims=True), 1e-12)


def ploskost(P, w):
    c = (P * w[:, None]).sum(0) / w.sum(); Q = (P - c) * np.sqrt(w)[:, None]
    _, _, vt = np.linalg.svd(Q, full_matrices=False); return c, vt[2]


import heapq
X0 = X.copy()
for pr in range(int(opt['prohody'])):
    Nv = normali(X); acc = np.zeros_like(X); wsum = np.zeros(N); n_iz = 0
    for v in LV:
        if v in BLIZ or len(lnb[v]) < 2: continue      # конец цепочки: направление линии не определено — карманы у концов
        # направление линии: через соседей по линии
        ends = [X[w] for w in lnb[v]]
        t = (ends[-1] - ends[0]) if len(ends) > 1 else (ends[0] - X[v])
        t /= max(np.linalg.norm(t), 1e-12); s_ = np.cross(t, Nv[v]); s_ /= max(np.linalg.norm(s_), 1e-12)
        # соседи по поверхности в радиусе R, не через вершины линии (кроме своей)
        dist = {v: 0.0}; pq = [(0.0, v)]; side = []
        while pq:
            d, u = heapq.heappop(pq)
            if d > dist.get(u, 9): continue
            for w in nb[u]:
                nd = d + float(np.linalg.norm(X[w] - X[u]))
                if nd > R or nd >= dist.get(w, 9): continue
                dist[w] = nd
                if w in lnb and w != v: continue
                heapq.heappush(pq, (nd, w))
        ids = [u for u in dist if u != v and u not in lnb]
        if len(ids) < 6: continue
        P = X[ids]; d_ = np.array([dist[u] for u in ids]); sg = (P - X[v]) @ s_
        A_, B_ = sg > 0, sg < 0
        if A_.sum() < 3 or B_.sum() < 3: continue
        w_ = (1 - d_ / R) ** 2 + 1e-3
        cA, nA = ploskost(P[A_], w_[A_]); cB, nB = ploskost(P[B_], w_[B_])
        if nA @ Nv[v] < 0: nA = -nA
        if nB @ Nv[v] < 0: nB = -nB
        ug = math.degrees(math.acos(np.clip(nA @ nB, -1, 1)))
        if ug < MINU: continue
        # ближайшая к v точка прямой пересечения плоскостей
        dl = np.cross(nA, nB); M = np.array([nA, nB, dl]); rhs = np.array([nA @ cA, nB @ cB, dl @ X[v]])
        q = np.linalg.solve(M, rhs)
        acc[v] += 4 * (q - X[v]); wsum[v] += 4; n_iz += 1
        for k, u in enumerate(ids):
            if u in BLIZ: continue
            n_ = nA if A_[k] else nB; c_ = cA if A_[k] else cB
            tgt = X[u] - n_ * ((X[u] - c_) @ n_)
            acc[u] += w_[k] * (tgt - X[u]); wsum[u] += w_[k]
    m = wsum > 0
    D = np.zeros_like(X); D[m] = acc[m] / wsum[m][:, None] * S
    ln = np.linalg.norm(D, axis=1); D *= np.minimum(1, MX / np.maximum(ln, 1e-12))[:, None]
    X = X + D
    print('ИЗЛОМ: проход %d — изломов %d, сдвинуто вершин %d, сдвиг средн %.4f, макс %.4f м' % (pr + 1, n_iz, int(m.sum()), ln[m].mean() if m.any() else 0, min(ln.max(), MX)))
for o in OBJ:
    kb = o.data.shape_keys.key_blocks[opt['imya']]; iw = o.matrix_world.inverted(); same = all((kb.data[k].co - o.data.vertices[k].co).length < 1e-6 for k in range(len(o.data.vertices)))
    for vi in range(len(o.data.vertices)):
        p = iw @ Vector(X[vm[(o.name, vi)]]); kb.data[vi].co = p
        if same: o.data.vertices[vi].co = p; o.data.shape_keys.key_blocks[0].data[vi].co = p
bpy.ops.wm.save_as_mainfile(filepath=out)
