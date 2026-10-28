"""СПИНА СТ.1 БЕЗ КАПЮШОНА (28.10, критик r2 п.2: «сзади и в 3/4 сзади — гладкая масса от шеи до пояса с резким нижним краем,
читается капюшоном или рюкзаком»). Стенд без органа-гребня её повторяет: это тело ключа «Волк1» — у цели ст.1 по спине длинный
гребень, и подгонка тела вобрала его объём в кожу; край — там, где у цели кончался гребень. Шаг перед огранкой ст.1: на задней
стороне спины (нормаль базы в покое смотрит назад, +y) между высотами `--z` ключ тянется к базе: Волк1 = база + k·(Волк1 − база),
k = 1 − (1 − `--k`)·w, вес w — плавно по нормали (0 при n.y ≤ 0, 1 при n.y ≥ `--ny`) и по высоте (затухание `--kraj` м у краёв).
Плечи, бока и грудь (нормаль не назад) ключ сохраняют.

  blender -b --factory-startup -P spina_blizhe.py -- вход.blend выход.blend [--imya Волк1] [--z 1.05:1.70] [--k 0.25] [--ny 0.5] [--kraj 0.08]
"""
import sys
import bpy
import numpy as np

a = sys.argv[sys.argv.index('--') + 1:]
src, out = a[0], a[1]
opt = dict(imya='Волк1', z='1.05:1.70', k='0.25', ny='0.5', kraj='0.08')
i = 2
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
Z0, Z1 = (float(x) for x in opt['z'].split(':')); K, NY, KR = float(opt['k']), float(opt['ny']), float(opt['kraj'])
bpy.ops.wm.open_mainfile(filepath=src)


def ss(x):
    x = np.clip(x, 0, 1); return x * x * (3 - 2 * x)


itog = []
for o in bpy.data.objects:
    if o.type != 'MESH' or '@' in o.name or not o.data.shape_keys: continue
    kb = o.data.shape_keys.key_blocks.get(opt['imya'])
    if kb is None: continue
    M = o.matrix_world; R3 = M.to_3x3()
    B = np.array([list(M @ v.co) for v in o.data.vertices]); X = np.array([list(M @ d.co) for d in kb.data])
    N = np.array([list((R3 @ v.normal).normalized()) for v in o.data.vertices])
    w = ss(N[:, 1] / NY) * ss((B[:, 2] - Z0) / KR) * ss((Z1 - B[:, 2]) / KR)
    if not (w > 0.01).any(): continue
    k = 1 - (1 - K) * w; X2 = B + (X - B) * k[:, None]
    Mi = M.inverted()
    for j in np.nonzero(w > 0.01)[0]: kb.data[j].co = Mi @ __import__('mathutils').Vector(X2[j])
    sd = np.linalg.norm(X2 - X, axis=1)
    itog.append('%s — вершин %d, сдвиг средн. %.1f, макс %.1f мм' % (o.name, int((w > 0.01).sum()), 1000 * sd[w > 0.01].mean(), 1000 * sd.max()))
    o.data.update()
print('СПИНА: ключ %s к базе ×%.2f на спине (z %.2f–%.2f): %s' % (opt['imya'], K, Z0, Z1, '; '.join(itog)))
bpy.ops.wm.save_as_mainfile(filepath=out)
