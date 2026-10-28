"""ГРИВА: ШОВ ПО ХРЕБТУ И ИГЛЫ (28.10, критик r2: п.9 «сзади вертикальная трещина по хребту между лопатками ~10 % корпуса»,
п.8 «тонкая палочка под подбородком»). Стенд без кусков `@Волк` их не показывает — оба в куске:
  1. ШОВ — у куска вдоль средней линии спины открытый край (левая и правая половины гривы не сшиты, z 1.21–1.50): вершины этого
     края (|x| < `--x`, сзади, высоты `--z`) сводятся на x = 0 и сливаются в пределах `--slit` м;
  2. ИГЛЫ — кончик, отстоящий от центра своих соседей больше `--igla` м и больше чем в 3 их радиуса (кончик края гривы на груди —
     23 см, в позе ст.2 с опущенной головой висит под челюстью), садится в этот центр; ниже `--igly_z` (когти, хвост) не трогается.

  blender -b --factory-startup -P griva_zashit.py -- вход.blend выход.blend [--kusok @Волк] [--x 0.06] [--z 1.15:1.75] [--slit 0.03]
      [--igla 0.03] [--igly_z 1.0:1.75]
"""
import sys
import bpy, bmesh
import numpy as np
from mathutils import Vector

a = sys.argv[sys.argv.index('--') + 1:]
src, out = a[0], a[1]
opt = dict(kusok='@Волк', x='0.06', z='1.15:1.75', slit='0.03', igla='0.03', igly_z='1.0:1.75')
i = 2
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
XM = float(opt['x']); Z0, Z1 = (float(v) for v in opt['z'].split(':')); IZ0, IZ1 = (float(v) for v in opt['igly_z'].split(':'))
bpy.ops.wm.open_mainfile(filepath=src)
o = bpy.data.objects[opt['kusok']]; M = o.matrix_world; Mi = M.inverted()
bm = bmesh.new(); bm.from_mesh(o.data); bm.verts.ensure_lookup_table()
# 1. шов по хребту
kr = [v for v in bm.verts if any(e.is_boundary for e in v.link_edges)]
sh = []
for v in kr:
    p = M @ v.co
    if abs(p.x) < XM and p.y > 0 and Z0 < p.z < Z1: sh.append(v)
for v in sh:
    p = M @ v.co; p.x = 0.0; v.co = Mi @ p
n0 = len(bm.verts)
bmesh.ops.remove_doubles(bm, verts=sh, dist=float(opt['slit']))
n_sl = n0 - len(bm.verts)
# 2. иглы
bm.verts.ensure_lookup_table(); n_ig = 0
for _ in range(3):
    P = np.array([list(M @ v.co) for v in bm.verts])
    for v in bm.verts:
        if not (IZ0 < P[v.index][2] < IZ1): continue
        nb = [e.other_vert(v).index for e in v.link_edges]
        if len(nb) < 2: continue
        c = P[nb].mean(0); d = np.linalg.norm(P[v.index] - c); r = np.mean(np.linalg.norm(P[nb] - c, axis=1)) + 1e-6
        if d > float(opt['igla']) and d / r > 3: v.co = Mi @ Vector(c); n_ig += 1
bm.to_mesh(o.data); bm.free(); o.data.update()
print('ГРИВА-ШОВ: %s — край по хребту %d вершин сведён на x = 0, слито %d; игл посажено %d' % (o.name, len(sh), n_sl, n_ig))
bpy.ops.wm.save_as_mainfile(filepath=out)
