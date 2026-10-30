"""МОРДА КЛИНОМ (30.10, критик новой ст.2 n5: «морда в 3/4 спереди коробчатая — передняя треть по ширине почти равна скулам, у листа
клин, сужающийся к носу ~на треть»). Шаг в покое: вершины куска `--kusok` с главной костью `голова` впереди `--y0` (лицо — к −y) сводятся
к средней линии по x в (1 − `--k`·t) раз, t — от 0 у `--y0` до 1 у мочки (самая передняя точка головы), плавно (smoothstep).

  blender -b --factory-startup -P morda_uzhe.py -- вход.blend выход.blend [--kusok @Волк] [--y0 -0.16] [--k 0.2]
"""
import sys
import bpy
import numpy as np
from mathutils import Vector

a = sys.argv[sys.argv.index('--') + 1:]
src, out = a[0], a[1]
opt = dict(kusok='@Волк', y0='-0.16', k='0.2')
i = 2
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
bpy.ops.wm.open_mainfile(filepath=src)
o = bpy.data.objects[opt['kusok']]; M = o.matrix_world; Mi = M.inverted(); gn = {g.index: g.name for g in o.vertex_groups}
ids = [v.index for v in o.data.vertices if v.groups and gn[max(v.groups, key=lambda g: g.weight).group] == 'голова']
P = np.array([list(M @ o.data.vertices[j].co) for j in ids]); Y0 = float(opt['y0']); YT = float(P[:, 1].min()); K = float(opt['k'])
n = 0
for j, p in zip(ids, P):
    if p[1] >= Y0: continue
    t = min(1.0, (Y0 - p[1]) / (Y0 - YT)); t = t * t * (3 - 2 * t)
    o.data.vertices[j].co = Mi @ Vector((p[0] * (1 - K * t), p[1], p[2])); n += 1
o.data.update()
print('МОРДА-УЖЕ: %s — вершин %d впереди y %.2f (мочка %.3f), к мочке ×%.2f по ширине' % (o.name, n, Y0, YT, 1 - K))
bpy.ops.wm.save_as_mainfile(filepath=out)
