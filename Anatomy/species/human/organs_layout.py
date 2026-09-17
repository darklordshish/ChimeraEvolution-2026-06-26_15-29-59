# -*- coding: utf-8 -*-
"""РАСКЛАДКА КУСКОВ ОРГАНОВ ЧЕЛОВЕКА — предплечья, кисти, голени и стопы ригблоками на концах узлов.

Контракт крепления — тот же, что у зверей (`../wolf/organs_layout.py`): кусок на конце узла, кадр «тело узла»
(+Z вдоль узла, +Y — перёд узла, +X — вбок), X/Y в диаметрах конца узла, Z в длинах узла. Отличие одно: узлы рук
человека отведены, поэтому кадр узла считается в 3D (`graph.node_axes`), а поворот куска — из его осей.

КИСТЬ — точка свапа и ближе всего к камере. Блок `лопата`: узкий толстый черешок у запястья, к пальцам шире и тоньше,
край чуть подогнут — ладонь лодочкой. Ладонь смотрит к бедру (−X у правой руки), поэтому подогнутый край (+Y блока)
развёрнут внутрь. Длина кисти 0.19 (канон: 0.8 лица), ширина по пястью 0.10, толщина 0.05.
СТОПА — `лапа`: подошва на земле, пятка узкая, пальцы широкие, высокий подъём у лодыжки. 0.26 × 0.10 × 0.08.

Запуск:  python organs_layout.py [--out путь]
"""
import argparse
import json
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


def bar(n, start, end, width, depth, overlap):
    d = G.v_unit(G.v_sub(end, start))
    start = G.v_sub(start, G.v_mul(d, overlap))
    length = G.v_len(G.v_sub(end, start))
    centre = G.v_add(start, G.v_mul(d, length / 2))
    return piece(n, 'брусок', centre, (width, depth, length), d, (0.0, 0.0, 1.0))


def arm():
    n = node_by_name('предплечье')
    # ПРЕДПЛЕЧЬЕ БРУСКОМ ОТ САМОГО ЛОКТЯ, а не от конца поля: на клетке 0.084 поле предплечья (Ø 0.10) почти съедается,
    # и брусок, начатый от его конца, висел отдельно от плеча со щелью у локтя (итерация 1). На 0.042 брусок тонет в поле
    forearm = bar(n, G.v_add(G.ELBOW, G.v_mul(G.v_unit(G.v_sub(G.WRIST, G.ELBOW)), 0.03)), G.WRIST, 0.085, 0.070, 0.0)
    d = G.v_unit(G.v_sub(G.WRIST, G.ELBOW))
    hand_len = 0.19
    start = G.v_sub(G.WRIST, G.v_mul(d, 0.025))                  # черешок входит в запястье
    centre = G.v_add(start, G.v_mul(d, hand_len / 2))
    # толщина 0.05: у `лопата` край тоньше черешка в 4.5 раза, и при 0.04 кисть анфас (ребром) читалась иглой
    hand = piece(n, 'лопата', centre, (0.100, 0.050, hand_len), d, (-1.0, 0.0, 0.0))
    return dict(slot='Руки', organ='Кисть', parts=[forearm, hand])


def leg():
    n = node_by_name('голень')
    shin = bar(n, G.SHIN_END, G.v_add(G.ANKLE, (0, -0.01, 0)), 0.075, 0.085, 0.06)
    w, h, l = 0.100, 0.080, 0.260
    heel_z = G.ANKLE[2] - 0.08
    centre = (G.ANKLE[0], h / 2, heel_z + l / 2)
    foot = piece(n, 'лапа', centre, (w, h, l), (0.0, 0.0, 1.0), (0.0, 1.0, 0.0))
    return dict(slot='Ноги', organ='Ноги', parts=[shin, foot])


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
