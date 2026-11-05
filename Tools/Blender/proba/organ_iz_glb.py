"""ОРГАН ИЗ ОБЪЁМА HUNYUAN (05.11, третий проход; цель по кропу `витрина/органы/` даёт человека в 3/4 с органом). Голова цели разворачивается по её же симметрии, садится на голову шасси по своду (масштаб — ширина черепа, сдвиг — верх и середина), вырезается орган: окно по высоте и глубине, кожа шасси как нижний край. Половина, смотревшая в камеру, зеркалится, прореживается до бюджета, край уходит под кожу. Веса — кость или тело.

  blender -b организм.blend --factory-startup --python-exit-code 1 -P organ_iz_glb.py -- выход.blend цель.glb Объект@Вид тр [--z0 1.6] [--z1 1.74] [--y1 -0.045] [--sheya_y -0.075] [--kost голова] [--okno morda|spina] [--nad 0.008] [--razv 180] [--plechi 0.56] [--y_sdvig 0.04] [--z_sdvig -0.09] [--vpered 1.2]
"""
import sys, math
import bpy, bmesh
import numpy as np
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree
from mathutils.kdtree import KDTree

a = sys.argv[sys.argv.index('--') + 1:]; OUT, GLB, IM, TR = a[0], a[1], a[2], int(a[3]); opt = dict(z0='1.60', z1='1.74', y1='-0.045', sheya_y='-0.075', kost='голова', okno='morda', nad='0.008', xmax='0.2'); i = 4
while i < len(a): opt[a[i].lstrip('-')] = a[i + 1]; i += 2
ZO, Z1, Y1, SY, NAD, XM = (float(opt[k]) for k in ('z0', 'z1', 'y1', 'sheya_y', 'nad', 'xmax'))
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
TELO = [o for o in bpy.data.objects if o.type == 'MESH' and '@' not in o.name and o.name != 'Cube']
Vb, Fb, Wb = [], [], []
for o in TELO:
    kb = o.data.shape_keys.key_blocks[0] if o.data.shape_keys else None; M = o.matrix_world; n0 = len(Vb); gn = {g.index: g.name for g in o.vertex_groups}
    for j, v in enumerate(o.data.vertices): Vb.append(M @ (kb.data[j].co if kb else v.co)); Wb.append({gn[g.group]: g.weight for g in v.groups if g.weight > 0})
    o.data.calc_loop_triangles(); Fb += [[n0 + q for q in t.vertices] for t in o.data.loop_triangles]
BVH = BVHTree.FromPolygons(Vb, Fb, all_triangles=True); VA = np.array([tuple(v) for v in Vb])
# цель
sush = set(bpy.data.objects); bpy.ops.import_scene.gltf(filepath=GLB); nov = [o for o in bpy.data.objects if o not in sush and o.type == 'MESH']
bpy.ops.object.select_all(action='DESELECT'); cel = nov[0]
for o in nov: o.select_set(True)
bpy.context.view_layer.objects.active = cel
if len(nov) > 1: bpy.ops.object.join()
cel.select_set(True); bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
d_ = cel.modifiers.new('д', 'DECIMATE'); d_.ratio = max(.02, 60000 / max(len(cel.data.polygons), 1)); bpy.ops.object.modifier_apply(modifier='д')
A = np.array([tuple(v.co) for v in cel.data.vertices]); top = A[:, 2].max(); H0 = A[:, 2].max() - A[:, 2].min()
# поворот по симметрии: у морды — свод цели, у спины — пояс плеч (голова там смотрит вбок); спина разворачивается к +y
spina = opt['okno'] != 'morda'
sv = A[(A[:, 2] > top - H0 * .28)] if not spina else A[(A[:, 2] > A[:, 2].min() + H0 * .05) & (A[:, 2] < A[:, 2].min() + H0 * .40)]; sv = sv[np.random.default_rng(1).choice(len(sv), min(4000, len(sv)), replace=False)]
def sim(th):
    c, s = math.cos(th), math.sin(th); P = sv[:, :2].dot(np.array([[c, -s], [s, c]]).T); cx = (P[:, 0].min() + P[:, 0].max()) / 2; M_ = P.copy(); M_[:, 0] = 2 * cx - M_[:, 0]
    k = KDTree(len(P))
    for j, p in enumerate(P): k.insert((p[0], p[1], 0), j)
    k.balance(); return np.mean([k.find((q[0], q[1], 0))[2] for q in M_[::4]])
th_ = min(np.radians(np.arange(-75, 76, 3)), key=sim); th_ = min(th_ + np.radians(np.arange(-3, 3.5, .5)), key=sim)
R = Matrix.Rotation(th_, 4, 'Z'); cel.data.transform(R); A = np.array([tuple(v.co) for v in cel.data.vertices])
print('ЦЕЛЬ: поворот %.1f°' % math.degrees(th_))
def srez(P, z, dz, xm=9): q = P[(np.abs(P[:, 2] - z) < dz) & (np.abs(P[:, 0]) < xm)]; return q
if not spina:
    # масштаб — ширина свода над ушами, глубина — лоб к лбу
    wh_ = srez(VA, 1.805, .008, .1); wh = wh_[:, 0].max() - wh_[:, 0].min(); s_ = 1.0
    for _ in range(4):
        q = srez(A, top - .045 / s_, .008 / s_); s_ = wh / (q[:, 0].max() - q[:, 0].min())
    q = srez(A, top - .045 / s_, .008 / s_); cx = (q[:, 0].max() + q[:, 0].min()) / 2
    q2 = srez(A, top - .06 / s_, .01 / s_); fr = srez(VA, 1.79, .008, .03)[:, 1].min(); q3 = q2[np.abs(q2[:, 0] - cx) < .03 / s_]
    T = Matrix.Translation((-cx * s_, fr - q3[:, 1].min() * s_, 1.85 - top * s_)) @ Matrix.Scale(s_, 4)
else:
    # масштаб — размах плеч, разворот спиной к +y: сторона, где выше перепад глубины на середине
    q = srez(A, A[:, 2].min() + H0 * .3, H0 * .03); cx = (q[:, 0].max() + q[:, 0].min()) / 2; s_ = float(opt.get('plechi', '.56')) / (q[:, 0].max() - q[:, 0].min())
    if float(opt.get('razv', '0')): cel.data.transform(Matrix.Rotation(math.radians(float(opt['razv'])), 4, 'Z')); A = np.array([tuple(v.co) for v in cel.data.vertices]); q = srez(A, A[:, 2].min() + H0 * .3, H0 * .03); cx = (q[:, 0].max() + q[:, 0].min()) / 2
    zr = 1.85 - top * s_; z_ = (1.40 - zr) / s_; yq = srez(A, z_, .02 / s_); yq = yq[np.abs(yq[:, 0] - cx) < .05 / s_]; yh = srez(VA, 1.40, .05, .08)[:, 1].max()
    T = Matrix.Translation((-cx * s_, yh + float(opt.get('y_sdvig', '0')) - yq[:, 1].max() * s_, zr + float(opt.get('z_sdvig', '0')))) @ Matrix.Scale(s_, 4)
cel.data.transform(T); cel.data.update(); A = np.array([tuple(v.co) for v in cel.data.vertices])
print('ЦЕЛЬ: масштаб %.3f, рамка %s .. %s' % (s_, A.min(0).round(3), A.max(0).round(3)))
# морда вытягивается вперёд от щёк: у цели череп крупнее нашего, и нос короче листа
if float(opt.get('vpered', '1')) != 1:
    for v in cel.data.vertices:
        if v.co.y < -.10: v.co.y = -.10 + (v.co.y + .10) * float(opt['vpered'])
    cel.data.update()
# вырезка
bm = bmesh.new(); bm.from_mesh(cel.data)
def rez(co, no):
    bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], dist=1e-5, plane_co=co, plane_no=no, clear_outer=True)
rez((0, 0, 0), (-1, 0, 0))   # половина x ≥ 0: она смотрела в камеру
if opt['okno'] == 'morda':
    rez((0, 0, Z1), (0, 0, 1)); rez((0, Y1, 0), (0, 1, 0)); rez((0, 0, ZO), (0, 0, -1)); rez((0, SY, ZO + .02), (0, .6, -.8))
    ost = [f for f in bm.faces]
else:
    rez((0, 0, Z1), (0, 0, 1)); rez((0, 0, ZO), (0, 0, -1)); rez((XM, 0, 0), (1, 0, 0)); rez((0, Y1, 0), (0, -1, 0))
    # остаётся только то, что выше кожи шасси на NAD
    ub = []
    for f in bm.faces:
        c = f.calc_center_median(); h = BVH.find_nearest(c)
        s2 = (c - h[0]).dot(h[1]) if h[0] is not None else 1
        if h[3] < NAD or s2 < 0: ub.append(f)
    bmesh.ops.delete(bm, geom=ub, context='FACES'); bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
bm.to_mesh(cel.data); bm.free(); cel.data.update(); n_vyr = len(cel.data.polygons)
# прореживание половины: ось держится
ok = TR / 2
d_ = cel.modifiers.new('д', 'DECIMATE'); d_.ratio = min(1.0, ok / max(n_vyr, 1)); d_.use_symmetry = False; bpy.context.view_layer.objects.active = cel; bpy.ops.object.modifier_apply(modifier='д')
bm = bmesh.new(); bm.from_mesh(cel.data); bmesh.ops.triangulate(bm, faces=bm.faces[:])
n_os = 0
for v in bm.verts:
    if abs(v.co.x) < .004: v.co.x = 0; n_os += 1
# край уходит под кожу: юбка без щели
n_kr = 0
for v in bm.verts:
    if any(e.is_boundary for e in v.link_edges) and v.co.x > 0:
        h = BVH.find_nearest(v.co)
        if h[0] is not None and (v.co - h[0]).dot(h[1]) > -0.002: v.co = h[0] - h[1] * .004; n_kr += 1
V = [v.co.copy() for v in bm.verts]; idx = {v: j for j, v in enumerate(bm.verts)}; F = [[idx[q] for q in f.verts] for f in bm.faces]; bm.free()
# зеркало
ma = {}; V2 = list(V)
for j, p in enumerate(V):
    if abs(p.x) < 1e-5: ma[j] = j
    else: ma[j] = len(V2); V2.append(Vector((-p.x, p.y, p.z)))
F2 = F + [[ma[q] for q in reversed(t)] for t in F]
stary = bpy.data.objects.get(IM)
if stary: bpy.data.objects.remove(stary, do_unlink=True)
bpy.data.objects.remove(cel, do_unlink=True)
me = bpy.data.meshes.new(IM); me.from_pydata([tuple(p) for p in V2], [], F2); me.update(); o = bpy.data.objects.new(IM, me); bpy.context.scene.collection.objects.link(o); o.parent = arm; m = o.modifiers.new('ск', 'ARMATURE'); m.object = arm
K = opt['kost']
for j, p in enumerate(V2):
    if K != 'тело': (o.vertex_groups.get(K) or o.vertex_groups.new(name=K)).add([j], 1.0, 'REPLACE')
    else:
        for n_, w_ in Wb[int(np.linalg.norm(VA - np.array(tuple(p)), axis=1).argmin())].items(): (o.vertex_groups.get(n_) or o.vertex_groups.new(name=n_)).add([j], w_, 'REPLACE')
for p_ in me.polygons: p_.use_smooth = False
me.calc_loop_triangles(); print('ОРГАН %s: %d тр из вырезки %d граней, на оси %d, край под кожу %d, рамка %s .. %s' % (IM, len(me.loop_triangles), n_vyr, n_os, n_kr, np.array(o.bound_box[0]).round(3), np.array(o.bound_box[6]).round(3)))
bpy.ops.wm.save_as_mainfile(filepath=OUT)
