"""СТОПА ЦЕЛИ КРУЧЕ И КОРОЧЕ (29.10, критик новой ст.2 п.2: «плюсна лежит ~27° к земле против ~55–60° у листа; стопа с плюсной ~30 %
роста против ~18 % — в 1.7 раза длиннее; лапа-лыжа»). Стопу сделал генератор, и подгонка её повторяет; позой не исправить — кость
стопы одна, пальцы ушли бы в землю. Правится цель в позе листа: вершины ниже скакательного сустава (`--z_hock`, у ног — дальше
`--x_min` от средней линии) сжимаются по глубине (y) к скакательному в `--k` раз; от `--z_hock` до `--z_full` сжатие нарастает
плавно, ниже — полностью. Ориентиры носков (плюсна_вн/нар) двигаются так же — пишется новый файл ориентиров.

  python cel_stopa.py цель.obj ориентиры.json выход.obj выход_ориентиры.json [--k 0.55] [--z_hock 0.32] [--z_full 0.18] [--x_min 0.1]
      [--y_hock 0.32]
"""
import sys, json
import numpy as np

a = sys.argv[1:]
src, lm_p, out, lm_out = a[:4]
opt = dict(k='0.55', z_hock='0.32', z_full='0.18', x_min='0.1', y_hock='0.32')
i = 4
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
K, ZH, ZF, XM, YH = (float(opt[n]) for n in ('k', 'z_hock', 'z_full', 'x_min', 'y_hock'))


def f(x, y, z):
    """тракт: x, y (−y — вперёд), z — вверх"""
    if z >= ZH or abs(x) < XM: return y
    t = min(1.0, (ZH - z) / (ZH - ZF)); t = t * t * (3 - 2 * t)
    return YH + (y - YH) * (1 - (1 - K) * t)


n = 0; lines = []
for ln in open(src, encoding='utf-8', errors='ignore'):
    if ln.startswith('v '):
        X, Yo, Zo = (float(v) for v in ln.split()[1:4])     # OBJ тракта: x, z_тракта, −y_тракта
        x, y, z = X, -Zo, Yo; y2 = f(x, y, z)
        if y2 != y: n += 1
        lines.append('v %.6f %.6f %.6f\n' % (x, z, -y2))
    else:
        lines.append(ln)
open(out, 'w', encoding='utf-8').writelines(lines)
L = json.load(open(lm_p, encoding='utf-8'))
for kname, (x, y, z) in list(L.items()):
    L[kname] = [x, round(f(x, y, z), 4), z]
json.dump(L, open(lm_out, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
print('СТОПА: сжато по глубине вершин %d (к скакательному y %.2f, ×%.2f ниже z %.2f, переход от z %.2f); носок y %.3f' % (
    n, YH, K, ZF, ZH, L['плюсна_вн'][1]))
