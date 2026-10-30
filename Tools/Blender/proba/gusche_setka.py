"""ГУЩЕ СЕТКА (30.10, проба плотности для рельефа поджарого): рёбра длиннее L м внутри объектов делятся пополам `PR` проходов
(bmesh subdivide, сетка-заливка, n-угольники — в треугольники). Края объектов (кольца швов) не трогаются: краевые вершины
сохраняют индекс и место. Ключи и веса bmesh интерполирует линейно — все ключи переносятся точно; веса затем
ограничиваются 4 костями с перенормировкой (средние точки смешивали наборы соседей).

  blender -b шасси.blend --factory-startup -P gusche_setka.py -- выход.blend Сердце,хребет,Руки 0.016 2
"""
# линейное деление длинных рёбер внутри объектов (края — кольца швов — не трогаются); ключи и веса bmesh интерполирует
import bpy, bmesh, sys
a=sys.argv[sys.argv.index('--')+1:]; out=a[0]; OB=a[1].split(','); L=float(a[2]); PR=int(a[3])
for o in bpy.data.objects:
    if o.name not in OB: continue
    for _ in range(PR):
        bm=bmesh.new(); bm.from_mesh(o.data)
        ed=[e for e in bm.edges if not e.is_boundary and (o.matrix_world@e.verts[0].co-o.matrix_world@e.verts[1].co).length>L]
        bmesh.ops.subdivide_edges(bm, edges=ed, cuts=1, use_grid_fill=True)
        bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts)>4])
        bm.to_mesh(o.data); bm.free(); o.data.update()
    print('G',o.name,len(o.data.polygons),'граней', 'краёв', sum(1 for e in o.data.edges if e.use_seam), len(o.data.vertices))
bpy.ops.wm.save_as_mainfile(filepath=out)
# не больше 4 костей на вершину (средние точки смешивали наборы костей соседей), с перенормировкой
for o in bpy.data.objects:
    if o.name not in OB: continue
    for x in bpy.context.view_layer.objects: x.select_set(False)
    bpy.context.view_layer.objects.active=o; o.select_set(True)
    bpy.ops.object.vertex_group_limit_total(group_select_mode='ALL', limit=4)
    bpy.ops.object.vertex_group_normalize_all(group_select_mode='ALL', lock_active=False)
bpy.ops.wm.save_as_mainfile(filepath=out)
