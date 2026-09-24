# -*- coding: utf-8 -*-
"""3D-ОБРАЗЕЦ ПРОТИВ ЛИСТА — ортосилуэт GLB из multiview-генератора против фигуры в панели листа (любой вид).

Образец собирается из ракурсов листа (Hunyuan3D-2mv), и сначала проверяется, что генератор не выдумал: его силуэт в
профиль (камера сбоку, морда влево — как SIDE LEFT листа) и в анфас против фигуры листа. Выравнивание — по земле и
лучшему сдвигу (`soglasie.pair`), масштаб — ЛУЧШИЙ в ±15 % от высоты фигуры: по полной высоте мерить нельзя: у лося
нормировка по макушке давала IoU 0.66, подбор — 0.69 при масштабе 0.975 (кайма шла от расхождения профилей листа). Масштаб
печатается: далёкий от 1 — сигнал, что верх фигуры (рога, уши) у образца и листа разный. Итог — IoU и расхождение краёв
по поясам в % высоты.
Оси образца: Y вверх, морда по +Z (так отдаёт генератор; проверено на человеке, волке и лосе).

Запуск:  python obrazec_sverka.py образец.glb лист.png '{"profile":[x0,x1,y0,y1],"front":[…]}' [thr] [out.png]
"""
import json
import os
import sys

import numpy as np
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, '..', '..', '..', 'Anatomy', 'species', 'human'))
import soglasie as SG                                   # noqa: E402
from mv_sverka import load_glb                          # noqa: E402

VIEWS = {'profile': (2, -1), 'profile_r': (2, 1), 'front': (0, -1), 'back': (0, 1)}   # ось образца по горизонтали и знак


def silhouette(V, T, axis, sign, H):
    u, y = sign * V[:, axis], V[:, 1]
    k = (H - 1) / (y.max() - y.min())
    im = Image.new('L', (int((u.max() - u.min()) * k) + 3, H + 2))
    d = ImageDraw.Draw(im)
    px = np.stack([(u - u.min()) * k + 1, (y.max() - y) * k + 1], 1)
    for t in T:
        d.polygon([tuple(px[i]) for i in t], fill=255)
    m = np.asarray(im) > 0
    ys = np.where(m.any(1))[0]
    return m[ys.min():ys.max() + 1]


def main(glb, sheet, panels, thr=55, out=None):
    V, T = load_glb(glb)
    L = np.asarray(Image.open(sheet).convert('L')).astype(int)
    img = []
    for name, box in panels.items():
        ref = SG.figure(L, box, thr)[0]
        best = None
        for sc in np.linspace(0.85, 1.15, 13):
            m = silhouette(V, T, *VIEWS[name], int(round(ref.shape[0] * sc)))
            r = SG.pair(ref, m[:, ::-1])                # pair зеркалит второй — здесь зеркало не нужно
            if best is None or r[0] > best[1][0]:
                best = (sc, r)
        sc, (iou, rows, P, Q) = best
        print('%-8s масштаб %.3f  IoU %.3f   ' % (name, sc, iou) + '  '.join('%s %.1f / %.1f %%' % (n, 100 * a / len(P), 100 * b / len(P))
                                                        for n, a, b in SG.bands(rows, len(P))))
        rgb = np.zeros(P.shape + (3,), np.uint8) + 30
        rgb[P & ~Q] = (230, 80, 60); rgb[Q & ~P] = (240, 200, 60); rgb[P & Q] = (130, 134, 142)
        img.append(rgb)
    if out:
        h = max(i.shape[0] for i in img)
        can = np.zeros((h, sum(i.shape[1] for i in img) + 20 * len(img), 3), np.uint8) + 30
        x = 0
        for i in img:
            can[h - i.shape[0]:, x:x + i.shape[1]] = i; x += i.shape[1] + 20
        Image.fromarray(can).resize((can.shape[1] * 2, can.shape[0] * 2), Image.NEAREST).save(out)
        print('наложение: лист красным, образец жёлтым, общее серым →', out)


if __name__ == '__main__':
    a = sys.argv
    main(a[1], a[2], json.loads(a[3]), int(a[4]) if len(a) > 4 else 55, a[5] if len(a) > 5 else None)
