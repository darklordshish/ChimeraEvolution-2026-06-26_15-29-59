# -*- coding: utf-8 -*-
"""СОГЛАСИЕ ЛИСТА — годится ли сгенерированный лист как ортопроекции одного тела (любой вид). Обобщение
`Anatomy/species/human/list_soglasie.py` на листы без готовых контуров.

Что обязано совпадать у ортопроекций одного тела:
  • ВЫСОТА — верх и низ фигуры одинаковы во всех четырёх видах (ортокамера не укрупняет близкое: у перспективы голова
    в анфас выше, чем в профиль);
  • АНФАС ↔ СПИНА — один силуэт зеркально;  ПРОФИЛЬ Л ↔ П — один силуэт зеркально.
Масштаб общий на лист (рендеры одного листа в одном масштабе, это и проверяется высотами), выравнивание — по земле
и лучшему сдвигу по горизонтали. Итог в долях роста фигуры в профиль и в пикселях; IoU — доля общего силуэта пары.

Фигура в панели — самая крупная связная область ярче фона (тень под лапами порогом не проходит; если проходит —
поднять thr). Панели — x0 x1 y0 y1 в пикселях листа.

Запуск:  python soglasie.py лист.png '{"front":[x0,x1,y0,y1],"side":[…],"back":[…],"side_r":[…]}' [thr] [out.png]
"""
import json
import sys

import numpy as np
from PIL import Image
from scipy import ndimage as nd


def figure(L, box, thr):
    x0, x1, y0, y1 = box
    m = nd.binary_closing(L[y0:y1, x0:x1] > thr, iterations=2)
    lab, n = nd.label(m)
    if n == 0:
        raise SystemExit('пустая панель %s' % box)
    big = 1 + int(np.argmax(nd.sum(m, lab, range(1, n + 1))))
    f = nd.binary_fill_holes(lab == big)
    ys = np.where(f.any(1))[0]
    return f[ys.min():ys.max() + 1], y0 + ys.min(), y0 + ys.max()


def pair(a, b):
    """b зеркально, по земле; лучший сдвиг по горизонтали → (IoU, расхождение краёв по строкам в px: [(строка от земли, dl, dr)])."""
    b = b[:, ::-1]
    h = max(len(a), len(b))
    A = np.zeros((h, a.shape[1]), bool); A[h - len(a):] = a
    B = np.zeros((h, b.shape[1]), bool); B[h - len(b):] = b
    best = None
    for s in range(-B.shape[1], A.shape[1]):
        w = max(A.shape[1], s + B.shape[1]) - min(0, s)
        P = np.zeros((h, w), bool); Q = np.zeros((h, w), bool)
        o = -min(0, s)
        P[:, o:o + A.shape[1]] = A; Q[:, o + s:o + s + B.shape[1]] = B
        iou = (P & Q).sum() / max(1, (P | Q).sum())
        if best is None or iou > best[0]:
            best = (iou, P, Q)
    iou, P, Q = best
    rows = []
    for r in range(h):
        p, q = np.where(P[r])[0], np.where(Q[r])[0]
        if len(p) and len(q):
            rows.append((h - 1 - r, p.min() - q.min(), p.max() - q.max()))
    return iou, rows, P, Q


def bands(rows, H):
    out = []
    for lo, hi, name in ((0.66, 1.01, 'верх'), (0.33, 0.66, 'середина'), (0.0, 0.33, 'низ')):
        d = [abs(x) for r, a, b in rows if lo * H <= r < hi * H for x in (a, b)]
        out.append((name, np.mean(d) if d else 0, max(d) if d else 0))
    return out


def main(sheet, panels, thr=55, out=None):
    L = np.asarray(Image.open(sheet).convert('L')).astype(int)
    F = {n: figure(L, b, thr) for n, b in panels.items()}
    H = F['side'][0].shape[0]
    print('ВЫСОТА фигуры, px (верх — низ): ' + ', '.join('%s %d (%d–%d)' % (n, f[0].shape[0], f[1], f[2]) for n, f in F.items()))
    hs = [f[0].shape[0] for f in F.values()]
    print('  разброс высот: %d px = %.1f %% роста в профиль' % (max(hs) - min(hs), 100.0 * (max(hs) - min(hs)) / H))
    img = []
    for a, b, name in (('front', 'back', 'АНФАС ↔ СПИНА'), ('side', 'side_r', 'ПРОФИЛЬ Л ↔ П')):
        if a not in F or b not in F:
            continue
        iou, rows, P, Q = pair(F[a][0], F[b][0])
        print('%s: IoU %.3f; расхождение краёв, px (сред / макс) и %% роста:' % (name, iou))
        for n, m, x in bands(rows, max(len(P), 1)):
            print('   %-9s %5.1f / %-5d  = %4.1f / %4.1f %%' % (n, m, x, 100 * m / H, 100 * x / H))
        rgb = np.zeros(P.shape + (3,), np.uint8) + 30
        rgb[P & ~Q] = (230, 80, 60); rgb[Q & ~P] = (240, 200, 60); rgb[P & Q] = (130, 134, 142)
        img.append(rgb)
    if out and img:
        h = max(i.shape[0] for i in img)
        can = np.zeros((h, sum(i.shape[1] for i in img) + 20 * len(img), 3), np.uint8) + 30
        x = 0
        for i in img:
            can[h - i.shape[0]:, x:x + i.shape[1]] = i; x += i.shape[1] + 20
        Image.fromarray(can).resize((can.shape[1] * 2, can.shape[0] * 2), Image.NEAREST).save(out)


if __name__ == '__main__':
    a = sys.argv
    main(a[1], json.loads(a[2]), int(a[3]) if len(a) > 3 else 55, a[4] if len(a) > 4 else None)
