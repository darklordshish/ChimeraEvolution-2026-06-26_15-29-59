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


def lin(h):
    c = [int(h[i:i + 2], 16) / 255 for i in (1, 3, 5)]; return tuple(x / 12.92 if x <= .04045 else ((x + .055) / 1.055) ** 2.4 for x in c) + (1,)
GLAZ = {'Человек': '#3A8DFF', 'Волк': '#FFB000', 'Змея': '#B8E000', 'Ёж': '#E0262B', 'Лось': '#B5542A'}
CVET = dict(канал=(.02, .02, .02, 1), чешуя=lin('#9AA092'), коготь=(.05, .045, .04, 1), броня=(.07, .065, .06, 1), нос=(.03, .03, .03, 1), зуб=(.86, .83, .72, 1), глаз=(.34, .30, .08, 1), зрачок=(.01, .01, .01, 1), янтарь=(.62, .34, .06, 1), карий=(.10, .055, .03, 1), бусина=(.015, .015, .02, 1), ямка=(.13, .11, .10, 1))


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
        me = bpy.data.meshes.new(s.im); me.from_pydata([tuple(p) for p in V], [], F); me.update(); o = bpy.data.objects.new(s.im, me)
        ch = s.im.split('@')[0].split('.')[1] if '.' in s.im.split('@')[0] else None
        vid_ = s.im.split('@')[1] if '@' in s.im else 'Человек'
        if ch:   # цвет: материал объекта `<слот>.<что>@<вид>`, код берёт `_BaseColor`
            c_ = lin(GLAZ[vid_]) if ch == 'глаз' else lin('#141414') if ch == 'зрачок' else CVET[ch]; mt = bpy.data.materials.new(ch); mt.diffuse_color = c_; mt.use_nodes = True; mt.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = c_; me.materials.append(mt)
        bpy.context.scene.collection.objects.link(o); o.parent = arm
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
    k = [(0, 1.772), (.07, 1.758), (.18, 1.762), (.32, 1.778), (.44, 1.802), (.56, 1.85), (.578, 1.874), (.50, 1.878), (.47, 1.89), (.552, 1.975), (.536, 1.985), (.43, 1.925), (.456, 2.05), (.438, 2.058), (.36, 1.955), (.366, 2.085), (.348, 2.088), (.278, 1.965), (.27, 2.055), (.255, 2.058), (.195, 1.95), (.165, 2.005), (.150, 2.0), (.115, 1.875), (.05, 1.842), (0, 1.838)]
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
    """коготь — изогнутый четырёхгранник двумя звеньями, тёмный рог; основание `Руки@<вид>` — манжета кончика пальца"""
    K = 'предплечье'; O = Org(im); osn = '.' not in im.split('@')[0]
    for tip, c in konchiki():
        d = (tip - c).normalized(); pr = Vector((-1, 0, 0)); s1 = pr.cross(d).normalized(); s2 = d.cross(s1).normalized(); st = tip - d * .010
        kv = lambda c_, a_, b_: [O.v(c_ + s1 * (a_ * x_) + s2 * (b_ * y_), K) for x_, y_ in ((1, 0), (0, 1), (-1, 0), (0, -1))]
        if osn:
            O.lofta([kv(st - d * .014, r * 1.0, r * .9), kv(st + d * .002, r * 1.12, r * 1.0)], nachalo=False, konec=False); continue
        k0 = kv(st, r * .95, r * .85); k1 = kv(tip + d * (dl * .45) + pr * (zagib * .35), r * .62, r * .55)
        O.lofta([k0, k1], nachalo=False, konec=False); O.konec(k1, O.v(tip + d * dl + pr * zagib, K))
    return O


@organ('Руки@Волк')
def kogti_volk(): return kogti('Руки@Волк', .045, .020, .010)
@organ('Руки.коготь@Волк')
def kogti_volk2(): return kogti('Руки.коготь@Волк', .045, .020, .010)
@organ('Руки@Ёж')
def kogti_ezh(): return kogti('Руки@Ёж', .032, .030, .011)
@organ('Руки.коготь@Ёж')
def kogti_ezh2(): return kogti('Руки.коготь@Ёж', .032, .030, .011)


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
    """лист вверх-в-стороны от макушки (`витрина/лось/вид.png`), округлый конец, чаша вперёд"""
    O = Org('Чутьё@Лось'); H = 'голова'; p, _ = kozha((.4, -.01, 1.80), (-1, 0, 0)); kor = p - Vector((.008, 0, 0)); d = Vector((.07, 0, .10)); bk = Vector((-.10, 0, .07)).normalized(); q = Vector((0, 1, 0)); sh = .024
    T = [kor + bk * sh * .5, kor + d * .45 + bk * sh, kor + d * .82 + bk * sh * .62, kor + d, kor + d * .82 - bk * sh * .62, kor + d * .45 - bk * sh, kor - bk * sh * .5]
    pr = [O.v(t_ - q * .004, H) for t_ in T]; za = [O.v(t_ + q * .004, H) for t_ in T]; c_ = O.v(kor + d * .45 - q * .010, H); c2 = O.v(kor + d * .45 + q * .006, H); n = len(T)
    for j in range(n):
        k = (j + 1) % n; O.f(pr[j], pr[k], c_); O.f(za[k], za[j], c2)
        if j < n - 1: O.f(pr[j], za[j], za[k]); O.f(pr[j], za[k], pr[k])
    return O


@organ('Чутьё@Ёж')
def ushi_ezh():
    O = Org('Чутьё@Ёж'); H = 'голова'; p, _ = kozha((.4, -.015, 1.725), (-1, 0, 0)); c0 = p + Vector((.004, 0, 0)); r = .022
    k0 = [O.v(c0 + Vector((.006 + .010 * math.cos(u) * .3, r * math.cos(u), r * math.sin(u))), H) for u in [j * math.pi / 3 for j in range(6)]]
    ts = O.v(c0 + Vector((-.002, 0, 0)), H); nar = O.v(c0 + Vector((.014, .006, 0)), H)
    for j in range(6):
        k = (j + 1) % 6; O.f(k0[j], k0[k], ts); O.f(k0[k], k0[j], nar)
    return O


@organ('Руки@Лось')
def kapsula_osn():
    O = Org('Руки@Лось'); K = 'предплечье'
    for tip, c in konchiki():
        d = (tip - c).normalized(); L_ = (tip - c).length * 2 + .012; koren = tip - d * L_; s1 = Vector((1, 0, 0)); s1 = (s1 - d * s1.dot(d)).normalized(); s2 = d.cross(s1).normalized()
        kol = lambda c_, r: [O.v(c_ + s1 * (r * a_) + s2 * (r * b_), K) for a_, b_ in ((1, 1), (-1, 1), (-1, -1), (1, -1))]
        O.lofta([kol(koren - d * .012, .021), kol(koren + d * .004, .022)], nachalo=False, konec=False); break   # основание нужно коду, видна броня
    return O


@organ('Руки.броня@Лось')
def kapsula():
    """броня крупнее пальцев: два сегмента на палец, между ними шов"""
    O = Org('Руки.броня@Лось'); K = 'предплечье'; Pal = konchiki(); Cc = [(tip + c) / 2 for tip, c in Pal]
    for i_, (tip, c) in enumerate(Pal):
        d = (tip - c).normalized(); L_ = (tip - c).length * 2 + .012; koren = tip - d * L_; s1 = Vector((1, 0, 0)); s1 = (s1 - d * s1.dot(d)).normalized(); s2 = d.cross(s1).normalized()
        sh_ = min((Cc[i_] - q).length for j_, q in enumerate(Cc) if j_ != i_); rr = max(.008, min(.0165, sh_ * .42))
        kol = lambda t_, r: [O.v(koren + d * (L_ * t_) + s1 * (r * a_) + s2 * (r * b_), K) for a_, b_ in ((1, 1), (-1, 1), (-1, -1), (1, -1))]
        O.lofta([kol(0, rr), kol(.47, rr * .92)], nachalo=False, konec=True)   # звено у основания
        O.lofta([kol(.53, rr * .88), kol(1.0, rr * .7)], nachalo=True, konec=True)   # звено кончика, зазор шва
    return O


EYE = dict(p=(.032, 1.733), n=Vector((.3, -.95, 0)).normalized(), r1=.015, r2=.011, h=.008, pod=.005)
def fan(O, c, n, r1, r2, h, sides=6, H='голова', pod=.002):
    s1 = n.cross(Vector((0, 0, 1))).normalized(); s2 = n.cross(s1).normalized(); kr = [O.v(c + s1 * (r1 * math.cos(u)) + s2 * (r2 * math.sin(u)) + n * pod, H) for u in [j * 2 * math.pi / sides for j in range(sides)]]; O.konec(kr, O.v(c + n * h, H))
    return kr


@organ('Чутьё@Змея')
def brov_zmeya():
    O = Org('Чутьё@Змея'); H = 'голова'
    p1, _ = kozha((.010, -.5, 1.752), (0, 1, 0)); p2, _ = kozha((.058, -.5, 1.758), (0, 1, 0)); c_ = p1 + Vector((0, -.014, -.004)); d_ = p2 + Vector((0, -.014, -.004)); e_ = (p1 + p2) / 2 + Vector((0, 0, .014))
    for q in ((O.v(p1, H), O.v(p2, H), O.v(d_, H)), (O.v(p1, H), O.v(d_, H), O.v(c_, H)), (O.v(p1, H), O.v(e_, H), O.v(p2, H))): O.f(*q)
    return O


def glaz_vid(vid, r1=.013, r2=.0095, h=.005, pod=.004):
    O = Org('Чутьё.глаз@%s' % vid); p, _ = kozha((EYE['p'][0], -.5, EYE['p'][1]), (0, 1, 0)); fan(O, p, EYE['n'], r1, r2, h, pod=pod); return O
def zrachok_vid(vid, forma, r2=.0095, h=.005, pod=.004):
    """зрачок плоской пластиной у вершины купола: круг, горизонтальный прямоугольник копытного, вертикальная щель"""
    O = Org('Чутьё.зрачок@%s' % vid); H = 'голова'; p, _ = kozha((EYE['p'][0], -.5, EYE['p'][1]), (0, 1, 0)); n = EYE['n']; s1 = n.cross(Vector((0, 0, 1))).normalized(); up = Vector((0, 0, 1))
    r1 = r2 * 1.37; vys = lambda ax, az: pod + (h - pod) * max(0, 1 - ((ax / r1) ** 2 + (az / r2) ** 2) ** .5) + .0009   # над куполом глаза на ~1 мм
    if forma == 'круг':
        rr = r2 * .36; kr = [O.v(p + n * vys(rr, 0) + s1 * (rr * math.cos(u)) + up * (rr * math.sin(u)), H) for u in [j * math.pi / 3 for j in range(6)]]; O.konec(kr, O.v(p + n * vys(0, 0), H))
    elif forma == 'полоса':
        a_ = [O.v(p + n * vys(r1 * .5, r2 * .2) + s1 * sx * (r1 * .5) + up * sz * (r2 * .2), H) for sx, sz in ((1, 1), (-1, 1), (-1, -1), (1, -1))]; m_ = O.v(p + n * vys(0, 0), H)
        for j in range(4): O.f(a_[j], a_[(j + 1) % 4], m_)
    return O


@organ('Чутьё.глаз@Волк')
def glaz_volk(): return glaz_vid('Волк')
@organ('Чутьё.зрачок@Волк')
def zr_volk(): return zrachok_vid('Волк', 'круг')
@organ('Чутьё.глаз@Лось')
def glaz_los(): return glaz_vid('Лось', r1=.015, r2=.011)
@organ('Чутьё.зрачок@Лось')
def zr_los(): return zrachok_vid('Лось', 'полоса', r2=.011)
@organ('Чутьё.глаз@Ёж')
def glaz_ezh(): return glaz_vid('Ёж', r1=.011, r2=.0095, h=.006)
@organ('Чутьё.зрачок@Ёж')
def zr_ezh(): return zrachok_vid('Ёж', 'круг', h=.006)


@organ('Чутьё.глаз@Змея')
def glaz_zmeya():
    O = Org('Чутьё.глаз@Змея'); p, _ = kozha((EYE['p'][0], -.5, EYE['p'][1]), (0, 1, 0)); fan(O, p, EYE['n'], EYE['r1'], EYE['r2'], EYE['h'], pod=EYE['pod']); return O


@organ('Чутьё.зрачок@Змея')
def zrachok():
    O = Org('Чутьё.зрачок@Змея'); H = 'голова'; p, _ = kozha((EYE['p'][0], -.5, EYE['p'][1]), (0, 1, 0)); n = EYE['n']; s1 = n.cross(Vector((0, 0, 1))).normalized(); c = p + n * (EYE['h'] + .0015)
    sv = EYE['pod'] + (EYE['h'] - EYE['pod']) * (1 - .008 / EYE['r2']) + .0015; a_ = O.v(c + s1 * .0028, H); b_ = O.v(c - s1 * .0028, H); v_ = O.v(p + n * sv + Vector((0, 0, .008)), H); n_ = O.v(p + n * sv - Vector((0, 0, .008)), H)
    O.f(a_, v_, b_); O.f(b_, n_, a_); return O


@organ('Чутьё.ямка@Змея')
def yamka():
    O = Org('Чутьё.ямка@Змея'); p, _ = kozha((.028, -.5, 1.695), (0, 1, 0)); fan(O, p, EYE['n'], .019, .015, .008, 5, pod=.006); return O


def cheshuya_romby(O, shag=.09, ug=.62, zatuh=0):
    """чешуя по телу (геймдизайнер 05.11: «по телу должна идти, а не бронеплитами»): кожа спины, шеи и плеч режется двумя семействами косых плоскостей в ромбическую сетку; каждая ячейка-ромб сжимается к своему центру на 12 % (между чешуями видна кожа) и приподнимается на 2 мм. Форма тела повторяется точно, рельефа нет — читается рисунком и тоном куска. Ряд ниже — по ходу тела сверху вниз"""
    bm = bmesh.new()
    for o in TELO:
        if o.name not in ('хребет', 'шея', 'Сердце', 'Руки'): continue
        M = o.matrix_world; kb = o.data.shape_keys.key_blocks[0] if o.data.shape_keys else None; o.data.calc_loop_triangles()
        for t_ in o.data.loop_triangles:
            A = [M @ (kb.data[j].co if kb else o.data.vertices[j].co) for j in t_.vertices]; c = sum(A, Vector()) / 3; n = (A[1] - A[0]).cross(A[2] - A[0])
            if n.length < 1e-9: continue
            n.normalize()
            if 1.06 < c.z < 1.64 and n.y > .25 and (o.name != 'Руки' or c.z > 1.34):
                try: bm.faces.new([bm.verts.new(a_) for a_ in A])
                except ValueError: pass
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-5)
    N1, N2 = Vector((math.sin(ug), 0, math.cos(ug))), Vector((-math.sin(ug), 0, math.cos(ug)))
    for N_ in (N1, N2):
        for k in range(-30, 31):
            bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], dist=1e-6, plane_co=N_ * (k * shag) + Vector((0, 0, 0)), plane_no=N_)
    # почти плоские соседи внутри одной ячейки — одной гранью: меньше треугольников, рисунок тот же
    def kl(f): c = f.calc_center_median(); return (math.floor(N1.dot(c) / shag), math.floor(N2.dot(c) / shag))
    bm.normal_update(); ed = [e for e in bm.edges if len(e.link_faces) == 2 and kl(e.link_faces[0]) == kl(e.link_faces[1]) and e.link_faces[0].normal.angle(e.link_faces[1].normal, 0) < math.radians(9)]
    bmesh.ops.dissolve_edges(bm, edges=ed, use_verts=True)
    bmesh.ops.triangulate(bm, faces=bm.faces[:]); bm.normal_update()
    yach = {}
    for f in bm.faces:
        c = f.calc_center_median(); key = (math.floor(N1.dot(c) / shag), math.floor(N2.dot(c) / shag)); yach.setdefault(key, []).append(f)
    for key, F in yach.items():
        Sa = sum(f.calc_area() for f in F)
        if Sa < 4e-4: continue   # обрезки ячеек у края — без них край ровнее
        cc = sum((f.calc_center_median() * f.calc_area() for f in F), Vector()) / Sa; nn = sum((f.normal * f.calc_area() for f in F), Vector())
        if nn.y < 0: nn = -nn
        nn.normalize(); V = {v for f_ in F for v in f_.verts}; pod = sum((v.co - cc).dot(nn) for v in V) / len(V) + .0018
        # ромб плоский: ячейка ложится на свою среднюю плоскость над кожей, внутренние рёбра растворяются — одна грань на ромб, свет ровный
        bc = bmesh.new(); mp = {}
        for f_ in F:
            q = []
            for v in f_.verts:
                if v not in mp:
                    q_ = cc + ((v.co - cc) - nn * (v.co - cc).dot(nn)) * (.90 - (.45 * min(max(1.33 - cc.z, 0) / .25, 1) if zatuh else 0))
                    mp[v] = bc.verts.new(q_ + nn * max(pod, (v.co - cc).dot(nn) + .0018))   # почти плоско: где кожа выше плоскости, ромб ложится на кожу
                q.append(mp[v])
            try: bc.faces.new(q)
            except ValueError: pass
        bc.normal_update(); bmesh.ops.dissolve_limit(bc, angle_limit=math.radians(6), verts=bc.verts[:], edges=bc.edges[:]); bmesh.ops.dissolve_degenerate(bc, edges=bc.edges[:], dist=1e-4)
        dv = [v for v in bc.verts if len(v.link_edges) == 2 and (v.link_edges[0].other_vert(v).co - v.co).normalized().dot((v.link_edges[1].other_vert(v).co - v.co).normalized()) < -.995]
        bmesh.ops.dissolve_verts(bc, verts=dv)
        bmesh.ops.triangulate(bc, faces=bc.faces[:])
        for f_ in bc.faces: O.f(*[O.v(v.co) for v in f_.verts])
        bc.free()
    bm.free()


@organ('Шкура@Змея')
def cheshuya_kili():
    """накладка чешуи (спека 10.11: рельеф геометрией только участком, ~200 тр; рисунок по телу даёт плитка): крупные кили той же формы, что объём Codex 06.11 — ромбическая сетка, в ячейке продольный киль (0.25,0.25)–(0.75,0.75), шесть скатов к углам. Шаг и высота крупнее плитки, чтобы кили читались на силуэте плеч в 3/4 сзади; углы ячейки на 2 мм под кожей"""
    O = Org('Шкура@Змея'); SHAG, UG, HK = .068, .62, .018; S_, C_ = math.sin(UG), math.cos(UG)
    def tochka(u, w):   # узел сетки (u, w) → точка кожи сзади и нормаль
        x = (u - w) * SHAG / (2 * S_); z = (u + w) * SHAG / (2 * C_) + Z0_
        d_ = Vector((0, -1, -.45)).normalized(); h = BVH.ray_cast(Vector((x, .08, z)) - d_ * .8, d_)   # луч проходит через узел на глубине спины
        if h[0] is None or h[1].dot(d_) > -.05 or not (1.24 < h[0].z < 1.66) or abs(x) > .33: return None
        if h[1].dot(d_) > 0: h = (h[0], -h[1])
        return h[0], h[1]
    Z0_ = 1.30; n_ = 0
    for i in range(-9, 10):
        for j in range(-9, 10):
            U = [tochka(i + a_, j + b_) for a_, b_ in ((0, 0), (1, 0), (1, 1), (0, 1), (.25, .25), (.75, .75))]
            if any(q is None for q in U) or max((U[k][0] - U[(k + 1) % 4][0]).length for k in range(4)) > SHAG * 1.6: continue   # ячейка, разорванная краем тела
            ug = [q[0] - q[1] * .002 for q in U[:4]]; kil = [U[k][0] + U[k][1] * HK for k in (4, 5)]
            a_, b_, c_, d_ = (O.v(p) for p in ug); r0, r1 = (O.v(p) for p in kil)
            for f in ((a_, b_, r0), (b_, r1, r0), (b_, c_, r1), (c_, d_, r1), (d_, r0, r1), (d_, a_, r0)): O.f(*f)
            n_ += 1
    print('ЧЕШУЯ: килей %d' % n_)
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
    O = Org('Шкура@Лось'); Se = [(1.67, .02, .0), (1.60, .06, .075), (1.59, .065, .06), (1.51, .10, .125), (1.50, .105, .105), (1.42, .125, .16), (1.41, .125, .135), (1.33, .115, .125), (1.32, .11, .10), (1.24, .075, .05), (1.17, .03, .004)]; Ko = []
    for z, w, h in Se:
        y0 = kozha((0, .5, z), (0, -1, 0))[0].y; y1 = kozha((w * .55, .5, z), (0, -1, 0))[0].y if BVH.ray_cast(Vector((w * .55, .5, z)), Vector((0, -1, 0)))[0] else y0; y2 = kozha((w, .5, z), (0, -1, 0))[0].y if BVH.ray_cast(Vector((w, .5, z)), Vector((0, -1, 0)))[0] else y0 - .03
        Ko.append([O.v((0, y0 + h, z)), O.v((w * .55, y1 + h * .72, z - .006)), O.v((w, y2 - .004, z - .012))])
    for r0, r1 in zip(Ko, Ko[1:]):
        for j in range(2): O.f(r0[j], r0[j + 1], r1[j + 1]); O.f(r0[j], r1[j + 1], r1[j])
    return O


@organ('Шкура@Ёж')
def igly_ezh():
    """ровные ряды одинаковых клиньев внахлёст: сетка по рядам со сдвигом на полшага, один размер, вниз; край ряда — наружу, контур плеч"""
    O = Org('Шкура@Ёж'); shag = .050; dl = .135; sh = .029
    for j, (z, xm) in enumerate(((1.66, .02), (1.605, .07), (1.55, .20), (1.495, .27), (1.44, .29), (1.385, .27), (1.33, .23), (1.275, .19), (1.22, .17), (1.165, .15), (1.11, .135))):
        x = 0 if j % 2 == 0 else shag / 2
        while x <= xm + 1e-4:
            k_ = x / max(xm, .05); list_sherst(O, x, z, dl, sh, .08 + (k_ ** 3) * .8, lift=.016); x += shag
    return O


@organ('Игломёт@Ёж')
def iglomet():
    """`витрина/человек-ёж/ступени.png` ст.1: три отдельные круглые трубы (8 граней) с просветом, наклон вперёд 12°, длина 0.30, концы над затылком; торец открыт и чуть скошен, внутри тёмный канал (`Игломёт.канал@Ёж`). Каждая труба стоит на своём коротком основании-конусе от кожи загривка"""
    O = Org('Игломёт@Ёж'); K = 'грудная клетка'; r = .034; ug = math.radians(12); d = Vector((0, -math.sin(ug), math.cos(ug))); L = .30; h_min = 9; S = 8
    Zb = 1.889 - L * math.cos(ug); Yb = .16; s1 = Vector((1, 0, 0)); s2 = d.cross(s1).normalized(); kon = []
    for x in (-.098, 0.0, .098):
        B = Vector((x, Yb - (.020 if x == 0 else 0), Zb))   # средняя вперёд: в профиль три трубы ступенькой; E = B + d * L; kon.append(E)
        for t_ in np.linspace(0, 1, 30):
            h_ = BVH.find_nearest(B + d * (L * t_)); h_min = min(h_min, h_[3] - r)
        kol = lambda c, q, sk=0: [O.v(c + s1 * (q * math.cos(u)) + s2 * (q * math.sin(u)) + d * (sk * math.sin(u)), K) for u in [j * 2 * math.pi / S for j in range(S)]]
        k0 = kol(B, r); k1 = kol(E, r, .018); k3 = kol(E, r * .7, .018)   # срез скошен: верх трубы длиннее спереди
        O.lofta([k0, k1], nachalo=True, konec=False)
        for j in range(S):
            k = (j + 1) % S; O.f(k1[j], k1[k], k3[k]); O.f(k1[j], k3[k], k3[j])
        # основание: конус от кожи загривка к низу трубы
        q, n = kozha((x * .8, .5, Zb - .05), (0, -1, 0)); C0 = q - n * .01
        kb = [O.v(C0 + s1 * (r * 1.5 * math.cos(u)) + Vector((0, 0, r * 1.2 * math.sin(u))), K) for u in [j * 2 * math.pi / S for j in range(S)]]
        O.lofta([kb, k0], nachalo=False, konec=False)
    E = kon[1]; print('ИГЛОМЁТ: труб 3, 8 граней, длина %.2f, диаметр %.0f мм, просвет %.0f мм, концы y %.3f z %.3f, зазор до тела мин %.0f мм' % (L, r * 2000, (.098 - 2 * r) * 1000, E.y, E.z, h_min * 1000))
    return O


@organ('Игломёт.канал@Ёж')
def iglomet_kanal():
    O = Org('Игломёт.канал@Ёж'); K = 'грудная клетка'; r = .034 * .7; ug = math.radians(12); d = Vector((0, -math.sin(ug), math.cos(ug))); L = .30; S = 8
    Zb = 1.889 - L * math.cos(ug); Yb = .16; s1 = Vector((1, 0, 0)); s2 = d.cross(s1).normalized()
    for x in (-.098, 0.0, .098):
        E = Vector((x, Yb - (.020 if x == 0 else 0), Zb)) + d * L
        kol = lambda c, sk=0: [O.v(c + s1 * (r * math.cos(u)) + s2 * (r * math.sin(u)) + d * (sk * math.sin(u)), K) for u in [j * 2 * math.pi / S for j in range(S)]]
        k3 = kol(E, .018); kd = kol(E - d * .09)
        for j in range(S):   # стенки канала смотрят внутрь
            k = (j + 1) % S; O.f(k3[j], kd[j], kd[k]); O.f(k3[j], kd[k], k3[k])
        O.konec(kd[::-1], O.v(E - d * .09, K))
    return O


def morda_tochki(im):
    o = bpy.data.objects[im]; A = np.array([tuple(o.matrix_world @ v.co) for v in o.data.vertices]); j = A[:, 1].argmin(); T = Vector(A[j]); print('МОРДА %s: кончик y %.3f z %.3f' % (im, T.y, T.z)); return T, A
def nos_kolpak(im, w, h, d):
    O = Org(im); T, _ = morda_tochki(im.split('.')[0] + '@' + im.split('@')[1]); H = 'голова'
    a_ = O.v(T + Vector((0, -.005, .001)), H); v_ = O.v(T + Vector((0, d, h)), H); s_ = O.v(T + Vector((w, d * .7, .001)), H); n_ = O.v(T + Vector((0, d * .5, -h * .8)), H)
    O.f(a_, s_, v_); O.f(a_, n_, s_); O.f(s_, n_, v_); return O
def zuby(im, sp):
    O = Org(im); T, _ = morda_tochki(im.split('.')[0] + '@' + im.split('@')[1]); H = 'голова'
    for dy, x, dz, dl, r in sp:
        y, z0 = T.y + dy, T.z + dz; b = [O.v((x - r, y + r * .8, z0), H), O.v((x + r, y + r * .8, z0), H), O.v((x, y - r, z0), H)]; O.konec(b, O.v((x, y, z0 + dl), H))
    return O
def nozdri():
    O = Org('Пасть.нос@Лось'); T, _ = morda_tochki('Пасть@Лось'); o = bpy.data.objects['Пасть@Лось']; me = o.data; me.calc_loop_triangles(); M = o.matrix_world; b_ = BVHTree.FromPolygons([M @ v.co for v in me.vertices], [list(t_.vertices) for t_ in me.loop_triangles], all_triangles=True); H = 'голова'
    h = b_.ray_cast(Vector((.020, -.6, 1.636)), Vector((0, 1, 0)))
    p = h[0] if h[0] is not None else T + Vector((.02, .02, .035)); n = Vector((.25, -1, 0)).normalized(); s1 = Vector((1, .25, 0)).normalized()
    a_ = O.v(p + n * .002 + Vector((0, 0, .018)), H); b1 = O.v(p + n * .002 + s1 * .008, H); c_ = O.v(p + n * .002 - Vector((0, 0, .018)), H); d_ = O.v(p + n * .002 - s1 * .008, H)
    O.f(a_, b1, c_); O.f(a_, c_, d_); return O
SOB2 = {'Пасть.нос@Волк': lambda: nos_kolpak('Пасть.нос@Волк', .017, .016, .03), 'Пасть.нос@Ёж': lambda: nos_kolpak('Пасть.нос@Ёж', .012, .012, .024), 'Пасть.нос@Лось': nozdri,
        'Пасть.зуб@Волк': lambda: zuby('Пасть.зуб@Волк', ((.030, .018, -.024, -.020, .006), (.070, .030, -.030, -.012, .005), (.046, .017, -.054, .016, .005))), 'Пасть.зуб@Змея': lambda: zuby('Пасть.зуб@Змея', ((.036, .040, -.022, -.070, .015),))}
if opt.get('glaz_bazy'):
    """голубой глаз человека — объект базы: его родное Чутьё; чужое Чутьё снимает слот `Чутьё` с ушами и глазом; формы-ключи — сдвиг ближайшей вершины головы"""
    g = bpy.data.objects['голова']; Mg = g.matrix_world; Gk = g.data.shape_keys.key_blocks if g.data.shape_keys else None; Gb = np.array([tuple(Mg @ v.co) for v in g.data.vertices])
    for nm, fn in (('Чутьё.глаз', lambda: glaz_vid('Человек', r1=.012, r2=.0088, h=.004, pod=.003)), ('Чутьё.зрачок', lambda: zrachok_vid('Человек', 'круг', r2=.0088, h=.004, pod=.003))):
        O = fn(); O.im = nm + '@Человек'; ob = O.sobrat(); ob.name = nm; ob.data.name = nm
        if Gk:
            ob.shape_key_add(name=Gk[0].name)
            for kb in Gk[1:]:
                k_ = ob.shape_key_add(name=kb.name, from_mix=False)
                for j, v in enumerate(ob.data.vertices):
                    i_ = int(np.linalg.norm(Gb - np.array(tuple(Mg @ v.co)), axis=1).argmin()); k_.data[j].co = v.co + Mg.inverted().to_3x3() @ (Mg.to_3x3() @ (kb.data[i_].co - Gk[0].data[i_].co))
        print('ГЛАЗ БАЗЫ %s: %d тр, ключей %d' % (nm, len(ob.data.polygons), len(Gk) - 1 if Gk else 0))
    bpy.ops.wm.save_as_mainfile(filepath=OUT); sys.exit(0)
if opt.get('cvet'):
    res = {}
    for im, fn in SOB2.items():
        if bpy.data.objects.get(im): bpy.data.objects.remove(bpy.data.objects[im], do_unlink=True)
        ob = fn().sobrat(); ob.data.calc_loop_triangles(); res[im] = len(ob.data.loop_triangles); print('ОРГАН %s: %d тр' % (im, res[im]))
    bpy.ops.wm.save_as_mainfile(filepath=OUT); sys.exit(0)
res = {}
for im, fn in Sob.items():
    if TOLKO and im not in TOLKO: continue
    O = fn(); ob = O.sobrat(zerkalo=not im.startswith(('Хвост', 'Шкура.чешуя', 'Игломёт', 'Шкура@Змея'))); ob.data.calc_loop_triangles(); res[im] = len(ob.data.loop_triangles); print('ОРГАН %s: %d тр' % (im, res[im]))
if opt.get('otchet'): json.dump(res, open(opt['otchet'], 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
bpy.ops.wm.save_as_mainfile(filepath=OUT)
