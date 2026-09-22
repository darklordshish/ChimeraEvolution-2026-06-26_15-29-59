# -*- coding: utf-8 -*-
"""РАСКЛАДКА ГОЛОВЫ ЕЖА — калибр места `голова` и доли деталей (устройство — как у волка и лося).

Кадр головы снимка: начало — основание черепа, ось — к кончику носа (`hedgehog_side_REFERENCE.jpg`, опоры `graph.py`).
Ширины — `hedgehog_front_REFERENCE.jpg`: голова по щекам 0.42 ширины юбки игл, мочка 0.07, рыльце у основания ~0.45
ширины головы. Рыльце ежа — клин (сходит к мочке в острие), морда волка тем же блоком и сделана.

Запуск:  python head_layout.py [--out путь]
"""
import argparse
import json
import math
import os

import graph as G

HERE = os.path.dirname(os.path.abspath(__file__))

HEAD_PX = {
    'основание_черепа': G.PHOTO['основание_черепа'],
    'кончик_носа':      G.PHOTO['кончик_носа'],
    'темя':             (1220, 660),   # верх головы под козырьком игл
    'челюсть_низ':      (1260, 880),   # низ челюсти под глазом
    'глаз':             G.PHOTO['глаз'],
    'ухо':              G.PHOTO['ухо'],
    'лоб_у_глаза':      (1283, 700),   # верх рыльца над глазом
    'подбородок_у_глаза': (1283, 880),
}
OUTLINE_W = 0.95 * G.TOP_Y            # ширина юбки игл по контуру (анфас)
W = round(0.42 * OUTLINE_W, 3)        # голова по щекам
NOSE_W = round(0.085 * OUTLINE_W, 3)  # мочка: на анфасе 0.07 юбки, но на острие клина такой ширины она сливалась с торцом — чуть крупнее
# калибр места `шея` = хребет (0.76, 0.60, 1.05) × sizeRel шеи (0.224, 0.300, 0.190)
NECK_CALIBRE = (0.76 * 0.224, 0.60 * 0.300, 1.05 * 0.190)


def world(p):
    z, y = G.px(*p)
    return (y, z)


def photo_frame():
    oy, oz = world(HEAD_PX['основание_черепа'])
    ny, nz = world(HEAD_PX['кончик_носа'])
    dy, dz = ny - oy, nz - oz
    L = math.hypot(dy, dz)
    u = (dy / L, dz / L)
    v = (u[1], -u[0])
    if v[0] < 0:
        v = (-v[0], -v[1])
    return (oy, oz), u, v, L


def local(name):
    (oy, oz), u, v, _ = photo_frame()
    y, z = world(HEAD_PX[name])
    d = (y - oy, z - oz)
    return (d[0] * u[0] + d[1] * u[1], d[0] * v[0] + d[1] * v[1])


_, _, _, L_PHOTO = photo_frame()
L = round(L_PHOTO + 0.005, 3)
H = round(local('темя')[1] - local('челюсть_низ')[1], 3)
# ДЛИННАЯ ОСЬ КАЛИБРА ОБЯЗАНА ОСТАТЬСЯ Z: по щекам голова ежа (0.36) шире своей длины (0.35), и ось места молча ушла бы
# в X — все доли головы поехали бы вбок (гоча «длинная ось выбирается автоматически»). Калибр по черепу, запас 8 %
W = round(min(W, L / 1.08), 3)


def rel(abs_size, cal):
    return tuple(round(a / c, 4) for a, c in zip(abs_size, cal))


def place(name, attach, along, up, lateral, size, euler=(0.0, 0.0, 0.0), note='', parent='голова'):
    return dict(name=name, parent=parent, attach=attach,
                attachOffset=[round(lateral / W, 4), round(up / H, 4), round(along / L - attach, 4)],
                sizeRel=list(rel(size, (W, H, L))), baseEuler=list(euler), note=note,
                metres=dict(along=round(along, 3), up=round(up, 3), lateral=round(lateral, 3), size=list(size)))


# ЗАМЕРЫ ОБОЛОЧКИ на клетке 0.084 (`ProbeM.SurfaceX`, наибольший x вершин в окне ±5 см): поверхность вбок в точке глаза
# (Y 0.357, Z 0.582) — 0.12…0.13, в точке уха (Y 0.405, Z 0.519) — 0.156. Итерация 3 ставила ухо по догадке 0.11 —
# оно целиком тонуло в голове
# на клетке 0.042 — 0.120 у глаза и 0.158 у уха: крупная голова ежа клетки почти не замечает
GRAIN = 0.042          # клетка проекта (решение геймдизайнера 22.09)
SURFACE_AT_EYE = {0.084: 0.118, 0.042: 0.120}[GRAIN]
SURFACE_AT_EAR = {0.084: 0.156, 0.042: 0.158}[GRAIN]


def layout():
    eye_a, eye_b = local('глаз')
    ear_a, ear_b = local('ухо')
    top_a, top_b = local('лоб_у_глаза')
    jaw_a, jaw_b = local('подбородок_у_глаза')

    # ПАСТЬ = РЫЛЬЦЕ: коробка от глаза (заход в череп на 4 см) до кончика носа, по высоте — от подбородка до лба у глаза
    m_a0, m_a1 = round(top_a - 0.04, 3), L
    m_b0, m_b1 = round(jaw_b, 3), round(top_b, 3)
    m_len, m_h, m_w = m_a1 - m_a0, m_b1 - m_b0, round(0.45 * W, 3)
    m_a, m_b = (m_a0 + m_a1) / 2, (m_b0 + m_b1) / 2

    places = [
        place('глаза', 0.5, eye_a, eye_b, SURFACE_AT_EYE + 0.004, (0.045, 0.045, 0.045),
              note='бусина на замеренной поверхности головы'),
        # УХО — маленькое, круглое, на две трети утоплено в иглы
        place('уши', 0.5, ear_a, ear_b + 0.03, SURFACE_AT_EAR + 0.01, (0.10, 0.10, 0.04), euler=(-15.0, 20.0, -35.0),
              note='центр уха; ухо растёт по +Y'),
        place('Пасть', 1.0, m_a, m_b, 0.0, (m_w, round(m_h, 3), round(m_len, 3)),
              note='коробка рыльца: блок `клин`'),
    ]
    nose_size = (NOSE_W, round(NOSE_W * 0.85, 3), round(NOSE_W * 0.8, 3))
    nose_a = L - nose_size[2] * 0.15                   # наполовину за острием: мочка — чёрная подушка, а не срез клина
    nose_b = -0.01
    places.append(dict(name='нос', parent='Пасть', attach=0.5,
                       attachOffset=[0.0, round((nose_b - m_b) / m_h, 4), round((nose_a - m_a) / m_len, 4)],
                       sizeRel=list(rel(nose_size, (m_w, m_h, m_len))), baseEuler=[0.0, 0.0, 0.0],
                       note='мочка на острие клина; доли — от калибра Пасти',
                       metres=dict(along=round(nose_a, 3), up=nose_b, lateral=0.0, size=list(nose_size))))
    return places, (m_w, m_h, m_len)


def senses():
    return [
        dict(role='Ear', block='ухо', offset=[0, 0, 0], scale=[1, 1, 1], euler=[0, 0, 0]),
        dict(role='Eye', block='глаз', offset=[0, 0, 0], scale=[0.60, 0.80, 0.90], euler=[0, 0, 0]),
        dict(role='Nose', block='капля', offset=[0, 0, 0], scale=[1, 1, 1], euler=[0, 0, 0]),
    ]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--out', default=os.path.join(HERE, 'out', 'ezh-head-layout-draft.json'))
    args = ap.parse_args()
    places, muzzle = layout()
    head_size = (W, H, L)
    doc = dict(head=dict(baseSize=list(head_size), sizeRel=list(rel(head_size, NECK_CALIBRE))),
               places=places, muzzle=dict(block='клин', offset=[0, 0, 0], scale=[1, 1, 1]), senses=senses())
    os.makedirs(os.path.dirname(args.out), exist_ok=True)
    with open(args.out, 'w', encoding='utf-8') as f:
        json.dump(doc, f, ensure_ascii=False, indent=2)
    print('калибр головы W×H×L = %.3f × %.3f × %.3f; sizeRel от шеи %s' % (head_size + (doc['head']['sizeRel'],)))
    for p in places:
        m = p['metres']
        print('  %-6s offset %-28s sizeRel %-28s (вдоль %.3f, к темени %.3f, вбок %.3f, габарит %s)' %
              (p['name'], p['attachOffset'], p['sizeRel'], m['along'], m['up'], m['lateral'], m['size']))
    print('рыльце %.3f × %.3f × %.3f' % muzzle)


if __name__ == '__main__':
    main()
