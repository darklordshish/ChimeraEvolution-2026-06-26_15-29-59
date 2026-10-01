# -*- coding: utf-8 -*-
"""ПЛЕЧО C ЭКСПЕРИМЕНТА КОНСИЛИУМА (итог §5, геометр §8.2): 3D-образец с листа → OBJ в осях стенда, выровненный по
нашему виду, — дальше тот же `decimate.py` до бюджета вида и тот же кадр (`ShotObj.cs`).

Выравнивание: длинная горизонтальная ось образца → Z стенда, нос — к +Z (у зверя голова выше хвоста: какой конец
длинной оси выше, тот и нос; у человека длинная ось — вертикаль, тогда перёд — по сырому выходу генератора, -Y Blender),
рост (по Y) — как у нашего вида, низ на земле, центр по X и Z — как у нашего вида. Форма образца не подгоняется: это и
есть «форма листа» на том же бюджете.

Запуск (Blender):  blender -b -P obrazec_v_obj.py -- образец.glb наш_вид.obj [ещё.obj …] выход.obj
  наш_вид.obj — выгрузка стенда (`ExportObj.cs`), по ней берутся рост и центр.
"""
import sys
import bpy
import numpy as np

a = sys.argv[sys.argv.index('--') + 1:]
glb, refs, dst = a[0], a[1:-1], a[-1]


def obj_points(path):
    return np.array([[float(t) for t in l.split()[1:4]] for l in open(path, encoding='utf-8') if l.startswith('v ')])


ref = np.vstack([obj_points(p) for p in refs])           # оси OBJ стенда: X (зеркало Unity), Y вверх, Z — к носу
rmin, rmax = ref.min(0), ref.max(0)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=glb)
pts, faces, base = [], [], 0
for o in [o for o in bpy.context.scene.objects if o.type == 'MESH']:
    M = o.matrix_world
    pts += [tuple(M @ v.co) for v in o.data.vertices]
    faces += [[base + i for i in p.vertices] for p in o.data.polygons]
    base += len(o.data.vertices)
P = np.array(pts)                                       # Blender: Z вверх, перёд образца -Y
S = np.column_stack([P[:, 0], P[:, 2], -P[:, 1]])       # → оси стенда: Y вверх, Z — перёд генератора

ext = S.max(0) - S.min(0)
if ext[0] > ext[2] * 1.15:                              # зверь лёг вдоль X — повернуть на 90° вокруг Y
    S = np.column_stack([-S[:, 2], S[:, 1], S[:, 0]])
zs = S[:, 2]; lo, hi = zs.min(), zs.max(); L = hi - lo
if ext.max() == ext[1]:
    pass                                                # стоящий (человек): перёд оставляем как дал генератор
elif S[zs > hi - 0.15 * L, 1].max() < S[zs < lo + 0.15 * L, 1].max():
    S = np.column_stack([-S[:, 0], S[:, 1], -S[:, 2]])  # выше задний конец — значит, нос сзади: разворот на 180°

k = (rmax[1] - rmin[1]) / (S[:, 1].max() - S[:, 1].min())
S = S * k
S[:, 1] -= S[:, 1].min() - rmin[1]
for i in (0, 2):
    S[:, i] += (rmin[i] + rmax[i]) / 2 - (S[:, i].min() + S[:, i].max()) / 2

with open(dst, 'w', encoding='utf-8') as f:
    f.write('o field\n')
    for v in S:
        f.write('v %.5f %.5f %.5f\n' % tuple(v))
    for p in faces:
        f.write('f %s\n' % ' '.join(str(i + 1) for i in p))
print('ОБРАЗЕЦ: %d вершин, %d граней, масштаб %.3f, габарит %s' % (len(S), len(faces), k, np.round(S.max(0) - S.min(0), 3)))
print('НАШ ВИД: габарит %s' % np.round(rmax - rmin, 3))
