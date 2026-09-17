# -*- coding: utf-8 -*-
"""РАСКЛАДКА НИЖНИХ НОГ ВОЛКА — пясть, плюсна и лапы ригблоками на концах узлов (поставка 3, письмо 17.09c §4).

КОНТРАКТ КРЕПЛЕНИЯ (механики, §4 письма) и как он здесь прочитан:
  • часть называет УЗЕЛ, на конце которого висит: `предплечье` (передняя), `голень` (задняя);
  • КАДР ЧАСТИ — «тело узла»: начало в конце узла, поворот узла × Euler(−90, 0, 0). Это тот же поворот, которым
    `MorphBuilder` уже ставит место на кость, поэтому второго правила кадров не заводится:
        +Z — вдоль узла (к земле у ног), +Y — перёд узла (−Z кости), +X — наружу-вбок;
  • ЕДИНИЦЫ — доли узла: по Z — длины узла, по X и Y — диаметра узла на его конце (2·r1). Метров нет;
  • euler — поза сегмента в этом кадре (углы сустава);
  • шов перекрыт: брусок начинается ВНУТРИ узла на свою толщину, а не встык с его концом.

ОТКУДА ЧИСЛА. Суставы — те же точки `graph.py` со снимка `wolf_standing_1.jpg`. Наклон пясти и плюсны снят
отдельно: запястье (1255, 1300) → лапа (1310, 1455) даёт 19.5° вперёд от отвеса, скакательный (170, 1150) →
лапа (240, 1455) — 12.9°. Лапа стоит подушкой на земле: низ блока `лапа` в Y = 0.

ПОСТАВКА 4 (клетка 0.084): поле ноги кончается там, где нога ещё не уже 0.1 м (`graph.FORE_END`, `graph.HIND_END`),
поэтому блоков на узле три: низ предплечья или голени, пясть или плюсна, лапа.

Запуск:  python organs_layout.py [--out путь]
"""
import argparse
import json
import math
import os

import graph as G

HERE = os.path.dirname(os.path.abspath(__file__))


def node_by_name(name):
    for n in G.build_nodes():
        if n['name'] == name:
            return n
    raise KeyError(name)


def sub(a, b):
    return tuple(x - y for x, y in zip(a, b))


def add(a, b):
    return tuple(x + y for x, y in zip(a, b))


def mul(a, k):
    return tuple(x * k for x in a)


def dot(a, b):
    return sum(x * y for x, y in zip(a, b))


def unit(a):
    l = math.sqrt(dot(a, a))
    return tuple(x / l for x in a)


def body_frame(n):
    """Оси «тела узла» в мире. Узел лежит в плоскости YZ, его мировой угол θ = atan2(dz, dy):
    кость +Y = (0, cosθ, sinθ), кость +Z = (0, −sinθ, cosθ); тело: Z = кость +Y, Y = −(кость +Z), X = X."""
    d = unit(sub(n['b'], n['a']))
    zb = d
    yb = (0.0, d[2], -d[1])          # −(кость +Z) = (0, sinθ, −cosθ), а sinθ = dz, cosθ = dy
    xb = (1.0, 0.0, 0.0)
    return xb, yb, zb


def part(n, block, centre, size, axis_dir, up_hint=None):
    """Часть в долях узла. centre — центр блока в мире, size — (ширина, толщина-вперёд, длина) в метрах,
    axis_dir — куда смотрит +Z блока в мире."""
    xb, yb, zb = body_frame(n)
    L = math.dist(n['a'], n['b'])
    D = 2.0 * n['r1']
    v = sub(centre, n['b'])
    offset = (dot(v, xb) / D, dot(v, yb) / D, dot(v, zb) / L)
    # угол сустава: поворот +Z блока от оси узла к переду узла (вокруг X). Unity: Euler(x) ведёт +Z к −Y
    # при x > 0, поэтому вперёд (+Y) — это отрицательный x
    s = unit(axis_dir)
    pitch = -math.degrees(math.atan2(dot(s, yb), dot(s, zb)))
    scale = (size[0] / D, size[1] / D, size[2] / L)
    r = lambda t: [round(x, 3) for x in t]
    return dict(node=n['name'], block=block, offset=r(offset), scale=r(scale), euler=[round(pitch, 1), 0.0, 0.0],
                metres=dict(centre=r(centre), size=r(size)))


def segment(n, start, end, width, depth, overlap_dir=None, overlap=0.0):
    """Брусок между двумя точками мира: начинается раньше `start` на `overlap` вдоль `overlap_dir` (шов перекрыт)."""
    if overlap_dir is not None:
        start = sub(start, mul(unit(overlap_dir), overlap))
    axis = sub(end, start)
    length = math.sqrt(dot(axis, axis))
    centre = add(start, mul(unit(axis), length * 0.5))
    return part(n, 'брусок', centre, (width, depth, length), axis)


def paw(n, fetlock, size, ahead):
    """Лапа подушкой на земле: низ в Y = 0, сгиб над путовым суставом, пальцы вперёд."""
    w, h, l = size
    centre = (fetlock[0], h * 0.5, fetlock[2] + ahead)
    return part(n, 'лапа', centre, (w, h, l), (0.0, 0.0, 1.0))


def fetlock_below(p, deg):
    """Путовый сустав под точкой p: наклон сегмента вперёд от отвеса на deg, высота над землёй постоянная."""
    fet_y = G.PAW_Y + 0.035
    return (p[0], fet_y, p[2] + (p[1] - fet_y) * math.tan(math.radians(deg)))


def front_leg():
    """ПЕРЕДНЯЯ: поле кончается на середине предплечья (`graph.FORE_END`). Ниже три блока на конце узла:
    низ предплечья до запястья, пясть 19.5° вперёд (снимок: запястье → лапа), лапа."""
    n = node_by_name('предплечье')
    d = sub(n['b'], n['a'])
    low = segment(n, n['b'], G.WRIST, 0.092, 0.098, d, 0.06)          # ширина предплечья на снимке 0.096 на Y 0.51
    fet = fetlock_below(G.WRIST, 19.5)
    pastern = segment(n, G.WRIST, fet, 0.068, 0.074, sub(fet, G.WRIST), 0.04)
    foot = paw(n, fet, (0.105, 0.070, 0.135), 0.030)
    return dict(slot='Руки', organ='Коготь', parts=[low, pastern, foot])


def hind_leg():
    """ЗАДНЯЯ: поле кончается над скакательным (`graph.HIND_END`). Ниже: низ голени до сустава, плюсна 12.9° вперёд
    (снимок: скакательный → лапа), лапа. Угол между двумя брусками и есть скакательный сустав — вместо шара поля,
    который на клетке торчал назад рваным шипом"""
    n = node_by_name('голень')
    d = sub(n['b'], n['a'])
    low = segment(n, n['b'], add(G.HOCK, mul(unit(d), 0.03)), 0.100, 0.105, d, 0.07)
    fet = fetlock_below(G.HOCK, 12.9)
    shank = segment(n, G.HOCK, fet, 0.070, 0.082, sub(fet, G.HOCK), 0.05)
    foot = paw(n, fet, (0.098, 0.065, 0.125), 0.028)
    return dict(slot='Ноги', organ='Волчьи ноги', parts=[low, shank, foot])


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--out', default=os.path.join(HERE, 'out', 'volk-organs-layout.json'))
    args = ap.parse_args()

    legs = [front_leg(), hind_leg()]
    os.makedirs(os.path.dirname(args.out), exist_ok=True)
    with open(args.out, 'w', encoding='utf-8') as f:
        json.dump(dict(organs=legs), f, ensure_ascii=False, indent=2)
    f3 = lambda v: ','.join('%g' % x for x in v)
    with open(os.path.splitext(args.out)[0] + '.txt', 'w', encoding='utf-8') as f:
        for lg in legs:
            for p in lg['parts']:
                f.write('leg|%s|%s|%s|%s|%s|%s\n' % (lg['organ'], p['node'], p['block'], f3(p['offset']), f3(p['scale']), f3(p['euler'])))

    for lg in legs:
        print('%s (%s):' % (lg['organ'], lg['slot']))
        for p in lg['parts']:
            print('  %-7s на «%s»: offset %-22s scale %-22s euler %s   (центр %s м, габарит %s м)' %
                  (p['block'], p['node'], p['offset'], p['scale'], p['euler'], p['metres']['centre'], p['metres']['size']))


if __name__ == '__main__':
    main()
