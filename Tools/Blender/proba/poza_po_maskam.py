"""ПОЗА СТУПЕНИ ПО МАСКАМ ЛИСТА (29.10, проба Б, ГеймБосс по SMAL: Zuffi и др. «3D Menagerie», Animal3D). У 3D-цели Hunyuan ноги
параллельны и стопа длинная — подгонка к цели этого не исправит; расхождение с листом убирается функционалом против МАСОК ЛИСТА.
Параметры — повороты костей вокруг оси X мира через сустав (как `poza_po_listu.py`: дети едут за родителем, корень по высоте
садит нижнюю стопу на землю), форма тела не трогается — швы, куски и веса целы. Функционал:
    Σ_ракурсы wᵥ (1 − IoUᵥ)  +  λ Σ (δ/10°)²  +  μ |z стопы − z стопы.L| / 0.01 м
IoU — как у стенда (`stend_k2.py`: треугольники в орто-проекции, рамка по фигуре, `norm512`; профиль — лучшая из двух сторон).
Минимум — методом Пауэлла (без градиента: параметров ~10–15, силуэт кусочно-постоянен).

  python poza_po_maskam.py тело.npz скелет_К2.json суставы.json поза.json выход.json --front f.png --profile p.png [--back b.png]
      [--kosti бедро,бедро.L,голень,голень.L,стопа,stopa.L,плечо,плечо.L,предплечье,предплечье.L,хребет,шея,голова] [--lam 0.02]
      [--mu 0.05] [--vesa 1,1,0.5] [--predel 30] [--iter 3] [--kadr префикс]
`тело.npz` — `telo_npz.py` (вершины покоя с ключом ступени, веса, треугольники, головы костей покоя).
"""
import sys, json, math, time
import numpy as np
from PIL import Image, ImageDraw
from scipy.optimize import minimize

a = sys.argv[1:]
tn, sk_p, sj_p, pz_p, out_p = a[:5]
opt = dict(ruki='0', spina='0', stopa='0', nu='0.002', kosti='стопа,стопа.L,плечо,плечо.L,предплечье,предплечье.L,хребет,шея,голова', nogi_ik='1', lam='0.02',
           mu='0.05', vesa='1,1,0.5', predel='30', iter='3')
i = 5
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
d = np.load(tn); V, W, F, RH, BN = d['V'], d['W'].astype(float), d['F'], d['RH'], list(d['BN'])
VB, KU = (d['VB'], d['KU']) if 'VB' in d else (V, np.zeros(len(V), bool))
FT = F[~KU[F[:, 0]]]                       # грани тела (у кусков покой — обратный скиннинг, мера не определена — как у стенда)
NB_ = np.cross(VB[FT[:, 1]] - VB[FT[:, 0]], VB[FT[:, 2]] - VB[FT[:, 0]]); GL = W.argmax(1)
sk, sj, pz = (json.load(open(p, encoding='utf-8')) for p in (sk_p, sj_p, pz_p))
K = {b['name']: b for b in sk['bones']}; P = {b['name']: b for b in pz['bones']}; S = {b['name']: b for b in sj['bones']}


def frame(b):
    h, t, z = (np.array(b[k], float) for k in ('head', 'tail', 'z_axis'))
    y = (t - h) / np.linalg.norm(t - h); x = np.cross(y, z); x /= np.linalg.norm(x); z2 = np.cross(x, y)
    return np.stack([x, y, z2], 1)


def rx(deg):
    c, s = math.cos(math.radians(deg)), math.sin(math.radians(deg))
    return np.array([[1, 0, 0], [0, c, -s], [0, s, c]])


D0 = {n: frame(P[n]) @ frame(K[n]).T for n in P}
par = {n: (S[n].get('parent') or '') for n in S}
order = []
def walk(n):
    order.append(n)
    for c in S:
        if par[c] == n: walk(c)
for n in S:
    if not par[n]: walk(n)
root = order[0]; R0 = np.array(P[root]['head'], float)


def fk(Dm, rh):
    PH, PT = {}, {}
    for n in order:
        p = par[n]
        PH[n] = rh.copy() if not p else PH[p] + Dm[p] @ (np.array(S[n]['head']) - np.array(S[p]['head']))
        PT[n] = PH[n] + Dm[n] @ (np.array(S[n]['tail']) - np.array(S[n]['head']))
    return PH, PT


KOSTI = [k for k in opt['kosti'].split(',') if k in S]
PH0, PT0 = fk(D0, R0); Z0 = min(PT0['стопа'][2], PT0['стопа.L'][2])
# НОГИ ОБРАТНОЙ КИНЕМАТИКОЙ (`--nogi_ik 1`): у каждой ноги два параметра — сдвиг скакательного по глубине и высоте (см), бедро и
# голень — двухзвенным решением в плоскости y–z (колено по ту же сторону), стопа держит ориентацию мира (плюс свой наклон из
# `--kosti`). Поворот одного бедра отрывал стопу от земли, штраф его гасил, и ноги не двигались
NIK = opt['nogi_ik'] == '1'
DERZ = {'стопа', 'стопа.L'} if NIK else set()
def yz(p): return np.array([p[1], p[2]])
def ug(v): return math.degrees(math.atan2(v[0], -v[1]))
def ik(s_, dy, dz):
    H, Kn, A_ = yz(PH0['бедро' + s_]), yz(PH0['голень' + s_]), yz(PH0['стопа' + s_])
    L1, L2 = np.linalg.norm(Kn - H), np.linalg.norm(A_ - Kn); A2 = A_ + np.array([dy, dz])
    def kol(Ap):
        dd = min(np.linalg.norm(Ap - H), L1 + L2 - 1e-6); u = (Ap - H) / np.linalg.norm(Ap - H)
        a_ = (L1 ** 2 - L2 ** 2 + dd ** 2) / (2 * dd); h_ = math.sqrt(max(L1 ** 2 - a_ ** 2, 0)); nn = np.array([-u[1], u[0]])
        return H + u * a_ + nn * h_, H + u * a_ - nn * h_, H + u * dd
    k1, k2, _ = kol(A_); sg = 0 if np.linalg.norm(k1 - Kn) < np.linalg.norm(k2 - Kn) else 1
    K2_ = kol(A2)[sg]; A2r = kol(A2)[2]
    d1 = ug(K2_ - H) - ug(Kn - H); d2 = ug(A2r - K2_) - ug(A_ - Kn)
    return d1, d2 - d1
# знак поворота вокруг X: «+» у poza_po_listu кладёт кость вперёд (к −y); проверяется на первом вызове
ZN = [1.0]


def poza(delta):
    POV = dict(zip(KOSTI, delta[:len(KOSTI)]))
    if NIK:
        q = delta[len(KOSTI):] / 100.0
        for s_, (dy, dz) in (('', q[0:2]), ('.L', q[2:4])):
            d1, d2 = ik(s_, dy, dz); POV['бедро' + s_] = POV.get('бедро' + s_, 0) + ZN[0] * d1; POV['голень' + s_] = POV.get('голень' + s_, 0) + ZN[0] * d2
    A, D2 = {}, {}
    for n in order:
        p = par[n]; base = np.eye(3) if (not p or n in DERZ) else A[p]
        A[n] = base @ rx(POV[n]) if n in POV else base
        D2[n] = A[n] @ D0[n]
    PH, PT = fk(D2, R0)
    dz = Z0 - min(PT['стопа'][2], PT['стопа.L'][2])
    PH, PT = fk(D2, R0 + np.array([0, 0, dz]))
    return D2, PH, PT


BI = [BN.index(n) if n in BN else -1 for n in order]
act = [j for j, b in enumerate(BI) if b >= 0 and W[:, b].any()]


def skin(D2, PH, Vx=None):
    Vx = V if Vx is None else Vx; Y = np.zeros_like(Vx)
    for j in act:
        n = order[j]; b = BI[j]; w = W[:, b]; m = w > 0
        Y[m] += w[m, None] * (PH[n] + (Vx[m] - RH[b]) @ D2[n].T)
    return Y


# ФОРМА СТОПЫ (`--stopa 1`, 29.10, ГеймБосс: «стопа формой вместе с позой по маскам»; критик: стопа-ласт ~30 % роста против ~18 %):
# у каждой стопы параметр — длина вдоль её кости покоя, в процентах (0 — как есть, −40 — ×0.6); вершины тела ступени и куска с весом
# кости стопы сжимаются к скакательному пропорционально весу (только то, что дальше сустава вдоль кости). Тело садится на землю
# нижней точкой меша (укороченная стопа иначе висела бы)
NST = 2 if opt['stopa'] == '1' else 0
SB = {}
for s_ in ('', '.L'):
    b = BN.index('стопа' + s_); h = np.array(K['стопа' + s_]['head'], float); t = np.array(K['стопа' + s_]['tail'], float)
    ax_ = (t - h) / np.linalg.norm(t - h); pr = (V - RH[b]) @ ax_
    SB[s_] = (b, RH[b], ax_, np.where(pr > 0, pr, 0.0) * W[:, b])


def stopa(kk):
    Vx = V.copy()
    for s_, k in zip(('', '.L'), kk):
        b, h, ax_, wp = SB[s_]; Vx += ((k / 100.0) * wp)[:, None] * ax_[None, :]
    return Vx


# МАССА СПИНЫ (`--spina 1`, 30.10, критик n3 и ГеймБосс: «спина листа выше и полнее нашей, пила её заполняет»): параметр — сдвиг задней
# половины корпуса в покое назад (+y) на a см; вес — за осью хребта (y от 0 до 12 см), по высоте от таза к плечам (0.95–1.60 м, края
# 15 см), без рук (|x| < 25 см); тело и кусок (гребень едет со спиной) — одним полем. Применение к бленду — spina_massa.py
def ss(t): t = np.clip(t, 0, 1); return t * t * (3 - 2 * t)
WSP = ss(V[:, 1] / 0.12) * ss((V[:, 2] - 0.95) / 0.15) * ss((1.60 - V[:, 2]) / 0.15) * ss((0.25 - np.abs(V[:, 0])) / 0.06)
NSP = 1 if opt['spina'] == '1' else 0


def spina(a_, Vx):
    return Vx + np.outer(WSP * a_ / 100.0, [0.0, 1.0, 0.0])


# МАССА РУК (`--ruki 1`, 30.10, критик n4: «рука — ровная труба»; ГеймБосс: «массу руки — по витрине, маской, а не по словам критика;
# лист даёт трубу — оставить трубу»): два параметра — радиальная толщина плеча и предплечья (%, общие для обеих рук); вершины с весом
# кости сдвигаются от её оси в покое пропорционально весу. Применение к бленду — ruki_massa.py
NRU = 2 if opt['ruki'] == '1' else 0
RU = []
for kn in ('плечо', 'плечо.L', 'предплечье', 'предплечье.L'):
    if kn not in BN: continue
    b_ = BN.index(kn); h_ = np.array(K[kn]['head'], float); t_ = np.array(K[kn]['tail'], float); u_ = (t_ - h_) / np.linalg.norm(t_ - h_)
    R_ = V - h_ - np.outer((V - h_) @ u_, u_)
    RU.append((0 if kn.startswith('плечо') else 1, W[:, b_][:, None] * R_))


def ruki(kk, Vx):
    for j, wr_ in RU: Vx = Vx + wr_ * (kk[j] / 100.0)
    return Vx


def norm512(fig):
    ys, xs = np.where(fig); fig = fig[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
    h, w = fig.shape; side = int(max(h, w) * 1.1); c = np.zeros((side, side), bool)
    c[side - h - (side - h) // 20: side - (side - h) // 20, (side - w) // 2:(side - w) // 2 + w] = fig
    idx = (np.arange(512) * side / 512).astype(int)
    return c[idx][:, idx]


def silujet(Y, ax, sgn):
    u = Y[:, ax] * sgn; v = Y[:, 2]; s = 1000 / max(u.max() - u.min(), v.max() - v.min())
    px = np.stack([(u - u.min()) * s + 10, (v.max() - v) * s + 10], -1)
    im = Image.new('1', (1024, 1024), 0); dr = ImageDraw.Draw(im)
    for t in px[F]: dr.polygon([tuple(t[0]), tuple(t[1]), tuple(t[2])], fill=1)
    return norm512(np.asarray(im, bool))


def maska(p): return np.asarray(Image.open(p).convert('L').resize((512, 512), Image.NEAREST)) < 128
def iou(s, m): return float((s & m).sum() / max((s | m).sum(), 1))


M = {k: maska(opt[k]) for k in ('front', 'profile', 'back') if opt.get(k)}
VES = dict(zip(('front', 'profile', 'back'), (float(x) for x in opt['vesa'].split(','))))
LAM, MU, PRED = float(opt['lam']), float(opt['mu']), float(opt['predel'])


def mery(Y):
    r = {}
    if 'front' in M: r['front'] = iou(silujet(Y, 0, -1), M['front'])
    if 'profile' in M:
        s = silujet(Y, 1, 1); r['profile'] = max(iou(s, M['profile']), iou(s[:, ::-1], M['profile']))
    if 'back' in M: r['back'] = iou(silujet(Y, 0, 1), M['back'])
    return r


# РАЗВОРОТЫ В ПОЗЕ — как у стенда: грань тела, чья нормаль в позе смотрит против нормали базового тела, повёрнутой главной костью
# первой вершины грани; штраф `--nu` за каждую (без него поза рук и головы добавляла 13–18 разворотов к r5)
BJ = {b: j for j, b in enumerate(BI)}


def razvoroty(Y, D2):
    Np_ = np.cross(Y[FT[:, 1]] - Y[FT[:, 0]], Y[FT[:, 2]] - Y[FT[:, 0]]); g = GL[FT[:, 0]]
    R = np.stack([D2[order[BJ[b]]] if b in BJ else np.eye(3) for b in range(len(BN))])
    RN = np.einsum('kij,kj->ki', R[g], NB_)
    ok = np.linalg.norm(np.cross(V[FT[:, 1]] - V[FT[:, 0]], V[FT[:, 2]] - V[FT[:, 0]]), axis=1) > 1e-10
    return int(((np.einsum('ki,ki->k', Np_, RN) < 0) & ok).sum())


def stan(delta):
    """поза и форма стопы → тело в позе, севшее нижней точкой меша на землю (Z мешa позы r5)"""
    nf = NST + NSP + NRU; dp = np.clip(delta[:len(delta) - nf], -PRED, PRED); D2, PH, PT = poza(dp)
    Vx = stopa(np.clip(delta[len(delta) - nf:len(delta) - nf + NST], -50, 20)) if NST else V
    if NSP: Vx = spina(float(np.clip(delta[len(delta) - NRU - 1], -5, 10)), Vx)
    if NRU: Vx = ruki(np.clip(delta[len(delta) - NRU:], -30, 30), Vx)
    Y = skin(D2, PH, Vx); dzm = ZM0[0] - Y[:, 2].min() if ZM0 else 0.0; Y[:, 2] += dzm
    sh = np.array([0, 0, dzm]); PH = {n: v + sh for n, v in PH.items()}; PT = {n: v + sh for n, v in PT.items()}
    return D2, PH, PT, Y, Vx


ZM0 = []
n_ev = [0]
def f(delta):
    D2, PH, PT, Y, Vx = stan(delta); r = mery(Y); n_ev[0] += 1
    dp = delta[:len(delta) - NST - NSP - NRU]; ds = delta[len(delta) - NST - NSP - NRU:]
    return (sum(VES[k] * (1 - v) for k, v in r.items()) + LAM * float(np.sum((np.clip(dp, -PRED, PRED) / 10) ** 2)) + LAM * float(np.sum((ds / 30) ** 2))
            + MU * abs(PT['стопа'][2] - PT['стопа.L'][2]) / 0.01 + float(opt['nu']) * razvoroty(Y, D2))


t0 = time.time()
NP = len(KOSTI) + (4 if NIK else 0) + NST + NSP + NRU
x = np.zeros(NP)
if NIK:   # знак: сдвиг скакательного правой ноги на +5 см назад должен сдвинуть его на +5 см
    for zn in (1.0, -1.0):
        ZN[0] = zn; t_ = np.zeros(NP); t_[len(KOSTI)] = 5.0; _, PHt, _ = poza(t_)
        if abs(PHt['стопа'][1] - PH0['стопа'][1] - 0.05) < 0.01: break
    print('ПОЗА-МАСКИ: знак поворота %+d, проверка сдвига скакательного %.3f м' % (ZN[0], PHt['стопа'][1] - PH0['стопа'][1]))
ZM0.append(float(skin(*poza(x)[:2])[:, 2].min()))
r0 = mery(skin(*poza(x)[:2]))
print('ПОЗА-МАСКИ: до — %s; разворотов %d; функционал %.4f (%.1f с на оценку)' % (', '.join('%s %.3f' % kv for kv in r0.items()), razvoroty(skin(*poza(x)[:2]), poza(x)[0]), f(x), time.time() - t0))
if opt.get('skan_ruki') and NRU:   # `--skan_ruki плечо:предплечье,…` (проценты) — меры при толщине рук, поза как есть
    for pr_ in opt['skan_ruki'].split(','):
        a1, a2 = (float(v) for v in pr_.split(':')); x_ = x.copy(); x_[-2:] = (a1, a2); D2s, PHs, PTs, Ys, _ = stan(x_); rs = mery(Ys)
        print('СКАН РУК плечо %+.0f %% предплечье %+.0f %%: %s; разворотов %d' % (a1, a2, ', '.join('%s %.3f' % kv for kv in rs.items()), razvoroty(Ys, D2s)))
    sys.exit(0)
if opt.get('skan_spina') and NSP:   # `--skan_spina 0,2,4` — меры при полноте спины, поза как есть
    for k in (float(v) for v in opt['skan_spina'].split(',')):
        x_ = x.copy(); x_[-1] = k; D2s, PHs, PTs, Ys, _ = stan(x_); rs = mery(Ys)
        print('СКАН СПИНЫ %+.1f см: %s; разворотов %d' % (k, ', '.join('%s %.3f' % kv for kv in rs.items()), razvoroty(Ys, D2s)))
    sys.exit(0)
if opt.get('skan_stopa') and NST:   # `--skan_stopa -40,-20,0` — функционал и меры при длине обеих стоп, поза как есть; без подбора
    for k in (float(v) for v in opt['skan_stopa'].split(',')):
        x_ = x.copy(); x_[len(x) - NSP - 2:len(x) - NSP] = k; D2s, PHs, PTs, Ys, _ = stan(x_); rs = mery(Ys)
        print('СКАН СТОПЫ %+.0f %%: %s; разворотов %d; функционал %.4f' % (k, ', '.join('%s %.3f' % kv for kv in rs.items()), razvoroty(Ys, D2s), f(x_)))
    sys.exit(0)
res = minimize(f, x, method='Powell', options=dict(maxiter=int(opt['iter']), xtol=0.5, ftol=1e-4))
x = res.x.copy(); D2, PH, PT, Y1, Vx1 = stan(x); r1 = mery(Y1)
print('ПОЗА-МАСКИ: после — %s; разворотов %d; функционал %.4f; оценок %d, %.0f с' % (', '.join('%s %.3f' % kv for kv in r1.items()), razvoroty(Y1, D2), f(x), n_ev[0], time.time() - t0))
print('ПОЗА-МАСКИ: повороты — ' + ', '.join('%s %+.1f°' % (k, v) for k, v in zip(KOSTI, x) if abs(v) > 0.05) + ('; скакательные (dy, dz см) — правый %+.1f %+.1f, левый %+.1f %+.1f' % tuple(x[len(KOSTI):len(KOSTI) + 4]) if NIK else '')
      + ('; длина стоп %+.1f %% / %+.1f %%' % tuple(np.clip(x[len(x) - NSP - 2:len(x) - NSP], -50, 20)) if NST else '')
      + ('; спина %+.2f см' % float(np.clip(x[len(x) - NRU - 1], -5, 10)) if NSP else '')
      + ('; руки: плечо %+.1f %%, предплечье %+.1f %%' % tuple(np.clip(x[-2:], -30, 30)) if NRU else ''))
print('ПОЗА-МАСКИ: стопы z %.3f / %.3f; колени y %.3f / %.3f, скакательные y %.3f / %.3f' % (
    PT['стопа'][2], PT['стопа.L'][2], PH['голень'][1], PH['голень.L'][1], PH['стопа'][1], PH['стопа.L'][1]))
out = []
for b in pz['bones']:
    n = b['name']; Fm = D2[n] @ frame(K[n])
    out.append(dict(b, head=[round(c, 5) for c in PH[n]], tail=[round(c, 5) for c in PT[n]], z_axis=[round(c, 5) for c in Fm[:, 2]]))
pz['bones'] = out
pz['note'] = pz.get('note', '') + '; поза по маскам листа (poza_po_maskam.py): ' + ', '.join('%s %+.1f°' % (k, v) for k, v in zip(KOSTI, x) if abs(v) > 0.05) + ('; скакательные dy/dz см %+.1f %+.1f %+.1f %+.1f' % tuple(x[len(KOSTI):len(KOSTI) + 4]) if NIK else '')
if NRU:
    rk_ = np.clip(x[-2:], -30, 30); pz['ruki_proc'] = {'плечо': round(float(rk_[0]), 2), 'предплечье': round(float(rk_[1]), 2)}
    pz['note'] += '; руки: плечо %+.1f %%, предплечье %+.1f %% (форма: ruki_massa.py)' % tuple(rk_)
if NSP:
    pz['spina_sm'] = round(float(np.clip(x[len(x) - NRU - 1], -5, 10)), 3); pz['note'] += '; спина %+.2f см (форма: spina_massa.py)' % pz['spina_sm']
if NST:
    ks = np.clip(x[len(x) - NSP - 2:len(x) - NSP], -50, 20); pz['stopa'] = {'стопа': round(1 + ks[0] / 100, 4), 'стопа.L': round(1 + ks[1] / 100, 4)}
    pz['note'] += '; длина стоп ×%.3f / ×%.3f (форма: stopa_dlina.py)' % tuple(1 + ks / 100)
json.dump(pz, open(out_p, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
if opt.get('kadr'):
    Y = Y1
    for nm, ax, sg in (('front', 0, -1), ('profile', 1, 1)):
        if nm not in M: continue
        s = silujet(Y, ax, sg); m = M[nm]
        if nm == 'profile' and iou(s[:, ::-1], m) > iou(s, m): s = s[:, ::-1]
        rgb = np.ones((512, 512, 3)); rgb[s & ~m] = (0.9, 0.2, 0.2); rgb[m & ~s] = (0.3, 0.5, 0.95); rgb[s & m] = (0.15, 0.15, 0.15)
        Image.fromarray((rgb * 255).astype(np.uint8)).save('%s_%s.png' % (opt['kadr'], nm))
