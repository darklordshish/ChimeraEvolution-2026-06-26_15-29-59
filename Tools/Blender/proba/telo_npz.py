"""ТЕЛО СТУПЕНИ В NPZ (29.10, проба Б — поза по маскам листа вне Blender): видимые на ступени объекты (тело с формой-ключом `--imya`
на весе 1, черта `--cherta`, без `--skryt`) — вершины покоя, веса костей (до 4), треугольники, головы костей покоя (мир); базовое тело (VB) и признак куска (KU) — для меры
разворотов граней, как у стенда.

  blender -b --factory-startup -P telo_npz.py -- вход.blend выход.npz [--imya Волк] [--cherta @Волк] [--skryt голова,Чутьё,Шкура]
"""
import sys
import bpy
import numpy as np

a = sys.argv[sys.argv.index('--') + 1:]
src, out = a[0], a[1]
opt = dict(imya='Волк', cherta='@Волк', skryt='голова,Чутьё,Шкура')
i = 2
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
SK = set(opt['skryt'].split(','))
bpy.ops.wm.open_mainfile(filepath=src)
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
BN = [b.name for b in arm.data.bones]; BI = {n: j for j, n in enumerate(BN)}
RH = np.array([list(arm.matrix_world @ b.head_local) for b in arm.data.bones])
V, W, F, OB, VB, KU = [], [], [], [], [], []
for o in bpy.data.objects:
    if o.type != 'MESH': continue
    if o.name.startswith('@'):
        if o.name != opt['cherta']: continue
        co = [o.matrix_world @ v.co for v in o.data.vertices]; cb = co
    else:
        if '@' in o.name or o.name.split('.')[0] in SK or not o.data.shape_keys or opt['imya'] not in o.data.shape_keys.key_blocks: continue
        co = [o.matrix_world @ d.co for d in o.data.shape_keys.key_blocks[opt['imya']].data]; cb = [o.matrix_world @ v.co for v in o.data.vertices]
    gn = {g.index: g.name for g in o.vertex_groups}; n0 = len(V)
    for v, c, c0 in zip(o.data.vertices, co, cb):
        V.append(list(c)); VB.append(list(c0)); KU.append(o.name.startswith('@')); w = np.zeros(len(BN))
        for g in v.groups:
            if gn[g.group] in BI and g.weight > 0: w[BI[gn[g.group]]] = g.weight
        s = w.sum(); W.append(w / s if s else w)
    for p in o.data.polygons:
        vs = list(p.vertices); F += [[n0 + vs[0], n0 + vs[k], n0 + vs[k + 1]] for k in range(1, len(vs) - 1)]
    OB.append(o.name)
np.savez(out, VB=np.array(VB), KU=np.array(KU), V=np.array(V), W=np.array(W, dtype=np.float32), F=np.array(F), RH=RH, BN=np.array(BN))
print('ТЕЛО-NPZ: %s — вершин %d, треугольников %d, костей %d' % (', '.join(OB), len(V), len(F), len(BN)))
