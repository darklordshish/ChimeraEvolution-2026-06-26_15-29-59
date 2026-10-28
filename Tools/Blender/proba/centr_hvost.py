"""ХВОСТ НА ОСЬ КРЕСТЦА (28.10, критик r2 п.5: «ст.1, хвост сзади сидит не по центру крестца, на ~3 % ширины таза левее —
читается грязным пятном на ягодице»). Замер: корень хвоста ст.1 почти на средней линии (x 1.2 см), а сам хвост уведён вбок
(среднее по x 9.6 см) — хвост цели ст.1 смотрит в сторону. Кусок поворачивается вокруг вертикали через центр корня (вершины ближе
`--koren` к телу ключа `--imya`) так, чтобы центр хвоста лёг в плоскость x = центр корня, и сдвигается по x на ось (x = 0).

  blender -b --factory-startup -P centr_hvost.py -- вход.blend выход.blend [--kusok @Волк1] [--imya Волк1] [--koren 0.02]
"""
import sys, math
import bpy
import numpy as np
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

a = sys.argv[sys.argv.index('--') + 1:]
src, out = a[0], a[1]
opt = dict(kusok='@Волк1', imya='Волк1', koren='0.02')
i = 2
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
bpy.ops.wm.open_mainfile(filepath=src)
T, F = [], []
for b in bpy.data.objects:
    if b.type != 'MESH' or '@' in b.name or not b.data.shape_keys: continue
    kb = b.data.shape_keys.key_blocks.get(opt['imya'])
    if kb is None: continue
    n = len(T); T += [b.matrix_world @ d.co for d in kb.data]; F += [[n + v for v in p.vertices] for p in b.data.polygons]
bv = BVHTree.FromPolygons(T, F)
o = bpy.data.objects[opt['kusok']]; M = o.matrix_world; Mi = M.inverted()
P = np.array([list(M @ v.co) for v in o.data.vertices])
d = np.array([bv.find_nearest(Vector(p))[3] for p in P])
c = P[d < float(opt['koren'])].mean(0); s = P.mean(0)
# угол поворота вокруг z через c: вектор c→s в плоскости xy повернуть так, чтобы его x стал 0 (хвост смотрит назад, +y)
v = s[:2] - c[:2]; ug = math.atan2(v[0], v[1])          # от +y к +x
R = Matrix.Rotation(ug, 4, 'Z')
for k, vv in enumerate(o.data.vertices):
    p = Vector(P[k] - c); p = R @ p; p = p + Vector(c); p.x -= c[0]; vv.co = Mi @ p
o.data.update()
P2 = np.array([list(M @ v.co) for v in o.data.vertices])
print('ХВОСТ-ОСЬ: %s — корень x %.1f → 0 см, поворот %.1f°, центр хвоста x %.1f → %.1f см' % (
    o.name, 100 * c[0], math.degrees(ug), 100 * s[0], 100 * P2.mean(0)[0]))
bpy.ops.wm.save_as_mainfile(filepath=out)
