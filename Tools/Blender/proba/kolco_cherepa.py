"""КОЛЬЦО ЧЕРЕПА НА ТОЙ ЖЕ БАЗЕ ПОСЛЕ ОГРАНКИ (05.11, Б6). `pasport_shva.py` ведёт кольцо по сдвигу прежней и новой базы с общей топологией; огранка топологию меняет, но 12 вершин кольца оставляет в объекте `голова`, только двигает их массами шеи (до 7 мм). Кольцо берётся с этих вершин: у каждой точки паспорта ближайшая вершина головы, порядок прежний, вершина 0 на верху. Вершины обязаны быть разными и лежать ближе `--dop` (по умолчанию 10 мм), иначе отказ. Правится только поля шва и счётчик граней.

  blender -b сборка.blend --factory-startup --python-exit-code 1 -P kolco_cherepa.py -- паспорт.json [--dop 0.01]
"""
import sys, json
import bpy
import numpy as np

a = sys.argv[sys.argv.index('--') + 1:]; pas = a[0]; DOP = float(a[a.index('--dop') + 1]) if '--dop' in a else 0.01
o = bpy.data.objects['голова']; kb = o.data.shape_keys.key_blocks.get('Поджарый') if o.data.shape_keys else None
A = np.array([tuple(o.matrix_world @ (kb.data[i].co if kb else v.co)) for i, v in enumerate(o.data.vertices)])
d = json.load(open(pas, encoding='utf-8')); s = d['seam']
R0 = np.array([[-u[0], -u[2], u[1]] for u in s['ring_m']]); D = np.linalg.norm(R0[:, None] - A[None], axis=2); J = D.argmin(1)
if len(set(J.tolist())) != len(J) or D.min(1).max() > DOP: sys.exit('КОЛЬЦО: вершины совпали или далеко: макс %.1f мм, разных %d из %d' % (D.min(1).max() * 1000, len(set(J.tolist())), len(J)))
R1 = A[J]; sd = np.array(s.get('side', [1, 0, 0]), float); up = np.array(s['up'], float); Ru = np.array([[-q[0], q[2], -q[1]] for q in R1]); c = Ru.mean(0)
el = [round(float((Ru - c).dot(sd).max()), 4), round(float((Ru - c).dot(up).max()), 4)]
print('КОЛЬЦО: центр %s → %s, эллипс %s → %s, сдвиг макс %.1f мм' % (s['center_m'], [round(float(q), 4) for q in c], s['ellipse_m'], el, D.min(1).max() * 1000))
s['ring_m'] = [[round(float(-q[0]), 5), round(float(q[2]), 5), round(float(-q[1]), 5)] for q in R1]; s['center_m'] = [round(float(q), 5) for q in c]; s['ellipse_m'] = el
o.data.calc_loop_triangles(); d['tris'] = d['tris_main'] = len(o.data.loop_triangles)
json.dump(d, open(pas, 'w', encoding='utf-8'), ensure_ascii=False, indent=2)
