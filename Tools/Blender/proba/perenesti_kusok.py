"""КУСОК — НА ТО ТЕЛО, НА КОТОРОЕ СЯДЕТ (27.10, решение ГеймБосса по зазору хвоста ст.1). Кусок ступени режется из подгонки к
своей цели и лежит в покое ТЕЛА ПОДГОНКИ (ступень 1 — ключ «Волк1», в игру не идёт), а игра рисует ступень телом ключа на вес
ступени («Волк» × 0.5). Хвост @Волк1 висел в 58 мм от тела и в игре, и на стенде (`stend_k2.py`, зазор черты).

Кусок переносится на разницу двух тел У СВОЕГО КОРНЯ: d = (база + вес·(ключ_игры − база)) − ключ_подгонки на вершинах тела в
корне куска (его вершины не дальше `--radius` сверх ближайшей к телу); сдвиг — средний d ближайших к ним вершин тела (жёстко: форма и веса куска те же). Тела одной топологии — шаблон К2.

  blender -b --factory-startup -P perenesti_kusok.py -- сборка.blend подгонка.blend @Волк1 Волк1 Волк 0.5 выход.blend [--radius 0.03]
"""
import sys
import bpy
import numpy as np
from mathutils import Vector
from mathutils import kdtree

a = sys.argv[sys.argv.index('--') + 1:]
dst, fit, kusok, k_fit, k_game, w, out = a[0], a[1], a[2], a[3], a[4], float(a[5]), a[6]
R = float(a[a.index('--radius') + 1]) if '--radius' in a else 0.03


def telo(path, key):
    """вершины тела шасси: база (ключ uid) → точка ключа, мир"""
    bpy.ops.wm.open_mainfile(filepath=path); out_ = {}
    for o in bpy.data.objects:
        if o.type != 'MESH' or '@' in o.name or not o.data.shape_keys or key not in o.data.shape_keys.key_blocks: continue
        kb = o.data.shape_keys.key_blocks[key]
        for vi, v in enumerate(o.data.vertices):
            b = o.matrix_world @ v.co
            out_[(round(b.x, 4), round(b.y, 4), round(b.z, 4))] = (np.array(b), np.array(o.matrix_world @ kb.data[vi].co))
    return out_


TF = telo(fit, k_fit)
TG = telo(dst, k_game)
obsh = [k for k in TF if k in TG]
B_ = np.array([TF[k][0] for k in obsh]); KF = np.array([TF[k][1] for k in obsh]); KG = np.array([TG[k][1] for k in obsh])
D = (B_ + w * (KG - B_)) - KF
o = bpy.data.objects[kusok]; Mw = o.matrix_world
P = np.array([Mw @ v.co for v in o.data.vertices])
kd = kdtree.KDTree(len(KF))
for i_, p in enumerate(KF): kd.insert(Vector(p), i_)
kd.balance()
d_min = [kd.find(Vector(p)) for p in P]
dm = min(x[2] for x in d_min)
# корень — вершины куска не дальше `--radius` сверх ближайшей; сдвиг — средний d их ближайших вершин тела (по двум точкам у
# одной ягодицы хвост получал боковой сдвиг 45 мм — у хвоста на средней линии его быть не должно)
idx = [j for (_, j, d_) in d_min if d_ <= dm + R]
t = D[idx].mean(0)
G = B_ + w * (KG - B_)
kg = kdtree.KDTree(len(G))
for i_, p in enumerate(G): kg.insert(Vector(p), i_)
kg.balance()
g0 = min(kg.find(Vector(p))[2] for p in P); g1 = min(kg.find(Vector(p + t))[2] for p in P)
# ДОЖИМ: разница тел у корня неоднородна, и жёсткий сдвиг по ней оставлял 18 мм (хвост ст.1: в позе 19 мм); корень (вершины в
# `--radius` от ближайшей) подводится к ближайшим вершинам тела игры средним вектором «корень → тело» в плоскости y–z: в позе 3.9 мм
for _ in range(2):
    F_ = [(p + t, kg.find(Vector(p + t))) for p in P]; gm = min(f[1][2] for f in F_)
    c = np.mean([np.array(f[1][0]) - f[0] for f in F_ if f[1][2] <= gm + R], axis=0)
    c[0] = 0.0   # дожим вбок не уводит: свободный x тянул корень к одной ягодице (боковой сдвиг 44 мм); боковая часть разницы тел остаётся
    t = t + c
print('ПЕРЕНОС: зазор куска до тела подгонки %.1f мм, до тела игры: до переноса %.1f, по разнице тел %.1f, после дожима %.1f мм' % (
    dm * 1000, g0 * 1000, g1 * 1000, min(kg.find(Vector(p + t))[2] for p in P) * 1000))
inv = Mw.inverted()
for v, p in zip(o.data.vertices, P): v.co = inv @ Vector(p + t)
print('ПЕРЕНОС: %s с тела «%s» на «%s»×%.2f — сдвиг %.1f мм (%s), по %d вершинам тела у корня' % (
    kusok, k_fit, k_game, w, float(np.linalg.norm(t)) * 1000, np.round(t * 1000, 1).tolist(), len(idx)))
bpy.ops.wm.save_as_mainfile(filepath=out)
