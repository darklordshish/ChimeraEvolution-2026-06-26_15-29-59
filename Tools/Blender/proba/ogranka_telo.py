"""ТЕЛО ПО ЛИСТУ ОГРАНКИ (04.11, r10; `витрина/человек/огранка-рёбра.png` — характер граней, пропорции — от плотной базы по `вид.png`). Зона тела (шея, торс, руки до запястья, ноги до щиколотки) перекладывается поверх границ объектов:
  1. зона сваривается и дробится; каждая её грань получает метку листа: точка → пиксель ракурса сплайном по опорным парам, слева и справа одна и та же половина листа, поэтому раскладка симметрична;
  2. метки становятся областями-гранями: мелкое растворяется, почти плоские соседи сливаются, области с большим разбросом нормалей или прогибом делятся;
  3. границы областей упрощаются Дугласом–Пекером (общая цепь одна на две области), каждая область заполняется по своему контуру без внутренних вершин. Вершины лежат на поверхности базы, поэтому контур держится;
  4. область целиком отходит объекту, которому принадлежит большая часть её площади; вершины на стыке объектов совпадают положением. Веса — от базы, барицентрически.
Зоны голова, кисти, стопы прореживаются на месте, кольца на стыке с зоной тела и кольца паспортов не меняются.

  blender -b база.blend --factory-startup --python-exit-code 1 -P ogranka_telo.py -- выход.blend metki.npz --pasporta шов.json,… [--dolya_kg 0.55] [--tol 0.012] [--min_pl 8] [--split 30] [--dev 0.01] [--merge 8] [--z_kist 0.955] [--z_stopa 0.09]
"""
import sys, json, os, math
import bpy, bmesh
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from mathutils.geometry import delaunay_2d_cdt

a = sys.argv[sys.argv.index('--') + 1:]
OUT, FM = os.path.abspath(a[0]), a[1]; opt = {a[i][2:]: a[i + 1] for i in range(2, len(a) - 1, 2)}
DKG = float(opt.get('dolya_kg', 0.55)); TOL = float(opt.get('tol', 0.012)); MINPL = float(opt.get('min_pl', 8)) * 1e-4; SPLIT = math.radians(float(opt.get('split', 30))); DEV = float(opt.get('dev', 0.01)); MERGE = math.radians(float(opt.get('merge', 8))); SPLITN = math.radians(float(opt.get('split_n', 70))); DEVN = float(opt.get('dev_n', 0.035)); MINPLN = float(opt.get('min_n', 45)) * 1e-4
ZW, ZF = float(opt.get('z_kist', 0.955)), float(opt.get('z_stopa', 0.09))
M_ = np.load(FM)
O = [o for o in bpy.data.objects if o.type == 'MESH' and '@' not in o.name]
KOLCA = np.zeros((0, 3))
for f_ in filter(None, opt.get('pasporta', '').split(',')):
    KOLCA = np.concatenate([KOLCA, np.array([[-u[0], -u[2], u[1]] for u in json.load(open(f_, encoding='utf-8'))['seam']['ring_m']])])


# ПОЛОВИНА: всё строится на x > 0 и отражается целиком в конце (вместе с весами): симметрия топологии точная, база несимметрична на сантиметры
for o in O:
    bm = bmesh.new(); bm.from_mesh(o.data); bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], dist=1e-6, plane_co=(0, 0, 0), plane_no=(1, 0, 0), clear_inner=True); bmesh.ops.triangulate(bm, faces=bm.faces[:])  # центр многоугольника и центр его треугольника разные: иначе на стыке зон теряется полоса
    if o.data.shape_keys:
        sh = bm.verts.layers.shape.get(o.data.shape_keys.key_blocks[0].name)
        for v in bm.verts: v[sh] = v.co
    bm.to_mesh(o.data); bm.free(); o.data.update()


def v_zone(o, c):
    """грань в зоне тела? c — мировой центр"""
    if o.name in ('голова', 'Чутьё'): return False
    if o.name == 'Руки': return c.z >= ZW
    if o.name == 'Ноги': return c.z >= ZF
    return True


# ПЛОТНАЯ БАЗА: снимок для весов и принадлежности
B_V, B_F, B_O = [], [], []
GR = {}
for k, o in enumerate(O):
    n0 = len(B_V); B_V += [o.matrix_world @ v.co for v in o.data.vertices]; o.data.calc_loop_triangles()
    for t in o.data.loop_triangles: B_F.append([n0 + q for q in t.vertices]); B_O.append(k)
    im = {g.index: g.name for g in o.vertex_groups}
    for v in o.data.vertices: GR[n0 + v.index] = {im[g.group]: g.weight for g in v.groups if im[g.group] != '~держать'}
bvh0 = BVHTree.FromPolygons(B_V, B_F, all_triangles=True); B_VA = np.array([tuple(p) for p in B_V])


def vesa_v(p):
    q, _, fi, _ = bvh0.find_nearest(p); t = B_F[fi]; A, B, C = [B_V[j] for j in t]
    v0, v1, v2 = B - A, C - A, q - A; d00, d01, d11, d20, d21 = v0.dot(v0), v0.dot(v1), v1.dot(v1), v2.dot(v0), v2.dot(v1); den = max(d00 * d11 - d01 * d01, 1e-14)
    w1 = (d11 * d20 - d01 * d21) / den; w2 = (d00 * d21 - d01 * d20) / den; bar = (1 - w1 - w2, w1, w2); R = {}
    for b_, j in zip(bar, t):
        for n, w in GR[j].items(): R[n] = R.get(n, 0) + max(b_, 0) * w
    return R


# 1. КЕПКА: голова, кисти, стопы прореживаются на месте; кольца держатся
for o in O:
    for x in bpy.context.view_layer.objects: x.select_set(False)
    bpy.context.view_layer.objects.active = o; o.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='DESELECT'); bm = bmesh.from_edit_mesh(o.data); M = o.matrix_world; n = 0
    Vo = np.array([tuple(M @ v.co) for v in bm.verts]); drz = {v.index for v in bm.verts if any(e.is_boundary for e in v.link_edges)}
    T_ = np.concatenate([np.array([tuple(x.matrix_world @ v.co) for v in x.data.vertices]) for x in O if x != o])
    drz |= set(np.nonzero(np.linalg.norm(Vo[:, None] - T_[None], axis=2).min(1) < 2e-4)[0].tolist())
    if len(KOLCA): drz |= set(np.nonzero(np.linalg.norm(Vo[:, None] - KOLCA[None], axis=2).min(1) < 2.5e-3)[0].tolist())
    for f in bm.faces:
        s = not v_zone(o, M @ f.calc_center_median()); f.select_set(s); n += s
    bm.select_flush(True)
    for v in bm.verts:   # стык с зоной тела
        fl = [f.select for f in v.link_faces]
        if fl and any(fl) and not all(fl): drz.add(v.index)
    bmesh.update_edit_mesh(o.data); bpy.ops.object.mode_set(mode='OBJECT')
    r_ob = float(opt.get('dolya_g', DKG)) if o.name in ('голова', 'Чутьё') else DKG
    if n and r_ob < 0.999:
        g = o.vertex_groups.new(name='~держать'); g.add(list(drz), 1.0, 'REPLACE'); o.vertex_groups.active = g
        bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.decimate(ratio=r_ob, use_vertex_group=True, vertex_group_factor=1000, invert_vertex_group=True); bpy.ops.object.mode_set(mode='OBJECT')
        o.vertex_groups.remove(o.vertex_groups['~держать'])

# 2. ЗОНА ТЕЛА: сварка и дробление
P_ = []; idx = {}; Fz = []
def vid_v(p):
    k = (round(p.x, 4), round(p.y, 4), round(p.z, 4))
    if k not in idx: idx[k] = len(P_); P_.append(p.copy())
    return idx[k]
for o in O:
    M = o.matrix_world; o.data.calc_loop_triangles()
    for t in o.data.loop_triangles:
        if v_zone(o, M @ t.center): Fz.append(tuple(vid_v(M @ o.data.vertices[q].co) for q in t.vertices))
bz = bmesh.new(); ori = bz.verts.layers.int.new('ori'); vs = [bz.verts.new(p) for p in P_]
for i, v in enumerate(vs): v[ori] = i + 1
for f in Fz:
    try: bz.faces.new([vs[q] for q in f])
    except ValueError: pass
bz.normal_update(); bmesh.ops.subdivide_edges(bz, edges=bz.edges[:], cuts=2, use_grid_fill=True); bmesh.ops.triangulate(bz, faces=bz.faces[:]); bz.normal_update()
bz.faces.ensure_lookup_table(); bz.verts.ensure_lookup_table(); bz.edges.ensure_lookup_table()
NF = len(bz.faces); C = np.array([tuple(f.calc_center_median()) for f in bz.faces]); N = np.array([tuple(f.normal) for f in bz.faces]); A = np.array([f.calc_area() for f in bz.faces])
FO = np.zeros(NF, int)
for i in range(NF): FO[i] = B_O[bvh0.find_nearest(Vector(C[i]))[2]]
print('ТЕЛО: зона %d → %d граней (дробление)' % (len(Fz), NF))

def tps(X, Y, lam=1e-4):
    """сплайн тонкой пластины: в Python Blender'а нет scipy"""
    def U(a_, b_):
        r = np.linalg.norm(a_[:, None] - b_[None], axis=2); return np.where(r > 0, r * r * np.log(np.maximum(r, 1e-12)), 0.0)
    n = len(X); P = np.c_[np.ones(n), X]; K_ = U(X, X) + lam * np.eye(n)
    S_ = np.linalg.solve(np.r_[np.c_[K_, P], np.c_[P.T, np.zeros((3, 3))]], np.r_[Y, np.zeros((3, Y.shape[1]))])
    return lambda q: U(q, X).dot(S_[:n]) + np.c_[np.ones(len(q)), q].dot(S_[n:])


# 3. МЕТКИ: опорные пары «тело → пиксель листа», половина листа
S = 435.7
FR = [(0, 1.85, 219, 23), (0, 1.525, 219, 178), (0.075, 1.53, 251, 172), (0.255, 1.47, 334, 205), (0.20, 1.53, 310, 178), (0.322, 1.15, 364, 325), (0.215, 1.30, 292, 262), (0.27, 1.17, 330, 330), (0.345, 0.93, 374, 425), (0.30, 0.95, 342, 425), (0.125, 1.15, 281, 325), (0.15, 1.06, 283, 365), (0.17, 0.90, 303, 470), (0, 1.285, 219, 262), (0, 1.10, 219, 350), (0, 0.885, 219, 490), (0.195, 0.53, 303, 590), (0.085, 0.53, 255, 590), (0.225, 0.12, 322, 770), (0.16, 0.12, 290, 770), (0.29, 0.01, 352, 822), (0.14, 0.01, 285, 822)]
PR = [(0.075, 1.85, 540, 23), (0.08, 1.72, 584, 70), (-0.125, 1.37, 495, 240), (0.125, 1.44, 618, 215), (-0.11, 1.10, 497, 360), (0.11, 1.10, 590, 365), (0.105, 0.95, 606, 430), (-0.07, 0.95, 495, 440), (-0.06, 0.52, 530, 605), (0.075, 0.52, 595, 605), (-0.02, 0.33, 548, 690), (0.12, 0.33, 612, 680), (0.02, 0.10, 570, 780), (0.11, 0.03, 610, 815), (-0.25, 0.01, 480, 822), (0.02, 1.53, 545, 175), (-0.03, 1.15, 530, 330), (0.10, 1.15, 610, 325), (-0.02, 0.92, 530, 430), (0.08, 0.92, 575, 430)]
sp_f = tps(np.array([q[:2] for q in FR], float), np.array([[q[2] - 219, q[3]] for q in FR], float))
sp_p = tps(np.array([q[:2] for q in PR], float), np.array([q[2:] for q in PR], float))
CF, CB = 219, 1233
def proba(kl, px, py):
    L = M_[kl]; x0 = int(M_[kl + '_kor'][0]); h, w = L.shape; ix = np.clip(np.round(px - x0).astype(int), 0, w - 1); iy = np.clip(np.round(py).astype(int), 0, h - 1); return L[iy, ix]
XA, Y_, ZA = np.abs(C[:, 0]), C[:, 1], C[:, 2]
pf = sp_f(np.c_[XA, ZA]); pp = sp_p(np.c_[Y_, ZA])
Lf = proba('front', CF + np.abs(pf[:, 0]), pf[:, 1]); Lb = proba('back', CB - np.abs(pf[:, 0]), pf[:, 1]); Lp = proba('profile', pp[:, 0], pp[:, 1])
Ok = np.array([o.name for o in O]); imo = Ok[FO]
konech = (imo == 'Ноги') | ((imo == 'Руки') & (XA > 0.2))
pro = konech & (np.abs(N[:, 0]) > np.abs(N[:, 1]) * 1.0)
if opt.get('otladka'): np.savez(opt['otladka'], pf=pf, pp=pp, C=C, N=N)
vid = np.where(pro, 2, np.where(N[:, 1] < 0, 0, 1)); L0 = np.where(vid == 0, Lf, np.where(vid == 1, Lb, Lp)); METKA = vid * 10000 + L0
# ПАНЕЛИ ТОРСА: перед и спина заданы рукой с листа поверх границ объектов (`--paneli`): одна область, свои точки и рёбра, симметрия по построению
PAN = json.load(open(opt['paneli'], encoding='utf-8')) if opt.get('paneli') else []
PMET = 900000
for i_, p_ in enumerate(PAN):
    m = (bool(p_.get('konech', 0)) | (~konech)) & (XA <= p_['x_max']) & (XA >= p_.get('x_min', 0)) & (ZA >= p_['z'][0]) & (ZA <= p_['z'][1]) & (N[:, 1] < -p_.get('ny', 0.3) if p_['vid'] == 'front' else (N[:, 1] > p_.get('ny', 0.3) if p_['vid'] == 'back' else N[:, 0] > 0.45)) & ((imo != 'Руки') | (not p_.get('konech', 0)) | (bool(p_.get('ruki', 0)) & (ZA >= p_.get('ruki_z', 0))))
    METKA[m] = PMET + i_
PM = METKA >= PMET

# 4. ОБЛАСТИ
FA = [[] for _ in range(NF)]
for f in bz.faces:
    for e in f.edges:
        for g in e.link_faces:
            if g is not f: FA[f.index].append(g.index)
REG = -np.ones(NF, int); nr = 0
for i in range(NF):
    if REG[i] >= 0: continue
    REG[i] = nr; st = [i]
    while st:
        c = st.pop()
        for j in FA[c]:
            if REG[j] < 0 and METKA[j] == METKA[i]: REG[j] = nr; st.append(j)
    nr += 1
print('ТЕЛО: связных меток %d' % nr)


def sv_regs():
    """слить по таблице перенумерованно"""
    u = np.unique(REG); m = {v: i for i, v in enumerate(u)}; return np.array([m[r] for r in REG]), len(u)


def sosedi():
    D = {}
    for i in range(NF):
        for j in FA[i]:
            if j > i and REG[i] != REG[j]:
                k = (min(REG[i], REG[j]), max(REG[i], REG[j])); D[k] = D.get(k, 0) + 1
    return D


def stat():
    n = REG.max() + 1; Ar = np.bincount(REG, A, n); Nm = np.zeros((n, 3))
    for k in range(3): Nm[:, k] = np.bincount(REG, A * N[:, k], n)
    ln = np.linalg.norm(Nm, axis=1); Nu = Nm / np.maximum(ln, 1e-12)[:, None]; return Ar, Nu


def rastvorit():
    global REG
    for _ in range(40):
        REG, n = sv_regs(); Ar, Nu = stat(); D = sosedi(); best = {}; NOG = np.bincount(REG, A * ((imo == 'Ноги') & (ZA > 0.1)), n) > 0.5 * Ar
        for (a_, b_), c in D.items():
            s = c * (1 + Nu[a_].dot(Nu[b_]))
            for x, y in ((a_, b_), (b_, a_)):
                if Ar[x] < (MINPLN if NOG[x] else MINPL) and (x not in best or s > best[x][0]): best[x] = (s, y)
        if not best: break
        for x in sorted(best, key=lambda q: Ar[q]):
            REG[REG == x] = best[x][1]


def slit():
    global REG
    REG, n = sv_regs(); Ar, Nu = stat(); D = sosedi(); par = list(range(n)); PR_ = {r for r in range(n) if PM[REG == r].mean() > 0.5}
    def nai(x):
        while par[x] != x: par[x] = par[par[x]]; x = par[x]
        return x
    for (a_, b_) in sorted(D, key=lambda k: -Nu[k[0]].dot(Nu[k[1]])):
        ug = math.acos(min(1, Nu[a_].dot(Nu[b_])))
        if ug < MERGE and nai(a_) != nai(b_) and a_ not in PR_ and b_ not in PR_: par[nai(a_)] = nai(b_)
    REG = np.array([nai(r) for r in REG])


def razbros(r):
    I = np.nonzero(REG == r)[0]; w = A[I]; n_ = (N[I] * w[:, None]).sum(0); n_ /= max(np.linalg.norm(n_), 1e-12); ug = np.arccos(np.clip(N[I].dot(n_), -1, 1)); c_ = (C[I] * w[:, None]).sum(0) / w.sum(); d_ = np.abs((C[I] - c_).dot(n_))
    return float(np.sort(ug)[int(len(I) * 0.9)]), float(np.sort(d_)[int(len(I) * 0.95)]), I, n_


def razdelit():
    global REG
    nd = 0
    for _ in range(8):
        REG, n = sv_regs(); nov = n; est = False
        for r in range(n):
            u_, d_, I, n_ = razbros(r)
            ng = ((imo[I] == 'Ноги') & (ZA[I] > 0.1)).mean() > 0.5
            if len(I) < 12 or A[I].sum() < MINPL * 2 or (u_ <= (SPLITN if ng else SPLIT) and d_ <= (DEVN if ng else DEV)) or PM[I].mean() > 0.5: continue
            s0 = N[I[np.argmin(N[I].dot(n_))]]; s1 = N[I[np.argmin(N[I].dot(s0))]]
            for _i in range(5):
                m = N[I].dot(s0) >= N[I].dot(s1); s0 = (N[I][m] * A[I][m][:, None]).sum(0) if m.any() else s0; s1 = (N[I][~m] * A[I][~m][:, None]).sum(0) if (~m).any() else s1
                s0 /= max(np.linalg.norm(s0), 1e-12); s1 /= max(np.linalg.norm(s1), 1e-12)
            if m.all() or (~m).all(): continue
            REG[I[~m]] = nov; nov += 1; est = True; nd += 1
        if not est: break
    return nd


rastvorit(); slit()
for _ in range(3):
    nd = razdelit(); rastvorit(); REG, nr = sv_regs()
print('ТЕЛО: областей %d' % nr)


# связность после слияний: одна область — один кусок
def svyaz():
    global REG
    seen = np.zeros(NF, bool); nov = REG.copy(); k = REG.max() + 1; used = set()
    for i in range(NF):
        if seen[i]: continue
        st = [i]; seen[i] = True
        while st:
            c = st.pop()
            for j in FA[c]:
                if not seen[j] and REG[j] == REG[i]: seen[j] = True; st.append(j)
                if REG[j] == REG[i] and not seen[j]: pass
        if REG[i] in used:
            # второй кусок той же области получает свой номер
            cur = [i]; mk = {i}
            while cur:
                c = cur.pop()
                for j in FA[c]:
                    if REG[j] == REG[i] and j not in mk: mk.add(j); cur.append(j)
            for j in mk: nov[j] = k
            k += 1
        used.add(REG[i])
    REG = nov


svyaz(); rastvorit(); REG, nr = sv_regs()

# 5. ЦЕПИ: граница областей упрощается один раз на пару областей
VP = np.array([tuple(v.co) for v in bz.verts]); VORI = np.array([v[ori] for v in bz.verts])
Vr = [set() for _ in bz.verts]; adj = {}
for e in bz.edges:
    lf = e.link_faces
    r_ = {REG[f.index] for f in lf}
    otkr = len(lf) == 1
    if len(r_) > 1 or otkr:
        a_, b_ = e.verts[0].index, e.verts[1].index; adj.setdefault(a_, []).append(b_); adj.setdefault(b_, []).append(a_)
        rr = set(r_) | ({-1} if otkr else set()); Vr[a_] |= rr; Vr[b_] |= rr
KRAJ = {e.verts[q].index for e in bz.edges if len(e.link_faces) == 1 for q in (0, 1)}
OS_ = {v for v in KRAJ if abs(VP[v][0]) < 1e-4}
UG = {v for v in adj if len(Vr[v]) >= 3 or len(adj[v]) != 2 or (v in KRAJ and VORI[v] > 0 and v not in OS_)}
# точки панелей на оси — углы цепи оси
PC = {}
osl = sorted(OS_)
for pan in PAN:
    for x, z, dy in pan['tochki']:
        if x != 0: continue
        cand = [v for v in osl if (VP[v][1] > 0) == (pan['vid'] == 'back')]
        if cand:
            v = min(cand, key=lambda q: abs(VP[q][2] - z))
            if abs(VP[v][2] - z) < 0.03: UG.add(v); PC[(pan['imya'], z)] = v


def dp(ch):
    """Дуглас — Пекер по индексам вершин цепи; край зоны — только свои исходные вершины, без потерь"""
    if len(ch) < 3: return ch
    P = VP[ch]; ot = P[-1] - P[0]; L = np.linalg.norm(ot)
    if L < 1e-9: d = np.linalg.norm(P - P[0], axis=1)
    else: d = np.linalg.norm(np.cross(P - P[0], ot / L), axis=1)
    i = int(d.argmax())
    if d[i] < TOL: return [ch[0], ch[-1]]
    return dp(ch[:i + 1])[:-1] + dp(ch[i:])


CH = []; vz = set()
for u in UG:
    for w in adj[u]:
        k = (min(u, w), max(u, w))
        if k in vz: continue
        ch = [u, w]; vz.add(k); pr, cu = u, w
        while cu not in UG:
            nx = [x for x in adj[cu] if x != pr][0]; vz.add((min(cu, nx), max(cu, nx))); ch.append(nx); pr, cu = cu, nx
        CH.append(ch)
for u in list(adj):   # замкнутые цепи без углов
    for w in adj[u]:
        k = (min(u, w), max(u, w))
        if k in vz: continue
        ch = [u, w]; vz.add(k); pr, cu = u, w
        while cu != u:
            nx = [x for x in adj[cu] if x != pr][0]; vz.add((min(cu, nx), max(cu, nx))); ch.append(nx); pr, cu = cu, nx
        t_ = len(ch) // 3; CH += [ch[:t_ + 1], ch[t_:2 * t_ + 1], ch[2 * t_:]]
PR_CH = []; para = {}
for ch in CH:
    s = dp(ch) if not (ch[0] in KRAJ and ch[-1] in KRAJ and all(c in KRAJ for c in ch)) or all(c in OS_ for c in ch) else [c for c in ch if VORI[c] > 0 or c in (ch[0], ch[-1])]
    PR_CH.append(s); k = (min(s[0], s[-1]), max(s[0], s[-1]))
    if len(s) == 2: para.setdefault(k, []).append(len(PR_CH) - 1)
for k, li in para.items():
    if len(li) > 1:     # две прямые между одними углами схлопнули бы область
        for i_ in li:
            ch = CH[i_]; P = VP[ch]; ot = P[-1] - P[0]; L = max(np.linalg.norm(ot), 1e-9); j = int(np.linalg.norm(np.cross(P - P[0], ot / L), axis=1).argmax())
            if 0 < j < len(ch) - 1: PR_CH[i_] = [ch[0], ch[j], ch[-1]]
RS = {}   # область → отрезки контура
ch_r = {}
for e in bz.edges:
    r_ = {REG[f.index] for f in e.link_faces}
    if len(r_) > 1 or len(e.link_faces) == 1:
        ch_r[(min(e.verts[0].index, e.verts[1].index), max(e.verts[0].index, e.verts[1].index))] = r_
for ch, s in zip(CH, PR_CH):
    r_ = ch_r[(min(ch[0], ch[1]), max(ch[0], ch[1]))]
    for r in r_:
        RS.setdefault(r, []).extend(zip(s, s[1:]))

# 6. ЗАПОЛНЕНИЕ
TRI = {}; plohie = 0; PNV = set(); PLOC = {}
VP0 = [Vector(p) for p in VP]; FV = [[v.index for v in f.verts] for f in bz.faces]


def reg_bvh(r):
    """обход грани сверяется только с плотными гранями своей области: у подмышки ближайшей поверхностью иначе оказывается рука"""
    return BVHTree.FromPolygons(VP0, [FV[i] for i in np.nonzero(REG == r)[0]], all_triangles=True)




def zapolnit_panel(r, pan, seg, ids, n_):
    """панель: контур области + точки листа с обеих сторон; триангуляция в проекции ракурса (x, z), рёбра панели — ограничения"""
    global VP
    ys = 1.0 if pan['vid'] == 'back' else -1.0; PRF = pan['vid'] == 'profile'; ko = 1 if PRF else 0; P2 = [Vector((VP[q][ko], VP[q][2])) for q in ids]; S2 = [(P2[ids.index(x)], P2[ids.index(y)]) for x, y in seg]
    def vnutri(cx, cy):
        c = 0
        for p_, q_ in S2:
            if (p_.y > cy) != (q_.y > cy) and cx < (q_.x - p_.x) * (cy - p_.y) / (q_.y - p_.y) + p_.x: c += 1
        return c % 2 == 1
    nom = {}; loc = list(ids); ot = 0; sl_ = 0; BLIZ = float(opt.get('bliz', 0.012))
    for ti, (x, z, dy) in enumerate(pan['tochki']):
        if x == 0:
            v = PC.get((pan['imya'], z))
            if v is None or v not in ids: ot += 1; continue
            nom[(ti, 1)] = ids.index(v); VP[v] = VP[v] + np.array([0, ys * dy, 0]); PNV.add(v); continue
        if not vnutri(x, z): ot += 1; continue
        h = bvh0.ray_cast(Vector((1.0, x, z)), Vector((-1, 0, 0))) if PRF else bvh0.ray_cast(Vector((x, ys * 2, z)), Vector((0, -ys, 0)))
        if h[0] is None: ot += 1; continue
        p2 = Vector((x, z)); bl = min(range(len(P2)), key=lambda q_: (P2[q_] - p2).length)
        if (P2[bl] - p2).length < BLIZ: nom[(ti, 1)] = bl; sl_ += 1; continue   # точка у контура или у соседней: одна вершина, без щепок
        p = np.array(tuple(h[0])) + (np.array([dy, 0, 0]) if PRF else np.array([0, ys * dy, 0])); VP = np.vstack([VP, p]); q = len(VP) - 1; PNV.add(q); nom[(ti, 1)] = len(loc); loc.append(q); P2.append(p2)
    ed = [(ids.index(x), ids.index(y)) for x, y in seg]; re_ = 0
    for i_, j_ in pan.get('rebra', []):
        if (i_, 1) in nom and (j_, 1) in nom and nom[(i_, 1)] != nom[(j_, 1)]:
            PLOC.setdefault(r, []).append((loc[nom[(i_, 1)]], loc[nom[(j_, 1)]]))
            A1, B1 = P2[nom[(i_, 1)]], P2[nom[(j_, 1)]]
            def per(p_, q_):   # ребро листа, пересекающее контур области, не идёт: иначе новая вершина и дыра
                d1 = (q_ - p_).cross(A1 - p_); d2 = (q_ - p_).cross(B1 - p_); d3 = (B1 - A1).cross(p_ - A1); d4 = (B1 - A1).cross(q_ - A1)
                return d1 * d2 < -1e-12 and d3 * d4 < -1e-12
            if any(per(p_, q_) for p_, q_ in S2): re_ += 1; continue
            ed.append((nom[(i_, 1)], nom[(j_, 1)]))
    # цепь с висячим концом панели не отделяет: конец дотягивается до контура, чтобы панель между цепями замкнулась
    if opt.get('zamknut') and pan['imya'] != 'низ сзади':   # на ноге сзади дотянутая цепь складывала грань у колена
        st_ = {}
        for x_, y_ in ed:
            st_[x_] = st_.get(x_, 0) + 1; st_[y_] = st_.get(y_, 0) + 1
        nk_ = len(ids); dz_ = 0
        for v_, c_ in list(st_.items()):
            if c_ != 1 or v_ < nk_: continue
            pv = P2[v_]; pr_ = (P2[0:nk_][0] - pv).length
            kand = sorted(range(nk_), key=lambda q_: (P2[q_] - pv).length)[:6]
            for q_ in kand:
                A1, B1 = pv, P2[q_]
                def per2(p_, q__):
                    d1 = (q__ - p_).cross(A1 - p_); d2 = (q__ - p_).cross(B1 - p_); d3 = (B1 - A1).cross(p_ - A1); d4 = (B1 - A1).cross(q__ - A1); return d1 * d2 < -1e-12 and d3 * d4 < -1e-12
                if (B1 - A1).length > 0.12: break
                if any(per2(P2[x_], P2[y_]) for x_, y_ in ed if v_ not in (x_, y_) and q_ not in (x_, y_)): continue
                ed.append((v_, q_)); PLOC.setdefault(r, []).append((loc[v_], loc[q_])); dz_ += 1; break
        print('ТЕЛО: панель %s: дотянуто концов %d' % (pan['imya'], dz_))
    PLITA = float(opt.get('plita', 0))
    for _pl in range(2):
        ve, _, fa, ov, _, _ = delaunay_2d_cdt(P2, ed, [], 0, 1e-7); T = []; rb_ = reg_bvh(r); pere_ = 0
        for f in fa:
            if any(not ov[q] for q in f): pere_ += 1; continue
            if not vnutri(sum(ve[q].x for q in f) / 3, sum(ve[q].y for q in f) / 3): continue
            t3 = [loc[ov[q][0]] for q in f]; A_, B_, C_ = VP[t3]
            if np.cross(B_ - A_, C_ - A_).dot(np.array(tuple(rb_.find_nearest(Vector((A_ + B_ + C_) / 3))[1]))) < 0: t3 = t3[::-1]
            T.append(t3)
        # плиты: большая грань получает вершину на поверхности, геометрия идёт за массой, а не за шумом
        if _pl == 0 and PLITA > 0:
            for t3 in T:
                pp_ = VP[t3]; c2 = pp_.mean(0); uu = (pp_[1] - pp_[0]); vv = (pp_[2] - pp_[0])
                if np.linalg.norm(np.cross(uu, vv)) / 2 > PLITA * 1e-4:
                    h = bvh0.ray_cast(Vector((c2[0], ys * 2, c2[2])), Vector((0, -ys, 0)))
                    if h[0] is not None:
                        VP = np.vstack([VP, np.array(tuple(h[0]))]); q = len(VP) - 1; PNV.add(q); loc.append(q); P2.append(Vector((c2[0], c2[2])))
        else: break
    print('ТЕЛО: панель %s: точек %d, отброшено %d, слито %d, рёбер снято %d, контур %d, граней %d, с пересечением %d' % (pan['imya'], len(pan['tochki']), ot, sl_, re_, len(ids), len(T), pere_))
    return T


for r, seg in RS.items():
    I = np.nonzero(REG == r)[0]; w_ = A[I]; n_ = (N[I] * w_[:, None]).sum(0); n_ /= max(np.linalg.norm(n_), 1e-12)
    ids = sorted({q for s in seg for q in s}); nm = {q: i for i, q in enumerate(ids)}
    pan = None
    if PM[I].mean() > 0.5: pan = PAN[int(np.bincount(METKA[I][PM[I]] - PMET).argmax())]
    if pan is not None:
        TRI[r] = zapolnit_panel(r, pan, seg, ids, n_); continue
    t_ = np.cross(n_, [0, 0, 1]);
    if np.linalg.norm(t_) < 0.1: t_ = np.cross(n_, [1, 0, 0])
    t_ /= np.linalg.norm(t_); b_ = np.cross(n_, t_); P2 = [Vector((VP[q].dot(t_), VP[q].dot(b_))) for q in ids]
    ve, ed, fa, ov, _, _ = delaunay_2d_cdt(P2, [(nm[x], nm[y]) for x, y in seg], [], 0, 1e-7)
    if any(not o_ for o_ in ov): plohie += 1
    S2 = [(P2[nm[x]], P2[nm[y]]) for x, y in seg]; T = []; rb_ = reg_bvh(r)
    for f in fa:
        if any(not ov[q] for q in f): continue
        cx = sum(ve[q].x for q in f) / 3; cy = sum(ve[q].y for q in f) / 3; pr_ = 0
        for p_, q_ in S2:
            if (p_.y > cy) != (q_.y > cy) and cx < (q_.x - p_.x) * (cy - p_.y) / (q_.y - p_.y) + p_.x: pr_ += 1
        if pr_ % 2:
            t3 = [ids[ov[q][0]] for q in f]; A_, B_, C_ = VP[t3]
            if np.cross(B_ - A_, C_ - A_).dot(np.array(tuple(rb_.find_nearest(Vector((A_ + B_ + C_) / 3))[1]))) < 0: t3 = t3[::-1]
            T.append(t3)
    TRI[r] = T
ntr = sum(len(x) for x in TRI.values()); print('ТЕЛО: областей заполнено %d, зона %d тр, пересечения контура в %d' % (len(TRI), ntr, plohie))

# КОЛЬЦО: граница области упирается в кольцо стыка между его вершинами, и лишняя вершина есть только у зоны (Т-стык, в позе волосяная щель). Такой угол сливается с ближайшей исходной вершиной кольца
KOLc = {}
for r, T_ in TRI.items():
    for t in T_:
        for v in t:
            if v < len(VORI) and v in KRAJ and VORI[v] == 0 and v not in OS_ and v not in KOLc:
                sv = [q for q in KRAJ if VORI[q] > 0 and q not in OS_ and np.linalg.norm(VP[q] - VP[v]) < 0.06]
                if sv: KOLc[v] = min(sv, key=lambda q: np.linalg.norm(VP[q] - VP[v]))
nd_ = 0
for r in TRI:
    nv_ = []
    for t in TRI[r]:
        t2 = [KOLc.get(q, q) for q in t]
        if len(set(t2)) == 3: nv_.append(t2)
        else: nd_ += 1
    TRI[r] = nv_
print('ТЕЛО: кольцо: углов слито %d, граней снято %d' % (len(KOLc), nd_))

# ЗАДЕЛКА: где контур области складывается в проекции, треугольник теряется при заполнении, и в теле дыра. Открытое ребро внутри зоны (край зоны и ось открыты законно) идёт в петлю, петля закрывается веером с обратным обходом
for _prox in range(4):
    # одинаковые грани: пара сокращается (складка нулевого объёма), нечётная остаётся одной
    Cnt = {}
    for r, T_ in TRI.items():
        for t in T_: Cnt.setdefault(tuple(sorted(t)), []).append((r, tuple(t)))
    ubr_ = 0
    for k_, li in Cnt.items():
        if len(li) > 1:
            for r, t in li[1:]:
                TRI[r].remove(list(t)); ubr_ += 1
    if ubr_: print('ТЕЛО: дубли граней сняты: %d' % ubr_)
    Rb = {}
    for r, T_ in TRI.items():
        for t in T_:
            for a_, b_ in zip(t, t[1:] + t[:1]): Rb[(a_, b_)] = r
    OTK = [(a_, b_) for (a_, b_) in Rb if (b_, a_) not in Rb and not (a_ in KRAJ and b_ in KRAJ)]
    Sl = {}
    for a_, b_ in OTK: Sl.setdefault(b_, []).append(a_)
    nz_ = 0; ost_ = 0
    while Sl:
        st = next(iter(Sl)); pet = [st]; cu = st
        while True:
            li = Sl.get(cu)
            if not li: break
            nx = li.pop()
            if not li: del Sl[cu]
            if nx == st: break
            pet.append(nx); cu = nx
        if len(pet) >= 3 and cu != pet[0] or len(pet) >= 3:
            r_ = Rb.get((pet[1], pet[0])) or Rb.get((pet[0], pet[-1])) or next(iter(TRI))
            for i_ in range(1, len(pet) - 1): TRI[r_].append([pet[0], pet[i_], pet[i_ + 1]]); nz_ += 1
        else: ost_ += 1
    print('ТЕЛО: заделка: открытых рёбер %d, граней добавлено %d, незамкнутых петель %d' % (len(OTK), nz_, ost_))
    if not OTK: break

# 5б. ПЛОСКОСТИ: угол области едет к пересечению плоскостей своих областей (как в двойной сети), иначе многоугольник области неплоский и грань рассыпается на оттенки. Край зоны не двигается, сдвиг ограничен: контур держит база
PL = {}
for r in RS:
    I = np.nonzero(REG == r)[0]; w_ = A[I]; n_ = (N[I] * w_[:, None]).sum(0); n_ /= max(np.linalg.norm(n_), 1e-12); PL[r] = (n_, (C[I] * w_[:, None]).sum(0) / w_.sum())
# перепад между областями: нормаль отходит от среднего соседей, тогда пересечение плоскостей даёт ребро резче, чем на базе
KN = float(opt.get('kn', 0))
if KN > 0:
    Sr = {}
    for r_ in ch_r.values():
        if len(r_) == 2:
            a_, b_ = tuple(r_)
            if a_ in PL and b_ in PL: Sr.setdefault(a_, set()).add(b_); Sr.setdefault(b_, set()).add(a_)
    PLn = {}
    for r, (n_, c_) in PL.items():
        if r in Sr:
            ms = np.mean([PL[q][0] for q in Sr[r]], axis=0); n2 = n_ + KN * (n_ - ms); PLn[r] = (n2 / max(np.linalg.norm(n2), 1e-12), c_)
    PL.update(PLn)
# НАКЛОН С ЛИСТА: тон грани листа задаёт её наклон к камере (яркая смотрит на камеру, тёмная отвёрнута), сторона наклона — от нормали базы. Пара области и её зеркала получает общую нормаль: раскладка симметрична. Трусы листа — не кожа, их тон не берётся
BE = float(opt.get('beta', 0)); TV0, TV1 = 0.37, 0.84; TMAX = math.radians(float(opt.get('tilt', 62)))
if BE > 0:
    VS = (np.array([0, -1.0, 0]), np.array([0, 1.0, 0]), np.array([1.0, 0, 0])); KV = ('front', 'back', 'profile'); PN = {}; GR_ = {}
    for r in PL:
        I = np.nonzero(REG == r)[0]; me = np.bincount(METKA[I].astype(np.int64), A[I]).argmax(); v_ = me // 10000; lb = me % 10000; n_, c_ = PL[r]
        cz = (C[I] * A[I][:, None]).sum(0) / A[I].sum(); tv = M_[KV[v_] + '_ton']; t_ = float(tv[lb]) if lb < len(tv) else 0.6
        if 0.72 < cz[2] < 1.07 and abs(cz[0]) < 0.2 or lb == 0: PN[r] = n_; continue
        ax = VS[v_].copy()
        if v_ == 2: ax = np.array([1.0 if cz[0] > 0 else -1.0, 0, 0])
        az = n_ - ax * n_.dot(ax); La = np.linalg.norm(az)
        if La < 1e-3: PN[r] = n_; continue
        az /= La; th = TMAX * float(np.clip((TV1 - t_) / (TV1 - TV0), 0, 1)); nt = ax * math.cos(th) + az * math.sin(th); n2 = (1 - BE) * n_ + BE * nt; PN[r] = n2 / np.linalg.norm(n2)
        GR_.setdefault((me, ), []).append(r)
    Fl = np.array([-1.0, 1, 1])
    for k_, rs in GR_.items():
        if len(rs) < 2: continue
        sg = [1.0 if PL[r][1][0] > 0 else -1.0 for r in rs]; ob = np.mean([PN[r] * (Fl if s_ < 0 else 1) for r, s_ in zip(rs, sg)], axis=0); ob /= max(np.linalg.norm(ob), 1e-12)
        for r, s_ in zip(rs, sg): PN[r] = ob * (Fl if s_ < 0 else 1)
    for r, n_ in PN.items(): PL[r] = (n_, PL[r][1])
SDV = float(opt.get('sdvig', 0.015)); LAM = float(opt.get('lam', 0.03)); dv_ = []
for v in {q for s_ in PR_CH for q in s_}:
    if v in KRAJ: continue
    R_ = [PL[r] for r in Vr[v] if r in PL and not PM[REG == r].mean() > 0.5]
    if len(R_) < 2: continue
    p0 = VP[v]; Aq = LAM * np.eye(3); bq = LAM * p0.copy()
    for n_, c_ in R_: Aq += np.outer(n_, n_); bq += n_ * n_.dot(c_)
    d_ = np.linalg.solve(Aq, bq) - p0; L_ = np.linalg.norm(d_)
    if L_ > SDV: d_ *= SDV / L_
    VP[v] = p0 + d_; dv_.append(min(L_, SDV))
# зеркальные углы — среднее положение
kl_ = sorted(set(q for s_ in PR_CH for q in s_) - KRAJ); Pk = VP[kl_]; mr_ = Pk * np.array([-1.0, 1, 1]); dm = np.linalg.norm(Pk[:, None] - mr_[None], axis=2); nn = dm.argmin(1); npar = 0
for i_, j_ in enumerate(nn):
    if nn[j_] == i_ and dm[i_, j_] < float(opt.get('zerk', 0.03)) and i_ <= j_:
        ob = (Pk[i_] + mr_[j_]) / 2; VP[kl_[i_]] = ob; VP[kl_[j_]] = ob * np.array([-1.0, 1, 1]); npar += 1
print('ТЕЛО: зеркальных пар углов %d из %d' % (npar, len(kl_)))
print('ТЕЛО: углов к плоскостям %d, сдвиг средн %.1f макс %.1f мм' % (len(dv_), np.mean(dv_) * 1000, max(dv_) * 1000))

# 5в. ПЕРЕПАД: рельеф базы при усреднении слабеет, а лист держится на перепаде соседних граней. Угол едет по своей нормали на долю отклонения от среднего соседей (выступ выше, ложбина глубже); край зоны стоит
OST = float(opt.get('ostrota', 0)); SDO = float(opt.get('sdvig_ost', 0.02))
if OST > 0:
    Sos = {}; Nv = {}
    for T in TRI.values():
        for t in T:
            n3 = np.cross(VP[t[1]] - VP[t[0]], VP[t[2]] - VP[t[0]])
            for i_, v in enumerate(t):
                Sos.setdefault(v, set()).update(q for q in t if q != v); Nv[v] = Nv.get(v, 0) + n3
    D_ = {}
    for v, so in Sos.items():
        if v in KRAJ or v in PNV: continue
        n3 = Nv[v] / max(np.linalg.norm(Nv[v]), 1e-12); d_ = (VP[v] - VP[list(so)].mean(0)).dot(n3) * OST; D_[v] = n3 * max(-SDO, min(SDO, d_))
    for v, d_ in D_.items(): VP[v] = VP[v] + d_
    print('ТЕЛО: перепад: углов %d, сдвиг средн %.1f мм' % (len(D_), np.mean([np.linalg.norm(x) for x in D_.values()]) * 1000))

# ПЛОСКОСТЬ ПАНЕЛИ: панель между цепями рёбер — одна масса. Вершины панели едут только по глубине ракурса к плоскостям своих панелей, поэтому контур в анфас не меняется. Мера: разброс нормалей внутри панели (средний угол к средней нормали по площади) и число граней на панель
PLAN = float(opt.get('plan', 0))
def podpaneli(r, T_):
    Zp = {(min(a_, b_), max(a_, b_)) for a_, b_ in PLOC.get(r, ())}; E_ = {}
    for i_, t in enumerate(T_):
        for a_, b_ in zip(t, t[1:] + t[:1]): E_.setdefault((min(a_, b_), max(a_, b_)), []).append(i_)
    so = [[] for _ in T_]
    for k_, li in E_.items():
        if len(li) == 2 and k_ not in Zp: so[li[0]].append(li[1]); so[li[1]].append(li[0])
    vid_ = [False] * len(T_); R_ = []
    for i_ in range(len(T_)):
        if vid_[i_]: continue
        st = [i_]; vid_[i_] = True; c_ = []
        while st:
            q = st.pop(); c_.append(q)
            for w in so[q]:
                if not vid_[w]: vid_[w] = True; st.append(w)
        R_.append(c_)
    return R_
def mera_pan(metka):
    Ug = []; Ng = []; Pl = 0; PoP = {}
    for r, T_ in TRI.items():
        if not PM[REG == r].mean() > 0.5: continue
        im_ = PAN[int(np.bincount(METKA[REG == r][PM[REG == r]] - PMET).argmax())]['imya']
        for c_ in podpaneli(r, T_):
            N_ = []; A_ = []
            for i_ in c_:
                n3 = np.cross(VP[T_[i_][1]] - VP[T_[i_][0]], VP[T_[i_][2]] - VP[T_[i_][0]]); s3 = np.linalg.norm(n3)
                if s3 > 1e-9: N_.append(n3 / s3); A_.append(s3 / 2)
            if not N_: continue
            N_ = np.array(N_); A_ = np.array(A_); mn = (N_ * A_[:, None]).sum(0); mn /= max(np.linalg.norm(mn), 1e-12); u_ = np.degrees(np.arccos(np.clip(N_.dot(mn), -1, 1))); Ug.append(float((u_ * A_).sum() / A_.sum())); Ng.append(len(c_)); Pl += 1; PoP.setdefault(im_, []).append((Ug[-1], len(c_)))
    print('ТЕЛО: панели %s: подпанелей %d, разброс нормалей медиана %.1f°, 90%% %.1f°, гр/подп медиана %.0f, макс %d' % (metka, Pl, np.median(Ug), np.percentile(Ug, 90), np.median(Ng), max(Ng)))
    for k_, v_ in PoP.items(): print('ТЕЛО:   %s %s: подп %d, разброс мед %.1f°, 90%% %.1f°, гр мед %.0f макс %d' % (metka, k_, len(v_), np.median([q[0] for q in v_]), np.percentile([q[0] for q in v_], 90), np.median([q[1] for q in v_]), max(q[1] for q in v_)))
mera_pan('до плоскости')
if PLAN > 0:
    LIM = PLAN; Y0 = {}; sum_ = 0; KONT = set()   # контур панели и ось держат контур тела: стоят
    for r, T_ in TRI.items():
        if not PM[REG == r].mean() > 0.5: continue
        E2 = {}
        for t in T_:
            for a_, b_ in zip(t, t[1:] + t[:1]): E2[(min(a_, b_), max(a_, b_))] = E2.get((min(a_, b_), max(a_, b_)), 0) + 1
        KONT |= {q for k_, n_ in E2.items() if n_ == 1 for q in k_}
    for _it in range(4):
        Sv = {}
        for r, T_ in TRI.items():
            if not PM[REG == r].mean() > 0.5: continue
            for c_ in podpaneli(r, T_):
                Vs = sorted({v for i_ in c_ for v in T_[i_]})
                if len(Vs) < 4: continue
                N_ = np.array([np.cross(VP[T_[i_][1]] - VP[T_[i_][0]], VP[T_[i_][2]] - VP[T_[i_][0]]) for i_ in c_]); mn = N_.sum(0); mn /= max(np.linalg.norm(mn), 1e-12); Nu_ = N_ / np.maximum(np.linalg.norm(N_, axis=1, keepdims=True), 1e-12)
                if np.degrees(np.arccos(np.clip(Nu_.dot(mn), -1, 1))).max() > 55 or abs(mn[1]) < 0.35: continue   # панель огибает тело, плоскостью её не сделать
                Q = VP[Vs]; Aq = np.c_[Q[:, 0], Q[:, 2], np.ones(len(Vs))]; co, *_r = np.linalg.lstsq(Aq, Q[:, 1], rcond=None)
                ar = sum(np.linalg.norm(np.cross(VP[T_[i_][1]] - VP[T_[i_][0]], VP[T_[i_][2]] - VP[T_[i_][0]])) / 2 for i_ in c_)
                for v, a_ in zip(Vs, Aq): Sv.setdefault(v, []).append((float(a_.dot(co)), ar))
        for v, li in Sv.items():
            if v in KRAJ or v in KONT: continue
            Y0.setdefault(v, VP[v][1]); yy = sum(a_ * b_ for a_, b_ in li) / sum(b_ for _a, b_ in li); VP[v][1] = Y0[v] + float(np.clip(yy - Y0[v], -LIM, LIM))
    dd = [abs(VP[v][1] - y_) for v, y_ in Y0.items()]; print('ТЕЛО: к плоскости: вершин %d, сдвиг по глубине медиана %.1f, макс %.1f мм' % (len(dd), np.median(dd) * 1000, max(dd) * 1000))
    mera_pan('после плоскости')

# ДИАГОНАЛИ: четырёхугольник из двух граней области переворачивается, если так они ближе к одной плоскости (убирает «битое стекло», число граней то же). Рёбра-ограничения панелей и границы областей стоят
FLIP = float(opt.get('flip', 3)); ALONG = int(opt.get('vdol', 0))
if FLIP > 0:
    Zap = set()
    for r, T_ in TRI.items():
        for a_, b_ in PLOC.get(r, ()): Zap.add((min(a_, b_), max(a_, b_)))
    npv = 0
    for _pr in range(6):
        est = 0
        for r, T_ in TRI.items():
            Ed = {}
            for i_, t in enumerate(T_):
                for a_, b_ in zip(t, t[1:] + t[:1]): Ed.setdefault((min(a_, b_), max(a_, b_)), []).append(i_)
            for (a_, b_), li in list(Ed.items()):
                if len(li) != 2 or (a_, b_) in Zap or li[0] == li[1]: continue
                t1, t2 = T_[li[0]], T_[li[1]]
                if not (set((a_, b_)) <= set(t1) and set((a_, b_)) <= set(t2)): continue
                c_ = [q for q in t1 if q not in (a_, b_)][0]; d__ = [q for q in t2 if q not in (a_, b_)][0]
                if c_ == d__: continue
                A_, B_, C_, D_ = VP[a_], VP[b_], VP[c_], VP[d__]
                def nrm(p, q, r_): n = np.cross(q - p, r_ - p); return n / max(np.linalg.norm(n), 1e-12), np.linalg.norm(n)
                i0 = t1.index(c_); tc = t1[i0:] + t1[:i0]; x_, y_ = tc[1], tc[2]; n1, s1 = nrm(VP[c_], VP[x_], VP[y_]); n2, s2 = nrm(VP[d__], VP[y_], VP[x_]); n3, s3 = nrm(VP[c_], VP[x_], VP[d__]); n4, s4 = nrm(VP[d__], VP[y_], VP[c_])
                if s3 < 1e-6 or s4 < 1e-6 or n3.dot(n1) < 0.2 or n4.dot(n1) < 0.2: continue
                if min(s3, s4) / max(s3, s4) < 0.04: continue
                u_st = math.acos(min(1, n1.dot(n2))); u_nv = math.acos(min(1, n3.dot(n4))); osn = VP[b_] - VP[a_]; diag = VP[d__] - VP[c_]; vert = lambda w: abs(w[2]) / max(np.linalg.norm(w), 1e-9)
                # вырожденная грань (высота меньше 3 мм при ребре от 3 см) меняет диагональ всегда; при почти равной плоскости диагональ идёт вдоль массы: на ногах и пояснице это по высоте
                vyr = (s1 / max(np.linalg.norm(osn), 1e-9) < 0.003 or s2 / max(np.linalg.norm(osn), 1e-9) < 0.003) and np.linalg.norm(osn) > 0.03
                vdol = ALONG and PM[REG == r].mean() > 0.5 and VP[c_][2] < 1.3 and u_nv < u_st + math.radians(2.5) and vert(diag) > vert(osn) + 0.2
                if (vyr or vdol or u_nv + math.radians(FLIP) < u_st) and tuple(sorted((c_, d__))) not in Ed:
                    T_[li[0]] = [c_, x_, d__]; T_[li[1]] = [d__, y_, c_]; est += 1; npv += 1; Ed = None; break
            if Ed is None: pass
        if not est: break
    print('ТЕЛО: диагоналей перевёрнуто %d' % npv); mera_pan('после диагоналей')

# СКЛАДКА ГРАНИ: грань вывернута среди всех соседей (--sklad x,y,z; центр грани): короткое ребро схлопывается, грани не вырождаются
for z_ in filter(None, opt.get('sklad', '').split(';')):
    p_ = np.array(list(map(float, z_.split(',')))); best = None
    for r, T_ in TRI.items():
        for i_, t in enumerate(T_):
            d_ = np.linalg.norm(VP[t].mean(0) - p_)
            if best is None or d_ < best[0]: best = (d_, r, i_)
    t = TRI[best[1]][best[2]]; ed_ = sorted(((np.linalg.norm(VP[a_] - VP[b_]), a_, b_) for a_, b_ in zip(t, t[1:] + t[:1])), key=lambda q: q[0])[0]; a_, b_ = ed_[1], ed_[2]
    if a_ in KRAJ: a_, b_ = b_, a_
    print('ТЕЛО: складка грани: центр в %.1f мм, площадь %.1f см², ребро %.1f мм' % (best[0] * 1000, np.linalg.norm(np.cross(VP[t[1]] - VP[t[0]], VP[t[2]] - VP[t[0]])) * 5000, ed_[0] * 1000))
    for r in TRI: TRI[r] = [[b_ if q == a_ else q for q in t2] for t2 in TRI[r] if not (a_ in t2 and b_ in t2)]
# ТОЧЕЧНО: вершина выводится наружу по положению (--vyvod файл: [{"p": [x, y, z], "d": [dx, dy, dz]}]), контур зоны стоит
if opt.get('vyvod'):
    for z_ in json.load(open(opt['vyvod'], encoding='utf-8')):
        vs_ = sorted({q for T_ in TRI.values() for t in T_ for q in t}, key=lambda q: np.linalg.norm(VP[q] - z_['p'])); v = vs_[0]; print('ТЕЛО: вывод: ближайшая %s в %.1f мм, сдвиг %s' % (np.round(VP[v], 3), np.linalg.norm(VP[v] - z_['p']) * 1000, z_['d'])); VP[v] = VP[v] + np.array(z_['d'])
# ШАГ: вершина вплотную к оси садится на ось, а грань целиком в плоскости оси — внутренняя перепонка между половинами, она снимается
nsh = 0
for v in {q for T_ in TRI.values() for t in T_ for q in t}:
    if VP[v][0] != 0 and VP[v][0] < 0.007 and (v not in KRAJ or VP[v][2] < 0.95) and VP[v][2] > 0.1: VP[v][0] = 0.0; nsh += 1   # половина x ≥ 0: сведение углов и перепад уводили вершины шага за ось, после отражения половины заходили друг за друга
npr = 0
for r in TRI:
    n0_ = len(TRI[r]); TRI[r] = [t for t in TRI[r] if not all(abs(VP[q][0]) < 1e-5 for q in t)]; npr += n0_ - len(TRI[r])
print('ТЕЛО: шаг: вершин на ось %d, перепонок снято %d' % (nsh, npr))

# 7. В ОБЪЕКТЫ: зона уходит из объектов, области — большинству площади
BOK = {b.name[:-2].split('.')[0] for a_ in bpy.data.objects if a_.type == 'ARMATURE' for b in a_.data.bones if b.name.endswith('.L')}
ROB = {r: int(np.bincount(FO[REG == r], A[REG == r], len(O)).argmax()) for r in TRI}
# панель режется по объектам потреугольно: иначе торс уходит одному объекту, а остальные пустеют, а объекты — контракт слотов
ROBT = {r: ([B_O[bvh0.find_nearest(Vector(VP[t].mean(0)))[2]] for t in T] if PM[REG == r].mean() > 0.5 else [ROB[r]] * len(T)) for r, T in TRI.items()}
for k, o in enumerate(O):
    M = o.matrix_world; iw = M.inverted(); bm = bmesh.new(); bm.from_mesh(o.data); dl = bm.verts.layers.deform.verify(); sh = bm.verts.layers.shape.get(o.data.shape_keys.key_blocks[0].name) if o.data.shape_keys else None
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if v_zone(o, M @ f.calc_center_median())], context='FACES_ONLY')
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    ID = {(round((M @ v.co).x, 4), round((M @ v.co).y, 4), round((M @ v.co).z, 4)): v for v in bm.verts}; nv = {}
    def vz_(q):
        if q in nv: return nv[q]
        p = Vector(VP[q]); kl = (round(p.x, 4), round(p.y, 4), round(p.z, 4))
        v = ID.get(kl)
        if v is None:
            v = bm.verts.new(iw @ p)
            if sh is not None: v[sh] = iw @ p
            for n_, w_ in vesa_v(p).items():
                if w_ > 1e-4:
                    g = o.vertex_groups.get(n_) or o.vertex_groups.new(name=n_); v[dl][g.index] = w_
        nv[q] = v; return v
    for r, T in TRI.items():
        for i_, t in enumerate(T):
            if ROBT[r][i_] != k: continue
            try: bm.faces.new([vz_(q) for q in t])
            except ValueError: pass
    # обход согласуется на половине, до отражения: на стыке зоны и кисти встречаются разные источники; сторона острова — по большинству площади
    bm.normal_update(); ost_ = set(bm.faces)
    while ost_:
        st = [next(iter(ost_))]; ch_ = set()
        while st:
            f_ = st.pop()
            if f_ in ch_: continue
            ch_.add(f_); st += [g_ for e in f_.edges for g_ in e.link_faces if g_ not in ch_]
        ost_ -= ch_; old_ = {f_: (f_.normal.copy(), f_.calc_area()) for f_ in ch_}
        bmesh.ops.recalc_face_normals(bm, faces=list(ch_)); bm.normal_update()
        if sum(a_ * f_.normal.dot(n_) for f_, (n_, a_) in old_.items()) < 0: bmesh.ops.reverse_faces(bm, faces=list(ch_)); bm.normal_update()
    # отражение половины: веса меняются именами .L ↔ без суффикса
    bm.verts.ensure_lookup_table(); st_ = set(bm.verts); fs_ = set(bm.faces); r_ = bmesh.ops.mirror(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], axis='X', merge_dist=2e-4)
    im_ = {g.index: g.name for g in o.vertex_groups}
    for v in {q for q in r_['geom'] if isinstance(q, bmesh.types.BMVert)} - st_:
        d0 = dict(v[dl]); v[dl].clear()
        for gi, w_ in d0.items():
            n_ = im_[gi]; n2 = n_[:-2] if n_.endswith('.L') else n_ + '.L'
            g = o.vertex_groups.get(n2) or (o.vertex_groups.get(n_) if n_.split('.')[0] not in BOK else None)
            if n_.split('.')[0] in BOK or n_.endswith('.L'):
                g = o.vertex_groups.get(n2) or o.vertex_groups.new(name=n2)
            else: g = o.vertex_groups.get(n_)
            v[dl][g.index] = w_
    bmesh.ops.reverse_faces(bm, faces=[f_ for f_ in bm.faces if (M @ f_.calc_center_median()).x < -1e-6])   # отражение обхода не меняет
    if sh is not None:
        for v in bm.verts: v[sh] = v.co
    bm.to_mesh(o.data); bm.free(); o.data.update()
bz.free()
# ПОСЛЕДНЯЯ ЗАДЕЛКА: открытое ребро без ответа рядом (у стыков объектов ответ есть, расхождение 0.02 мм) — настоящая дыра
def otkr():
    R = []
    for k, o in enumerate(O):
        M = o.matrix_world
        for e in o.data.edges: pass
        bm = bmesh.new(); bm.from_mesh(o.data)
        for e in bm.edges:
            if len(e.link_faces) == 1: R.append((k, e.index, np.array(tuple(M @ e.verts[0].co)), np.array(tuple(M @ e.verts[1].co))))
        bm.free()
    return R
Rk = otkr(); PA = np.array([q[2] for q in Rk]); PB = np.array([q[3] for q in Rk]); Pm = (PA + PB) / 2; d_ = PB - PA; zap = {}
for i_, (k, ei, a_, b_) in enumerate(Rk):
    t_ = np.clip(((Pm[i_] - PA) * d_).sum(1) / np.maximum((d_ * d_).sum(1), 1e-12), 0, 1); D_ = np.linalg.norm(PA + d_ * t_[:, None] - Pm[i_], axis=1); D_[i_] = 9
    if D_.min() > 0.0015 and abs(Pm[i_][0]) > 1e-4 and O[k].name not in ('голова', 'Чутьё'): zap.setdefault(k, []).append(ei)
# петли открытых рёбер идут и через границу объектов: собираются по положению, грань отходит объекту первого ребра
Kl = lambda p: tuple(np.round(p, 4)); Ad = {}; Ob_ = {}
for i_, (k, ei, a_, b_) in enumerate(Rk):
    if ei not in zap.get(k, ()): continue
    A_k, B_k = Kl(a_), Kl(b_); Ad.setdefault(A_k, []).append(B_k); Ad.setdefault(B_k, []).append(A_k); Ob_[frozenset((A_k, B_k))] = k
    PK = None
vz_o = set(); Pet = []
for st_ in list(Ad):
    for nx_ in Ad[st_]:
        if frozenset((st_, nx_)) in vz_o: continue
        pt = [st_]; pr_, cu = st_, nx_; vz_o.add(frozenset((st_, nx_)))
        while cu != st_ and len(pt) < 40:
            pt.append(cu); so_ = [q for q in Ad[cu] if q != pr_ and frozenset((cu, q)) not in vz_o]
            if not so_: cu = None; break
            pr_, cu = cu, so_[0]; vz_o.add(frozenset((pt[-1], cu)))
        if cu == st_ and len(pt) >= 3: Pet.append(pt)
nz2 = 0
for pt in Pet:
    k = Ob_.get(frozenset((pt[0], pt[1])), 0); o = O[k]; M = o.matrix_world; iw = M.inverted(); bm = bmesh.new(); bm.from_mesh(o.data); dl = bm.verts.layers.deform.verify(); sh = bm.verts.layers.shape.get(o.data.shape_keys.key_blocks[0].name) if o.data.shape_keys else None; Id_ = {Kl(np.array(tuple(M @ v.co))): v for v in bm.verts}
    def vv(q):
        v = Id_.get(q)
        if v is not None: return v
        p_ = Vector(q); v = bm.verts.new(iw @ p_)
        if sh is not None: v[sh] = iw @ p_
        sg_ = p_.x < 0
        for n_, w_ in vesa_v(Vector((abs(p_.x), p_.y, p_.z))).items():
            if w_ > 1e-4:
                n2 = (n_[:-2] if n_.endswith('.L') else n_ + '.L') if sg_ and any(b.name == (n_[:-2] if n_.endswith('.L') else n_ + '.L') for a_ in bpy.data.objects if a_.type == 'ARMATURE' for b in a_.data.bones) else n_
                g = o.vertex_groups.get(n2) or o.vertex_groups.new(name=n2); v[dl][g.index] = w_
        Id_[q] = v; return v
    Vs = [vv(q) for q in pt]; bm.verts.index_update()
    for i_ in range(1, len(Vs) - 1):
        try:
            f_ = bm.faces.new([Vs[0], Vs[i_], Vs[i_ + 1]]); f_.normal_update(); c_ = M @ f_.calc_center_median(); n0 = bvh0.find_nearest(Vector((abs(c_.x), c_.y, c_.z)))[1]
            if (M.to_3x3() @ f_.normal).dot(Vector((n0.x * (-1 if c_.x < 0 else 1), n0.y, n0.z))) < 0: f_.normal_flip()
            nz2 += 1
        except ValueError: pass
    bm.to_mesh(o.data); bm.free(); o.data.update()
# угол: два открытых ребра из одной вершины, а третье ребро уже есть: одна грань
def ug_():
    global nz2
    for q, so in list(Ad.items()):
        if len(so) != 2 or len(Ad[so[0]]) != 1 or len(Ad[so[1]]) != 1: continue
        k = Ob_.get(frozenset((q, so[0])), 0); o = O[k]; M = o.matrix_world; iw = M.inverted(); bm = bmesh.new(); bm.from_mesh(o.data); dl = bm.verts.layers.deform.verify(); sh = bm.verts.layers.shape.get(o.data.shape_keys.key_blocks[0].name) if o.data.shape_keys else None; Id_ = {Kl(np.array(tuple(M @ v.co))): v for v in bm.verts}
        Vs = []
        for p_ in (q, so[0], so[1]):
            v = Id_.get(p_)
            if v is None:
                pp_ = Vector(p_); v = bm.verts.new(iw @ pp_)
                if sh is not None: v[sh] = iw @ pp_
                nb = min((x for x in bm.verts if x is not v), key=lambda x: (x.co - v.co).length)
                for gi, w_ in nb[dl].items(): v[dl][gi] = w_
            Vs.append(v)
        try:
            f_ = bm.faces.new(Vs); f_.normal_update(); c_ = M @ f_.calc_center_median(); n0 = bvh0.find_nearest(Vector((abs(c_.x), c_.y, c_.z)))[1]
            if (M.to_3x3() @ f_.normal).dot(Vector((n0.x * (-1 if c_.x < 0 else 1), n0.y, n0.z))) < 0: f_.normal_flip()
            nz2 += 1
        except ValueError: pass
        bm.to_mesh(o.data); bm.free(); o.data.update()
ug_()
print('ТЕЛО: последняя заделка: открытых рёбер %d, петель %d, граней %d' % (sum(len(v) for v in zap.values()), len(Pet), nz2))
# СКЛАДКА: вершина внутри перекрывающегося веера (ребро с тремя гранями): вершина снимается вместе со своими гранями, их контур уже закрыт соседними гранями. Какая — числом из разбора (`--ubrat_v x,y,z`, обе стороны)
if opt.get('ubrat_v'):
    x_, y_, z_ = map(float, opt['ubrat_v'].split(','))
    for o in O:
        M = o.matrix_world; bm = bmesh.new(); bm.from_mesh(o.data); uv = [v for v in bm.verts if abs(abs((M @ v.co).x) - x_) < 0.006 and abs((M @ v.co).y - y_) < 0.006 and abs((M @ v.co).z - z_) < 0.006]
        if uv:
            n3 = sum(1 for e in bm.edges if len(e.link_faces) > 2); nf = sum(len(v.link_faces) for v in uv); bmesh.ops.delete(bm, geom=uv, context='VERTS'); n4 = sum(1 for e in bm.edges if len(e.link_faces) > 2)
            print('ТЕЛО: складка %s: снято вершин %d, граней %d; рёбер с тремя гранями %d → %d' % (o.name, len(uv), nf, n3, n4)); bm.to_mesh(o.data); o.data.update()
        bm.free()
print('ТЕЛО: итог %d тр: %s' % (sum(len(o.data.polygons) for o in O), {o.name: len(o.data.polygons) for o in O}))
bpy.ops.wm.save_as_mainfile(filepath=OUT)
