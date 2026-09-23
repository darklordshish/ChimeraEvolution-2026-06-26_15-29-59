# -*- coding: utf-8 -*-
"""СВЕРКА РУКИ ПО ДОЛЯМ ДЛИНЫ — ширина оболочки анфас против листа на тех же долях сегмента.

Руки по длине с листом расходятся намеренно (суставы по данным), поэтому высотой их не сверить: сверка — на доле t плеча
и предплечья. Ширина — поперёк руки (горизонтальный отрезок × cos наклона). Наша — с ортокадра анфас (`ProbeM.Shot`,
вид `nose`, 1000 px на 2 м, центр кадра на высоте 0.95; справа в кадре −X, правая рука — слева), листа — `graph.front`.

Запуск:  python arm_fit.py кадр-анфас.png   → таблица t: лист / мы / невязка
"""
import math
import sys

import numpy as np
from PIL import Image

import graph as G


def ours_width(img, a, b, t, near):
    """Ширина нашей руки поперёк на доле t сегмента a→b (мир, правая рука)."""
    L = np.asarray(img.convert('L')).astype(int) > 60
    p = G.lerp(a, b, t)
    row = int(round(500 - (p[1] - 0.95) / 0.002))
    col_axis = 500 - (p[0] + near) / 0.002
    runs, start = [], None
    for c, v in enumerate(L[row]):
        if v and start is None:
            start = c
        if not v and start is not None:
            runs.append((start, c - 1)); start = None
    run = min(runs, key=lambda r: abs((r[0] + r[1]) / 2 - col_axis))
    cos = abs(a[1] - b[1]) / math.hypot(b[0] - a[0], b[1] - a[1])
    return (run[1] - run[0] + 1) * 0.002 * cos


def table(path):
    img = Image.open(path)
    out = []
    for upper, ts in ((True, G.UPPER_T), (False, G.FORE_T)):
        a, b = (G.SHOULDER, G.ELBOW) if upper else (G.ELBOW, G.WRIST)
        for t in ts:
            c, hw, _ = G.arm_level(t, upper)
            ref = 2 * (hw - G.ARM_GROW - G.arm_fit(t, upper))
            shift = c[0] - G.lerp(a, b, t)[0]
            w = ours_width(img, a, b, t, shift)
            out.append((('плечо' if upper else 'предплечье'), t, ref, w))
    return out


if __name__ == '__main__':
    for seg, t, ref, w in table(sys.argv[1]):
        print('%-10s t %.2f  лист %.3f  мы %.3f  невязка %+.3f' % (seg, t, ref, w, ref - w))
