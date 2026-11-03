"""ДЕТЕКТОР ШВОВ ПАСПОРТОВ (03.11, решение ГеймБосса: индексы не держим, положение через паспорта). Сварка `PartPieces` ищет кольцо по близости 2 мм и при промахе МОЛЧИТ. Для каждого паспорта с `seam.ring_m` — расстояние точек кольца до меша: `kraj` — до ближайшей вершины меша (нужно для сварки, порог `--porog`, 2 мм), `kozha` — до поверхности (для посадочных мест на сплошной коже кольца в меше нет, важна только посадка: порог 5 мм).
Выход 1, если есть нарушения. Паспорта задаются файлами или папкой.

  blender -b меш.blend --factory-startup --python-exit-code 1 -P shvy_detektor.py -- паспорт.json|папка … [--porog 0.002] [--imya Поджарый] [--bez голова,Чутьё]
Для FBX: первым аргументом `--fbx файл.fbx`.
"""
import sys, json, os, glob
import bpy
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

a = sys.argv[sys.argv.index('--') + 1:]
opt = dict(porog='0.002', imya='Поджарый', fbx=''); fl = []
i = 0
while i < len(a):
    if a[i].startswith('--'): opt[a[i][2:]] = a[i + 1]; i += 2
    else: fl.append(a[i]); i += 1
if opt['fbx']:
    bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.fbx(filepath=opt['fbx'], axis_forward='-Z', axis_up='Y', automatic_bone_orientation=False)
V, F = [], []
for o in bpy.data.objects:
    if o.type != 'MESH' or '@' in o.name: continue
    kb = o.data.shape_keys.key_blocks.get(opt['imya']) if o.data.shape_keys else None
    n0 = len(V); V += [o.matrix_world @ (kb.data[j].co if kb else o.data.vertices[j].co) for j in range(len(o.data.vertices))]
    o.data.calc_loop_triangles(); F += [[n0 + q for q in t.vertices] for t in o.data.loop_triangles]
bvh = BVHTree.FromPolygons(V, F, all_triangles=True); VA = np.array([list(v) for v in V])
PS = []
for f in fl:
    PS += sorted(glob.glob(os.path.join(f, 'chelovek-*.json'))) if os.path.isdir(f) else [f]
bad = 0
for f in PS:
    try: d = json.load(open(f, encoding='utf-8'))
    except Exception: continue
    s = d.get('seam') if isinstance(d, dict) else None
    if not s or not s.get('ring_m'): continue
    R = np.array([[-u[0], -u[2], u[1]] for u in s['ring_m']])
    dv = np.linalg.norm(R[:, None] - VA[None], axis=2).min(1); ds = np.array([bvh.find_nearest(Vector(p))[3] for p in R])
    seat = str(d.get('kind', '')).startswith('посадочное')
    ok = (ds.max() < 0.005) if seat else (dv.max() < float(opt['porog']))
    bad += 0 if ok else 1
    print('ШОВ %s %s «%s»: %s вершин %d; до вершины %.1f / %.1f мм, до кожи %.1f / %.1f мм (средн / макс)' % ('ОК ' if ok else 'НЕТ', os.path.basename(f), s.get('type'), 'место' if seat else 'кольцо', len(R), dv.mean() * 1000, dv.max() * 1000, ds.mean() * 1000, ds.max() * 1000))
print('ШВЫ: нарушений %d из %d' % (bad, len(PS)))
sys.exit(1 if bad else 0)
