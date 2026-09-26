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


# ШЕЯ И ГОЛОВА — ТОЖЕ ПОД ИГЛАМИ (Хеджхалк 25.09): на листе морда выглядывает из колючего капюшона, иглы с загривка и темени
# ПЛЕЧИ И ГРУДЬ — ТОЖЕ ПОД ИГЛАМИ (критик r3: в анфас плечи и бока шеи ниже челюсти были голыми «валунами»)
BODY_NODES = [n for n in G.build_nodes() if n['name'] in ('хребет', 'козырёк', 'шея', 'голова', 'плечо', 'грудь')]
# ЗЕРКАЛЬНЫЕ УЗЛЫ (`mirror`) в графе описаны правой стороной — левую копию для поиска поверхности добавить явно, иначе
# левое плечо без игл (критик r4: асимметрия анфаса на 10 см)
BODY_NODES += [dict(n, a=(-n['a'][0],) + tuple(n['a'][1:]), b=(-n['b'][0],) + tuple(n['b'][1:]))
               for n in BODY_NODES if n.get('mirror')]


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
SPINE_LEN_SIDE = 0.22   # игла на боку; на спине — SPINE_LEN
LON_MAX = 130            # долгота сетки игл от хребта, градусы (было 90)
TILT_TOP, TILT_SIDE = 40.0, 58.0   # завал от нормали на спине и на боку, градусы
FLOW_DOWN = 0.45                   # на боку поток идёт назад и вниз (≈ 25° ниже горизонтали)
FRONT_FROM = 0.30                  # вперёд от этой Z (шея, голова) иглы короче и лежат плотнее
DOME_PEAK_Z = -0.05                # пик купола — над серединой спины (было — загривок)
# ИГЛЫ ХЕДЖХАЛКА (25.09): длина на спине 0.42 (на боку SPINE_LEN_SIDE), основание 0.11 — конусы перекрываются в шубу,
# завал — TILT_TOP/TILT_SIDE ниже. Было 0.17 × 0.05, завал 62° (природный ёж)
SPINE_LEN, SPINE_D, SPINE_EMBED = 0.42, 0.11, 0.08
DOME_CENTRE = (0.0, 0.47, -0.02)                    # центр туши Хеджхалка (было 0.36, −0.05 — купол)
LATS = tuple(range(-84, 85, 11))                       # градусы: минус — к хвосту (сетка шуба; было 15 — редко, 8 — 460 игл, дорого)
SKIRT_Y_FRONT, CHEST_OPEN, CROWN_Z = 0.24, 0.08, 0.64
SKIRT_Y = 0.30                                         # ниже — лапы без игл; по бокам иглы почти до локтей (было 0.20 — юбка до земли, 0.30)
FACE = dict(z=0.66, y=0.58)                            # впереди и ниже — открытая морда; щёки в иглах — колючий капюшон (было 0.30/0.50, 0.46/0.40)
# УШИ ОТКРЫТЫ (25.09): вокруг уха игл нет — прежде уши тонули в иглах. Мир, м: ухо по раскладке головы
EAR_POS, EAR_CLEAR = (0.12, 0.76, 0.64), 0.08
REAR_Y = 0.62                                          # ниже — задний торец без игл; выше иглы стекают с крупа назад
FRONT_Z = 0.78                                         # иглы до темени (было 0.40 — кончик козырька)


def spine_directions():
    for i, lat in enumerate(LATS):
        step = 14 if abs(lat) < 60 else 28             # у полюсов сетки кольцо короче — реже, иначе иглы в пучке
        # ДОЛГОТА НИЖЕ ГОРИЗОНТАЛИ (25.09): до 90° сетка покрывала лишь верхнюю половину туши — низ боков был лысым по
        # построению. У Хеджхалка иглы спускаются почти до брюха; брюхо и лапы отсекает SKIRT_Y
        lons = range(0, LON_MAX + 1, step) if i % 2 == 0 else range(step // 2, LON_MAX + 1, step)
        for lon in sorted({l for x in lons for l in (x, -x)}):
            la, lo = math.radians(lat), math.radians(lon)
            yield lat, lon, v_unit((math.sin(lo) * math.cos(la), math.cos(lo) * math.cos(la), math.sin(la)))


def spines():
    skin_centre = v_add(G.SPINE_A, v_mul(v_unit(v_sub(G.SPINE_B, G.SPINE_A)), 0.5 * SKIN_CAL[2]))
    parts = []
    for lat, lon, d in spine_directions():
        p = surface(DOME_CENTRE, d)
        skirt = SKIRT_Y_FRONT if p[2] > 0.10 else SKIRT_Y            # спереди мантия спускается до локтей
        if p[1] < skirt or (p[2] > FACE['z'] and p[1] < FACE['y']) or p[2] > FRONT_Z:
            continue
        if abs(p[0]) < CHEST_OPEN and p[2] > 0.20 and p[1] < 0.45:       # грудь по центру открыта клином
            continue
        if p[2] > CROWN_Z and p[1] > 0.62 and abs(p[0]) < 0.10:         # лоб без игл: ряд темени начинается за ушами («рог», r3)
            continue
        if any(v_len(v_sub(p, (sx * EAR_POS[0], EAR_POS[1], EAR_POS[2]))) < EAR_CLEAR for sx in (1, -1)):
            continue
        n = normal(p)
        if n[2] < -0.55 and p[1] < REAR_Y:              # ЗАД БЕЗ ИГЛ (геймдизайнер 25.09: «иглы из зада вытащишь?»)
            continue
        # КУПОЛ, ЗАЧЁСАННЫЙ НАЗАД (критик r1, 25.09): иглы торчали звездой во все стороны и закрывали морду и грудь, линия
        # верха была клином с пиком на загривке. Теперь направление — ПОТОК по обводу тела: на спине назад-вверх, на боках
        # назад-вниз, на заду вниз (иглы свисают юбкой, а не торчат щёткой). Завал от нормали — от TILT_TOP (спина) до
        # TILT_SIDE (бока); на шее, щеках и груди иглы короткие и лежат почти вплотную назад
        side = max(0.0, 1.0 - max(0.0, n[1]))                       # 0 — спина, 1 — бок и ниже
        flow = (0.0, -FLOW_DOWN * side, -1.0)
        if n[2] < -0.3:                                              # зад: свисают вниз
            flow = (0.0, -1.0, -0.35)
        back = v_sub(flow, v_mul(n, v_dot(flow, n)))
        if v_len(back) < 0.2:
            back = v_sub((0.0, -1.0, 0.0), v_mul(n, v_dot((0.0, -1.0, 0.0), n)))
        back = v_unit(back)
        front = max(0.0, min(1.0, (p[2] - FRONT_FROM) / 0.25))       # 0 — туша, 1 — шея и голова
        tilt = TILT_TOP + (TILT_SIDE - TILT_TOP) * side + 8.0 * math.sin(lat * 3.7 + lon * 1.3)
        if front > 0.0:                                          # шея и голова: иглы стоят в контуре (критик r2: при +18° пропадали)
            tilt = min(tilt, 40.0) if n[1] > 0.3 else 48.0   # щёки и шея 48°: при ≥65 ложились вдоль кожи (r4)   # темя стоит, щёки и бока шеи лежат назад
        t = math.radians(min(tilt, 80.0))
        grow = v_unit(v_add(v_mul(n, math.cos(t)), v_mul(back, math.sin(t))))
        base = v_sub(p, v_mul(grow, SPINE_EMBED * (0.5 + 0.5 * max(0.0, n[1]))))
        # ДЛИНА — КУПОЛ: максимум над серединой спины (Z DOME_PEAK_Z), к голове и крупу короче; на боках короче, чем
        # на спине; спереди (шея, щёки, грудь) — 35 %. Плюс разнобой ±15 %
        length = SPINE_LEN_SIDE + (SPINE_LEN - SPINE_LEN_SIDE) * max(0.0, n[1]) ** 1.5
        dome = max(0.55, 1.25 - 1.3 * ((p[2] - DOME_PEAK_Z) / 0.55) ** 2)
        jitter = 1.0 + 0.15 * math.sin(lat * 12.9898 + lon * 78.233)
        # ШАПКА И ГРИВА (критик r2): темя и загривок ×0.9–1.0, щёки ×0.55 — иглы начинаются сразу за надбровьем
        length *= dome * jitter * (1.0 if n[1] > 0.3 else 1.0 - 0.45 * front)
        if front > 0.0:
            length = max(length, 0.24 if n[1] > 0.3 else 0.14)
        if n[1] < 0.15 and p[2] > 0.15:                               # грудь и низ шеи: коротко, чтобы морда и грудь открывались
            length *= 0.45
        d = SPINE_D * max(0.5, min(1.0, length / 0.36))             # короткая игла — тоньше: иначе на голове «рога» (r3)
        centre = v_add(base, v_mul(grow, length / 2))
        x, yy, z = frame(grow, (1.0, 0.0, 0.0) if abs(grow[0]) < 0.9 else (0.0, 0.0, 1.0), 'y')
        rel = v_sub(centre, skin_centre)
        parts.append(dict(block='игла',
                          offset=[round(rel[k] / SKIN_CAL[k], 4) for k in range(3)],
                          scale=[round(d / SKIN_CAL[0], 4), round(length / SKIN_CAL[1], 4), round(d / SKIN_CAL[2], 4)],
                          euler=euler_from_axes(x, yy, z), note='широта %d, долгота %d' % (lat, lon),
                          metres=dict(centre=[round(c, 3) for c in centre], size=[round(d, 3), round(length, 3), round(d, 3)])))
    return parts


# ── ИГЛОМЁТ ──────────────────────────────────────────────────────────────────────────────────────────
# ГРЕБЕНЬ ТРУБОК — ЭТО ИГЛОМЁТ (решение геймдизайнера 25.09): трубки гребнем на спине — оружие, как на листе Хеджхалка
# («spines on back are ranged attack»). Привитый другому, игломёт принесёт гребень — по контуру видно, что химера стреляет.
# Залп летит по прицелу (`QuillVolley`: направление от цели), а не по стволам, поэтому трубки можно валить назад.
# Прежний игломёт — 4 ствола веером вперёд над шеей: в профиль читался рогом.
# ГРЕБЕНЬ НА ЗАГРИВКЕ И СМОТРИТ ВВЕРХ-ВПЕРЁД (геймдизайнер 25.09: «он стреляет иглами вперёд, поэтому игломёт на загривке»):
# по контуру видно, куда бьёт. Итерации 1–2 ставили гребень вдоль спины с завалом назад, как на листе в покое.
# МЕСТО `Игломёт` в бутстрапе стоит над шеей — так и задумано: замер стендом 25.09 — начало (0, 0.640, 0.406) м, поворот места −10° по X.
# Трубки ставятся в МИРЕ вдоль хребта и пересчитываются в кадр места. Переедет место — пересчитать GUN_ORIGIN.
GUN_ORIGIN, GUN_PITCH = (0.0, 0.661, 0.325), -10.0   # перемерено 25.09 под граф Хеджхалка (было 0.640, 0.406)
CREST_Z = (0.08, 0.44)             # загривок: от лопаток к шее
CREST_N, CREST_D = 18, 0.060      # итерация 2: 13 спиц по 4.5 см читались узким «ирокезом»


def rx(deg, v):
    t = math.radians(deg)
    x, y, z = v
    return (x, y * math.cos(t) - z * math.sin(t), y * math.sin(t) + z * math.cos(t))


def to_gun(p):
    """Мир → кадр места игломёта: R⁻¹·(p − начало), R — поворот места Euler(−10, 0, 0)."""
    return rx(-GUN_PITCH, v_sub(p, GUN_ORIGIN))


def gun():
    parts = []
    for i in range(CREST_N):
        f = i / (CREST_N - 1)                               # 0 — за лопатками, 1 — у шеи
        z = CREST_Z[0] + f * (CREST_Z[1] - CREST_Z[0])
        side = (-1) ** i * (0.0 if i % 3 == 0 else 0.07)   # три ряда: средний и два боковых вразбежку
        length = 0.44 + 0.18 * math.sin(math.pi * min(1.0, 0.2 + 0.8 * f))            # длиннее к середине загривка; над шубой (было 0.28 + 0.14 — терялись)
        pitch = math.radians(-(12 + 22 * f))                # наклон ВПЕРЁД: у шеи сильнее — стволы смотрят туда, куда залп
        splay = math.radians(26 * (1 if side > 0 else -1 if side < 0 else 0))   # веер шире (было 14°)
        g = (math.sin(splay), math.cos(pitch) * math.cos(splay), -math.sin(pitch) * math.cos(splay))
        top = surface(DOME_CENTRE, v_unit(v_sub((side, 0.9, z), DOME_CENTRE)))
        base = v_sub(top, v_mul(g, 0.06))                   # основание утоплено в тушу на 6 см
        centre = v_add(base, v_mul(g, length / 2))
        c, gl = to_gun(centre), rx(-GUN_PITCH, g)
        x, yy, zz = frame(gl, (1.0, 0.0, 0.0), 'y')
        parts.append(dict(block='игла', offset=[round(v / GUN_CAL, 4) for v in c],
                          scale=[round(CREST_D / GUN_CAL, 4), round(length / GUN_CAL, 4), round(CREST_D / GUN_CAL, 4)],
                          euler=euler_from_axes(x, yy, zz), note='трубка гребня %d' % i,
                          metres=dict(centre=[round(v, 3) for v in centre], size=[CREST_D, round(length, 3), CREST_D])))
    return parts


# ── ЗАДНИЕ СТОПЫ ─────────────────────────────────────────────────────────────────────────────────────
def paw(node_name, heel, toe_z, width, height, claws):
    """КОНЕЦ КОНЕЧНОСТИ — аугмент (П4 спеки «место — плейсхолдер», 26.09): лапа блоком `лапа` на конце узла шасси и когти
    `клин` у пальцев. Ёж стопоходящий: подошва на земле от пятки/запястья до пальцев, пальцы вперёд, когти вниз-вперёд
    (лист: крупные когти на всех четырёх лапах). heel — (x, z) начала лапы, toe_z — кончик пальцев."""
    n = next(x for x in G.build_nodes() if x['name'] == node_name)
    x, z0 = heel
    length = toe_z - z0
    parts = [_WL.part(n, 'лапа', (x, height * 0.5, z0 + length * 0.5), (width, height, length), (0.0, 0.0, 1.0))]
    for i in range(claws):
        dx = (i - (claws - 1) / 2) * width * 0.26
        parts.append(_WL.part(n, 'клин', (x + dx, 0.022, toe_z + 0.025), (0.022, 0.024, 0.075), (0.0, -0.45, 1.0)))
    return parts


def fore_paws():
    """ПЕРЕДНИЕ ЛАПЫ — орган «Ежиные лапы» на гнезде `Руки` (решение геймдизайнера 26.09): широкая кисть-веер и 4 когтя."""
    n = next(x for x in G.build_nodes() if x['name'] == 'предплечье')
    return dict(slot='Руки', organ='Ежиные лапы', parts=paw('предплечье', (n['b'][0], n['b'][2] - 0.06), n['b'][2] + 0.12,
                                                           0.15, 0.10, 4))


def hind_feet():
    """ЗАДНИЕ СТОПЫ — орган «Ежиные ноги» на гнезде `Ноги`: стопа от пятки вперёд и 4 когтя."""
    n = next(x for x in G.build_nodes() if x['name'] == 'голень')
    return dict(slot='Ноги', organ='Ежиные ноги', parts=paw('голень', (n['b'][0], n['b'][2] - 0.06), n['b'][2] + 0.16,
                                                           0.14, 0.10, 4))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--out', default=os.path.join(HERE, 'out', 'ezh-organs-layout-draft.json'))
    args = ap.parse_args()
    sp = spines()
    organs = [fore_paws(), hind_feet(),
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
