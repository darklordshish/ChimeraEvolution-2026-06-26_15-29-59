# -*- coding: utf-8 -*-
"""ТОЧКА ВХОДА. Запуск:

    blender -b -P make.py -- --species wolf --layer 1 --views profile,front,head

`--layer` повторяет `SpeciesSO.buildLayers`: 1 кости, 2 +мышцы, 3 +признаки, 4 всё. Слои разведены
не для красоты отчёта: пока смотришь всегда на итог, правится симптом, и каждая правка силуэта ломает
анатомию. Слой не начинается, пока предыдущий не принят.
"""
import sys, os, argparse, importlib

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
AP = argparse.ArgumentParser()
AP.add_argument('--species', default='wolf')
AP.add_argument('--layer', type=int, default=1)
AP.add_argument('--cage', action='store_true', help='строить ОБОЛОЧКУ ПО КЛЕТКЕ, а не поле по костям')
AP.add_argument('--views', default='profile,front,head')
AP.add_argument('--out', default=os.path.join(HERE, 'out'))
AP.add_argument('--res', type=int, default=1500)
AP.add_argument('--slots', action='store_true', help='красить по слотам (диагностика)')
AP.add_argument('--cell', type=float, default=0.020, help='шаг вокселя покрова, м')
AP.add_argument('--fur', type=float, default=0.010, help='ость поверх тела, м')
AP.add_argument('--close', type=float, default=0.032, help='радиус замыкания («катящийся шар»), м')
AP.add_argument('--fbx', action='store_true', help='экспортировать FBX со скиннингом')
AP.add_argument('--blend', action='store_true', help='сохранить .blend рядом с превью')
A = AP.parse_args(argv)

from chimera import build
from chimera.skel import from_points, SKELETON, MUSCLE, FEATURE, CUT

sp = importlib.import_module('species.' + A.species)
bones, placed, warn = from_points(sp.DEFS, sp.P)

LAYERS = {1: (SKELETON,), 2: (SKELETON, MUSCLE), 3: (SKELETON, MUSCLE, FEATURE),
          4: (SKELETON, MUSCLE, FEATURE, CUT)}[A.layer]
KIND = {1: 'кости', 2: 'мышцы', 3: 'признаки', 4: 'покров'}[A.layer]

build.clear()
rig = build.armature(bones, placed)
if A.cage:
    # ОБОЛОЧКА ПО ТАБЛИЦЕ. Скелет остаётся ригом: клетка описывает форму, кости её носят.
    from chimera import cagemesh
    objs = cagemesh.build(importlib.import_module('species.%s_cage' % A.species).CAGES)
    KIND = 'клетка'
else:
    objs = build.geometry(bones, placed, LAYERS)
# СЛОЙ 3 — ЭТО НЕ «ещё объёмы», А ОБТЯЖКА. Кости и мышцы дают поле, покров даёт по нему поверхность
if A.layer >= 3 and not A.cage:
    objs = build.hide(objs, cell=A.cell, fur=A.fur, close=A.close)
build.paint_slots(objs) if A.slots else build.paint(objs, 'кости' if A.layer == 1 else KIND)

os.makedirs(A.out, exist_ok=True)
made = []
for v in A.views.split(','):
    v = v.strip()
    if not v:
        continue
    p = os.path.join(A.out, '%s_%s_%s%s.png' % (A.species, 'cage' if A.cage else 'L%d' % A.layer, v, '_slots' if A.slots else ''))
    build.render(p, view=v, res=A.res, W=getattr(sp, 'W', None), centre=build.bounds(objs))
    made.append(p)

if A.fbx:
    build.skin(objs, rig)
    dst = os.path.join(os.path.dirname(os.path.dirname(HERE)),
                       'Assets', '_Chimera', 'Models', '%s_L%d.fbx' % (A.species, A.layer))
    build.export_fbx(dst, objs, rig)
    print('FBX:', dst)

if A.blend:
    import bpy
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(A.out, '%s_L%d.blend' % (A.species, A.layer)))

tri = sum(len(o.data.polygons) for o in objs)
print('\nСОБРАНО: слой %d (%s) · частей %d · полигонов %d' % (A.layer, KIND, len(objs), tri))
for o in objs:
    print('   %-10s %5d полигонов' % (o.name, len(o.data.polygons)))
print('ПРЕВЬЮ:')
for p in made:
    print('   ' + p)
