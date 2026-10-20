"""УПЛОТНИТЬ СЕТКУ К2 ПОД ШЕСТЬ ОБЛАСТЕЙ МАСС (20.10, консилиум формы: решение «шаблон = шасси», направление Д — грудная
клетка, связь грудь–живот–таз, лопатки и плечи раздельно, шея, основания конечностей, переходы). Опыт подгонки
(`Референсы/работа/модельная/2026-10-20-oboroten-telo-cel/`) показал: подгонка больше не мешает, а на груди и спине К2 нет
вершин, чтобы описать клин груди, раздел лопаток и талию (торс ~200 вершин).

Внутренние рёбра выбранных объектов делятся пополам (`subdivide_edges`, веса костей и формы-ключи интерполируются);
рёбра на краю объекта (швы с соседями: шея–голова, рука–корпус, нога–корпус) НЕ делятся — число вершин на швах остаётся
прежним, куски и паспорта швов садятся как раньше. Зона задаётся объектом и полосой по высоте.

  blender -b --factory-startup -P uplotnit_k2.py -- вход.blend выход.blend [--zony хребет:0:9,Сердце:0:9,шея:0:9,Руки:1.15:9,Ноги:0:0.62] [--raz 1]
"""
import sys
import bpy, bmesh

a = sys.argv[sys.argv.index('--') + 1:]
src, out = a[0], a[1]
opt = {}
i = 2
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
ZONY = [(z.split(':')[0], float(z.split(':')[1]), float(z.split(':')[2]))
        for z in opt.get('zony', 'хребет:0:9,Сердце:0:9,шея:0:9,Руки:1.15:9,Ноги:0:0.62').split(',')]
RAZ = int(opt.get('raz', '1'))

bpy.ops.wm.open_mainfile(filepath=src)
for name, z0, z1 in ZONY:
    o = bpy.data.objects.get(name)
    if o is None:
        print('УПЛОТН: нет объекта', name); continue
    n0 = len(o.data.vertices)
    for _ in range(RAZ):
        bm = bmesh.new(); bm.from_mesh(o.data)
        mw = o.matrix_world
        edges = [e for e in bm.edges if not e.is_boundary and all(z0 <= (mw @ v.co).z <= z1 for v in e.verts)]
        bmesh.ops.subdivide_edges(bm, edges=edges, cuts=1, use_grid_fill=False)
        bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 3])
        bm.to_mesh(o.data); bm.free(); o.data.update()
    print('УПЛОТН: %s z %.2f–%.2f: вершин %d → %d' % (name, z0, z1, n0, len(o.data.vertices)))
bpy.ops.wm.save_as_mainfile(filepath=out)
print('УПЛОТН: готово →', out)
