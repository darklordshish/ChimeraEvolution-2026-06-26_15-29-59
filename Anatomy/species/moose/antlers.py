# -*- coding: utf-8 -*-
"""РОГА ЛОСЯ — куски органа «Рога» ригблоками (`брусок`, `лопата`, `рог`) вместо 18 примитивов.

КАДР. Место `Рога` стоит на розетке (`head_layout.py`), его поворот гасит наклон головы — кадр ГОРИЗОНТАЛЕН:
+X наружу (правый рог; левый даёт mirrorX места), +Y вверх, +Z вперёд.

КАЛИБР — КУБ 0.60 м, и это не удобство счёта, а масштаб ХИМЕРЫ. Куски органа заданы в долях места, поэтому на чужом
носителе рога вырастают по его калибру `Рога` (у волка 0.10 × 0.13 × 0.14). Прежний лось держал рога в калибре
0.61 × 0.39 × 0.24 при размахе 2.31 м — привитые волку, они выходили ~0.4 м. Куб 0.60 при размахе 2.6 м даёт волку
те же ~0.4–0.55 м. (Черновик с калибром 0.10 ставил бы волку рога в полный лосиный рост.) Куб, а не прежняя коробка:
кусок повёрнут, и неравный калибр растягивал бы лопату по-разному на разных носителях.

ОТКУДА ЧИСЛА (метры от розетки):
  • `moose_head_front.jpg` (анфас, 0.442 м на 215 px): ствол уходит вбок почти горизонтально на 0.23 м, задняя лопата
    поднимается от конца ствола наружу-вверх до 0.84 вбок и 0.78 вверх, по её дальнему краю 4–5 отростков; передние
    отростки торчат вверх над стволом;
  • `moose_side_REFERENCE.jpg` (профиль, опоры `graph.py`): задняя лопата уходит назад на 0.78 при подъёме 0.73,
    передняя (надглазничная) — вперёд до 0.86 при подъёме до 0.5–0.7, с отростками вперёд-вверх.
Лопата лося — вогнутая чаша: её плоскость смотрит вверх и внутрь, к голове.

ПОВОРОТ БЛОКА задаётся осями: куда смотрит его +Z (ось роста у `брусок`/`лопата`) или +Y (у `рог`) и куда — нормаль.
Углы Эйлера Unity (порядок Z, X, Y: R = Ry·Rx·Rz) считаются из этих осей здесь, руками не подбираются.
"""
import math

CAL = 0.60     # калибр места «Рога», м — куб


def v_add(a, b): return tuple(x + y for x, y in zip(a, b))
def v_sub(a, b): return tuple(x - y for x, y in zip(a, b))
def v_mul(a, k): return tuple(x * k for x in a)
def v_dot(a, b): return sum(x * y for x, y in zip(a, b))
def v_len(a): return math.sqrt(v_dot(a, a))
def v_unit(a): return v_mul(a, 1.0 / v_len(a))


def v_cross(a, b):
    return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def euler_from_axes(x, y, z):
    """Ортонормированные оси блока в кадре места → углы Эйлера Unity. R = Ry(ey)·Rx(ex)·Rz(ez), столбцы R = (x, y, z)."""
    r = [[x[0], y[0], z[0]], [x[1], y[1], z[1]], [x[2], y[2], z[2]]]
    ex = math.degrees(math.asin(max(-1.0, min(1.0, -r[1][2]))))
    ey = math.degrees(math.atan2(r[0][2], r[2][2]))
    ez = math.degrees(math.atan2(r[1][0], r[1][1]))
    return [round(ex, 1), round(ey, 1), round(ez, 1)]


def frame(grow, normal_hint, grow_axis):
    """Оси блока: `grow` — направление роста, `normal_hint` — примерная нормаль (ортогонализуется).
    grow_axis 'z' — растёт по +Z, нормаль = +Y; 'y' — растёт по +Y, нормаль = +Z."""
    g = v_unit(grow)
    n = v_sub(normal_hint, v_mul(g, v_dot(normal_hint, g)))
    n = v_unit(n)
    if grow_axis == 'z':
        z, y = g, n
        x = v_cross(y, z)
    else:
        y, z = g, n
        x = v_cross(y, z)
    return x, y, z


def piece(block, start, grow, length, width, thick, normal_hint, grow_axis, overlap=0.0, note=''):
    """Кусок от точки `start` в направлении `grow` на `length` (начинается раньше на `overlap` — шов перекрыт)."""
    g = v_unit(grow)
    start = v_sub(start, v_mul(g, overlap))
    length += overlap
    centre = v_add(start, v_mul(g, length * 0.5))
    x, y, z = frame(grow, normal_hint, grow_axis)
    size = (width, thick, length) if grow_axis == 'z' else (width, length, thick)
    r = lambda t: [round(c, 3) for c in t]
    return dict(block=block, offset=r(v_mul(centre, 1 / CAL)), scale=r(v_mul(size, 1 / CAL)),
                euler=euler_from_axes(x, y, z), note=note,
                metres=dict(start=r(start), end=r(v_add(start, v_mul(g, length))), size=r(size)))


def antler():
    parts = []
    # СТВОЛ: от розетки вбок, чуть вверх и назад
    beam_end = (0.24, 0.05, -0.03)
    parts.append(piece('брусок', (0.0, 0.0, 0.0), beam_end, v_len(beam_end), 0.10, 0.10, (0, 1, 0), 'z', overlap=0.04,
                       note='ствол'))

    # ЗАДНЯЯ ЛОПАТА: от конца ствола наружу-вверх-назад; плоскость — к голове и вверх (нормаль внутрь-вверх)
    rear_dir = (0.70, 0.62, -0.45)
    rear_len = 0.95
    # лопата смотрит внутрь-вверх-ВПЕРЁД: широкой она видна и на анфасе, и в профиль (итерация 1: плоскость вдоль тела
    # давала на анфасе узкую доску галочкой)
    rear_normal = (-0.45, 0.50, 0.55)
    parts.append(piece('лопата', beam_end, rear_dir, rear_len, 0.56, 0.08, rear_normal, 'z', overlap=0.08,
                       note='задняя лопата'))
    # отростки по дальнему краю задней лопаты: от края дальше вдоль лопаты и вверх
    g = v_unit(rear_dir)
    xw, _, _ = frame(rear_dir, rear_normal, 'z')          # ширина лопаты в её плоскости
    rim = v_add(beam_end, v_mul(g, rear_len * 0.92))
    for i, (s, ln) in enumerate([(-0.22, 0.24), (-0.08, 0.30), (0.06, 0.28), (0.20, 0.22)]):
        base = v_add(rim, v_mul(xw, s))
        tine_dir = v_unit(v_add(v_mul(g, 0.7), (0.0, 0.55, 0.0)))
        tine_dir = v_unit(v_add(tine_dir, v_mul(xw, s * 1.2)))
        parts.append(piece('рог', base, tine_dir, ln, 0.065, 0.065, (0, 0, -1), 'y', overlap=0.05,
                           note='отросток задней лопаты %d' % (i + 1)))

    # ПЕРЕДНЯЯ ЛОПАТА (надглазничная): от конца ствола вперёд, наружу и вверх; отростки вперёд-вверх
    front_start = v_add(beam_end, (-0.04, 0.0, 0.04))
    front_dir = (0.40, 0.30, 0.55)
    front_len = 0.58
    front_normal = (-0.35, 0.80, -0.20)
    parts.append(piece('лопата', front_start, front_dir, front_len, 0.30, 0.07, front_normal, 'z', overlap=0.06,
                       note='передняя лопата'))
    gf = v_unit(front_dir)
    xf, _, _ = frame(front_dir, front_normal, 'z')
    rim_f = v_add(front_start, v_mul(gf, front_len * 0.92))
    for i, (s, ln) in enumerate([(-0.11, 0.22), (0.0, 0.26), (0.11, 0.20)]):
        base = v_add(rim_f, v_mul(xf, s))
        tine_dir = v_unit(v_add(v_mul(gf, 0.8), (0.0, 0.5, 0.0)))
        tine_dir = v_unit(v_add(tine_dir, v_mul(xf, s * 1.5)))
        parts.append(piece('рог', base, tine_dir, ln, 0.06, 0.06, (0, 0, -1), 'y', overlap=0.05,
                           note='отросток передней лопаты %d' % (i + 1)))
    return parts


if __name__ == '__main__':
    for p in antler():
        print('%-7s %-28s offset %-24s scale %-22s euler %s' % (p['block'], p['note'], p['offset'], p['scale'], p['euler']))
