"""РЕЛЬЕФ ЧЕШУИ ПО СЕТКЕ — ГЕОМЕТРИЯ CODEX К БЮДЖЕТУ (06.11, ГеймБосс: «не изобретай заново»). Объект `Шкура.рельеф-проба@Змея` из пробы Codex (`ChimeraEvolution-codex/Experiments/zmeya-relief-grid-2026-11-06/optimized-preview.blend`, 8 848 тр) переносится в сборку и сводится к бюджету планарным прореживанием: почти плоские соседние грани сливаются, а защищённые рёбра остаются — границы тонов (материал) и рёбра с изломом больше `--zashch` (киль и края ромбов). Порог слияния подбирается под бюджет. Веса — с ближайшей точки кожи тела (объекты `--telo`), барицентрически. На выходе тоны `Шкура.тон<N>@Змея` по материалам Codex: прежние тоны змеи заменяются.

  blender -b сборка.blend --factory-startup --python-exit-code 1 -P relef_byudzhet.py -- выход.blend проба.blend тр [--zashch 18] [--telo хребет,шея,Сердце,Руки] [--tony 1.0,0.74]
"""
import sys, math
import bpy, bmesh
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

a = sys.argv[sys.argv.index('--') + 1:]; OUT, SRC, TR = a[0], a[1], int(a[2]); opt = dict(zashch='18', telo='хребет,шея,Сердце,Руки', tony='1.0,0.74'); i = 3
while i < len(a): opt[a[i].lstrip('-')] = a[i + 1]; i += 2
IM = 'Шкура.рельеф-проба@Змея'
with bpy.data.libraries.load(SRC) as (s_, d_): d_.objects = [IM]
r = d_.objects[0]; bpy.context.scene.collection.objects.link(r)
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
bm = bmesh.new(); bm.from_mesh(r.data); bm.transform(r.matrix_world); bm.normal_update()
bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-6)
ZA = math.radians(float(opt['zashch']))
def zashch(e):
    if len(e.link_faces) != 2: return True
    f0, f1 = e.link_faces
    return f0.material_index != f1.material_index or f0.normal.angle(f1.normal, 0) > ZA
n0 = len(bm.faces)
# подбор порога слияния: дихотомия по углу
def svesti(ang):
    b = bm.copy(); b.normal_update()
    for e in b.edges: e.smooth = not zashch(e)
    bmesh.ops.dissolve_limit(b, angle_limit=ang, use_dissolve_boundaries=False, verts=b.verts[:], edges=[e for e in b.edges if e.smooth], delimit={'MATERIAL', 'SHARP'})
    bmesh.ops.triangulate(b, faces=b.faces[:]); return b, len(b.faces)
lo, hi = math.radians(.5), ZA * .98; best = None
for _ in range(14):
    m = (lo + hi) / 2; b, n = svesti(m)
    if best: best[0].free()
    best = (b, n, m)
    if n > TR: lo = m
    else: hi = m
b, n, m = best
if n > TR * 1.08:
    print('РЕЛЬЕФ: порог %.1f° дал %d тр, бюджет %d недостижим без нарушения защищённых рёбер' % (math.degrees(m), n, TR))
print('РЕЛЬЕФ: %d → %d тр (бюджет %d), порог слияния %.1f°, защита излома > %.0f°' % (n0, n, TR, math.degrees(m), math.degrees(ZA)))
# тела: веса барицентрически с ближайшего треугольника кожи
TELO = [bpy.data.objects[x] for x in opt['telo'].split(',') if bpy.data.objects.get(x)]
V, F, W = [], [], []
for o in TELO:
    kb = o.data.shape_keys.key_blocks[0] if o.data.shape_keys else None; M = o.matrix_world; n_ = len(V); gn = {g.index: g.name for g in o.vertex_groups}
    for j, v in enumerate(o.data.vertices): V.append(M @ (kb.data[j].co if kb else v.co)); W.append({gn[g.group]: g.weight for g in v.groups if g.weight > 0})
    o.data.calc_loop_triangles(); F += [[n_ + q for q in t.vertices] for t in o.data.loop_triangles]
BVH = BVHTree.FromPolygons(V, F, all_triangles=True); kosti = {x.name for x in arm.data.bones}
def ves(p):
    q, nn, fi, d = BVH.find_nearest(p); t = F[fi]; A, B, C = (V[k] for k in t)
    v0, v1, v2 = B - A, C - A, q - A; d00, d01, d11, d20, d21 = v0.dot(v0), v0.dot(v1), v1.dot(v1), v2.dot(v0), v2.dot(v1); den = d00 * d11 - d01 * d01 or 1e-12
    bv = (d11 * d20 - d01 * d21) / den; bw = (d00 * d21 - d01 * d20) / den; bu = 1 - bv - bw; w = {}
    for k, c in zip(t, (bu, bv, bw)):
        for g, x in W[k].items():
            if g in kosti: w[g] = w.get(g, 0) + max(c, 0) * x
    kw = sorted(w.items(), key=lambda q_: -q_[1])[:4]; s = sum(x for _, x in kw) or 1
    return {g: x / s for g, x in kw}, d
# раскладка по материалам в тоны
for o in [x for x in bpy.data.objects if x.name.startswith('Шкура.тон') and x.name.endswith('@Змея')]: bpy.data.objects.remove(o, do_unlink=True)
TONY = [float(x) for x in opt['tony'].split(',')]; dmax = 0.0; itog = {}
for k, ton in enumerate(TONY):
    b2 = b.copy(); bmesh.ops.delete(b2, geom=[f for f in b2.faces if f.material_index != k], context='FACES'); bmesh.ops.delete(b2, geom=[v for v in b2.verts if not v.link_faces], context='VERTS')
    if not b2.faces: b2.free(); continue
    nm = 'Шкура.тон%d@Змея' % (k + 1); me = bpy.data.meshes.new(nm); b2.to_mesh(me); b2.free()
    mt = bpy.data.materials.new('тон%d' % (k + 1)); c = (ton, ton, ton, 1); mt.diffuse_color = c; mt.use_nodes = True; mt.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = c
    me.materials.clear(); me.materials.append(mt)
    for p in me.polygons: p.material_index = 0; p.use_smooth = False
    ob = bpy.data.objects.new(nm, me); bpy.context.scene.collection.objects.link(ob); ob.parent = arm; md = ob.modifiers.new('ск', 'ARMATURE'); md.object = arm
    for j, v in enumerate(me.vertices):
        w, d = ves(v.co); dmax = max(dmax, d)
        for g, x in w.items(): (ob.vertex_groups.get(g) or ob.vertex_groups.new(name=g)).add([j], x, 'REPLACE')
    me.calc_loop_triangles(); itog[nm] = len(me.loop_triangles)
b.free(); bpy.data.objects.remove(r, do_unlink=True)
print('РЕЛЬЕФ: тоны %s, всего %d тр; вершина дальше всего от кожи %.1f мм' % (itog, sum(itog.values()), dmax * 1000))
bpy.ops.wm.save_as_mainfile(filepath=OUT)
