# -*- coding: utf-8 -*-
"""ГЕОМЕТРИЯ: один примитив на все слои — лофт вдоль оси кости.

ПОЧЕМУ ОДИН ПРИМИТИВ. Спека прототипа требовала «один архитектурный метод» на непохожие части:
если голову приходится лечить приёмами, которых не было у ноги, метод недоделан. Здесь и кость, и
мышца, и покров — это ось плюс профиль радиуса плюс сечение; различаются они ПРОФИЛЕМ, а не кодом.

ПОЧЕМУ НЕ КАПСУЛА. Капсула даёт ровный радиус, и тело из капсул читается «шариками» — этим и
кончилась прошлая парадигма. Профиль вдоль оси — то место, где силуэт вообще появляется:
у кости шейка тоньше суставов, у мышцы брюшко толще сухожилий.
"""
import math
import bpy, bmesh
from mathutils import Vector, Matrix


# ── ПРОФИЛИ РАДИУСА ВДОЛЬ ОСИ. t ∈ [0,1] от начала кости к концу, множитель к линейному r0→r1
def prof_long(t, waist=0.52, w=0.17):
    """ТРУБЧАТАЯ КОСТЬ: два эпифиза и тонкая диафиза между ними. Это и есть «кость», а не палка."""
    bell = max(math.exp(-(t / w) ** 2), math.exp(-((1.0 - t) / w) ** 2))
    return waist + (1.0 - waist) * bell


def prof_blade(t):
    """ПЛОСКАЯ КОСТЬ (лопатка, крыло подвздошной): широкая у основания, сходит на нет к суставу."""
    return 0.35 + 0.65 * (1.0 - t) ** 0.6


def prof_spindle(t):
    """МЫШЦА: сухожилия на концах, брюшко посередине. Смещено к началу — мясо сидит проксимально."""
    return 0.30 + 0.70 * math.sin(math.pi * min(1.0, max(0.0, t)) ** 0.85) ** 0.8


def prof_plain(t):
    return 1.0


def prof_egg(t):
    """ЯЙЦО: округло с обоих концов. Мозговая коробка, круп, любая замкнутая ёмкость.

    Без него торцы лофта плоские, и череп сзади выглядит срезанным ножом — дефект, который на
    рендере в профиль читается мгновенно, а в числах не виден вовсе."""
    return math.sqrt(max(0.05, 1.0 - (2.0 * t - 1.0) ** 2 * 0.55))


def prof_dome(t):
    """Тупой у начала, округлый к концу: морда, палец, рог — всё, что кончается кончиком, а не срезом."""
    return math.sqrt(max(0.05, 1.0 - t ** 3 * 0.92))


PROFILES = {'long': prof_long, 'blade': prof_blade, 'spindle': prof_spindle, 'plain': prof_plain,
            'egg': prof_egg, 'dome': prof_dome}


def _frame(rot3):
    """Матрица кости из списков → оси. Столбцы: локальные X (ширина), Y (ось роста), Z (глубина)."""
    m = Matrix(((rot3[0][0], rot3[0][1], rot3[0][2]),
                (rot3[1][0], rot3[1][1], rot3[1][2]),
                (rot3[2][0], rot3[2][1], rot3[2][2])))
    return m.col[0].normalized(), m.col[1].normalized(), m.col[2].normalized()


def loft(bm, axis_pts, radii, ax, az, section, depth, sides, cap_start=True, cap_end=True):
    """Кольца вдоль оси → оболочка. `ax`/`az` — поперечные оси, сечение эллиптическое.

    ЭЛЛИПС, А НЕ КРУГ: у волка грудь глубокая и узкая (примерно 1:2), ухо плоское по толщине.
    Круглым сечением такое не задать вовсе — тело выйдет бочкой при любых длинах."""
    rings = []
    for p, r in zip(axis_pts, radii):
        ring = []
        for i in range(sides):
            a = 2.0 * math.pi * i / sides
            off = ax * (math.cos(a) * r * section) + az * (math.sin(a) * r * depth)
            ring.append(bm.verts.new(p + off))
        rings.append(ring)
    for r0, r1 in zip(rings[:-1], rings[1:]):
        for i in range(sides):
            j = (i + 1) % sides
            bm.faces.new((r0[i], r0[j], r1[j], r1[i]))
    if cap_start:
        bm.faces.new(tuple(reversed(rings[0])))
    if cap_end:
        bm.faces.new(tuple(rings[-1]))
    return rings


def bone_geo(bm, b, pos, rot, tip, profile='long', segs=None, sides=12, bend=None, grow=1.0):
    """Одна кость в меш. `bend` — вбок от прямой оси (доли длины), так гнутся ребро и хвост.

    `grow` — общий множитель радиуса: одна и та же ось обрастает по-разному в слое кости и в слое
    мышц, и заводить ради этого второй набор чисел было бы ложью — толщина кости и толщина мяса
    вокруг неё это одна анатомия в двух масштабах."""
    p0, p1 = Vector(pos), Vector(tip)
    L = (p1 - p0).length
    if L < 1e-6:
        return
    ax, ay, az = _frame(rot)
    n = segs if segs else max(6, int(L / 0.02))
    f = PROFILES[profile]
    pts, rads = [], []
    for i in range(n + 1):
        t = i / n
        p = p0 + (p1 - p0) * t
        if bend:
            s = math.sin(math.pi * t)                    # прогиб максимален в середине, на суставах ноль
            p = p + (ax * bend[0] + az * bend[1]) * (s * L)
        pts.append(p)
        rads.append((b.r0 + (b.r1 - b.r0) * t) * f(t) * grow)
    loft(bm, pts, rads, ax, az, b.section, b.depth, sides)


def new_mesh(name, coll=None):
    me = bpy.data.meshes.new(name)
    ob = bpy.data.objects.new(name, me)
    (coll or bpy.context.scene.collection).objects.link(ob)
    return ob, me


def finish(ob, me, bm, smooth=True):
    """НОРМАЛИ ПЕРЕСЧИТЫВАЮТСЯ НАРУЖУ — не косметика, а условие работы булевых операций.

    `normal_update` лишь пересчитывает по имеющейся намотке, а намотка у лофта зависит от знака
    сечения и от того, растёт радиус или падает. У резака с нормалями внутрь разность вычитает не
    конус, а ВСЁ ОСТАЛЬНОЕ: череп исчезал целиком, оставляя пару лоскутов, и выглядело это как
    «булево не работает». Ошибки при этом нет нигде."""
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.normal_update()
    bm.to_mesh(me)
    bm.free()
    if smooth:
        for p in me.polygons:
            p.use_smooth = True
    return ob


def shell_geo(bm, stations, p0, rot3, length, sides=24, power=2.05, width=1.0, grow=1.0):
    """ОБОЛОЧКА ПО СТАНЦИЯМ ДВУХ ОРТОГРАФИЙ: профиль даёт верх и низ, вид сверху — ширину.

    Сечение — СУПЕРЭЛЛИПС, а не эллипс: у черепа бока плоские, свод почти прямой, а чистый эллипс
    делает из него дирижабль. Верх и низ считаются от оси РАЗДЕЛЬНО — череп несимметричен по высоте,
    и попытка обойтись одним радиусом ровно этим и кончается: коробка садится ниже свода.

    Чего метод НЕ УМЕЕТ и это надо знать: вогнутостей поперёк взгляда у него нет. Височная яма и
    подглазничная впадина силуэтом не задаются — их вырезает слой `cut`."""
    ax, ay, az = _frame(rot3)
    up = -az                       # у кости, направленной вперёд, локальная Z смотрит ВНИЗ
    rings = []
    for st in stations:
        t, rtop, rbot, half = st[0], st[1], st[2], st[3]
        c = p0 + ay * (t * length)
        hw = max(1e-5, half * length * width * grow)
        hu = max(1e-5, rtop * length * grow)
        hd = max(1e-5, -rbot * length * grow)
        ring = []
        for i in range(sides):
            a = 2.0 * math.pi * i / sides
            ca, sa = math.cos(a), math.sin(a)
            sx = math.copysign(abs(ca) ** (2.0 / power), ca)
            sy = math.copysign(abs(sa) ** (2.0 / power), sa)
            ring.append(bm.verts.new(c + ax * (sx * hw) + up * (sy * (hu if sy >= 0 else hd))))
        rings.append(ring)
    for r0, r1 in zip(rings[:-1], rings[1:]):
        for i in range(sides):
            j = (i + 1) % sides
            bm.faces.new((r0[i], r0[j], r1[j], r1[i]))
    bm.faces.new(tuple(reversed(rings[0])))
    bm.faces.new(tuple(rings[-1]))
