"""ПАСПОРТ ШВА НА НОВОЙ БАЗЕ (03.11, решение ГеймБосса: положение держим через паспорта, имя шва — контракт). Кольцо паспорта на прежней базе привязывается к её вершинам (≤ 2 мм; ключ `--imya`), на новой базе берётся та же вершина: топология объекта между файлами одна (между ними только сдвиги формы). Пересчитываются `ring_m`, `center_m`, `ellipse_m` (полуоси по `side` и `up`); имя, кость, ось не меняются. Выход — кандидат в рабочую папку, в поставку не кладётся.

  blender -b прежняя.blend --factory-startup -P pasport_shva.py -- новая.blend паспорт.json выход.json [--imya Поджарый]
"""
import sys, json
import bpy
import numpy as np

a = sys.argv[sys.argv.index('--') + 1:]
nov, pas, out = a[:3]; KEY = a[a.index('--imya') + 1] if '--imya' in a else 'Поджарый'


def snimok():
    R = {}
    for o in bpy.data.objects:
        if o.type == 'MESH' and '@' not in o.name and o.data.shape_keys and KEY in o.data.shape_keys.key_blocks:
            R[o.name] = np.array([tuple(o.matrix_world @ k.co) for k in o.data.shape_keys.key_blocks[KEY].data])
    return R


S0 = snimok()
bpy.ops.wm.open_mainfile(filepath=nov); S1 = snimok()
d = json.load(open(pas, encoding='utf-8')); s = d['seam']
U = lambda q: [round(float(-q[0]), 5), round(float(q[2]), 5), round(float(-q[1]), 5)]
R0 = np.array([[-u[0], -u[2], u[1]] for u in s['ring_m']]); R1 = []
for p in R0:
    best = (9, None, None)
    for n, P in S0.items():
        if n not in S1 or len(S1[n]) != len(P): continue
        dd = np.linalg.norm(P - p, axis=1); j = int(dd.argmin())
        if dd[j] < best[0]: best = (float(dd[j]), n, j)
    if best[0] > 0.002: print('ПАСПОРТ: точка %s — ближайшая вершина %.1f мм, сдвиг берётся от неё' % (p.round(3), best[0] * 1000))
    R1.append(p + (S1[best[1]][best[2]] - S0[best[1]][best[2]]))
R1 = np.array(R1); sd = np.array(s.get('side', [1, 0, 0]), float); up = np.array(s['up'], float); Ru = np.array([[-q[0], q[2], -q[1]] for q in R1]); c = Ru.mean(0)
el = [round(float((Ru - c).dot(sd).max()), 4), round(float((Ru - c).dot(up).max()), 4)]
print('ПАСПОРТ: %s — центр %s → %s, эллипс %s → %s, сдвиг кольца макс %.1f мм' % (s['type'], s['center_m'], [round(float(q), 4) for q in c], s['ellipse_m'], el, float(np.linalg.norm(R1 - R0, axis=1).max()) * 1000))
s['ring_m'] = [U(q) for q in R1]; s['center_m'] = [round(float(q), 5) for q in c]; s['ellipse_m'] = el
json.dump(d, open(out, 'w', encoding='utf-8'), ensure_ascii=False, indent=2)
