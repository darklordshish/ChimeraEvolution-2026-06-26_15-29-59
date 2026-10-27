"""ГРИВА И ХВОСТ — МАССОЙ ПРЯДЕЙ, А НЕ СТРУЖКОЙ (27.10, ГеймБосс по P2: «осколки-шипы по всему контуру головы и плеч и на груди,
торчат на 2–5 см; на листе ступеней пряди крупные — укрупнить и прижать»). Шерстяной кусок режется из цели генератора с тонкими
прядями; на куске (до слияния со звериными кусками тела):

  1. УКРУПНЕНИЕ — прореживание схлопыванием рёбер до доли `--dolya` треугольников (веса костей Blender переносит сам);
  2. ШИПЫ — сглаживание Таубина `--glad` шагов (λ 0.5, μ −0.53: без усадки объёма) — тонкие кончики уходят в массу пряди;
  3. ПРИЖИМ — отступ шерсти от поверхности тела (форма-ключ `--imya` шасси) сжимается в `--prizhim` раз: грива ложится на объём.

  blender -b --factory-startup -P griva_massa.py -- вход.blend выход.blend [--kusok @Волк] [--imya Волк] [--dolya 0.35] [--glad 6]
      [--prizhim 0.8] [--ne_trogat голова,предплечье,…]
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
ids = [v.index for v in o.data.vertices if glavnaya(v) not in NT]
sherst.add(ids, 1.0, 'REPLACE')
# 1. укрупнение — только по группе «шерсть»
bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
m = o.modifiers.new('d', 'DECIMATE'); m.ratio = float(opt['dolya']); m.use_collapse_triangulate = True
m.vertex_group = 'шерсть'; m.vertex_group_factor = 1000.0
bpy.ops.object.modifier_apply(modifier='d')
gi = o.vertex_groups['шерсть'].index
SH = np.array([any(g.group == gi and g.weight > 0.5 for g in v.groups) for v in o.data.vertices])
o.vertex_groups.remove(o.vertex_groups['шерсть'])
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
K = float(opt['prizhim']); Mw = o.matrix_world; inv = Mw.inverted(); sd = []
if T:
    BV = BVHTree.FromPolygons(T, TF)
    for k, v in enumerate(bm.verts):
        if not SH[k]: v.co = Vector(V[k]); continue
        p = Mw @ Vector(V[k]); q = BV.find_nearest(p)[0]
        p2 = q + (p - q) * K if q is not None else p; sd.append((p2 - p).length); v.co = inv @ p2
else:
    for k, v in enumerate(bm.verts): v.co = Vector(V[k])
bm.to_mesh(o.data); bm.free(); o.data.update()
print('ГРИВА-МАССА: %s — граней %d → %d, Таубин %s шагов, прижим ×%.2f (сдвиг средн. %.1f мм)' % (
    o.name, n0, len(o.data.polygons), opt['glad'], K, 1000 * float(np.mean(sd)) if sd else 0))
bpy.ops.wm.save_as_mainfile(filepath=out)
