# -*- coding: utf-8 -*-
"""РАСКЛАДКА ГОЛОВЫ ЧЕЛОВЕКА — калибр места `голова`, лицо (Пасть), мочка носа, глаза, уши.

У ЧЕЛОВЕКА ГОЛОВА ВЫТЯНУТА ВВЕРХ. Место `голова` длиннее всего по Y, и `MorphBuilder.Place` кладёт эту ось вдоль узла:
узел идёт от уровня подбородка к своду, начало места — начало узла. В долях места:
    вверх от начала узла  = (attach + attachOffset.y) · H
    вперёд                = attachOffset.z · D
    вбок                  = attachOffset.x · W

ОТКУДА ЧИСЛА. Ортографии головы человека в паспорте нет (`REFS.md`: профиль черепа — обломок свода). Пропорции —
канон головы (Лумис) в игровом калибре: подбородок 1.595, глаза 1.705, мочка носа 1.665, уши на линии глаз, голова
0.16 × 0.245 × 0.20. Глубины, куда садятся глаза, мочка и лицо, — ЗАМЕР поверхности оболочки на клетке (`SURFACE`):
на 0.084 лоб у глаз кончается на Z 0.050, на 0.042 — на 0.070, и деталь, посаженная «по канону», утонула бы или
повисла. Клетка выбирается ключом `--cell`.

ЛИЦО = ПАСТЬ. Нижняя часть лица (челюсть и рот) — деталь Пасти: привили волчью Пасть — у человека морда оборотня.

Запуск:  python head_layout.py [--cell 0.084] [--out путь]
"""
import argparse
import json
import math
import os

import graph as G

HERE = os.path.dirname(os.path.abspath(__file__))

W, H, D = 0.160, 0.245, 0.200
NECK_CALIBRE = (0.470 * 0.268, 0.600 * 0.223, 0.240 * 0.525)   # хребет × sizeRel шеи (бутстрап)
HEAD_NODE = next(n for n in G.build_nodes() if n['name'] == 'голова')

# ЗАМЕР поверхности (`ProbeM.SurfaceZ`/`SurfaceX`, окно ±4 см): лоб у глаза, лицо по оси, бок головы у уха
SURFACE = {
    0.084: dict(eye_front=0.050, face_front=0.048, ear_side=0.056),
    0.042: dict(eye_front=0.070, face_front=0.078, ear_side=0.071),
}


def frame():
    x, y, z = G.node_axes(HEAD_NODE['a'], HEAD_NODE['b'])
    return HEAD_NODE['a'], x, y, z


def local(p):
    o, x, y, z = frame()
    d = G.v_sub(p, o)
    return G.v_dot(d, x), G.v_dot(d, y), G.v_dot(d, z)      # (вбок, вверх вдоль узла, вперёд)


def rel(abs_size, cal):
    return [round(a / c, 4) for a, c in zip(abs_size, cal)]


def place(name, attach, world, size, euler=(0.0, 0.0, 0.0), note='', parent='голова', cal=(W, H, D)):
    lat, up, fwd = local(world)
    cw, ch, cd = cal
    return dict(name=name, parent=parent, attach=attach,
                attachOffset=[round(lat / cw, 4), round(up / ch - attach, 4), round(fwd / cd, 4)],
                sizeRel=rel(size, cal), baseEuler=list(euler), note=note,
                metres=dict(world=[round(c, 3) for c in world], size=list(size)))


def layout(cell):
    s = SURFACE[cell]
    # ЛИЦО: коробка от челюстного угла (Z −0.03) до губ (поверхность лица + 5 см), от подбородка до основания носа
    m_size = (0.110, 0.075, round(s['face_front'] + 0.05 + 0.03, 3))
    m_world = (0.0, 1.635, round((s['face_front'] + 0.05 - 0.03) / 2, 3))
    places = [
        place('Пасть', 0.0, m_world, m_size, note='нижняя часть лица: блок `брусок` челюстью назад'),
        place('глаза', 0.0, (0.032, 1.705, s['eye_front'] - 0.004), (0.020, 0.016, 0.030),
              note='глаз поперёк головы: блок поворачивается на 90° в `senses`'),
        place('уши', 0.0, (s['ear_side'] + 0.006, 1.695, -0.010), (0.020, 0.060, 0.035), note='ухо: капля тупым концом вверх'),
    ]
    # МОЧКА НОСА на Пасти, доли — от калибра Пасти; нос выходит из лица над губами
    lat, up, fwd = local(m_world)
    nose_world = (0.0, 1.672, s['face_front'] + 0.035)
    nl, nu, nf = local(nose_world)
    nose_size = (0.035, 0.030, 0.050)
    places.append(dict(name='нос', parent='Пасть', attach=0.5,
                       attachOffset=[0.0, round((nu - up) / m_size[1], 4), round((nf - fwd) / m_size[2] - 0.0, 4)],
                       sizeRel=rel(nose_size, m_size), baseEuler=[0.0, 0.0, 0.0],
                       note='мочка на лице над губами; доли — от калибра Пасти',
                       metres=dict(world=[round(c, 3) for c in nose_world], size=list(nose_size))))
    return places


def senses():
    return [
        # ГЛАЗ ПОПЕРЁК: миндаль блока вытянут по Z, у человека глаз шире, чем глубже — поворот на 90° вокруг Y
        dict(role='Eye', block='глаз', offset=[0, 0, 0], scale=[1, 1, 1], euler=[0, 90, 0]),
        # УХО: капля тупым концом вверх (завиток широкий, мочка узкая); поворот −90° по X кладёт длину капли в высоту,
        # поэтому доли размера переставлены: высота 0.06 = глубина места × 1.714, глубина 0.035 = высота места × 0.583
        dict(role='Ear', block='капля', offset=[0, 0, 0], scale=[1, 0.583, 1.714], euler=[-90, 0, 0]),
        # НОС: капля от переносицы к кончику, наклон вниз на 48° — тупой конец внизу, у ноздрей
        dict(role='Nose', block='капля', offset=[0, 0, 0], scale=[1, 1, 1], euler=[48, 0, 0]),
    ]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--cell', type=float, default=0.084)
    ap.add_argument('--out', default=os.path.join(HERE, 'out', 'chelovek-head-layout-draft.json'))
    args = ap.parse_args()
    places = layout(args.cell)
    doc = dict(head=dict(baseSize=[W, H, D], sizeRel=rel((W, H, D), NECK_CALIBRE)), places=places,
               muzzle=dict(block='брусок', offset=[0, 0, 0], scale=[1, 1, 1]), senses=senses())
    os.makedirs(os.path.dirname(args.out), exist_ok=True)
    with open(args.out, 'w', encoding='utf-8') as f:
        json.dump(doc, f, ensure_ascii=False, indent=2)
    print('клетка %.3f; калибр головы %.3f × %.3f × %.3f, sizeRel от шеи %s' % ((args.cell, W, H, D) + (doc['head']['sizeRel'],)))
    for p in places:
        print('  %-6s attach %.1f offset %-28s sizeRel %-26s мир %s' % (p['name'], p['attach'], p['attachOffset'], p['sizeRel'], p['metres']['world']))


if __name__ == '__main__':
    main()
