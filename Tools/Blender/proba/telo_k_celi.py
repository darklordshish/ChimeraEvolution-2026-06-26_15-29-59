"""ТЕЛО КЛЮЧА К ЦЕЛИ НАД ШВОМ КУСКА (29.10, критик новой ст.2 п.1: «ботфорт — на шве посреди бедра верх лапы шире бедра над ним на
10–12 %, ступенька по окружности»). Кусок лапы — сама цель, а тело ключа подгонкой до цели на бедре не доходит (95 % — 4.6 см):
на шве толстая цель встречает тонкое тело. Здесь вершины тела ключа `--imya` с главной костью из `--kosti` в полосе от шва `--z0`
(высота покоя) вверх на `--pol` м доводятся до ближайшей точки цели в покое (`цель.obj`, файл `--cel_pokoj` подгонки) — только наружу
(тело внутри цели), с весом от 1 у шва до 0 на краю полосы (smoothstep); ниже шва — полностью (тело там под куском).

  blender -b --factory-startup -P telo_k_celi.py -- вход.blend выход.blend цель.obj [--imya Волк] [--kosti бедро,бедро.L] [--z0 0.72] [--pol 0.15]
"""
import sys
import bpy
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

a = sys.argv[sys.argv.index('--') + 1:]
src, out, cel = a[0], a[1], a[2]
opt = dict(imya='Волк', kosti='бедро,бедро.L', z0='0.72', pol='0.15')
i = 3
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
KO = set(opt['kosti'].split(',')); Z0, POL = float(opt['z0']), float(opt['pol'])
bpy.ops.wm.open_mainfile(filepath=src)
V, F = [], []
for ln in open(cel, encoding='utf-8', errors='ignore'):
    if ln.startswith('v '):
        x, y, z = (float(t) for t in ln.split()[1:4]); V.append(Vector((x, -z, y)))      # OBJ тракта: Y вверх
    elif ln.startswith('f '):
        ix = [int(t.split('/')[0]) - 1 for t in ln.split()[1:]]; F += [[ix[0], ix[k], ix[k + 1]] for k in range(1, len(ix) - 1)]
bvh = BVHTree.FromPolygons(V, F)
itog = []
for o in bpy.data.objects:
    if o.type != 'MESH' or '@' in o.name or not o.data.shape_keys or opt['imya'] not in o.data.shape_keys.key_blocks: continue
    kb = o.data.shape_keys.key_blocks[opt['imya']]; gn = {g.index: g.name for g in o.vertex_groups}
    M = o.matrix_world; Mi = M.inverted()
    # нормали формы-ключа
    P = [M @ d.co for d in kb.data]; N = [Vector((0, 0, 0)) for _ in P]
    for f in o.data.polygons:
        vs = list(f.vertices); n = (P[vs[1]] - P[vs[0]]).cross(P[vs[2]] - P[vs[0]])
        for v in vs: N[v] += n
    n_, sd = 0, []
    for v in o.data.vertices:
        if not v.groups or gn[max(v.groups, key=lambda g: g.weight).group] not in KO: continue
        p = P[v.index]; t = (p.z - Z0) / POL
        if t >= 1: continue
        w = 1.0 if t <= 0 else 1 - t * t * (3 - 2 * t)
        q, _, _, d = bvh.find_nearest(p)
        if q is None or N[v.index].length == 0 or (q - p).dot(N[v.index].normalized()) <= 0: continue
        np_ = p + (q - p) * w; kb.data[v.index].co = Mi @ np_; sd.append((np_ - p).length); n_ += 1
    if n_: itog.append('%s: %d вершин, сдвиг средн. %.1f, макс %.1f мм' % (o.name, n_, 1000 * np.mean(sd), 1000 * max(sd)))
    o.data.update()
print('ТЕЛО-К-ЦЕЛИ: %s над швом z %.2f, полоса %.2f — %s' % (','.join(sorted(KO)), Z0, POL, '; '.join(itog) or 'нечего'))
bpy.ops.wm.save_as_mainfile(filepath=out)
