"""ЛОЖЕ ОРГАНА НА ГОЛОВЕ — ЗАПЛАТКОЙ (28.10c, дизайн-линия, повтор осмотра дыр п.3: «светлая полоса у виска 0.4–0.6H» ст.1; детектор —
изнанка головы на высоте ушей до 26 см²). У шаблона К2 под человеческим ухом (объект `Чутьё`) на голове — чашка-гнездо: 30 граней,
обращённых внутрь. При ухе её не видно; на ст.1 человеческое ухо скрыто волчьим органом `Чутьё@Волк` — и гнездо светит изнанкой.
Гнездо заглаживается: грани, у которых согласование нормалей переворачивает нормаль, — область; её внутренние вершины (край области
на месте) сглаживаются Лапласом во ВСЕХ формах-ключах и базе, затем нормали наружу.

  blender -b --factory-startup -P zakryt_lozhe.py -- вход.blend выход.blend [--obekt голова] [--shagi 40]
"""
import sys
import bpy, bmesh
import numpy as np

a = sys.argv[sys.argv.index('--') + 1:]
src, out = a[0], a[1]
opt = dict(obekt='голова', shagi='40')
i = 2
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
bpy.ops.wm.open_mainfile(filepath=src)
o = bpy.data.objects[opt['obekt']]
bm = bmesh.new(); bm.from_mesh(o.data); bm.faces.ensure_lookup_table()
N0 = [f.normal.copy() for f in bm.faces]
bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
lozhe = {f.index for f, n in zip(bm.faces, N0) if f.normal.dot(n) < 0}
if not lozhe: raise SystemExit('ЛОЖЕ: на %s вывернутых граней нет' % o.name)
vl = {v.index for f in lozhe for v in bm.faces[f].verts}
# край области — вершины, у которых есть грань вне ложа: держат шов с остальной головой
bm.verts.ensure_lookup_table()
kraj = {v for v in vl if any(f.index not in lozhe for f in bm.verts[v].link_faces)}
# шов с другими объектами (ухо `Чутьё` пришито к краю гнезда) сглаживается вместе с гнездом, а пришитые вершины уха едут за ним в
# тех же формах-ключах — иначе на ст.0 ухо отходило (край 98 мм, 28.10c); при закреплённом шве гнездо почти не заглаживалось
from mathutils import kdtree
dr = [(b_, v.index, b_.matrix_world @ v.co) for b_ in bpy.data.objects if b_.type == 'MESH' and b_ != o and '@' not in b_.name for v in b_.data.vertices]
kd = kdtree.KDTree(len(dr))
for j, x in enumerate(dr): kd.insert(x[2], j)
kd.balance()
shov = {}
for v in vl:
    for _, j, d_ in kd.find_range(o.matrix_world @ bm.verts[v].co, 0.001): shov.setdefault(v, []).append(dr[j][:2])
vn = sorted(vl - kraj)
NB = {v: [e.other_vert(bm.verts[v]).index for e in bm.verts[v].link_edges] for v in vn}
bm.to_mesh(o.data); bm.free()                       # нормали согласованы — запись порядка вершин граней
sloi = [kb.data for kb in o.data.shape_keys.key_blocks] if o.data.shape_keys else [o.data.vertices]
for d in sloi:
    X = np.array([list(x.co) for x in d])
    for _ in range(int(opt['shagi'])):
        for v in vn: X[v] = X[NB[v]].mean(0)
    for v in vn: d[v].co = X[v]
if o.data.shape_keys:                                # база меша = Basis
    for v in vn: o.data.vertices[v].co = o.data.shape_keys.key_blocks[0].data[v].co
n_sh = 0
for v in vn:
    for b_, j in shov.get(v, []):
        n_sh += 1
        if o.data.shape_keys and b_.data.shape_keys:
            for kb in o.data.shape_keys.key_blocks:
                kb2 = b_.data.shape_keys.key_blocks.get(kb.name)
                if kb2: kb2.data[j].co = b_.matrix_world.inverted() @ (o.matrix_world @ kb.data[v].co)
        b_.data.vertices[j].co = b_.matrix_world.inverted() @ (o.matrix_world @ o.data.vertices[v].co)
        b_.data.update()
o.data.update()
print('ЛОЖЕ: %s — вывернутых граней %d, заглажено вершин %d (край %d на месте), пришитых вершин других объектов сдвинуто %d, слоёв %d' % (o.name, len(lozhe), len(vn), len(kraj), n_sh, len(sloi)))
bpy.ops.wm.save_as_mainfile(filepath=out)
