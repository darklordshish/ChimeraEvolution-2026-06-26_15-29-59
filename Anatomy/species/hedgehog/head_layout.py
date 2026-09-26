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
    'темя':             G.PHOTO['темя'],   # (25.09: точки листа Хеджхалка, было — фото природного ежа)
    'челюсть_низ':      (412, 322),        # низ челюсти под глазом
    'глаз':             G.PHOTO['глаз'],
    'ухо':              G.PHOTO['ухо'],
    'лоб_у_глаза':      (416, 275),        # верх рыла над глазом
    'подбородок_у_глаза': (416, 322),
}
OUTLINE_W = 0.95 * G.TOP_Y            # ширина юбки игл по контуру (анфас)
W = round(0.42 * OUTLINE_W, 3)        # голова по щекам
NOSE_W = round(0.10 * OUTLINE_W, 3)   # крупнее (r3: «шайбочка»)  # мочка: на анфасе 0.07 юбки, но на острие клина такой ширины она сливалась с торцом — чуть крупнее
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
SURFACE_AT_EYE = {0.084: 0.150, 0.042: 0.150}[GRAIN]   # Хеджхалк 25.09: голова крупнее (череп r 0.19 → 0.16 ×0.95); было 0.120
SURFACE_AT_EAR = {0.084: 0.120, 0.042: 0.120}[GRAIN]   # ухо у темени, ближе к оси (было 0.158 у купола)
MUZZLE_FROM = 0.10      # морда от затылка, м — щёки внутри черепа
EYE_ALONG, EYE_UP = 0.155, 0.030  # на черепе под навесом (r6: глаз сидел на торце морды — назад на 3.5 см)   # под надбровьем, на стыке морды и черепа (критик r1: глаза торчали над мордой)
EAR_ALONG, EAR_UP, EAR_LAT = 0.04, 0.17, 0.12   # уши меньше, прижаты назад-вбок (критик r1: «корона из трёх рогов»)
MUZZLE_W = 0.80         # ширина рыла к ширине головы (было 0.45 — рыльце ежа)
MUZZLE_BACK = 0.08      # рыло заходит в череп за глаз, м
MUZZLE_H_ABS = 0.26     # высота рыла у основания, м — как череп у глаз
MUZZLE_UP = -0.03       # центр рыла чуть ниже оси черепа, м (было −0.066 — висело под черепом)
MUZZLE_FWD = -0.10       # рыло вперёд за калибр головы, м: короткое читалось кабаньим пятачком, у Хеджхалка морда волчья
KLIN = [(-0.50, 1.00, 1.00), (-0.10, 0.80, 0.76), (+0.50, 0.43, 0.40)]   # сечения `клин` (`Tools/Blender/blocks.py`)


def klin_at(t):
    for (t0, w0, h0), (t1, w1, h1) in zip(KLIN, KLIN[1:]):
        if t0 <= t <= t1:
            k = (t - t0) / (t1 - t0)
            return w0 + (w1 - w0) * k, h0 + (h1 - h0) * k
    return KLIN[-1][1:]


def teeth(muzzle):
    """ПАСТЬ И НАДБРОВЬЯ ХЕДЖХАЛКА (критик r1, 25.09). Детали в кадре калибра Пасти (доли W, H, L; t от центра морды).
      • нижняя челюсть — толстый `клин` (≈ 9 см у угла), угол заведён назад под глаз и утоплен в щёку; раскрыта на 16°
        от шарнира: передний край ниже заднего, щель открывается к носу, а не идёт вдоль морды;
      • зубы — ряд `игла` по верхней и нижней кромке, по 4 на сторону: пасть хищника — главная деталь листа;
      • надбровья — `клин` козырьком над каждым глазом: глаз утоплен под бровью, взгляд злой."""
    W_, H_, L_ = muzzle
    white = [0.95, 0.94, 0.90, 1.0]
    out = [dict(name='нижняя челюсть', block='клин', euler=[16, 0, 0],
                offset=[0.0, -0.60, -0.10], scale=[0.70, 0.42, 0.96])]   # r6: доска → клин выше у угла
    for side in (+1, -1):
        s = '(пр)' if side > 0 else '(лев)'
        for i, t in enumerate((0.34, 0.22, 0.10, -0.02)):
            big = i == 0
            k = 1.0 - (t + 0.10) / 0.60 * 0.45                 # сужение морды к носу (сечения `морда`)
            out.append(dict(name='зуб верхний %d %s' % (i, s), block='игла', euler=[180, 0, 0], color=white,
                            offset=[round(side * 0.30 * k, 3), -0.50, round(t, 3)],
                            scale=[round((0.022 if big else 0.016) / W_, 3), round((0.07 if big else 0.04) / H_, 3),
                                   round((0.022 if big else 0.016) / L_, 3)]))
            out.append(dict(name='зуб нижний %d %s' % (i, s), block='игла', euler=[16, 0, 0], color=white,
                            offset=[round(side * 0.26 * k, 3), -0.62 - 0.08 * (t + 0.1), round(t - 0.04, 3)],
                            scale=[round(0.014 / W_, 3), round((0.05 if big else 0.03) / H_, 3), round(0.014 / L_, 3)]))
    # НАДБРОВЬЯ — НАВЕС НАД КАЖДЫМ ГЛАЗОМ (критик r4, 27.09): один клин во весь лоб в профиль стоял столбом, а глаз торчал
    # на его верхушке. Теперь два коротких клина остриём вперёд-вниз; r6: вынос 3 см тонул в поле — навес крупнее и выше
    for side in (+1, -1):
        out.append(dict(name='надбровье ' + ('(пр)' if side > 0 else '(лев)'), block='клин', euler=[24, side * 14, 0],
                        offset=[round(side * 0.34, 3), 0.34, -0.24], scale=[0.40, 0.22, 0.46]))
    return out


def layout():
    # МОРДА — ПЕРЕДНЯЯ ЧАСТЬ ГОЛОВЫ БЛОКОМ `морда` (25.09): щёки, перелом лба, переносица, нос. От щёк (MUZZLE_FROM,
    # внутри черепа) до носа; череп-поле держит лоб и затылок. Прежние клин и рыльце читались кротом
    m_a0, m_a1 = MUZZLE_FROM, round(L + MUZZLE_FWD, 3)
    m_len, m_h, m_w = m_a1 - m_a0, round(MUZZLE_H_ABS, 3), round(MUZZLE_W * W, 3)
    m_a, m_b = (m_a0 + m_a1) / 2, MUZZLE_UP
    # ГЛАЗА — НА ПЕРЕЛОМЕ ЛБА, у верхнего угла морды, смотрят вперёд-вбок (лист: крупные, на стопе). Вбок — по сечению
    # блока (`blocks.py`, морда: ширина 0.96 на t −0.22, 0.70 на −0.04); прежний вынос по замеру старого черепа их топил
    eye_a = EYE_ALONG
    t = (eye_a - m_a) / m_len
    k = 1.0 + (0.96 - 1.0) * (t + 0.30) / 0.14 if t < -0.16 else 0.96 + (0.70 - 0.96) * (t + 0.16) / 0.12
    eye_lat = round(m_w / 2 * k + 0.004, 3)   # утоплен на треть, а не целиком (r3: была видна верхушка)
    places = [
        place('глаза', 0.5, eye_a, EYE_UP, eye_lat, (0.085, 0.055, 0.075), euler=(0.0, 25.0, -12.0),   # крупнее, миндалём к носу (r2)
             
              note='на стопе, у верхнего угла морды; смотрят вперёд-вбок'),
        # УШИ — ТОРЧКОМ НАД ТЕМЕНЕМ, острые (лист): прежние 0.10 × 0.10 тонули в иглах
        place('уши', 0.5, EAR_ALONG, EAR_UP, EAR_LAT, (0.09, 0.11, 0.04), euler=(40.0, 20.0, -35.0),   # остриём назад (r2: при −48 смотрело вперёд)
             
              note='центр уха; ухо растёт по +Y'),
        place('Пасть', 1.0, m_a, m_b, 0.0, (m_w, round(m_h, 3), round(m_len, 3)),
              note='морда: блок `морда` + нижняя челюсть и клыки (Хеджхалк)'),
    ]
    nose_size = (NOSE_W, round(NOSE_W * 0.85, 3), round(NOSE_W * 0.8, 3))
    nose_a = m_a1 - nose_size[2] * 0.30
    nose_b = round(m_b - 0.24 * m_h + 0.01, 3)         # центр торца морды опущен (сечения блока: cy −0.24)
    places.append(dict(name='нос', parent='Пасть', attach=0.5,
                       attachOffset=[0.0, round((nose_b - m_b) / m_h, 4), round((nose_a - m_a) / m_len, 4)],
                       sizeRel=list(rel(nose_size, (m_w, m_h, m_len))), baseEuler=[0.0, 0.0, 0.0],
                       note='мочка на торце морды; доли — от калибра Пасти',
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
               places=places, muzzle=dict(block='морда', offset=[0, 0, 0], scale=[0.86, 1, 1]), teeth=teeth(muzzle), senses=senses())
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
