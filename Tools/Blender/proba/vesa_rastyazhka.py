"""ВЕСА КУСКА БЕЗ СКАЧКОВ (28.10, критик r2 п.8: «ст.2, анфас — тонкая палочка под подбородком»; стенд `--rastyazhka`: у `@Волк`
142 ребра в позе ст.2 длиннее, чем в покое, в 15–23 раза — ребро 6 мм в покое между вершиной на голове и вершиной на шее,
голова в позе опущена — столбик 14 см под челюстью; так же складки воротника на 1.42–1.47 м). Веса кусков сняты с цели по
вершинам и на коротких рёбрах скачут с кости на кость.

Зона — вершины рёбер короче `--dlina` м в покое, у которых веса концов различаются больше `--skachok` (сумма модулей), плюс
`--kolca` колец; в зоне веса диффундируют `--shagi` шагов (среднее с соседями пополам), затем не больше 4 костей и нормировка.
Шаг идёт ПОСЛЕ vesa_final без перевязки: поза кусков должна поменяться — именно в ней был столбик.

  blender -b --factory-startup -P vesa_rastyazhka.py -- вход.blend выход.blend [--kusok @Волк] [--dlina 0.015] [--skachok 1.0]
      [--kolca 2] [--shagi 12] [--udalit 1]
"""
import sys
import bpy, bmesh
import numpy as np

a = sys.argv[sys.argv.index('--') + 1:]
src, out = a[0], a[1]
opt = dict(kusok='@Волк', dlina='0.015', skachok='1.0', kolca='2', shagi='12')
i = 2
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
bpy.ops.wm.open_mainfile(filepath=src)
itog = []
for kn in opt['kusok'].split(','):
    o = bpy.data.objects[kn]; me = o.data; ng = len(o.vertex_groups)
    W = np.zeros((len(me.vertices), ng))
    for v in me.vertices:
        for g in v.groups: W[v.index, g.group] = g.weight
    bm = bmesh.new(); bm.from_mesh(me); bm.verts.ensure_lookup_table()
    NB = [[e.other_vert(v).index for e in v.link_edges] for v in bm.verts]
    zona = set()
    for e in bm.edges:
        u, v = e.verts[0].index, e.verts[1].index
        if e.calc_length() < float(opt['dlina']) and np.abs(W[u] - W[v]).sum() > float(opt['skachok']): zona |= {u, v}
    n_jad = len(zona)
    # `--udalit 1` — перемычку не сглаживать, а вырезать: грани, у которых есть короткое ребро со скачком весов (диффузия весов
    # растягивала столбик под челюстью в ленту до груди — проба 28.10)
    if opt.get('udalit') == '1':
        ud = [f for f in bm.faces if any(e.calc_length() < float(opt['dlina']) and np.abs(W[e.verts[0].index] - W[e.verts[1].index]).sum() > float(opt['skachok']) for e in f.edges)]
        bmesh.ops.delete(bm, geom=ud, context='FACES')
        bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
        bm.to_mesh(me); bm.free(); me.update()
        itog.append('%s: перемычек вырезано граней %d' % (kn, len(ud))); continue
    for _ in range(int(opt['kolca'])): zona |= {n for k in list(zona) for n in NB[k]}
    bm.free()
    z = sorted(zona)
    for _ in range(int(opt['shagi'])):
        W2 = W.copy()
        for k in z:
            if NB[k]: W2[k] = 0.5 * W[k] + 0.5 * W[NB[k]].mean(0)
        W = W2
    for k in z:
        top = np.argsort(-W[k])[:4]; w = np.zeros(ng); w[top] = np.clip(W[k][top], 0, None); s = w.sum() or 1; w /= s
        for g in list(me.vertices[k].groups): o.vertex_groups[g.group].remove([k])
        for gi in np.nonzero(w > 1e-4)[0]: o.vertex_groups[int(gi)].add([k], float(w[gi]), 'REPLACE')
    me.update()
    itog.append('%s: скачков на коротких рёбрах — вершин %d, зона с кольцами %d' % (kn, n_jad, len(z)))
print('ВЕСА-РАСТЯЖКА: ' + '; '.join(itog))
bpy.ops.wm.save_as_mainfile(filepath=out)
