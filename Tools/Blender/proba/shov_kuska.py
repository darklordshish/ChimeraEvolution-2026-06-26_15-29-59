"""ШОВ КУСКА — БЕЗ СТУПЕНИ (27.10, критик ряда, пп. 3 и 6: «шов-сапог на голени — звериная стопа вставлена в ногу, кусок шире на
10–15 %», «хвост висит с просветом у корня, сзади в нём дыра»). В куске `@Волк` открытый край в выбранных областях (главная
кость вершины из `--kosti`) и полоса у него тянутся к поверхности тела шасси (форма-ключ `--imya`): у самого края — на тело, к концу
полосы `--polosa` — по-старому. Окружность куска на шве совпадает с телом, ступени нет. `--zalit 1` — открытые края в этих
областях заливаются (дыра хвоста при отсечении задних граней).

  blender -b --factory-startup -P shov_kuska.py -- вход.blend выход.blend --kosti голень,голень.L,стопа,стопа.L [--imya Волк]
      [--kusok @Волк] [--polosa 0.05] [--zalit 0]
"""
import sys
import bpy, bmesh
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

a = sys.argv[sys.argv.index('--') + 1:]
src, out = a[0], a[1]
opt = dict(imya='Волк', kusok='@Волк', polosa='0.05', zalit='0')
i = 2
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
KO = set(opt['kosti'].split(',')); POL = float(opt['polosa'])
bpy.ops.wm.open_mainfile(filepath=src)
o = bpy.data.objects[opt['kusok']]; Mw = o.matrix_world; inv = Mw.inverted()
gn = {g.index: g.name for g in o.vertex_groups}
glav = [gn[max(v.groups, key=lambda g: g.weight).group] if v.groups else '' for v in o.data.vertices]
T, TF = [], []
for b in bpy.data.objects:
    if b.type != 'MESH' or '@' in b.name or not b.data.shape_keys or opt['imya'] not in b.data.shape_keys.key_blocks: continue
    kb = b.data.shape_keys.key_blocks[opt['imya']]; b0 = len(T)
    T += [b.matrix_world @ d.co for d in kb.data]; TF += [[b0 + v for v in p.vertices] for p in b.data.polygons]
BV = BVHTree.FromPolygons(T, TF)
bm = bmesh.new(); bm.from_mesh(o.data); bm.verts.ensure_lookup_table()
kraj = [v for v in bm.verts if glav[v.index] in KO and any(e.is_boundary for e in v.link_edges)]
KP = np.array([list(Mw @ v.co) for v in kraj]) if kraj else np.zeros((0, 3))
n_t, sd = 0, []
if len(KP):
    for v in bm.verts:
        if glav[v.index] not in KO: continue
        p = Mw @ v.co; d = float(np.min(np.linalg.norm(KP - np.array(p), axis=1)))
        if d > POL: continue
        q = BV.find_nearest(p)[0]
        if q is None: continue
        w = 1.0 - d / POL; p2 = p.lerp(q, w); sd.append((p2 - p).length); v.co = inv @ p2; n_t += 1
n_z = 0
if opt['zalit'] == '1':
    ed = [e for e in bm.edges if e.is_boundary and all(glav[v.index] in KO for v in e.verts)]
    r = bmesh.ops.holes_fill(bm, edges=ed, sides=0); n_z = len(r.get('faces', []))
    bmesh.ops.triangulate(bm, faces=r.get('faces', []))
bm.to_mesh(o.data); bm.free(); o.data.update()
print('ШОВ: %s (%s) — край %d вершин, притянуто %d (сдвиг средн. %.1f, макс %.1f мм), залито граней %d' % (
    o.name, opt['kosti'], len(kraj), n_t, 1000 * float(np.mean(sd)) if sd else 0, 1000 * max(sd, default=0), n_z))
bpy.ops.wm.save_as_mainfile(filepath=out)
