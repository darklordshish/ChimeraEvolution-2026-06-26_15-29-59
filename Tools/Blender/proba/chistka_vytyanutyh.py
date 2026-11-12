"""ЧИСТКА ВЫТЯНУТЫХ ГРАНЕЙ (03.11, критик r2: «прорези — слиты тонкие длинные грани; детектор искал нормали, добавить вытянутость»). Тёмная «прорезь» на
крупных гранях — треугольник длиной 6–10 см и высотой 2–7 мм: он стоит ребром и ловит тень. Вытянутость = длинная сторона / высота на неё.
Для грани с вытянутостью > `--a`: (1) поворот длинного ребра, если пара граней почти плоская (объём тетраэдра < `--v` см³) и худшая вытянутость пары падает;
(2) иначе слияние короткого ребра: вершина вливается в соседа, линия перелома и край объекта не двигаются, ребро края не сливается. Цель — база = ключ (перед
шагом `prorezhivanie.py … 1`), остальные ключи — как у оставшейся вершины. Несколько проходов.

  blender -b вход.blend --factory-startup -P chistka_vytyanutyh.py -- выход.blend [--ob Сердце,хребет] [--a 6] [--v 1.5] [--prohody 6] [--imya Поджарый]
"""
import sys
import bpy, bmesh

a = sys.argv[sys.argv.index('--') + 1:]
out = a[0]
opt = dict(ob='Сердце,хребет', a='6', v='1.5', prohody='6', imya='Поджарый')
i = 1
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
A_, V_ = float(opt['a']), float(opt['v']) * 1e-6


def asp(p):
    e = [(p[0] - p[1]).length, (p[1] - p[2]).length, (p[2] - p[0]).length]; L = max(e); ar = (p[1] - p[0]).cross(p[2] - p[0]).length / 2
    return L * L / max(2 * ar, 1e-12)


for nm in opt['ob'].split(','):
    o = bpy.data.objects[nm]; bm = bmesh.new(); bm.from_mesh(o.data); al = bm.edges.layers.int.get('перелом'); n0 = len(bm.faces); rot = sl = 0
    sk = bm.verts.layers.shape.get(opt['imya']); P = lambda v: v[sk] if sk else v.co
    kraj = lambda v: any(e.is_boundary for e in v.link_edges)
    lin = lambda v: al is not None and any(e[al] > 0 for e in v.link_edges)
    for _ in range(int(opt['prohody'])):
        plohie = sorted([f for f in bm.faces if len(f.verts) == 3 and asp([P(v) for v in f.verts]) > A_], key=lambda f: -asp([P(v) for v in f.verts]))
        if not plohie: break
        trog = set()
        for f in plohie:
            if not f.is_valid or any(x in trog for x in f.verts): continue
            es = sorted(f.edges, key=lambda e: (P(e.verts[0]) - P(e.verts[1])).length); dl, kr = es[-1], es[0]
            sdelano = False
            if len(dl.link_faces) == 2 and (al is None or dl[al] == 0):
                g = dl.link_faces[0] if dl.link_faces[1] == f else dl.link_faces[1]
                if len(g.verts) == 3:
                    u, w = dl.verts; c = next(v for v in f.verts if v not in dl.verts); d = next(v for v in g.verts if v not in dl.verts)
                    c_ = P(c); vol = abs((P(u) - c_).dot((P(w) - c_).cross(P(d) - c_))) / 6
                    old = max(asp([P(u), P(w), c_]), asp([P(u), P(w), P(d)])); new = max(asp([c_, P(d), P(u)]), asp([c_, P(d), P(w)]))
                    if vol < V_ and new < old and bm.edges.get((c, d)) is None:
                        r = bmesh.ops.rotate_edges(bm, edges=[dl], use_ccw=False)
                        if r['edges']: rot += 1; sdelano = True; trog |= {u, w, c, d}
            if not sdelano and not kr.is_boundary:
                u, w = kr.verts
                if kraj(u) and kraj(w): continue
                if kraj(u) or (lin(u) and not kraj(w) and not lin(w)): keep, drop = u, w
                elif kraj(w) or lin(w): keep, drop = w, u
                else: keep, drop = u, w
                if lin(keep) and lin(drop) and (al is None or kr[al] == 0): continue
                trog |= {keep} | {e.other_vert(drop) for e in drop.link_edges}
                bmesh.ops.weld_verts(bm, targetmap={drop: keep}); sl += 1
    bm.to_mesh(o.data); bm.free(); o.data.update()
    print('ВЫТЯНУТЫЕ: %s — граней %d → %d, повёрнуто %d, слито %d' % (nm, n0, len(o.data.polygons), rot, sl))
bpy.ops.wm.save_as_mainfile(filepath=out)
