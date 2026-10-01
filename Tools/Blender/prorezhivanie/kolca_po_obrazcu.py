# -*- coding: utf-8 -*-
"""ПЛЕЧО D′ ЭКСПЕРИМЕНТА КОНСИЛИУМА (письмо механик 2026-10-02 §2): кольца тем же способом, что D, но по ФОРМЕ ЛИСТА —
лучами по 3D-образцу (выровненному `obrazec_v_obj.py`), а не по полю капсул. Вопрос: держат ли кольца форму листа.

Оси берутся С ОБРАЗЦА, а не с нашего графа (ноги, шея и хвост у образца стоят не там):
  • брюхо — нижний край туши по средней линии (5-й перцентиль высоты в средней трети длины);
  • ноги — вершины ниже брюха и в стороне от средней линии, четыре группы (лево/право × перёд/зад), ось — центры слоёв по
    высоте от земли до брюха, продолжена в тушу на 15 см (стык — перекрытием, как у D);
  • хребет с шеей и головой — центры слоёв вдоль длины по вершинам выше брюха, от крупа до носа;
  • хвост — вершины позади крупа ниже брюха у средней линии, ось — центры слоёв по высоте сверху вниз.
Кольцо — n вершин; радиус каждой — лучом снаружи к оси (первое пересечение — внешняя оболочка), предел — 1.8 полуразмаха
слоя; промах — радиус слоя. Вершин в кольце: 16 у хребта (письмо 01d §1.2: пояс груди), 8 у ног и хвоста.

Запуск:  blender -b -P kolca_po_obrazcu.py -- образец_выровненный.obj выход.obj [шаг_хребта шаг_ног]
"""
import math
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

a = sys.argv[sys.argv.index('--') + 1:]
src, dst = a[0], a[1]
DZ = float(a[2]) if len(a) > 2 else 0.07
DY = float(a[3]) if len(a) > 3 else 0.06

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.wm.obj_import(filepath=src, forward_axis='NEGATIVE_Z', up_axis='Y')
f = [o for o in bpy.context.scene.objects if o.type == 'MESH'][0]
bpy.context.view_layer.objects.active = f; f.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
bvh = BVHTree.FromObject(f, bpy.context.evaluated_depsgraph_get())
Pb = np.array([tuple(v.co) for v in f.data.vertices])
P = np.column_stack([Pb[:, 0], Pb[:, 2], -Pb[:, 1]])           # Blender → оси OBJ стенда: Y вверх, Z — к носу


def B(p):                                                      # оси OBJ → Blender
    return Vector((p[0], -p[2], p[1]))


zmin, zmax = P[:, 2].min(), P[:, 2].max(); L = zmax - zmin
mid = (np.abs(P[:, 0]) < 0.04) & (P[:, 2] > zmin + L / 3) & (P[:, 2] < zmax - L / 3)
belly = float(np.percentile(P[mid, 1], 5))
print('брюхо %.3f, длина %.3f' % (belly, L))

# ── оси ──
legs_v = P[(P[:, 1] < belly - 0.03) & (np.abs(P[:, 0]) > 0.055)]
zc = float(np.median(legs_v[:, 2]))
chains = []
def spread(s, c, cols):
    return float(np.percentile(np.linalg.norm(s[:, cols] - c, axis=1), 90))


# НОГА ПО СЛЕДУ: начало — стопа (слой у земли, там нет ни хвоста, ни соседней ноги), дальше вверх в слой берутся только
# точки рядом с центром предыдущего слоя — иначе в «заднюю ногу» затекает хвост, и кольца раздуваются в диски
for sx in (1, -1):
    for front in (True, False):
        g = legs_v[(np.sign(legs_v[:, 0]) == sx) & ((legs_v[:, 2] > zc) == front)]
        foot = g[g[:, 1] < 0.08]
        c = np.median(foot[:, [0, 2]], axis=0)
        pts = []
        for y in np.arange(0.02, belly - 0.02, DY):
            s = g[(g[:, 1] >= y - DY / 2) & (g[:, 1] < y + DY / 2)]
            s = s[np.linalg.norm(s[:, [0, 2]] - c, axis=1) < 0.13]
            if len(s) > 20:
                c = s[:, [0, 2]].mean(0)
                pts.append((c[0], y, c[1], spread(s, c, [0, 2])))
        top = pts[-1]
        pts.append((top[0], top[1] + 0.15, top[2], top[3]))
        chains.append(('нога', pts[::-1], 8))                 # сверху вниз

body = P[P[:, 1] > belly]
tail_v = P[(P[:, 1] < belly) & (np.abs(P[:, 0]) < 0.07) & (P[:, 2] < legs_v[:, 2].min() + 0.02)]
spine = []
for z in np.arange(body[:, 2].min() + DZ / 2, zmax, DZ):
    s = body[(body[:, 2] >= z - DZ / 2) & (body[:, 2] < z + DZ / 2)]
    if len(s) > 20:
        c = s.mean(0)
        spine.append((0.0, c[1], z, spread(s, c[:2], [0, 1])))
spine.append((0.0, spine[-1][1], zmax - 0.01, spine[-1][3] * 0.5))
chains.append(('хребет', spine, 16))
if len(tail_v) > 50:
    tail = []
    for y in np.arange(tail_v[:, 1].max(), tail_v[:, 1].min(), -DY):
        s = tail_v[(tail_v[:, 1] <= y + DY / 2) & (tail_v[:, 1] > y - DY / 2)]
        if len(s) > 20:
            c = s[:, [0, 2]].mean(0); tail.append((c[0], y, c[1], spread(s, c, [0, 2])))
    sp0 = spine[0]
    chains.append(('хвост', [(0.0, sp0[1], sp0[2], sp0[3] * 0.6)] + tail, 8))

# ── кольца ──
bm = bmesh.new(); hits = misses = 0
UP = Vector((0, 0, 1)); FWD = Vector((0, -1, 0))
for name, pts, n in chains:
    C = [B(p[:3]) for p in pts]
    rings = []
    for i, p in enumerate(pts):
        t = (C[min(i + 1, len(C) - 1)] - C[max(i - 1, 0)]).normalized()
        hint = UP if name != 'нога' else FWD
        up = hint - t * hint.dot(t)
        if up.length < 1e-4:
            up = FWD - t * FWD.dot(t)
        up.normalize(); side = t.cross(up)
        rmax = max(0.04, 1.8 * p[3]); rmax = min(rmax, 0.2) if name != 'хребет' else rmax; vs = []
        for j in range(n):
            th = 2 * math.pi * j / n
            d = (up * math.cos(th) + side * math.sin(th)).normalized()
            loc, nor, idx, dist = bvh.ray_cast(C[i] + d * rmax, -d, rmax)
            r = (rmax - dist) if loc is not None else p[3]
            hits += loc is not None; misses += loc is None
            vs.append(bm.verts.new(C[i] + d * r))
        rings.append((C[i], vs))
    for (_, r0), (_, r1) in zip(rings, rings[1:]):
        for j in range(n):
            bm.faces.new([r0[j], r0[(j + 1) % n], r1[(j + 1) % n], r1[j]])
    for (c, r), first in ((rings[0], True), (rings[-1], False)):
        cv = bm.verts.new(c)
        for j in range(n):
            bm.faces.new([cv, r[(j + 1) % n], r[j]] if first else [cv, r[j], r[(j + 1) % n]])

for o in list(bpy.data.objects):
    bpy.data.objects.remove(o, do_unlink=True)
me = bpy.data.meshes.new('field'); bm.normal_update(); bm.to_mesh(me); bm.free()
ob = bpy.data.objects.new('field', me); bpy.context.collection.objects.link(ob)
bpy.context.view_layer.objects.active = ob; ob.select_set(True)
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT'); bpy.ops.mesh.normals_make_consistent(inside=False)
bpy.ops.object.mode_set(mode='OBJECT')
m = ob.modifiers.new('tri', 'TRIANGULATE'); bpy.ops.object.modifier_apply(modifier=m.name)
print("D': %d тр; цепей %d (%s); лучей с попаданием %d, без %d" % (sum(len(p.vertices) - 2 for p in ob.data.polygons),
      len(chains), ', '.join('%s %d колец' % (c[0], len(c[1])) for c in chains), hits, misses))
bpy.ops.wm.obj_export(filepath=dst, export_selected_objects=False, forward_axis='NEGATIVE_Z', up_axis='Y',
                      export_materials=False, export_normals=False, export_uv=False)
