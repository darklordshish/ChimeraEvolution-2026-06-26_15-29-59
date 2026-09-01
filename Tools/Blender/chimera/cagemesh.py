# -*- coding: utf-8 -*-
"""МЕШ ИЗ КЛЕТКИ: таблица сечений → оболочка слота.

Здесь кончается путь «референс → точки → кости → мышцы → отливка → таблица» и начинается модель.
Геометрия строится ТОЛЬКО из таблицы: ни поля, ни вокселей на этом шаге уже нет. Оттого меш химеры
получается даром — смешал таблицы, построил тем же кодом.

ЧТО ДЕЛАЕТ ЛОКАЛЬНОСТЬ ЧИТАЕМОЙ. Слот — отдельная оболочка со своим кольцом на стыке, поэтому
привитый орган меняет форму СВОЕГО места и места-родителя, а не всего зверя (решение пользователя:
«химера может и должна быть локальноузнаваемой, иначе будет нечитаемой»).
"""
import math
import bpy
import bmesh
from mathutils import Vector

from .build import to_b
from .cage import frames, PAIRED

MIN_R = 0.004       # тоньше не рисуем: вырожденное кольцо схлопывается в точку и даёт дыру
BURY = 1.6          # насколько глубоко корень слота уходит в родителя, в радиусах своего кольца


def _ring(center, up, side, radii):
    n = len(radii)
    out = []
    for j, r in enumerate(radii):
        a = 2.0 * math.pi * j / n
        d = [up[k] * math.cos(a) + side[k] * math.sin(a) for k in range(3)]
        out.append(tuple(center[k] + d[k] * r for k in range(3)))
    return out


def _clean(c):
    """Выбросить вырожденные станции, подтянуть одиночные нули.

    РАДИУС 0 ЗНАЧИТ РАЗНОЕ, и различать обязательно. Если пуст ВЕСЬ круг или почти весь — это
    корневая станция: плоть там принадлежит соседу (у хвоста крестцу, у ноги тазу), и слот попросту
    начинается дальше. Если пусты один-два сектора — это дыра в форме (под мордой, где отдельная
    челюсть), и кольцо надо оставить, подтянув нули к соседям, иначе оболочка схлопнется в точку и
    даст видимую глазом прореху."""
    rows = []
    for (u, radii), ctr, dr in zip(c['stations'], c['centers'], c['dirs']):
        live = [r for r in radii if r > 1e-6]
        if len(live) * 2 < len(radii):
            continue
        avg = sum(live) / len(live)
        rows.append((u, [max(r, MIN_R, 0.30 * avg) if r < 1e-6 else r for r in radii], ctr, dr))
    return rows


def _bury(first, second, sink):
    """Лишнее кольцо, УТОПЛЕННОЕ В РОДИТЕЛЕ: так закрывается стык.

    ЧАСТИ НЕ СШИВАЮТСЯ ОБЩИМ КОЛЬЦОМ, А ВХОДЯТ ДРУГ В ДРУГА. Попытка сшить была: корневое кольцо
    мерилось по объединению «свой слот плюс родитель», и хвост получил в основании радиус 0.600 —
    луч прошёл торс насквозь. Оно и понятно: хвост не срастается с крупом одним диаметром, он из
    крупа ТОРЧИТ. Диаметры соседей на стыке разные по существу, и общее кольцо им врёт.

    Погружение же честно: слот кончается там, где кончается его плоть, а щели нет, потому что его
    начало лежит внутри соседа. Для низкополигонального стиля это ещё и дешевле шва — ни одной
    лишней вершины на стыке, кроме одного кольца."""
    u, radii, ctr, dr = first
    step = math.sqrt(sum((second[2][k] - ctr[k]) ** 2 for k in range(3)))
    back = min(BURY * sum(radii) / len(radii), 0.9 * step, sink)
    return (u, radii, tuple(ctr[k] - dr[k] * back for k in range(3)), dr)


def shell(slot, c, mirror=False):
    """Оболочка одного слота: кольца по станциям, четырёхугольники между ними, торцы шапками."""
    rows = _clean(c)
    if len(rows) < 2:
        return None
    rows = [_bury(rows[0], rows[1], c.get('sink', 9.0))] + rows
    n = c['n']
    verts, faces = [], []
    # КАДР ВОССТАНАВЛИВАЕТСЯ ТЕМ ЖЕ ПЕРЕНОСОМ, что и при обмере, — иначе кольца сядут повёрнутыми
    # относительно чисел, и оболочка выйдет перекрученной при совершенно верной таблице
    for (u, radii, ctr, dr), (up, side) in zip(rows, frames([r[3] for r in rows])):
        if mirror:
            ctr = (-ctr[0], ctr[1], ctr[2])
            up, side = (-up[0], up[1], up[2]), (-side[0], side[1], side[2])
        verts += _ring(ctr, up, side, radii)
    for i in range(len(rows) - 1):
        a, b = i * n, (i + 1) * n
        for j in range(n):
            k = (j + 1) % n
            faces.append((a + j, a + k, b + k, b + j))
    # ТОРЦЫ — КОНУСАМИ, А НЕ ПЛОСКИМИ ВЕЕРАМИ. Вершина отодвигается на вылет плоти за станцию, и
    # это не стоит ни одного лишнего полигона: у веера вершина всё равно есть, вопрос только в том,
    # где она стоит. Плоский вариант давал груди вертикальную плиту, а морде — обрубок.
    #     Корневой торец вылета не получает: он и так утоплен в родителе, и высовывать его наружу
    # значило бы выпихнуть плечо из груди.
    caps = c.get('caps', (0.0, 0.0))
    for base, rev, out in ((0, True, 0.0), ((len(rows) - 1) * n, False, caps[1])):
        mid = len(verts)
        d = rows[-1][3] if not rev else rows[0][3]
        verts.append(tuple(sum(verts[base + j][k] for j in range(n)) / n + d[k] * out for k in range(3)))
        for j in range(n):
            t = (base + j, base + (j + 1) % n, mid)
            faces.append(t[::-1] if rev else t)

    me = bpy.data.meshes.new(slot)
    me.from_pydata([to_b(v) for v in verts], [], faces)
    me.validate()
    ob = bpy.data.objects.new(slot + ('←' if mirror else ''), me)
    bpy.context.collection.objects.link(ob)
    bm = bmesh.new(); bm.from_mesh(me)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(me); bm.free()
    me.shade_smooth()
    return ob


def build(cages):
    """Все слоты вида. Парные — правый и его зеркало: мерился один, второй получается отражением."""
    objs = []
    for slot, c in cages.items():
        for mir in ((False, True) if slot in PAIRED else (False,)):
            ob = shell(slot, c, mir)
            if ob is not None:
                objs.append(ob)
    return objs
