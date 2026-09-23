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


def load_obj(path):
    """Вершины и треугольники OBJ (грани веером; индексы `v/vt/vn`, отрицательные — от конца)."""
    V, T = [], []
    for line in open(path):
        if line.startswith('v '):
            V.append(tuple(map(float, line.split()[1:4])))
        elif line.startswith('f '):
            ids = [int(w.split('/')[0]) for w in line.split()[1:]]
            ids = [i - 1 if i > 0 else len(V) + i for i in ids]
            T.extend((ids[0], ids[k], ids[k + 1]) for k in range(1, len(ids) - 1))
    return V, T


def section(V, T, h):
    """СРЕЗ ОБОЛОЧКИ ПЛОСКОСТЬЮ y = h — точки пересечения рёбер. Вершины слоем ±1 см не годятся: на сетке main узлы идут
    через клетку (4.2 см), и половина высот попадала между рядами — сверка врала на 17 см (23.09, переезд в свою папку)."""
    out = []
    for t in T:
        for i, j in ((t[0], t[1]), (t[1], t[2]), (t[2], t[0])):
            a, b = V[i], V[j]
            if (a[1] - h) * (b[1] - h) < 0 and a[1] != b[1]:
                k = (h - a[1]) / (b[1] - a[1])
                out.append((-(a[0] + (b[0] - a[0]) * k), h, a[2] + (b[2] - a[2]) * k))   # X зеркален выгрузкой
    return out


def ours(path, torso=0.16):
    V, T = load_obj(path)
    out = {}
    for h in HEIGHTS:
        pts = [p for p in section(V, T, h) if abs(p[0]) < torso]
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
