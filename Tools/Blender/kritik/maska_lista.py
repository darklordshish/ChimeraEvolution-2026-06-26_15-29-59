# -*- coding: utf-8 -*-
"""МАСКА СИЛУЭТА С ЛИСТА — для детектора силуэтов механик (`chimera-silhouette`, письмо 2026-10-01e) и метрики M5
эксперимента консилиума. Фигура листа светлая на тёмном фоне → порог яркости, крупнейшая связная область, дыры залиты.
Формат (письмо 01e §2): фигура ЧЁРНАЯ НА БЕЛОМ, без земли и подписей, низ фигуры — земля, `profile` — морда ВПРАВО.

Запуск:  python maska_lista.py вход.png выход.png [--crop x0,y0,x1,y1] [--flip] [--porog 70]
"""
import argparse

import numpy as np
from PIL import Image
from scipy import ndimage

ap = argparse.ArgumentParser()
ap.add_argument('src'); ap.add_argument('dst')
ap.add_argument('--crop', default=None, help='x0,y0,x1,y1 — оставить только эту область (соседние фигуры листа)')
ap.add_argument('--flip', action='store_true', help='отразить по горизонтали (морда должна смотреть вправо)')
ap.add_argument('--porog', type=int, default=70, help='порог яркости фигуры (0–255)')
ap.add_argument('--zakryt', type=int, default=4, help='радиус морфологического закрытия, px: тёмные щели гривы и тени — не дыры силуэта')
a = ap.parse_args()

im = Image.open(a.src).convert('L')
if a.crop:
    im = im.crop(tuple(int(v) for v in a.crop.split(',')))
g = np.asarray(im)
m = g > a.porog
lab, n = ndimage.label(m)
if n == 0:
    raise SystemExit('фигуры нет: порог %d слишком высок' % a.porog)
sizes = ndimage.sum(m, lab, range(1, n + 1))
m = lab == (1 + int(np.argmax(sizes)))
if a.zakryt > 0:
    yy, xx = np.ogrid[-a.zakryt:a.zakryt + 1, -a.zakryt:a.zakryt + 1]
    m = ndimage.binary_closing(m, structure=xx * xx + yy * yy <= a.zakryt * a.zakryt, iterations=1)
m = ndimage.binary_fill_holes(m)
ys, xs = np.nonzero(m)
m = m[ys.min():ys.max() + 1, xs.min():xs.max() + 1]          # обрезка по фигуре: низ фигуры — земля
if a.flip:
    m = m[:, ::-1]
pad = 8
out = np.full((m.shape[0] + 2 * pad, m.shape[1] + 2 * pad), 255, np.uint8)
out[pad:pad + m.shape[0], pad:pad + m.shape[1]][m] = 0
Image.fromarray(out).save(a.dst)
print('%s: фигура %d × %d px, доля %.3f' % (a.dst, m.shape[1], m.shape[0], m.mean()))
