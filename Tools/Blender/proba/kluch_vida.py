"""ИДЕНТИЧНОСТЬ КЛЮЧОМ (опыт 14.10, решение геймдизайнера «давай попробуем»): форма цели (оборотень по листу, тракт
Hunyuan) переносится на ТОПОЛОГИЮ меша шасси (человек К2: те же объекты, число и порядок вершин) и ложится в каждый объект
формой-ключом с именем вида-донора (`Волк`). Игра ставит вес ключа по идентичности; кости едут за суставами цели.

Перенос по ориентирам (как Wrap / Landmark Warp):
  1. ОРИЕНТИРЫ — суставы скелета шасси (`*-skelet.json`) и точки кожи (макушка, нос, подбородок, кончики пальцев,
     носки, пятки). У шасси считаются здесь, у цели приходят файлом `ориентиры.json` (имя → точка в метрах тела, оси
     Blender) — снятые с цели замером (`--istochnik` печатает имена и точки шасси как образец формата);
  2. ГРУБО — тонкая пластина (TPS, ядро r) по парам ориентиров тянет все вершины шасси к цели;
  3. ТОЧНО — `--iter` раз: каждая вершина к ближайшей точке поверхности цели (не дальше `--maxd`), поле сдвигов
     сглаживается по связности (`--smooth` проходов) — сетка не рвётся и не слипается. Швы между объектами (кольца шеи,
     ушей) — общие вершины: сдвиг считается один на точку, стыки не расходятся;
  4. объекты-исключения (`--bez`, по умолчанию уши `Чутьё`: у цели их форма — кусок-черта) едут только пластиной;
  5. СУСТАВЫ ЦЕЛИ — те же суставы шасси, перенесённые пластиной, — файлом рядом (формат `*-skelet.json`).

  blender -b --factory-startup -P kluch_vida.py -- шасси.blend цель.obj ориентиры.json выход.fbx скелет_шасси.json суставы_цели.json
      [--imya Волк] [--iter 4] [--smooth 6] [--maxd 0.08] [--maxd_obj голова:0.025,шея:0.025 [--zatylok y]] [--bez Чутьё] [--lam 1e-4]
  blender -b --factory-startup -P kluch_vida.py -- шасси.blend - - - скелет_шасси.json - --istochnik ориентиры_шасси.json
"""
import sys, json, math
import bpy, bmesh
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

a = sys.argv[sys.argv.index('--') + 1:]
src_blend, tgt_obj, lm_path, out_fbx, skel_path, joints_out = a[:6]
opt = dict(imya='Волк', iter='4', smooth='6', maxd='0.08', bez='Чутьё', lam='1e-4')
i = 6
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
NAME = opt['imya']; BEZ = set(opt['bez'].split(',')) if opt['bez'] else set()

bpy.ops.wm.open_mainfile(filepath=src_blend)
OBJ = [o for o in bpy.data.objects if o.type == 'MESH']
sk = json.load(open(skel_path, encoding='utf-8'))
BN = {b['name']: b for b in sk['bones']}


def world(o):
    return [o.matrix_world @ v.co for v in o.data.vertices]


# ---------- ориентиры шасси ----------
def landmarks():
    L = {}
    J = (('таз', 'head'), ('хребет', 'head'), ('шея', 'head'), ('голова', 'head'), ('голова', 'tail'))
    for b, e in J:
        if b in BN: L['%s:%s' % (b, e)] = Vector(BN[b][e])
    for s in ('', '.L'):
        for b, e in (('плечо', 'head'), ('предплечье', 'head'), ('предплечье', 'tail'), ('бедро', 'head'), ('голень', 'head'), ('голень', 'tail')):
            if b + s in BN: L['%s%s:%s' % (b, s, e)] = Vector(BN[b + s][e])
    V = [p for o in OBJ for p in world(o)]
    hd = [p for p in V if p.z > 1.55 and abs(p.x) < 0.10]
    L['макушка'] = max(hd, key=lambda p: p.z)
    L['нос'] = min((p for p in hd if 1.62 < p.z < 1.75), key=lambda p: p.y)
    L['подбородок'] = min((p for p in hd if 1.56 < p.z < 1.63 and abs(p.x) < 0.02), key=lambda p: p.y)
    L['затылок'] = max((p for p in hd if 1.62 < p.z < 1.75 and abs(p.x) < 0.03), key=lambda p: p.y)
    for sg, s in ((-1, ''), (1, '.L')):          # без суффикса — сторона −X Blender
        side = [p for p in V if p.x * sg > 0.05]
        L['пальцы' + s] = min((p for p in side if p.z > 0.4 and abs(p.x) > 0.26), key=lambda p: p.z)     # кисть — дальше 0.26 от оси: ближе стоит бедро
        foot = [p for p in side if p.z < 0.12]
        L['носок' + s] = min(foot, key=lambda p: p.y)
        L['пятка' + s] = max(foot, key=lambda p: p.y)
    return L


LS = landmarks()
if 'istochnik' in opt:
    json.dump({k: [round(c, 4) for c in v] for k, v in LS.items()}, open(opt['istochnik'], 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    print('КЛЮЧ: ориентиров шасси %d → %s' % (len(LS), opt['istochnik']))
    sys.exit(0)

LT = {k: Vector(v) for k, v in json.load(open(lm_path, encoding='utf-8')).items()}
names = [k for k in LS if k in LT]
miss = [k for k in LS if k not in LT]
P = np.array([LS[k] for k in names]); Q = np.array([LT[k] for k in names])

# ---------- 2. тонкая пластина ----------
n = len(P)
K = np.linalg.norm(P[:, None] - P[None], axis=2) + float(opt['lam']) * np.eye(n)
Pm = np.hstack([np.ones((n, 1)), P])
A = np.zeros((n + 4, n + 4)); A[:n, :n] = K; A[:n, n:] = Pm; A[n:, :n] = Pm.T
B = np.zeros((n + 4, 3)); B[:n] = Q
W = np.linalg.solve(A, B)


def tps(X):
    X = np.asarray(X)
    return np.linalg.norm(X[:, None] - P[None], axis=2) @ W[:n] + np.hstack([np.ones((len(X), 1)), X]) @ W[n:]


# ---------- общие вершины всех объектов (швы) ----------
key_of = lambda p: (round(p.x, 5), round(p.y, 5), round(p.z, 5))
uid, U0, owner = {}, [], []
vmap = {}
for o in OBJ:
    for vi, p in enumerate(world(o)):
        k = key_of(p)
        if k not in uid:
            uid[k] = len(U0); U0.append(p); owner.append(set())
        vmap[(o.name, vi)] = uid[k]; owner[uid[k]].add(o.name)
U0 = np.array(U0)
nb = [set() for _ in U0]
for o in OBJ:
    for e in o.data.edges:
        x, y = vmap[(o.name, e.vertices[0])], vmap[(o.name, e.vertices[1])]
        nb[x].add(y); nb[y].add(x)
free = np.array([not (own & BEZ) for own in owner])       # вершины ушей (и их кольца) — только пластиной
# ПРЕДЕЛ ПРИТЯЖКИ ПО ОБЪЕКТАМ (`--maxd_obj голова:0.025,шея:0.025`): у цели вокруг головы и шеи — грива (кусок-черта, из
# кожи её не тянут); с общим пределом затылок шасси прилипал к гриве и вытягивался гребнем. Вершина на стыке объектов
# берёт меньший предел
MAXD_OBJ = {kv.split(':')[0]: float(kv.split(':')[1]) for kv in opt.get('maxd_obj', '').split(',') if ':' in kv}

X = tps(U0)
# ---------- 3. к поверхности цели ----------
bpy.ops.wm.obj_import(filepath=tgt_obj, forward_axis='NEGATIVE_Z', up_axis='Y')
tg = [o for o in bpy.context.selected_objects if o.type == 'MESH'][0]
bm = bmesh.new(); bm.from_mesh(tg.data); bm.transform(tg.matrix_world)
bvh = BVHTree.FromBMesh(bm)
maxd = float(opt['maxd'])
# предел объекта действует только за плоскостью `--zatylok` (y шасси, Blender: морда в −Y) — лицо тянется к цели полностью
YZ = float(opt.get('zatylok', '-9'))
vmax = np.array([min([MAXD_OBJ.get(nm, maxd) if U0[k_][1] > YZ else maxd for nm in own]) for k_, own in enumerate(owner)])
for it in range(int(opt['iter'])):
    D = np.zeros_like(X)
    for k_ in range(len(X)):
        if not free[k_]:
            continue
        hit = bvh.find_nearest(Vector(X[k_]))
        if hit[0] is not None and hit[3] < vmax[k_]:
            D[k_] = np.array(hit[0]) - X[k_]
    for _ in range(int(opt['smooth'])):
        D = np.array([0.5 * D[k_] + 0.5 * (np.mean([D[j] for j in nb[k_]], axis=0) if nb[k_] else D[k_]) if free[k_] else D[k_] for k_ in range(len(D))])
    X = X + D
    print('КЛЮЧ: проход %d, средний сдвиг %.4f м, наибольший %.4f' % (it + 1, np.linalg.norm(D, axis=1).mean(), np.linalg.norm(D, axis=1).max()))
res = [bvh.find_nearest(Vector(X[k_]))[3] for k_ in range(len(X)) if free[k_]]
print('КЛЮЧ: до поверхности цели — средн. %.4f м, 95%% %.4f, макс %.4f; ориентиров %d, нет у цели: %s' % (
    float(np.mean(res)), float(np.percentile(res, 95)), float(np.max(res)), n, ', '.join(miss) or '—'))
bpy.data.objects.remove(tg, do_unlink=True)

# ---------- ключи ----------
for o in OBJ:
    if o.data.shape_keys is None:
        o.shape_key_add(name='Basis')
    kb = o.shape_key_add(name=NAME, from_mix=False)
    inv = o.matrix_world.inverted()
    for vi in range(len(o.data.vertices)):
        kb.data[vi].co = inv @ Vector(X[vmap[(o.name, vi)]])
    print('КЛЮЧ: «%s» у объекта `%s`: %d вершин' % (NAME, o.name, len(o.data.vertices)))

# ---------- 5. суставы цели ----------
out = dict(sk); bones = []
for b in sk['bones']:
    h, t = tps([b['head'], b['tail']])
    bones.append(dict(b, head=[round(float(c), 5) for c in h], tail=[round(float(c), 5) for c in t]))
out['bones'] = bones
out['note'] = 'суставы цели «%s» на шасси: суставы шасси, перенесённые тонкой пластиной по ориентирам (kluch_vida.py); оси как у скелета шасси' % NAME
json.dump(out, open(joints_out, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)

ao = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
bpy.ops.object.select_all(action='SELECT')
bpy.context.view_layer.objects.active = ao
bpy.ops.export_scene.fbx(filepath=out_fbx, use_selection=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z',
                         axis_up='Y', object_types={'ARMATURE', 'MESH'}, add_leaf_bones=False, bake_anim=False,
                         mesh_smooth_type='FACE', use_armature_deform_only=False)
if 'blend' in opt:
    bpy.ops.wm.save_as_mainfile(filepath=opt['blend'])
print('КЛЮЧ: готово → %s, суставы → %s' % (out_fbx, joints_out))
