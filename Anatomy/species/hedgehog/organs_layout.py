# -*- coding: utf-8 -*-
"""РАСКЛАДКА КУСКОВ ОРГАНОВ ЕЖА — иглы, игломёт и задние стопы ригблоками (`<вид>-organs-layout.json`).

ИГЛЫ (орган «Иглы», место `Шкура`). Куски в каноничном кадре места (X поперёк, Y наружу, Z вдоль тела;
`visualAlignToBody`), в долях калибра `Шкура` (0.76 × 0.60 × 1.05). Центр места — начало узла `хребет` + половина
длины места вдоль узла. Узел горизонтален, поэтому кадр места совпадает с мировым.
  • где стоит игла — точка ПОВЕРХНОСТИ ПОЛЯ, найденная лучом из центра купола по графу (поле узлов считается
    здесь же, с ростом оболочки на клетке 0.084);
  • куда смотрит — нормаль, заваленная назад на 62°: у ежа иглы лежат к хвосту, торчком они только в клубке;
  • основание утоплено в купол, наружу выходит ~2/3 длины.
Направления — сетка широт и долгот купола вразбежку, симметричная по бокам, без лица под козырьком и без брюха.

ИГЛОМЁТ (орган «Игломёт», своё место на спине). Четыре длинные иглы веером вперёд, как у прежнего органа:
куда смотрят стволы — туда и летит залп. Калибр места 0.24 куб.

ЗАДНИЕ СТОПЫ (орган «Ежиные ноги», узел `голень`). Ёж стопоходящий: блок `лапа` подошвой на земле, пальцы вперёд.

Запуск:  python organs_layout.py [--out путь]
"""
import argparse
import importlib.util
import json
import math
import os

import graph as G

HERE = os.path.dirname(os.path.abspath(__file__))


def _load(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


_WL = _load('wolf_organs', os.path.join(HERE, '..', 'wolf', 'organs_layout.py'))
_AN = _load('moose_antlers', os.path.join(HERE, '..', 'moose', 'antlers.py'))
v_add, v_sub, v_mul, v_dot, v_len, v_unit = _AN.v_add, _AN.v_sub, _AN.v_mul, _AN.v_dot, _AN.v_len, _AN.v_unit
frame, euler_from_axes = _AN.frame, _AN.euler_from_axes

SKIN_CAL = (0.76, 0.60, 1.05)          # калибр места `Шкура` = калибр `хребет`
GUN_CAL = 0.24                         # калибр места `Игломёт`
# РОСТ ОБОЛОЧКИ КУПОЛА против расчёта узлов — ЗАМЕР, а не поправка волка: верх купола 0.767/0.768 при расчёте 0.765, бок
# 0.330/0.330 при 0.328 (клетки 0.084/0.042) — у крупного купола рост ~0.3 см. Прежние 0.025 (поправка волка) выносили
# иглы на 2 см наружу; основание утоплено на 7 см, поэтому они держались, но глубже теперь честнее
GRAIN = 0.042          # клетка проекта (решение геймдизайнера 22.09)
GROWTH = {0.084: 0.004, 0.042: 0.004}[GRAIN]


# ── ПОЛЕ ГРАФА (приближение для расстановки): эллиптические конусы узлов, объединение по минимуму ─────────────
def node_sdf(n, p):
    a, b = n['a'], n['b']
    ab = v_sub(b, a)
    L2 = v_dot(ab, ab)
    t = max(0.0, min(1.0, v_dot(v_sub(p, a), ab) / L2))
    c = v_add(a, v_mul(ab, t))
    q = v_sub(p, c)
    d = v_unit(ab)
    perp_in_plane = v_sub(q, v_mul(d, v_dot(q, d)))
    qx = perp_in_plane[0]
    qz = v_len((0.0, perp_in_plane[1], perp_in_plane[2]))
    along = v_dot(q, d)                               # за торцом — сферическая шапка
    r = n['r0'] + (n['r1'] - n['r0']) * t
    return math.sqrt((qx / n['section']) ** 2 + (qz / n['depth']) ** 2 + along ** 2) - r


BODY_NODES = [n for n in G.build_nodes() if n['name'] in ('хребет', 'козырёк')]


def sdf(p):
    return min(node_sdf(n, p) for n in BODY_NODES) - GROWTH


def surface(origin, direction):
    """Точка на поверхности купола по лучу из центра (бисекция по знаку поля)."""
    lo, hi = 0.0, 2.0
    for _ in range(60):
        mid = (lo + hi) / 2
        if sdf(v_add(origin, v_mul(direction, mid))) < 0:
            lo = mid
        else:
            hi = mid
    return v_add(origin, v_mul(direction, lo))


def normal(p, e=0.005):
    g = tuple((sdf(v_add(p, tuple(e if i == k else 0 for i in range(3)))) -
               sdf(v_sub(p, tuple(e if i == k else 0 for i in range(3))))) for k in range(3))
    return v_unit(g)


# ── ИГЛЫ ─────────────────────────────────────────────────────────────────────────────────────────────
# СЕТКА, А НЕ СПИРАЛЬ: широта — от хвоста к голове (полюс сетки на оси тела), долгота — от хребта вбок, ряды вразбежку.
# Долготы симметричны, поэтому левый бок — зеркало правого. Итерация 1 (спираль Фибоначчи, 31 игла по 0.24 м, завал
# 50°) дала редкие палки, торчащие на 0.1 м над контуром снимка и на 0.2 м за задом, и неровный узор по бокам;
# итерация 2 (сетка до широты 36°) — иглы только на задней половине купола
SPINE_LEN, SPINE_D, SPINE_EMBED, SPINE_TILT = 0.17, 0.05, 0.07, 62.0
DOME_CENTRE = (0.0, 0.36, -0.05)
LATS = tuple(range(-75, 76, 15))                       # градусы: минус — к хвосту; итерация 2 кончалась на 36 — перёд был лысым
SKIRT_Y = 0.20                                         # ниже — мех юбки, игл нет
FACE = dict(z=0.30, y=0.50)                            # впереди и ниже — лицо под козырьком
FRONT_Z = 0.40                                         # дальше — кончик козырька: поле там тоньше расчёта, игла висела в воздухе


def spine_directions():
    for i, lat in enumerate(LATS):
        step = 20 if abs(lat) < 60 else 40             # у полюсов сетки кольцо короче — реже, иначе иглы в пучке
        lons = range(0, 91, step) if i % 2 == 0 else range(step // 2, 91, step)
        for lon in sorted({l for x in lons for l in (x, -x)}):
            la, lo = math.radians(lat), math.radians(lon)
            yield lat, lon, v_unit((math.sin(lo) * math.cos(la), math.cos(lo) * math.cos(la), math.sin(la)))


def spines():
    skin_centre = v_add(G.SPINE_A, v_mul(v_unit(v_sub(G.SPINE_B, G.SPINE_A)), 0.5 * SKIN_CAL[2]))
    parts = []
    for lat, lon, d in spine_directions():
        p = surface(DOME_CENTRE, d)
        if p[1] < SKIRT_Y or (p[2] > FACE['z'] and p[1] < FACE['y']) or p[2] > FRONT_Z:
            continue
        n = normal(p)
        back = v_sub((0.0, 0.0, -1.0), v_mul(n, v_dot((0.0, 0.0, -1.0), n)))
        if v_len(back) < 0.2:                          # на самом заду нормаль уже смотрит назад — валим иглу вниз
            back = v_sub((0.0, -1.0, 0.0), v_mul(n, v_dot((0.0, -1.0, 0.0), n)))
        back = v_unit(back)
        t = math.radians(SPINE_TILT)
        grow = v_unit(v_add(v_mul(n, math.cos(t)), v_mul(back, math.sin(t))))
        base = v_sub(p, v_mul(grow, SPINE_EMBED))
        centre = v_add(base, v_mul(grow, SPINE_LEN / 2))
        x, yy, z = frame(grow, (1.0, 0.0, 0.0) if abs(grow[0]) < 0.9 else (0.0, 0.0, 1.0), 'y')
        rel = v_sub(centre, skin_centre)
        parts.append(dict(block='игла',
                          offset=[round(rel[k] / SKIN_CAL[k], 4) for k in range(3)],
                          scale=[round(SPINE_D / SKIN_CAL[0], 4), round(SPINE_LEN / SKIN_CAL[1], 4), round(SPINE_D / SKIN_CAL[2], 4)],
                          euler=euler_from_axes(x, yy, z), note='широта %d, долгота %d' % (lat, lon),
                          metres=dict(centre=[round(c, 3) for c in centre], size=[SPINE_D, SPINE_LEN, SPINE_D])))
    return parts


# ── ИГЛОМЁТ ──────────────────────────────────────────────────────────────────────────────────────────
def gun():
    """Четыре ствола веером вперёд, приподняты на 10°: в долях места 0.24 м."""
    parts = []
    for side, yaw in ((-1.5, -13.0), (-0.5, -4.5), (0.5, 4.5), (1.5, 13.0)):
        length, d = 0.42, 0.045
        g = (math.sin(math.radians(yaw)) * math.cos(math.radians(10)), math.sin(math.radians(10)),
             math.cos(math.radians(yaw)) * math.cos(math.radians(10)))
        centre = (side * 0.07, 0.03, 0.12)
        x, yy, z = frame(g, (0.0, 1.0, 0.0), 'y')
        parts.append(dict(block='игла', offset=[round(c / GUN_CAL, 4) for c in centre],
                          scale=[round(d / GUN_CAL, 4), round(length / GUN_CAL, 4), round(d / GUN_CAL, 4)],
                          euler=euler_from_axes(x, yy, z),
                          metres=dict(centre=list(centre), size=[d, length, d])))
    return parts


# ── ЗАДНИЕ СТОПЫ ─────────────────────────────────────────────────────────────────────────────────────
def hind_feet():
    n = next(n for n in G.build_nodes() if n['name'] == 'голень')
    w, h, l = 0.14, 0.09, 0.22
    centre = (G.X_HIND, h * 0.5, n['b'][2] + 0.05)
    foot = _WL.part(n, 'лапа', centre, (w, h, l), (0.0, 0.0, 1.0))
    return dict(slot='Ноги', organ='Ежиные ноги', parts=[foot])


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--out', default=os.path.join(HERE, 'out', 'ezh-organs-layout-draft.json'))
    args = ap.parse_args()
    sp = spines()
    organs = [hind_feet(),
              dict(slot='Шкура', organ='Иглы', parts=sp),
              dict(slot='Игломёт', organ='Игломёт', parts=gun())]
    os.makedirs(os.path.dirname(args.out), exist_ok=True)
    with open(args.out, 'w', encoding='utf-8') as f:
        json.dump(dict(organs=organs), f, ensure_ascii=False, indent=2)
    for o in organs:
        print('%s (%s): кусков %d' % (o['organ'], o['slot'], len(o['parts'])))
    ys = [p['metres']['centre'][1] for p in sp]
    print('иглы: центры по Y %.2f…%.2f' % (min(ys), max(ys)))


if __name__ == '__main__':
    main()
