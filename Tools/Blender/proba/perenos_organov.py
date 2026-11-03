"""ПЕРЕНОС ОРГАНОВ НА ШАБЛОНЕ НА НОВУЮ БАЗУ (03.11, ГеймБосс: «волчьи уши и гребень висят клочьями — та же задача»; пробник аугментов
31.10: органы К2 расходятся с поджарой базой). Вершина органа (`<слот>@<вид>`, база и `рост`) — точка на ТЕЛЕ-ИСТОЧНИКЕ плюс смещение:
ближайшая точка базы источника → (треугольник, барицентрика) и смещение в рамке треугольника (ребро, нормаль). Та же точка и та же
рамка берутся на МОСТЕ (тот же треугольник в форме подгонки) — смещение едет с кожей и поворачивается с ней. Для объектов, не менявшихся
подгонкой (голова, у источника своя база), мост — сама база источника.

Выход: объекты органов с весами из FBX, привязанные к арматуре файла. Опора: база обоих состояний органа считается от одной и той же
точки кожи — ближайшей к вершине БАЗЫ органа (она прижата к телу), `рост` — смещением от неё же; иначе кончик когтя цеплялся за бедро.

  blender -b новая.blend --factory-startup -P perenos_organov.py -- выход.blend источник.fbx мост.blend [--organy Чутьё@Волк,Шкура@Волк,Руки@Волк] [--most_kluch Поджарый] [--kak_est @Волк1,@Лось — куски черт: они в форме ключа вида, ключ источника не менялся, позиция остаётся]
"""
import sys
import bpy
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

a = sys.argv[sys.argv.index('--') + 1:]
out, src_fbx, most_p = a[:3]
opt = dict(organy='Чутьё@Волк,Шкура@Волк,Руки@Волк', most_kluch='Поджарый')
i = 3
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
ORG = opt['organy'].split(',')
ARM = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]


def tris(o):
    me = o.data; me.calc_loop_triangles(); return np.array([list(t.vertices) for t in me.loop_triangles], dtype=np.int64)


with bpy.data.libraries.load(most_p) as (s_, d_): d_.objects = [n for n in s_.objects]
MOST = {}
for ob in d_.objects:
    if ob is None or ob.type != 'MESH' or '@' in ob.name or not ob.data.shape_keys: continue
    kb = ob.data.shape_keys.key_blocks.get(opt['most_kluch'])
    if kb is None: continue
    K = np.zeros(len(kb.data) * 3); kb.data.foreach_get('co', K); MOST[ob.name.split('.')[0]] = K.reshape(-1, 3)
keep = set(bpy.data.objects)
bpy.ops.import_scene.fbx(filepath=src_fbx, axis_forward='-Z', axis_up='Y', automatic_bone_orientation=False)
imp = [o for o in bpy.data.objects if o not in keep]
# единая поверхность источника и моста
PS, PM, TT = [], [], []; n0 = 0
for ob in imp:
    if ob.type != 'MESH' or '@' in ob.name: continue
    nm = ob.name.split('.')[0]; Mw = np.array(ob.matrix_world); kb = ob.data.shape_keys.key_blocks[0]
    c = np.zeros(len(kb.data) * 3); kb.data.foreach_get('co', c); B = c.reshape(-1, 3) @ Mw[:3, :3].T + Mw[:3, 3]
    M_ = MOST.get(nm)
    if M_ is None or len(M_) != len(B) or nm in ('голова', 'Чутьё'): M_ = B   # не подгонялось: мост — своя база
    PS.append(B); PM.append(M_); TT.append(tris(ob) + n0); n0 += len(B)
PS, PM, TT = np.vstack(PS), np.vstack(PM), np.vstack(TT)
bvh = BVHTree.FromPolygons([Vector(p) for p in PS], TT.tolist(), all_triangles=True)


def bary(p, A, B, C):
    v0, v1, v2 = B - A, C - A, p - A
    d00, d01, d11, d20, d21 = v0 @ v0, v0 @ v1, v1 @ v1, v2 @ v0, v2 @ v1
    den = d00 * d11 - d01 * d01
    if abs(den) < 1e-14: return np.array([1.0, 0, 0])
    v = (d11 * d20 - d01 * d21) / den; w = (d00 * d21 - d01 * d20) / den
    return np.array([1 - v - w, v, w])


def ramka(A, B, C):
    e = B - A; e = e / max(np.linalg.norm(e), 1e-12); n = np.cross(B - A, C - A); n = n / max(np.linalg.norm(n), 1e-12)
    return np.array([e, np.cross(n, e), n])


for nm in ORG:
    ob = next((o for o in imp if o.name.split('.')[0] == nm), None)
    if ob is None: print('ОРГАН: %s — нет в источнике' % nm); continue
    Mw = np.array(ob.matrix_world); kb = ob.data.shape_keys.key_blocks
    get = lambda k_: (lambda c_: c_.reshape(-1, 3) @ Mw[:3, :3].T + Mw[:3, 3])((lambda c: (k_.data.foreach_get('co', c), c)[1])(np.zeros(len(k_.data) * 3)))
    B0 = get(kb[0]); Rs = get(kb['рост']) if 'рост' in kb else B0.copy()
    N0, Nr = np.zeros_like(B0), np.zeros_like(B0); sd = []
    for k, p in enumerate(B0):
        loc, _, fi, _ = bvh.find_nearest(Vector(p)); t = TT[fi]
        w = bary(np.array(loc), PS[t[0]], PS[t[1]], PS[t[2]])
        Fs = ramka(PS[t[0]], PS[t[1]], PS[t[2]]); Fm = ramka(PM[t[0]], PM[t[1]], PM[t[2]])
        qs = w[0] * PS[t[0]] + w[1] * PS[t[1]] + w[2] * PS[t[2]]; qm = w[0] * PM[t[0]] + w[1] * PM[t[1]] + w[2] * PM[t[2]]
        N0[k] = qm + Fm.T @ (Fs @ (p - qs)); Nr[k] = qm + Fm.T @ (Fs @ (Rs[k] - qs)); sd.append(np.linalg.norm(N0[k] - p))
    # в локальные координаты нового родителя (арматура файла)
    inv = np.array(ARM.matrix_world.inverted())
    to_l = lambda X: X @ inv[:3, :3].T + inv[:3, 3]
    ob.parent = ARM; ob.matrix_parent_inverse.identity(); ob.matrix_basis.identity()
    for m in ob.modifiers:
        if m.type == 'ARMATURE': m.object = ARM
    Bl, Rl = to_l(N0), to_l(Nr)
    kb[0].data.foreach_set('co', Bl.ravel()); ob.data.vertices.foreach_set('co', Bl.ravel())
    if 'рост' in kb: kb['рост'].data.foreach_set('co', Rl.ravel())
    ob.name = nm; ob.data.name = nm
    print('ОРГАН: %s — вершин %d, сдвиг средн %.1f мм, макс %.1f мм' % (nm, len(B0), float(np.mean(sd)) * 1000, float(np.max(sd)) * 1000))
KE = [q for q in opt.get('kak_est', '').split(',') if q]
for nm in KE:
    ob = next((o for o in imp if o.name.split('.')[0] == nm), None)
    if ob is None: print('ОРГАН: %s — нет в источнике' % nm); continue
    Mw = ob.matrix_world.copy(); inv = ARM.matrix_world.inverted()
    Q = [inv @ (Mw @ v.co) for v in ob.data.vertices]
    ob.parent = ARM; ob.matrix_parent_inverse.identity(); ob.matrix_basis.identity()
    for m in ob.modifiers:
        if m.type == 'ARMATURE': m.object = ARM
    for v, q in zip(ob.data.vertices, Q): v.co = q
    ob.name = nm; ob.data.name = nm
    print('ОРГАН: %s — как есть, вершин %d' % (nm, len(Q)))
for ob in imp:
    if ob.name not in ORG and ob.name not in KE: bpy.data.objects.remove(ob)
bpy.ops.wm.save_as_mainfile(filepath=out)
