"""ДВИЖОК НАКЛАДОК ШКУРЫ (13.11, решение геймдизайнера 12.11, спека `2026-11-12-vechnaya-evolyuciya-les.md` §6а п.2: у органа одного слота одно устройство, доноры различаются данными). Рельеф-накладка Шкуры — один движок и по файлу данных на донора (`zmeya/volk/los/ezh.json`), а не четыре функции с числами в метрах человека.

Каркас шасси (`karkas.json`) строится по арматуре: O — основание шеи, A — вдоль спины к тазу, L — поперёк, N — наружу, T — длина спины, W — полуширина плеч. Координаты в данных — «метры человека в каркасе»: a вдоль L, b вдоль A, c вдоль N; на шасси точка (a, b, c) переводится в мир как O + L·a·(W/Wh) + A·b·g + N·c·g, g = T/Th. Бюджет пропорционален габариту: шаги раскладок делятся на sqrt(g), размеры элементов умножаются на sqrt(g) — число элементов ≈ ×g. Посадка на кожу одна для всех: ближайшая точка тела к точке над кожей (на человеке g = 1 и всё как было).

  из генератора органов: import nakladka; nakladka.postroit(данные, 'Человек')
  проверка на чужом шасси:
  blender -b --factory-startup --python-exit-code 1 -P nakladka.py -- --fbx шасси.fbx --shassi Лось --donor volk --out выход.blend
"""
import sys, os, json, math
import bpy, bmesh
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

try: HERE = os.path.dirname(os.path.abspath(__file__))
except NameError: HERE = os.path.dirname(os.path.abspath(sys.argv[sys.argv.index('--python') + 1])) if '--python' in sys.argv else os.getcwd()
TH, WH, H0 = 0.49122, 0.185, 0.058   # длина спины и полуширина плеч человека; подъём точки запроса над кожей (прежнее «y = 0.10»)
_KARKAS = None
_SHASSI = {}


def karkas():
    global _KARKAS
    if _KARKAS is None: _KARKAS = json.load(open(os.path.join(HERE, 'karkas.json'), encoding='utf-8'))
    return _KARKAS


class Shassi:
    """каркас и кожа шасси: арматура, BVH тела, веса вершин"""
    def __init__(s, imya, arm=None):
        s.imya = imya; s.arm = arm or [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]; K = karkas()[imya]; M = s.arm.matrix_world; bn = s.arm.data.bones
        s.O = M @ bn[K['nachalo']].head_local; d = M @ bn[K['k_tazu']].head_local - s.O; s.T = d.length; s.A = d / s.T; s.L = Vector((1, 0, 0))
        sp = Vector(K['spina']); s.N = (sp - s.A * sp.dot(s.A)).normalized()
        pl = bn[K['plecho']]; s.W = max(abs((M @ pl.head_local).x), abs((M @ pl.tail_local).x))
        s.g = s.T / TH; s.kw = s.W / WH
        if abs(s.g - 1) < .01 and abs(s.kw - 1) < .03: s.g = s.kw = 1.0   # человек — тождество
        s.sg = math.sqrt(s.g)
        s.telo = [o for o in bpy.data.objects if o.type == 'MESH' and '@' not in o.name and o.name not in ('Cube', 'Рога')]
        Vb, Fb, s.Wb = [], [], []
        for o in s.telo:
            kb = o.data.shape_keys.key_blocks[0] if o.data.shape_keys else None; Mo = o.matrix_world; n0 = len(Vb); gn = {g_.index: g_.name for g_ in o.vertex_groups}
            for j, v in enumerate(o.data.vertices):
                Vb.append(Mo @ (kb.data[j].co if kb else v.co)); s.Wb.append({gn[g_.group]: g_.weight for g_ in v.groups if g_.weight > 0})
            o.data.calc_loop_triangles(); Fb += [[n0 + q for q in t.vertices] for t in o.data.loop_triangles]
        s.BVH = BVHTree.FromPolygons(Vb, Fb, all_triangles=True); s.VA = np.array([tuple(v) for v in Vb])
    def mir(s, a, b, c=0.0): return s.O + s.L * (a * s.kw) + s.A * (b * s.g) + s.N * (c * s.g)
    def b_of(s, p): return (p - s.O).dot(s.A) / s.g
    def kozha(s, a, b):
        """точка кожи и нормаль наружу: ближайшая к точке над кожей на (a, b)"""
        p, n = s.BVH.find_nearest(s.mir(a, b, H0))[:2]
        if n.dot(s.N) < 0: n = -n
        if abs(a) < 1e-9: p = Vector((0, p.y, p.z)); n = Vector((0, n.y, n.z)).normalized()   # на оси точка строго на оси: зеркало сходится
        return Vector(p), Vector(n)
    def ves(s, p): return s.Wb[int(np.linalg.norm(s.VA - np.array(tuple(p)), axis=1).argmin())]


def kesh(imya, arm=None):
    arm = arm or [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]; k = (imya, arm.name, id(arm))
    if k not in _SHASSI: _SHASSI[k] = Shassi(imya, arm)
    return _SHASSI[k]


class Gm:
    """вершины и грани; вершина с тем же положением (до 5 знаков) одна"""
    def __init__(s): s.V = []; s.F = []; s.os = {}; s.n = 0
    def v(s, p):
        p = Vector(p); k_ = (round(p.x, 5), round(p.y, 5), round(p.z, 5))
        if k_ in s.os: return s.os[k_]
        s.V.append(p); s.os[k_] = len(s.V) - 1; return s.os[k_]
    def f(s, *q):
        if len(set(q)) == len(q): s.F.append(tuple(q))


def sobrat(S, G, im, zerkalo):
    """объект Blender: зеркало по x, веса ближайшей вершины тела, модификатор арматуры, грани без сглаживания, треугольники"""
    V = list(G.V); F = list(G.F)
    if zerkalo:
        ind = {}
        for j, p in enumerate(G.V):
            if abs(p.x) < 1e-5: ind[j] = j
            else: ind[j] = len(V); V.append(Vector((-p.x, p.y, p.z)))
        F += [tuple(ind[q] for q in reversed(t)) for t in G.F]
    me = bpy.data.meshes.new(im); me.from_pydata([tuple(p) for p in V], [], F); me.update(); o = bpy.data.objects.new(im, me)
    bpy.context.scene.collection.objects.link(o); o.parent = S.arm; m = o.modifiers.new('ск', 'ARMATURE'); m.object = S.arm
    for j, p in enumerate(V):
        for n_, w_ in S.ves(p).items(): (o.vertex_groups.get(n_) or o.vertex_groups.new(name=n_)).add([j], w_, 'REPLACE')
    for p_ in me.polygons: p_.use_smooth = False
    bm = bmesh.new(); bm.from_mesh(me); bmesh.ops.triangulate(bm, faces=bm.faces[:]); bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:]); bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS"); bm.to_mesh(me); bm.free(); me.update(); return o


# ---------- элементы ----------
def el_list(S, G, a, b, E, nakl, dl=None):
    """лист/игла: корень на коже, кончик по nakl (0 — вниз по спине, к π/2 — наружу), ребро приподнято"""
    sg = S.sg; dl = (dl if dl is not None else E['dl']) * sg; sh = E['sh'] * sg; lift = E['lift'] * sg; shir = E.get('shir', False)
    p, ns = S.kozha(a, b); sgn = 1 if a >= 0 else -1
    n = (S.L * (sgn * .7 * min(abs(a) / .25, 1)) + S.N * .75 - S.A * .15).normalized(); w_ = E.get('ns_ves', 1.0); nz = E.get('ns_ves_nizhe')
    if nz and b > nz[0]: w_ = nz[1]   # ниже по спине: корень на ровной коже, нормаль кожи листу не нужна (на трапеции наклонной плоскости — нужна: иначе лист уходит под кожу)
    n = (n + ns * w_).normalized()
    Av = (S.A + Vector((0, 0, -1)) * E.get('vniz', 0)).normalized()   # «вниз» элемента: вдоль спины с долей вниз по весу (vniz) — у четвероногого спина горизонтальна, и без этого лист ложится вдоль холки; у человека A и так вниз, разницы нет
    vn = Av * math.cos(nakl) + S.L * sgn * math.sin(nakl); vn = (vn - n * vn.dot(n)).normalized(); bk = n.cross(vn).normalized()
    if bk.dot(S.L) * sgn < 0: bk = -bk
    sym = abs(a) < 1e-4; v = G.v
    koren = v(p - n * (.006 * sg) - vn * (.01 * sg)); l = v(p + vn * (dl * .4) + bk * sh + n * lift)
    r = None if sym else v(p + vn * (dl * .4) - bk * sh + n * lift); gr = v(p + vn * (dl * .45) + n * (lift + sh * .3)); kon = v(p + vn * dl + n * (lift * .8))
    if shir:
        rl = v(p - n * (.014 * sg) + bk * sh * .7); rr = None if sym else v(p - n * (.014 * sg) - bk * sh * .7)
        F_ = ((koren, gr, rl), (rl, gr, l), (l, gr, kon)) if sym else ((rl, gr, l), (rr, r, gr), (rl, rr, gr), (l, gr, kon), (gr, r, kon))
    else: F_ = ((koren, gr, l), (l, gr, kon)) if sym else ((koren, gr, l), (koren, r, gr), (l, gr, kon), (gr, r, kon))
    for q in F_: G.f(*q)
    G.n += 1


def _resamp(lst, n):
    """список чисел (или кортежей) → n точек равномерно по индексу, линейно; n равно длине — как есть"""
    if n == len(lst) or len(lst) < 2: return list(lst)
    out = []
    for k in range(n):
        t = k * (len(lst) - 1) / max(n - 1, 1); i = min(int(t), len(lst) - 2); f = t - i; x0, x1 = lst[i], lst[i + 1]
        out.append([u + (w - u) * f for u, w in zip(x0, x1)] if isinstance(x0, (list, tuple)) else x0 + (x1 - x0) * f)
    return out


def raskl_veer(S, G, R, E):
    sg = S.sg; b0 = R['centr'][1]; DL = R['dl_mnozh']; kr_ = R['krai']; nk = R['nakl']; rows = R['ryady']
    nr = max(len(rows), int(round(len(rows) * sg)))
    cols = _resamp([[r['r'], r['dl'], r['sh'], r['lift']] for r in rows], nr)
    for k, (r_, dl_, sh_, lift_) in enumerate(cols):
        src = rows[min(len(rows) - 1, int(round(k * (len(rows) - 1) / max(nr - 1, 1))))]; ug = _resamp(src['ugly'], int(round(len(src['ugly']) * sg)))
        for m, th in enumerate(ug):
            kr = th > kr_['ot_ugla']; e = dict(E, dl=dl_, sh=sh_, lift=lift_)
            el_list(S, G, r_ * math.sin(th), b0 + r_ * math.cos(th), e, nk['baza'] + th * (kr_['nakl_k'] if kr else nk['k']), dl=dl_ * DL[(k * 4 + m) % len(DL)] * (kr_['dl'] if kr else 1))
    for d in R.get('dop', []): el_list(S, G, d['a'], d['b'], dict(E, dl=d['dl'], sh=d['sh'], lift=d['lift']), d['nakl'])


def raskl_ryady(S, G, R, E):
    sg = S.sg; shag = R['shag'] / sg; rows = _resamp(R['ryady'], int(round(len(R['ryady']) * sg))); nk = R['nakl']
    for j, (b, am) in enumerate(rows):
        a = 0 if j % 2 == 0 else shag / 2
        while a <= am + 1e-4:
            el_list(S, G, a, b, E, nk['baza'] + ((a / max(am, .05)) ** 3) * nk['k3']); a += shag


def _interp(tab, x):
    """кусочно-линейная по возрастающему первому столбцу; за краями — крайнее значение"""
    if x <= tab[0][0]: return tab[0][1]
    for (x0, y0), (x1, y1) in zip(tab, tab[1:]):
        if x0 <= x <= x1: return y0 + (y1 - y0) * (x - x0) / (x1 - x0)
    return tab[-1][1]


def raskl_liniya(S, G, R, E):
    """гребень вдоль спины: сечение из трёх точек над кожей, полосами по профилю"""
    sg = S.sg; prof = R['profil']; wt = sorted(R['shirina']); wk = R['shir_k']; Ko = []
    for b, h in prof:
        w = _interp(wt, b) * wk; h *= sg; ver = []
        for a_, dh, dz in ((0, h, 0), (w * .55, h * .72, .006 * sg), (w, -.004 * sg, .012 * sg)):
            p, _ = S.kozha(a_, b); ver.append(G.v(p + S.N * dh + S.A * dz))
        Ko.append(ver)
    for r0, r1 in zip(Ko, Ko[1:]):
        for j in range(2): G.f(r0[j], r0[j + 1], r1[j + 1]); G.f(r0[j], r1[j + 1], r1[j])
    G.n += len(Ko)


def raskl_romb(S, G, R, E):
    """ромбическая сетка косых плоскостей: пластина-черепица на ячейку, скат под нижними кромками"""
    sg = S.sg; shag = R['shag'] / sg; ug = R['ug']; Sn, Cs = math.sin(ug), math.cos(ug); nah = E['nah']; hk, bok, verh = E['hk'] * sg, E['bok'] * sg, E['verh'] * sg
    da, db = nah * R['shag'] / (2 * Sn), nah * R['shag'] / (2 * Cs); razm = math.hypot(da * S.kw / sg, db * S.g / sg) / math.hypot(da, db)   # размер ячейки на шасси к человеческому
    SH = R['shag'] * razm
    def tochka(u, w):
        a = (u - w) * shag / (2 * Sn); b = R['b0'] - (u + w) * shag / (2 * Cs)
        if abs(a) > R['a_max']: return None
        p, n = S.kozha(a, b)
        if not (R['b_min'] < S.b_of(p) < R['b_max']): return None
        return p, n
    def glub(m): q_, n_q, _, _ = S.BVH.find_nearest(m); return (q_ - m).dot(n_q)
    NR = int(math.ceil(9 * sg))
    for i in range(-NR, NR + 1):
        for j in range(-NR, NR + 1):
            U = [tochka(i + .5 + (a_ - .5) * nah, j + .5 + (b_ - .5) * nah) for a_, b_ in ((0, 0), (1, 0), (1, 1), (0, 1))]
            if any(q is None for q in U) or max((U[k][0] - U[(k + 1) % 4][0]).length for k in range(4)) > SH * nah * E['rastyazh']: continue
            (pb, nb), (pr, nr), (pt, nt), (pl, nl) = U
            if max(glub((pb + nb * hk + pt - nt * verh) / 2), glub((pr + nr * bok + pl + nl * bok) / 2)) > E['glub_max'] * sg: continue
            B, Rr, T, L = G.v(pb + nb * hk), G.v(pr + nr * bok), G.v(pt - nt * verh), G.v(pl + nl * bok)
            Ux = [tochka(i + .5 + (a_ - .5) * (nah + E['skat_dop']), j + .5 + (b_ - .5) * (nah + E['skat_dop'])) for a_, b_ in ((0, 0), (1, 0), (0, 1))]
            Bs, Rs, Ls = (G.v(q[0] - q[1] * (E['skat_zazor'] * sg)) if q and (q[0] - p).length < SH * E['skat_dalnost'] else None for q, (p, n_) in zip(Ux, ((pb, nb), (pr, nr), (pl, nl))))
            G.f(T, Rr, B); G.f(T, B, L)
            if Bs is not None:
                if Rs is not None: G.f(Rr, Rs, Bs); G.f(Rr, Bs, B)
                if Ls is not None: G.f(L, B, Bs); G.f(L, Bs, Ls)
            G.n += 1


RASKL = {'veer': raskl_veer, 'ryady': raskl_ryady, 'liniya': raskl_liniya, 'romb': raskl_romb}


def postroit(d, shassi='Человек', arm=None, im=None):
    """орган по данным донора на шасси: готовый объект Blender"""
    S = kesh(shassi, arm); G = Gm(); R = d['raskladka']; im = im or d['organ']
    RASKL[R['tip']](S, G, R, d.get('element', {}))
    o = sobrat(S, G, im, d.get('zerkalo', True)); o.data.calc_loop_triangles(); tr = len(o.data.loop_triangles); B = d['byudzhet'] * S.g
    print('НАКЛАДКА %s на %s: элементов %d, тр %d (бюджет %d·g = %.0f), g = %.3f' % (d['organ'], shassi, G.n, tr, d['byudzhet'], B, S.g))
    if tr > 1.15 * B: print('ПРЕДУПРЕЖДЕНИЕ: %s на %s — %d тр больше 1.15 бюджета (%.0f)' % (d['organ'], shassi, tr, B))
    return o


def zagruzit(imya):
    return json.load(open(os.path.join(HERE, imya + '.json'), encoding='utf-8'))


if __name__ == '__main__':
    a = sys.argv[sys.argv.index('--') + 1:]; opt = {}; i = 0
    while i < len(a): opt[a[i].lstrip('-')] = a[i + 1]; i += 2
    bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.fbx(filepath=opt['fbx'])
    postroit(zagruzit(opt['donor']), opt['shassi']); bpy.ops.wm.save_as_mainfile(filepath=opt['out'])
