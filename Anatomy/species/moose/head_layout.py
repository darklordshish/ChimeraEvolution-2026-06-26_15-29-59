# -*- coding: utf-8 -*-
"""РАСКЛАДКА ГОЛОВЫ ЛОСЯ — калибр места `голова` и доли деталей на ней (устройство — как у волка, `../wolf/head_layout.py`).

КАК ЭТО СВЯЗАНО С ИГРОЙ. Место `голова` погашено графом и берёт у одноимённого узла ПОЗУ: начало места — в начале
узла, длинная ось (Z) — вдоль узла, +Y — к темени. Калибр (W × H × L) место держит СВОЙ, от него — доли деталей:
    вдоль оси от начала узла  = (attach + attachOffset.z) · L
    к темени                  = attachOffset.y · H
    вбок                      = attachOffset.x · W

ОТКУДА ЧИСЛА.
  • профиль — `ref/photo/moose_side_REFERENCE.jpg` (опоры и масштаб `graph.py`), точки переведены в кадр ГОЛОВЫ СНИМКА:
    начало — основание черепа, ось — к кончику носа;
  • ширины — `ref/photo/moose_head_front.jpg` (голова строго анфас, открыт 17.09): доли от ширины головы по глазам,
    215 px. Сама ширина головы в метрах — из пропорции лося (голова по глазам ≈ 0.48 длины головы: 33 см при 68 см),
    профиль её не даёт: 0.48 · 0.92 = 0.44 м.

Запуск:  python head_layout.py [--out путь]
"""
import argparse
import json
import math
import os

import graph as G

HERE = os.path.dirname(os.path.abspath(__file__))

# КЛЕТКА ПРОЕКТА — 0.042 (решение геймдизайнера 22.09: «как у человека, у всех»). Замеры поверхности — на обе клетки
GRAIN = 0.042

# ── ТОЧКИ ГОЛОВЫ НА СНИМКЕ-ПРОФИЛЕ, px исходника ─────────────────────────────────────────────────────
HEAD_PX = {
    'основание_черепа': G.PHOTO['основание_черепа'],
    'кончик_носа':      G.PHOTO['кончик_носа'],
    'темя':             (400, 385),    # верх черепа между основаниями рогов
    'рог_основание':    (385, 390),    # розетка
    'глаз':             (318, 418),
    'переносица':       (300, 428),    # верх морды над глазом
    'челюсть_низ':      (320, 648),    # низ челюсти под глазом
    'ноздря':           (165, 580),
    'губа_низ':         (125, 645),    # нависающая верхняя губа, низ
}
# КОНТУР МОРДЫ на профиле: верх (спинка носа, «римский» горб) и низ (губа, подбородок, челюсть), px
MUZZLE_TOP = [(300, 428), (250, 440), (200, 470), (160, 510), (130, 545), (112, 580), (108, 610)]
MUZZLE_BOTTOM = [(330, 645), (300, 652), (250, 660), (200, 662), (160, 652), (125, 645), (110, 628)]

# ── АНФАС: ширины в долях ширины головы по глазам (215 px, центр x 607) ───────────────────────────────
FRONT_HEAD_W_PX = 215.0
FRONT = {
    'глаз_вбок':        83 / FRONT_HEAD_W_PX,     # центр глаза от оси
    'морда_у_глаз':     110 / FRONT_HEAD_W_PX,    # спинка носа под глазами (y 620) — самое узкое
    'морда_середина':   120 / FRONT_HEAD_W_PX,    # y 680
    'морда_ноздри':     135 / FRONT_HEAD_W_PX,    # y 740 — ноздри раздают морду вширь
    'рог_вбок':         80 / FRONT_HEAD_W_PX,     # розетка от оси
    'ухо_вбок':         117 / FRONT_HEAD_W_PX,    # середина уха от оси
    'ухо_над_глазом':   175 / FRONT_HEAD_W_PX,    # середина уха выше глаза
    'ухо_длина':        150 / FRONT_HEAD_W_PX,
    'ухо_ширина':       90 / FRONT_HEAD_W_PX,
}


def world(p):
    z, y = G.px(*p)
    return (y, z)


def photo_frame():
    oy, oz = world(HEAD_PX['основание_черепа'])
    ny, nz = world(HEAD_PX['кончик_носа'])
    dy, dz = ny - oy, nz - oz
    L = math.hypot(dy, dz)
    u = (dy / L, dz / L)                 # вдоль головы
    v = (u[1], -u[0])                    # к темени
    if v[0] < 0:
        v = (-v[0], -v[1])
    return (oy, oz), u, v, L


def local_p(p):
    (oy, oz), u, v, _ = photo_frame()
    y, z = world(p)
    d = (y - oy, z - oz)
    return (d[0] * u[0] + d[1] * u[1], d[0] * v[0] + d[1] * v[1])   # (вдоль, к темени)


def local(name):
    return local_p(HEAD_PX[name])


def head_deg():
    """Наклон головы на снимке, градусы к горизонту (минус — носом вниз). Им же ставится узел `голова` в графе."""
    _, u, _, _ = photo_frame()
    return math.degrees(math.atan2(u[0], u[1]))


# ── КАЛИБР МЕСТА «голова» ────────────────────────────────────────────────────────────────────────────
_, _, _, L_PHOTO = photo_frame()
L = round(L_PHOTO + 0.005, 3)
H = round(local('темя')[1] - local('челюсть_низ')[1], 3)
W = round(0.48 * L, 3)
# калибр места `шея` = калибр корня `хребет` × sizeRel шеи (бутстрап: (0.521, 0.791, 2.284) × (0.805, 0.708, 0.306))
NECK_CALIBRE = (0.521 * 0.805, 0.791 * 0.708, 2.284 * 0.306)


def rel(abs_size, cal):
    return tuple(round(a / c, 4) for a, c in zip(abs_size, cal))


def place(name, attach, along, up, lateral, size, euler=(0.0, 0.0, 0.0), note='', parent='голова'):
    return dict(name=name, parent=parent, attach=attach,
                attachOffset=[round(lateral / W, 4), round(up / H, 4), round(along / L - attach, 4)],
                sizeRel=list(rel(size, (W, H, L))), baseEuler=list(euler), note=note,
                metres=dict(along=round(along, 3), up=round(up, 3), lateral=round(lateral, 3), size=list(size)))


def muzzle_profile():
    """Контур морды в кадре головы: [(вдоль, верх, низ)] — для генератора блока и сверки."""
    top = sorted(local_p(p) for p in MUZZLE_TOP)
    bot = sorted(local_p(p) for p in MUZZLE_BOTTOM)

    def interp(pts, a):
        for (a0, b0), (a1, b1) in zip(pts, pts[1:]):
            if a0 <= a <= a1:
                return b0 + (b1 - b0) * (a - a0) / (a1 - a0 or 1)
        return pts[0][1] if a < pts[0][0] else pts[-1][1]
    lo, hi = max(top[0][0], bot[0][0]), min(top[-1][0], bot[-1][0])
    return [(a, interp(top, a), interp(bot, a)) for a in [lo + (hi - lo) * i / 8 for i in range(9)]], top, bot


def unity_rot(euler, v):
    """Поворот вектора углами Эйлера Unity: R = Ry·Rx·Rz."""
    ex, ey, ez = (math.radians(e) for e in euler)
    x, y, z = v
    x, y = x * math.cos(ez) - y * math.sin(ez), x * math.sin(ez) + y * math.cos(ez)
    y, z = y * math.cos(ex) - z * math.sin(ex), y * math.sin(ex) + z * math.cos(ex)
    x, z = x * math.cos(ey) + z * math.sin(ey), -x * math.sin(ey) + z * math.cos(ey)
    return (x, y, z)


def layout():
    eye_a, eye_b = local('глаз')
    horn_a, horn_b = local('рог_основание')
    top_a, top_b = local('переносица')
    jaw_a, jaw_b = local('челюсть_низ')
    nos_a, nos_b = local('ноздря')

    # ПАСТЬ = МОРДА. Коробка от переносицы над глазом (морда заходит в череп на 6 см) до кончика носа; по высоте — от низа
    # челюсти до спинки носа над глазом. Ширина — по ноздрям, самой широкой станции морды
    m_a0, m_a1 = round(top_a - 0.06, 3), L
    m_b0, m_b1 = round(jaw_b, 3), round(top_b, 3)
    m_len, m_h, m_w = m_a1 - m_a0, m_b1 - m_b0, round(FRONT['морда_ноздри'] * W, 3)
    m_a, m_b = (m_a0 + m_a1) / 2, (m_b0 + m_b1) / 2

    hn = next(n for n in G.build_nodes() if n['name'] == 'голова')
    # ЗАМЕР, А НЕ РАСЧЁТ: поверхность оболочки узла `голова` вбок в точке глаза (`ProbeM.SurfaceX` по вершинам) — 0.152 м на
    # клетке 0.084, 0.171 на 0.042 (расчёт давал 0.163, анфас — 0.164). Глаз шириной 3.5 см садится центром на 4 мм
    # наружу: треть в черепе, выступ ~2 см. Со старым замером на 0.042 он утонул бы почти целиком
    SURFACE_AT_EYE = {0.084: 0.152, 0.042: 0.137}
    eye_lat = round(SURFACE_AT_EYE[GRAIN] + 0.004, 3)
    # ВЕРХ ЧЕРЕПА у основания уха — расчёт узла плюс рост оболочки: замер верха 2.548 на 0.084 и 2.572 на 0.042
    GROWTH_AT_TOP = {0.084: 0.018, 0.042: 0.042}

    ear_len, ear_w = FRONT['ухо_длина'] * W, FRONT['ухо_ширина'] * W
    # УХО ОТ ОСНОВАНИЯ, а не от центра: основание — на поверхности черепа за розеткой рога (вбок 0.12, на 2 см внутри верха
    # узла), центр уха — на полдлины вдоль его оси. Итерация 5 ставила центр по анфасу, и основание висело в воздухе
    # на 9 см над черепом, прикрытое стволом рога (низ уха на 3 см выше оболочки)
    EAR_EULER = (-34.0, 35.0, -55.0)
    ear_axis = unity_rot(EAR_EULER, (0.0, 1.0, 0.0))            # (вбок, к темени, вдоль) в кадре головы
    top = hn['r0'] * hn['depth'] + GROWTH_AT_TOP[GRAIN]
    ear_base = (0.12, top - 0.02, horn_a - 0.07)
    ear_c = tuple(b + a * ear_len * 0.5 for b, a in zip(ear_base, ear_axis))
    places = [
        place('глаза', 0.5, eye_a, eye_b, eye_lat, (0.07, 0.07, 0.07),
              note='на замеренной поверхности узла `голова`, треть в черепе'),
        # УХО — основанием на черепе за розеткой, развалено вбок и назад, как на анфасе. Наклон головы (−24°) в угле снят
        place('уши', 0.5, ear_c[2], ear_c[1], ear_c[0],
              (round(ear_w, 3), round(ear_len, 3), 0.05), euler=EAR_EULER,
              note='центр уха; ухо растёт по +Y'),
        place('Пасть', 1.0, m_a, m_b, 0.0, (m_w, m_h, round(m_len, 3)),
              note='коробка морды: блок `булава`; ось Z длиннее высоты с запасом'),
        place('Рога', 0.5, horn_a, horn_b, FRONT['рог_вбок'] * W, (0.60, 0.60, 0.60), euler=(head_deg(), 0.0, 0.0),
              note='розетка рога; калибр — куб 0.60 (им рога масштабируются на химере, `antlers.py`), кадр выровнен в горизонт'),
    ]
    # МОЧКА ЕДЕТ С МОРДОЙ: доли от калибра Пасти, `parent` — сверка. У лося мочка — подушка ноздрей на переднем торце
    nose_size = (round(FRONT['морда_ноздри'] * W * 0.92, 3), 0.16, 0.12)
    nose_a = L - nose_size[2] * 0.5 + 0.02
    nose = dict(name='нос', parent='Пасть', attach=0.5,
                attachOffset=[0.0, round((nos_b - m_b) / m_h, 4), round((nose_a - m_a) / m_len, 4)],
                sizeRel=list(rel(nose_size, (m_w, m_h, m_len))), baseEuler=[0.0, 0.0, 0.0],
                note='подушка ноздрей на торце морды; доли — от калибра Пасти',
                metres=dict(along=round(nose_a, 3), up=round(nos_b, 3), lateral=0.0, size=list(nose_size)))
    places.append(nose)
    return places, (m_w, m_h, m_len), (m_a, m_b)


def senses():
    """Признаки чувств блоками, в калибре мест-адресов. Цвет глаза не задаётся — это канал механики (у лося — слух)."""
    return [
        dict(role='Ear', block='ухо', offset=[0, 0, 0], scale=[1, 1, 1], euler=[0, 0, 0]),
        dict(role='Eye', block='глаз', offset=[0, 0, 0], scale=[0.50, 0.55, 1.10], euler=[0, 0, 0]),
        dict(role='Nose', block='капля', offset=[0, 0, 0], scale=[1.0, 1.0, 1.0], euler=[0, 0, 0]),
    ]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--out', default=os.path.join(HERE, 'out', 'los-head-layout-draft.json'))
    args = ap.parse_args()

    places, muzzle, centre = layout()
    head_size = (W, H, L)
    doc = dict(
        head=dict(baseSize=list(head_size), sizeRel=list(rel(head_size, NECK_CALIBRE))),
        places=places,
        muzzle=dict(block='булава', offset=[0, 0, 0], scale=[1, 1, 1]),
        senses=senses(),
    )
    os.makedirs(os.path.dirname(args.out), exist_ok=True)
    with open(args.out, 'w', encoding='utf-8') as f:
        json.dump(doc, f, ensure_ascii=False, indent=2)

    print('наклон головы на снимке %.1f°' % head_deg())
    print('калибр головы W×H×L = %.3f × %.3f × %.3f м; sizeRel от шеи = %s' % (head_size + (doc['head']['sizeRel'],)))
    for p in places:
        m = p['metres']
        print('  %-6s attach %.1f  offset %-28s sizeRel %-28s  (вдоль %.3f, к темени %.3f, вбок %.3f, габарит %s)' %
              (p['name'], p['attach'], p['attachOffset'], p['sizeRel'], m['along'], m['up'], m['lateral'], m['size']))
    prof, _, _ = muzzle_profile()
    mw, mh, ml = muzzle
    ma, mb = centre
    print('коробка морды %.3f × %.3f × %.3f, центр вдоль %.3f, к темени %.3f' % (mw, mh, ml, ma, mb))
    print('контур морды в долях коробки: t (−0.5…0.5) · верх · низ · высота · центр')
    for a, t, b in prof:
        print('  t %+.2f  верх %+.2f  низ %+.2f  h %.2f  cy %+.2f' %
              ((a - ma) / ml, (t - mb) / mh, (b - mb) / mh, (t - b) / mh, ((t + b) / 2 - mb) / mh))


if __name__ == '__main__':
    main()
