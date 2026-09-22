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

ЛИЦО = ПАСТЬ. Нижняя часть лица — челюсть и рот, блок `челюсть` Пасти: привили волчью Пасть — у человека морда оборотня.

Запуск:  python head_layout.py [--cell 0.042] [--out путь]
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
LIPS = {0.084: 0.050, 0.042: 0.015}
SURFACE = {
    0.084: dict(eye_front=0.050, face_front=0.048, ear_side=0.056),
    0.042: dict(eye_front=0.074, face_front=0.083, ear_side=0.080),   # перемерено 22.09 под череп атлета по данным (сечение 0.98)
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
    # МЕСТО ПАСТИ — нижняя часть лица: от углов челюсти (Z −0.045) до подбородка, от низа подбородка (1.582) до основания
    # носа (1.66), по углам челюсти 0.125. Сама челюсть — поле (узлы `челюсть` графа), а на месте рисуются только ГУБЫ
    # (`MUZZLE` ниже). Калибр места остаётся калибром лица: по нему растёт привитая волчья Пасть — морда оборотня
    lips = LIPS[cell]
    front = s['face_front'] + lips
    m_size = (0.125, 0.078, round(front + 0.045, 3))
    m_world = (0.0, 1.621, round((front - 0.045) / 2, 3))
    places = [
        place('Пасть', 0.0, m_world, m_size, note='нижняя часть лица; рисуются губы (`MUZZLE`), челюсть — поле'),
        place('глаза', 0.0, (0.032, 1.705, s['eye_front'] - 0.004), (0.020, 0.016, 0.030),
              note='глаз поперёк головы: блок поворачивается на 90° в `senses`'),
        place('уши', 0.0, (s['ear_side'] + 0.006, 1.695, -0.010), (0.020, 0.060, 0.035), note='ухо: капля тупым концом вверх'),
    ]
    # НОС — греческий: прямой, продолжает линию лба, от переносицы (1.715, вровень с лбом) к ноздрям (1.658, на 2.6 см
    # впереди лица). Блок `клин` широким торцом вниз (ноздри), узким вверх (переносица)
    lat, up, fwd = local(m_world)
    # ...утоплен: при толщине 2.6 см и центре на 1 см впереди лица нос торчал клювом на 4 см (кадр 22.09). Теперь кончик
    # выходит на ~1.5 см, переносица вровень с лбом
    # ...и ПОЧТИ ОТВЕСЕН (8°), толще (2.6 см у ноздрей): при наклоне 25° клин торчал из лица колышком — толщина симметрична
    # вокруг оси, и наклон утапливал переносицу, а низ выносил отдельно (итерация w8). Теперь в профиль трапеция: переносица
    # выходит на ~0.8 см, ноздри — на ~2.3 см
    nose_world = (0.0, 1.684, s['face_front'] + 0.006)
    nl, nu, nf = local(nose_world)
    nose_size = (0.032, 0.026, 0.052)
    places.append(dict(name='нос', parent='Пасть', attach=0.5,
                       attachOffset=[0.0, round((nu - up) / m_size[1], 4), round((nf - fwd) / m_size[2], 4)],
                       sizeRel=rel(nose_size, m_size), baseEuler=[0.0, 0.0, 0.0],
                       note='нос: доли — от калибра Пасти (нос едет с мордой)',
                       metres=dict(world=[round(c, 3) for c in nose_world], size=list(nose_size))))
    return places


# ГУБЫ — часть органа «Рот» на месте Пасти, в долях места: у переднего края коробки, чуть выше её середины, почти целиком
# в поле лица — наружу ~5 мм. Блок `капля` тупым концом вперёд, широкая и плоская
MUZZLE = dict(block='капля', offset=[0.0, 0.10, 0.36], scale=[0.42, 0.22, 0.20])


def senses():
    return [
        # ГЛАЗ ПОПЕРЁК: миндаль блока вытянут по Z, у человека глаз шире, чем глубже — поворот на 90° вокруг Y
        dict(role='Eye', block='глаз', offset=[0, 0, 0], scale=[1, 1, 1], euler=[0, 90, 0]),
        # УХО: капля тупым концом вверх (завиток широкий, мочка узкая); поворот −90° по X кладёт длину капли в высоту,
        # поэтому доли размера переставлены: высота 0.06 = глубина места × 1.714, глубина 0.035 = высота места × 0.583
        dict(role='Ear', block='капля', offset=[0, 0, 0], scale=[1, 0.583, 1.714], euler=[-90, 0, 0]),
        # НОС: клин широким торцом (ноздри) вниз, узким (переносица) вверх-назад по линии лба; толщина — наружу из лица.
        # Поворот (−98, 0, 180): +Z блока — вверх и назад на 8° от отвеса, +Y — вперёд из лица
        dict(role='Nose', block='клин', offset=[0, 0, 0], scale=[1, 1, 1], euler=[-98, 0, 180]),
    ]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--cell', type=float, default=0.042)   # клетка проекта (решение геймдизайнера 22.09)
    ap.add_argument('--out', default=os.path.join(HERE, 'out', 'chelovek-head-layout-draft.json'))
    args = ap.parse_args()
    places = layout(args.cell)
    doc = dict(head=dict(baseSize=[W, H, D], sizeRel=rel((W, H, D), NECK_CALIBRE)), places=places,
               muzzle=MUZZLE, senses=senses())
    os.makedirs(os.path.dirname(args.out), exist_ok=True)
    with open(args.out, 'w', encoding='utf-8') as f:
        json.dump(doc, f, ensure_ascii=False, indent=2)
    print('клетка %.3f; калибр головы %.3f × %.3f × %.3f, sizeRel от шеи %s' % ((args.cell, W, H, D) + (doc['head']['sizeRel'],)))
    for p in places:
        print('  %-6s attach %.1f offset %-28s sizeRel %-26s мир %s' % (p['name'], p['attach'], p['attachOffset'], p['sizeRel'], p['metres']['world']))


if __name__ == '__main__':
    main()
