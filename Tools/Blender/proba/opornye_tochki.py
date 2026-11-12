"""ОПОРНЫЕ ТОЧКИ (03.11, рецепт геймдизайнера: «лоу поли + вершины треугольников в нужных местах, по анатомии — без лишних деталей»). Точки анатомии из файла бросаются лучом на тело (обе стороны зеркально), ближайшая вершина едет на точку; цепи между точками (ключица, дуга рёбер, ость) проходятся через ~`--shag` м теми же вершинами. Такие вершины кладутся в группу `~опора`: `ryad_byudzheta.py` их не схлопывает и соединяет рёбрами. Вершины края объекта не двигаются, только держатся. Топология не меняется.

Файл: {"tochki": {"имя": {"vid": "front|back|side|side_in|top", "u": |x| (для side, side_in: y), "z": высота, "start": начало луча по |x| (side, side_in), "y": (top)}}, "cepi": [["имя", "имя", …], …]}. Точка с "u": 0 в анфас и сзади — одна, на оси.

  blender -b база.blend --factory-startup --python-exit-code 1 -P opornye_tochki.py -- выход.blend точки.json --kadr папка [--shag 0.05] [--maks 0.03]
Пишет рядом с выходом `<выход>.cepi.json` — цепи в мировых точках, и `--kadr` — кадры с метками.
"""
import sys, json, math, os
import bpy, bmesh
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

a = sys.argv[sys.argv.index('--') + 1:]
OUT, FT = os.path.abspath(a[0]), a[1]
opt = {a[i][2:]: a[i + 1] for i in range(2, len(a) - 1, 2)}; SHAG = float(opt.get('shag', 0.05)); MAKS = float(opt.get('maks', 0.03))
D = json.load(open(FT, encoding='utf-8'))
O = [o for o in bpy.data.objects if o.type == 'MESH' and '@' not in o.name]
V, F, VO = [], [], []
for o in O:
    n0 = len(V); V += [o.matrix_world @ v.co for v in o.data.vertices]; VO += [(o, v.index) for v in o.data.vertices]
    o.data.calc_loop_triangles(); F += [[n0 + q for q in t.vertices] for t in o.data.loop_triangles]
bvh = BVHTree.FromPolygons(V, F, all_triangles=True); VA = np.array([tuple(v) for v in V])
kraj = set()
n0 = 0
for o in O:
    bm = bmesh.new(); bm.from_mesh(o.data)
    kraj |= {n0 + v.index for v in bm.verts if any(e.is_boundary for e in v.link_edges)}; n0 += len(o.data.vertices); bm.free()


def luch(t, sg):
    x = t.get('u', 0) * sg; z = t['z']; v = t['vid']
    if v == 'front': o_, d_ = Vector((x, -2, z)), Vector((0, 1, 0))
    elif v == 'back': o_, d_ = Vector((x, 2, z)), Vector((0, -1, 0))
    elif v == 'side': o_, d_ = Vector((sg * t['start'], t['u'], z)), Vector((-sg, 0, 0))
    elif v == 'side_in': o_, d_ = Vector((sg * t['start'], t['u'], z)), Vector((sg, 0, 0))
    else: o_, d_ = Vector((t['u'] * sg, t['y'], 2.5)), Vector((0, 0, -1))
    return bvh.ray_cast(o_, d_)[0]


PT = {}; promah = []
for n, t in D['tochki'].items():
    for sg, sf in ((1, '.L'), (-1, '')):
        if t.get('u', 0) == 0 and t['vid'] in ('front', 'back') and sg < 0: PT[n] = PT.get(n + '.L'); continue
        h = luch(t, sg)
        if h is None: promah.append(n + sf); continue
        PT[n + sf] = h
if promah: sys.exit('ОПОРНЫЕ: мимо тела: %s' % promah)
PT = {k: v for k, v in PT.items() if v is not None}
CEPI = []
for c in D['cepi']:
    for sf in ('.L', ''):
        im = [(n + sf if (n + sf) in PT else n) for n in c]
        CEPI.append(im)
zan = {}; sdv = []; held = 0


def vzyat(p):
    global held
    d = np.linalg.norm(VA - np.array(tuple(p)), axis=1); j = int(d.argmin())
    if j in zan: return j
    zan[j] = p
    if j in kraj or d[j] > MAKS: held += 1; return j
    sdv.append(float(d[j])); return j


for n, p in PT.items(): vzyat(p)
Cw = []
for im in CEPI:
    ch = []
    for x, y in zip(im, im[1:]):
        A, B = PT[x], PT[y]; m = max(1, int(round((B - A).length / SHAG)))
        for k in range(m + 1):
            q = bvh.find_nearest(A.lerp(B, k / m))[0]; j = vzyat(q)
            if not ch or ch[-1] != j: ch.append(j)
    Cw.append(ch)
# сдвиг и группа: нормали соседних граней не должны вывернуться
bad = 0
for j, p in zan.items():
    o, i = VO[j]
    if j not in kraj and (VA[j] - np.array(tuple(p))).dot(VA[j] - np.array(tuple(p))) <= MAKS ** 2:
        o.data.vertices[i].co = o.matrix_world.inverted() @ p
        if o.data.shape_keys: o.data.shape_keys.key_blocks[0].data[i].co = o.data.vertices[i].co
for o in O:
    g = o.vertex_groups.new(name='~опора'); g.add([VO[j][1] for j in zan if VO[j][0] is o], 1.0, 'REPLACE'); o.data.update()
json.dump([[tuple(float(q) for q in (VO[j][0].matrix_world @ VO[j][0].data.vertices[VO[j][1]].co)) for j in ch] for ch in Cw], open(OUT + '.cepi.json', 'w', encoding='utf-8'))
bpy.ops.wm.save_as_mainfile(filepath=OUT)
print('ОПОРНЫЕ: точек %d, вершин в опоре %d, цепей %d, сдвиг средн %.1f макс %.1f мм, держатся без сдвига %d' % (len(PT), len(zan), len(Cw), np.mean(sdv) * 1000, max(sdv) * 1000, held))
if opt.get('kadr'):
    KD = os.path.abspath(opt['kadr']); os.makedirs(KD, exist_ok=True); sc = bpy.context.scene
    for n, p in PT.items():
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=0.012, location=p); bpy.context.object.color = (0.9, 0.1, 0.1, 1)
    for ch in Cw:
        for j in ch:
            o, i = VO[j]; bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=0.006, location=o.matrix_world @ o.data.vertices[i].co); bpy.context.object.color = (0.1, 0.3, 0.9, 1)
    for o in bpy.data.objects:
        if o.type == 'MESH' and '@' in o.name: o.hide_render = True
        if o in O: o.color = (0.72, 0.66, 0.60, 1)
    w = sc.display; sc.render.engine = 'BLENDER_WORKBENCH'; w.shading.light = 'STUDIO'; w.shading.color_type = 'OBJECT'; w.shading.show_shadows = False; w.shading.show_cavity = False
    sc.world = sc.world or bpy.data.worlds.new('w'); sc.world.color = (0.82, 0.83, 0.84); sc.render.resolution_x, sc.render.resolution_y = 500, 800; sc.render.image_settings.file_format = 'PNG'
    for k, yaw, nk in (('front', 0, 4), ('back', 180, 4), ('side', 270, 0), ('q34back', 218, 8)):
        d = bpy.data.cameras.new(k); d.type = 'ORTHO'; d.ortho_scale = 2.05; c = bpy.data.objects.new(k, d); sc.collection.objects.link(c)
        y, n = math.radians(yaw), math.radians(nk); nap = Vector((math.sin(y) * math.cos(n), -math.cos(y) * math.cos(n), math.sin(n)))
        c.location = Vector((0, 0, 0.93)) + nap * 6; c.rotation_euler = (-nap).to_track_quat('-Z', 'Y').to_euler(); sc.camera = c
        sc.render.filepath = os.path.join(KD, 'opora_%s.png' % k); bpy.ops.render.render(write_still=True)
