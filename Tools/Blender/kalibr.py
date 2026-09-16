# -*- coding: utf-8 -*-
"""ДЕТЕКТОР КАЛИБРА: вид обязан пересчитываться ЦЕЛИКОМ одним числом.

Правило записано в каждом файле вида («все размеры вида задаются долями: так вид можно пересчитать
целиком») и до сих пор нигде не проверялось. Проверка простая и не требует знать анатомию: собрать
вид при калибре W и при 2W. Если правило соблюдено, ВСЕ координаты и толщины обязаны удвоиться
ровно. Кость, у которой это не так, содержит число в метрах — своё или унаследованное.

Ловится именно то, на чём мы горели дважды: у ежа смена калибра с 0.320 на 0.935 (в 2.92 раза)
удлинила зверя лишь в 2.37 раза, потому что часть чисел не была долями. Габаритом такое не видно —
видно только поимённо.
"""
import io
import os
import re
import sys
import importlib
import tempfile

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')

from chimera.skel import from_points   # noqa: E402

K = 2.0            # во столько раз растягиваем калибр
EPS = 2e-2         # 2%: w() квантует до 0.1 мм, на тонкой кости это уже почти процент


def собрать(mod, множитель=1.0):
    """Сборка вида при калибре, умноженном на `множитель`. Файл подменяется во временной копии —
    трогать исходник нельзя, а импортировать дважды с разным W иначе не выйдет."""
    путь = 'species/%s.py' % mod
    src = io.open(путь, encoding='utf-8').read()
    if множитель != 1.0:
        m = re.search(r'^W = ([\d.]+)', src, re.M)
        src = src[:m.start()] + 'W = %.10f' % (float(m.group(1)) * множитель) + src[m.end():]
    имя = '_kal_%s' % mod
    tmp = os.path.join('species', имя + '.py')
    io.open(tmp, 'w', encoding='utf-8').write(src)
    try:
        sp = importlib.import_module('species.' + имя)
        importlib.reload(sp)
        bones, pl, _ = from_points(sp.DEFS, sp.P, verbose=False)
        return sp.W, {b.name: b for b in bones}, pl
    finally:
        os.remove(tmp)
        for f in os.listdir('species/__pycache__') if os.path.isdir('species/__pycache__') else []:
            if f.startswith(имя):
                os.remove(os.path.join('species/__pycache__', f))


def проверить(mod):
    W1, by1, pl1 = собрать(mod, 1.0)
    W2, by2, pl2 = собрать(mod, K)
    масштаб = W2 / W1
    беды = []
    ИМЕНА = ['начало x', 'начало y', 'начало z', 'конец x', 'конец y', 'конец z',
             'r0', 'r1', 'длина']
    for n in pl1:
        a, b = pl1[n], pl2[n]
        # координаты начала и конца плюс толщины — всё, чем кость задаёт размер
        знач1 = list(a[0]) + list(a[2]) + [by1[n].r0, by1[n].r1, by1[n].length]
        знач2 = list(b[0]) + list(b[2]) + [by2[n].r0, by2[n].r1, by2[n].length]
        худ, кто = 0.0, ''
        for имя, v1, v2 in zip(ИМЕНА, знач1, знач2):
            ожид = v1 * масштаб
            d = abs(v2 - ожид)
            # НОРМИРОВАТЬ НА САМО ЧИСЛО НЕЛЬЗЯ: у координаты около нуля любой шум даёт сотни
            # процентов. Опорой берём калибр — расхождение меряется в долях размера зверя
            норма = max(abs(ожид), W1 * 0.02)
            if d / норма > худ:
                худ, кто = d / норма, имя
        if худ > EPS:
            беды.append((худ, n, by1[n].socket, кто))
    return W1, len(pl1), sorted(беды, reverse=True)


if __name__ == '__main__':
    виды = sys.argv[1:] or ['wolf', 'human', 'moose', 'hedgehog', 'snake']
    плохо = 0
    for mod in виды:
        W, всего, беды = проверить(mod)
        if not беды:
            print('%-9s W=%.3f · %3d костей · ВЕСЬ ВИД В ДОЛЯХ' % (mod, W, всего))
            continue
        плохо += 1
        print('%-9s W=%.3f · %3d костей · НЕ В ДОЛЯХ: %d' % (mod, W, всего, len(беды)))
        for худ, n, sock, кто in беды[:25]:
            print('        %-20s [%-7s] %-9s не масштабируется, расхождение %.0f%%'
                  % (n, sock, кто, худ * 100))
        if len(беды) > 25:
            print('        ... и ещё %d' % (len(беды) - 25))
    sys.exit(1 if плохо else 0)
