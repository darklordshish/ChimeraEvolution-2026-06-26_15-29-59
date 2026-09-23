# -*- coding: utf-8 -*-
"""СВЕРКА ПРОФИЛЯ — задний и передний край силуэта в профиль (оболочка против листа) и ПЕРЕПАДЫ «пик — впадина».

Сверка «ширина и глубина на уровне ±2 см» слепа к ошибкам одного знака: каждая в допуске, а вместе они вдвое гасят
S-линию спины (рецензия 23.09: лопатки → поясница у листа 7.9 см, у нас 4.7; ягодица над поясницей +3.9 против +0.8).
Поэтому меряется и рельеф линии: пик лопаток (1.30–1.50), впадина поясницы (1.00–1.20), пик ягодиц (0.85–1.02).

Наша оболочка — OBJ, выгруженный стендом (`ExportObj`: X зеркален, Y вверх, Z вперёд). Лист — `graph.side` (уже со сдвигом DZ).
МЫ — ТОЛЬКО ТОРС (|x| < 0.16): руки по данным длиннее листа, локоть на 5 см ниже, и на высоте поясницы у нас висит верх
предплечья, отнесённый назад, а у листа — середина предплечья, ушедшая вперёд. Силуэт целиком мерил бы руку, а не спину.
Запуск:  python profile_check.py оболочка.obj
"""
import sys

import graph as G

HEIGHTS = [round(1.55 - 0.02 * i, 2) for i in range(36)]


def ours(path, torso=0.16):
    V = []
    for line in open(path):
        if line.startswith('v '):
            x, y, z = map(float, line.split()[1:4])
            if abs(x) < torso:
                V.append((-x, y, z))
    out = {}
    for h in HEIGHTS:
        pts = [v for v in V if abs(v[1] - h) < 0.011]
        if pts:
            out[h] = (min(p[2] for p in pts), max(p[2] for p in pts))
    return out


def relief(prof):
    def pick(lo, hi, f):
        return f((prof[h][0], h) for h in prof if lo <= h <= hi)
    peak_back = pick(1.30, 1.50, min)            # самая задняя точка лопаток (z минимален)
    lumbar = pick(1.00, 1.20, max)               # поясница — самая передняя точка задней линии
    glute = pick(0.85, 1.02, min)                # ягодица
    return peak_back, lumbar, glute


def main(path):
    o = ours(path)
    r = {h: G.side(h) for h in HEIGHTS}
    print('высота   лист зад  мы зад   Δ      | лист перед  мы перед  Δ')
    for h in HEIGHTS:
        if h in o:
            print('%.2f    %.3f   %.3f  %+.3f  |  %.3f     %.3f   %+.3f' % (h, r[h][0], o[h][0], o[h][0] - r[h][0], r[h][1], o[h][1], o[h][1] - r[h][1]))
    for name, prof in (('лист', r), ('мы', o)):
        (pb, hb), (lu, hl), (gl, hg) = relief(prof)
        print('%s: лопатки %.3f на %.2f · поясница %.3f на %.2f · ягодица %.3f на %.2f → лопатки−поясница %.3f, ягодица−поясница %.3f'
              % (name, pb, hb, lu, hl, gl, hg, lu - pb, lu - gl))


if __name__ == '__main__':
    if len(sys.argv) > 2 and sys.argv[2] == '--весь':   # весь силуэт с руками — как видит профиль глаз
        _o = ours
        ours = lambda path: _o(path, torso=9.0)
    main(sys.argv[1])
