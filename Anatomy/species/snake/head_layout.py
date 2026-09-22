# -*- coding: utf-8 -*-
"""РАСКЛАДКА ГОЛОВЫ ЗМЕИ — калибр, рыло (Пасть), глаза, термоямки.

Кадр головы — узел `голова` графа: начало на затылке, ось к кончику рыла. Калибр: W × H прежние (0.24 × 0.163),
L — от затылка до кончика рыла (0.56). В `head` только `baseSize`: `sizeRel` у головы змеи нулевой (калибр читается
из `baseSize`, у цепного родителя долю не возьмёшь), и раскладка его не трогает.

Анфас `rattlesnake_head_front.jpg` (доли ширины затылка): у глаз 0.59, у рыла 0.55; глаза на верхних углах головы,
под нависающим щитком; термоямки — между ноздрёй и глазом, ниже линии глаза. Профиль `rattlesnake_head_lateral.jpg`:
глаз на 0.40 длины от кончика рыла, ямка — на 0.28 от кончика.

Запуск:  python head_layout.py [--out путь]
"""
import argparse
import json
import os

import graph as G

HERE = os.path.dirname(os.path.abspath(__file__))

W, H = G.HEAD_W, G.HEAD_H
L = round(G.HEAD_TIP_Z - G.HEAD_BACK_Z, 3)


def rel(abs_size, cal):
    return tuple(round(a / c, 4) for a, c in zip(abs_size, cal))


def place(name, attach, along, up, lateral, size, euler=(0.0, 0.0, 0.0), note='', parent='голова', cal=None):
    cw, ch, cl = cal or (W, H, L)
    return dict(name=name, parent=parent, attach=attach,
                attachOffset=[round(lateral / cw, 4), round(up / ch, 4), round(along / cl - attach, 4)],
                sizeRel=list(rel(size, (cw, ch, cl))), baseEuler=list(euler), note=note,
                metres=dict(along=round(along, 3), up=round(up, 3), lateral=round(lateral, 3), size=list(size)))


# ЗАМЕР ОБОЛОЧКИ (`ProbeM.SurfaceX`, окно ±4 см): поверхность узла `голова` вбок у глаза — 0.058 на клетке 0.084 (узел
# худел до пластины), 0.093 на 0.042. Глаз на верхнем углу головы выступает из-под щитка вбок: центр на 4 мм снаружи.
# Строка 0.084 — число, стоявшее в поставке 7 (0.090, глаз там выступал на 3 см: поле было тоньше расчёта)
GRAIN = 0.042          # клетка проекта (решение геймдизайнера 22.09)
SURFACE_AT_EYE = {0.084: 0.086, 0.042: 0.093}[GRAIN] + 0.004


def layout():
    # РЫЛО = ПАСТЬ: от глаз (заход в череп 5 см) до кончика; ширина у рыла 0.55 затылка, высота — 0.7 высоты головы
    # итерация 1: брусок 0.13 × 0.12 × 0.33 был крупнее черепа и висел отдельной коробкой ниже темени; итерация 2:
    # ровный брусок в профиль торчал трубой — клин короче, верх вровень с теменем, сходит к кончику
    m_a0, m_a1 = 0.30, L
    m_len = m_a1 - m_a0
    m_w, m_h = round(W * G.FRONT['у_рыла'], 3), round(H * 0.55, 3)
    m_a, m_b = (m_a0 + m_a1) / 2, round(H * 0.5 * 0.55 - m_h / 2 + 0.01, 3)
    # ГЛАЗ — НА ЧЕРЕПЕ, у его переднего края (где голова сужается к рылу): по снимку он на 0.40 длины от кончика, но
    # в нашем кадре там уже брусок рыла шириной 0.12, и глаз висел бы в 4 см от кожи. Ставим на конец узла черепа
    eye_a, eye_b = 0.24, 0.045
    pit_a, pit_b = L - 0.28 * L, -0.005
    places = [
        place('Пасть', 1.0, m_a, m_b, 0.0, (m_w, m_h, round(m_len, 3)), note='рыло: блок `клин`, короткое и плоское'),
        place('глаза', 0.5, eye_a, eye_b, SURFACE_AT_EYE, (0.05, 0.05, 0.05), note='на верхнем углу головы, под щитком'),
        # ТЕРМОЯМКА — на боку рыла: вбок = полуширина бруска на этой длине (сужение бруска к торцу 0.72)
        place('ямки', 0.5, pit_a, pit_b, round(m_w / 2 * (1 - 0.28 * (pit_a - m_a0) / m_len), 3), (0.035, 0.03, 0.03),
              note='ямка между ноздрёй и глазом, ниже линии глаза'),
    ]
    return places, (m_w, m_h, m_len)


def senses():
    return [
        dict(role='Eye', block='глаз', offset=[0, 0, 0], scale=[0.60, 0.80, 1.10], euler=[0, 0, 0]),
        dict(role='Pit', block='капля', offset=[0, 0, 0], scale=[0.50, 1.00, 1.00], euler=[0, 0, 0]),
    ]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--out', default=os.path.join(HERE, 'out', 'zmeya-head-layout-draft.json'))
    args = ap.parse_args()
    places, muzzle = layout()
    doc = dict(head=dict(baseSize=[W, H, L]), places=places,
               muzzle=dict(block='клин', offset=[0, 0, 0], scale=[1, 1, 1]), senses=senses())
    os.makedirs(os.path.dirname(args.out), exist_ok=True)
    with open(args.out, 'w', encoding='utf-8') as f:
        json.dump(doc, f, ensure_ascii=False, indent=2)
    print('калибр головы W×H×L = %.3f × %.3f × %.3f' % (W, H, L))
    for p in places:
        m = p['metres']
        print('  %-6s offset %-26s sizeRel %-26s (вдоль %.3f, к темени %.3f, вбок %.3f)' %
              (p['name'], p['attachOffset'], p['sizeRel'], m['along'], m['up'], m['lateral']))
    print('рыло %.3f × %.3f × %.3f' % muzzle)


if __name__ == '__main__':
    main()
