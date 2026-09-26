# -*- coding: utf-8 -*-
"""ПЕРЕВОД КУСКОВ АУГМЕНТОВ С УЗЛА ДОНОРА НА ГНЕЗДО (спека `2026-09-26-adresaciya-detaley-himery.md` П3, формат —
письмо механик `FEEDBACK-2026-09-26d-bilder-gnezd.md` §2).

Кусок на узле (`node`): кадр — конец узла, +Z вдоль узла, +Y перёд, X/Y в диаметрах конца узла, Z в длинах узла
(`MorphBuilder.NodeParts`). Этот адрес — словарь вида-донора: на чужом шасси узла нет или он другой, и кусок улетает.
Кусок в гнезде (`nest: true`): кадр — гнездо НОСИТЕЛЯ из `<вид>-places-layout.json`: начало `pos`, +Z вдоль `dir`, +Y —
мировой верх (у вертикального гнезда — перёд тела), offset и scale — в `unit` гнезда одним числом, euler — доворот.

Перевод без потерь на своём виде: кусок восстанавливается в метрах тела по графу поставки (та же кинематика, что
`SkeletonBuilder.Place`) и записывается в кадре своего гнезда. На своём шасси деталь встаёт туда же, где стояла; на
чужом — в гнездо носителя, в его калибре.

Запуск:  python nest_convert.py <вид-файл> <место> [<место> …]     например: python nest_convert.py volk Руки Ноги
         Пасть — по замеру стенда: nest_convert.past(вид, файл-замера) (см. ниже)
"""
import json
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '..', '..'))
H = os.path.join(ROOT, 'Docs', 'models', 'handoff')
sys.path.insert(0, os.path.join(ROOT, 'Tools', 'Blender', 'kritik'))
import faza  # noqa: E402


def mat(cols):
    """Матрица по столбцам-осям."""
    return [[cols[j][i] for j in range(3)] for i in range(3)]


def T(m):
    return [[m[j][i] for j in range(3)] for i in range(3)]


def mul(a, b):
    return [[sum(a[i][k] * b[k][j] for k in range(3)) for j in range(3)] for i in range(3)]


def app(m, v):
    return [sum(m[i][k] * v[k] for k in range(3)) for i in range(3)]


def unit(v):
    n = math.sqrt(sum(x * x for x in v))
    return [x / n for x in v]


def cross(a, b):
    return [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]]


def euler_to(e):
    return faza.euler_matrix(dict(x=e[0], y=e[1], z=e[2]))


def to_euler(r):
    """Матрица → углы Unity (R = Ry·Rx·Rz)."""
    x = math.degrees(math.asin(max(-1.0, min(1.0, -r[1][2]))))
    y = math.degrees(math.atan2(r[0][2], r[2][2]))
    z = math.degrees(math.atan2(r[1][0], r[1][1]))
    return [round(x, 2), round(y, 2), round(z, 2)]


def node_frame(graph, name):
    """Кадр куска на узле: начало — конец узла, F = поворот узла · Euler(−90, 0, 0), единица (D, D, L)."""
    by = {n['name']: n for n in graph['nodes']}
    pos, rot = faza.world(graph)[name]
    n = by[name]
    end = [p + d for p, d in zip(pos, app(rot, [0, n['length'], 0]))]
    F = mul(rot, euler_to([-90, 0, 0]))
    D = 2 * n['r1']
    return end, F, (D, D, n['length'])


def nest_frame(place):
    """Кадр гнезда: +Z вдоль dir, +Y — мировой верх, у вертикального гнезда — перёд тела; +X = Y × Z."""
    z = unit(place['dir'])
    hint = [0, 0, 1] if abs(z[1]) > 0.9 else [0, 1, 0]
    y = unit([h - z[i] * sum(hh * zz for hh, zz in zip(hint, z)) for i, h in enumerate(hint)])
    x = cross(y, z)
    return place['pos'], mat([x, y, z]), place['unit']


def convert(part, graph, place):
    end, F, (D, _, L) = node_frame(graph, part['node'])
    u3 = (D, D, L)
    centre = [e + d for e, d in zip(end, app(F, [part['offset'][i] * u3[i] for i in range(3)]))]
    size = [part['scale'][i] * u3[i] for i in range(3)]
    R = mul(F, euler_to(part.get('euler', [0, 0, 0])))
    pos, N, u = nest_frame(place)
    local = app(T(N), [c - p for c, p in zip(centre, pos)])
    out = {k: v for k, v in part.items() if k not in ('node', 'offset', 'scale', 'euler', 'metres')}
    out.update(nest=True, offset=[round(v / u, 4) for v in local], scale=[round(v / u, 4) for v in size],
               euler=to_euler(mul(T(N), R)),
               metres=dict(centre=[round(c, 3) for c in centre], size=[round(s, 3) for s in size]))
    return out


def main(file, slots):
    graph = json.load(open(os.path.join(H, file + '-graph.json'), encoding='utf-8'))
    places = {p['name']: p for p in json.load(open(os.path.join(H, file + '-places-layout.json'), encoding='utf-8'))['places']}
    path = os.path.join(H, file + '-organs-layout.json')
    doc = json.load(open(path, encoding='utf-8'))
    for o in doc['organs']:
        if o['slot'] not in slots:
            continue
        if any(p.get('nest') for p in o['parts']):
            print('%s: уже в гнезде' % o['organ'])
            continue
        o['parts'] = [convert(p, graph, places[o['slot']]) for p in o['parts']]
        print('%-14s → гнездо %-5s (%s), кусков %d' % (o['organ'], o['slot'], places[o['slot']]['host'], len(o['parts'])))
    with open(path, 'w', encoding='utf-8') as f:
        json.dump(doc, f, ensure_ascii=False, indent=2)


if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2:])


# ── ПАСТЬ ИЗ ЗАМЕРА СТЕНДА ──────────────────────────────────────────────────────────────────────────────
# Морда и зубы лежат в `<вид>-head-layout.json` в долях места Пасти (кадр места — от раскладки головы и калибра
# шасси), пересчитывать эту цепочку в Python — повторять билдер. Поэтому Пасть переводится ПО ЗАМЕРУ: стенд
# `Kadr.Shot` собирает вид настоящим билдером, eval выписывает каждый кусок места `Пасть` (блок, позиция, поворот,
# размер) в файл «блок;x;y;z;qx;qy;qz;qw;sx;sy;sz», по строке на кусок в порядке морда → зубы.
def quat_mat(x, y, z, w):
    return [[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
            [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
            [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]]


def past(file, measured):
    places = {p['name']: p for p in json.load(open(os.path.join(H, file + '-places-layout.json'), encoding='utf-8'))['places']}
    pos, N, u = nest_frame(places['Пасть'])
    rows = [l.split(';') for l in open(measured, encoding='utf-8').read().strip().splitlines()]
    path = os.path.join(H, file + '-head-layout.json')
    doc = json.load(open(path, encoding='utf-8'))
    old = [doc['muzzle']] + list(doc.get('teeth', []))
    if len(rows) != len(old):
        raise SystemExit('%s: кусков по замеру %d, в раскладке %d' % (file, len(rows), len(old)))
    new = []
    for r, o in zip(rows, old):
        c = [float(v) for v in r[1:4]]
        R = quat_mat(*[float(v) for v in r[4:8]])
        size = [float(v) for v in r[8:11]]
        if r[0] != o['block']:
            raise SystemExit('%s: блок по замеру %s, в раскладке %s' % (file, r[0], o['block']))
        local = app(T(N), [a - b for a, b in zip(c, pos)])
        p = {k: v for k, v in o.items() if k not in ('offset', 'scale', 'euler')}
        p.update(nest=True, offset=[round(v / u, 4) for v in local], scale=[round(v / u, 4) for v in size],
                 euler=to_euler(mul(T(N), R)))
        new.append(p)
    doc['muzzle'], doc['teeth'] = new[0], new[1:]
    with open(path, 'w', encoding='utf-8') as f:
        json.dump(doc, f, ensure_ascii=False, indent=2)
    print('%-9s Пасть → гнездо (%s): кусков %d' % (file, places['Пасть']['host'], len(new)))
