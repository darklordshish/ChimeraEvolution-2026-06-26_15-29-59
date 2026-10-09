"""ПРОВЕРКА МЕША ДО РИГА (критик, проход 4: «проверить машиной»): неманифолд (рёбра не на двух гранях), граничные рёбра,
вырожденные грани, самопересечения (BVH), симметрия по X (вершина ↔ зеркальная), число островов.

  blender -b --factory-startup -P proverka_mesha.py -- меш.obj
Печатает строку «ПРОВЕРКА: …» и «ЧИСТО» / «ЕСТЬ ЗАМЕЧАНИЯ».
"""
import sys
import bpy, bmesh
from mathutils import kdtree
from mathutils.bvhtree import BVHTree

a = sys.argv[sys.argv.index('--') + 1:]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.wm.obj_import(filepath=a[0], forward_axis='NEGATIVE_Z', up_axis='Y')
ob = [o for o in bpy.data.objects if o.type == 'MESH'][0]
bm = bmesh.new(); bm.from_mesh(ob.data)
bm.verts.ensure_lookup_table()
nonman = [e for e in bm.edges if not e.is_manifold]
boundary = [e for e in bm.edges if e.is_boundary]
degen = [f for f in bm.faces if f.calc_area() < 1e-8]
bvh = BVHTree.FromBMesh(bm)
pairs = bvh.overlap(bvh)
# соседние грани (общая вершина) — не самопересечение
inter = [(i, j) for i, j in pairs if i < j and not set(v.index for v in bm.faces[i].verts) & set(v.index for v in bm.faces[j].verts)]
kd = kdtree.KDTree(len(bm.verts))
for v in bm.verts:
    kd.insert(v.co, v.index)
kd.balance()
asym = 0
for v in bm.verts:
    co, i, d = kd.find((-v.co.x, v.co.y, v.co.z))
    if d > 1e-4:
        asym += 1
islands = 0
seen = set()
for v in bm.verts:
    if v.index in seen:
        continue
    islands += 1
    st = [v]
    while st:
        u = st.pop()
        if u.index in seen:
            continue
        seen.add(u.index)
        st.extend(e.other_vert(u) for e in u.link_edges)
print('ПРОВЕРКА: граней %d, неманифолд %d, граничных %d, вырожденных %d, самопересечений %d, несимметричных вершин %d, островов %d'
      % (len(bm.faces), len(nonman), len(boundary), len(degen), len(inter), asym, islands))
for e in (nonman + boundary)[:12]:
    c = (e.verts[0].co + e.verts[1].co) / 2
    print('  ребро у (%.3f %.3f %.3f), граней %d' % (c.x, c.y, c.z, len(e.link_faces)))
print('ЧИСТО' if not (nonman or boundary or degen or inter or asym) else 'ЕСТЬ ЗАМЕЧАНИЯ')
