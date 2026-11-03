"""ГРАНИ ОБЛАСТИ (03.11, обобщение `press_grani.py`; критик r2: «спина — рябь 3–5 см, нужно по листу: одна плоскость надостной, ребро ости, одна подостная»). Область объекта (все вершины грани в
рамке |x|, z и на своей стороне тела) вырезается и заполняется заново из заданных точек формы: ограниченная триангуляция Делоне по проекции (x, z), край области сохраняется, включая края
объекта (швы). Глубина точки — с поверхности тела лучом с этой стороны плюс `dy` наружу. Обе половины тела зеркально. Веса и прочие ключи — от ближайшей вершины края. Ключ и база правятся вместе.
Файл областей: [{"imya", "ob", "vid": "back"|"front", "x": [a, b], "z": [c, d], "tochki": [[|x|, z, dy], …]}, …].
`"rebra": [[i, j], …]` — рёбра-ограничения между точками (номера в `tochki`, зеркальная половина — сама): грань идёт по черте, а не как положит Делоне.
`"abs": 1` — третье число точки абсолютный y (лицо: x и z с анфаса листа, y с профиля), не от поверхности. `"os": 1` — область через ось: один проход, |x| < x[1], точки с |x| > 0 зеркалятся.
Область с дырками (остров других граней внутри) — внутренние контуры остаются ограничениями, дырка не закрывается.

  blender -b вход.blend --factory-startup -P grani_oblasti.py -- выход.blend --oblasti файл.json [--imya Поджарый]
"""
import sys, json
import bpy, bmesh
from mathutils import Vector, kdtree
from mathutils.bvhtree import BVHTree
from mathutils.geometry import delaunay_2d_cdt

a = sys.argv[sys.argv.index('--') + 1:]
out = a[0]
opt = dict(imya='Поджарый')
i = 1
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
OBL = json.load(open(opt['oblasti'], encoding='utf-8'))


def telo():
    V, F = [], []
    for b in bpy.data.objects:
        if b.type != 'MESH' or '@' in b.name or not b.data.shape_keys or opt['imya'] not in b.data.shape_keys.key_blocks: continue
        kb = b.data.shape_keys.key_blocks[opt['imya']]; n0 = len(V); V += [b.matrix_world @ k.co for k in kb.data]
        b.data.calc_loop_triangles(); F += [[n0 + q for q in t.vertices] for t in b.data.loop_triangles]
    return BVHTree.FromPolygons(V, F, all_triangles=True)


for ob_ in OBL:
    for sd in ((0,) if ob_.get('os') else (-1, 1)):
        o = bpy.data.objects[ob_['ob']]; Mw = o.matrix_world; iw = Mw.inverted(); bvh = telo()
        ysg = 1.0 if ob_['vid'] == 'back' else -1.0
        bm = bmesh.new(); bm.from_mesh(o.data)
        keys = [k.name for k in o.data.shape_keys.key_blocks]; sh = {k: bm.verts.layers.shape[k] for k in keys}; dv = bm.verts.layers.deform.active; KS = sh[opt['imya']]
        W_ = lambda v: Mw @ v[KS]
        vn = lambda v: (abs(W_(v).x) <= ob_['x'][1] if sd == 0 else ob_['x'][0] <= W_(v).x * sd <= ob_['x'][1]) and ob_['z'][0] <= W_(v).z <= ob_['z'][1] and W_(v).y * ysg > 0
        def smotrit(f):      # грань смотрит на свою сторону: иначе проекция (x, z) складывается у плеча и подмышки, и ограничения пересекаются
            q = [W_(v) for v in f.verts]; n_ = (q[1] - q[0]).cross(q[2] - q[0])
            if n_.length < 1e-12: return False
            if ob_.get('niz_z'):     # лицо: вся передняя половина рамки (нос складывается в проекции, его грани уходят целиком), кроме граней под подбородком; край идёт по щекам
                c_ = sum(q, Vector()) / len(q); nz = n_.normalized()
                return c_.y < ob_.get('y_max', -0.04) and not (nz.z < -0.5 and c_.z < ob_['niz_z'])
            return n_.normalized().y * ysg > float(ob_.get('nakl', 0.25))
        ubr = [f for f in bm.faces if all(vn(v) for v in f.verts) and smotrit(f)]
        if not ubr: print('ГРАНИ: %s %+d — пусто' % (ob_['imya'], sd)); bm.free(); continue
        # крупнейший связный кусок
        ost = set(ubr); best = []
        while ost:
            st = [next(iter(ost))]; c_ = set()
            while st:
                f = st.pop()
                if f in c_ or f not in ost: continue
                c_.add(f)
                for e in f.edges:
                    for g in e.link_faces:
                        if g in ost and g not in c_: st.append(g)
            ost -= c_
            if len(c_) > len(best): best = list(c_)
        ubr = best; S_ = set(ubr)
        gran = {e for f in ubr for e in f.edges if sum(1 for g in e.link_faces if g in S_) == 1}
        vg = list({v for e in gran for v in e.verts}); idx = {v: j for j, v in enumerate(vg)}
        vnutr = {v for f in ubr for v in f.verts} - set(vg)
        coords = [Vector((W_(v).x, W_(v).z)) for v in vg]; edges = [(idx[e.verts[0]], idx[e.verts[1]]) for e in gran]
        P = []; num = {}
        for ti, (x, z, dy) in enumerate(ob_['tochki']):
            num[(ti, 1)] = len(P); P.append(((x if sd == 0 else sd * x), z, dy))
            if sd == 0 and x > 0: num[(ti, -1)] = len(P); P.append((-x, z, dy))
            else: num[(ti, -1)] = num[(ti, 1)]
        # проекция прежней области для проверки «внутри»
        tr = []
        for f in ubr:
            q = [W_(v) for v in f.verts]
            for k in range(1, len(q) - 1): tr.append(((q[0].x, q[0].z), (q[k].x, q[k].z), (q[k + 1].x, q[k + 1].z)))

        def vnutri(p):
            for A, B, C in tr:
                d1 = (p[0] - B[0]) * (A[1] - B[1]) - (A[0] - B[0]) * (p[1] - B[1]); d2 = (p[0] - C[0]) * (B[1] - C[1]) - (B[0] - C[0]) * (p[1] - C[1]); d3 = (p[0] - A[0]) * (C[1] - A[1]) - (C[0] - A[0]) * (p[1] - A[1])
                if not ((d1 < 0 or d2 < 0 or d3 < 0) and (d1 > 0 or d2 > 0 or d3 > 0)): return True
            return False
        vne = [q for q in P if not vnutri(q[:2])]
        if vne: print('ГРАНИ: %s — точки вне области отброшены: %s' % (ob_['imya'], [(round(q[0], 3), round(q[1], 3)) for q in vne]))
        ost_ = [j for j, q in enumerate(P) if vnutri(q[:2])]; nov_n = {j: n_ for n_, j in enumerate(ost_)}; P = [P[j] for j in ost_]
        n_g = len(coords); coords += [Vector((x, z)) for x, z, _ in P]
        for i_, j_ in ob_.get('rebra', []):
            for z_s in (1, -1):
                a_, b_ = num[(i_, z_s)], num[(j_, z_s)]
                if a_ in nov_n and b_ in nov_n and a_ != b_: edges.append((n_g + nov_n[a_], n_g + nov_n[b_]))
        # внешний контур: триангуляция внутри ограничений, лишнее снаружи области отсекается проверкой: центр грани должен лежать внутри прежней области
        vs, _, fs, ov, _, _ = delaunay_2d_cdt(coords, edges, [], 1, 1e-6)
        kd = kdtree.KDTree(len(vg))
        for j, v in enumerate(vg): kd.insert(W_(v), j)
        kd.balance()
        mapv = {}; nov = []
        for j, ol in enumerate(ov):
            if not ol: sys.exit('ГРАНИ: триангуляция добавила точку (ограничения пересекаются) в %s' % ob_['imya'])
            q = ol[0]
            if q < len(vg): mapv[j] = vg[q]
            else: nov.append((j, P[q - len(vg)]))
        n_ub = len(ubr)
        bmesh.ops.delete(bm, geom=ubr, context='FACES_ONLY')
        bmesh.ops.delete(bm, geom=[v for v in vnutr if v.is_valid], context='VERTS')
        for j, (x, z, dy) in nov:
            if ob_.get('abs'): p = Vector((x, dy, z))
            else:
                h = bvh.ray_cast(Vector((x, ysg * 1.0, z)), Vector((0, -ysg, 0)))
                if h[0] is None: sys.exit('ГРАНИ: луч мимо тела (%.3f, %.3f)' % (x, z))
                p = Vector((x, h[0].y + ysg * dy, z))
            v = bm.verts.new(iw @ p); _, near, _ = kd.find(p); s = vg[near]
            for k in keys: v[sh[k]] = (iw @ p) + (s[sh[k]] - s[KS])
            for g, w in s[dv].items(): v[dv][g] = w
            mapv[j] = v
        n_new = 0
        for f in fs:
            c2 = (sum(vs[q][0] for q in f) / 3, sum(vs[q][1] for q in f) / 3)
            if not vnutri(c2): continue
            try:
                nf = bm.faces.new([mapv[q] for q in f]); n_new += 1; nf.normal_update()
                if (Mw.to_3x3() @ nf.normal).y * ysg < 0: nf.normal_flip()
            except ValueError: pass
        kr = sum(1 for e in bm.edges if e.is_boundary)
        bm.to_mesh(o.data); bm.free(); o.data.update()
        print('ГРАНИ: %s %+d — граней %d → %d, края %d вершин, новых %d, открытых рёбер объекта %d' % (ob_['imya'], sd, n_ub, n_new, len(vg), len(nov), kr))
bpy.ops.wm.save_as_mainfile(filepath=out)
