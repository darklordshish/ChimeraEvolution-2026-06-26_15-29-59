# -*- coding: utf-8 -*-
"""СВЕРКА АНФАС ПО ВЫСОТЕ — полуширина оболочки против листа там, где тело по высоте с листом совпадает: шея, скат
трапеции, верх плеча (1.40–1.62).

Скат «шея — плечо» ловится только так: ширина на каждом уровне в допуске ±1 см, а начало ската на шее выше листа на 2 см
— шея «бычья» (четвёртая рецензия 23.09: на 1.566 у нас 18.3 при листе 12.4). Наша — срез OBJ плоскостью (`profile_check`),
лист — `graph.front`.

Запуск:  python front_check.py оболочка.obj [y_низ y_верх]
"""
import sys

import graph as G
from profile_check import load_obj, section


def main(path, lo=1.40, hi=1.62):
    V, T = load_obj(path)
    print('высота   лист   мы     Δ')
    y = hi
    while y >= lo - 1e-9:
        pts = section(V, T, y)
        ref = G.front(y, -0.40, 0.40)
        if pts and ref:
            ours = max(abs(p[0]) for p in pts)
            print('%.3f   %.3f  %.3f  %+.3f' % (y, ref[1], ours, ours - ref[1]))
        y = round(y - 0.01, 3)


if __name__ == '__main__':
    main(sys.argv[1], *map(float, sys.argv[2:4]))
