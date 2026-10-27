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

  blender -b --factory-startup -P ogranka.py -- вход.blend выход.blend [--imya Волк] [--rezhim myshcy|massy [--ploshchad_massy 0.02 --povorot 12 --sredina_shag 0.12 --zhivot 0.055:1.0:1.13:1.27|0 --zhivot_sdvig 0.035]] [--sila 1] [--iter 30] [--lap 0]
      [--ploshchad_ploskosti 0.008] [--min_oblast 0.003] [--ploshchad 0.006] [--masshtab 0.08] [--bez голова,Чутьё] [--linii 2|1|0] [--maks_sdvig 0.025] [--v_poze klyuchi/<…>-poza.json] [--kuski 1 [--ploshchad_kuski 0.01]]
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


def svyaz(F, bar=None):
    """соседство граней по общим рёбрам; через ребро из `bar` (линия перелома) соседства нет"""
    ed = {}
    for fi, f in enumerate(F):
        for i_ in range(len(f)):
            e = tuple(sorted((f[i_], f[(i_ + 1) % len(f)]))); ed.setdefault(e, []).append(fi)
    adj = [[] for _ in F]
    for e, fs in ed.items():
        if bar and e in bar: continue
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


def oblasti_massy(P, F, C, A, nb, fixed, bar):
    """МАССЫ ОТ ЛИНИЙ (`--rezhim massy`, 27.10, Г шаг 2): k-средних режут гладкий торс клиньями («щит до паха», полка), потому что
    граница кластера ложится где угодно. Здесь области растут от затравок по поверхности (Дейкстра по соседству граней), через
    ребро линии Г не проходят, а цена шага растёт с поворотом сглаженной нормали — сама собой граница встаёт там, где поверхность
    поворачивает, и не встаёт посреди широкой плоскости. Затравки — `--ploshchad_massy` м² на массу, 4 прохода Ллойда"""
    import heapq
    Ps = P.copy()
    for _ in range(30):
        for lam_ in (0.5, -0.53):
            Ls = np.array([Ps[nb[v]].mean(0) - Ps[v] if nb[v] else np.zeros(3) for v in range(len(Ps))])
            Ps = np.where(fixed[:, None], Ps, Ps + lam_ * Ls)
    _, Ns, _ = geom(Ps, F)
    adj = svyaz(F, bar); beta = float(opt.get('povorot', '12'))
    K = max(4, int(A.sum() / float(opt.get('ploshchad_massy', '0.02'))))
    # затравки: дальние точки по центрам граней
    # затравки на СРЕДНЕЙ ЛИНИИ передней поверхности торса через `--sredina_shag` м (0.12): иначе ни одна масса не переходит через
    # средину, левая и правая половины живота сходятся килем от груди до паха — запрещённый «центральный щит» листа 03
    seeds = []
    sh_ = float(opt.get('sredina_shag', '0.12'))
    if sh_ > 0:
        mid = np.nonzero((np.abs(C[:, 0]) < 0.015) & (Ns[:, 1] < -0.6))[0]
        if len(mid):
            for z_ in np.arange(C[mid, 2].min() + sh_ / 2, C[mid, 2].max(), sh_):
                seeds.append(int(mid[np.argmin(np.abs(C[mid, 2] - z_))]))
    if not seeds: seeds = [int(np.argmax(A))]
    dmin = np.min([np.linalg.norm(C - C[s_], axis=1) for s_ in seeds], axis=0)
    for _ in range(K - len(seeds)):
        seeds.append(int(np.argmax(dmin))); dmin = np.minimum(dmin, np.linalg.norm(C - C[seeds[-1]], axis=1))
    for it in range(4):
        dist = np.full(len(F), np.inf); lab = -np.ones(len(F), dtype=np.int64); pq = []
        for k, f0 in enumerate(seeds): dist[f0] = 0.0; lab[f0] = k; heapq.heappush(pq, (0.0, f0))
        while pq:
            d, g = heapq.heappop(pq)
            if d > dist[g]: continue
            for h_ in adj[g]:
                nd = d + float(np.linalg.norm(C[g] - C[h_])) * (1.0 + beta * (1.0 - float(Ns[g] @ Ns[h_])))
                if nd < dist[h_]: dist[h_] = nd; lab[h_] = lab[g]; heapq.heappush(pq, (nd, h_))
        lab[lab < 0] = len(seeds)          # не досягнутые (за линией без затравки) — своя область, разделит svyaznye
        if it == 3: break
        new_ = []
        for k in range(len(seeds)):
            m = np.nonzero(lab == k)[0]
            if not len(m): new_.append(seeds[k]); continue
            cn = (Ns[m] * A[m, None]).sum(0); cn /= max(np.linalg.norm(cn), 1e-9); cc = (C[m] * A[m, None]).sum(0) / A[m].sum()
            new_.append(int(m[np.argmin(np.linalg.norm(C[m] - cc, axis=1) * (1 + beta * (1 - Ns[m] @ cn)))]))
        seeds = new_
    return lab


def svyaznye(lab, F, A, bar=None):
    """область — связный кусок (иначе общая плоскость через средний центр сдвигала грани разной глубины до 20 см); островки
    мельче `--min_oblast` сливаются с соседом по большинству общих рёбер"""
    adj = svyaz(F, bar)
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
        # ПРЕДЕЛ СДВИГА (`--maks_sdvig` м, 27.10): огранка поворачивает плоскости, а не переносит объём — массы без предела
        # сдвигали вершины до 11 см (развороты в позе 186 → 216, IoU анфас вниз)
        if opt.get('maks_sdvig'):
            d_ = P - P0; n_ = np.linalg.norm(d_, axis=1); m_ = np.full(len(P), float(opt['maks_sdvig']))
            if MAKS_V is not None: m_ = np.maximum(m_, MAKS_V)
            P = P0 + d_ * np.minimum(1.0, m_ / np.maximum(n_, 1e-12))[:, None]
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
# ЛИНИИ ПЕРЕЛОМА (27.10, пункт Г): рёбра с атрибутом `перелом` (linii_k2.py; 2 — главная Г, 1 — второстепенная В) — граница
# областей: область не связывается через такое ребро, и грань ломается по линии листа, а не там, где легли k-средние.
# `--linii 2` — только Г (по умолчанию: второстепенные «могут исчезать в тумане», записка дизайн-линии), `--linii 1` — и В, `0` — выкл
LIN = int(opt.get('linii', '2')); BAR = set()
for o in body:
    Mw = o.matrix_world; kb = o.data.shape_keys.key_blocks[NAME]
    gn = {g.index: g.name for g in o.vertex_groups}
    for vi, v in enumerate(o.data.vertices):
        bpos = Mw @ v.co; k = (round(bpos.x, 5), round(bpos.y, 5), round(bpos.z, 5))
        if k not in uid:
            uid[k] = len(Pu); Pu.append(list(Mw @ kb.data[vi].co)); Bu.append(list(bpos))
        vmap[(o.name, vi)] = uid[k]
    at = o.data.attributes.get('перелом')
    if at and LIN > 0:
        vals = [0] * len(o.data.edges); at.data.foreach_get('value', vals)
        for ed, s_ in zip(o.data.edges, vals):
            if s_ >= LIN: BAR.add(tuple(sorted((vmap[(o.name, ed.vertices[0])], vmap[(o.name, ed.vertices[1])]))))
    vb = [gn[max(v.groups, key=lambda g: g.weight).group] if v.groups else '' for v in o.data.vertices]
    for pl in o.data.polygons:
        Fu.append([vmap[(o.name, vi)] for vi in pl.vertices]); fobj.append(o.name)
        fbone.append(max(set(vb[vi] for vi in pl.vertices), key=lambda n: sum(1 for vi in pl.vertices if vb[vi] == n)))
P = np.array(Pu); B0 = np.array(Bu); MAKS_V = None
# ОГРАНКА В ПОЗЕ СТУПЕНИ (`--v_poze поза.json`, 27.10, диагноз shum.py: мятость давала огранка на ПОКОЕ ключа — покой есть обратный
# скиннинг и сам мятый (медиана разброса 10.5° против 3.7° в позе); плоскости, выпрямленные на нём, поза гнёт в мелкие изломы:
# сырой ключ в позе 3.7°/13 % изломов, после огранки 4.9°/23 %). Ключ ставится в позу матрицами скиннинга игры (как vesa_final.py),
# области и плоскости считаются там, обратно в покой — обращением тех же матриц (принцип (а) опыта дизайн-линии d47c063)
MV = None
if opt.get('v_poze'):
    from mathutils import Matrix
    PZ = {b['name']: b for b in __import__('json').load(open(opt['v_poze'], encoding='utf-8'))['bones']}
    SKM = {}
    for bone in ARM.data.bones:
        if bone.name not in PZ: continue
        pz = PZ[bone.name]; h, t, zz = (Vector(pz[k]) for k in ('head', 'tail', 'z_axis'))
        yv = (t - h).normalized(); xv = yv.cross(zz).normalized(); zv = xv.cross(yv)
        rest = AW @ bone.matrix_local
        dd = Matrix((xv, yv, zv)).transposed() @ rest.to_3x3().inverted()
        SKM[bone.name] = np.array(Matrix.Translation(h) @ dd.to_4x4() @ Matrix.Translation(-rest.translation))
    MV = [None] * len(P)
    for o in body:
        gn = {g.index: g.name for g in o.vertex_groups}
        for vi, v in enumerate(o.data.vertices):
            u = vmap[(o.name, vi)]
            if MV[u] is not None: continue
            ws = {gn[g.group]: g.weight for g in v.groups if gn[g.group] in SKM and g.weight > 0}; sw = sum(ws.values())
            MV[u] = sum((SKM[n] * (w / sw) for n, w in ws.items()), np.zeros((4, 4))) if sw else np.eye(4)
    MV = np.array(MV)
    P = np.einsum('vij,vj->vi', MV, np.c_[P, np.ones(len(P))])[:, :3]
    print('ОГРАНКА: в позе %s — обусловленность макс %.2f' % (opt['v_poze'].split('/')[-1], max(np.linalg.cond(m[:3, :3]) for m in MV)))
nb, vf, fixed = topologiya(Fu, len(P))
C, N, A = geom(P, Fu)
if opt.get('rezhim') == 'myshcy':
    fmysh = [myshca(B0[f].mean(0), fbone[fi], fobj[fi]) for fi, f in enumerate(Fu)]
    # стопа не трогается совсем: слияние островков склеивало её грани в плоскости (сдвиг до 16 см)
    for fi, f in enumerate(Fu):
        if fmysh[fi][0] == 'стопа': fixed[f] = True
    lab = oblasti_myshcy(P, Fu, nb, fixed, A, fmysh)
elif opt.get('rezhim') == 'massy':
    for fi, f in enumerate(Fu):        # стопа не трогается (как в режиме мышц)
        if fbone[fi].split('.')[0] == 'стопа': fixed[f] = True
    lab = oblasti_massy(P, Fu, C, A, nb, fixed, BAR)
    # ЖИВОТ — ШИРОКОЙ ПЕРЕДНЕЙ ПОВЕРХНОСТЬЮ (`--zhivot x:z0:zm:z1`, 27.10, дизайн-линия по ряду B/D/E′: «треугольный щит над тазом —
    # схождение диагоналей вниз и центральный киль; В4 — два раздельных продольных края, ширина живота до таза»). Массы сами
    # сходились к паху клином. Зона между краями пресса задаётся в БАЗОВЫХ координатах шаблона (там стоят линии листа; ключ её
    # везёт): |x| < x, z0..z1, спереди; две горизонтальные массы через середину (граница — zm), без киля и без острия.
    # Дуга В3 — выше z1, своей стеной; бока — Г1 и массы
    if opt.get('zhivot', '0.055:1.0:1.13:1.27') != '0':
        zx, z0_, zm_, z1_ = (float(x) for x in opt.get('zhivot', '0.055:1.0:1.13:1.27').split(':'))
        _, Nb_, _ = geom(B0, Fu); Cb = np.array([B0[f].mean(0) for f in Fu]); nl = lab.max() + 1; nz = 0
        MAKS_V = np.zeros(len(P))
        # ГРАНИЦЫ ЗОНЫ — ПУТИ ПО РЁБРАМ (27.10, ГеймБосс по P2: «пила из мелких треугольников поперёк живота» — граница по центрам
        # граней шла зубцами): низ, середина и верх — кратчайшие пути поперёк живота на своей высоте (штраф за уход по высоте),
        # зона — заливка граней от середины живота, через пути и стены линий не переходит
        import heapq
        def put_poperek(zz):
            fr_ = [v for v in range(len(B0)) if B0[v][1] < -0.05 and abs(B0[v][2] - zz) < 0.03]
            a_ = min((v for v in fr_ if B0[v][0] < 0), key=lambda v: abs(B0[v][0] + zx + 0.02) + abs(B0[v][2] - zz))
            b_ = min((v for v in fr_ if B0[v][0] > 0), key=lambda v: abs(B0[v][0] - zx - 0.02) + abs(B0[v][2] - zz))
            dist = {a_: 0.0}; prev = {}; pq = [(0.0, a_)]
            while pq:
                d, x = heapq.heappop(pq)
                if x == b_: break
                if d > dist.get(x, 1e9): continue
                for y in nb[x]:
                    if B0[y][1] > -0.02: continue
                    nd = d + float(np.linalg.norm(B0[x] - B0[y])) * (1 + 200 * abs(B0[y][2] - zz))
                    if nd < dist.get(y, 1e9): dist[y] = nd; prev[y] = x; heapq.heappush(pq, (nd, y))
            pth = [b_]
            while pth[-1] != a_ and pth[-1] in prev: pth.append(prev[pth[-1]])
            return {tuple(sorted(e)) for e in zip(pth, pth[1:])}
        STENY = put_poperek(z0_) | put_poperek(zm_) | put_poperek(z1_) | BAR
        adjz = svyaz(Fu, STENY)
        v_zone = lambda fi: abs(Cb[fi][0]) < zx + 0.03 and z0_ - 0.03 < Cb[fi][2] < z1_ + 0.03 and Nb_[fi][1] < -0.3
        for zz, sl in (((z0_ + zm_) / 2, 0), ((zm_ + z1_) / 2, 1)):
            f0 = min((fi for fi in range(len(Fu)) if v_zone(fi)), key=lambda fi: abs(Cb[fi][0]) + abs(Cb[fi][2] - zz))
            st, seen_ = [f0], {f0}
            while st:
                g = st.pop(); lab[g] = nl + sl; nz += 1
                MAKS_V[Fu[g]] = float(opt.get('zhivot_sdvig', '0.035'))   # живот держит ширину до таза — свой предел сдвига
                for h_ in adjz[g]:
                    if h_ not in seen_ and v_zone(h_): seen_.add(h_); st.append(h_)
        print('ОГРАНКА: живот — %d граней в двух массах (|x| < %.3f, z %.2f–%.2f–%.2f в базе)' % (nz, zx, z0_, zm_, z1_))
else:
    lab = oblasti_kmeans(C, N, A, float(opt['ploshchad']))
lab = svyaznye(lab, Fu, A, BAR)
P2 = vypryamit(P, Fu, lab, fixed, nb, vf)
if MV is not None:   # обратно в покой ключа — теми же матрицами
    Pr = np.linalg.solve(MV, np.c_[P2, np.ones(len(P2))][:, :, None])[:, :3, 0]
    Pr0 = np.linalg.solve(MV, np.c_[P, np.ones(len(P))][:, :, None])[:, :3, 0]
    P2, P = Pr, Pr0
dv = np.linalg.norm(P2 - P, axis=1)
if opt.get('otladka') == '1' and opt.get('rezhim') == 'myshcy':
    for v in np.argsort(-dv)[:6]:
        print('ОТЛАДКА: вершина %d база %s сдвиг %.3f, мышцы %s' % (v, np.round(B0[v], 3), dv[v], sorted(set(str(fmysh[f][:3]) for f in vf[v]))))
for o in body:
    kb = o.data.shape_keys.key_blocks[NAME]; inv = o.matrix_world.inverted()
    for vi in range(len(o.data.vertices)):
        kb.data[vi].co = inv @ Vector(P2[vmap[(o.name, vi)]])
print('ОГРАНКА: тело одной сеткой (%s) — областей %d, вершин %d (неподвижных: %d), рёбер линий %d, сдвиг средн. %.4f, макс %.4f м' % (
    ', '.join(o.name for o in body), len(np.unique(lab)), len(P), int(fixed.sum()), len(BAR), dv.mean(), dv.max()))

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
