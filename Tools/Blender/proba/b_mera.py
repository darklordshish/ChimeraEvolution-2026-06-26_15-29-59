"""СРЕЗЫ ТЕЛА ПО ЛИСТАМ (05.11, проверка чисел критика до правки): собранная сетка против `вид.png` и `огранка.png` по высотам, анфас: полуширина шеи и пояса, интервал руки, ноги. Лист нормирован по росту 1.85 м.

  python b_mera.py тело.npz вид.png огранка.png
Тело — выгрузка V, F из Blender.
"""
import sys
import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage as ndi

Tf, Vd, Og = sys.argv[1:4]; B = np.load(Tf); V, F = B['V'], B['F']; S0 = 435.7
def mk(f, x0, x1, ym):
    s = np.asarray(Image.open(f).convert('L'))[:ym, x0:x1] < 235; m = ndi.binary_opening(ndi.binary_fill_holes(ndi.binary_closing(s, iterations=2)), iterations=1); y = np.nonzero(m.any(1))[0]; x = np.nonzero(m[y.min() + 5:y.max()].any(0))[0]; return m, 1.85 / (y.max() - y.min()), y.max(), (x.min() + x.max() + 1) / 2
Mv, sv, yv, cv = mk(Vd, 0, 415, 905); Mo, so, yo, co = mk(Og, 40, 400, 826)
im = Image.new('1', (1000, 1000), 0); d = ImageDraw.Draw(im); SS = 500
for f in F: d.polygon([(500 + SS * V[q, 0], 950 - SS * V[q, 2]) for q in f], fill=1)
Mt = np.asarray(im)
def inter(M, y, c, s):
    r = M[int(round(y))]; pr = np.nonzero(r[int(c):])[0]; lv = np.nonzero(r[:int(c)][::-1])[0]
    def run(q):
        if not len(q): return []
        return [(a[0] * s, (a[-1] + 1) * s) for a in np.split(q, np.nonzero(np.diff(q) > 1)[0] + 1) if len(a) >= 2]
    A_, B_ = run(pr), run(lv); return [((a[0] + b[0]) / 2, (a[1] + b[1]) / 2) for a, b in zip(A_, B_)] if len(A_) == len(B_) else A_
f_ = lambda I: ' '.join('%.3f-%.3f' % x for x in I)
print('z     тело                 вид                  огранка')
for z in [1.64, 1.62, 1.60, 1.58, 1.56, 1.54, 1.52, 1.50, 1.44, 1.38, 1.30, 1.25, 1.20, 1.15, 1.10, 1.05, 1.00, 0.95, 0.60, 0.50, 0.40, 0.33, 0.25, 0.15]:
    print('%.2f  %-30s %-30s %-30s' % (z, f_(inter(Mt, 950 - SS * z, 500, 1 / SS)), f_(inter(Mv, yv - z / sv, cv, sv)), f_(inter(Mo, yo - z / so, co, so))))
