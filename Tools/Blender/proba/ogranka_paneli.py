"""ПАНЕЛИ ИЗ ГРАФА ЛИСТА (04.11, проба способа). Граф `ogranka_vershiny.py` в пикселях → координаты тела сплайном тонкой пластины по опорным парам зон «тело — лист» (лист шире в плечах на 8–10 %: прибавка не переносится, пары это и выравнивают). Анфас и сзади идут в панели `грудь` и `спина` (верх z ≥ `--z`, низ остаётся прежним), профиль — в новую панель `дельта бок`. Глубину даст поверхность при сборке; `--ruka файл.json` — ручной сдвиг опорных вершин числом: [{"vid", "x", "z", "dy"}], ближайшая вершина ракурса.

  python ogranka_paneli.py граф.json r10-paneli.json выход.json [--z 1.225] [--ruka сдвиги.json]
"""
import sys, json
import numpy as np

a = sys.argv[1:]; G, P0, OUT = a[:3]; ZC = float(a[a.index('--z') + 1]) if '--z' in a else 1.225
FR = [(0, 1.85, 219, 23), (0, 1.525, 219, 178), (0.075, 1.53, 251, 172), (0.255, 1.47, 334, 205), (0.20, 1.53, 310, 178), (0.322, 1.15, 364, 325), (0.215, 1.30, 292, 262), (0.27, 1.17, 330, 330), (0.345, 0.93, 374, 425), (0.30, 0.95, 342, 425), (0.125, 1.15, 281, 325), (0.15, 1.06, 283, 365), (0.17, 0.90, 303, 470), (0, 1.285, 219, 262), (0, 1.10, 219, 350), (0, 0.885, 219, 490)]
PR = [(0.075, 1.85, 540, 23), (0.08, 1.72, 584, 70), (-0.125, 1.37, 495, 240), (0.125, 1.44, 618, 215), (-0.11, 1.10, 497, 360), (0.11, 1.10, 590, 365), (0.02, 1.53, 545, 175), (-0.03, 1.15, 530, 330), (0.10, 1.15, 610, 325), (-0.02, 0.92, 530, 430), (0.08, 0.92, 575, 430)]


def tps(X, Y, lam=1e-3):
    def U(a_, b_):
        r = np.linalg.norm(a_[:, None] - b_[None], axis=2); return np.where(r > 0, r * r * np.log(np.maximum(r, 1e-12)), 0.0)
    n = len(X); P = np.c_[np.ones(n), X]; K_ = U(X, X) + lam * np.eye(n)
    S_ = np.linalg.solve(np.r_[np.c_[K_, P], np.c_[P.T, np.zeros((3, 3))]], np.r_[Y, np.zeros((3, Y.shape[1]))])
    return lambda q: U(q, X).dot(S_[:n]) + np.c_[np.ones(len(q)), q].dot(S_[n:])


inv_f = tps(np.array([[q[2] - 219, q[3]] for q in FR], float), np.array([q[:2] for q in FR], float))
inv_p = tps(np.array([q[2:] for q in PR], float), np.array([q[:2] for q in PR], float))
D = json.load(open(G, encoding='utf-8')); PN = json.load(open(P0, encoding='utf-8')); by = {p['imya']: p for p in PN}


def perevod(r, k):
    T = np.array(r['tochki'], float)
    if k == 'front': Q = inv_f(np.c_[T[:, 0] - 219, T[:, 1]]); Q[np.abs(T[:, 0] - 219) < 5, 0] = 0
    elif k == 'back': Q = inv_f(np.c_[1233 - T[:, 0], T[:, 1]]); Q[np.abs(T[:, 0] - 1233) < 5, 0] = 0
    else: Q = inv_p(T)
    return Q


def v_panel(p, k):
    Q = perevod(D[k], k); ost = [[float(t[0]), float(t[1]), float(t[2])] for t in p['tochki'] if t[1] < ZC]; st = {i: j for j, i in enumerate(i for i, t in enumerate(p['tochki']) if t[1] < ZC)}
    reb = [[st[x], st[y]] for x, y in p['rebra'] if x in st and y in st]; n0 = len(ost)
    ost += [[round(float(q[0]), 4), round(float(q[1]), 4), 0.0] for q in Q]
    reb += [[x + n0, y + n0] for x, y in D[k]['rebra']]
    p['tochki'] = ost; p['rebra'] = reb; return len(Q)


nf = v_panel(by['грудь'], 'front'); nb = v_panel(by['спина'], 'back')
for k, x_m in (('грудь', 0.31), ('спина', 0.31)):
    by[k].update(x_max=x_m, konech=1, ruki=1, ruki_z=1.27)
Qp = perevod(D['profile'], 'profile')
PN.append(dict(imya='дельта бок', vid='profile', x_min=0.17, x_max=0.34, z=[1.27, 1.56], konech=1, ruki=1, ruki_z=1.27, tochki=[[round(float(q[0]), 4), round(float(q[1]), 4), 0.0] for q in Qp], rebra=D['profile']['rebra']))
if '--ruka' in a:
    for z_ in json.load(open(a[a.index('--ruka') + 1], encoding='utf-8')):
        p = next(q for q in PN if q['imya'] == z_['panel']); T = np.array([t[:2] for t in p['tochki']]); i = int(np.linalg.norm(T - [z_['x'], z_['z']], axis=1).argmin()); p['tochki'][i][2] = z_['dy']; print('РУКОЙ: %s (%.3f, %.3f) ближайшая (%.3f, %.3f) dy %+.3f' % (z_['panel'], z_['x'], z_['z'], T[i][0], T[i][1], z_['dy']))
json.dump(PN, open(OUT, 'w', encoding='utf-8'), ensure_ascii=False, indent=0)
print('ПАНЕЛИ: анфас %d, сзади %d, профиль %d вершин с листа' % (nf, nb, len(Qp)))
