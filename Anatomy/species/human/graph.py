# -*- coding: utf-8 -*-
"""СИЛУЭТНЫЙ ГРАФ ЧЕЛОВЕКА — поставка 8 модельной линии. Устройство — как у зверей (`../wolf/graph.py`).

ОТКУДА ЧИСЛА.
  • высоты и выносы суставов — `ref/skeleton/human_skeleton_front.png` (анатомическая позиция, ортография, открыт 17.09),
    сетка 50 px: макушка 45 px, подошва 1930 px; руки на снимке чуть отведены — так и оставлено, между рукой и
    бедром на клетке 0.084 нужен просвет;
  • калибр — игровой: макушка 1.84 м (прежний верх головы по карте тел 1.836), глаза 1.70 — там их ждёт первое лицо;
  • глубины (Z) — канон атлета в 8 голов (голова 0.23): ортографического профиля человека в паспорте НЕТ (`REFS.md`:
    Везалий опирается на постамент, Геракл в контрапосте, профиль черепа — обломок). Сложение — Фарнезский Геракл:
    тяжёлая грудь, толстая талия, широкая спина.
Координаты: Y от земли, Z вперёд, X вбок (правая сторона; левую даёт mirrorX).

ПОЛЕ И БЛОКИ. Правило «тоньше ~0.1 м — блоком»: предплечье у кисти 0.065, голень у лодыжки 0.07 — поле рук кончается
на трети предплечья, поле ног — на нижней трети голени. Ниже — куски органов «Кисть» и «Ноги» на узлах (`organs_layout.py`).
Имена узлов конечностей — как у зверей (`плечо`, `предплечье`, `бедро`, `голень`): привитые волчьи лапы сядут на концы
человечьих узлов, а не на коробку места.

ПЕРЕВОД ТОЧЕК В `Bone` — ОБЩИЙ, В 3D. У зверей узлы лежат в плоскости YZ, и перевод волка считает только поворот Rx.
Руки человека отведены, узлу нужен поворот и по Z. Здесь поворот узла строится из осей: +Y кости — вдоль узла,
+X кости — мировой X, снятый с оси узла (так `section` остаётся шириной, а `depth` — глубиной), +Z = X × Y.
Для узлов в плоскости YZ получается ровно то же, что у волка.

Запуск:  python graph.py [--out путь]
"""
import argparse
import json
import math
import os

HERE = os.path.dirname(os.path.abspath(__file__))

# ── СНИМОК → МЕТРЫ ──────────────────────────────────────────────────────────────────────────────────
CROWN_PX, SOLE_PX, MID_X_PX = 45.0, 1930.0, 480.0
CROWN_Y = 1.84
S = CROWN_Y / (SOLE_PX - CROWN_PX)


def px(x, y):
    """Пиксель скелета анфас → (X, Y), X — на правую сторону фигуры (лево снимка)."""
    return ((MID_X_PX - x) * S, (SOLE_PX - y) * S)


PHOTO = {
    'макушка':        (480, 45),
    'подбородок':     (480, 300),
    'плечевой':       (277, 416),
    'локоть':         (202, 710),
    'запястье':       (177, 976),
    'кончики_пальцев': (170, 1177),
    'тазобедренный':  (347, 911),
    'колено':         (411, 1403),
    'лодыжка':        (427, 1830),
}


def P(name):
    return px(*PHOTO[name])


def v_sub(a, b): return tuple(x - y for x, y in zip(a, b))
def v_add(a, b): return tuple(x + y for x, y in zip(a, b))
def v_mul(a, k): return tuple(x * k for x in a)
def v_dot(a, b): return sum(x * y for x, y in zip(a, b))
def v_len(a): return math.sqrt(v_dot(a, a))
def v_unit(a): return v_mul(a, 1.0 / v_len(a))
def v_cross(a, b): return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def point_at_y(a, b, y):
    t = (a[1] - y) / (a[1] - b[1])
    return v_add(a, v_mul(v_sub(b, a), t))


# ── СУСТАВЫ, м ───────────────────────────────────────────────────────────────────────────────────────
x_sh, y_sh = P('плечевой')
x_el, y_el = P('локоть')
x_wr, y_wr = P('запястье')
x_hip, y_hip = P('тазобедренный')
x_kn, y_kn = P('колено')
x_an, y_an = P('лодыжка')

# РУКИ И НОГИ РАЗВЕДЕНЫ ШИРЕ СКЕЛЕТА (итерация 1): при выносах со снимка поле рук сливалось с грудью и талией, а бёдра —
# друг с другом «юбкой» до колен уже на клетке 0.042. Руки — лёгкая «A»: локоть на 3 см, запястье на 6 см дальше от
# оси, чем на скелете; стопы на ширине таза (центры лодыжек ±0.10 против ±0.05). Высоты — снимка
SHOULDER = (round(x_sh, 3), round(y_sh, 3), -0.02)
ELBOW = (round(x_el + 0.03, 3), round(y_el, 3), -0.04)
WRIST = (round(x_wr + 0.06, 3), round(y_wr, 3), 0.0)
HIP = (0.115, round(y_hip - 0.02, 3), -0.02)
KNEE = (0.105, round(y_kn, 3), 0.0)
ANKLE = (0.100, 0.095, -0.02)

# ПОЛЕ РУКИ — до трети предплечья (Y 1.08: здесь предплечье атлета ещё ~0.10 м), ПОЛЕ НОГИ — до Y 0.27 (икра ~0.11)
FORE_END = point_at_y(ELBOW, WRIST, 1.08)
SHIN_END = point_at_y(KNEE, ANKLE, 0.27)


def node(name, socket, parent, a, b, r0, r1, section=1.0, depth=1.0, blend=0.0, mirror=False):
    return dict(name=name, socket=socket, parent=parent, a=tuple(a), b=tuple(b), r0=r0, r1=r1,
                section=section, depth=depth, blend=blend, mirror=mirror)


def build_nodes():
    return [
        # ТАЛИЯ — корень. Начало на 1.07: место `хребет` (0.60 по Y) встаёт центром туда, где стояло (1.37), и закрытые
        # места-графты (Хвост, Игломёт) остаются на своих местах
        node('хребет', 'хребет', '', (0, 1.07, -0.02), (0, 1.30, -0.01), 0.135, 0.130, section=1.12, depth=0.78, blend=0.10),
        # ТАЗ и ягодицы: шире талии (бёдра 0.35 м), зад на −0.15
        node('таз', 'хребет', 'хребет', (0, 1.08, -0.025), (0, 0.87, -0.04), 0.150, 0.120, section=1.20, depth=0.85, blend=0.10),
        # ГРУДЬ — слот `Сердце`: грудная клетка атлета 0.35 × 0.26 с широчайшими
        node('грудь', 'Сердце', 'хребет', (0, 1.18, -0.005), (0, 1.42, -0.015), 0.140, 0.155, section=1.13, depth=0.85, blend=0.12),
        node('шея', 'шея', 'грудь', (0, 1.41, -0.035), (0, 1.62, -0.02), 0.075, 0.060, section=1.10, depth=0.95, blend=0.08),
        # ГОЛОВА — от уровня подбородка к своду: место `голова` у человека вытянуто по Y и ложится вдоль узла, начало места
        # = начало узла. Лицо спереди снизу — деталь Пасти
        node('голова', 'голова', 'шея', (0, 1.595, 0.005), (0, 1.745, -0.01), 0.068, 0.090, section=0.85, depth=1.10, blend=0.06),
        # ПЛЕЧЕВОЙ ПОЯС: трапеция от шеи к плечу даёт скат плеч, конец — шапка дельты
        node('лопатка', 'Руки', 'грудь', (0.05, 1.53, -0.045), SHOULDER, 0.055, 0.068, section=0.90, depth=0.75, blend=0.10, mirror=True),
        node('плечо', 'Руки', 'лопатка', SHOULDER, ELBOW, 0.062, 0.048, section=0.95, depth=1.0, blend=0.04, mirror=True),
        node('предплечье', 'Руки', 'плечо', ELBOW, FORE_END, 0.050, 0.046, section=1.10, depth=0.90, blend=0.03, mirror=True),
        node('бедро', 'Ноги', 'таз', HIP, KNEE, 0.100, 0.060, section=0.85, depth=1.0, blend=0.06, mirror=True),
        node('голень', 'Ноги', 'бедро', KNEE, SHIN_END, 0.058, 0.050, section=0.95, depth=1.12, blend=0.03, mirror=True),
    ]


HIDES = ['хребет', 'шея', 'голова', 'Шкура', 'Сердце', 'Руки', 'Ноги']


# ── ГЕОМЕТРИЯ → `Bone`, в 3D ─────────────────────────────────────────────────────────────────────────
def node_axes(a, b):
    """Оси кости в мире: y — вдоль узла, x — мировой X без составляющей вдоль узла, z = x × y (правило Unity)."""
    y = v_unit(v_sub(b, a))
    ref = (1.0, 0.0, 0.0) if abs(y[0]) < 0.9 else (0.0, 0.0, 1.0)
    x = v_unit(v_sub(ref, v_mul(y, v_dot(ref, y))))
    z = v_cross(x, y)
    return x, y, z


def mat_cols(x, y, z):
    return [[x[0], y[0], z[0]], [x[1], y[1], z[1]], [x[2], y[2], z[2]]]


def mat_t(m):
    return [[m[j][i] for j in range(3)] for i in range(3)]


def mat_mul(a, b):
    return [[sum(a[i][k] * b[k][j] for k in range(3)) for j in range(3)] for i in range(3)]


def mat_vec(m, v):
    return tuple(sum(m[i][k] * v[k] for k in range(3)) for i in range(3))


def euler_of(m):
    """Матрица поворота → углы Эйлера Unity (R = Ry·Rx·Rz)."""
    ex = math.degrees(math.asin(max(-1.0, min(1.0, -m[1][2]))))
    ey = math.degrees(math.atan2(m[0][2], m[2][2]))
    ez = math.degrees(math.atan2(m[1][0], m[1][1]))
    return ex, ey, ez


def to_bones(nodes):
    world = {}
    out = []
    for n in nodes:
        a, b = n['a'], n['b']
        rw = mat_cols(*node_axes(a, b))
        length = v_len(v_sub(b, a))
        bone = dict(name=n['name'], parent=n['parent'], layer=0, socket=n['socket'],
                    origin=dict(x=0.0, y=0.0, z=0.0), attach=1.0, freeOrigin=False,
                    length=round(length, 4), endBone='', endAttach=1.0, dir=dict(x=0.0, y=0.0, z=0.0),
                    r0=n['r0'], r1=n['r1'], section=n['section'], depth=n['depth'],
                    blend=n['blend'], chain=0, mirrorX=n['mirror'])
        if not n['parent']:
            rel = rw
            bone['origin'] = dict(x=round(a[0], 4), y=round(a[1], 4), z=round(a[2], 4))
        else:
            pa, pr, plen = world[n['parent']]
            rel = mat_mul(mat_t(pr), rw)
            tip = v_add(pa, v_mul(tuple(pr[i][1] for i in range(3)), plen))
            if v_len(v_sub(a, tip)) < 1e-4:
                bone['attach'] = 1.0
            else:
                loc = mat_vec(mat_t(pr), v_sub(a, pa))
                bone['freeOrigin'] = True
                bone['origin'] = dict(x=round(loc[0], 4), y=round(loc[1], 4), z=round(loc[2], 4))
        ex, ey, ez = euler_of(rel)
        bone['dir'] = dict(x=round(ex, 3), y=round(ey, 3), z=round(ez, 3))
        world[n['name']] = (a, rw, length)
        out.append(bone)
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--out', default=os.path.join(HERE, 'out', 'chelovek-graph-draft.json'))
    args = ap.parse_args()
    nodes = build_nodes()
    bones = to_bones(nodes)
    os.makedirs(os.path.dirname(args.out), exist_ok=True)
    with open(args.out, 'w', encoding='utf-8') as f:
        json.dump(dict(species='Человек', hides=HIDES, nodes=bones), f, ensure_ascii=False, indent=2)
    print('узлов', len(bones), '→', args.out)
    for n in nodes:
        print('  %-10s %-7s (%.3f %.3f %.3f) → (%.3f %.3f %.3f)  r %.3f→%.3f' %
              ((n['name'], n['socket']) + tuple(n['a']) + tuple(n['b']) + (n['r0'], n['r1'])))


if __name__ == '__main__':
    main()
