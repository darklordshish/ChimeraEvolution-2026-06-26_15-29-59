"""ПРОРЕЖИВАНИЕ ПО ФОРМЕ КЛЮЧА (30.10, решение геймдизайнера: «уплотнять — не low-poly уже, может даже меньше треугольников»):
база объектов := ключ `KEY`, затем квадратичное схлопывание рёбер (Blender decimate, доля `R`) на гранях, не касающихся края
объекта — кольца швов и их вершины не меняются. Крупные грани встают на плоском, рёбра остаются там, где поверхность
поворачивает; объём держится (огранка проекцией на плоскости срезала выпуклость: IoU 0.903 → 0.85–0.88). Остальные ключи
интерполируются; веса — 4 кости с перенормировкой.

  blender -b шасси.blend --factory-startup -P prorezhivanie.py -- выход.blend Поджарый Сердце,хребет,Руки 0.33
"""
import bpy, bmesh, sys
a=sys.argv[sys.argv.index('--')+1:]; out, KEY, OB, R = a[0], a[1], a[2].split(','), float(a[3])
for o in bpy.data.objects:
    if o.type!='MESH' or o.name not in OB: continue
    kb=o.data.shape_keys.key_blocks
    for i,v in enumerate(o.data.vertices): v.co=kb[KEY].data[i].co.copy(); kb[0].data[i].co=v.co.copy()
    n0=len(o.data.polygons)
    for x in bpy.context.view_layer.objects: x.select_set(False)
    bpy.context.view_layer.objects.active=o; o.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT'); bm=bmesh.from_edit_mesh(o.data)
    kraj={v for v in bm.verts if any(e.is_boundary for e in v.link_edges)}
    for f in bm.faces: f.select_set(not any(v in kraj for v in f.verts))
    bmesh.update_edit_mesh(o.data)
    bpy.ops.mesh.decimate(ratio=R)
    bpy.ops.mesh.select_all(action='SELECT'); bpy.ops.mesh.quads_convert_to_tris()
    bpy.ops.object.mode_set(mode='OBJECT')
    bpy.ops.object.vertex_group_limit_total(group_select_mode='ALL', limit=4)
    bpy.ops.object.vertex_group_normalize_all(group_select_mode='ALL', lock_active=False)
    print('D',o.name,n0,'→',len(o.data.polygons))
bpy.ops.wm.save_as_mainfile(filepath=out)
