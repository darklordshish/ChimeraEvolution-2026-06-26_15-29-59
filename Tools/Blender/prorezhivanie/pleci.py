# -*- coding: utf-8 -*-
"""ПЛЕЧИ A′ И D ЭКСПЕРИМЕНТА КОНСИЛИУМА (геометр `07` §8.2 и §10.4; итог консилиума §5).

A′ — та же оболочка поля, но квад режется по кратчайшей диагонали: треугольники склеиваются обратно в квады
(`tris_to_quads`) и режутся заново `SHORTEST_DIAGONAL`. Эмуляция флага мешера.

D — «развёртка колец» (вариант В7): по нынешнему графу волка вдоль цепей разметки ставятся кольца; радиус каждой
вершины кольца снимается ЛУЧОМ с поля (мелкая клетка) — снаружи внутрь к оси, первое пересечение = внешняя оболочка
(у поля слоты перекрываются, изнутри луч упёрся бы во внутреннюю стенку соседа). Нет пересечения в пределах `Rmax` —
эллипс узла (r · section, r · depth). Кольца цепи соединяются квадами, концы — веером. Развилки (плечо, бедро, шея) —
грубо, перекрытием труб, как и разрешил геометр; детали (морда, уши, лапы) — блоки стенда, как у A и B.
Вершин в кольце: 12 у хребта и шеи, 8 у конечностей и хвоста (письмо 2026-10-01d §1.2).

Запуск:  blender -b -P pleci.py -- A1  поле.obj выход.obj
         blender -b -P pleci.py -- D   поле_мелкое.obj граф_мира.json выход.obj
  граф_мира.json — мировые узлы вида в осях OBJ стенда (выгрузка `DumpGraph`).
"""
import json
import math
import sys

import bmesh
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

a = sys.argv[sys.argv.index('--') + 1:]
mode = a[0]


def load_fields(path):
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    bpy.ops.wm.obj_import(filepath=path, forward_axis='NEGATIVE_Z', up_axis='Y')
    fields = [o for o in bpy.context.scene.objects if o.name.startswith('field')]
    for o in [o for o in bpy.context.scene.objects if not o.name.startswith('field')]:
        bpy.data.objects.remove(o, do_unlink=True)
    bpy.ops.object.select_all(action='DESELECT')
    for o in fields:
        o.select_set(True)
    bpy.context.view_layer.objects.active = fields[0]
    bpy.ops.object.join()
    f = bpy.context.view_layer.objects.active
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.remove_doubles(threshold=0.0005); bpy.ops.object.mode_set(mode='OBJECT')
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)   # импорт кладёт поворот осей в трансформ
    f.name = 'field'
    return f


def export(dst):
    bpy.ops.wm.obj_export(filepath=dst, export_selected_objects=False, forward_axis='NEGATIVE_Z', up_axis='Y',
                          export_materials=False, export_normals=False, export_uv=False)


def tris(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)


if mode == 'A1':
    f = load_fields(a[1])
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.tris_convert_to_quads(face_threshold=math.radians(60), shape_threshold=math.radians(60))
    bpy.ops.object.mode_set(mode='OBJECT')
    m = f.modifiers.new('tri', 'TRIANGULATE'); m.quad_method = 'SHORTEST_DIAGONAL'
    bpy.ops.object.modifier_apply(modifier=m.name)
    print('A1: %d тр' % tris(f))
    export(a[2])
    sys.exit(0)

# ── D: кольца ────────────────────────────────────────────────────────────────────────────────────────
f = load_fields(a[1])
# OBJ-импорт повернул оси (forward −Z, up Y → Blender Z вверх); граф — в осях OBJ. Переводим граф так же: (x, y, z)ᵒᵇʲ →
# (x, −z, y)ᴮˡᵉⁿᵈᵉʳ, а выход экспортируется обратно тем же экспортом
def B(p):
    return Vector((p[0], -p[2], p[1]))


bvh = BVHTree.FromObject(f, bpy.context.evaluated_depsgraph_get())
nodes = {n['name']: n for n in json.load(open(a[2], encoding='utf-8'))}
UP = Vector((0, 0, 1)); FWD = Vector((0, -1, 0))


def chain_points(names, start_from_a=True):
    pts = [B(nodes[names[0]]['a'])] if start_from_a else []
    for n in names:
        pts.append(B(nodes[n]['b']))
    return pts


def radius_at(names, i_seg, t):
    n = nodes[names[min(i_seg, len(names) - 1)]]
    return n['r0'] + (n['r1'] - n['r0']) * t


CHAINS = [  # (имя, точки оси, узлы для радиуса по умолчанию, вершин в кольце, колец на сегмент, «верх» — спина или перёд)
    ('хребет', [B(nodes['крестец']['b']), B(nodes['хребет']['a']), B(nodes['грудной']['a']), B(nodes['грудной']['b']),
                B(nodes['шея']['b']), B(nodes['голова']['b'])],
     ['крестец', 'хребет', 'грудной', 'шея', 'голова'], 12, [2, 4, 4, 4, 2], 'спина'),
    ('перед', chain_points(['лопатка', 'плечо', 'предплечье']), ['лопатка', 'плечо', 'предплечье'], 8, [3, 3, 3], 'перёд'),
    ('зад', chain_points(['бедро', 'голень']), ['бедро', 'голень'], 8, [4, 4], 'перёд'),
    ('хвост', chain_points(['хвост', 'хвост_кисть']), ['хвост', 'хвост_кисть'], 8, [3, 3], 'спина'),
]
MIRROR = {'перед', 'зад'}

bm = bmesh.new()
hits = misses = 0


def ring(c, t, up_hint, n, r_def, sec, dep):
    global hits, misses
    t = t.normalized()
    up = (up_hint - t * up_hint.dot(t))
    if up.length < 1e-4:
        up = FWD - t * FWD.dot(t)
    up.normalize(); side = t.cross(up)
    vs = []
    for j in range(n):
        th = 2 * math.pi * j / n
        d = (up * math.cos(th) + side * math.sin(th)).normalized()
        rmax = 3.0 * r_def
        loc, nor, idx, dist = bvh.ray_cast(c + d * rmax, -d, rmax)
        if loc is not None:
            r = rmax - dist; hits += 1
        else:
            e = (sec * math.sin(th)) ** 2 + (dep * math.cos(th)) ** 2
            r = r_def * math.sqrt(e); misses += 1
        vs.append(bm.verts.new(c + d * r))
    return vs


for name, pts, nn, n, ks, verh in CHAINS:
    for mirror in ([False, True] if name in MIRROR else [False]):
        P = [Vector((-p.x, p.y, p.z)) if mirror else p for p in pts]
        rings = []
        for s in range(len(P) - 1):
            k = ks[s]
            node = nodes[nn[s]]
            for i in range(k + (1 if s == len(P) - 2 else 0)):
                tt = i / k
                c = P[s].lerp(P[s + 1], tt)
                tan = (P[s + 1] - P[s])
                if i == 0 and s > 0:
                    tan = (P[s + 1] - P[s]).normalized() + (P[s] - P[s - 1]).normalized()
                hint = UP if verh == 'спина' else FWD
                r_def = node['r0'] + (node['r1'] - node['r0']) * min(1.0, tt)
                rings.append((c, ring(c, tan, hint, n, r_def, node['section'], node['depth'])))
        for (_, r0), (_, r1) in zip(rings, rings[1:]):
            for j in range(n):
                q = [r0[j], r0[(j + 1) % n], r1[(j + 1) % n], r1[j]]
                bm.faces.new(q[::-1] if mirror else q)
        for (c, r), flip in ((rings[0], True), (rings[-1], False)):
            cv = bm.verts.new(c)
            for j in range(n):
                tri = [cv, r[(j + 1) % n], r[j]] if flip else [cv, r[j], r[(j + 1) % n]]
                bm.faces.new(tri[::-1] if mirror else tri)

for o in list(bpy.data.objects):
    bpy.data.objects.remove(o, do_unlink=True)
me = bpy.data.meshes.new('field'); bm.to_mesh(me); bm.free()
ob = bpy.data.objects.new('field', me); bpy.context.collection.objects.link(ob)
bpy.context.view_layer.objects.active = ob; ob.select_set(True)
m = ob.modifiers.new('tri', 'TRIANGULATE'); bpy.ops.object.modifier_apply(modifier=m.name)
print('D: %d тр; лучей с попаданием %d, без %d' % (tris(ob), hits, misses))
export(a[3])
