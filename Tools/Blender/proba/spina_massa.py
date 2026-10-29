"""МАССА СПИНЫ (30.10, критик n3 и ГеймБосс: «спина листа выше и полнее нашей»): поле `poza_po_maskam.py --spina 1` — задняя половина
корпуса в покое сдвигается назад (+y) на `--a` см; вес — за осью хребта (y 0…12 см), по высоте 0.95–1.60 м (края 15 см), без рук
(|x| < 25 см). Применяется к форме-ключу `--imya` объектов тела и к базе куска `--kusok` (гребень едет со спиной). Шаг — до ключей
областей (oblasti_k2.py берёт форму ключа).

  blender -b --factory-startup -P spina_massa.py -- вход.blend выход.blend --a 6.6 [--imya Волк] [--kusok @Волк]
"""
import sys
import bpy
import numpy as np
from mathutils import Vector

a = sys.argv[sys.argv.index('--') + 1:]
src, out = a[0], a[1]
opt = dict(a='0', imya='Волк', kusok='@Волк')
i = 2
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
A = float(opt['a']) / 100.0
bpy.ops.wm.open_mainfile(filepath=src)


def ss(t): t = np.clip(t, 0, 1); return t * t * (3 - 2 * t)
def w(P): return ss(P[:, 1] / 0.12) * ss((P[:, 2] - 0.95) / 0.15) * ss((1.60 - P[:, 2]) / 0.15) * ss((0.25 - np.abs(P[:, 0])) / 0.06)


itog = []
for o in bpy.data.objects:
    if o.type != 'MESH': continue
    if o.name == opt['kusok']: data = o.data.vertices
    elif '@' not in o.name and o.data.shape_keys and opt['imya'] in o.data.shape_keys.key_blocks: data = o.data.shape_keys.key_blocks[opt['imya']].data
    else: continue
    M = o.matrix_world; Mi = M.inverted()
    P = np.array([list(M @ d.co) for d in data]); ww = w(P)
    for d, p, x in zip(data, P, ww):
        if x > 1e-4: d.co = Mi @ Vector((p[0], p[1] + A * x, p[2]))
    o.data.update(); itog.append('%s %d' % (o.name, int((ww > 1e-4).sum())))
print('СПИНА-МАССА: +%.2f см назад — вершин: %s' % (100 * A, ', '.join(itog)))
bpy.ops.wm.save_as_mainfile(filepath=out)
