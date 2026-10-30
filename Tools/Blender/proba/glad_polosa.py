"""ГЛАДЬ В ПОЛОСЕ ВЫСОТ (30.10, критик поджарого c3: «поперёк спины на 0.44 роста тёмная складка, как шов пояса»). Цель Hunyuan —
по листу в шортах, и подогнанное тело повторяло пояс шорт валиком. Форма-ключ `--imya` в полосе высот покоя `--z z0:z1`
сглаживается Таубином (`--shagi` проходов, λ 0.5 / μ −0.53) с весом, плавно спадающим к краям полосы на `--kraj` м; общие вершины
объектов — одна точка, открытые края (кольца швов) неподвижны. Вне полосы форма не меняется.
`--tochki "x,y,z,r;…"` (31.10, критик c7–c8: клин у ости, щель на Г1, «клюв» под грудной — устойчивые места в 5–10 граней) — вместо
полосы круги в проекции (x, z): вес спадает от центра к радиусу r (м); y — только сторона (+1 спина, −1 перед).

  blender -b вход.blend --factory-startup -P glad_polosa.py -- выход.blend [--imya Поджарый] [--z 0.98:1.16] [--kraj 0.04] [--shagi 10] [--ob Сердце,хребет — двигаются только вершины этих объектов; общие с другими стоят]
"""
import sys
import bpy, bmesh
import numpy as np
from mathutils import kdtree

a = sys.argv[sys.argv.index('--') + 1:]
out = a[0]
opt = dict(imya='Поджарый', z='0.98:1.16', kraj='0.04', shagi='10')
i = 1
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
z0, z1 = (float(x) for x in opt['z'].split(':')); KR = float(opt['kraj'])
OBJ = [o for o in bpy.data.objects if o.type == 'MESH' and '@' not in o.name and o.data.shape_keys and opt['imya'] in o.data.shape_keys.key_blocks]
OB = set(opt['ob'].split(',')) if opt.get('ob') else {o.name for o in OBJ}
flat = []
for o in OBJ:
    for vi, v in enumerate(o.data.vertices): flat.append((o, vi, o.matrix_world @ v.co))
kd = kdtree.KDTree(len(flat))
for j, (_, _, p) in enumerate(flat): kd.insert(p, j)
kd.balance()
rep = [None] * len(flat); X = []; FIX = []
for j, (o, vi, p) in enumerate(flat):
    if rep[j] is not None: continue
    u = len(X); X.append(list(o.matrix_world @ o.data.shape_keys.key_blocks[opt['imya']].data[vi].co)); FIX.append(False)
    for (_, jj, _) in kd.find_range(p, 0.0005):
        if rep[jj] is None: rep[jj] = u
        if flat[jj][0].name not in OB: FIX[u] = 'чужая'
vm = {(o.name, vi): rep[j] for j, (o, vi, _) in enumerate(flat)}
X = np.array(X); FIX = list(FIX); nb = [set() for _ in range(len(X))]
for o in OBJ:
    bm = bmesh.new(); bm.from_mesh(o.data)
    for e in bm.edges:
        x, y = vm[(o.name, e.verts[0].index)], vm[(o.name, e.verts[1].index)]; nb[x].add(y); nb[y].add(x)
        if e.is_boundary:
            for q in (x, y):
                if FIX[q] is False: FIX[q] = True
    bm.free()
# открытый край тела — край, не общий с другим объектом: у общих вершин швов (шея–корпус) соседи с обеих сторон, их не держим
NB = [list(s) for s in nb]
Z = X[:, 2]
Wt = np.clip(np.minimum(Z - z0, z1 - Z) / KR, 0, 1); Wt = Wt * Wt * (3 - 2 * Wt)
if opt.get('tochki'):
    Wt = np.zeros(len(X))
    for t_ in opt['tochki'].split(';'):
        cx, cy, cz, r_ = (float(q) for q in t_.split(','))
        d_ = np.hypot(X[:, 0] - cx, X[:, 2] - cz); w_ = np.clip(1 - d_ / r_, 0, 1) * (np.sign(X[:, 1]) == np.sign(cy)); Wt = np.maximum(Wt, w_ * w_ * (3 - 2 * w_))
kraj_tela = np.array([(FIX[k] is True and len(NB[k]) < 4) or FIX[k] == 'чужая' for k in range(len(X))])
Wt[kraj_tela] = 0
X0 = X.copy()
for _ in range(int(opt['shagi'])):
    for lam in (0.5, -0.53):
        L = np.array([X[n].mean(0) - X[k] if n else np.zeros(3) for k, n in enumerate(NB)]); X = X + lam * Wt[:, None] * L
for o in OBJ:
    kb = o.data.shape_keys.key_blocks[opt['imya']]; iw = o.matrix_world.inverted()
    for vi in range(len(o.data.vertices)): kb.data[vi].co = iw @ __import__('mathutils').Vector(X[vm[(o.name, vi)]])
d = np.linalg.norm(X - X0, axis=1)
print('ГЛАДЬ: ' + ('шары %s' % opt['tochki'] if opt.get('tochki') else '') + ' полоса %.2f–%.2f м, вершин с весом > 0.5: %d, сдвиг средн %.4f, макс %.4f м' % (z0, z1, int((Wt > 0.5).sum()), d[Wt > 0].mean(), d.max()))
bpy.ops.wm.save_as_mainfile(filepath=out)
