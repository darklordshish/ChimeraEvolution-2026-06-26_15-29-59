"""ПРИЖАТЬ ОРГАН К КОЖЕ (06.11, чешуя змеи из объёма: «толстая накидка — снова бронеплиты»). Каждая вершина объекта уходит на ближайшую точку кожи тела и поднимается над ней на долю своей прежней высоты, не больше `--maks` и не меньше `--min`: рисунок пластин остаётся рельефом в миллиметры, контур тела не растёт, щели между пластинами смыкаются (их стенки ложатся на кожу). Ниже `--srez` грани объекта снимаются, вместо них добавляется затухание из второго файла (объект того же имени, только его грани ниже `--srez`). Цвет — `--cvet`.

  blender -b сборка.blend --factory-startup --python-exit-code 1 -P prizhat_organ.py -- выход.blend Объект [--dolya 0.15] [--maks 0.003] [--min 0.0008] [--srez 1.33 --zatuh вторая.blend] [--cvet #848580] [--krai #5E5F5A [--krai_z -0.25]]
"""
import sys
import bpy, bmesh
from mathutils import Vector
from mathutils.bvhtree import BVHTree

a = sys.argv[sys.argv.index('--') + 1:]; OUT, IM = a[:2]; opt = dict(dolya='0.15', maks='0.003', min='0.0008'); i = 2
while i < len(a): opt[a[i].lstrip('-')] = a[i + 1]; i += 2
DL, MX, MN = float(opt['dolya']), float(opt['maks']), float(opt['min'])
TELO = [o for o in bpy.data.objects if o.type == 'MESH' and '@' not in o.name and o.name != 'Cube' and '.' not in o.name]
V, F = [], []
for o in TELO:
    kb = o.data.shape_keys.key_blocks[0] if o.data.shape_keys else None; M = o.matrix_world; n0 = len(V)
    V += [M @ (kb.data[j].co if kb else v.co) for j, v in enumerate(o.data.vertices)]; o.data.calc_loop_triangles(); F += [[n0 + q for q in t.vertices] for t in o.data.loop_triangles]
BVH = BVHTree.FromPolygons(V, F, all_triangles=True)
o = bpy.data.objects[IM]; M = o.matrix_world; Mi = M.inverted()
bm = bmesh.new(); bm.from_mesh(o.data)
if opt.get('srez'):
    zs = float(opt['srez']); ub = [f for f in bm.faces if (M @ f.calc_center_median()).z < zs]; bmesh.ops.delete(bm, geom=ub, context='FACES'); bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
hmax = 0.0
# кромки пластин: грани объёма, смотревшие вниз (подрез под пластиной), уходят отдельным тёмным объектом `<слот>.край@<вид>` — рисунок чешуи держится тоном, а не рельефом
bm.normal_update(); KL = bm.faces.layers.int.new('край'); krai = bool(opt.get('krai'))
for f in bm.faces: f[KL] = int(krai and (M.to_3x3() @ f.normal).normalized().z < float(opt.get('krai_z', '-0.25')))
for v in bm.verts:
    p = M @ v.co; q, n, _, d = BVH.find_nearest(p); h = (p - q).dot(n); hmax = max(hmax, h)
    v.co = Mi @ (q + n * min(max(h * DL, MN), MX))
n_osn = len(bm.faces)
if opt.get('zatuh'):
    with bpy.data.libraries.load(opt['zatuh']) as (s_, d_): d_.objects = [IM]
    z = d_.objects[0]; Mz = z.matrix_world; zs = float(opt['srez'])
    for f in z.data.polygons:
        P = [Mz @ z.data.vertices[j].co for j in f.vertices]
        if sum(P, Vector()).z / len(P) >= zs: continue
        q = []
        for p in P:
            h_ = BVH.find_nearest(p); q.append(bm.verts.new(Mi @ (h_[0] + h_[1] * min(max((p - h_[0]).dot(h_[1]), MN), MX))))
        try: bm.faces.new(q)
        except ValueError: pass
bmesh.ops.triangulate(bm, faces=bm.faces[:]); bm.normal_update()
if krai:
    bk = bm.copy(); KB = bk.faces.layers.int.get('край'); bmesh.ops.delete(bk, geom=[f for f in bk.faces if not f[KB]], context='FACES'); bmesh.ops.delete(bk, geom=[v for v in bk.verts if not v.link_faces], context='VERTS')
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if f[KL]], context='FACES'); bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    sl = IM.split('@')[0].split('.')[0]; nk = sl + '.край@' + IM.split('@')[1]; st = bpy.data.objects.get(nk)
    if st: bpy.data.objects.remove(st, do_unlink=True)
    for L_ in list(bk.verts.layers.deform.values()): bk.verts.layers.deform.remove(L_)   # веса прежнего объекта по чужим индексам групп
    mk = bpy.data.meshes.new(nk); bk.to_mesh(mk); bk.free(); ok = bpy.data.objects.new(nk, mk); bpy.context.scene.collection.objects.link(ok); ok.parent = o.parent; ok.matrix_world = o.matrix_world.copy()
    mm = ok.modifiers.new('ск', 'ARMATURE'); mm.object = [x for x in bpy.data.objects if x.type == 'ARMATURE'][0]
    h = opt['krai']; c_ = [int(h[k:k + 2], 16) / 255 for k in (1, 3, 5)]; c_ = tuple(x / 12.92 if x <= .04045 else ((x + .055) / 1.055) ** 2.4 for x in c_) + (1,)
    mt = bpy.data.materials.new('край'); mt.diffuse_color = c_; mt.use_nodes = True; mt.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = c_; mk.materials.append(mt)
    for p_ in mk.polygons: p_.use_smooth = False
bm.to_mesh(o.data); o.data.update(); bm.free()
# веса заново по ближайшей вершине тела: прежние снимались с объёма над кожей
import numpy as np
VA = np.array([tuple(v) for v in V]); Wb = []
for t in TELO:
    gn = {g.index: g.name for g in t.vertex_groups}; Wb += [{gn[g.group]: g.weight for g in v.groups if g.weight > 0} for v in t.data.vertices]
for o_ in [o] + ([bpy.data.objects[nk]] if krai else []):
    for g in list(o_.vertex_groups): o_.vertex_groups.remove(g)
    o_.data.vertices.foreach_set('select', [False] * len(o_.data.vertices))
    for j, v in enumerate(o_.data.vertices):
        for n_, w_ in Wb[int(np.linalg.norm(VA - np.array(tuple(o_.matrix_world @ v.co)), axis=1).argmin())].items(): (o_.vertex_groups.get(n_) or o_.vertex_groups.new(name=n_)).add([j], w_, 'REPLACE')
if opt.get('cvet'):
    h = opt['cvet']; c_ = [int(h[k:k + 2], 16) / 255 for k in (1, 3, 5)]; c_ = tuple(x / 12.92 if x <= .04045 else ((x + .055) / 1.055) ** 2.4 for x in c_) + (1,)
    for m in o.data.materials:
        m.diffuse_color = c_
        if m.use_nodes: m.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = c_
o.data.calc_loop_triangles(); print('ПРИЖАТО %s: высота была до %.0f мм, стала до %.1f мм; граней %d (основа %d, затухание %d)' % (IM, hmax * 1000, MX * 1000, len(o.data.loop_triangles), n_osn, len(o.data.polygons) - n_osn))
bpy.ops.wm.save_as_mainfile(filepath=OUT)
