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


def prizma(O, kontur, y_ot, tol, kost):
    """плита с видимой толщиной: контур (u, z) заполнен спереди и сзади, боковая полоса — толщина"""
    n = len(kontur); E = [(j, (j + 1) % n) for j in range(n)]
    cdt = delaunay_2d_cdt([Vector(p) for p in kontur], E, [], 0, 1e-6); Vo, Fo, Oo = cdt[0], cdt[2], cdt[3]; orig = [q[0] for q in Oo]; PP = np.array(kontur)
    def vnutri(c):
        x, z = c; f_ = False
        for j in range(n):
            a_, b_ = PP[j], PP[(j + 1) % n]
            if (a_[1] > z) != (b_[1] > z) and x < a_[0] + (z - a_[1]) / (b_[1] - a_[1]) * (b_[0] - a_[0]): f_ = not f_
        return f_
    tri = [t for t in Fo if vnutri(tuple(sum((Vector(Vo[q]) for q in t), Vector((0, 0))) / 3))]
    I = {}
    for sg in (-1, 1):
        I[sg] = [O.v((u, y_ot(u, z) + sg * tol, z), kost) for u, z in kontur]
        for t in tri:
            q = [I[sg][orig[x]] for x in t]; O.f(*(q if sg > 0 else q[::-1]))
    for j in range(n):
        k = (j + 1) % n; O.f(I[1][j], I[1][k], I[-1][k]); O.f(I[1][j], I[-1][k], I[-1][j])


@organ('Рога@Лось')
def roga():
    """короткий стержень от головы, толстая лопата, 6 тупых отростков по внешнему краю; толщина видна"""
    O = Org('Рога@Лось'); X = .070
    k = [(0, 1.772), (.08, 1.762), (.20, 1.776), (.33, 1.79), (.44, 1.815), (.545, 1.858), (.545, 1.888), (.455, 1.885), (.525, 1.96), (.505, 1.985), (.415, 1.925), (.445, 2.05), (.415, 2.062), (.345, 1.95), (.362, 2.082), (.332, 2.082), (.272, 1.955), (.272, 2.052), (.245, 2.052), (.192, 1.94), (.172, 2.0), (.148, 1.99), (.112, 1.862), (.05, 1.83), (0, 1.828)]
    prizma(O, [(X + u, z) for u, z in k], lambda u, z: -.03 - .10 * (u - X) + .08 * (z - 1.8), .016, 'голова'); return O


def morda2(O, verh, niz, zuby=(), H='голова', nos=None):
    """морда единым клином от лица: сечения верха — точки половины от хребта носа вниз к линии рта (x, y, z), последняя на оси; низ — челюсть отдельной массой"""
    def kol(r): return [O.v(p, H) for p in r]
    U = [kol(r) for r in verh]
    for r0, r1 in zip(U, U[1:]):
        for j in range(len(r0) - 1): O.f(r0[j], r1[j], r1[j + 1]); O.f(r0[j], r1[j + 1], r0[j + 1])
    t_ = U[-1]
    for j in range(1, len(t_) - 1): O.f(t_[0], t_[j], t_[j + 1])
    if niz:
        J = [kol(r) for r in niz]
        for r0, r1 in zip(J, J[1:]):
            for j in range(len(r0) - 1): O.f(r0[j], r1[j], r1[j + 1]); O.f(r0[j], r1[j + 1], r0[j + 1])
        t_ = J[-1]
        for j in range(1, len(t_) - 1): O.f(t_[0], t_[j], t_[j + 1])
    for x, y, z0, dl, r in zuby:
        b = [O.v((x - r, y + r * .8, z0), H), O.v((x + r, y + r * .8, z0), H), O.v((x, y - r, z0), H)]; O.konec(b, O.v((x, y, z0 + dl), H))


def kor_lico(x, z, vgl=.008):
    """точка на лице для корня морды: чуть внутри кожи"""
    for k_ in range(14):   # точка шире лица берёт глубину с края кожи
        if BVH.ray_cast(Vector((x - k_ * .004, -.5, z)), Vector((0, 1, 0)))[0] is not None: break
    p, _ = kozha((x - k_ * .004, -.5, z), (0, 1, 0)); return (x, p.y + vgl, z)


@organ('Пасть@Волк')
def morda_volk():
    O = Org('Пасть@Волк')
    # корень — от переносицы и скул, ширина лица; хребет носа прямой к мочке
    r0 = [kor_lico(0, 1.728), kor_lico(.020, 1.722), kor_lico(.058, 1.694), kor_lico(.062, 1.652), kor_lico(0, 1.652)]
    r1 = [(0, -.176, 1.708), (.014, -.172, 1.700), (.032, -.160, 1.682), (.034, -.156, 1.650), (0, -.168, 1.650)]
    r2 = [(0, -.226, 1.682), (.010, -.222, 1.678), (.017, -.216, 1.668), (.018, -.214, 1.655), (0, -.226, 1.655)]
    j0 = [kor_lico(0, 1.640, .006), kor_lico(.052, 1.645, .006), kor_lico(.050, 1.620, .004), (0, -.095, 1.606)]
    j0[0] = (0, -.150, 1.650); j0[3] = (0, -.112, 1.600); j1 = [(0, -.190, 1.646), (.026, -.176, 1.647), (.024, -.166, 1.628), (0, -.176, 1.618)]; j2 = [(0, -.208, 1.642), (.014, -.202, 1.643), (.011, -.198, 1.634), (0, -.204, 1.628)]
    morda2(O, [r0, r1, r2], [j0, j1, j2], zuby=[(.019, -.206, 1.653, -.016, .005), (.030, -.170, 1.650, -.010, .005)]); return O


@organ('Пасть@Лось')
def morda_los():
    O = Org('Пасть@Лось')
    r0 = [kor_lico(0, 1.730), kor_lico(.022, 1.722), kor_lico(.060, 1.690), kor_lico(.060, 1.640), kor_lico(0, 1.640)]
    r1 = [(0, -.195, 1.712), (.026, -.188, 1.702), (.052, -.172, 1.672), (.054, -.170, 1.622), (0, -.182, 1.622)]
    r2 = [(0, -.262, 1.682), (.032, -.258, 1.672), (.056, -.244, 1.640), (.056, -.236, 1.594), (0, -.246, 1.590)]
    r3 = [(0, -.296, 1.646), (.024, -.292, 1.640), (.040, -.280, 1.618), (.040, -.268, 1.590), (0, -.276, 1.584)]
    j0 = [(0, -.150, 1.616), (.048, -.120, 1.620), (.044, -.110, 1.596), (0, -.118, 1.584)]; j1 = [(0, -.215, 1.606), (.036, -.195, 1.608), (.032, -.185, 1.590), (0, -.200, 1.580)]; j2 = [(0, -.235, 1.600), (.020, -.228, 1.600), (.016, -.222, 1.590), (0, -.228, 1.584)]
    morda2(O, [r0, r1, r2, r3], [j0, j1, j2])
    a_ = O.v((.012, -.297, 1.635), 'голова'); b_ = O.v((.034, -.284, 1.630), 'голова'); c_ = O.v((.024, -.285, 1.604), 'голова'); d_ = O.v((.022, -.270, 1.622), 'голова')
    for q in ((a_, b_, d_), (b_, c_, d_), (c_, a_, d_)): O.f(*q)
    return O


@organ('Пасть@Ёж')
def morda_ezh():
    O = Org('Пасть@Ёж')
    r0 = [kor_lico(0, 1.722), kor_lico(.020, 1.716), kor_lico(.056, 1.690), kor_lico(.058, 1.652), kor_lico(0, 1.652)]
    r1 = [(0, -.170, 1.694), (.012, -.166, 1.688), (.028, -.156, 1.674), (.030, -.150, 1.654), (0, -.162, 1.654)]
    r2 = [(0, -.214, 1.668), (.007, -.212, 1.665), (.011, -.208, 1.659), (.011, -.204, 1.655), (0, -.210, 1.655)]
    j0 = [(0, -.142, 1.645), (.046, -.105, 1.646), (.044, -.098, 1.622), (0, -.110, 1.604)]; j1 = [(0, -.178, 1.649), (.016, -.170, 1.648), (.014, -.162, 1.638), (0, -.170, 1.630)]
    morda2(O, [r0, r1, r2], [j0, j1]); return O


@organ('Пасть@Змея')
def morda_zmeya():
    O = Org('Пасть@Змея')
    r0 = [kor_lico(0, 1.728), kor_lico(.022, 1.722), kor_lico(.060, 1.694), kor_lico(.068, 1.650), kor_lico(0, 1.650)]
    r1 = [(0, -.175, 1.704), (.022, -.172, 1.698), (.062, -.158, 1.676), (.074, -.152, 1.650), (0, -.168, 1.650)]
    r2 = [(0, -.222, 1.676), (.022, -.220, 1.672), (.052, -.208, 1.660), (.058, -.200, 1.650), (0, -.220, 1.650)]
    j0 = [(0, -.142, 1.640), (.062, -.100, 1.642), (.058, -.095, 1.618), (0, -.110, 1.602)]; j1 = [(0, -.195, 1.643), (.052, -.170, 1.642), (.046, -.160, 1.628), (0, -.180, 1.622)]
    morda2(O, [r0, r1, r2], [j0, j1], zuby=[(.038, -.192, 1.650, -.062, .009)]); return O


@organ('Чутьё@Волк')
def ushi_volk():
    """вдвое короче, широкое основание, внутренняя плоскость к лицу"""
    O = Org('Чутьё@Волк'); H = 'голова'; ver = O.v((.078, -.012, 1.945), H); Tk = []
    for x, y, z in ((.026, -.034, 1.86), (.078, -.034, 1.775), (.078, .018, 1.775), (.026, .018, 1.86)):
        s_ = Vector((x * 4, y, z + .30)); p, _ = kozha(s_, Vector((0, y, 1.75)) - s_); Tk.append(O.v(p - Vector((.004, 0, .004)), H))
    a_, b_, c_, d_ = Tk; h = O.v((.058, -.012, 1.845), H)
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


def kogti(im, dl, zagib, r):
    O = Org(im); K = 'предплечье'
    for tip, c in konchiki():
        d = (tip - c).normalized(); pr = Vector((-1, 0, 0)); s1 = pr.cross(d).normalized(); s2 = d.cross(s1).normalized(); st = tip - d * .012
        k0 = [O.v(st + s1 * (r * math.cos(u)) + s2 * (r * .9 * math.sin(u)), K) for u in (0, 2.09, 4.19)]; mid = tip + d * (dl * .45) + pr * (zagib * .3)
        k1 = [O.v(mid + s1 * (r * .6 * math.cos(u)) + s2 * (r * .55 * math.sin(u)), K) for u in (0, 2.09, 4.19)]
        for j in range(3):
            k = (j + 1) % 3; O.f(k0[j], k0[k], k1[k]); O.f(k0[j], k1[k], k1[j])
        O.konec(k1, O.v(tip + d * dl + pr * zagib, K))
    return O


@organ('Руки@Волк')
def kogti_volk(): return kogti('Руки@Волк', .047, .021, .012)
@organ('Руки@Ёж')
def kogti_ezh(): return kogti('Руки@Ёж', .034, .034, .013)


def list_sherst(O, x, z, dl, sh, nakl, kost=None, lift=.012):
    """лист: корень на коже, кончик по направлению nakl (0 — вниз, к π/2 — наружу), ребро приподнято; нормаль от оси спины"""
    p0 = Vector((x, .4, z)); d = Vector((0, -1, 0))
    if BVH.ray_cast(p0, d)[0] is None: return
    p, n = kozha(p0, d); n = Vector((min(abs(x) / .25, 1) * .7, .75, .15)).normalized()
    vn = Vector((math.sin(nakl), 0, -math.cos(nakl))); vn = (vn - n * vn.dot(n)).normalized(); bk = n.cross(vn).normalized()
    if bk.x < 0: bk = -bk
    koren = O.v(p - n * .006 - vn * .01, kost); l = O.v(p + vn * (dl * .4) + bk * sh + n * lift, kost); r = O.v(p + vn * (dl * .4) - bk * sh + n * lift, kost)
    gr = O.v(p + vn * (dl * .45) + n * (lift + sh * .3), kost); kon = O.v(p + vn * dl + n * (lift * .8), kost)
    for q in (((koren, gr, l), (l, gr, kon)) if abs(x) < 1e-4 else ((koren, gr, l), (koren, r, gr), (l, gr, kon), (gr, r, kon))): O.f(*q)


@organ('Шкура@Волк')
def griva():
    """клинья веером от основания шеи на загривок и плечи; два ряда, кончики наружу за контур плеч"""
    O = Org('Шкура@Волк')
    for r, dl, sh, ug in ((.12, .16, .030, (0, .35, .70, 1.05)), (.215, .18, .032, (.17, .50, .82, 1.12)), (.31, .17, .030, (0, .30, .58, .84))):
        for th in ug:
            list_sherst(O, r * math.sin(th), 1.60 - r * math.cos(th), dl, sh, th, lift=.020)
    return O


def uho_list(O, dl_x, dl_z, sh, z0=1.745, H='голова'):
    """лист уха плашмя к лицу, ширина треть длины; две стороны раздельно, чтобы свет шёл по одной стороне"""
    p, _ = kozha((.4, -.02, z0), (-1, 0, 0)); kor = p - Vector((.005, 0, 0)); d = Vector((dl_x, 0, dl_z)); bk = Vector((-dl_z, 0, dl_x)).normalized(); q = Vector((0, 1, 0))
    T = [kor + bk * (sh * .45), kor + d * .45 + bk * sh, kor + d, kor + d * .45 - bk * sh, kor - bk * (sh * .45)]
    pr = [O.v(t_ - q * .004, H) for t_ in T]; za = [O.v(t_ + q * .004, H) for t_ in T]; c_ = O.v(kor + d * .42 - q * .012, H); c2 = O.v(kor + d * .42 + q * .012, H)
    for j in range(4):
        O.f(pr[j], pr[j + 1], c_); O.f(za[j + 1], za[j], c2); O.f(pr[j], za[j], za[j + 1]); O.f(pr[j], za[j + 1], pr[j + 1])
    O.f(pr[4], pr[0], c_); O.f(za[0], za[4], c2)


@organ('Чутьё@Лось')
def ushi_los():
    O = Org('Чутьё@Лось'); uho_list(O, .135, .095, .033); return O


@organ('Чутьё@Ёж')
def ushi_ezh():
    O = Org('Чутьё@Ёж'); H = 'голова'; p, _ = kozha((.4, -.015, 1.725), (-1, 0, 0)); c0 = p + Vector((.004, 0, 0)); r = .022
    k0 = [O.v(c0 + Vector((.006 + .010 * math.cos(u) * .3, r * math.cos(u), r * math.sin(u))), H) for u in [j * math.pi / 3 for j in range(6)]]
    ts = O.v(c0 + Vector((-.002, 0, 0)), H); nar = O.v(c0 + Vector((.014, .006, 0)), H)
    for j in range(6):
        k = (j + 1) % 6; O.f(k0[j], k0[k], ts); O.f(k0[k], k0[j], nar)
    return O


@organ('Руки@Лось')
def kapsula():
    """броня крупнее пальцев: два сегмента на палец, 78 граней на кисть"""
    O = Org('Руки@Лось'); K = 'предплечье'
    for tip, c in konchiki():
        d = (tip - c).normalized(); L_ = (tip - c).length * 2 + .012; koren = tip - d * L_; s1 = Vector((1, 0, 0)); s1 = (s1 - d * s1.dot(d)).normalized(); s2 = d.cross(s1).normalized(); Ring = []
        for t, r in ((0, .023), (.52, .021), (1.0, .011)):
            c_ = koren + d * (L_ * t); Ring.append([O.v(c_ + s1 * (r * a_) + s2 * (r * b_), K) for a_, b_ in ((1, 1), (-1, 1), (-1, -1), (1, -1))])
        O.lofta(Ring, nachalo=False, konec=False)
    return O


@organ('Чутьё@Змея')
def glaz_zmeya():
    O = Org('Чутьё@Змея'); H = 'голова'
    def diskom(c, n, r1, r2, h, sides=6):
        s1 = n.cross(Vector((0, 0, 1))).normalized(); s2 = n.cross(s1).normalized(); kr = [O.v(c + s1 * (r1 * math.cos(u)) + s2 * (r2 * math.sin(u)) + n * .002, H) for u in [j * 2 * math.pi / sides for j in range(sides)]]; O.konec(kr, O.v(c + n * h, H))
    p, n = kozha((.032, -.5, 1.733), (0, 1, 0)); diskom(p, Vector((.3, -.95, 0)).normalized(), .025, .017, .012)
    p, n = kozha((.028, -.5, 1.695), (0, 1, 0)); diskom(p, Vector((.3, -.95, 0)).normalized(), .017, .013, -.006, 5)
    p1, _ = kozha((.010, -.5, 1.752), (0, 1, 0)); p2, _ = kozha((.056, -.5, 1.758), (0, 1, 0)); c_ = p1 + Vector((0, -.014, -.004)); d_ = p2 + Vector((0, -.014, -.004)); e_ = (p1 + p2) / 2 + Vector((0, 0, .014))
    for q in ((O.v(p1, H), O.v(p2, H), O.v(d_, H)), (O.v(p1, H), O.v(d_, H), O.v(c_, H)), (O.v(p1, H), O.v(e_, H), O.v(p2, H))): O.f(*q)
    return O


def plastina(O, x, z, r_x, r_z):
    """плита с толщиной: верх лежит над кожей, боковые стенки уходят под неё"""
    if BVH.ray_cast(Vector((x, .4, z)), Vector((0, -1, 0)))[0] is None: return
    p, _ = kozha((x, .4, z), (0, -1, 0)); n = Vector((min(abs(x) / .25, 1) * .6, .8, .1)).normalized()
    bk = Vector((0, 0, 1)).cross(n).normalized(); bk = bk if bk.x > 0 else -bk; vn = n.cross(bk).normalized(); vn = vn if vn.z > 0 else -vn; v_ = .014
    T = [p + vn * r_z + n * .006, p + bk * r_x + n * v_, p - vn * r_z + n * (v_ + .005), p - bk * r_x + n * v_]; B = [t - n * .03 for t in T]; m_ = O.v(p + n * (v_ + .004))
    Ti = [O.v(t) for t in T]; Bi = [O.v(t) for t in B]; Z = (0, 1, 2) if abs(x) < 1e-4 else (0, 1, 2, 3)
    for j in Z:
        k = (j + 1) % 4; O.f(Ti[j], Ti[k], m_); O.f(Ti[k], Ti[j], Bi[j]); O.f(Ti[k], Bi[j], Bi[k])


@organ('Шкура@Змея')
def cheshuya():
    O = Org('Шкура@Змея')
    for z, xs, r in ((1.50, (.06, .17), .068), (1.40, (0, .115, .225), .068), (1.29, (.06, .17), .064), (1.18, (0, .11), .056)):
        for x in xs: plastina(O, x, z, r, r * 1.0)
    return O


@organ('Хвост@Змея')
def hvost_zmeya():
    O = Org('Хвост@Змея'); K = 'таз'; Sec = [(.0, .10, 1.00, .075), (.0, .19, .94, .095), (.12, .32, .78, .090), (.30, .44, .57, .076), (.52, .55, .39, .062), (.78, .64, .25, .048), (1.04, .70, .14, .036), (1.26, .74, .09, .027)]; Ring = []
    def kol(x, y, z, r, dv):
        d = Vector(dv).normalized(); s1 = d.cross(Vector((0, 0, 1))); s1 = s1.normalized() if s1.length > 1e-3 else Vector((1, 0, 0)); s2 = d.cross(s1).normalized()
        return [O.v(Vector((x, y, z)) + s1 * (r * math.cos(u)) + s2 * (r * .85 * math.sin(u)), K) for u in [j * 2 * math.pi / 5 for j in range(5)]]
    for j, s in enumerate(Sec):
        pr = Sec[max(j - 1, 0)]; sl = Sec[min(j + 1, len(Sec) - 1)]; Ring.append(kol(s[0], s[1], s[2], s[3], (sl[0] - pr[0], sl[1] - pr[1], sl[2] - pr[2])))
    d = Vector((.86, .10, .50)).normalized(); e = Vector(Sec[-1][:3]); r = .040
    for j in range(4):
        c0 = e + d * (.05 * j + .015); Ring.append(kol(c0.x, c0.y, c0.z, r - .005 * j, tuple(d))); c1 = e + d * (.05 * j + .05); Ring.append(kol(c1.x, c1.y, c1.z, (r - .005 * j) * .72, tuple(d)))
    O.lofta(Ring, nachalo=True, konec=False); O.konec(Ring[-1], O.v(e + d * .24, K)); return O


@organ('Шкура@Лось')
def gorb():
    """цельная масса холки: от основания шеи до лопаток; сечения общие, гребень одной линией с двумя уступами"""
    O = Org('Шкура@Лось'); Se = [(1.61, .020, .022), (1.555, .055, .085), (1.54, .060, .060), (1.475, .090, .130), (1.46, .095, .100), (1.395, .112, .165), (1.38, .115, .130), (1.31, .105, .125), (1.21, .070, .055), (1.14, .030, .008)]; Ko = []
    for z, w, h in Se:
        y0 = kozha((0, .5, z), (0, -1, 0))[0].y; y1 = kozha((w * .55, .5, z), (0, -1, 0))[0].y if BVH.ray_cast(Vector((w * .55, .5, z)), Vector((0, -1, 0)))[0] else y0; y2 = kozha((w, .5, z), (0, -1, 0))[0].y if BVH.ray_cast(Vector((w, .5, z)), Vector((0, -1, 0)))[0] else y0 - .03
        Ko.append([O.v((0, y0 + h, z)), O.v((w * .55, y1 + h * .72, z - .006)), O.v((w, y2 - .004, z - .012))])
    for r0, r1 in zip(Ko, Ko[1:]):
        for j in range(2): O.f(r0[j], r0[j + 1], r1[j + 1]); O.f(r0[j], r1[j + 1], r1[j])
    return O


@organ('Шкура@Ёж')
def igly_ezh():
    """ровные ряды одинаковых клиньев внахлёст: сетка по рядам со сдвигом на полшага, один размер, вниз; край ряда — наружу, контур плеч"""
    O = Org('Шкура@Ёж'); shag = .062; dl = .135; sh = .028
    for j, (z, xm) in enumerate(((1.60, .02), (1.53, .08), (1.46, .25), (1.39, .29), (1.32, .26), (1.25, .21), (1.18, .17), (1.11, .15), (1.04, .13))):
        x = 0 if j % 2 == 0 else shag / 2
        while x <= xm + 1e-4:
            k_ = x / max(xm, .05); list_sherst(O, x, z, dl, sh, .08 + (k_ ** 3) * .8, lift=.016); x += shag
    return O


@organ('Игломёт@Ёж')
def iglomet():
    """четыре гранёные полые трубы рядом над черепом, 48° к горизонту, в профиль концы над лбом и перед ним; низ труб на упоре, упор стоит на загривке. Линия проходит над сводом и затылком с запасом"""
    O = Org('Игломёт@Ёж'); ug = math.radians(48); d = Vector((0, -math.cos(ug), math.sin(ug))); K = 'грудная клетка'; s1 = Vector((1, 0, 0)); s2 = d.cross(s1).normalized(); r = .021; Y0 = .13; Z0 = 1.912 + (-.03 - Y0) * math.tan(ug); dl = (Y0 + .15) / math.cos(ug); h_min = 9
    for x in (.025, .075):
        c0 = Vector((x, Y0, Z0)); c1 = c0 + d * dl
        for t_ in np.linspace(0, 1, 40):
            h_ = BVH.find_nearest(c0 + d * (dl * t_)); h_min = min(h_min, h_[3] - r)
        def kol(c, q): return [O.v(c + s1 * (q * math.cos(u)) + s2 * (q * math.sin(u)), K) for u in [j * math.pi / 3 + .52 for j in range(6)]]
        k0 = kol(c0, r); k1 = kol(c1, r); k3 = kol(c1, r * .68); dn_ = O.v(c1 - d * .06, K)
        O.lofta([k0, k1], nachalo=True, konec=False)
        for j in range(6):
            k = (j + 1) % 6; O.f(k1[j], k1[k], k3[k]); O.f(k1[j], k3[k], k3[j])
        O.konec(k3, dn_)
    # упор: от загривка к низу труб
    p0, _ = kozha((.03, .4, 1.545), (0, -1, 0)); w = .085; yt = Y0 - .05; zt = Z0 - .03
    N = [O.v((0, p0.y - .02, 1.53), K), O.v((w, p0.y - .03, 1.53), K), O.v((w, p0.y - .01, 1.565), K), O.v((0, p0.y, 1.565), K)]; V_ = [O.v((0, yt, zt - .025), K), O.v((w, yt, zt - .025), K), O.v((w, yt + .022, zt + .004), K), O.v((0, yt + .022, zt + .004), K)]
    for j in range(3): O.f(N[j], N[j + 1], V_[j + 1]); O.f(N[j], V_[j + 1], V_[j])
    O.f(V_[0], V_[1], V_[2]); O.f(V_[0], V_[2], V_[3])
    print('ИГЛОМЁТ: длина %.2f, конец y %.3f z %.3f, зазор до тела мин %.0f мм' % (dl, (c0 + d * dl).y, (c0 + d * dl).z, h_min * 1000))
    return O


res = {}
for im, fn in Sob.items():
    if TOLKO and im not in TOLKO: continue
    O = fn(); ob = O.sobrat(zerkalo=not im.startswith('Хвост')); ob.data.calc_loop_triangles(); res[im] = len(ob.data.loop_triangles); print('ОРГАН %s: %d тр' % (im, res[im]))
if opt.get('otchet'): json.dump(res, open(opt['otchet'], 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
bpy.ops.wm.save_as_mainfile(filepath=OUT)
