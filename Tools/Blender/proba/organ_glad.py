"""ОРГАН НА ШАБЛОНЕ — БЕЗ СТРУЖКИ И ПАЛОЧЕК (27.10, ГеймБосс по ст.1: «гребень — стружка; когти на кистях — тонкие палочки, торчат
в сторону, укоротить»). Объект органа `<слот>@<вид>` (organ_objekt.py): база прижата к телу, форма-ключ «рост» — полная форма.
Правится только «рост»:
  - `--ukorot k` — рост = база + (рост − база)·k: орган вырастает на долю k (когти короче);
  - `--glad n` — Таубин по «росту» (λ 0.5, μ −0.53, края не двигаются): шипы стружки уходят в массу.

  blender -b --factory-startup -P organ_glad.py -- вход.blend выход.blend --obekt Руки@Волк [--ukorot 0.6] [--glad 0] [--k_telu 0.4]
"""
import sys
import bpy, bmesh
import numpy as np
from mathutils import Vector

a = sys.argv[sys.argv.index('--') + 1:]
src, out = a[0], a[1]
opt = dict(ukorot='1', glad='0')
i = 2
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
bpy.ops.wm.open_mainfile(filepath=src)
o = bpy.data.objects[opt['obekt']]; me = o.data
kb = me.shape_keys.key_blocks['рост']; B = np.array([list(v.co) for v in me.vertices]); R = np.array([list(d.co) for d in kb.data])
R = B + (R - B) * float(opt['ukorot'])
bm = bmesh.new(); bm.from_mesh(me); bm.verts.ensure_lookup_table()
NB = [[e.other_vert(v).index for e in v.link_edges] for v in bm.verts]; kraj = np.array([any(e.is_boundary for e in v.link_edges) for v in bm.verts]); bm.free()
for _ in range(int(opt['glad'])):
    for lam in (0.5, -0.53):
        L = np.array([R[nb_].mean(0) - R[k] if nb_ else np.zeros(3) for k, nb_ in enumerate(NB)]); L[kraj] = 0; R = R + lam * L
# `--k_telu k` — палочки когтей в самой БАЗЕ органа (рост у когтей ст.1 сдвигает до 3 мм — ukorot их не трогал, 27.10): вершины
# дальше 5 мм от тела шасси (база, без ключей) придвигаются к ближайшей точке тела в k раз — и в базе, и в «росте»
if opt.get('k_telu'):
    from mathutils.bvhtree import BVHTree
    T, TF = [], []
    for b in bpy.data.objects:
        if b.type != 'MESH' or '@' in b.name: continue
        b0 = len(T); T += [b.matrix_world @ v.co for v in b.data.vertices]; TF += [[b0 + v for v in p_.vertices] for p_ in b.data.polygons]
    BV = BVHTree.FromPolygons(T, TF); kt = float(opt['k_telu']); Mw = o.matrix_world; inv = Mw.inverted(); n_k = 0
    for arr in (B, R):
        for j in range(len(arr)):
            p_ = Mw @ Vector(arr[j]); q = BV.find_nearest(p_)[0]
            if q is not None and (p_ - q).length > 0.005: arr[j] = np.array(inv @ (q + (p_ - q) * kt)); n_k += 1
    for v, b_ in zip(me.vertices, B): v.co = Vector(b_)
    print('ОРГАН-ГЛАДЬ: %s — к телу ×%.2f, вершин %d' % (o.name, kt, n_k // 2))
for d, r in zip(kb.data, R): d.co = Vector(r)
print('ОРГАН-ГЛАДЬ: %s — рост ×%s, Таубин %s шагов' % (o.name, opt['ukorot'], opt['glad']))
bpy.ops.wm.save_as_mainfile(filepath=out)
