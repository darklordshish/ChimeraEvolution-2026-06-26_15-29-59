# -*- coding: utf-8 -*-
"""ЯКОРЬ ФАЗЫ СЕТКИ — копия графа поставки с крошечным узлом за кадром. Только для замеров, в поставку не идёт.

Сетка оболочки (`BoneMesher.Polygonize`) по X симметрична оси тела, а по Y и Z начинается от ГАБАРИТА узлов (низ — стопы,
зад — самый задний узел с его радиусом и смешением). Правка крайнего узла на 1 см сдвигает фазу сетки для всего тела, и на
клетке 0.042 любой замер прыгает на 0.5–2.5 см без связи с правкой (23.09, человек: «сужение шеи» от сдвига лопатки).
Якорь ниже и позади тела задаёт габарит сам и сдвигает фазу на долю клетки k/n. Сверку правки ведут по n фазам: форма,
которая держится только на удачной фазе, у химеры не удержится — там габарит меняется при каждом графте.

Якорь стоит в ПОСТОЯННОЙ точке (по умолчанию y −1.0, z −3.0 плюс доля клетки): сравнивать варианты графа можно только при
одной и той же фазе, а якорь «от габарита» уезжал бы вместе с правкой. Скрипт считает габарит графа той же кинематикой,
что `SkeletonBuilder.Place`, и отказывается, если якорь его не перекрывает (у волка сетка уходит назад до −0.88 — прежний
якорь на −0.80 её не задавал, поймано проверкой скилла критика).

Запуск:  python faza.py граф.json k n выход.json [клетка] [--y Y --z Z]
"""
import argparse
import json
import math


def euler_matrix(e):
    """Углы Эйлера Unity (градусы) → матрица поворота R = Ry·Rx·Rz (как `Quaternion.Euler`), столбцы — оси кости."""
    x, y, z = (math.radians(e[k]) for k in ('x', 'y', 'z'))
    cx, sx, cy, sy, cz, sz = math.cos(x), math.sin(x), math.cos(y), math.sin(y), math.cos(z), math.sin(z)
    ry = [[cy, 0, sy], [0, 1, 0], [-sy, 0, cy]]
    rx = [[1, 0, 0], [0, cx, -sx], [0, sx, cx]]
    rz = [[cz, -sz, 0], [sz, cz, 0], [0, 0, 1]]
    return mul(mul(ry, rx), rz)


def mul(a, b):
    return [[sum(a[i][k] * b[k][j] for k in range(3)) for j in range(3)] for i in range(3)]


def apply(m, v):
    return [sum(m[i][k] * v[k] for k in range(3)) for i in range(3)]


def world(doc):
    """Начало и поворот каждой кости в мире — `SkeletonBuilder.Place`: сустав на доле `attach` оси родителя или
    смещение `origin` в кадре родителя (`freeOrigin`), поворот наследуется."""
    by = {b['name']: b for b in doc['nodes']}
    done = {}

    def place(b, depth=0):
        if b['name'] in done:
            return done[b['name']]
        rot, pos = euler_matrix(b['dir']), [b['origin'][k] for k in 'xyz']
        par = by.get(b['parent']) if b['parent'] else None
        if par is not None and depth < 16:
            ppos, prot = place(par, depth + 1)
            off = [b['origin'][k] for k in 'xyz'] if b.get('freeOrigin') else [0.0, par['length'] * b.get('attach', 1.0), 0.0]
            pos = [p + d for p, d in zip(ppos, apply(prot, off))]
            rot = mul(prot, rot)
        done[b['name']] = (pos, rot)
        return done[b['name']]

    return {b['name']: place(b) for b in doc['nodes']}


def extent(doc):
    """Низ и зад габарита сетки (без запаса в клетку) — как в `BoneMesher.Polygonize`."""
    w = world(doc)
    lo_y = lo_z = 9.0
    for b in doc['nodes']:
        pos, rot = w[b['name']]
        tip = [p + d for p, d in zip(pos, apply(rot, [0.0, b['length'], 0.0]))]
        r = max(b['r0'], b['r1']) * max(1.0, b['section'], b['depth']) + b['blend']
        lo_y = min(lo_y, pos[1] - r, tip[1] - r)
        lo_z = min(lo_z, pos[2] - r, tip[2] - r)
    return lo_y, lo_z


def anchored(doc, k, n, cell=0.042, y=-1.0, z=-3.0):
    lo_y, lo_z = extent(doc)
    if y > lo_y - 0.05 or z > lo_z - 0.05:
        raise SystemExit('якорь (y %.2f, z %.2f) не перекрывает габарит графа (низ %.2f, зад %.2f) — вынеси дальше --y/--z'
                         % (y, z, lo_y, lo_z))
    root = next(b for b in doc['nodes'] if not b['parent'])
    o = [root['origin'][c] for c in 'xyz']
    R = euler_matrix(root['dir'])
    d = cell * k / n
    p = (0.0, y + d, z + d)
    loc = [sum(R[i][j] * (p[i] - o[i]) for i in range(3)) for j in range(3)]      # Rᵀ · (p − начало корня)
    anchor = dict(root)
    anchor.update(name='~якорь', parent=root['name'], layer=0, origin=dict(x=loc[0], y=loc[1], z=loc[2]),
                  attach=1.0, freeOrigin=True, length=0.001, endBone='', endAttach=1.0, dir=dict(x=0.0, y=0.0, z=0.0),
                  r0=0.002, r1=0.002, section=1.0, depth=1.0, blend=0.0, chain=0, mirrorX=False)
    out = dict(doc)
    out['nodes'] = doc['nodes'] + [anchor]
    return out


if __name__ == '__main__':
    ap = argparse.ArgumentParser()
    ap.add_argument('src'); ap.add_argument('k', type=int); ap.add_argument('n', type=int); ap.add_argument('dst')
    ap.add_argument('cell', type=float, nargs='?', default=0.042)
    ap.add_argument('--y', type=float, default=-1.0); ap.add_argument('--z', type=float, default=-3.0)
    a = ap.parse_args()
    doc = json.load(open(a.src, encoding='utf-8'))
    json.dump(anchored(doc, a.k, a.n, a.cell, a.y, a.z), open(a.dst, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    print(a.dst)
