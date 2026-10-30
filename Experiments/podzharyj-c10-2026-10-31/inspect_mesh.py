"""Read-only mesh diagnostics in the evaluated Podzhary key; output paths explicit."""
import bpy, bmesh, sys, json, math
from mathutils import Vector
args = sys.argv[sys.argv.index('--') + 1:]
bpy.ops.wm.open_mainfile(filepath=args[0])
report = []
for o in bpy.data.objects:
    if o.type != 'MESH' or '@' in o.name or not o.data.shape_keys:
        continue
    kb = o.data.shape_keys.key_blocks.get('Поджарый')
    if kb is None:
        continue
    bm = bmesh.new(); bm.from_mesh(o.data)
    bm.verts.ensure_lookup_table(); bm.faces.ensure_lookup_table()
    for v in bm.verts:
        v.co = o.matrix_world @ kb.data[v.index].co
    bm.normal_update()
    bad = []
    for f in bm.faces:
        c = f.calc_center_median()
        if not .95 < c.z < 1.6 or o.name not in ('Сердце', 'хребет', 'Руки'):
            continue
        neighbors = {g for v in f.verts for g in v.link_faces if g != f}
        n = sum((g.normal * g.calc_area() for g in neighbors), Vector())
        angle = math.degrees(f.normal.angle(n)) if n.length > 1e-10 else 0
        if angle > 35:
            bad.append(dict(face=f.index, center=list(c), normal=list(f.normal), angle=angle,
                            verts=[v.index for v in f.verts], area=f.calc_area()))
    report.append(dict(object=o.name, vertices=len(bm.verts), triangles=sum(len(f.verts)-2 for f in bm.faces),
                       boundaries=sum(e.is_boundary for e in bm.edges),
                       nonmanifold=sum(not e.is_manifold and not e.is_boundary for e in bm.edges),
                       outliers=sorted(bad, key=lambda x:-x['angle'])))
    bm.free()
with open(args[1], 'w', encoding='utf-8') as f:
    json.dump(report, f, ensure_ascii=False, indent=2)
for r in report:
    print('MESH', r['object'], r['vertices'], r['triangles'], 'nonmanifold', r['nonmanifold'])
    for q in r['outliers'][:5]: print('OUTLIER', q)
