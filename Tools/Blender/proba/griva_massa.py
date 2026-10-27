"""ГРИВА И ХВОСТ — МАССОЙ ПРЯДЕЙ, А НЕ СТРУЖКОЙ (27.10, ГеймБосс по P2: «осколки-шипы по всему контуру головы и плеч и на груди,
торчат на 2–5 см; на листе ступеней пряди крупные — укрупнить и прижать»). Шерстяной кусок режется из цели генератора с тонкими
прядями; на куске (до слияния со звериными кусками тела):

  1. УКРУПНЕНИЕ — прореживание схлопыванием рёбер до доли `--dolya` треугольников (веса костей Blender переносит сам);
  2. ШИПЫ — сглаживание Таубина `--glad` шагов (λ 0.5, μ −0.53: без усадки объёма) — тонкие кончики уходят в массу пряди;
  3. ПРИЖИМ — отступ шерсти от поверхности тела (форма-ключ `--imya` шасси) сжимается в `--prizhim` раз: грива ложится на объём.

  blender -b --factory-startup -P griva_massa.py -- вход.blend выход.blend [--kusok @Волк] [--imya Волк] [--dolya 0.35] [--glad 6]
      [--prizhim 0.8] [--ne_trogat голова,предплечье,…] [--hvost_ne 0.08:1.05|0] [--zad 1.2:0.5]
"""
import sys
import bpy, bmesh
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

a = sys.argv[sys.argv.index('--') + 1:]
src, out = a[0], a[1]
opt = dict(kusok='@Волк', imya='Волк', dolya='0.35', glad='6', prizhim='0.8')
i = 2
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
bpy.ops.wm.open_mainfile(filepath=src)
o = bpy.data.objects[opt['kusok']]; n0 = len(o.data.polygons)
# ТОЛЬКО ШЕРСТЬ: в куске `@Волк` слиты и голова с ушами, и кисти, и лапы — их главная кость (`--ne_trogat`) не шерстяная;
# первая проба 27.10 укрупнила голову в ком
NT = set(opt.get('ne_trogat', 'голова,предплечье,предплечье.L,голень,голень.L,стопа,стопа.L').split(','))
gn = {g.index: g.name for g in o.vertex_groups}
def glavnaya(v): return gn[max(v.groups, key=lambda g: g.weight).group] if v.groups else ''
sherst = o.vertex_groups.get('шерсть') or o.vertex_groups.new(name='шерсть')
# ХВОСТ — НЕ ТРОГАТЬ (`--hvost_ne y:z`, ГеймБосс по G2: «хвост стал плоской лопастью — лист-клинок в профиль»): прореживание
# схлопывало его объём; хвост — вершины позади (y > y) и ниже поясницы (z < z) в покое
HN = [float(x) for x in opt.get('hvost_ne', '0.08:1.05').split(':')] if opt.get('hvost_ne', '0.08:1.05') != '0' else None
def hvost(v):
    p = o.matrix_world @ v.co; return HN is not None and p.y > HN[0] and p.z < HN[1]
ids = [v.index for v in o.data.vertices if glavnaya(v) not in NT and not hvost(v)]
sherst.add(ids, 1.0, 'REPLACE')
# 1. укрупнение — только по группе «шерсть»
bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
# доля — от граней ШЕРСТИ, а не всего куска: иначе вынутый из группы хвост отдавал свою долю гриве, и её прореживало вдвое
# (G4 27.10: IoU ст.2 0.679 → 0.591)
ids_s = set(ids); n_sh = sum(1 for p in o.data.polygons if all(v in ids_s for v in p.vertices))
m = o.modifiers.new('d', 'DECIMATE'); m.use_collapse_triangulate = True
m.ratio = max(0.05, 1.0 - (1.0 - float(opt['dolya'])) * n_sh / max(n0, 1))
m.vertex_group = 'шерсть'; m.vertex_group_factor = 1000.0
bpy.ops.object.modifier_apply(modifier='d')
# сглаживание и прижим — вся шерсть, ВКЛЮЧАЯ хвост: хвост не прореживается (объём), но стружку Таубин с него снимает (G5 27.10)
o.vertex_groups.remove(o.vertex_groups['шерсть'])
gn = {g.index: g.name for g in o.vertex_groups}
SH = np.array([glavnaya(v) not in NT for v in o.data.vertices])
# 2. шипы
bm = bmesh.new(); bm.from_mesh(o.data); bm.verts.ensure_lookup_table()
V = np.array([list(v.co) for v in bm.verts]); NB = [[e.other_vert(v).index for e in v.link_edges] for v in bm.verts]
kraj = np.array([any(e.is_boundary for e in v.link_edges) for v in bm.verts])
for _ in range(int(opt['glad'])):
    for lam in (0.5, -0.53):
        L = np.array([V[nb_].mean(0) - V[k] if nb_ else np.zeros(3) for k, nb_ in enumerate(NB)])
        L[kraj | ~SH] = 0; V = V + lam * L
# 3. прижим к телу
T, TF = [], []
for b in bpy.data.objects:
    if b.type != 'MESH' or '@' in b.name or not b.data.shape_keys or opt['imya'] not in b.data.shape_keys.key_blocks: continue
    kb = b.data.shape_keys.key_blocks[opt['imya']]; b0 = len(T)
    T += [b.matrix_world @ d.co for d in kb.data]; TF += [[b0 + v for v in p.vertices] for p in b.data.polygons]
ZAD = [float(x) for x in opt['zad'].split(':')] if opt.get('zad') else None
K = float(opt['prizhim']); Mw = o.matrix_world; inv = Mw.inverted(); sd = []
if T:
    BV = BVHTree.FromPolygons(T, TF)
    for k, v in enumerate(bm.verts):
        if not SH[k]: v.co = Vector(V[k]); continue
        p = Mw @ Vector(V[k]); q = BV.find_nearest(p)[0]
        k_ = K
        # ЛЕНТА ВДОЛЬ СПИНЫ (`--zad z:k`, ГеймБосс по G2: «тонкая пластина от затылка, отстаёт на 2–4 см»): шерсть сзади выше `z`
        # прижимается сильнее (`k`), только то, что отстоит от тела больше 1 см
        if ZAD and q is not None and p.y > 0.03 and abs(p.x) < (ZAD[2] if len(ZAD) > 2 else 0.08) and p.z > ZAD[0] and (p - q).length > 0.01: k_ = min(K, ZAD[1])   # только полоса вдоль хребта: бока гривы держат силуэт (G3: IoU 0.679 → 0.588)
        p2 = q + (p - q) * k_ if q is not None else p; sd.append((p2 - p).length); v.co = inv @ p2
else:
    for k, v in enumerate(bm.verts): v.co = Vector(V[k])
bm.to_mesh(o.data); bm.free(); o.data.update()
print('ГРИВА-МАССА: %s — граней %d → %d, Таубин %s шагов, прижим ×%.2f (сдвиг средн. %.1f мм)' % (
    o.name, n0, len(o.data.polygons), opt['glad'], K, 1000 * float(np.mean(sd)) if sd else 0))
bpy.ops.wm.save_as_mainfile(filepath=out)
