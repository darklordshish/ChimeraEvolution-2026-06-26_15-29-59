"""КУСОК НА ШОВ: КАДР И ЗАЗОР (03.11, ГеймБосс: «волчья голова-кусок и грива на новом шве кадром, зазор числом»). Кусок из FBX донора ставится формулой `PartPieces`: кадр шва куска → кадр места, масштаб `--k` или супремум отношений эллипсов. Зазор: от точек кольца куска на месте до кожи тела и до кольца носителя. Это Blender-приближение игры: без сварки, позы и ключей; проверка в Unity остаётся за механиками.

  blender -b тело.blend --factory-startup -P kusok_na_shov.py -- кусок.json шов.json кадр_префикс [--imya Поджарый] [--k авто] [--skryt голова,Чутьё — объекты носителя, которые кусок забирает] [--kluch_kuska двуногий] [--m 0.9 --zc 1.6 — кадр]
"""
import sys, json, math
import bpy
import numpy as np
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

a = sys.argv[sys.argv.index('--') + 1:]
pj, cj, pre = a[:3]; opt = dict(imya='Поджарый')
i = 3
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
P = json.load(open(pj, encoding='utf-8')); C = json.load(open(cj, encoding='utf-8')); d, c = P['seam'], C['seam']
SK = set(opt.get('skryt', '').split(','))
for o in list(bpy.data.objects):
    if o.type == 'MESH' and ('@' in o.name or o.name in SK): bpy.data.objects.remove(o)
V, F = [], []
for o in bpy.data.objects:
    if o.type != 'MESH': continue
    kb = o.data.shape_keys.key_blocks.get(opt['imya']) if o.data.shape_keys else None
    for k in (o.data.shape_keys.key_blocks if o.data.shape_keys else []): k.value = 0
    if kb: kb.value = 1
    for p_ in o.data.polygons: p_.use_smooth = False
    n0 = len(V); V += [o.matrix_world @ (kb.data[j].co if kb else o.data.vertices[j].co) for j in range(len(o.data.vertices))]
    o.data.calc_loop_triangles(); F += [[n0 + q for q in t.vertices] for t in o.data.loop_triangles]
bvh = BVHTree.FromPolygons(V, F, all_triangles=True)
keep = set(bpy.data.objects)
bpy.ops.import_scene.fbx(filepath=P['fbx'], axis_forward='-Z', axis_up='Y', automatic_bone_orientation=False)
imp = [o for o in bpy.data.objects if o not in keep]
kl = opt.get('kluch_kuska'); sv, sf = [], []
for nm_ in P['objects'].values():
    src = next((o for o in imp if o.name.split('.')[0] == nm_), None)
    if src is None: continue
    kb = src.data.shape_keys.key_blocks.get(kl) if kl and src.data.shape_keys else None
    n0 = len(sv); sv += [src.matrix_world @ (kb.data[j].co if kb else src.data.vertices[j].co) for j in range(len(src.data.vertices))]
    src.data.calc_loop_triangles(); sf += [[n0 + q for q in t.vertices] for t in src.data.loop_triangles]
for o in imp: bpy.data.objects.remove(o)
Uv = lambda q: Vector((-q.x, q.z, -q.y)); Bk = lambda u: Vector((-u.x, -u.z, u.y))
V3 = lambda l: Vector(l)


def frame(ax, up):
    f = ax.normalized(); r = up.cross(f).normalized(); u = f.cross(r); return Matrix((r, u, f)).transposed()


k = float(opt['k']) if opt.get('k') else max(c['ellipse_m'][0] / d['ellipse_m'][0], c['ellipse_m'][1] / d['ellipse_m'][1])
rot = frame(V3(c['axis_out']), V3(c['up'])) @ frame(V3(d['axis_out']), V3(d['up'])).inverted(); dC, cC = V3(P.get('center_biped_m', d['center_m'])) if kl else V3(d['center_m']), V3(c['center_m'])
P2 = [Bk(cC + rot @ (Uv(p) - dC) * k) for p in sv]
me = bpy.data.meshes.new('кусок'); me.from_pydata(P2, [], sf); ob = bpy.data.objects.new('кусок', me); bpy.context.scene.collection.objects.link(ob)
Rp = [Bk(cC + rot @ (V3(u) - dC) * k) for u in d['ring_m']] if d.get('ring_m') else []; Rc = [Bk(V3(u)) for u in c.get('ring_m', [])]
ds = [bvh.find_nearest(p)[3] for p in Rp]; dr = [min((p - q).length for q in Rc) for p in Rp] if Rc else []
print('ЗАЗОР: k %.3f; кольцо куска → кожа средн %.1f макс %.1f мм%s' % (k, np.mean(ds) * 1000, np.max(ds) * 1000, '; → кольцо носителя средн %.1f макс %.1f мм' % (np.mean(dr) * 1000, np.max(dr) * 1000) if dr else ''))
sc = bpy.context.scene; sc.render.engine = 'BLENDER_WORKBENCH'; sc.render.resolution_x = 600; sc.render.resolution_y = 700; sc.world = bpy.data.worlds.new('w'); sc.world.color = (.75, .75, .75)
sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'SINGLE'; sc.display.shading.single_color = (.72, .68, .62); sc.display.shading.show_backface_culling = False
cam = bpy.data.objects.new('c', bpy.data.cameras.new('c')); sc.collection.objects.link(cam); sc.camera = cam; cam.data.type = 'ORTHO'; cam.data.ortho_scale = float(opt.get('m', 0.9))
zc = float(opt.get('zc', 1.6))
for nm, ang in [('front', 0), ('profile', -90), ('34', 30), ('34back', 150)]:
    r = math.radians(ang); dd = Vector((math.sin(r), -math.cos(r), 0)); cam.location = Vector((0, 0, zc)) + dd * 4; cam.rotation_euler = (math.pi / 2, 0, r)
    sc.render.filepath = pre + '_' + nm + '.png'; bpy.ops.render.render(write_still=True)
