"""NICP АМБЕРГА С РОБАСТНЫМ ВЕСОМ (29.10, проба А: нежёсткая регистрация вместо ручек подгонки; AMM-NRR не взят — патент, «только
для исследований», и под Windows не собрать). Optimal Step NICP (Amberg, Romdhani, Vetter 2007): в каждой вершине аффинная
матрица Xᵢ (4×3), энергия
    E = Σᵢ wᵢ |vᵢᵀXᵢ − uᵢ|²  +  α Σ₍ᵢⱼ₎ |G(Xᵢ − Xⱼ)|²  +  κ Σ_закреплённые |vᵢᵀXᵢ − vᵢ|²,   G = diag(1, 1, 1, γ)
решается разреженным МНК при убывающей жёсткости α; uᵢ — ближайшая точка цели СВОЕЙ ЧАСТИ (рука к руке, бедро к бедру) по
плоскости касания (точка–плоскость), вес — Уэлша wᵢ = exp(−d²/2σ²) с убывающим σ (выбросы и иглы генератора гаснут сами, без
порогов), и 0 при нормалях врозь. Запускается из podgonka_v_poze.py (`--nicp 1`) обычным Python (scipy):

  python nicp_amberg.py вход.npz выход.npz [--alfa 100,50,20,10,5,2,1] [--gamma 1] [--sigma 0.05,0.01] [--iter 3] [--kappa 100]
      [--normal 0 (порог cos нормалей; −1 — без)] [--ploskost 1 (точка–плоскость; 0 — точка–точка)]
вход: X (n×3) — тело в позе, E (m×2) — рёбра, part (n) — номер части (−1 — без пары), free (n), T (k×3) / TN (k×3) / TP (k) — точки
цели, их нормали и части.
"""
import sys, time
import numpy as np
import scipy.sparse as sp
from scipy.sparse.linalg import splu
from scipy.spatial import cKDTree

a = sys.argv[1:]
src, out = a[0], a[1]
opt = dict(alfa='100,50,20,10,5,2,1', gamma='1', sigma='0.05,0.01', iter='3', kappa='100', normal='0', ploskost='1', vstr='0')
i = 2
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
d = np.load(src)
X0, E, part, free, T, TN, TP = d['X'], d['E'], d['part'], d['free'].astype(bool), d['T'], d['TN'], d['TP']
n = len(X0); m = len(E); t0 = time.time()
ALFA = [float(v) for v in opt['alfa'].split(',')]; G = float(opt['gamma']); KAPPA = float(opt['kappa'])
S0, S1 = (float(v) for v in opt['sigma'].split(','))
# стыки частей и ближайшие точки — по частям
KD = {p: (cKDTree(T[TP == p]), np.nonzero(TP == p)[0]) for p in np.unique(TP)}
# M ⊗ G: строка на ребро и координату однородной вершины
ri = np.repeat(np.arange(m), 2); ci = E.ravel(); vals = np.tile([1.0, -1.0], m)
Minc = sp.csr_matrix((vals, (ri, ci)), shape=(m, n))
MG = sp.kron(Minc, sp.diags([1, 1, 1, G]), format='csr')
V4 = np.c_[X0, np.ones(n)]
rows = np.repeat(np.arange(n), 4); cols = (np.arange(n)[:, None] * 4 + np.arange(4)).ravel()
D = sp.csr_matrix((V4.ravel(), (rows, cols)), shape=(n, 4 * n))
Xa = np.tile(np.r_[np.eye(3), np.zeros((1, 3))], (n, 1))          # 4n × 3 — тождество


def normals(Y, F):
    nr = np.zeros_like(Y); fn = np.cross(Y[F[:, 1]] - Y[F[:, 0]], Y[F[:, 2]] - Y[F[:, 0]])
    for j in range(3): np.add.at(nr, F[:, j], fn)
    l = np.linalg.norm(nr, axis=1); l[l == 0] = 1; return nr / l[:, None]


F = d['F']
n_st = len(ALFA); log = []
for si, al in enumerate(ALFA):
    sig = S0 * (S1 / S0) ** (si / max(n_st - 1, 1))
    for it in range(int(opt['iter'])):
        Y = D @ Xa; NY = normals(Y, F)
        U = Y.copy(); w = np.zeros(n)
        for p, (kd, ids) in KD.items():
            sel = np.nonzero((part == p) & free)[0]
            if not len(sel): continue
            dist, j = kd.query(Y[sel]); q = T[ids[j]]; nq = TN[ids[j]]
            # точка–плоскость: проекция вершины на касательную плоскость цели
            u = (Y[sel] - nq * np.einsum('ij,ij->i', Y[sel] - q, nq)[:, None]) if opt['ploskost'] == '1' else q
            dd = np.linalg.norm(u - Y[sel], axis=1) + 0 * dist
            ww = np.exp(-(dist ** 2) / (2 * sig ** 2)) * (np.einsum('ij,ij->i', NY[sel], nq) > float(opt['normal']))
            U[sel] = u; w[sel] = ww
        # закреплённые (голова — кусок) держатся на месте
        fix = ~free; U[fix] = X0[fix]; w[fix] = KAPPA
        Wd = sp.diags(np.sqrt(w))
        blocks = [al * MG, Wd @ D]; rhs = [np.zeros((4 * m, 3)), Wd @ U]
        # ВСТРЕЧНЫЕ ПАРЫ (`--vstr 1`, как в podgonka_v_poze.py): точка цели тянет ближайшую вершину своей части — тело не ложится на
        # одну стенку цели; вес — Уэлша по расстоянию, вклад каждой точки — отдельной строкой
        if opt['vstr'] == '1':
            rr, cc_, wv, tt = [], [], [], []
            for p, (kd, ids) in KD.items():
                sel = np.nonzero((part == p) & free)[0]
                if not len(sel): continue
                dist, j = cKDTree(Y[sel]).query(T[ids]); k = sel[j]
                ww = np.exp(-(dist ** 2) / (2 * sig ** 2)) * (np.einsum('ij,ij->i', NY[k], TN[ids]) > float(opt['normal']))
                rr.append(k); wv.append(ww); tt.append(T[ids])
            k = np.concatenate(rr); ww = np.sqrt(np.concatenate(wv) * float(opt.get('vstr_ves', '1'))); tt = np.concatenate(tt)
            nr = len(k); Dv = sp.csr_matrix((V4[k].ravel() * np.repeat(ww, 4), (np.repeat(np.arange(nr), 4), (k[:, None] * 4 + np.arange(4)).ravel())), shape=(nr, 4 * n))
            blocks.append(Dv); rhs.append(tt * ww[:, None])
        A = sp.vstack(blocks).tocsc(); B = np.vstack(rhs)
        lu = splu((A.T @ A).tocsc()); Xa = lu.solve(A.T @ B)
    Y = D @ Xa
    log.append('α %.1f σ %.3f: пар с весом >0.5 %d, средн. до цели %.4f м' % (al, sig, int((w[free] > 0.5).sum()),
               float(np.mean(np.linalg.norm(U[free] - Y[free], axis=1)))))
Y = D @ Xa; np.savez(out, Y=Y)
# итог: до своей части цели (точки цели) и вывернутые грани относительно тела в позе до притяжки
dd = np.zeros(n)
for p, (kd, ids) in KD.items():
    sel = np.nonzero((part == p) & free)[0]
    if len(sel): dd[sel] = kd.query(Y[sel])[0]
fn0 = np.cross(X0[F[:, 1]] - X0[F[:, 0]], X0[F[:, 2]] - X0[F[:, 0]]); fn1 = np.cross(Y[F[:, 1]] - Y[F[:, 0]], Y[F[:, 2]] - Y[F[:, 0]])
cov = []
kdY = {p: cKDTree(Y[(part == p) & free]) for p in np.unique(TP) if ((part == p) & free).any()}
for p, kd_ in kdY.items(): cov.append(kd_.query(T[TP == p])[0])
cov = np.concatenate(cov)
print('NICP-ИТОГ: до цели средн. %.4f, 95%% %.4f м; покрытие средн. %.4f, 95%% %.4f м; вывернутых граней %d' % (
    dd[free & (part >= 0)].mean(), np.percentile(dd[free & (part >= 0)], 95), cov.mean(), np.percentile(cov, 95), int((np.einsum('ij,ij->i', fn0, fn1) < 0).sum())))
for s in log: print('NICP: ' + s)
print('NICP: вершин %d, рёбер %d, точек цели %d, %.1f с' % (n, m, len(T), time.time() - t0))
