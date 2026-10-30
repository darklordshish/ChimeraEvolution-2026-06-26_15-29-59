"""СОВМЕЩЕНИЕ ЦЕЛИ С ШАБЛОНОМ ПЕРЕНОСОМ (30.10, поджарый человек). `cel_norm.py` ставит цель по рамке (рост 1.85, стопы на земле),
но не по глубине: цель листа поджарого человека встала на 3.7 см позади К2 (центры срезов по y: голова +0.037, торс +0.035,
голень +0.042). Подгонка уводила тело назад, а замороженная голова оставалась — ступенька под челюстью. Перенос по y (и x)
ищется ICP только переносом: ближайшие вершины шаблона, 80 % ближайших пар; высота не трогается, x обнуляется (симметрия).

  python cel_sovmestit.py шаблон_вершины.npy цель.obj выход.obj
шаблон_вершины.npy — вершины шаблона в осях Blender (n×3); цель и выход — OBJ в осях Y-вверх, как у `cel_norm.py`.
"""
import sys
import numpy as np
from scipy.spatial import cKDTree

kp, src, out = sys.argv[1:4]
K = np.load(kp)
L = open(src).read().splitlines()
V = np.array([list(map(float, l.split()[1:4])) for l in L if l.startswith('v ')])
T = np.c_[V[:, 0], -V[:, 2], V[:, 1]]
kd = cKDTree(K); t = np.zeros(3)
for _ in range(30):
    d, i = kd.query(T + t); m = d < np.percentile(d, 80)
    t += (K[i[m]] - (T[m] + t)).mean(0) * np.array([1, 1, 0])
print('СОВМЕЩЕНИЕ: перенос цели (Blender x, y, z) =', t.round(4), '— x обнулён')
t[0] = 0
T2 = T + t; res = []; j = 0
for l in L:
    if l.startswith('v '):
        p = T2[j]; j += 1; res.append('v %.6f %.6f %.6f' % (p[0], p[2], -p[1]))
    else:
        res.append(l)
open(out, 'w').write('\n'.join(res) + '\n')
