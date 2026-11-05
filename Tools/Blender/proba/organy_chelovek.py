"""ОРГАНЫ ДОНОРОВ НА ЧЕЛОВЕКЕ (05.11, листы `витрина/органы/`, паспорт там же). Орган — объект `<слот>@<вид>` в меше шасси: меши волка и лося не меняются, кусок органа при таком объекте не рисуется (`MorphBuilder.ByOrganObject`). Форма задана точками в метрах тела Б8, язык граней тот же: крупные плоскости, без лишних вершин. Веса — от ближайшей вершины тела или кость целиком. Размеры сняты по телу человека, а не с листа. Симметрия точная.

  blender -b тело.blend --factory-startup --python-exit-code 1 -P organy_chelovek.py -- выход.blend [--tolko Рога@Лось,…] [--otchet тр.json]
Дальше каждый объект идёт в сборку через `organ_objekt.py` (база прижата к телу, форма `рост` полная).
"""
import sys, math, json
import bpy, bmesh
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from mathutils.geometry import delaunay_2d_cdt

a = sys.argv[sys.argv.index('--') + 1:]; OUT = a[0]; opt = {}; i = 1
while i < len(a): opt[a[i].lstrip('-')] = a[i + 1]; i += 2
TOLKO = set(filter(None, opt.get('tolko', '').split(',')))
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
TELO = [o for o in bpy.data.objects if o.type == 'MESH' and '@' not in o.name and o.name != 'Cube']
Vb, Fb, Wb = [], [], []
for o in TELO:
    kb = o.data.shape_keys.key_blocks[0] if o.data.shape_keys else None; M = o.matrix_world; n0 = len(Vb); gn = {g.index: g.name for g in o.vertex_groups}
    for j, v in enumerate(o.data.vertices):
        Vb.append(M @ (kb.data[j].co if kb else v.co)); Wb.append({gn[g.group]: g.weight for g in v.groups if g.weight > 0})
    o.data.calc_loop_triangles(); Fb += [[n0 + q for q in t.vertices] for t in o.data.loop_triangles]
BVH = BVHTree.FromPolygons(Vb, Fb, all_triangles=True); VA = np.array([tuple(v) for v in Vb])


def kozha(p, d):
    """первая точка кожи на луче из p по d: (точка, нормаль)"""
    h = BVH.ray_cast(Vector(p), Vector(d).normalized())
    if h[0] is None: sys.exit('ОРГАН: луч %s %s мимо тела' % (p, d))
    n = h[1] if h[1].dot(Vector(d)) < 0 else -h[1]; return h[0], n


def ves_tela(p):
    return Wb[int(np.linalg.norm(VA - np.array(tuple(p)), axis=1).argmin())]


class Org:
    """половина органа: вершины и грани; зеркалит сам, x > 0 — исходная сторона"""
    def __init__(s, im): s.im = im; s.V = []; s.F = []; s.K = []; s.os = {}
    def v(s, p, kost=None):
        """kost: имя кости (для x>0 с суффиксом .L, если в группах есть), None — веса ближайшего тела"""
        p = Vector(p); k_ = (round(p.x, 5), round(p.y, 5), round(p.z, 5))
        if k_ in s.os: return s.os[k_]
        s.V.append(p); s.K.append(kost); s.os[k_] = len(s.V) - 1; return s.os[k_]
    def f(s, *q):
        if len(set(q)) == len(q): s.F.append(tuple(q))
    def lofta(s, kol, nachalo=True, konec=True):
        """кольца равной длины, оболочка; торцы — веером"""
        for r0, r1 in zip(kol, kol[1:]):
            n = len(r0)
            for j in range(n):
                k = (j + 1) % n; s.f(r0[j], r0[k], r1[k]); s.f(r0[j], r1[k], r1[j])
        for r_, ok, rev in ((kol[0], nachalo, True), (kol[-1], konec, False)):
            if not ok: continue
            for j in range(1, len(r_) - 1): s.f(*((r_[0], r_[j + 1], r_[j]) if rev else (r_[0], r_[j], r_[j + 1])))
    def konec(s, kol, tip):
        n = len(kol)
        for j in range(n): s.f(kol[j], kol[(j + 1) % n], tip)
    def sobrat(s, zerkalo=True):
        V = list(s.V); K = list(s.K); F = list(s.F); ind = {}
        if zerkalo:
            for j, p in enumerate(s.V):
                if abs(p.x) < 1e-5: ind[j] = j
                else: ind[j] = len(V); V.append(Vector((-p.x, p.y, p.z))); K.append(s.K[j])
            F += [tuple(ind[q] for q in reversed(t)) for t in s.F]
        me = bpy.data.meshes.new(s.im); me.from_pydata([tuple(p) for p in V], [], F); me.update(); o = bpy.data.objects.new(s.im, me); bpy.context.scene.collection.objects.link(o); o.parent = arm
        m = o.modifiers.new('ск', 'ARMATURE'); m.object = arm
        imena = {g.name for g in TELO[0].vertex_groups} | {g.name for t_ in TELO for g in t_.vertex_groups}
        for j, (p, k) in enumerate(zip(V, K)):
            if k:
                nm = k if not (p.x > 0 and (k + '.L') in imena) else k + '.L'
                if nm not in imena: sys.exit('ОРГАН: нет кости %s' % nm)
                (o.vertex_groups.get(nm) or o.vertex_groups.new(name=nm)).add([j], 1.0, 'REPLACE')
            else:
                for n_, w_ in ves_tela(p).items(): (o.vertex_groups.get(n_) or o.vertex_groups.new(name=n_)).add([j], w_, 'REPLACE')
        for p_ in me.polygons: p_.use_smooth = False
        bm = bmesh.new(); bm.from_mesh(me); bmesh.ops.triangulate(bm, faces=bm.faces[:]); bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:]); bm.to_mesh(me); bm.free(); me.update(); return o


def plast(O, kontur, sredina, y_ot, tolshch, kost):
    """плоский орган с заострённым краем: контур (u, z) и внутренние точки сгибаются лодочкой; y_ot(u, z) — серединная поверхность, tolshch — полутолщина внутренних точек"""
    P2 = kontur + sredina; n = len(kontur); E = [(j, (j + 1) % n) for j in range(n)]
    cdt = delaunay_2d_cdt([Vector(p) for p in P2], E, [], 0, 1e-6); Vo, Fo, Oo = cdt[0], cdt[2], cdt[3]; orig = [q[0] for q in Oo]
    PP = np.array(kontur)
    def vnutri(c):
        x, z = c; f_ = False
        for j in range(n):
            a_, b_ = PP[j], PP[(j + 1) % n]
            if (a_[1] > z) != (b_[1] > z) and x < a_[0] + (z - a_[1]) / (b_[1] - a_[1]) * (b_[0] - a_[0]): f_ = not f_
        return f_
    tri = [t for t in Fo if vnutri(tuple(sum((Vector(Vo[q]) for q in t), Vector((0, 0))) / 3))]
    for sg in (-1, 1):
        ind = {}
        for j, (u, z) in enumerate(P2):
            yy = y_ot(u, z); dy = 0 if j < n else sg * tolshch
            ind[j] = O.v((u, yy + dy, z), kost)
        for t in tri:
            q = [ind[orig[x]] for x in t]; O.f(*(q if sg > 0 else q[::-1]))


Sob = {}
def organ(im):
    def d(fn):
        Sob[im] = fn; return fn
    return d


@organ('Рога@Лось')
def roga():
    O = Org('Рога@Лось'); X0 = 0.068
    k = [(X0, 1.775), (X0 + .10, 1.765), (X0 + .20, 1.785), (X0 + .30, 1.785), (X0 + .42, 1.805), (X0 + .535, 1.875), (X0 + .445, 1.87), (X0 + .52, 1.965), (X0 + .41, 1.905), (X0 + .445, 2.04), (X0 + .345, 1.925), (X0 + .36, 2.07), (X0 + .275, 1.945), (X0 + .27, 2.05), (X0 + .205, 1.925), (X0 + .13, 2.00), (X0 + .115, 1.865), (X0 + .05, 1.825), (X0, 1.82)]
    sr = [(X0 + .24, 1.85), (X0 + .36, 1.87), (X0 + .12, 1.80)]
    plast(O, k, sr, lambda u, z: -.03 - .10 * (u - X0) + .10 * (z - 1.8), .014, 'голова')
    return O


@organ('Пасть@Волк')
def morda_volk():
    O = Org('Пасть@Волк'); H = 'голова'
    def kol(y, w, zt, zm, zb=None):
        return [O.v((0, y, zm), H), O.v((w, y, zm), H), O.v((w * .96, y, zt - .016 * (w / .05)), H), O.v((0, y, zt), H)]
    # верх морды: половина сечения домиком; ось — общие точки
    U = [kol(-.085, .060, 1.732, 1.668), kol(-.150, .050, 1.722, 1.666), kol(-.205, .036, 1.708, 1.664), kol(-.228, .028, 1.698, 1.664)]
    for r0, r1 in zip(U, U[1:]):
        for j in range(3): O.f(r0[j], r1[j], r1[j + 1]); O.f(r0[j], r1[j + 1], r0[j + 1])
    t_ = U[-1]; O.f(t_[0], t_[1], t_[2]); O.f(t_[0], t_[2], t_[3])
    # низ морды: нижняя челюсть короче, рот приоткрыт
    def kolj(y, w, zv, zn):
        return [O.v((0, y, zn), H), O.v((w * .8, y, zn + .004), H), O.v((w, y, zv), H), O.v((0, y, zv), H)]
    J = [kolj(-.090, .056, 1.660, 1.622), kolj(-.150, .046, 1.658, 1.632), kolj(-.198, .028, 1.655, 1.642)]
    for r0, r1 in zip(J, J[1:]):
        for j in range(3): O.f(r0[j], r0[j + 1], r1[j + 1]); O.f(r0[j], r1[j + 1], r1[j])
    t_ = J[-1]; O.f(t_[0], t_[2], t_[1]); O.f(t_[0], t_[3], t_[2])
    # клыки: верхний и нижний, по три грани
    for (x, y, z0, dl) in ((.026, -.197, 1.668, -.020), (.030, -.150, 1.670, -.010), (.019, -.176, 1.655, .012)):
        b = [O.v((x - .006, y + .005, z0), H), O.v((x + .006, y + .005, z0), H), O.v((x, y - .007, z0), H)]; O.konec(b, O.v((x, y, z0 + dl), H))
    return O


@organ('Чутьё@Волк')
def ushi_volk():
    O = Org('Чутьё@Волк'); H = 'голова'
    ver = O.v((.105, -.004, 1.985), H)
    Tk = []
    for y, z in ((-.035, 1.775), (-.035, 1.835), (.025, 1.835), (.025, 1.775)):
        p, _ = kozha((.35, y, z + .10), (-.35, 0, -.10)); Tk.append(O.v(p - Vector((.006, 0, .003)), H))
    a_, b_, c_, d_ = Tk
    h = O.v((.082, -.014, 1.875), H)
    for q in ((ver, b_, h), (ver, h, a_), (a_, h, b_), (ver, a_, d_), (ver, d_, c_), (ver, c_, b_)): O.f(*q)
    return O


def konchiki():
    """кончики пальцев кисти x>0 базы: острова руки ниже запястья"""
    o = bpy.data.objects['Руки']; M = o.matrix_world; bm = bmesh.new(); bm.from_mesh(o.data); seen = set(); R = []
    for v in bm.verts:
        if v in seen or (M @ v.co).x <= 0: continue
        st = [v]; seen.add(v); L = []
        while st:
            u = st.pop(); L.append(M @ u.co)
            for e in u.link_edges:
                q = e.other_vert(u)
                if q not in seen: seen.add(q); st.append(q)
        A = np.array([tuple(p) for p in L])
        if len(L) <= 16 and A[:, 2].max() < 0.97: R.append(A)
    bm.free(); T = []
    for A in R:
        i_ = np.argsort(A[:, 2])[:3]; T.append((Vector(A[i_].mean(0)), Vector(A.mean(0))))
    return T


@organ('Руки@Волк')
def kogti_volk():
    O = Org('Руки@Волк'); T = konchiki(); print('КОГТИ: кончиков на кисть %d' % len(T)); K = 'предплечье'
    for tip, c in T:
        d = (tip - c).normalized(); pr = Vector((-1, 0, 0))   # внутрь кисти (к бедру): коготь загнут к ладони
        st = tip - d * .012
        s1 = pr.cross(d).normalized(); s2 = d.cross(s1).normalized()
        kol0 = [O.v(st + s1 * (.009 * math.cos(u)) + s2 * (.008 * math.sin(u)), K) for u in (0, 2.09, 4.19)]
        mid = tip + d * .018 + pr * .004
        kol1 = [O.v(mid + s1 * (.005 * math.cos(u)) + s2 * (.0045 * math.sin(u)), K) for u in (0, 2.09, 4.19)]
        for j in range(3):
            k = (j + 1) % 3; O.f(kol0[j], kol0[k], kol1[k]); O.f(kol0[j], kol1[k], kol1[j])
        O.konec(kol1, O.v(tip + d * .034 + pr * .014, K))
    return O


def list_sherst(O, x, z, dl, sh, nakl, sboku=False, kost=None):
    """лист гривы: корень на коже (луч сзади или сверху-сбоку), кончик вниз и наружу, ребро приподнято"""
    p0 = Vector((x, .35, z)) if not sboku else Vector((x + .3, .05, z + .25))
    d = Vector((0, -1, 0)) if not sboku else Vector((-.77, -.1, -.63))
    if BVH.ray_cast(Vector(p0), d.normalized())[0] is None: return   # за контуром тела лист не садится
    p, n = kozha(p0, d)
    n = Vector((min(abs(x) / .25, 1) * .7, .75, .15)).normalized() if not sboku else n   # нормаль от оси спины, не от случайной грани: ряды ложатся ровно
    vn = Vector((math.sin(nakl), 0, -math.cos(nakl))); vn = (vn - n * vn.dot(n)).normalized(); bk = n.cross(vn).normalized()
    if bk.x < 0: bk = -bk
    koren = O.v(p - n * .006 - vn * .01, kost); l = O.v(p + vn * (dl * .4) + bk * sh + n * .012, kost); r = O.v(p + vn * (dl * .4) - bk * sh + n * .012, kost)
    gr = O.v(p + vn * (dl * .45) + n * (.012 + sh * .3), kost); kon = O.v(p + vn * dl + n * .016, kost)
    for q in (((koren, gr, l), (l, gr, kon)) if abs(x) < 1e-4 else ((koren, gr, l), (koren, r, gr), (l, gr, kon), (gr, r, kon))): O.f(*q)


@organ('Шкура@Волк')
def griva():
    O = Org('Шкура@Волк')
    # загривок и плечи: ряды листьев вниз и наружу, ряд перекрывает ряд, размеры по телу
    for x, z, dl, sh, nk in ((0, 1.585, .16, .05, 0), (.052, 1.56, .16, .05, .2), (0, 1.46, .18, .055, 0), (.082, 1.475, .17, .055, .35), (.16, 1.47, .16, .052, .7), (.045, 1.355, .17, .05, .1), (.125, 1.375, .17, .052, .35), (.205, 1.385, .15, .05, .8)):
        list_sherst(O, x, z, dl, sh, nk)
    return O


def morda(O, verh, niz, zuby=(), H='голова'):
    """морда: верх — сечения домиком (y, полуширина, z верха, z низа), низ — челюсть; половина с осью"""
    def kv(y, w, zt, zm): return [O.v((0, y, zm), H), O.v((w, y, zm), H), O.v((w * .96, y, zt - .28 * w), H), O.v((0, y, zt), H)]
    U = [kv(*s) for s in verh]
    for r0, r1 in zip(U, U[1:]):
        for j in range(3): O.f(r0[j], r1[j], r1[j + 1]); O.f(r0[j], r1[j + 1], r0[j + 1])
    t_ = U[-1]; O.f(t_[0], t_[1], t_[2]); O.f(t_[0], t_[2], t_[3])
    if niz:
        def kj(y, w, zv, zn): return [O.v((0, y, zn), H), O.v((w * .8, y, zn + .004), H), O.v((w, y, zv), H), O.v((0, y, zv), H)]
        J = [kj(*s) for s in niz]
        for r0, r1 in zip(J, J[1:]):
            for j in range(3): O.f(r0[j], r0[j + 1], r1[j + 1]); O.f(r0[j], r1[j + 1], r1[j])
        t_ = J[-1]; O.f(t_[0], t_[2], t_[1]); O.f(t_[0], t_[3], t_[2])
    for x, y, z0, dl, r in zuby:
        b = [O.v((x - r, y + r * .8, z0), H), O.v((x + r, y + r * .8, z0), H), O.v((x, y - r, z0), H)]; O.konec(b, O.v((x, y, z0 + dl), H))


@organ('Пасть@Лось')
def morda_los():
    O = Org('Пасть@Лось')
    morda(O, [(-.085, .060, 1.728, 1.668), (-.155, .058, 1.712, 1.650), (-.225, .056, 1.690, 1.632), (-.262, .046, 1.668, 1.628)], [(-.095, .054, 1.650, 1.618), (-.175, .046, 1.640, 1.618), (-.235, .036, 1.626, 1.612)])
    # ноздри: вдавленные клинья на торце
    for x_, y_ in ((.026, -.264),):
        a_ = O.v((x_ - .012, y_, 1.666), 'голова'); b_ = O.v((x_ + .012, y_, 1.660), 'голова'); c_ = O.v((x_, y_, 1.640), 'голова'); d_ = O.v((x_, y_ + .012, 1.655), 'голова')
        for q in ((a_, b_, d_), (b_, c_, d_), (c_, a_, d_)): O.f(*q)
    return O


def uho(O, x, z, dl_x, dl_z, sh, naz=0.0, H='голова'):
    """лист уха: основание на коже, кончик вверх и в сторону, вогнут"""
    p, _ = kozha((.4, -.02, z), (-1, 0, 0)); kor = p - Vector((.006, 0, 0)); tip = kor + Vector((dl_x, naz, dl_z)); d = (tip - kor)
    bk = Vector((0, 1, 0)).cross(d).normalized(); a_ = O.v(kor + Vector((0, -sh * .6, 0)), H); b_ = O.v(kor + Vector((0, sh * .6, 0)), H)
    m1 = O.v(kor + d * .45 + Vector((0, -sh, 0)) + bk * .006, H); m2 = O.v(kor + d * .45 + Vector((0, sh, 0)) + bk * .006, H); c_ = O.v(kor + d * .4 - bk * .010, H); t_ = O.v(tip, H)
    for q in ((a_, c_, m1), (m1, c_, t_), (c_, m2, t_), (c_, b_, m2), (a_, b_, c_), (a_, m1, t_), (m1, t_, m2), (t_, m2, b_)): O.f(*q)


@organ('Чутьё@Лось')
def ushi_los():
    O = Org('Чутьё@Лось'); uho(O, 0, 1.76, .155, .095, .032, .02); return O


@organ('Чутьё@Ёж')
def ushi_ezh():
    O = Org('Чутьё@Ёж'); H = 'голова'; p, _ = kozha((.4, -.015, 1.725), (-1, 0, 0)); c0 = p + Vector((.004, 0, 0)); r = .022
    k0 = [O.v(c0 + Vector((.006 + .010 * math.cos(u) * .3, r * math.cos(u), r * math.sin(u))), H) for u in [j * math.pi / 3 for j in range(6)]]
    ts = O.v(c0 + Vector((-.002, 0, 0)), H); nar = O.v(c0 + Vector((.014, .006, 0)), H)
    for j in range(6):
        k = (j + 1) % 6; O.f(k0[j], k0[k], ts); O.f(k0[k], k0[j], nar)
    return O


def bruski_palcev(O, K='предплечье'):
    for tip, c in konchiki():
        d = (tip - c).normalized(); L_ = (tip - c).length * 2 + .01; koren = tip - d * L_
        s1 = Vector((1, 0, 0)); s1 = (s1 - d * s1.dot(d)).normalized(); s2 = d.cross(s1).normalized(); Ring = []
        for t, r in ((0, .017), (.38, .0135), (.72, .0150), (1.0, .0115)):
            c_ = koren + d * (L_ * t); Ring.append([O.v(c_ + s1 * (r * a_) + s2 * (r * b_), K) for a_, b_ in ((1, 1), (-1, 1), (-1, -1), (1, -1))])
        O.lofta(Ring, nachalo=False, konec=False); O.konec(Ring[-1], O.v(tip + d * .012, K))


@organ('Руки@Лось')
def kapsula():
    O = Org('Руки@Лось'); bruski_palcev(O); return O


@organ('Руки@Ёж')
def kogti_ezh():
    O = Org('Руки@Ёж'); K = 'предплечье'
    for tip, c in konchiki():
        d = (tip - c).normalized(); pr = Vector((-1, 0, 0)); s1 = pr.cross(d).normalized(); s2 = d.cross(s1).normalized(); st = tip - d * .008
        k0 = [O.v(st + s1 * (.010 * math.cos(u)) + s2 * (.009 * math.sin(u)), K) for u in (0, 2.09, 4.19)]; mid = tip + d * .012 + pr * .008
        k1 = [O.v(mid + s1 * (.006 * math.cos(u)) + s2 * (.005 * math.sin(u)), K) for u in (0, 2.09, 4.19)]
        for j in range(3):
            k = (j + 1) % 3; O.f(k0[j], k0[k], k1[k]); O.f(k0[j], k1[k], k1[j])
        O.konec(k1, O.v(tip + d * .022 + pr * .026, K))
    return O


@organ('Пасть@Ёж')
def morda_ezh():
    O = Org('Пасть@Ёж'); morda(O, [(-.085, .060, 1.724, 1.660), (-.145, .042, 1.708, 1.660), (-.205, .020, 1.686, 1.660), (-.232, .010, 1.674, 1.660)], [(-.095, .050, 1.652, 1.626), (-.160, .026, 1.650, 1.640)]); return O


@organ('Пасть@Змея')
def morda_zmeya():
    O = Org('Пасть@Змея'); morda(O, [(-.085, .074, 1.732, 1.664), (-.150, .076, 1.718, 1.662), (-.200, .066, 1.704, 1.660), (-.226, .040, 1.692, 1.660)], [(-.095, .066, 1.650, 1.626), (-.180, .056, 1.650, 1.634)], zuby=[(.036, -.206, 1.664, -.052, .008)]); return O


@organ('Чутьё@Змея')
def glaz_zmeya():
    O = Org('Чутьё@Змея'); H = 'голова'
    def diskom(c, n, r1, r2, h, sides=6):
        s1 = n.cross(Vector((0, 0, 1))).normalized(); s2 = n.cross(s1).normalized(); kr = [O.v(c + s1 * (r1 * math.cos(u)) + s2 * (r2 * math.sin(u)) + n * .001, H) for u in [j * 2 * math.pi / sides for j in range(sides)]]; O.konec(kr, O.v(c + n * h, H))
    p, n = kozha((.03, -.5, 1.735), (0, 1, 0)); diskom(p, n, .017, .012, .009)   # глаз с подъёмом вместо глазницы
    p, n = kozha((.022, -.5, 1.70), (0, 1, 0)); diskom(p, n, .012, .009, -.004, 5)   # ямка: вдавлена
    # бровь-козырёк над глазом
    p1, _ = kozha((.012, -.5, 1.752), (0, 1, 0)); p2, _ = kozha((.05, -.5, 1.758), (0, 1, 0)); c_ = p1 + Vector((0, -.012, -.004)); d_ = p2 + Vector((0, -.012, -.004)); e_ = (p1 + p2) / 2 + Vector((0, 0, .012))
    for q in ((O.v(p1, H), O.v(p2, H), O.v(d_, H)), (O.v(p1, H), O.v(d_, H), O.v(c_, H)), (O.v(p1, H), O.v(e_, H), O.v(p2, H))): O.f(*q)
    return O


def plastina(O, x, z, r_x, r_z):
    if BVH.ray_cast(Vector((x, .4, z)), Vector((0, -1, 0)))[0] is None: return
    p, n = kozha((x, .4, z), (0, -1, 0))
    if abs(n.y) < .3: n = Vector((0, 1, 0))
    bk = Vector((0, 0, 1)).cross(n).normalized(); vn = n.cross(bk).normalized(); vn = vn if vn.z > 0 else -vn; bk = bk if bk.x > 0 else -bk; lf = .014
    a_ = O.v(p + vn * r_z + n * .003); b_ = O.v(p + bk * r_x + n * lf); c_ = O.v(p - vn * r_z + n * (lf + .004)); d_ = O.v(p - bk * r_x + n * lf); m_ = O.v(p + n * (lf + .006))
    if abs(x) < 1e-4:
        for q in ((a_, b_, m_), (b_, c_, m_)): O.f(*q)
    else:
        for q in ((a_, b_, m_), (b_, c_, m_), (c_, d_, m_), (d_, a_, m_)): O.f(*q)


@organ('Шкура@Змея')
def cheshuya():
    O = Org('Шкура@Змея')
    for ry, (z, xs, r) in enumerate(((1.47, (0, .11, .22), .062), (1.37, (.055, .165), .060), (1.27, (0, .11), .058), (1.17, (.055,), .050))):
        for x in xs: plastina(O, x, z, r, r * .95)
    return O


@organ('Хвост@Змея')
def hvost_zmeya():
    O = Org('Хвост@Змея'); K = 'таз'; Sec = [(.0, .185, .96, .060), (.09, .30, .79, .060), (.21, .44, .58, .050), (.35, .57, .40, .042), (.52, .69, .25, .034), (.72, .78, .14, .026), (.92, .84, .09, .020)]; Ring = []
    def kol(x, y, z, r, dv):
        d = Vector(dv).normalized(); s1 = d.cross(Vector((0, 0, 1))); s1 = s1.normalized() if s1.length > 1e-3 else Vector((1, 0, 0)); s2 = d.cross(s1).normalized()
        return [O.v(Vector((x, y, z)) + s1 * (r * math.cos(u)) + s2 * (r * .85 * math.sin(u)), K) for u in [j * 2 * math.pi / 5 for j in range(5)]]
    for j, s in enumerate(Sec):
        pr = Sec[max(j - 1, 0)]; sl = Sec[min(j + 1, len(Sec) - 1)]; Ring.append(kol(s[0], s[1], s[2], s[3], (sl[0] - pr[0], sl[1] - pr[1], sl[2] - pr[2])))
    # погремушка: кольца ступенями вверх
    d = Vector((.75, .25, .30)).normalized(); e = Vector(Sec[-1][:3]); r = .030
    for j in range(4):
        c0 = e + d * (.045 * j + .015); Ring.append(kol(c0.x, c0.y, c0.z, r - .004 * j, tuple(d))); c1 = e + d * (.045 * j + .045); Ring.append(kol(c1.x, c1.y, c1.z, (r - .004 * j) * .72, tuple(d)))
    O.lofta(Ring, nachalo=True, konec=False); O.konec(Ring[-1], O.v(e + d * .22, K)); return O


@organ('Шкура@Лось')
def gorb():
    """холка и горб: брусья ступенями по хребту, шея → лопатки; ширина к середине"""
    O = Org('Шкура@Лось')
    St = [(1.60, .016, .024), (1.52, .030, .032), (1.44, .044, .040), (1.36, .052, .044), (1.28, .044, .036), (1.20, .026, .024)]
    for j, (z, w, h) in enumerate(St):
        p, n = kozha((0, .5, z), (0, -1, 0)); zn = z - .10
        q, _ = kozha((0, .5, zn), (0, -1, 0)); y0 = p.y; y1 = q.y
        kv = [O.v((0, y0 - .012, z + .005)), O.v((w, y0 - .012, z - .003)), O.v((w, y1 - .006, zn + .018)), O.v((0, y1 - .006, zn + .018))]
        tp = [O.v((0, y0 + h, z - .008)), O.v((w * .7, y0 + h * .7, z - .012)), O.v((w * .8, y1 + h * .55, zn + .012)), O.v((0, y1 + h * .8, zn + .016))]
        O.f(kv[0], kv[1], tp[1]); O.f(kv[0], tp[1], tp[0]); O.f(kv[1], kv[2], tp[2]); O.f(kv[1], tp[2], tp[1]); O.f(tp[0], tp[1], tp[2]); O.f(tp[0], tp[2], tp[3]); O.f(tp[3], tp[2], kv[2]); O.f(tp[3], kv[2], kv[3])
    return O


@organ('Шкура@Ёж')
def igly_ezh():
    O = Org('Шкура@Ёж')
    Ry = [(1.62, (0, .04), .10, .022), (1.54, (.03, .09), .11, .024), (1.47, (0, .07, .14, .20), .12, .026), (1.39, (.04, .11, .18, .245), .13, .027), (1.31, (0, .08, .15, .21), .13, .027), (1.23, (.04, .11, .17), .13, .026), (1.15, (0, .08, .135), .12, .025), (1.07, (.045, .11), .11, .023)]
    for z, xs, dl, sh in Ry:
        for x in xs: list_sherst(O, x, z, dl, sh, .12 + x * 1.3)
    return O


@organ('Игломёт@Ёж')
def iglomet():
    """трубы вдвое короче листа, основание на загривке, вверх-вперёд 60° к горизонту, назад не смотрят; держатся сбоку от черепа"""
    O = Org('Игломёт@Ёж'); d = Vector((0, -.5, .866)); K = 'грудная клетка'; dl = .38; s1 = Vector((1, 0, 0)); s2 = d.cross(s1).normalized(); r = .020
    for x in (.085, .130):
        p0, _ = kozha((x, .4, 1.47), (0, -1, 0)); c0 = p0 - d * .03; c1 = c0 + d * dl
        def kol(c, q): return [O.v(c + s1 * (q * math.cos(u)) + s2 * (q * math.sin(u)), K) for u in [j * 2 * math.pi / 5 + .3 for j in range(5)]]
        k0 = kol(c0, r); k1 = kol(c1, r); k3 = kol(c1, r * .6); dn_ = O.v(c1 - d * .05, K)
        O.lofta([k0, k1], nachalo=False, konec=False)
        for j in range(5):
            k = (j + 1) % 5; O.f(k1[j], k1[k], k3[k]); O.f(k1[j], k3[k], k3[j])
        O.konec(k3, dn_)
    return O


res = {}
for im, fn in Sob.items():
    if TOLKO and im not in TOLKO: continue
    O = fn(); ob = O.sobrat(zerkalo=not im.startswith('Хвост')); ob.data.calc_loop_triangles(); res[im] = len(ob.data.loop_triangles); print('ОРГАН %s: %d тр' % (im, res[im]))
if opt.get('otchet'): json.dump(res, open(opt['otchet'], 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
bpy.ops.wm.save_as_mainfile(filepath=OUT)
