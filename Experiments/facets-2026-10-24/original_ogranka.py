"""ОГРАНКА ПО ОБЪЁМАМ (21.10, шаг 6 Д консилиума; решение геймдизайнера по r12: «крупные чистые грани, как на листе; грудь, пресс,
дельты, бедро — гранями по объёмам; у гривы и хвоста — крупные грани по прядям»). Форма-ключ после подгонки несёт шум цели:
соседние грани смотрят в разные стороны, и плоская заливка рисует щепу. Огранка собирает грани в области и выпрямляет каждую в
плоскость:

  1. области: `--rezhim myshcy` — КАРТА МЫШЦ шаблона К2 (`myshca`: ~25 мышц на сторону по анатомическим границам в базовых
     координатах К2, до ключа — карта привязана к топологии и годится под любой ключ), внутри мышцы 1–6 плоскостей k-средних по
     нормали сглаженной копии (число — площадь / `--ploshchad_ploskosti`); без режима — k-средних по [центр / `--masshtab`,
     нормаль] на всю поверхность (`--ploshchad` м² на область);
  2. область — связный кусок; островки мельче `--min_oblast` м² сливаются с соседом;
  3. вершина тянется к плоскостям своих граней (среднее проекций) с силой `--sila`, `--iter` раз; `--lap` держит исходный
     лапласиан (0 — не держит: исходный лапласиан и есть шум).
  ТЕЛО — ОДНОЙ СЕТКОЙ: объекты шасси сшиты по базовым положениям вершин (шов шея–голова, рука–корпус — общие вершины), неподвижен
  только открытый край; раньше край каждого объекта стоял, и на стыке выпрямленных областей торчали шипы («крылья» у плеч).
  Куски `@` (`--kuski 1`) — каждый своей сеткой, режим k-средних с площадью `--ploshchad_kuski`.
  (Режим «кость × участок × сектор» снят 21.10: резал мышцы поперёк, полосы на груди — критик r13.)

Правится только форма-ключ `--imya` (базовая форма К2 — нет) и сетка кусков.

  blender -b --factory-startup -P ogranka.py -- вход.blend выход.blend [--imya Волк] [--rezhim myshcy] [--sila 1] [--iter 30] [--lap 0]
      [--ploshchad_ploskosti 0.008] [--min_oblast 0.003] [--ploshchad 0.006] [--masshtab 0.08] [--bez голова,Чутьё] [--kuski 1 [--ploshchad_kuski 0.01]]
"""
import sys
import bpy
import numpy as np
from mathutils import Vector

a = sys.argv[sys.argv.index('--') + 1:]
src, out = a[0], a[1]
opt = dict(imya='Волк', ploshchad_kuski='0.01', ploshchad='0.006', masshtab='0.08', sila='0.7', iter='6', bez='голова,Чутьё',
           lap='0.3', min_oblast='0.002', ploshchad_ploskosti='0.008')
i = 2
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
NAME = opt['imya']; BEZ = set(opt['bez'].split(',')) if opt['bez'] else set()
rng = np.random.default_rng(7)

bpy.ops.wm.open_mainfile(filepath=src)
ARM = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]; AW = ARM.matrix_world


def u_ang(c, bn):
    """доля по длине кости и угол вокруг оси: 0 — вперёд, 90 — наружу, 270 — внутрь (зеркально по стороне)"""
    pb = ARM.data.bones.get(bn)
    h = np.array(list(AW @ pb.head_local)); t = np.array(list(AW @ pb.tail_local)); ax = t - h; L2 = max(ax @ ax, 1e-9)
    u = float(np.clip((c - h) @ ax / L2, 0, 0.999)); r = c - (h + ax * u); a_ = ax / np.sqrt(L2)
    fw = np.array([0, -1.0, 0]); fw = fw - a_ * (fw @ a_); fw /= max(np.linalg.norm(fw), 1e-9); sd = np.cross(a_, fw)
    sg = -1.0 if bn.endswith('.L') else 1.0
    return u, np.degrees((np.arctan2(sg * (r @ sd), r @ fw) + 2 * np.pi) % (2 * np.pi))


def sektor(ang, granicy):
    """номер сектора по списку границ в градусах (по кругу)"""
    for i, g in enumerate(granicy):
        if ang < g: return i
    return 0


def myshca(c, bn, obj):
    x, y, z = c; ax_ = abs(x); sd = 'L' if x > 0 else 'R'; b = bn.split('.')[0]
    if b == 'стопа': return ('стопа', tuple(np.round(c, 4)))     # не трогается: ось кости стопы в покое горизонтальна, а форма —
                                                                 # пальцеходящая плюсна; каждая грань — своя область
    front = y < 0.01
    if b in ('плечо',):
        u, a = u_ang(c, bn)
        if u < 0.3: return (sd, 'дельта', sektor(a, [50, 130, 360]))           # передний / средний / задний пучок
        s_ = sektor(a, [60, 120, 240, 300, 360])                               # бицепс / наружная / трицепс / внутр. / бицепс
        s_ = 0 if s_ == 4 else s_
        return (sd, 'плечо', s_, int(u > 0.62))
    if b == 'предплечье':
        u, a = u_ang(c, bn)
        return (sd, 'предплечье', sektor(a, [90, 270, 360]) % 2, int(u > 0.45))  # разгибатели снаружи / сгибатели внутри
    if b == 'лопатка':
        if front: return (sd, 'грудная', 'верх')
        return (sd, 'трапеция', int(z > 1.5))
    if b == 'бедро':
        u, a = u_ang(c, bn)
        if a < 40 or a > 330: return (sd, 'RF', int(u > 0.5))
        if a < 140: return (sd, 'VL' if u < 0.7 else 'VL_низ')
        if a < 220: return (sd, 'задняя', int(u > 0.5), int(a > 180))
        if u > 0.6: return (sd, 'VM')                                          # капля над коленом
        return (sd, 'приводящие', int(u > 0.3))
    if b == 'голень':
        u, a = u_ang(c, bn)
        if u < 0.12: return (sd, 'колено', int(90 < a < 270))
        if 120 < a < 240 and u < 0.55: return (sd, 'икра', int(a > 180))     # две головки
        return (sd, 'голень', sektor(a, [120, 240, 360]) % 2, int(u > 0.55))
    if b == 'таз' or (obj == 'Ноги' and b in ('хребет', 'живот')):
        if not front: return (sd, 'ягодица', int(z > 0.92))
        return (sd, 'пах')
    if obj == 'шея' or b == 'шея':
        if front: return (sd, 'шея', 'перед')
        return (sd, 'шея', 'бок' if ax_ > 0.04 else 'зад')
    # торс: грудная клетка, живот, хребет
    if front:
        z_grud = 1.27 + 0.55 * ax_                     # нижний край грудной — косо вверх к подмышке
        if z > z_grud and ax_ < 0.21: return (sd, 'грудная', int(z > z_grud + 0.07))
        if ax_ < 0.085:
            for k, zk in enumerate((1.05, 1.12, 1.19)):    # шевроны пресса
                if z < zk + 0.35 * ax_: return (sd, 'пресс', k)
            return (sd, 'пресс', 3)
        if z > 1.21: return (sd, 'зубчатая')
        return (sd, 'косая', int(z > 1.10))
    if z > 1.40: return (sd, 'трапеция', 0)
    if ax_ < 0.055: return (sd, 'разгибатели', int(z > 1.18))
    return (sd, 'широчайшая', int(z > 1.24), int(ax_ > 0.12))


def geom(P, F):
    C = np.array([P[f].mean(0) for f in F])
    N = np.array([np.cross(P[f[1]] - P[f[0]], P[f[2]] - P[f[0]]) for f in F])
    A = np.linalg.norm(N, axis=1) / 2; N = N / np.maximum(2 * A, 1e-12)[:, None]
    return C, N, A


def svyaz(F):
    ed = {}
    for fi, f in enumerate(F):
        for i_ in range(len(f)):
            e = tuple(sorted((f[i_], f[(i_ + 1) % len(f)]))); ed.setdefault(e, []).append(fi)
    adj = [[] for _ in F]
    for fs in ed.values():
        for a_ in fs:
            for b_ in fs:
                if a_ != b_: adj[a_].append(b_)
    return adj


def oblasti_myshcy(P, F, nb, fixed, A, fmysh):
    """мышца → 1–6 плоскостей по нормали сглаженной копии (Таубин 30 шагов: шум подгонки рассыпал группы на крошки)"""
    Ps = P.copy()
    for _ in range(30):
        for lam_ in (0.5, -0.53):
            Ls = np.array([Ps[nb[v]].mean(0) - Ps[v] if nb[v] else np.zeros(3) for v in range(len(Ps))])
            Ps = np.where(fixed[:, None], Ps, Ps + lam_ * Ls)
    _, N, _ = geom(Ps, F)
    key_ = {}; lab = np.array([key_.setdefault(k, len(key_)) for k in fmysh])
    lab2 = np.zeros_like(lab); nxt = 0
    for k in np.unique(lab):
        idx = np.nonzero(lab == k)[0]
        kk_ = int(np.clip(A[idx].sum() / float(opt['ploshchad_ploskosti']), 1, 6))
        if kk_ == 1 or len(idx) < 4:
            lab2[idx] = nxt; nxt += 1; continue
        Nn = N[idx]; cen = Nn[rng.choice(len(idx), size=kk_, replace=False)]
        for _ in range(10):
            l_ = ((Nn[:, None] - cen[None]) ** 2).sum(2).argmin(1)
            for j in range(kk_):
                m = l_ == j
                if m.any(): cen[j] = (Nn[m] * A[idx][m, None]).sum(0)
                cen[j] /= max(np.linalg.norm(cen[j]), 1e-9)
        lab2[idx] = nxt + l_; nxt += kk_
    return lab2


def oblasti_kmeans(C, N, A, plosh):
    K = max(4, int(A.sum() / plosh))
    feat = np.hstack([C / float(opt['masshtab']), N])
    cen = feat[rng.choice(len(C), size=min(K, len(C)), replace=False)]
    for _ in range(12):
        lab = ((feat[:, None, :] - cen[None]) ** 2).sum(2).argmin(1)
        for k in range(len(cen)):
            m = lab == k
            if m.any(): cen[k] = (feat[m] * A[m, None]).sum(0) / A[m].sum()
    return lab


def svyaznye(lab, F, A):
    """область — связный кусок (иначе общая плоскость через средний центр сдвигала грани разной глубины до 20 см); островки
    мельче `--min_oblast` сливаются с соседом по большинству общих рёбер"""
    adj = svyaz(F)
    lab3 = -np.ones(len(F), dtype=np.int64); nxt = 0
    for f0 in range(len(F)):
        if lab3[f0] >= 0: continue
        st = [f0]; lab3[f0] = nxt
        while st:
            g = st.pop()
            for h_ in adj[g]:
                if lab3[h_] < 0 and lab[h_] == lab[f0]: lab3[h_] = nxt; st.append(h_)
        nxt += 1
    amin = float(opt['min_oblast'])
    for _ in range(6):
        ar = np.bincount(lab3, weights=A, minlength=nxt)
        small = set(np.nonzero(ar < amin)[0].tolist())
        if not small: break
        for f0 in range(len(F)):
            if lab3[f0] not in small: continue
            cnt = {}
            for h_ in adj[f0]:
                if lab3[h_] != lab3[f0]: cnt[lab3[h_]] = cnt.get(lab3[h_], 0) + 1
            if cnt: lab3[f0] = max(cnt, key=cnt.get)
    return np.unique(lab3, return_inverse=True)[1]


def vypryamit(P, F, lab, fixed, nb, vf):
    P0 = P.copy()
    for _ in range(int(opt['iter'])):
        C, N, A = geom(P, F)
        PN, PC = {}, {}
        for k in np.unique(lab):
            m = lab == k; n_ = (N[m] * A[m, None]).sum(0); n_ /= max(np.linalg.norm(n_), 1e-12)
            PN[k] = n_; PC[k] = (C[m] * A[m, None]).sum(0) / A[m].sum()
        T = P.copy()
        for v in range(len(P)):
            if fixed[v] or not vf[v]: continue
            T[v] = np.mean([P[v] - PN[lab[f]] * np.dot(P[v] - PC[lab[f]], PN[lab[f]]) for f in vf[v]], axis=0)
        L = np.array([P[v] - P[nb[v]].mean(0) if nb[v] else np.zeros(3) for v in range(len(P))])
        L0 = np.array([P0[v] - P0[nb[v]].mean(0) if nb[v] else np.zeros(3) for v in range(len(P))])
        P = np.where(fixed[:, None], P, P + float(opt['sila']) * (T - P) - float(opt['lap']) * (L - L0))
    return P


def topologiya(F, n):
    """соседи, грани вершины и неподвижные: открытый край (ребро одной грани) и неманифолд (ребро трёх граней и больше)"""
    nb = [set() for _ in range(n)]; vf = [[] for _ in range(n)]; ec = {}
    for fi, f in enumerate(F):
        for i_ in range(len(f)):
            x_, y_ = f[i_], f[(i_ + 1) % len(f)]
            nb[x_].add(y_); nb[y_].add(x_); vf[x_].append(fi)
            e = tuple(sorted((x_, y_))); ec[e] = ec.get(e, 0) + 1
    fixed = np.zeros(n, bool)
    for (x_, y_), c_ in ec.items():
        if c_ != 2: fixed[x_] = fixed[y_] = True      # открытый край и неманифолд (объекты шасси перекрываются на стыках)
    return [sorted(s_) for s_ in nb], vf, fixed


# ---------- ТЕЛО ОДНОЙ СЕТКОЙ ----------
body = [o for o in bpy.data.objects if o.type == 'MESH' and not o.name.startswith('@') and o.name not in BEZ
        and o.data.shape_keys and NAME in o.data.shape_keys.key_blocks]
uid, Pu, Bu, vmap = {}, [], [], {}
Fu, fobj, fbone = [], [], []
for o in body:
    Mw = o.matrix_world; kb = o.data.shape_keys.key_blocks[NAME]
    gn = {g.index: g.name for g in o.vertex_groups}
    for vi, v in enumerate(o.data.vertices):
        bpos = Mw @ v.co; k = (round(bpos.x, 5), round(bpos.y, 5), round(bpos.z, 5))
        if k not in uid:
            uid[k] = len(Pu); Pu.append(list(Mw @ kb.data[vi].co)); Bu.append(list(bpos))
        vmap[(o.name, vi)] = uid[k]
    vb = [gn[max(v.groups, key=lambda g: g.weight).group] if v.groups else '' for v in o.data.vertices]
    for pl in o.data.polygons:
        Fu.append([vmap[(o.name, vi)] for vi in pl.vertices]); fobj.append(o.name)
        fbone.append(max(set(vb[vi] for vi in pl.vertices), key=lambda n: sum(1 for vi in pl.vertices if vb[vi] == n)))
P = np.array(Pu); B0 = np.array(Bu)
nb, vf, fixed = topologiya(Fu, len(P))
C, N, A = geom(P, Fu)
if opt.get('rezhim') == 'myshcy':
    fmysh = [myshca(B0[f].mean(0), fbone[fi], fobj[fi]) for fi, f in enumerate(Fu)]
    # стопа не трогается совсем: слияние островков склеивало её грани в плоскости (сдвиг до 16 см)
    for fi, f in enumerate(Fu):
        if fmysh[fi][0] == 'стопа': fixed[f] = True
    lab = oblasti_myshcy(P, Fu, nb, fixed, A, fmysh)
else:
    lab = oblasti_kmeans(C, N, A, float(opt['ploshchad']))
lab = svyaznye(lab, Fu, A)
P2 = vypryamit(P, Fu, lab, fixed, nb, vf)
dv = np.linalg.norm(P2 - P, axis=1)
if opt.get('otladka') == '1' and opt.get('rezhim') == 'myshcy':
    for v in np.argsort(-dv)[:6]:
        print('ОТЛАДКА: вершина %d база %s сдвиг %.3f, мышцы %s' % (v, np.round(B0[v], 3), dv[v], sorted(set(str(fmysh[f][:3]) for f in vf[v]))))
for o in body:
    kb = o.data.shape_keys.key_blocks[NAME]; inv = o.matrix_world.inverted()
    for vi in range(len(o.data.vertices)):
        kb.data[vi].co = inv @ Vector(P2[vmap[(o.name, vi)]])
print('ОГРАНКА: тело одной сеткой (%s) — областей %d, вершин %d (неподвижных: %d), сдвиг средн. %.4f, макс %.4f м' % (
    ', '.join(o.name for o in body), len(np.unique(lab)), len(P), int(fixed.sum()), dv.mean(), dv.max()))

# ---------- КУСКИ ----------
if opt.get('kuski', '0') == '1':
    for o in [o for o in bpy.data.objects if o.type == 'MESH' and o.name.startswith('@')]:
        Pk = np.array([list(v.co) for v in o.data.vertices]); Fk = [list(pl.vertices) for pl in o.data.polygons]
        nbk, vfk, fxk = topologiya(Fk, len(Pk))
        Ck, Nk, Ak = geom(Pk, Fk)
        labk = svyaznye(oblasti_kmeans(Ck, Nk, Ak, float(opt['ploshchad_kuski'])), Fk, Ak)
        Pk2 = vypryamit(Pk, Fk, labk, fxk, nbk, vfk)
        for v, p_ in zip(o.data.vertices, Pk2): v.co = p_
        print('ОГРАНКА: кусок %s — областей %d, сдвиг средн. %.4f м' % (o.name, len(np.unique(labk)), float(np.linalg.norm(Pk2 - Pk, axis=1).mean())))
bpy.ops.wm.save_as_mainfile(filepath=out)
