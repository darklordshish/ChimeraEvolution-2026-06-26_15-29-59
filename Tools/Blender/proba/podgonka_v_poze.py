"""ПОДГОНКА В ПОЗЕ ЦЕЛИ (20.10, консилиум формы, направление Б: «сперва поза скелетом, потом форма в позе покоя»).
Колонка ст.2 листа оборотня сгорблена; выпрямлять цель тонкой пластиной по суставам нельзя — она гнётся как резина (шея
дугой, торс сплющен), и тело, подогнанное к такой цели, выходит рваным. Поэтому наоборот: скелет шасси ставится в позу
цели ВРАЩЕНИЯМИ костей (длины свои), сетка подгоняется к цели в этой позе по частям тела, и результат возвращается в позу
покоя обратным скиннингом (rest = (Σ wᵢ Mᵢ)⁻¹ · posed). Пропорции цели остаются в форме (длины костей — свои), поза — в
скелете.

  1. ПОЗА: кость за костью от корня — поворот, совмещающий направление кости шасси с направлением той же кости цели
     (по её ориентирам в позе листа); корень ещё и переносится в таз цели;
  2. ГРУБО: остаток (суставы шасси в позе → суставы цели) — тонкая пластина;
  3. ТОЧНО: `--iter` раз — вершина к ближайшей точке СВОЕЙ части цели (часть шасси — по наибольшему весу кости, грань
     цели — по ближайшему отрезку кости цели; грани дальше `--dal` от всех костей — хвост, пряди — не в счёт), сдвиги
     сглаживаются по связности (общие вершины швов — одна точка);
  4. обратно в покой: вершина умножается на обратную матрицу своего скина; результат — форма-ключ `--imya`.

  blender -b --factory-startup -P podgonka_v_poze.py -- шасси.blend цель.obj ориентиры_цели.json скелет_шасси.json выход.blend
      [--imya Волк] [--zhestkost 30,15,8,4] [--normal 0.5] [--maxd 0.25] [--dal 0.12] [--bez голова,Чутьё] [--poza_obj файл.obj] [--poza файл.json] [--sustavy файл.json] [--cel_pokoj файл.obj] [--dop_cel f.obj --dop_lm f.json --dop_pokoj f.obj] [--vstrechnye 1 [--vstr_maxd 0.05|0.25,предплечье:0.05]] [--cel_glad 0|шагов [--cel_glad_obl торс --cel_glad_x 0.2]] [--cel_glad_kol 30:0.14] [--cel_golen 0.4:0.25] [--bez_razvorota 1|0] [--lap_glad 0|шагов] [--ruki_po_kosti 1]
Ориентиры цели — имена как у `kluch_vida.py` (сустав:head/tail, носок, плюсна_вн/нар…), оси Blender, метры тела.
"""
import sys, json
import bpy, bmesh
import numpy as np
from mathutils import Vector, Matrix, Quaternion
from mathutils.bvhtree import BVHTree

a = sys.argv[sys.argv.index('--') + 1:]
src, tgt, lm_p, sk_p, out = a[:5]
opt = dict(imya='Волк', iter='10', smooth='8', maxd='0.25', dal='0.12', bez='голова,Чутьё')
i = 5
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
NAME = opt['imya']; BEZ = set(opt['bez'].split(',')) if opt['bez'] else set()
LT = {k: Vector(v) for k, v in json.load(open(lm_p, encoding='utf-8')).items()}
SK = {b['name']: b for b in json.load(open(sk_p, encoding='utf-8'))['bones']}

bpy.ops.wm.open_mainfile(filepath=src)
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
OBJ = [o for o in bpy.data.objects if o.type == 'MESH' and not o.name.startswith('@')]
for o in OBJ:
    if o.data.shape_keys:
        for kb in o.data.shape_keys.key_blocks: kb.value = 0.0


def mid(*ks):
    return sum((LT[k] for k in ks), Vector()) / len(ks)


# ---------- кость → направление цели (голова, хвост), по стороне ----------
def tgt_dir(bn):
    s = '.L' if bn.endswith('.L') else ''
    base = bn[:-2] if s else bn
    T = {
        'хребет': ('таз:head', 'шея:head'), 'шея': ('шея:head', 'голова:head'), 'голова': ('голова:head', 'голова:tail'),
        'плечо': ('плечо%s:head', 'предплечье%s:head'), 'предплечье': ('предплечье%s:head', 'предплечье%s:tail'),
        'бедро': ('бедро%s:head', 'голень%s:head'), 'голень': ('голень%s:head', 'голень%s:tail'),
    }
    if base == 'стопа':
        return LT['голень%s:tail' % s], mid('плюсна_вн%s' % s, 'плюсна_нар%s' % s)
    if base not in T: return None
    h, t = T[base]
    h, t = (h % s if '%s' in h else h), (t % s if '%s' in t else t)
    if h not in LT or t not in LT: return None
    return LT[h], LT[t]


# ---------- 1. поза ----------
bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode='POSE')
for pb in arm.pose.bones:
    pb.rotation_mode = 'QUATERNION'; pb.rotation_quaternion = Quaternion(); pb.location = (0, 0, 0)
order = []
def walk(b):
    order.append(b.name)
    for c in b.children: walk(c)
for b in arm.data.bones:
    if b.parent is None: walk(b)
AW = arm.matrix_world
root = arm.pose.bones[order[0]]
# корень в таз цели
bpy.context.view_layer.update()
d_ = LT['таз:head'] - AW @ root.head
root.matrix = Matrix.Translation(AW.inverted().to_3x3() @ d_) @ root.matrix
bpy.context.view_layer.update()
n_rot = 0
for bn in order:
    td = tgt_dir(bn)
    if td is None: continue
    pb = arm.pose.bones[bn]
    bpy.context.view_layer.update()
    cur = (AW @ pb.tail) - (AW @ pb.head)
    want = td[1] - td[0]
    if cur.length < 1e-6 or want.length < 1e-6: continue
    q = cur.normalized().rotation_difference(want.normalized())
    hw = AW @ pb.head
    Mw = AW @ pb.matrix
    Mw2 = Matrix.Translation(hw) @ q.to_matrix().to_4x4() @ Matrix.Translation(-hw) @ Mw
    pb.matrix = AW.inverted() @ Mw2
    n_rot += 1
bpy.context.view_layer.update()
bpy.ops.object.mode_set(mode='OBJECT')
print('ПОЗА: повёрнуто костей %d' % n_rot)

# ---------- общие вершины, части, позированные точки ----------
dg = bpy.context.evaluated_depsgraph_get()
key_of = lambda p: (round(p.x, 5), round(p.y, 5), round(p.z, 5))
uid, R0, owner, vmap = {}, [], [], {}
for o in OBJ:
    for vi, v in enumerate(o.data.vertices):
        p = o.matrix_world @ v.co; k = key_of(p)
        if k not in uid:
            uid[k] = len(R0); R0.append(p); owner.append(set())
        vmap[(o.name, vi)] = uid[k]; owner[uid[k]].add(o.name)
N = len(R0)
P = [None] * N
for o in OBJ:
    e = o.evaluated_get(dg); me = e.to_mesh()
    for vi, v in enumerate(me.vertices):
        P[vmap[(o.name, vi)]] = e.matrix_world @ v.co
    e.to_mesh_clear()
nb = [set() for _ in range(N)]
for o in OBJ:
    for ed in o.data.edges:
        x, y = vmap[(o.name, ed.vertices[0])], vmap[(o.name, ed.vertices[1])]
        nb[x].add(y); nb[y].add(x)
PART = {'хребет': 'корпус', 'грудная клетка': 'корпус', 'живот': 'корпус', 'таз': 'корпус', 'лопатка': 'корпус', 'лопатка.L': 'корпус',
        'шея': 'шея', 'голова': 'голова', 'плечо': 'плечо', 'плечо.L': 'плечо.L', 'предплечье': 'предплечье', 'предплечье.L': 'предплечье.L',
        'бедро': 'бедро', 'бедро.L': 'бедро.L', 'голень': 'голень', 'голень.L': 'голень.L', 'стопа': 'голень', 'стопа.L': 'голень.L'}
vpart = [None] * N
free = [True] * N
W = [dict() for _ in range(N)]
for o in OBJ:
    gn = {g.index: g.name for g in o.vertex_groups}
    for vi, v in enumerate(o.data.vertices):
        k = vmap[(o.name, vi)]
        for ge in v.groups:
            W[k][gn[ge.group]] = W[k].get(gn[ge.group], 0.0) + ge.weight
        best = max(((gn[ge.group], ge.weight) for ge in v.groups if PART.get(gn[ge.group])), key=lambda x: x[1], default=None)
        if best: vpart[k] = PART[best[0]]
        if o.name in BEZ: free[k] = False

# ---------- цель: части ----------
bpy.ops.wm.obj_import(filepath=tgt, forward_axis='NEGATIVE_Z', up_axis='Y')
tg = [o for o in bpy.context.selected_objects if o.type == 'MESH'][0]
bm = bmesh.new(); bm.from_mesh(tg.data); bm.transform(tg.matrix_world); bm.verts.ensure_lookup_table(); bm.faces.ensure_lookup_table()
# ЦЕЛЬ — ОБЪЁМАМИ, А НЕ ГРАНЯМИ (`--cel_glad шагов`, 27.10, пункт В разбора тракта): цель с генератора — гранёный low-poly с шумом,
# и подгонка списывала в ключ каждую его грань и рябь; грани потом всё равно ставит огранка. Таубин (λ 0.5, μ −0.53) объём
# не сжимает, а грани и мелкий шум снимает; края цели (открытые рёбра) не двигаются
# `--cel_glad_obl торс` (27.10, Г шаг (б)): только по торсу — от таза до шеи цели (ориентиры `таз:head`, `шея:head`), не дальше
# `--cel_glad_x` (0.2 м) от средней линии; вес спадает к краям полосы (колокол по высоте, линейно по |x|) — центральный желоб и
# узкий столб живота цели (несёт генератор, огранка их проявляла «щитом до паха») сглаживаются, руки и ноги — нет
def cel_glad(bm_, n_):
    if n_ <= 0: return
    V = np.array([list(v.co) for v in bm_.verts]); NB = [[e.other_vert(v).index for e in v.link_edges] for v in bm_.verts]
    fix = np.array([any(not e.is_manifold for e in v.link_edges) for v in bm_.verts])
    Wm = np.ones(len(V))
    if opt.get('cel_glad_obl') == 'торс':
        z0, z1 = LT['таз:head'].z, LT['шея:head'].z; xm = float(opt.get('cel_glad_x', '0.2'))
        t_ = np.clip((V[:, 2] - z0) / max(z1 - z0, 1e-6), 0, 1)
        Wm = np.sin(np.pi * t_) ** 2 * np.clip(1 - np.abs(V[:, 0]) / xm, 0, 1)
        print('ЦЕЛЬ: сглаживание по торсу — вершин с весом > 0.5: %d из %d' % (int((Wm > 0.5).sum()), len(V)))
    for _ in range(n_):
        for lam in (0.5, -0.53):
            L = np.array([V[nb_].mean(0) - V[k] if nb_ else np.zeros(3) for k, nb_ in enumerate(NB)])
            L[fix] = 0; V = V + lam * Wm[:, None] * L
    for v, c in zip(bm_.verts, V): v.co = Vector(c)
    bm_.normal_update()
cel_glad(bm, int(opt.get('cel_glad', '0')))
# ГОЛЕНЬ ЦЕЛИ БЕЗ ГОЛЕНИЩА (`--cel_golen do:otpusk`, 28.10, дизайн-линия FEEDBACK-2026-10-28d п.1, вариант В: «передняя и боковые
# плоскости через колено непрерывно, полнота икры — сзади»; та же правка в ключе после подгонки двигала рывком 23 вершины на 5 см —
# развороты 191 → 216): у цели вершины у отрезка голени (ориентиры голень:head → голень:tail) раскладываются на бок (мировая X) и
# перёд (−y); бок к средней линии и перёд ограничены p90 конца бедра (бедро:head → голень:head, t > 0.75); зад (икра) свободен
def _cel_golen():   # в функции: имена tg/h/t — глобальные у подгонки (tg — объект цели), их не затирать
    DO_, OT_ = (float(x) for x in opt['cel_golen'].split(':'))
    V = np.array([list(v.co) for v in bm.verts]); n_g = 0
    for s_ in ('', '.L'):
        if 'голень%s:head' % s_ not in LT: continue
        h, t = np.array(LT['голень%s:head' % s_]), np.array(LT['голень%s:tail' % s_]); bh = np.array(LT['бедро%s:head' % s_])
        def proj(a_, b_):
            d = b_ - a_; L = np.linalg.norm(d); u = d / L; Q = V - a_; tt = Q @ u / L; R = Q - np.outer(Q @ u, u); return tt, R, u
        tg, Rg, ug = proj(h, t); tb, Rb, _ = proj(bh, h)
        sb = np.array([1.0, 0, 0]) - ug[0] * ug; sb /= np.linalg.norm(sb); fr = np.cross(ug, sb)
        if fr[1] > 0: fr = -fr
        rg = np.linalg.norm(Rg, axis=1); rb = np.linalg.norm(Rb, axis=1)
        mb = (tb > 0.75) & (tb < 1.0) & (rb < 0.25); mg = (tg > -0.05) & (tg < DO_ + OT_) & (rg < 0.25) & (rg < rb)
        if not mb.any(): continue
        bok = float(np.percentile(np.abs(Rb[mb] @ sb), 90)); per = float(np.percentile(Rb[mb] @ fr, 90)); xo = float(h[0])
        for k in np.nonzero(mg)[0]:
            ss_ = 0.0 if tg[k] <= DO_ else (tg[k] - DO_) / OT_; ss_ = ss_ * ss_ * (3 - 2 * ss_)
            b_ = float(Rg[k] @ sb); f_ = float(Rg[k] @ fr); nb_, nf_ = b_, f_
            if abs(b_) > bok + ss_ and np.sign(b_) != np.sign(xo): nb_ = np.sign(b_) * (bok + ss_)
            if f_ > per + ss_: nf_ = per + ss_
            if nb_ != b_ or nf_ != f_: V[k] = V[k] + sb * (nb_ - b_) + fr * (nf_ - f_); n_g += 1
    for v, c in zip(bm.verts, V): v.co = Vector(c)
    bm.normal_update()
    print('ЦЕЛЬ: голень без голенища — сведено вершин %d (до t %.2f, отпуск %.2f)' % (n_g, DO_, OT_))


if opt.get('cel_golen'): _cel_golen()
# КОЛЕНИ ЦЕЛИ (`--cel_glad_kol шагов:радиус`, 28.10, критик r2 п.1 — «сапоги» ст.2): у цели Hunyuan сзади у колена горизонтальный
# ободок — верх икры как край голенища; подгонка переносила его в ключ, а позу и веса он переживал. Таубин в шаре радиуса вокруг
# ориентиров `голень:head`/`голень.L:head`, вес (1 − d/r)²
if opt.get('cel_glad_kol'):
    n_k, r_k = opt['cel_glad_kol'].split(':'); n_k, r_k = int(n_k), float(r_k)
    V = np.array([list(v.co) for v in bm.verts]); NB = [[e.other_vert(v).index for e in v.link_edges] for v in bm.verts]
    fix = np.array([any(not e.is_manifold for e in v.link_edges) for v in bm.verts]); Wk = np.zeros(len(V))
    for nm_ in ('голень:head', 'голень.L:head'):
        if nm_ in LT: Wk = np.maximum(Wk, np.clip(1 - np.linalg.norm(V - np.array(LT[nm_]), axis=1) / r_k, 0, 1) ** 2)
    for _ in range(n_k):
        for lam in (0.5, -0.53):
            L = np.array([V[nb_].mean(0) - V[k] if nb_ else np.zeros(3) for k, nb_ in enumerate(NB)]); L[fix] = 0; V = V + lam * Wk[:, None] * L
    for v, c in zip(bm.verts, V): v.co = Vector(c)
    bm.normal_update()
    print('ЦЕЛЬ: колени — вершин с весом > 0.5: %d, %d шагов, радиус %.2f м' % (int((Wk > 0.5).sum()), n_k, r_k))
segs = []
for bn in PART:
    td = tgt_dir(bn)
    if td: segs.append((PART[bn], td[0], td[1]))
def seg_d(p, a_, b_):
    ab = b_ - a_; u = max(0.0, min(1.0, (p - a_).dot(ab) / max(ab.length_squared, 1e-9))); return (p - (a_ + ab * u)).length
# ---------- 2. остаток суставов — пластина ----------
Pa = np.array([list(p) for p in P])
SRC, DST = [], []
bpy.context.view_layer.update()
for bn in PART:
    td = tgt_dir(bn)
    if td and bn in arm.pose.bones:
        pb = arm.pose.bones[bn]
        SRC += [list(AW @ pb.head), list(AW @ pb.tail)]; DST += [list(td[0]), list(td[1])]
S_, D_ = np.array(SRC), np.array(DST)
n = len(S_); Kk = np.linalg.norm(S_[:, None] - S_[None], axis=2) + 1e-4 * np.eye(n)
Pm = np.hstack([np.ones((n, 1)), S_]); A = np.zeros((n + 4, n + 4)); A[:n, :n] = Kk; A[:n, n:] = Pm; A[n:, :n] = Pm.T
Bm = np.zeros((n + 4, 3)); Bm[:n] = D_; Wt = np.linalg.solve(A, Bm)
X = np.linalg.norm(Pa[:, None] - S_[None], axis=2) @ Wt[:n] + np.hstack([np.ones((N, 1)), Pa]) @ Wt[n:]

DAL = float(opt['dal'])
# РАЗМЕТКА ЦЕЛИ ПО ТЕЛУ В ПОЗЕ: грань берёт часть ближайшей вершины шасси (после позы и пластины). По ближайшей КОСТИ
# грудь цели уходила к плечевой кости (она ближе позвоночника), корпусу доставалось 932 грани из 30 тыс., и тело рвалось
# на границах частей. Грани дальше `--dal` от тела (хвост, пряди) — не в счёт
from mathutils import kdtree
KD = kdtree.KDTree(N)
for k in range(N): KD.insert(Vector(X[k]), k)
KD.balance()
fpart = {}
for f_ in bm.faces:
    c_ = f_.calc_center_median(); co_, k_, d_ = KD.find(c_)
    # голова цели — кусок (черта ступени), к ней тело не тянется: грань, ближайшая к кости «голова» цели, не в счёт
    if min(segs, key=lambda s_: seg_d(c_, s_[1], s_[2]))[0] == 'голова': continue
    if d_ <= DAL and vpart[k_]: fpart.setdefault(vpart[k_], []).append(f_)
BVH = {}
for pn, fl in fpart.items():
    vs = {}; polys = []
    for f_ in fl:
        ids = []
        for v_ in f_.verts:
            if v_.index not in vs: vs[v_.index] = len(vs)
            ids.append(vs[v_.index])
        polys.append(ids)
    co = [None] * len(vs)
    for vi_, j in vs.items(): co[j] = bm.verts[vi_].co.copy()
    BVH[pn] = BVHTree.FromPolygons(co, polys)
print('ЧАСТИ ЦЕЛИ: ' + ', '.join('%s %d' % (pn, len(fl)) for pn, fl in sorted(fpart.items())))

# ---------- 3. притяжка по частям ----------
MAXD = float(opt['maxd'])
# РЕГУЛЯРИЗАЦИЯ ЛАПЛАСИАНОМ (как NICP Амберга — жёсткость убывает): min Σ s·|L X − L X₀|² + Σ m_k |X_k − C_k|². Пара (вершина →
# ближайшая точка своей части) берётся, только если нормали смотрят в одну сторону (cos > `--normal`): иначе верх трапеции
# цеплялся за бок дельты цели и вытягивался «крылом». Решение — сопряжёнными градиентами на numpy (scipy в Blender нет)
NBI = [np.array(sorted(nb[k]), dtype=np.int64) for k in range(N)]
DEG = np.array([max(1, len(x)) for x in NBI], dtype=float)
FLAT = np.concatenate([x for x in NBI]); OWN = np.concatenate([np.full(len(x), k) for k, x in enumerate(NBI)])
def lap(Y):
    s = np.zeros_like(Y); np.add.at(s, OWN, Y[FLAT]); return Y - s / DEG[:, None]
def lapT(Y):
    r = Y.copy(); w = Y / DEG[:, None]; np.subtract.at(r, FLAT, w[OWN]); return r
def normals(Y):
    nrm = np.zeros_like(Y)
    for o in OBJ:
        for pl in o.data.polygons:
            ids = [vmap[(o.name, vi)] for vi in pl.vertices]
            a_, b_, c_ = Y[ids[0]], Y[ids[1]], Y[ids[2]]
            n_ = np.cross(b_ - a_, c_ - a_)
            for j in ids: nrm[j] += n_
    l = np.linalg.norm(nrm, axis=1); l[l == 0] = 1; return nrm / l[:, None]
X0 = X.copy(); D0 = lap(X0)
# РЕЛЬЕФ — ОТ ЦЕЛИ, А НЕ ОТ ШАБЛОНА (`--lap_glad шагов`, 27.10, Г шаг (б)): регуляризация держит лапласиан шаблона, то есть его
# мелкий рельеф, — и белая линия живота и пресс человека К2 переезжали в ключ волка центральным желобом («щит до паха»), хотя у
# цели живот гладкий. Держится лапласиан СГЛАЖЕННОГО шаблона (Таубин): крупная форма шаблона та же, рельеф даёт цель
if int(opt.get('lap_glad', '0')) > 0:
    Xs = X0.copy(); FR = np.array(free)
    for _ in range(int(opt['lap_glad'])):
        for lam_ in (0.5, -0.53):
            Xs = Xs + lam_ * np.where(FR[:, None], -lap(Xs), 0.0)
    D0 = lap(Xs)
    print('ПОДГОНКА: лапласиан шаблона сглажен (%s шагов Таубина)' % opt['lap_glad'])
FACES = np.array([[vmap[(o.name, pl.vertices[0])], vmap[(o.name, pl.vertices[j])], vmap[(o.name, pl.vertices[j + 1])]]
                  for o in OBJ for pl in o.data.polygons for j in range(1, len(pl.vertices) - 1)])
def fnorm(Y):
    return np.cross(Y[FACES[:, 1]] - Y[FACES[:, 0]], Y[FACES[:, 2]] - Y[FACES[:, 0]])
N_REF = fnorm(X0)
BEZ_RAZV = opt.get('bez_razvorota', '1') == '1'
def razvernuty(Y):
    """грани, чья нормаль смотрит против нормали шаблона в позе (до притяжки)"""
    return np.where(np.einsum('ij,ij->i', fnorm(Y), N_REF) < 0)[0]
def resh(s_, M, C, Y0):
    """min s·|L Y − L X₀|² + Σ m_k |Y_k − C_k|² — сопряжённые градиенты от Y0"""
    A = lambda Y: s_ * lapT(lap(Y)) + M[:, None] * Y
    b = s_ * lapT(D0) + M[:, None] * C
    Y = Y0.copy(); r = b - A(Y); p = r.copy(); rs = np.sum(r * r, axis=0)
    for _cg in range(200):
        Ap = A(p); al = rs / np.maximum(np.sum(p * Ap, axis=0), 1e-12)
        Y += p * al; r -= Ap * al; rn = np.sum(r * r, axis=0)
        if np.all(rn < 1e-10): break
        p = r + p * (rn / np.maximum(rs, 1e-20)); rs = rn
    return Y
FREE = np.array(free)
COSN = float(opt.get('normal', '0.5'))
# ВСТРЕЧНЫЕ ПАРЫ (`--vstrechnye 1`, 21.10, критик лося: «голень — труба без скакательного сустава» при среднем расстоянии
# тела до цели 0.45 см): пара «вершина → ближайшая точка цели» не требует, чтобы цель была ПОКРЫТА — вершины голени ложились
# на переднюю стенку, а бугор скакательного и копыто не доставались никому. Точка цели (центр грани своей части) тянет к
# себе ближайшую вершину той же части; цель вершины — среднее всех её пар (как двусторонний NICP)
VSTR = opt.get('vstrechnye', '0') == '1'
# `--vstr_maxd` — свой предел встречной пары (критики r3/r2: когти цели вытягивали из кулаков шипы 15–20 см, низ головы — лоскуты на шее)
# число — для всех частей, `часть:число` — своё (волку шерсть дальше 10 см от тела на ногах, а когти — только у кистей)
VMAXD, VMAXP = float(opt['maxd']), {}
for x_ in opt.get('vstr_maxd', opt['maxd']).split(','):
    if ':' in x_: VMAXP[x_.split(':')[0]] = float(x_.split(':')[1])
    else: VMAXD = float(x_)
TS = {}
if VSTR:
    for pn, fl in fpart.items():
        TS[pn] = (np.array([list(f_.calc_center_median()) for f_ in fl]), np.array([list(f_.normal) for f_ in fl]))
for s_ in [float(v) for v in opt.get('zhestkost', '30,15,8,4').split(',')]:
    for _ in range(2):
        NX = normals(X)
        C = X.copy(); M = np.zeros(N)
        for k in range(N):
            if not FREE[k] or vpart[k] not in BVH: continue
            hit = BVH[vpart[k]].find_nearest(Vector(X[k]))
            if hit[0] is None or hit[3] > MAXD: continue
            if np.dot(NX[k], np.array(hit[1])) < COSN: continue
            C[k] = np.array(hit[0]); M[k] = 1.0
        if VSTR:
            acc = np.zeros_like(X); cnt = np.zeros(N)
            acc[M == 1] = C[M == 1]; cnt[M == 1] = 1
            for pn, (cs, ns) in TS.items():
                ids = [k for k in range(N) if FREE[k] and vpart[k] == pn]
                if not ids: continue
                kd_ = kdtree.KDTree(len(ids))
                for j, k in enumerate(ids): kd_.insert(Vector(X[k]), j)
                kd_.balance()
                for c_, n_ in zip(cs, ns):
                    co_, j, d_ = kd_.find(Vector(c_))
                    k = ids[j]
                    if d_ > VMAXP.get(pn, VMAXD) or np.dot(NX[k], n_) < COSN: continue
                    acc[k] += c_; cnt[k] += 1
            has = cnt > 0
            C[has] = acc[has] / cnt[has][:, None]; M[has] = 1.0
        # несвободные (голова) держатся на месте жёстко
        M[~FREE] = 10.0; C[~FREE] = X0[~FREE]
        Y = resh(s_, M, C, X)
        # БЕЗ РАЗВОРОТА ГРАНЕЙ (27.10, пункт В): пара тянет вершину через соседку — грань выворачивается (тёмные провалы в игре,
        # которые потом расправляла формовка). Вершины вывернутых граней и их соседи отпускают пары, лапласиан возвращает их к
        # форме шаблона; три захода, одно кольцо соседей (волк ст.2: 313 → 131 вывернутых, расстояние до цели 2.8 → 4.2 мм; два кольца
        # и шесть заходов хуже — пар 3343 из 5702, 6.7 мм, а вывернутых к концу 156). `--bez_razvorota 0` — по-старому
        if BEZ_RAZV:
            for _r in range(3):
                bad = razvernuty(Y)
                if not len(bad): break
                vs = set(FACES[bad].ravel()); vs |= {u for v in list(vs) for u in nb[v]}
                vs = np.array(sorted(vs)); M[vs] = np.where(FREE[vs], 0.0, M[vs]); Y = resh(s_, M, C, Y)
        X = Y
    print('ПОДГОНКА: жёсткость %.1f, пар %d из %d, вывернутых граней %d' % (s_, int((M == 1).sum()), int(FREE.sum()), len(razvernuty(X))))
_bad = razvernuty(X); _bp = {}
for f_ in FACES[_bad]: _bp[vpart[f_[0]] or ('несвободные' if not free[f_[0]] else '—')] = _bp.get(vpart[f_[0]] or ('несвободные' if not free[f_[0]] else '—'), 0) + 1
print('ПОДГОНКА: вывернутые грани по частям — ' + ', '.join('%s %d' % kv for kv in sorted(_bp.items(), key=lambda kv: -kv[1])))
res = [BVH[vpart[k]].find_nearest(Vector(X[k]))[3] for k in range(N) if free[k] and vpart[k] in BVH]
print('ПОДГОНКА: до своей части цели — средн. %.4f м, 95%% %.4f' % (float(np.mean(res)), float(np.percentile(res, 95))))
# обратная мера — ПОКРЫТИЕ: от точки цели (центр грани своей части) до ближайшей вершины тела той же части; пара «тело → цель»
# мала и тогда, когда тело легло на одну стенку цели
cov = {}
for pn, fl in fpart.items():
    ids = [k for k in range(N) if vpart[k] == pn]
    if not ids: continue
    kd_ = kdtree.KDTree(len(ids))
    for j, k in enumerate(ids): kd_.insert(Vector(X[k]), j)
    kd_.balance()
    cov[pn] = [kd_.find(f_.calc_center_median())[2] for f_ in fl]
allc = np.concatenate([np.array(v) for v in cov.values()])
print('ПОКРЫТИЕ: от цели до тела — средн. %.4f м, 95%% %.4f; по частям 95%%: %s' % (float(allc.mean()), float(np.percentile(allc, 95)),
      ', '.join('%s %.3f' % (pn, float(np.percentile(v, 95))) for pn, v in sorted(cov.items()))))

if opt.get('poza_obj'):
    # тело в позе цели (до возврата в покой) — для кадра «подгонка против цели»; `--poza_obj файл.obj` (оси Blender)
    with open(opt['poza_obj'], 'w') as fo:
        for p in X: fo.write('v %.5f %.5f %.5f' % (p[0], p[2], -p[1]) + chr(10))
        for o in OBJ:
            for pl in o.data.polygons: fo.write('f ' + ' '.join(str(vmap[(o.name, vi)] + 1) for vi in pl.vertices) + chr(10))
# ---------- 4. обратно в покой ----------
if opt.get('proverka', '0') == '1':
    X = Pa.copy()
# ЦЕПЬ НА ДЛИНАХ ЦЕЛИ (ГеймБосс 21.10: поза, посчитанная на длинах К2, роняла оборотня вперёд, стопа висела в 0.27–0.39 м над
# землёй). Игра ставит кость поворотом D = рамка позы · покой К2⁻¹ (по весу ключа сферически), а смещения берёт из суставов
# покоя: голова ребёнка = голова родителя + D_родителя·(покой ребёнка − покой родителя), хвост = голова + D·(покой хвоста −
# покой головы). Суставы покоя выводятся так, чтобы эта цепь давала РОВНО суставы цели: смещение цели, повёрнутое назад
# поворотом родителя. Кость без ориентиров жёстко едет с родителем: смещение К2 от его головы или хвоста (что ближе).
# Корень: покой — К2, поза — таз цели; стопа ложится на землю цели (плюсна z ≈ 0)
bpy.context.view_layer.update()
D, RH, RT, PH, PT = {}, {}, {}, {}, {}
for bn in order:
    pb = arm.pose.bones[bn]; par = pb.parent
    D[bn] = (AW @ pb.matrix).to_3x3() @ (AW @ pb.bone.matrix_local).to_3x3().inverted()
    h0, t0 = AW @ pb.bone.head_local, AW @ pb.bone.tail_local
    td = tgt_dir(bn)
    if par is None:
        RH[bn] = h0; PH[bn] = Vector(td[0]) if td else AW @ pb.head
    else:
        pn = par.name
        if td:
            RH[bn] = RH[pn] + D[pn].inverted() @ (Vector(td[0]) - PH[pn])
        else:
            # без ориентиров — жёстко к родителю: смещение К2 от его головы или хвоста (что ближе в покое К2)
            ph0, pt0 = AW @ par.bone.head_local, AW @ par.bone.tail_local
            RH[bn] = (RT[pn] + (h0 - pt0)) if (h0 - pt0).length < (h0 - ph0).length else (RH[pn] + (h0 - ph0))
        PH[bn] = PH[pn] + D[pn] @ (RH[bn] - RH[pn])
    RT[bn] = RH[bn] + (D[bn].inverted() @ (Vector(td[1]) - Vector(td[0])) if td else (t0 - h0))
    PT[bn] = PH[bn] + D[bn] @ (RT[bn] - RH[bn])
TGT = {bn: (Vector(tgt_dir(bn)[0]), Vector(tgt_dir(bn)[1])) for bn in order if tgt_dir(bn)}
err = max(max((PH[bn] - TGT[bn][0]).length, (PT[bn] - TGT[bn][1]).length) for bn in TGT)
niz = min(min(PH[bn].z, PT[bn].z) for bn in order if bn.startswith('стопа'))
print('ЦЕПЬ: длины цели, прямая кинематика игры до цели — макс %.4f м; низ стопы z %.3f м' % (err, niz))
# скин — как в игре: кость в позе = голова цепи цели PH, поворот D, привязка — покой К2 (без этого форма в покое снималась
# цепью на длинах К2 и в игре съезжала бы на разницу длин)
skin = {bn: (Matrix.Translation(PH[bn]) @ D[bn].to_4x4() @ Matrix.Translation(-(AW @ arm.pose.bones[bn].bone.head_local))) for bn in order}
if opt.get('proverka', '0') == '1':     # проверка туда-обратно — на той же цепи К2, которой тело ставилось в позу
    skin = {pb.name: (AW @ pb.matrix @ pb.bone.matrix_local.inverted() @ AW.inverted()) for pb in arm.pose.bones}
REST = np.zeros_like(X)
for k in range(N):
    ws = W[k]; tot = sum(w for g, w in ws.items() if g in skin)
    if tot <= 0: REST[k] = X[k]; continue
    M = sum((skin[g] * (w / tot) for g, w in ws.items() if g in skin), Matrix.Diagonal((0, 0, 0, 0)))
    REST[k] = np.array(M.inverted() @ Vector(X[k]))
# ЦЕЛЬ В ПОКОЕ (`--cel_pokoj файл.obj`, 21.10): голова и прочие черты ступени режутся из цели `kusok_celi.py`, а он ждёт цель
# поверх тела с ключом на весе 1, то есть в ПОКОЕ. Вершина цели снимается из позы обратной матрицей скина ближайшей
# вершины подогнанного тела; у головы цели (ближайшая кость цели — «голова») — матрицей одной кости «голова», иначе на
# стыке шеи весы смешивания мнут морду и рога
if opt.get('cel_pokoj') or opt.get('dop_pokoj'):
    KDX = kdtree.KDTree(N)
    for k in range(N): KDX.insert(Vector(X[k]), k)
    KDX.balance()
    # вдали от тела (дальше `--dal`: хвост, пряди гривы) — по ближайшей вершине КОРПУСА, шеи или головы: хвост цели у
    # икры, и по ближайшей вершине он ехал бы с голенью (в покое — доской вдоль ноги, при шаге — маятником)
    KDC_ids = [k for k in range(N) if vpart[k] in ('корпус', 'шея', 'голова')]
    KDC = kdtree.KDTree(len(KDC_ids))
    for j, k in enumerate(KDC_ids): KDC.insert(Vector(X[k]), j)
    KDC.balance()
    RPK = opt.get('ruki_po_kosti') == '1'; KDP = {}
    if RPK:
        for pn_ in ('плечо', 'плечо.L', 'предплечье', 'предплечье.L'):
            ids_ = [k for k in range(N) if vpart[k] == pn_]; t_ = kdtree.KDTree(len(ids_))
            for j, k in enumerate(ids_): t_.insert(Vector(X[k]), j)
            t_.balance(); KDP[pn_] = (t_, ids_)
    def ves(k):
        ws = {g: w for g, w in W[k].items() if g in skin}
        top = sorted(ws.items(), key=lambda kv: -kv[1])[:4]; tot = sum(w for _, w in top)
        return {g: w / tot for g, w in top} if tot > 0 else {}
    # ВЕСА ЦЕЛИ — тем же правилом, что и матрица снятия (файл `<cel_pokoj>.vesa.json`, вершина за вершиной): кусок,
    # вырезанный `kusok_celi.py --vesa`, в позе ступени встаёт ровно туда, где был у цели. С весами, перенесёнными с тела
    # в покое, хвост и пряди рвались — другое правило давало другую матрицу
    def snyat(bm_, out_p, golova=True):
      cache, VES = {}, []
      with open(out_p, 'w') as fo:
        for v_ in bm_.verts:
            p = v_.co
            if golova and min(segs, key=lambda s_: seg_d(p, s_[1], s_[2]))[0] == 'голова':
                ws = {'голова': 1.0}
            else:
                _, k_, d_ = KDX.find(p)
                if d_ > DAL: k_ = KDC_ids[KDC.find(p)[1]]
                # `--ruki_po_kosti 1` (29.10, новая ст.2: кисти листа висят у бёдер, когти цели брали вес таза и в позе ступени
                # тянулись лезвиями к бедру): вершина цели, ближайшая к кости РУКИ цели, — по ближайшей вершине тела той же части
                if RPK:
                    pn_ = min(segs, key=lambda s_: seg_d(p, s_[1], s_[2]))[0]
                    if pn_ in KDP and vpart[k_] != pn_: k_ = KDP[pn_][1][KDP[pn_][0].find(p)[1]]
                ws = ves(k_)
            key_ = tuple(sorted(ws.items()))
            if key_ not in cache:
                cache[key_] = sum((skin[g] * w for g, w in ws.items()), Matrix.Diagonal((0, 0, 0, 0))).inverted() if ws else Matrix.Identity(4)
            q = cache[key_] @ p
            VES.append({g: round(w, 4) for g, w in ws.items()})
            fo.write('v %.5f %.5f %.5f' % (q.x, q.z, -q.y) + chr(10))
        for f_ in bm_.faces: fo.write('f ' + ' '.join(str(v_.index + 1) for v_ in f_.verts) + chr(10))
      json.dump(VES, open(out_p + '.vesa.json', 'w', encoding='utf-8'), ensure_ascii=False)
      print('ФАЙЛ: цель в покое →', out_p, '(+ .vesa.json)')
    if opt.get('cel_pokoj'): snyat(bm, opt['cel_pokoj'])
    # ВТОРАЯ ЦЕЛЬ (`--dop_cel файл.obj --dop_lm ориентиры.json --dop_pokoj выход.obj`, 21.10): тело подгоняется к голой цели
    # (витрина/оборотень/тело.png), а грива и хвост есть только у цели колонки ст.2 — в своей позе. Она переводится в позу
    # голой цели тонкой пластиной по общим ориентирам и снимается в покой тем же правилом: `kusok_celi.py` вырезает из неё то,
    # что дальше зазора от нового тела
    if opt.get('dop_pokoj'):
        LD = {k: Vector(v) for k, v in json.load(open(opt['dop_lm'], encoding='utf-8')).items()}
        ks = [k for k in LD if k in LT]
        S2, D2_ = np.array([list(LD[k]) for k in ks]), np.array([list(LT[k]) for k in ks])
        n2 = len(ks); K2_ = np.linalg.norm(S2[:, None] - S2[None], axis=2) + 1e-4 * np.eye(n2)
        P2 = np.hstack([np.ones((n2, 1)), S2]); A2 = np.zeros((n2 + 4, n2 + 4)); A2[:n2, :n2] = K2_; A2[:n2, n2:] = P2; A2[n2:, :n2] = P2.T
        B2 = np.zeros((n2 + 4, 3)); B2[:n2] = D2_; W2 = np.linalg.solve(A2, B2)
        bpy.ops.wm.obj_import(filepath=opt['dop_cel'], forward_axis='NEGATIVE_Z', up_axis='Y')
        tg2 = [o for o in bpy.context.selected_objects if o.type == 'MESH'][0]
        bm2 = bmesh.new(); bm2.from_mesh(tg2.data); bm2.transform(tg2.matrix_world); bm2.verts.ensure_lookup_table()
        Q = np.array([list(v_.co) for v_ in bm2.verts])
        Q = np.linalg.norm(Q[:, None] - S2[None], axis=2) @ W2[:n2] + np.hstack([np.ones((len(Q), 1)), Q]) @ W2[n2:]
        for v_, q_ in zip(bm2.verts, Q): v_.co = Vector(q_)
        print('ВТОРАЯ ЦЕЛЬ: %d вершин переведены в позу цели по %d общим ориентирам' % (len(Q), n2))
        # у второй цели правило «у головы — одна кость головы» не берётся (критик r6): с колонки режутся грива и хвост, и
        # грива на затылке, привязанная к голове, поднималась с ней при правке позы головы — просветы к холке
        snyat(bm2, opt['dop_pokoj'], golova=False)
        bpy.data.objects.remove(tg2, do_unlink=True)
# ФАЙЛЫ ДЛЯ ИГРЫ (решение ГеймБосса 21.10, вариант Б: ключ — форма в покое, позу ступени даёт слой кода):
#  `--poza файл` — кости шасси В ПОЗЕ ЦЕЛИ (head/tail/z_axis, оси Blender, длины свои — только повороты), игра выводит поворот
#  кости разностью рамок покоя и позы и ставит его по весу ключа сферически, после привязки скина;
#  `--sustavy файл` — суставы цели В ПОКОЕ под новую форму: голова и хвост кости — в её рамке позы, переведённые обратно в
#  покой обратной матрицей той же кости (пропорции цели без позы)
def frames(src_bones):
    out_ = []
    for b in SK.values():
        pb = arm.pose.bones.get(b['name'])
        if pb is None: continue
        M = AW @ pb.matrix
        out_.append(src_bones(b, pb, M))
    return out_
if opt.get('poza'):
    def posed(b, pb, M):
        z = (M.to_3x3() @ Vector((0, 0, 1))).normalized()
        return dict(name=b['name'], parent=b.get('parent', ''), head=[round(c, 5) for c in PH[b['name']]], tail=[round(c, 5) for c in PT[b['name']]],
                    z_axis=[round(c, 5) for c in z])
    sk0 = json.load(open(sk_p, encoding='utf-8'))
    pz = dict(species=sk0.get('species', 'Человек'), kluch=NAME, axes=sk0.get('axes', ''), bones=frames(posed),
              note='кости в позе цели «%s» на ступени 2 (вес 1): рамка (z_axis, направление) — поворот; head/tail — суставы цели '
                   '(цепь на длинах суставов покоя *-sustavy.json); podgonka_v_poze.py' % NAME)
    json.dump(pz, open(opt['poza'], 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    print('ФАЙЛ: поза →', opt['poza'])
if opt.get('sustavy'):
    def rest_j(b, pb, M):
        return dict(name=b['name'], parent=b.get('parent', ''), head=[round(c, 5) for c in RH[b['name']]], tail=[round(c, 5) for c in RT[b['name']]],
                    z_axis=b.get('z_axis', [0, 0, 1]))
    sk0 = json.load(open(sk_p, encoding='utf-8'))
    sj = dict(sk0); sj['bones'] = frames(rest_j)
    sj['note'] = ('суставы цели «%s» в ПОКОЕ шасси: смещения цели, повёрнутые назад поворотом родителя из *-poza.json — прямая '
                  'кинематика игры (D·смещение покоя) даёт суставы цели; podgonka_v_poze.py' % NAME)
    json.dump(sj, open(opt['sustavy'], 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    print('ФАЙЛ: суставы покоя →', opt['sustavy'])
# поза сбрасывается, форма-ключ
bpy.context.view_layer.objects.active = arm; bpy.ops.object.mode_set(mode='POSE')
for pb in arm.pose.bones:
    pb.rotation_quaternion = Quaternion(); pb.location = (0, 0, 0)
bpy.ops.object.mode_set(mode='OBJECT')
bpy.data.objects.remove(tg, do_unlink=True)
for o in OBJ:
    if o.data.shape_keys is None: o.shape_key_add(name='Basis')
    if NAME in o.data.shape_keys.key_blocks: o.shape_key_remove(o.data.shape_keys.key_blocks[NAME])
    kb = o.shape_key_add(name=NAME, from_mix=False); inv = o.matrix_world.inverted()
    for vi in range(len(o.data.vertices)):
        kb.data[vi].co = inv @ Vector(REST[vmap[(o.name, vi)]])
err = np.linalg.norm(REST - np.array([list(p) for p in R0]), axis=1)
print('ОБРАТНО: отклонение покоя от исходного К2 — средн. %.4f, макс %.4f м (при --proverka 1 должно быть ~0)' % (err.mean(), err.max()))
bpy.ops.wm.save_as_mainfile(filepath=out)
print('ПОДГОНКА: готово →', out)
