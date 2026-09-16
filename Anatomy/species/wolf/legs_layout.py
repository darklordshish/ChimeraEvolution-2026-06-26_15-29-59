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
лапа (240, 1455) — 12.9°. Лапа стоит подушкой на земле: низ капли в Y = 0.

Запуск:  python legs_layout.py [--out путь]
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


def leg(node_name, slot, organ, pastern_deg, bar_base, bar_len_extra, paw_size, paw_ahead):
    n = node_by_name(node_name)
    wrist = n['b']
    D = 2.0 * n['r1']
    d_node = unit(sub(n['b'], n['a']))

    # брусок: начало внутри узла на свою толщину, конец — путовый сустав над лапой, под снятым со снимка углом
    thick = bar_base[0]
    start = sub(wrist, mul(d_node, thick))
    fet_y = G.PAW_Y + 0.035
    drop = wrist[1] - fet_y
    fetlock = (wrist[0], fet_y, wrist[2] + drop * math.tan(math.radians(pastern_deg)))
    bar_axis = sub(fetlock, start)
    bar_len = math.sqrt(dot(bar_axis, bar_axis)) + bar_len_extra
    bar_centre = add(start, mul(unit(bar_axis), bar_len * 0.5))
    bar = part(n, 'брусок', bar_centre, (bar_base[0], bar_base[1], bar_len), bar_axis)

    # лапа: подушкой на земле, пальцами вперёд, центр чуть впереди путового сустава
    w, h, l = paw_size
    paw_centre = (wrist[0], h * 0.5, fetlock[2] + paw_ahead)
    paw = part(n, 'капля', paw_centre, (w, h, l), (0.0, 0.0, 1.0))
    return dict(slot=slot, organ=organ, parts=[bar, paw])


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--out', default=os.path.join(HERE, 'out', 'volk-legs-layout.json'))
    args = ap.parse_args()

    legs = [
        # передняя: пясть 0.075 × 0.075 у запястья (диаметр конца предплечья 0.076), лапа 0.085 × 0.06 × 0.13
        leg('предплечье', 'Руки', 'Коготь', 19.5, (0.072, 0.076), 0.015, (0.085, 0.060, 0.130), 0.035),
        # задняя: плюсна уже конца голени (0.09) — у псовых плюсна сухая, лапа чуть меньше передней
        leg('голень', 'Ноги', 'Волчьи ноги', 12.9, (0.066, 0.074), 0.015, (0.080, 0.055, 0.120), 0.030),
    ]
    os.makedirs(os.path.dirname(args.out), exist_ok=True)
    with open(args.out, 'w', encoding='utf-8') as f:
        json.dump(dict(legs=legs), f, ensure_ascii=False, indent=2)
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
