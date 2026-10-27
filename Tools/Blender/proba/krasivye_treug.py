"""КРАСИВЫЕ ТРЕУГОЛЬНИКИ ШАБЛОНА (27.10, ГеймБосс по P2: «пила из мелких треугольников поперёк живота»; диагноз — длинные тонкие
треугольники К2 лежат поперёк двух плоскостей огранки и получают промежуточную нормаль — зубец; часть родил перенос линий
перелома, сдвинув вершины на линии). Рёбра внутри объектов шасси поворачиваются (bmesh beautify: триангуляция ближе к
равносторонней) — число вершин, кольца швов, края объектов, рёбра линий перелома (`перелом` > 0) не трогаются.

  blender -b --factory-startup -P krasivye_treug.py -- вход.blend выход.blend [--obekty Сердце,хребет,Ноги,Руки,шея]
"""
import sys
import bpy, bmesh

a = sys.argv[sys.argv.index('--') + 1:]
src, out = a[0], a[1]
opt = dict(obekty='Сердце,хребет,Ноги,Руки,шея')
i = 2
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
bpy.ops.wm.open_mainfile(filepath=src)
for nm in opt['obekty'].split(','):
    o = bpy.data.objects.get(nm)
    if o is None or o.type != 'MESH': continue
    me = o.data
    at = me.attributes.get('перелом'); lin = [0] * len(me.edges)
    if at: at.data.foreach_get('value', lin)
    if me.shape_keys:
        raise SystemExit('КРАСИВЫЕ: %s несёт формы-ключи — шаг только для шаблона до подгонки' % nm)
    bm = bmesh.new(); bm.from_mesh(me); bm.edges.ensure_lookup_table(); bm.faces.ensure_lookup_table()
    lin_k = {frozenset((e.verts[0].index, e.verts[1].index)): s_ for e, s_ in zip(bm.edges, lin) if s_ > 0}
    mozhno = [e for e in bm.edges if e.is_manifold and not e.is_boundary and frozenset((e.verts[0].index, e.verts[1].index)) not in lin_k
              and all(len(f.verts) == 3 for f in e.link_faces)]
    n0 = len(bm.faces)
    r = bmesh.ops.beautify_fill(bm, faces=[f for f in bm.faces if len(f.verts) == 3], edges=mozhno, use_restrict_tag=False)
    bm.to_mesh(me); bm.free(); me.update()
    # атрибут линий — заново по паре вершин (рёбра линий не поворачивались, но индексы рёбер сменились), сорт Г/В — прежний
    at = me.attributes.get('перелом') or me.attributes.new('перелом', 'INT', 'EDGE')
    at.data.foreach_set('value', [lin_k.get(frozenset((e.vertices[0], e.vertices[1])), 0) for e in me.edges])
    for e in me.edges: e.use_edge_sharp = lin_k.get(frozenset((e.vertices[0], e.vertices[1])), 0) == 2
    print('КРАСИВЫЕ: %s — граней %d, повёрнуто рёбер %d' % (nm, n0, len(r.get('geom', []))))
bpy.ops.wm.save_as_mainfile(filepath=out)
