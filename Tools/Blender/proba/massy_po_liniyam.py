"""МАССЫ ПО ЛИНИЯМ (31.10, ретопология торса поджарого, решение геймдизайнера «давай а»; критик c5: «двигать массы, а не углы —
лопатку выдвинуть объёмом на 1.5–2.5 см, низ клетки вперёд, живот под Λ утопить на 1–1.5 см, поясница одной плоскостью»).
Поворот плоскостей вокруг линии давал прорези: угол рос на полосе в 1–2 грани, масса не двигалась. Здесь двигается масса —
область тела по одну сторону линии сдвигается по нормали, и граница сдвига проходит РОВНО по рёбрам линии (`перелом_линия` от
`linii_k2.py`): ребро между массами получается шириной в массу, а не в грань.

Масса (`--massy файл.json`, список):
  {"imya": "лопатка", "vid": "back"|"front", "x": [|x|0, |x|1], "z": [z0, z1], "sdvig": +0.02 (м, по нормали; − — внутрь),
   "granicy": [{"liniya": 6, "storona": "nizhe"|"vyshe"|"snaruzhi"|"vnutri"}, …],   # резкая граница по линии
               (или {"tochki": [[|x|, z], …], "storona": …} — своя ломаная там, где линии листа нет: паховая V под шортами)
   "spad": {"os": "x"|"z", "ot": a, "do": b}}                                            # плавный спад к нулю по оси (вне линий);
                                                                                          # список спадов — веса перемножаются
   или {"imya": "поясница", …, "ploskost": 1}  — область выпрямляется в плоскость (взвешенные МНК), без сдвига.
Сторона по линии — по её ломаной в проекции вида: «ниже/выше» — z против z линии на том же |x| (горизонтальные: Г6, Г2), «снаружи/
внутри» — |x| против |x| линии на той же высоте (вертикальные: Г7). Линии симметричны — берётся |x|. Неподвижны открытый край тела
и вершины линий самих (они — ребро); общие вершины объектов едут во всех объектах разом.

  blender -b вход.blend --factory-startup -P massy_po_liniyam.py -- выход.blend --massy массы.json [--imya Поджарый]
"""
import sys, json
import bpy, bmesh
import numpy as np
from mathutils import kdtree, Vector

a = sys.argv[sys.argv.index('--') + 1:]
out = a[0]
opt = dict(imya='Поджарый')
i = 1
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
M = json.load(open(opt['massy'], encoding='utf-8'))
OBJ = [o for o in bpy.data.objects if o.type == 'MESH' and '@' not in o.name and o.data.shape_keys and opt['imya'] in o.data.shape_keys.key_blocks]
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
X = np.array(X); N = len(X); F = []; kraj = {}; LP = {}
for o in OBJ:
    bm = bmesh.new(); bm.from_mesh(o.data); ai = bm.edges.layers.int.get('перелом_линия')
    for e in bm.edges:
        x, y = vm[(o.name, e.verts[0].index)], vm[(o.name, e.verts[1].index)]
        if e.is_boundary: kraj[tuple(sorted((x, y)))] = kraj.get(tuple(sorted((x, y))), 0) + 1
        if ai is not None and e[ai]:
            for q in (x, y): LP.setdefault(e[ai], set()).add(q)
    for f in bm.faces: F.append([vm[(o.name, v.index)] for v in f.verts])
    bm.free()
FIX = np.zeros(N, bool)
for (x, y), c in kraj.items():
    if c == 1: FIX[x] = FIX[y] = True
LINV = set().union(*LP.values()) if LP else set()


def normali(X):
    Nv = np.zeros_like(X)
    for f in F:
        for k in range(1, len(f) - 1):
            n = np.cross(X[f[k]] - X[f[0]], X[f[k + 1]] - X[f[0]]); Nv[f[0]] += n; Nv[f[k]] += n; Nv[f[k + 1]] += n
    return Nv / np.maximum(np.linalg.norm(Nv, axis=1, keepdims=True), 1e-12)


Nv = normali(X); AX = np.abs(X[:, 0]); D = np.zeros_like(X)


def lomanaya(lid, vid):
    """точки линии на нужной стороне тела (спина y > 0, перед y < 0) в осях (|x|, z)"""
    ids = [k for k in LP.get(lid, ()) if (X[k, 1] > -0.02 if vid == 'back' else X[k, 1] < 0.02 if vid == 'front' else True)]
    return np.array([[AX[k], X[k, 2]] for k in ids]) if ids else None


for m in M:
    vid = m.get('vid', 'back'); sel = (X[:, 1] > 0.0) if vid == 'back' else (X[:, 1] < 0.0) if vid == 'front' else np.ones(N, bool)   # 'vse' — обе стороны (предплечье)
    sel &= (AX >= m['x'][0]) & (AX <= m['x'][1]) & (X[:, 2] >= m['z'][0]) & (X[:, 2] <= m['z'][1]) & ~FIX
    for g in m.get('granicy', []):
        L = np.array(g['tochki'], float) if 'tochki' in g else lomanaya(g['liniya'], vid)
        if L is None or len(L) < 2: print('МАССЫ: %s — линии %s нет' % (m['imya'], g.get('liniya'))); continue
        if g['storona'] in ('nizhe', 'vyshe'):
            o_ = np.argsort(L[:, 0]); zl = np.interp(AX, L[o_, 0], L[o_, 1], left=np.nan, right=np.nan)
            ok = (X[:, 2] < zl) if g['storona'] == 'nizhe' else (X[:, 2] > zl)
            # за пределами линии по |x| граница не действует (масса уходит под соседнюю линию плавным спадом)
            sel &= np.where(np.isnan(zl), True, ok)
        else:
            o_ = np.argsort(L[:, 1]); xl = np.interp(X[:, 2], L[o_, 1], L[o_, 0], left=np.nan, right=np.nan)
            ok = (AX > xl) if g['storona'] == 'snaruzhi' else (AX < xl)
            sel &= np.where(np.isnan(xl), True, ok)
    sel_i = np.nonzero(sel)[0]
    if not len(sel_i): print('МАССЫ: %s — пусто' % m['imya']); continue
    w = np.ones(len(sel_i))
    for sp in ([m['spad']] if isinstance(m.get('spad'), dict) else m.get('spad', [])):
        c_ = AX[sel_i] if sp['os'] == 'x' else X[sel_i, 2]
        t = np.clip((c_ - sp['do']) / (sp['ot'] - sp['do']), 0, 1); w = w * t * t * (3 - 2 * t)
    if m.get('ploskost'):
        P = X[sel_i]; c = (P * w[:, None]).sum(0) / w.sum(); _, _, vt = np.linalg.svd((P - c) * np.sqrt(w)[:, None], full_matrices=False); n = vt[2]
        D[sel_i] += -((P - c) @ n)[:, None] * n * w[:, None]
    else:
        D[sel_i] += Nv[sel_i] * m['sdvig'] * w[:, None]
    print('МАССЫ: %s — вершин %d, сдвиг средн %.4f м' % (m['imya'], len(sel_i), float(np.linalg.norm(D[sel_i], axis=1).mean())))
X = X + D
for o in OBJ:
    kb = o.data.shape_keys.key_blocks[opt['imya']]; iw = o.matrix_world.inverted()
    same = all((kb.data[k].co - o.data.vertices[k].co).length < 1e-6 for k in range(len(o.data.vertices)))
    for vi in range(len(o.data.vertices)):
        p = iw @ Vector(X[vm[(o.name, vi)]]); kb.data[vi].co = p
        if same: o.data.vertices[vi].co = p; o.data.shape_keys.key_blocks[0].data[vi].co = p
bpy.ops.wm.save_as_mainfile(filepath=out)
