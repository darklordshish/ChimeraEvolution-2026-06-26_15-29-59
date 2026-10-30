"""ЧИСТКА ЩЕРБИН (31.10, критик поджарого c7–c8: «клин у ости, щель на Г1, клюв под грудной — устойчивые места в 5–10 граней,
прицельно»; ручная наводка по координатам промахивается — проценты высоты на глаз, а прореживание каждый раз кладёт грани иначе).
Детектор вместо наводки: грань, чья нормаль отходит от нормали ОКРУЖАЮЩЕЙ поверхности (средняя по граням в радиусе `--radius` м,
взвешенная площадью) больше чем на `--ugol`°, — щербина (смотрит вниз, внутрь, торчит клювом). Вершины щербин (и их кольцо с
весом 0.5) разглаживаются Таубином `--shagi` проходов; остальное стоит. Широкие плоскости и рёбра между массами — грани с
нормалью как у соседей — не трогаются. Открытый край тела неподвижен, общие вершины объектов — одна точка.

`--povorot 1` — после глади грани, вывернутые больше чем на 90° (щель на Г1: тонкий треугольник, перекинутый через соседа, гладь
его не разворачивает), получают поворот самого длинного ребра (`bmesh.ops.rotate_edges`, не на краю объекта): число вершин то же.

  blender -b вход.blend --factory-startup -P chistka_shcherbin.py -- выход.blend [--imya Поджарый] [--ob Сердце,хребет,Руки]
      [--radius 0.05] [--ugol 45] [--shagi 4] [--z 0.9:1.6] [--kadr отчёт.json]
"""
import sys, json, math
import bpy, bmesh
import numpy as np
from mathutils import kdtree, Vector

a = sys.argv[sys.argv.index('--') + 1:]
out = a[0]
opt = dict(imya='Поджарый', ob='Сердце,хребет,Руки', radius='0.05', ugol='45', shagi='4', z='0.9:1.6')
i = 1
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
R, UG = float(opt['radius']), float(opt['ugol']); z0, z1 = (float(x) for x in opt['z'].split(':')); OB = set(opt['ob'].split(','))
OBJ = [o for o in bpy.data.objects if o.type == 'MESH' and '@' not in o.name and o.data.shape_keys and opt['imya'] in o.data.shape_keys.key_blocks]
flat = []
for o in OBJ:
    for vi, v in enumerate(o.data.vertices): flat.append((o, vi, o.matrix_world @ v.co))
kd = kdtree.KDTree(len(flat))
for j, (_, _, p) in enumerate(flat): kd.insert(p, j)
kd.balance()
rep = [None] * len(flat); X = []; CHUZH = []
for j, (o, vi, p) in enumerate(flat):
    if rep[j] is not None: continue
    u = len(X); X.append(list(o.matrix_world @ o.data.shape_keys.key_blocks[opt['imya']].data[vi].co)); CHUZH.append(False)
    for (_, jj, _) in kd.find_range(p, 0.0005):
        if rep[jj] is None: rep[jj] = u
        if flat[jj][0].name not in OB: CHUZH[u] = True
vm = {(o.name, vi): rep[j] for j, (o, vi, _) in enumerate(flat)}
X = np.array(X); N = len(X); F = []; FO = []; kraj = {}; nb = [set() for _ in range(N)]
for o in OBJ:
    bm = bmesh.new(); bm.from_mesh(o.data)
    for e in bm.edges:
        x, y = vm[(o.name, e.verts[0].index)], vm[(o.name, e.verts[1].index)]; nb[x].add(y); nb[y].add(x)
        if e.is_boundary: kraj[tuple(sorted((x, y)))] = kraj.get(tuple(sorted((x, y))), 0) + 1
    for f in bm.faces: F.append([vm[(o.name, v.index)] for v in f.verts]); FO.append(o.name)
    bm.free()
FIX = np.array(CHUZH)
for (x, y), c in kraj.items():
    if c == 1: FIX[x] = FIX[y] = True
C = np.array([X[f].mean(0) for f in F])
Nf = np.array([np.cross(X[f[1]] - X[f[0]], X[f[2]] - X[f[0]]) for f in F]); A = np.linalg.norm(Nf, axis=1) / 2; Nf /= np.maximum(2 * A, 1e-12)[:, None]
kf = kdtree.KDTree(len(F))
for k, c in enumerate(C): kf.insert(Vector(c), k)
kf.balance()
bad = []
for k in range(len(F)):
    if FO[k] not in OB or not (z0 <= C[k, 2] <= z1): continue
    ids = [q for (_, q, _) in kf.find_range(Vector(C[k]), R) if q != k]
    if len(ids) < 4: continue
    m = (Nf[ids] * A[ids, None]).sum(0); m /= max(np.linalg.norm(m), 1e-12)
    ug = math.degrees(math.acos(np.clip(Nf[k] @ m, -1, 1)))
    if ug > UG: bad.append((k, ug))
W = np.zeros(N)
for k, _ in bad:
    for v in F[k]:
        W[v] = 1.0
        for w in nb[v]: W[w] = max(W[w], 0.5)
W[FIX] = 0
NB = [list(s) for s in nb]
X0 = X.copy()
for _ in range(int(opt['shagi'])):
    for lam in (0.5, -0.53):
        L = np.array([X[n].mean(0) - X[k] if n else np.zeros(3) for k, n in enumerate(NB)]); X = X + lam * W[:, None] * L
d = np.linalg.norm(X - X0, axis=1)
print('ЩЕРБИНЫ: граней %d (угол к окружению > %.0f°), вершин разглажено %d, сдвиг средн %.4f, макс %.4f м' % (len(bad), UG, int((W > 0).sum()), d[W > 0].mean() if (W > 0).any() else 0, d.max()))
for k, ug in sorted(bad, key=lambda t: -t[1])[:12]:
    print('  щербина: %s z %.2f x %+.2f y %+.2f — %.0f°' % (FO[k], C[k, 2], C[k, 0], C[k, 1], ug))
if opt.get('kadr'):
    json.dump([dict(ob=FO[k], c=[round(float(q), 3) for q in C[k]], ugol=round(ug, 1)) for k, ug in bad], open(opt['kadr'], 'w', encoding='utf-8'), ensure_ascii=False)
for o in OBJ:
    kb = o.data.shape_keys.key_blocks[opt['imya']]; iw = o.matrix_world.inverted()
    same = all((kb.data[k].co - o.data.vertices[k].co).length < 1e-6 for k in range(len(o.data.vertices)))
    for vi in range(len(o.data.vertices)):
        p = iw @ Vector(X[vm[(o.name, vi)]]); kb.data[vi].co = p
        if same: o.data.vertices[vi].co = p; o.data.shape_keys.key_blocks[0].data[vi].co = p
if opt.get('povorot') == '1':
    n_rot = 0
    for o in OBJ:
        if o.name not in OB: continue
        kb = o.data.shape_keys.key_blocks[opt['imya']]
        bm = bmesh.new(); bm.from_mesh(o.data); bm.faces.ensure_lookup_table(); bm.verts.ensure_lookup_table()
        sk = bm.verts.layers.shape.get(opt['imya'])
        P = lambda v: o.matrix_world @ (v[sk] if sk else v.co)
        for _ in range(3):
            bm.faces.ensure_lookup_table(); kf2 = kdtree.KDTree(len(bm.faces)); Cs = []
            for f in bm.faces:
                c = sum((P(v) for v in f.verts), Vector()) / len(f.verts); Cs.append(c); kf2.insert(c, f.index)
            kf2.balance()
            def nrm(f):
                vs = [P(v) for v in f.verts]; return (vs[1] - vs[0]).cross(vs[2] - vs[0])
            Ns = [nrm(f) for f in bm.faces]; ed = set()
            for f in bm.faces:
                if not (z0 <= Cs[f.index].z <= z1): continue
                m = Vector()
                for (_, q, _) in kf2.find_range(Cs[f.index], R):
                    if q != f.index: m += Ns[q]
                if m.length < 1e-9 or Ns[f.index].length < 1e-12: continue
                if Ns[f.index].normalized().dot(m.normalized()) < 0:
                    e = max(f.edges, key=lambda e_: (P(e_.verts[0]) - P(e_.verts[1])).length)
                    if not e.is_boundary and len(e.link_faces) == 2: ed.add(e)
            if not ed: break
            r_ = bmesh.ops.rotate_edges(bm, edges=list(ed), use_ccw=False); n_rot += len(r_['edges'])
        bm.to_mesh(o.data); bm.free(); o.data.update()
    print('ЩЕРБИНЫ: повёрнуто рёбер %d' % n_rot)
bpy.ops.wm.save_as_mainfile(filepath=out)
