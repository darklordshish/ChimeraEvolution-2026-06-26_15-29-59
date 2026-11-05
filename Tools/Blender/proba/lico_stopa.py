"""ЛИЦО И СТОПА ПО ТОЧКАМ (05.11, Б7; лист `витрина/человек/огранка.png`). Прореживание оставляло на половину лица два десятка случайных вершин и 200 граней на стопу. Здесь обе зоны собираются рукой поверх готовой огранки, веса от ближайшей прежней вершины, симметрия точная.
ЛИЦО: проём в объекте `голова` (кольцо шеи и уши не трогает) заполняется точками листа: (x, z, вынос от плотной поверхности) и рёбрами-границами плоскостей; полоса глазниц разорвана переносицей, свод шире, угол челюсти наружу.
БОЛЬШОЙ ПАЛЕЦ: отдельный брусок двумя звеньями, отведён от ладони.
Запускать с внешним `timeout`, в циклах стоят предохранители: зависание 05.11 стоило часа.
СТОПА: ниже `--z_st` лодыжка режется, стопа: девять точек контура, пять пальцев брусками, без внутренних вершин. Размер берётся с прежней стопы.

  blender -b огранка.blend --factory-startup --python-exit-code 1 -P lico_stopa.py -- выход.blend плотная_база.blend [--z_st 0.105] [--svod 0.004] [--bez_lica 1] [--bez_stopy 1] [--bez_palca 1]
"""
import sys, math
import bpy, bmesh
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from mathutils.geometry import delaunay_2d_cdt

a = sys.argv[sys.argv.index('--') + 1:]; OUT, BAZ = a[:2]; opt = dict(z_st='0.105', svod='0.004', bez_lica='0', bez_stopy='0'); i = 2
while i < len(a): opt[a[i].lstrip('-')] = a[i + 1]; i += 2
ZS = float(opt['z_st']); SV = float(opt['svod'])


def snimok(bm, M):
    """положение и веса прежних вершин"""
    d = bm.verts.layers.deform.verify(); return np.array([tuple(M @ v.co) for v in bm.verts]), [dict(v[d]) for v in bm.verts], d
def dat_ves(v, p, d, A, G):
    for k_, x_ in G[int(np.linalg.norm(A - np.array(tuple(p)), axis=1).argmin())].items(): v[d][k_] = x_


def soglasovat(bm, novye):
    """обход новых граней согласуется от прежних через общие рёбра: иначе брусок пальца выворачивается стенкой к соседу"""
    nov_ = set(novye); och = [f for f in bm.faces if f not in nov_]; n_fl = 0
    n_it = 0
    while och:
        n_it += 1
        if n_it > 100000: sys.exit('СОГЛАСОВАНИЕ: цикл не сходится')
        f = och.pop()
        para = [(l2.face, l2.vert == l.vert) for l in f.loops for l2 in l.edge.link_loops if l2.face in nov_]
        for g, odin in para:   # разворот меняет петли: сперва собрать, потом вращать
            if g in nov_:
                nov_.discard(g); och.append(g)
                if odin: g.normal_flip(); n_fl += 1
    if nov_: sys.exit('СОГЛАСОВАНИЕ: не дотянулось до %d граней' % len(nov_))
    return n_fl


# ЛИЦО: ось — (имя, z, вынос), половина — имя: (x, z, вынос); рёбра — границы плоскостей
OS = [('l0', 1.790, -.002), ('gl', 1.748, 0), ('kor', 1.733, -.007), ('sp', 1.715, -.001), ('nos', 1.698, .002), ('pod', 1.683, -.005), ('gu', 1.672, -.003), ('rot', 1.664, -.003), ('gn', 1.656, 0), ('bor', 1.648, .004), ('pdb', 1.638, .009)]
TO = dict(f1=(.030, 1.795, -.001), b1=(.014, 1.746, .001), b2=(.032, 1.757, .002), b3=(.058, 1.750, .001), e1=(.012, 1.733, -.007), e2=(.030, 1.735, -.008), e3=(.052, 1.735, -.003),
          u1=(.014, 1.720, -.004), u2=(.034, 1.717, -.003), sk=(.060, 1.718, .001), n1=(.0065, 1.715, -.003), n2=(.015, 1.688, -.003), n3=(.007, 1.681, -.004), h1=(.038, 1.685, -.008), h2=(.055, 1.683, -.005),
          m1=(.027, 1.664, -.003), g1=(.012, 1.673, -.004), g2=(.012, 1.656, 0), d1=(.024, 1.639, .009), d2=(.026, 1.650, .004), ch=(.060, 1.665, .004))
LOB = ('l0', 'gl', 'f1', 'b1', 'b2')   # лоб одной плоскостью (глубина снята сбоку, x 0.03): иначе гребень плотного черепа идёт клином к носу
ROT = {'gu', 'rot', 'gn', 'g1', 'g2', 'm1'}   # губы — от одной высоты плотной поверхности: без бантика
RE = 'gl-b1 b1-b2 b2-b3 b1-e1 e1-u1 u1-u2 u2-e3 e3-b3 kor-e1 e1-n1 n1-sp n1-n2 n2-n3 n3-pod e3-sk u2-sk sk-h2 n2-h1 h1-m1 m1-d2 d2-d1 rot-m1 pod-g1 g1-m1 gn-g2 g2-m1 bor-d2 sk-ch ch-d1'.split()
if opt['bez_lica'] != '1':
    with bpy.data.libraries.load(BAZ) as (src, dst): dst.objects = ['голова', 'Чутьё']
    Vd, Fd = [], []
    for o_ in dst.objects:
        n0 = len(Vd); Vd += [o_.matrix_world @ v.co for v in o_.data.vertices]; o_.data.calc_loop_triangles(); Fd += [[n0 + q for q in t.vertices] for t in o_.data.loop_triangles]
    bvh = BVHTree.FromPolygons(Vd, Fd, all_triangles=True)
    def Y(x, z, dy):
        for k_ in range(12):   # точка шире плотной головы (угол челюсти) берёт глубину с края поверхности
            h = bvh.ray_cast(Vector((x - k_ * 0.002, -1, z)), Vector((0, 1, 0)))
            if h[0] is not None: return h[0].y - dy
        sys.exit('ЛИЦО: нет поверхности в %.3f %.3f' % (x, z))
    o = bpy.data.objects['голова']; M = o.matrix_world; Mi = M.inverted(); bm = bmesh.new(); bm.from_mesh(o.data); bm.verts.ensure_lookup_table(); A0, G0, dfl = snimok(bm, M)
    # свод и угол челюсти: сдвиг вершин до проёма
    for v in bm.verts:
        p = M @ v.co; t = max(0.0, min(1.0, (p.z - 1.76) / 0.03))
        if t > 0 and abs(p.x) > 1e-4: p.x += math.copysign(SV * t * min(abs(p.x) / 0.05, 1.0), p.x)
        if 1.645 < p.z < 1.665 and abs(p.x) > 0.05 and p.y < -0.03: p.x += math.copysign(0.004, p.x); p.z -= 0.003
        if p.z < 1.632 and p.y < -0.10 and abs(p.x) < 0.03: p.y -= 0.009   # подбородок вперёд
        if 1.62 < p.z < 1.632 and -0.08 < p.y < -0.06 and abs(p.x) < 0.02: p.z = 1.612; p.y -= 0.008   # карман под челюстью: без подреза над шеей
        v.co = Mi @ p
    R = lambda p: p.y < -0.06 and 1.63 < p.z < 1.81 and abs(p.x) < 0.0625
    fs = [f for f in bm.faces if any(R(M @ v.co) for v in f.verts)]; Hv = {v for f in fs for v in f.verts}
    was = {e for e in bm.edges if e.is_boundary}; bmesh.ops.delete(bm, geom=fs, context='FACES_ONLY')
    be = [e for e in bm.edges if e.is_boundary and e not in was and e.verts[0] in Hv and e.verts[1] in Hv]
    nb = {}
    for e in be:
        for v in e.verts: nb.setdefault(v, []).append(e.other_vert(v))
    for v in list(Hv):
        if not v.link_faces: bm.verts.remove(v)
    pos = lambda v: M @ v.co
    ax = sorted([v for v in nb if abs(pos(v).x) < 1e-4], key=lambda v: -pos(v).z)
    if len(ax) != 2: sys.exit('ЛИЦО: на оси края проёма %d вершин' % len(ax))
    top, bot = ax; ch = [top]; prev = None
    while ch[-1] != bot:
        if len(ch) > len(nb): sys.exit('ЛИЦО: край проёма зациклился')
        nx = [q for q in nb[ch[-1]] if q != prev and pos(q).x > -1e-4]
        if len(nx) != 1: sys.exit('ЛИЦО: край проёма не цепь после %d вершин' % len(ch))
        prev = ch[-1]; ch.append(nx[0])
    print('ЛИЦО: проём %d граней, край половины %d вершин, ось %.3f → %.3f' % (len(fs), len(ch), pos(top).z, pos(bot).z))
    Yv, Yn = Y(.03, 1.795, 0), Y(.03, 1.75, 0); Ylob = lambda z_: Yv + (Yn - Yv) * (1.795 - z_) / .045
    P3 = [pos(v) for v in ch]; nm = {}
    for k_, z_, dy in OS:
        if pos(bot).z + 1e-3 < z_ < pos(top).z - 1e-3: nm[k_] = len(P3); P3.append(Vector((0, Ylob(z_) if k_ in LOB else Y(0, 1.672 if k_ in ROT else z_, dy), z_)))
    for k_, (x_, z_, dy) in TO.items(): nm[k_] = len(P3); P3.append(Vector((x_, Ylob(z_) if k_ in LOB else Y(x_, 1.672 if k_ in ROT else z_, dy), z_)))
    n_ch = len(ch); Co = [(q.x, q.z) for q in P3]; E = [(i_, i_ + 1) for i_ in range(n_ch - 1)]
    osc = [nm[k_] for k_, z_, _ in OS if k_ in nm]; E += [(0, osc[0])] + list(zip(osc, osc[1:])) + [(osc[-1], n_ch - 1)]
    PP = np.array([Co[i_] for i_ in list(range(n_ch)) + osc[::-1]])
    for s_ in RE:
        x_, y_ = s_.split('-')
        if x_ in nm and y_ in nm: E.append((nm[x_], nm[y_]))
    imena = {v_: k_ for k_, v_ in nm.items()}
    def kr(e, f):
        a_, b_, c_, d_ = [np.array(Co[q]) for q in (*e, *f)]; o_ = lambda p, q, r: (q[0] - p[0]) * (r[1] - p[1]) - (q[1] - p[1]) * (r[0] - p[0])
        return not set(e) & set(f) and o_(a_, b_, c_) * o_(a_, b_, d_) < 0 and o_(c_, d_, a_) * o_(c_, d_, b_) < 0
    bad = [(e, f) for i_, e in enumerate(E) for f in E[i_ + 1:] if kr(e, f)]
    for e, f in bad: print('ЛИЦО: пересечение %s × %s' % ([imena.get(q, Co[q]) for q in e], [imena.get(q, Co[q]) for q in f]))
    if bad: print('ЛИЦО: край ' + ' '.join('%.3f,%.3f' % Co[i_] for i_ in range(n_ch)))
    CDT = delaunay_2d_cdt([Vector(c) for c in Co], E, [], 0, 1e-5)
    Vo, Fo, Oo = CDT[0], CDT[2], CDT[3]
    if len(Vo) != len(Co): sys.exit('ЛИЦО: рёбра пересекаются: новых точек %d' % (len(Vo) - len(Co)))
    orig = [q[0] for q in Oo]
    def vnutri(c):
        x, z = c; f_ = False
        for i_ in range(len(PP)):
            a_, b_ = PP[i_], PP[(i_ + 1) % len(PP)]
            if (a_[1] > z) != (b_[1] > z) and x < a_[0] + (z - a_[1]) / (b_[1] - a_[1]) * (b_[0] - a_[0]): f_ = not f_
        return f_
    tri = [t for t in Fo if vnutri(sum((Vector(Vo[q]) for q in t), Vector((0, 0))) / 3)]
    ko = {}
    def vert(q, s):
        j = orig[q]; p = P3[j].copy()
        if s < 0: p.x = -p.x
        k_ = (round(p.x, 4), round(p.y, 4), round(p.z, 4))
        if k_ in ko: return ko[k_]
        if j < n_ch:
            v_ = ch[j] if s > 0 else min(nb, key=lambda u: (pos(u) - p).length if u.is_valid else 9)
            if (pos(v_) - p).length > 1e-3: sys.exit('ЛИЦО: нет зеркальной вершины края')
        else:
            v_ = bm.verts.new(Mi @ p); dat_ves(v_, p, dfl, A0, G0)
        ko[k_] = v_; return v_
    n_tr = 0; nov_gr = []
    for s in (1, -1):
        for t in tri:
            vv = [vert(q, s) for q in t]
            f = bm.faces.new(vv); f.normal_update(); n_tr += 1
            nov_gr.append(f)
    soglasovat(bm, nov_gr); bm.normal_update(); bm.to_mesh(o.data); bm.free(); o.data.update()
    print('ЛИЦО: новых граней %d, точек половины %d' % (n_tr, len(TO) + len(nm) - len(TO)))

# УХО: круглее и прижато; кольцо не трогается, сдвигаются внешние точки
if opt.get('bez_uha') != '1':
    o = bpy.data.objects['Чутьё']; M = o.matrix_world; Mi = M.inverted(); n_u = 0
    for (x0, y0, z0), (x1, y1, z1) in (((.074, -.003, 1.772), (.074, -.012, 1.757)), ((.088, -.008, 1.725), (.080, -.006, 1.725)), ((.078, -.029, 1.745), (.076, -.027, 1.745))):
        for sx in (1, -1):
            v = min(o.data.vertices, key=lambda u: ((M @ u.co) - Vector((x0 * sx, y0, z0))).length)
            if ((M @ v.co) - Vector((x0 * sx, y0, z0))).length > 0.002: sys.exit('УХО: нет точки %.3f %.3f %.3f' % (x0, y0, z0))
            v.co = Mi @ Vector((x1 * sx, y1, z1)); n_u += 1
            if o.data.shape_keys:
                for kb_ in o.data.shape_keys.key_blocks: kb_.data[v.index].co = v.co
    o.data.update(); print('УХО: сдвинуто точек %d' % n_u)

# ШЕЯ ПОД ГОЛОВОЙ: огранка упростила шею, и её стенка на x ±0.05, z 1.56–1.60 вышла сквозь кожу головы тонкой тёмной полосой (двойная геометрия). Вершины шеи над кожей уходят под неё
if opt.get('bez_shei') != '1':
    h = bpy.data.objects['голова']; Mh = h.matrix_world; h.data.calc_loop_triangles(); Vh = [Mh @ v.co for v in h.data.vertices]; bh = BVHTree.FromPolygons(Vh, [list(t_.vertices) for t_ in h.data.loop_triangles], all_triangles=True)
    o = bpy.data.objects['шея']; M = o.matrix_world; Mi = M.inverted(); n_sh = 0; mx = 0.0
    def snaruzhi(p):
        q, nn, _, d_ = bh.find_nearest(p); return q, nn, d_ if (p - q).dot(nn) > 0 else -d_
    for v in o.data.vertices:
        p = M @ v.co
        if not (p.z > 1.555 and abs(p.x) < 0.075): continue
        q, nn, d_ = snaruzhi(p)
        if abs(d_) < 2e-4 or p.y > 0.03: continue   # вершины стыка с головой и затылок не трогаются
        if d_ > 2e-4 or d_ > -0.018:
            v.co = Mi @ (q - nn * 0.018); n_sh += 1; mx = max(mx, d_)
            if o.data.shape_keys:   # без этого меш рисуется по ключу-базе и правка молчит
                for kb_ in o.data.shape_keys.key_blocks: kb_.data[v.index].co = v.co   # глубоко: плоскость шеи, почти касательная к коже, и давала тонкий язык
    o.data.update(); o.data.calc_loop_triangles(); ost = 0.0
    for t_ in o.data.loop_triangles:
        c_ = M @ t_.center
        if c_.z > 1.555 and abs(c_.x) < 0.07: ost = max(ost, snaruzhi(c_)[2])
    print('ШЕЯ: под кожу головы ушло вершин %d, выступали до %.1f мм; центр грани над кожей теперь до %.1f мм' % (n_sh, mx * 1000, ost * 1000))

# СТОПА
if opt['bez_stopy'] != '1':
    o = bpy.data.objects['Ноги']; M = o.matrix_world; Mi = M.inverted(); bm = bmesh.new(); bm.from_mesh(o.data); bm.verts.ensure_lookup_table(); An, G0, dfl = snimok(bm, M); n_was = len(bm.faces)
    Rr = An[(An[:, 0] > 0) & (An[:, 2] < 0.05)]
    He = Rr[Rr[:, 1] > Rr[:, 1].max() - 0.03].mean(0); To = Rr[Rr[:, 1] < Rr[:, 1].min() + 0.03].mean(0); ax_ = (To - He) * [1, 1, 0]; L_ = np.linalg.norm(ax_); ax_ = ax_ / L_; la = np.array([-ax_[1], ax_[0], 0.0]); la = la if la[0] > 0 else -la
    S_ = (Rr - He).dot(ax_) / L_; Vl = (Rr - He).dot(la)
    def shir(s0):
        q = Vl[np.abs(S_ - s0) < 0.12]; return (float(q.max()), float(-q.min())) if len(q) else (0.04, 0.04)
    print('СТОПА: длина %.3f, ось %s, полуширина наружу/внутрь: пятка %s, свод %s, подъём %s' % (L_, ax_.round(3), np.round(shir(.08), 3), np.round(shir(.4), 3), np.round(shir(.7), 3)))
    bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], dist=1e-5, plane_co=Mi @ Vector((0, 0, ZS)), plane_no=(Mi.to_3x3() @ Vector((0, 0, 1))).normalized(), clear_inner=True)
    bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 3])
    ot = [e for e in bm.edges if e.is_boundary and abs((M @ e.verts[0].co).z - ZS) < 1e-4 and abs((M @ e.verts[1].co).z - ZS) < 1e-4]
    pz = lambda v: M @ v.co
    nov_gr = []
    for sg in (1, -1):
        be = [e for e in ot if pz(e.verts[0]).x * sg > 0]; nb = {}
        for e in be:
            for v in e.verts: nb.setdefault(v, []).append(e.other_vert(v))
        st = next(iter(nb)); Lp = [st]; pr = None
        while True:
            if len(Lp) > len(nb): sys.exit('СТОПА: кольцо лодыжки зациклилось')
            nx = [q for q in nb[Lp[-1]] if q != pr]
            if not nx or nx[0] == st: break
            pr = Lp[-1]; Lp.append(nx[0])
        if len(Lp) != len(nb): sys.exit('СТОПА: кольцо лодыжки не одно: %d из %d' % (len(Lp), len(nb)))
        def T(s, v, z):
            w = shir(s); u = He + ax_ * L_ * s + la * (v * (w[0] if v > 0 else w[1])); return Vector((float(u[0]) * sg, float(u[1]), z))
        def nov(p):
            v_ = bm.verts.new(Mi @ p); dat_ves(v_, p, dfl, An, G0); return v_
        # контур верха Q и подошвы B; обход: большой палец → внутрь → пятка → наружу → мизинец → щель
        V_ = [1.0, .76, .72, .48, .44, .20, .16, -.12, -.20]   # щели короткие: пальцы связаны у основания, большой шире
        Qd = [(.82, -1, .042), (.40, -.75, .056), (.08, -.8, .055), (.0, 0, .062), (.08, .8, .050), (.40, .85, .038)] + [(.82, v_, .026 + .016 * (1 - v_) / 1.2) for v_ in V_]
        Bd = [(.82, -1, 0), (.40, -.6, 0), (.08, -.7, 0), (.02, 0, 0), (.08, .75, 0), (.40, .85, 0)] + [(.82, v_, 0) for v_ in V_]
        Q = [nov(T(*q)) for q in Qd]; B = [nov(T(*q)) for q in Bd]; n = len(Q); cc = Vector((float((He + ax_ * L_ * .4)[0]) * sg, float((He + ax_ * L_ * .4)[1]), .03)); Cm = Mi @ cc
        def gr(*vs):
            f = bm.faces.new(vs); nov_gr.append(f); return f
        KOR = (6, 8, 10, 12, 14)   # корни пальцев: от мизинца к большому; 14 → 0 большой
        for i_ in range(n):
            j_ = (i_ + 1) % n
            if i_ in KOR: continue
            gr(Q[i_], Q[j_], B[j_]); gr(Q[i_], B[j_], B[i_])
        for i_ in range(1, n - 1): gr(B[0], B[i_], B[i_ + 1])
        def palec(a0, a1, b1, b0, t0, t1):
            c0 = nov(T(*t0)); c1 = nov(T(*t1)); d0 = nov(T(t0[0], t0[1], 0)); d1 = nov(T(t1[0], t1[1], 0))
            gr(a0, a1, c1); gr(a0, c1, c0); gr(a1, b1, d1); gr(a1, d1, c1); gr(b0, a0, c0); gr(b0, c0, d0); gr(c0, c1, d1); gr(c0, d1, d0); gr(b0, d0, d1); gr(b0, d1, b1)
        palec(Q[14], Q[0], B[0], B[14], (1.02, -.24, .030), (1.01, -.97, .028))
        for k_, (s_, z_) in enumerate(((.90, .016), (.93, .018), (.96, .020), (.98, .022))):
            i_ = 6 + 2 * k_; palec(Q[i_], Q[i_ + 1], B[i_ + 1], B[i_], (s_, V_[2 * k_] - .02, z_), (s_, V_[2 * k_ + 1] + .02, z_))
        # верх: контур Q с кольцом лодыжки, застёжка по кратчайшей диагонали
        L = Lp; cq = sum((pz(v) for v in Q), Vector()) / n; cl = sum((pz(v) for v in L), Vector()) / len(L)
        ang = lambda p, c: math.atan2(p.y - c.y, p.x - c.x)
        obh = lambda R_, c: sum(math.remainder(ang(pz(R_[(i_ + 1) % len(R_)]), c) - ang(pz(R_[i_]), c), 2 * math.pi) for i_ in range(len(R_))) > 0
        if obh(L, cl) != obh(Q, cq): L = L[::-1]
        # застёжка по углу вокруг центра лодыжки: кольца звёздны от него, складок нет
        ug = lambda v: math.atan2(pz(v).y - cl.y, pz(v).x - cl.x)
        i0 = min(range(len(L)), key=lambda i_: (pz(L[i_]) - pz(Q[3])).length); L = L[i0:] + L[:i0]; Qs = Q[3:] + Q[:3]; nl = len(L); zn = 1 if obh(L, cl) else -1; a0 = ug(Qs[0])
        par = lambda v: (zn * (ug(v) - a0)) % (2 * math.pi)
        pL = [par(v) for v in L]; pL[0] = 0.0; pQ = [par(v) for v in Qs]; pQ[0] = 0.0; i_ = j_ = 0
        while i_ < nl or j_ < n:
            da = pL[i_ + 1] if i_ + 1 < nl else 2 * math.pi; db = pQ[j_ + 1] if j_ + 1 < n else 2 * math.pi
            if i_ < nl and (j_ >= n or da <= db): gr(L[i_ % nl], Qs[j_ % n], L[(i_ + 1) % nl]); i_ += 1
            else: gr(L[i_ % nl], Qs[j_ % n], Qs[(j_ + 1) % n]); j_ += 1
    soglasovat(bm, nov_gr); bm.normal_update(); n_st = len(bm.faces); bm.to_mesh(o.data); o.data.update(); print('СТОПА: граней объекта %d → %d' % (n_was, n_st)); bm.free()
# БОЛЬШОЙ ПАЛЕЦ: был плоской пластиной на краю ладони; теперь отдельный брусок двумя звеньями, отведён от ладони. Веса кисти одной костью, прежний палец тоже был отдельным островом
if opt.get('bez_palca') != '1':
    o = bpy.data.objects['Руки']; M = o.matrix_world; Mi = M.inverted(); bm = bmesh.new(); bm.from_mesh(o.data); bm.verts.ensure_lookup_table(); An, G0, dfl = snimok(bm, M); n_was = len(bm.faces)
    for sg in (1, -1):
        v0 = min(bm.verts, key=lambda v: ((M @ v.co) - Vector((.314 * sg, -.116, .849))).length); Ost = {v0}; och_ = [v0]
        while och_:
            u = och_.pop()
            for e_ in u.link_edges:
                q = e_.other_vert(u)
                if q not in Ost: Ost.add(q); och_.append(q)
        if len(Ost) > 12: sys.exit('ПАЛЕЦ: прежний палец не отдельный остров: %d вершин' % len(Ost))
        print('ПАЛЕЦ: снят прежний брусок, вершин %d' % len(Ost)); bmesh.ops.delete(bm, geom=list(Ost), context='VERTS')
        # сечение звена (x, y, z центра; полутолщина по x, полуширина по направлению): корень внутри ладони, сустав, кончик
        Sc = [(.318, -.070, .922, .009, .014), (.312, -.103, .876, .012, .014), (.309, -.118, .832, .009, .011)]
        Kol = []
        for x_, y_, z_, hx, hw in Sc:
            Kol.append([nov_ for nov_ in [bm.verts.new(Mi @ Vector(((x_ + dx) * sg, y_ + dy, z_ + dz))) for dx, dy, dz in ((-hx, -hw * .8, -hw * .6), (hx, -hw * .8, -hw * .6), (hx, hw * .8, hw * .6), (-hx, hw * .8, hw * .6))]])
        pal = []
        for r_ in Kol:
            for v_ in r_: dat_ves(v_, M @ v_.co, dfl, An, G0)
        for r0, r1 in zip(Kol, Kol[1:]):
            for i_ in range(4):
                j_ = (i_ + 1) % 4; pal.append(bm.faces.new((r0[i_], r0[j_], r1[j_], r1[i_])))
        pal.append(bm.faces.new(Kol[0][::-1])); pal.append(bm.faces.new(Kol[-1]))
        c_ = sum((v.co for r_ in Kol for v in r_), Vector()) / 12; bmesh.ops.recalc_face_normals(bm, faces=pal)
        ot_ = sum((f.normal.dot(f.calc_center_median() - c_) for f in pal))
        if ot_ < 0:
            for f in pal: f.normal_flip()
        bmesh.ops.triangulate(bm, faces=pal)
    bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 3]); bm.normal_update(); n_st = len(bm.faces); otk = sum(e.is_boundary for e in bm.edges); bm.to_mesh(o.data); o.data.update(); print('ПАЛЕЦ: граней объекта %d → %d, открытых рёбер %d' % (n_was, n_st, otk)); bm.free()
bpy.ops.wm.save_as_mainfile(filepath=OUT)
