# -*- coding: utf-8 -*-
"""РАСКЛАДКА КУСКОВ ОРГАНОВ ЧЕЛОВЕКА — кисти и стопы ригблоками на концах узлов.

Контракт крепления — тот же, что у зверей (`../wolf/organs_layout.py`): кусок на конце узла, кадр «тело узла»
(+Z вдоль узла, +Y — перёд узла, +X — вбок), X/Y в диаметрах конца узла, Z в длинах узла. Отличие одно: узлы рук
человека отведены, поэтому кадр узла считается в 3D (`graph.node_axes`), а поворот куска — из его осей.

КИСТЬ — точка свапа и ближе всего к камере. Блок `кисть` (рукавица: запястье, широкая ладонь, пальцы подогнуты к ладони),
0.20 × 0.093 × 0.05 (ANSUR II, атлеты: длина 0.204, ширина 0.093). ЛАДОНЬ СМОТРИТ К БЕДРУ И НАЗАД НА 25°: так висит
расслабленная рука (Бронза A из Риаче), и анфас видна часть тыла кисти. В поставке 10 ладонь смотрела строго к бедру —
анфас кисть стояла ребром 0.048 и читалась лезвием.
СТОПА — блок `стопа` (длинная и плоская, подъём под лодыжкой, шире всего плюсна) 0.28 × 0.105 × 0.075; пятка на 5.5 см
позади центра голеностопа. `лапа` зверя анфас читалась копытцем.
ПРЕДПЛЕЧЬЕ И ГОЛЕНЬ — ПОЛЕМ (узлы графа до запястья и лодыжки): бруски ниже поля читались протезами.

Запуск:  python organs_layout.py [--out путь]
"""
import argparse
import json
import math
import os

import graph as G

HERE = os.path.dirname(os.path.abspath(__file__))


def euler_from_axes(x, y, z):
    return [round(e, 1) for e in G.euler_of(G.mat_cols(x, y, z))]


def node_by_name(name):
    return next(n for n in G.build_nodes() if n['name'] == name)


def part_frame(n):
    """Оси кадра куска в мире: z — вдоль узла (кость +Y), y — перёд узла (кость −Z), x — кость X."""
    bx, by, bz = G.node_axes(n['a'], n['b'])
    return bx, G.v_mul(bz, -1.0), by


def piece(n, block, centre, size, grow, normal):
    """Кусок: центр и габарит (ширина по x блока, толщина по y, длина по z) в метрах; `grow` — куда смотрит +Z блока,
    `normal` — примерно куда +Y блока. Всё в мире; переводится в доли и углы кадра узла."""
    xb, yb, zb = part_frame(n)
    L = G.v_len(G.v_sub(n['b'], n['a']))
    Dm = 2.0 * n['r1']
    v = G.v_sub(centre, n['b'])
    z = G.v_unit(grow)
    y = G.v_unit(G.v_sub(normal, G.v_mul(z, G.v_dot(normal, z))))
    x = G.v_cross(y, z)
    # оси блока в кадре куска
    to_part = lambda w: (G.v_dot(w, xb), G.v_dot(w, yb), G.v_dot(w, zb))
    r = lambda t: [round(c, 3) for c in t]
    return dict(node=n['name'], block=block,
                offset=r((G.v_dot(v, xb) / Dm, G.v_dot(v, yb) / Dm, G.v_dot(v, zb) / L)),
                scale=r((size[0] / Dm, size[1] / Dm, size[2] / L)),
                euler=euler_from_axes(to_part(x), to_part(y), to_part(z)),
                metres=dict(centre=r(centre), size=r(size)))


def arm():
    n = node_by_name('предплечье')
    d = G.v_unit(G.v_sub(G.WRIST, G.ELBOW))
    hand_len = 0.20
    start = G.v_sub(G.WRIST, G.v_mul(d, 0.03))                   # запястье кисти входит в конец поля предплечья
    centre = G.v_add(start, G.v_mul(d, hand_len / 2))
    back = math.radians(25)
    palm = (-math.cos(back), 0.0, -math.sin(back))                # к бедру (−X у правой руки) и назад
    hand = piece(n, 'кисть', centre, (0.093, 0.050, hand_len), d, palm)
    return dict(slot='Руки', organ='Кисть', parts=[hand])


def leg():
    n = node_by_name('голень')
    w, h, l = 0.105, 0.075, 0.280
    # НОСОК НАРУЖУ на 15°: у листа носки развёрнуты на 15–20° во всех ракурсах, параллельные блоки читались «по стойке смирно»
    # (рецензия 23.09). Пятка — на 6.5 см позади середины голеностопа вдоль оси стопы
    toe = math.radians(15.0)
    d = (math.sin(toe), 0.0, math.cos(toe))
    heel = (G.ANKLE[0] - d[0] * 0.065, 0.0, G.ANKLE[2] - d[2] * 0.065)
    centre = (heel[0] + d[0] * l / 2, h / 2, heel[2] + d[2] * l / 2)
    foot = piece(n, 'стопа', centre, (w, h, l), d, (0.0, 1.0, 0.0))
    return dict(slot='Ноги', organ='Ноги', parts=[foot])


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--out', default=os.path.join(HERE, 'out', 'chelovek-organs-layout-draft.json'))
    args = ap.parse_args()
    organs = [arm(), leg()]
    os.makedirs(os.path.dirname(args.out), exist_ok=True)
    with open(args.out, 'w', encoding='utf-8') as f:
        json.dump(dict(organs=organs), f, ensure_ascii=False, indent=2)
    for o in organs:
        print('%s (%s):' % (o['organ'], o['slot']))
        for p in o['parts']:
            print('  %-7s на «%s»: offset %-24s scale %-22s euler %-20s центр %s габарит %s' %
                  (p['block'], p['node'], p['offset'], p['scale'], p['euler'], p['metres']['centre'], p['metres']['size']))


if __name__ == '__main__':
    main()
