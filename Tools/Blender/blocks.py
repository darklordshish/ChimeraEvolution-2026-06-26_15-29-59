# -*- coding: utf-8 -*-
"""БИБЛИОТЕКА РИГБЛОКОВ — жёсткие детали, которые подменяют куб на месте (контракт `SPEC-model-contract.md` §7.4).

ТРЕБОВАНИЯ, И ПОЧЕМУ ОНИ ТАКИЕ (коротко, полное — в контракте):
  • габарит РОВНО 1×1×1, центр в нуле — `scale` в данных значит габарит куска, блок встаёт вместо куба;
  • кадр блока: +Z вдоль тела, +Y наружу — так сборщик кладёт детали;
  • имя объекта = имя блока — по нему `ShapeCatalog` отдаёт меш;
  • один FBX на библиотеку, плоское затенение, без материалов и UV — цвет даёт код.

Оси Blender → Unity — ЗАМЕРЕНО 17.09, а не взято из памяти. Каталог форм берёт голый `Mesh`, без трансформа
корня, поэтому конверсия осей обязана лежать В ВЕРШИНАХ. Пробный несимметричный меш (+X на 1, +Y на 2, +Z на 3):
  экспорт без Apply Transform  → в меше оси Blender, конверсия уходит поворотом корня ±90° — блок лёг бы набок;
  экспорт С Apply Transform (`bake_space_transform=True`) + в Unity Bake Axis Conversion ВЫКЛ →
      Unity (x, y, z) = (−x, z, −y) Blender, корень без поворота. Это и есть правило для блоков.
(Для моделей с арматурой §8 контракта Apply Transform запрещает — у блока иерархии нет, ломать нечего.)
Блок описывается В КАДРЕ UNITY (x вбок, y наружу, z вдоль) и кладётся в Blender как (−x, −z, y): морда в −Y,
верх в +Z, как у любого зверя в Blender. Геометрию ниже поэтому можно читать прямо в игровых долях.

СЕЧЕНИЯ ВМЕСТО ЛЕПКИ. Каждый блок — «лофт»: кольца сечений вдоль +Z. Кольцо задаётся положением t, шириной,
высотой и сдвигом центра по y — ровно теми числами, что снимаются с референса в профиль и анфас. Мелкой
лепки нет намеренно: блок работает силуэтом в тумане на 20 метрах.

Запуск:  blender -b -P blocks.py
"""
import math
import os
import sys

import bmesh
import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, '..', '..', 'Assets', '_Chimera', 'Models', 'ригблоки.fbx'))

# ── СЕЧЕНИЕ ──────────────────────────────────────────────────────────────────────────────────────────
# Восьмиугольник со срезанными углами: в анфас читается «округлым», а стоит 8 вершин.
# CH — доля полуширины, на которой начинается срез угла (0.5 = ромб, 1 = прямоугольник)
CH = 0.55


def ring(w, h, cy=0.0, ch=CH):
    """Кольцо сечения в долях блока: ширина w, высота h, центр по y."""
    hx, hy = w * 0.5, h * 0.5
    pts = [(hx, hy * ch), (hx * ch, hy), (-hx * ch, hy), (-hx, hy * ch),
           (-hx, -hy * ch), (-hx * ch, -hy), (hx * ch, -hy), (hx, -hy * ch)]
    return [(x, y + cy) for x, y in pts]


def loft(name, rings):
    """rings: [(t, [(x, y)…]), …] вдоль +Z от −0.5 до +0.5. Торцы закрыты."""
    bm = bmesh.new()
    loops = []
    for t, pts in rings:
        # Unity (x, y, z) → Blender (−x, −z, y); экспорт с Apply Transform вернёт ровно Unity-кадр
        loops.append([bm.verts.new((-x, -t, y)) for x, y in pts])
    n = len(loops[0])
    for a, b in zip(loops, loops[1:]):
        for i in range(n):
            j = (i + 1) % n
            bm.faces.new((a[i], a[j], b[j], b[i]))
    bm.faces.new(list(reversed(loops[0])))
    bm.faces.new(loops[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    # ОБХОД — КАК ЕСТЬ, НОРМАЛИ — НЕ ИЗ ФАЙЛА. Экспорт с Apply Transform зеркалит X (правая система → левая):
    # обход граней при этом выходит верным (сверено на кубе-примитиве Unity: у клина все 44 грани наружу), а
    # ВЕКТОРЫ нормалей из файла — нет, и клин светился изнанкой. Поэтому в импорте нормали считаются по граням
    # (`Normals: Calculate`, угол 0 — плоское затенение), а не берутся из FBX
    bmesh.ops.triangulate(bm, faces=bm.faces)

    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = False
    ob = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(ob)
    return ob


def fit_unit(ob):
    """Проверка контракта, а не подгонка: габарит обязан быть 1×1×1 с центром в нуле уже по построению."""
    xs = [v.co.x for v in ob.data.vertices]
    ys = [v.co.y for v in ob.data.vertices]
    zs = [v.co.z for v in ob.data.vertices]
    size = (max(xs) - min(xs), max(ys) - min(ys), max(zs) - min(zs))
    center = ((max(xs) + min(xs)) / 2, (max(ys) + min(ys)) / 2, (max(zs) + min(zs)) / 2)
    bad = [s for s in size if abs(s - 1.0) > 1e-4] + [c for c in center if abs(c) > 1e-4]
    tris = len(ob.data.polygons)
    print('  %-8s габарит %.3f×%.3f×%.3f, центр (%.3f %.3f %.3f), треугольников %d%s' %
          ((ob.name,) + size + center + (tris, '  ← НАРУШЕНИЕ КОНТРАКТА' if bad else '')))
    return not bad


# ── БЛОКИ ────────────────────────────────────────────────────────────────────────────────────────────

def klin():
    """КЛИН — сужающаяся призма: морда зверя, клюв, основание рога.

    Первый носитель — морда волка. Числа сечений сняты с `Anatomy/species/wolf/ref/photo/wolf_standing_1.jpg`
    в кадре головы (`Anatomy/species/wolf/head_layout.py`): от основания в черепе до мочки морда сходит
    по ширине до 0.43 и по высоте до 0.40. Верх (спинка носа) и низ (линия челюсти) сходятся почти
    симметрично, поэтому центр кольца не смещён. Щёки у основания дают небольшую выпуклость — среднее
    кольцо шире прямой линии между торцами."""
    return loft('клин', [
        (-0.50, ring(1.00, 1.00)),
        (-0.10, ring(0.80, 0.76)),
        (+0.50, ring(0.43, 0.40)),
    ])


def brusok():
    """СУЖАЮЩИЙСЯ БРУСОК — пясть и плюсна: почти не гнутся, торчат наружу из туши, значит деталь.

    Широкий торец (−Z) заходит в конец узла ноги на свою толщину — шов перекрыт, а не состыкован. К лапе брусок
    сходит до 0.72 по ширине и 0.80 по глубине: у псовых пясть сплюснута с боков сильнее, чем спереди назад.
    Немного суставной «шишки» у запястья даёт второе кольцо — без неё нога читается палкой."""
    return loft('брусок', [
        (-0.50, ring(1.00, 1.00)),
        (-0.30, ring(0.92, 0.96)),
        (+0.50, ring(0.72, 0.80)),
    ])


def kaplya():
    """КАПЛЯ — лапа, коготь-подушка, глаз, мочка: округлый объём с тупым и острым концом.

    Тупой конец на +Z (пальцы лапы вперёд), острый на −Z (пяточный бугор). Низ приплюснут: центр колец сдвинут
    вверх, чтобы подушка стояла на земле гранью, а не касалась её точкой (гоча посадки из §8 контракта)."""
    return loft('капля', [
        (-0.50, ring(0.30, 0.30, cy=+0.05)),
        (-0.25, ring(0.78, 0.80, cy=+0.02)),
        (+0.15, ring(1.00, 1.00)),
        (+0.42, ring(0.82, 0.70, cy=-0.08)),
        (+0.50, ring(0.46, 0.34, cy=-0.12)),
    ])


BLOCKS = [klin, brusok, kaplya]


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    ok = True
    print('ригблоки → %s' % OUT)
    for make in BLOCKS:
        ok &= fit_unit(make())
    if not ok:
        print('ЭКСПОРТ ОТМЕНЁН: габарит блока не 1×1×1')
        sys.exit(1)

    for ob in bpy.context.scene.objects:
        ob.select_set(True)
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=OUT, use_selection=True, object_types={'MESH'},
                             global_scale=1.0, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                             axis_forward='-Z', axis_up='Y', bake_space_transform=True,
                             mesh_smooth_type='FACE', use_mesh_modifiers=True,
                             add_leaf_bones=False, bake_anim=False, path_mode='AUTO')
    print('готово')


if __name__ == '__main__':
    main()
