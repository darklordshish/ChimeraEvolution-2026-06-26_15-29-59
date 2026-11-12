"""ТОЧКИ ЛИСТА КАК ЗАШУМЛЁННЫЕ ИЗМЕРЕНИЯ (04.11, геймдизайнер: «поправка на стохастичность»). Лист сгенерирован: вершина на нём — измерение с ошибкой, а не истина.
  (а) левая и правая половина ракурса: граф снимается с обеих, половина отражается и сопоставляется; пара усредняется, непарная вершина отбрасывается; расхождение пар — оценка шума: сигма одной половины = медиана·1.4826 расхождения / √2;
  (б) один ориентир в разных ракурсах: по лучу на поверхность точки становятся трёхмерными, близкие из разных ракурсов сводятся по высоте средним Хубера (порог 1.345 σ, дальше 3 σ выброс и не участвует);
  (в) вершины тянутся мягко: min Σ ρ((p − m)/σ) + λ Σ |p − среднее соседей графа|², сдвиг ограничен 2 σ. Глубину и дальше даёт поверхность.
Контроль — `kontrol`: вершины проецируются в ракурсы 3/4 листа, не участвующие в подгонке; мера — расстояние до ближайшей линии листа, рядом уровень случайности: те же расчёты для случайных точек поверхности зоны.

  python ogranka_shum.py полный граф.json тело.npz паленали.json выход.json шум.json
  python ogranka_shum.py kontrol огранка-рёбра.png тело.npz сырые.json мягкие.json
"""
import sys, json
import numpy as np
from PIL import Image
from scipy import ndimage as ndi

MMPX = 1000 / 435.7


def tps(X, Y, lam=1e-3):
    def U(a_, b_):
        r = np.linalg.norm(a_[:, None] - b_[None], axis=2); return np.where(r > 0, r * r * np.log(np.maximum(r, 1e-12)), 0.0)
    n = len(X); P = np.c_[np.ones(n), X]; K_ = U(X, X) + lam * np.eye(n)
    S_ = np.linalg.solve(np.r_[np.c_[K_, P], np.c_[P.T, np.zeros((3, 3))]], np.r_[Y, np.zeros((3, Y.shape[1]))])
    return lambda q: U(q, X).dot(S_[:n]) + np.c_[np.ones(len(q)), q].dot(S_[n:])


FR = [(0, 1.85, 219, 23), (0, 1.525, 219, 178), (0.075, 1.53, 251, 172), (0.255, 1.47, 334, 205), (0.20, 1.53, 310, 178), (0.322, 1.15, 364, 325), (0.215, 1.30, 292, 262), (0.27, 1.17, 330, 330), (0.345, 0.93, 374, 425), (0.30, 0.95, 342, 425), (0.125, 1.15, 281, 325), (0.15, 1.06, 283, 365), (0.17, 0.90, 303, 470), (0, 1.285, 219, 262), (0, 1.10, 219, 350), (0, 0.885, 219, 490)]
PR = [(0.075, 1.85, 540, 23), (0.08, 1.72, 584, 70), (-0.125, 1.37, 495, 240), (0.125, 1.44, 618, 215), (-0.11, 1.10, 497, 360), (0.11, 1.10, 590, 365), (0.02, 1.53, 545, 175), (-0.03, 1.15, 530, 330), (0.10, 1.15, 610, 325), (-0.02, 0.92, 530, 430), (0.08, 0.92, 575, 430)]
inv_f = tps(np.array([[q[2] - 219, q[3]] for q in FR], float), np.array([q[:2] for q in FR], float))
inv_p = tps(np.array([q[2:] for q in PR], float), np.array([q[:2] for q in PR], float))


def luch(O_, D_, T):
    """первое попадание луча в треугольники; нормаль; numpy"""
    v0, v1, v2 = T[:, 0], T[:, 1], T[:, 2]; e1, e2 = v1 - v0, v2 - v0; h = np.cross(D_, e2); a_ = (e1 * h).sum(1); ok = np.abs(a_) > 1e-12; f = np.where(ok, 1 / np.where(ok, a_, 1), 0)
    s = O_ - v0; u = f * (s * h).sum(1); q = np.cross(s, e1); v = f * (q * D_).sum(1); t = f * (q * e2).sum(1); m = ok & (u >= 0) & (v >= 0) & (u + v <= 1) & (t > 1e-6)
    if not m.any(): return None, None
    i = np.nonzero(m)[0][t[m].argmin()]; n = np.cross(e1[i], e2[i]); return O_ + D_ * t[i], n / np.linalg.norm(n)


def tri3d(q, kl, T):
    """точка ракурса (u, z) → поверхность"""
    if kl == 'front': return luch(np.array([q[0], -2, q[1]]), np.array([0, 1.0, 0]), T)
    if kl == 'back': return luch(np.array([q[0], 2, q[1]]), np.array([0, -1.0, 0]), T)
    return luch(np.array([1.0, q[0], q[1]]), np.array([-1.0, 0, 0]), T)


def huber(z, sg, it=8):
    m = np.median(z)
    for _ in range(it):
        r = np.abs(z - m) / max(sg, 1e-9); w = np.where(r <= 1.345, 1.0, 1.345 / np.maximum(r, 1e-9)); w[r > 3] = 0
        if w.sum() == 0: break
        m = (w * z).sum() / w.sum()
    return m, np.abs(z - m) / max(sg, 1e-9) > 3


if sys.argv[1] == 'polnyj':
    Gf, Tf, Pn, Out, Sh = sys.argv[2:7]
    G = json.load(open(Gf, encoding='utf-8')); B = np.load(Tf); T = B['V'][B['F']]; OT = {}
    # (а) половины
    SG = {}; IS = {}
    for k, cx, r_, l_ in (('front', 219, 'front', 'front_l'), ('back', 1233, 'back', 'back_r')):
        A_ = np.array(G[r_]['tochki'], float); C_ = np.array(G[l_]['tochki'], float) * [-1, 1] + [2 * cx, 0]
        d = np.linalg.norm(A_[:, None] - C_[None], axis=2); nA = d.argmin(1); nC = d.argmin(0); pary = [(i, int(nA[i])) for i in range(len(A_)) if nC[nA[i]] == i and d[i, nA[i]] < 8]
        ost = np.array([A_[i] - C_[j] for i, j in pary]); sd = np.median(ost, axis=0); C2 = C_ + sd   # сдвиг оси листа
        rs = np.array([np.linalg.norm(A_[i] - C2[j]) for i, j in pary]); sg = 1.4826 * np.median(rs) / np.sqrt(2) * MMPX
        SG[k] = float(sg); Pk = {i: (A_[i] + C2[j]) / 2 for i, j in pary}
        OT[k] = dict(vershin_pravaya=len(A_), vershin_levaya=len(C_), par=len(pary), sdvig_osi_px=[round(float(x), 2) for x in sd], rasxozhd_med_mm=round(float(np.median(rs)) * MMPX, 2), rasxozhd_95_mm=round(float(np.percentile(rs, 95)) * MMPX, 2), sigma_mm=round(sg, 2))
        IS[k] = dict(P={i: p for i, p in Pk.items()}, E=[e for e in G[r_]['rebra'] if e[0] in Pk and e[1] in Pk])
    pf = np.array(G['profile']['tochki'], float); SG['profile'] = float(np.mean([SG['front'], SG['back']])); IS['profile'] = dict(P={i: p for i, p in enumerate(pf)}, E=G['profile']['rebra'])
    OT['profile'] = dict(vershin=len(pf), sigma_mm=round(SG['profile'], 2), primech='второй половины нет: сигма взята средним анфаса и спины')
    # в координаты тела
    Q = {}
    for k in IS:
        ids = sorted(IS[k]['P']); P_ = np.array([IS[k]['P'][i] for i in ids]); cx = 219 if k == 'front' else 1233
        if k == 'front': M = inv_f(np.c_[P_[:, 0] - cx, P_[:, 1]]); M[np.abs(P_[:, 0] - cx) < 5, 0] = 0
        elif k == 'back': M = inv_f(np.c_[cx - P_[:, 0], P_[:, 1]]); M[np.abs(P_[:, 0] - cx) < 5, 0] = 0
        else: M = inv_p(P_)
        Q[k] = dict(ids=ids, M=M, raw=M.copy())
    # (б) сведение по высоте
    Pt3 = []
    for k in Q:
        for i_, q in enumerate(Q[k]['M']):
            p, _ = tri3d(q, k, T)
            if p is not None: Pt3.append((k, i_, p))
    A3 = np.array([p for _, _, p in Pt3]); sgm = SG['front'] / 1000; R3 = max(0.015, 3 * sgm); bud = set(); ncl = 0; nvy = 0; sv = []
    for a, (k, i_, p) in enumerate(Pt3):
        if a in bud: continue
        d = np.linalg.norm(A3 - p, axis=1); cl = [b for b in np.nonzero(d < R3)[0] if Pt3[b][0] != k or b == a]; cl = [b for b in cl if b not in bud]
        if len({Pt3[b][0] for b in cl}) < 2: continue
        zz = np.array([A3[b][2] for b in cl]); m_, vy = huber(zz, sgm); ncl += 1; nvy += int(vy.sum())
        for b, o_ in zip(cl, vy):
            bud.add(b)
            if not o_: sv.append(abs(A3[b][2] - m_)); Q[Pt3[b][0]]['M'][Pt3[b][1], 1] = m_
    OT['svedenie'] = dict(klasterov=ncl, vybrosov=nvy, sdvig_z_med_mm=round(float(np.median(sv)) * 1000, 2) if sv else 0, sdvig_z_maks_mm=round(float(np.max(sv)) * 1000, 2) if sv else 0)
    # (в) мягко: Хубер к измерению + гладкость по графу, предел 2 σ
    LAM = 25.0; dv = []   # безразмерно: данные в σ, гладкость в длинах ребра
    for k in Q:
        ids = Q[k]['ids']; ix = {i: j for j, i in enumerate(ids)}; m = Q[k]['M']; p = m.copy(); s_ = SG[k] / 1000; sos = {j: [] for j in range(len(ids))}
        for a_, b_ in IS[k]['E']:
            if a_ in ix and b_ in ix: sos[ix[a_]].append(ix[b_]); sos[ix[b_]].append(ix[a_])
        for _ in range(12):
            nov = p.copy()
            for j in range(len(ids)):
                if abs(m[j, 0]) < 1e-9 and k != 'profile': continue
                r = np.linalg.norm(p[j] - m[j]) / s_; w = 1.0 if r <= 1.345 else 1.345 / r; av = np.mean(p[sos[j]], axis=0) if sos[j] else p[j]; L0 = np.mean(np.linalg.norm(m[sos[j]] - m[j], axis=1)) if sos[j] else 1.0; ws = LAM * (s_ / max(L0, 1e-3)) ** 2 if sos[j] else 0
                nov[j] = (w * m[j] + ws * av) / (w + ws)
            p = nov
        dl = p - m; L_ = np.linalg.norm(dl, axis=1); kl = np.minimum(1, 2 * s_ / np.maximum(L_, 1e-9))[:, None]; Q[k]['p'] = m + dl * kl; dv += list(np.minimum(L_, 2 * s_))
    OT['myagko'] = dict(lam=LAM, sdvig_med_mm=round(float(np.median(dv)) * 1000, 2), sdvig_maks_mm=round(float(np.max(dv)) * 1000, 2), predel='2 σ')
    PN = json.load(open(Pn, encoding='utf-8')); by = {p['imya']: p for p in PN}; ZC = 1.225; VY = {}
    def v_panel(p, k, kl='p'):
        ost = [list(map(float, t)) for t in p['tochki'] if t[1] < ZC]; st = {i: j for j, i in enumerate(i for i, t in enumerate(p['tochki']) if t[1] < ZC)}; reb = [[st[x], st[y]] for x, y in p['rebra'] if x in st and y in st]; n0 = len(ost); ix = {i: j for j, i in enumerate(Q[k]['ids'])}
        ost += [[round(float(q[0]), 4), round(float(q[1]), 4), 0.0] for q in Q[k][kl]]; reb += [[ix[a_] + n0, ix[b_] + n0] for a_, b_ in IS[k]['E'] if a_ in ix and b_ in ix]
        p['tochki'] = ost; p['rebra'] = reb
    v_panel(by['грудь'], 'front'); v_panel(by['спина'], 'back')
    for k_ in ('грудь', 'спина'): by[k_].update(x_max=0.31, konech=1, ruki=1, ruki_z=1.27)
    ix = {i: j for j, i in enumerate(Q['profile']['ids'])}
    PN.append(dict(imya='дельта бок', vid='profile', x_min=0.17, x_max=0.34, z=[1.27, 1.56], konech=1, ruki=1, ruki_z=1.27, tochki=[[round(float(q[0]), 4), round(float(q[1]), 4), 0.0] for q in Q['profile']['p']], rebra=[[ix[a_], ix[b_]] for a_, b_ in IS['profile']['E']]))
    json.dump(PN, open(Out, 'w', encoding='utf-8'), ensure_ascii=False, indent=0); json.dump(OT, open(Sh, 'w', encoding='utf-8'), ensure_ascii=False, indent=1); print(json.dumps(OT, ensure_ascii=False))
else:
    Lf, Tf, A_, B_ = sys.argv[2:6]; B0 = np.load(Tf); T = B0['V'][B0['F']]; g = np.asarray(Image.open(Lf).convert('L')) < 70
    def tochki(f):
        R = []
        for p in json.load(open(f, encoding='utf-8')):
            if p['imya'] not in ('грудь', 'спина', 'дельта бок'): continue
            k = {'грудь': 'front', 'спина': 'back', 'дельта бок': 'profile'}[p['imya']]
            for t in p['tochki']:
                if k != 'profile' and t[1] < 1.225: continue
                q, n = tri3d(t[:2], k, T)
                if q is not None: R.append((q, n))
        return R
    rng = np.random.default_rng(5); c_ = T.mean(1); nn = np.cross(T[:, 1] - T[:, 0], T[:, 2] - T[:, 0]); nn /= np.maximum(np.linalg.norm(nn, axis=1, keepdims=True), 1e-12); zn = np.nonzero((c_[:, 2] > 1.25) & (c_[:, 2] < 1.56) & (np.abs(c_[:, 0]) < 0.31))[0]
    SL = [(c_[i], nn[i]) for i in rng.choice(zn, 80)]; Vars = dict(syrye=tochki(A_), myagkie=tochki(B_), sluchajnye=SL); Rz = {}
    # ракурс один на все наборы: подбирается по контуру плотного тела к маске фигуры листа, а не по линиям, иначе случайные точки подгоняются так же хорошо
    Vb = B0['V']; Nb = None
    for imya, (x0, x1, yg) in dict(tri_chetv_pered=(720, 1010, (-55, 55)), tri_chetv_zad=(1450, 1735, (125, 235))).items():
        s0 = np.asarray(Image.open(Lf).convert('L'))[:826, x0:x1] < 235; fig = ndi.binary_fill_holes(ndi.binary_closing(s0, iterations=2)); cr = g[:826, x0:x1] & fig; DT = ndi.distance_transform_edt(~cr); best = (-1,)
        for yaw in np.arange(yg[0], yg[1] + 1, 5):
            y = np.radians(yaw); u = Vb[:, 0] * np.cos(y) + Vb[:, 1] * np.sin(y)
            for sc in (420, 436, 452):
                iy = np.clip(np.round(829 - sc * Vb[:, 2]).astype(int), 0, 825)
                for cx in range(60, x1 - x0 - 60, 4):
                    ix = np.clip(np.round(cx + sc * u).astype(int), 0, fig.shape[1] - 1); m_ = np.zeros(fig.shape, bool); m_[iy, ix] = True; m_ = ndi.binary_dilation(m_, iterations=5); iou = (m_ & fig).sum() / max((m_ | fig).sum(), 1)
                    if iou > best[0]: best = (float(iou), float(yaw), sc, cx)
        _, yw, sc, cx = best; y = np.radians(yw); vw = np.array([np.sin(y), -np.cos(y), 0]); Rz[imya] = dict(ugol=yw, masshtab=sc, sdvig=cx, iou_kontura=round(best[0], 3))
        for v, P in Vars.items():
            A0 = np.array([q for q, _ in P]); N0 = np.array([n for _, n in P]); A0 = np.vstack([A0, A0 * [-1, 1, 1]]); N0 = np.vstack([N0, N0 * [-1, 1, 1]]); m = N0.dot(vw) > 0.2; ix = np.clip(np.round(cx + sc * (A0[m, 0] * np.cos(y) + A0[m, 1] * np.sin(y))).astype(int), 0, DT.shape[1] - 1); iy = np.clip(np.round(829 - sc * A0[m, 2]).astype(int), 0, 825); D_ = DT[iy, ix] * MMPX
            Rz[imya][v] = dict(med_mm=round(float(np.median(D_)), 2), kv75_mm=round(float(np.percentile(D_, 75)), 2), tochek=int(m.sum()))
    print(json.dumps(Rz, ensure_ascii=False, indent=0))
