"""КОЛЕНО БЕЗ МАНЖЕТЫ (28.10, критик r2 п.1: «ст.2, анфас и сзади: манжета по кругу на высоте колена, верх шире бедра ~10 %,
ноги заправлены в голенища»; дизайн-линия 28.10c п.5: «порог и рябь на переходе голень → лапа»). Стенд без кусков `@Волк`
показал, что манжета — в самом теле: ключ подгонки снял с цели край лапы, обёрнутой вокруг голени. Шаг перед огранкой: в ключе
`--imya` зона колена — вершины, у которых вес голени между `--vesa` (переход бедро ↔ голень), плюс `--kolca` колец — сглаживается
Таубином (λ 0.5, μ −0.53: объём держится) с весом, затухающим к краю зоны; вне зоны ключ не трогается.

  blender -b --factory-startup -P glad_koleno.py -- вход.blend выход.blend [--imya Волк] [--obekt Ноги] [--vesa 0.03:0.97] [--kolca 3] [--shagi 12]
"""
import sys
import bpy, bmesh
import numpy as np

a = sys.argv[sys.argv.index('--') + 1:]
src, out = a[0], a[1]
opt = dict(imya='Волк', obekt='Ноги', vesa='0.03:0.97', kolca='3', shagi='12')
i = 2
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
w0, w1 = (float(x) for x in opt['vesa'].split(':'))
bpy.ops.wm.open_mainfile(filepath=src)
o = bpy.data.objects[opt['obekt']]; kb = o.data.shape_keys.key_blocks[opt['imya']]
gi = {g.name: g.index for g in o.vertex_groups}
GOL = {gi[n] for n in ('голень', 'голень.L') if n in gi}
bm = bmesh.new(); bm.from_mesh(o.data); bm.verts.ensure_lookup_table()
NB = [[e.other_vert(v).index for e in v.link_edges] for v in bm.verts]; bm.free()
wg = np.array([sum(g.weight for g in v.groups if g.group in GOL) for v in o.data.vertices])
zona = {k for k in range(len(wg)) if w0 < wg[k] < w1}
yadro = set(zona); kolco = {}
for r in range(1, int(opt['kolca']) + 1):
    nov = {n for k in zona for n in NB[k]} - zona
    for n in nov: kolco[n] = r
    zona |= nov
R = int(opt['kolca']) + 1
wz = np.zeros(len(wg))
for k in zona: wz[k] = 1.0 if k in yadro else 1 - kolco[k] / R
X = np.array([list(d.co) for d in kb.data]); X0 = X.copy()
for _ in range(int(opt['shagi'])):
    for lam in (0.5, -0.53):
        L = np.array([X[nb].mean(0) - X[k] if nb else np.zeros(3) for k, nb in enumerate(NB)]); X = X + (lam * wz)[:, None] * L
for k in zona: kb.data[k].co = X[k]
o.data.update()
sd = np.linalg.norm(X - X0, axis=1)[list(zona)]
print('КОЛЕНО: %s, ключ %s — зона %d вершин (переход %d + %d колец), сдвиг средн. %.1f, макс %.1f мм' % (
    o.name, kb.name, len(zona), len(yadro), R - 1, 1000 * sd.mean(), 1000 * sd.max()))
bpy.ops.wm.save_as_mainfile(filepath=out)
