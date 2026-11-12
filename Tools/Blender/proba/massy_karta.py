"""КАРТА МАСС «ТЕЛО → ЛИСТ» (04.11, ряд вариаций массы). По высоте сравниваются контуры плотной базы и `огранка.png`: анфас — интервалы половины фигуры (торс, рука, нога; обе половины листа усреднены), профиль — перед и зад. Трусы на листе закрывают таз и верх бедра: эти высоты интерполируются между соседними. Выход — таблица для `massy_po_listu.py` и маски листа для IoU (правило `stend_k2.py`: чёрная фигура, 512×512).

  python massy_karta.py огранка.png тело.npz карта.json маска_анфас.png маска_профиль.png
"""
import sys, json
import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage as ndi

Lf, Tf, Out, Mf, Mp = sys.argv[1:6]
g = np.asarray(Image.open(Lf).convert('L')); S = 806 / 1.85; Y0 = 829
B = np.load(Tf); V, F = B['V'], B['F']
ZS = np.round(np.arange(0.10, 1.62, 0.01), 3)


def mask_list(x0, x1):
    s = g[:826, x0:x1] < 235; return ndi.binary_opening(ndi.binary_fill_holes(ndi.binary_closing(s, iterations=2)), iterations=1)


def norm512(fig):
    ys, xs = np.where(fig); fig = fig[ys.min():ys.max() + 1, xs.min():xs.max() + 1]; h, w = fig.shape; side = int(max(h, w) * 1.1); c = np.zeros((side, side), bool)
    c[side - h - (side - h) // 20: side - (side - h) // 20, (side - w) // 2:(side - w) // 2 + w] = fig; idx = (np.arange(512) * side / 512).astype(int); return c[idx][:, idx]


Fm, Pm = mask_list(40, 400), mask_list(450, 640)
Image.fromarray(np.where(norm512(Fm), 0, 255).astype(np.uint8)).save(Mf); Image.fromarray(np.where(norm512(Pm), 0, 255).astype(np.uint8)).save(Mp)


def rast(u, z, W, cx):
    im = Image.new('1', (W, 900), 0); d = ImageDraw.Draw(im)
    for f in F: d.polygon([(float(cx + S * u[q]), float(Y0 - S * z[q])) for q in f], fill=1)
    return np.asarray(im)


Bm = rast(V[:, 0], V[:, 2], 500, 250); Bp = rast(V[:, 1], V[:, 2], 500, 250)


def inter(r, c, sg, mn=3):
    q = np.nonzero(r)[0]
    if not len(q): return []
    R = np.split(q, np.nonzero(np.diff(q) > 1)[0] + 1); return [((a[0] - c) * sg / S, (a[-1] + 1 - c) * sg / S) for a in R if len(a) >= mn]


def poloviny(M, c):
    """полуфигура: столбцы правее оси и зеркало левой, среднее по границам — через маску-среднее"""
    return M


T = {k: [] for k in ('z', 'kol', 'ms', 'ls')}; Rk = []
for z in ZS:
    y_l = int(round(Y0 - S * z)); y_m = y_l
    # лист: ось 219 в кадре со сдвигом 40
    r_ = Fm[y_l] ; c = 219 - 40; pr = r_[c:]; lv = r_[:c][::-1]
    Il, Ip = inter(lv, 0, 1), inter(pr, 0, 1)
    Is = [((a[0] + b[0]) / 2, (a[1] + b[1]) / 2) for a, b in zip(Il, Ip)] if len(Il) == len(Ip) else Ip
    r2 = Bm[y_m]; Im = inter(r2[250:], 0, 1)
    q_ = np.nonzero(Pm[y_l])[0]; ql = ((q_[0] + 450 - 545) / S, (q_[-1] + 1 + 450 - 545) / S) if len(q_) else None
    q2 = np.nonzero(Bp[y_m])[0]; qm = ((q2[0] - 250) / S, (q2[-1] + 1 - 250) / S) if len(q2) else None
    Rk.append(dict(z=float(z), list=Is, telo=Im, prof_list=ql, prof_telo=qm))
json.dump(Rk, open(Out, 'w', encoding='utf-8'), ensure_ascii=False)
for r in Rk[::10]:
    print('z %.2f тело %s лист %s | профиль тело %s лист %s' % (r['z'], [(round(a, 3), round(b, 3)) for a, b in r['telo']], [(round(a, 3), round(b, 3)) for a, b in r['list']], tuple(round(x, 3) for x in r['prof_telo']) if r['prof_telo'] else None, tuple(round(x, 3) for x in r['prof_list']) if r['prof_list'] else None))
