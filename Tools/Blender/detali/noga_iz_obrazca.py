# -*- coding: utf-8 -*-
"""ДЕТАЛЬ-ПИЛОТ «Волк · Руки · четвероногий» (письмо механик `Docs/models/FEEDBACK-2026-10-02b-pilot-noga.md` с поправками
ГеймБосса 02.10: меш — В МЕТРАХ ТЕЛА ВОЛКА, оси Unity, там, где нога стоит в позе покоя графа; арматура — граф волка;
веса — `плечо`, `предплечье`, лапа к `предплечье`). Эксперимент консилиума: деталь — меш по форме листа.

Шаги:
  1. образец листа (выровненный `obrazec_v_obj.py`) → правая передняя нога: ось по следу от стопы, грани ниже брюха,
     связная область стопы; прореживание QEM до бюджета; срез верха плоскостью у подмышки;
  2. ПЕРЕСАДКА НА СУСТАВЫ ВОЛКА по высоте: земля → земля, запястье образца → та же высота (лапа своего размера), локоть →
     конец `плечо`, плечевой сустав → начало `плечо`; поперёк — смещение от оси образца переносится на ось костей волка
     без масштаба (толщина листа сохраняется);
  3. культя: петля подмышки → кольцо → КОЛЬЦО ШВА из 8 вершин на плечевом суставе (внутри туши, на поле уходит в тело);
     вершина 0 — перёд тела, обход против часовой при взгляде из детали к телу;
  4. арматура — все узлы графа волка в позе покоя (`DumpGraph`), имена — узлы; веса: выше локтя `плечо`, ниже —
     `предплечье`, переход ±3 см;
  5. форма-ключ `двуногий` (рука оборотня по листу `human/ref/chimera/oboroten-…png`): плечо и предплечье мощнее, пальцы
     длиннее; кольцо шва ключ не двигает (01d §1.4); поза — костями;
  6. FBX как у `chimera_rig.export_fbx` (без `bake_space_transform`: корень едет с поворотом −90°, в Unity — Bake Axis
     Conversion) и паспорт JSON.

Запуск:  blender -b -P noga_iz_obrazca.py -- образец.obj граф_мира.json выход.fbx паспорт.json [бюджет] [кадр.png]
"""
import json
import math
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Vector

a = sys.argv[sys.argv.index('--') + 1:]
src, graph_path, fbx, passport = a[0], a[1], a[2], a[3]
BUDGET = int(a[4]) if len(a) > 4 else 450
SHOT = a[5] if len(a) > 5 else None
N_RING = 8


def to_b(p):            # Unity (x, y, z) → Blender: модель смотрит в −Y, правая сторона Unity +X = Blender −X
    return Vector((-p[0], -p[2], p[1]))


def from_b(v):
    return np.array([-v[0], v[2], -v[1]])


# ── граф волка (выгрузка DumpGraph в осях OBJ стенда: x зеркален) → Unity ──
G = {n['name']: dict(n, a=np.array([-n['a'][0], n['a'][1], n['a'][2]]), b=np.array([-n['b'][0], n['b'][1], n['b'][2]]))
     for n in json.load(open(graph_path, encoding='utf-8'))}
S, E_, W = G['плечо']['a'], G['плечо']['b'], G['предплечье']['b']
GROUND = np.array([W[0], 0.0, W[2]])

# ── образец в осях Unity ──
V, F = [], []
for l in open(src, encoding='utf-8'):
    if l.startswith('v '):
        x, y, z = (float(t) for t in l.split()[1:4]); V.append((-x, y, z))
    elif l.startswith('f '):
        F.append([int(t.split('/')[0]) - 1 for t in l.split()[1:]])
P = np.array(V)
zmin, zmax = P[:, 2].min(), P[:, 2].max(); L = zmax - zmin
mid = (np.abs(P[:, 0]) < 0.04) & (P[:, 2] > zmin + L / 3) & (P[:, 2] < zmax - L / 3)
belly = float(np.percentile(P[mid, 1], 5))
legs = (P[:, 1] < belly - 0.03) & (np.abs(P[:, 0]) > 0.055)
zc = float(np.median(P[legs, 2]))
g = np.nonzero(legs & (P[:, 0] > 0) & (P[:, 2] > zc))[0]
c = np.median(P[g[P[g, 1] < 0.08]][:, [0, 2]], axis=0); axis = []
DY = 0.04
for y in np.arange(0.02, belly - 0.01, DY):
    s = g[(P[g, 1] >= y - DY / 2) & (P[g, 1] < y + DY / 2)]
    s = s[np.linalg.norm(P[s][:, [0, 2]] - c, axis=1) < 0.13]
    if len(s) > 20:
        c = P[s][:, [0, 2]].mean(0); axis.append((c[0], y, c[1]))
axis = np.array(axis)
cut = belly - 0.01
elbow_s = None


def axis_s(y):
    """Ось ноги образца на высоте y; выше подмышки — прямая к плечевому суставу образца."""
    if y > axis[-1, 1] and elbow_s is not None:
        t = (y - elbow_s[1]) / max(1e-6, shoulder_s[1] - elbow_s[1])
        return elbow_s + (shoulder_s - elbow_s) * np.clip(t, 0, 1.5)
    i = np.clip(np.searchsorted(axis[:, 1], y), 1, len(axis) - 1)
    t = (y - axis[i - 1, 1]) / (axis[i, 1] - axis[i - 1, 1])
    return axis[i - 1] + (axis[i] - axis[i - 1]) * np.clip(t, 0, 1)


inleg = np.zeros(len(P), bool)
for i in np.nonzero((P[:, 1] < cut) & (P[:, 0] > 0))[0]:
    ax = axis_s(P[i, 1]); inleg[i] = np.hypot(P[i, 0] - ax[0], P[i, 2] - ax[2]) < 0.13
faces = [f for f in F if all(inleg[v] for v in f)]

bpy.ops.wm.read_factory_settings(use_empty=True)
bm = bmesh.new(); vmap = {}
for f in faces:
    for v in f:
        if v not in vmap:
            vmap[v] = bm.verts.new(to_b(P[v]))
for f in faces:
    try:
        bm.faces.new([vmap[v] for v in f])
    except ValueError:
        pass
bm.verts.ensure_lookup_table()
low = min(bm.verts, key=lambda v: v.co.z); keep, stack = {low}, [low]
while stack:
    v = stack.pop()
    for e in v.link_edges:
        o = e.other_vert(v)
        if o not in keep:
            keep.add(o); stack.append(o)
bmesh.ops.delete(bm, geom=[v for v in bm.verts if v not in keep], context='VERTS')
me = bpy.data.meshes.new('Руки'); bm.to_mesh(me); bm.free()
ob = bpy.data.objects.new('Руки', me); bpy.context.collection.objects.link(ob)
bpy.context.view_layer.objects.active = ob; ob.select_set(True)
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.remove_doubles(threshold=0.0005); bpy.ops.object.mode_set(mode='OBJECT')
n0 = sum(len(p.vertices) - 2 for p in me.polygons)
m = ob.modifiers.new('dec', 'DECIMATE'); m.ratio = BUDGET / max(1, n0); bpy.ops.object.modifier_apply(modifier=m.name)
bm = bmesh.new(); bm.from_mesh(me)
plane_z = min(max(v.co.z for v in bm.verts), cut) - 0.03
bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], dist=1e-5,
                       plane_co=Vector((0, 0, plane_z)), plane_no=Vector((0, 0, 1)), clear_outer=True)
bm.to_mesh(me); bm.free()

# суставы образца
elbow_s = axis_s(plane_z)
wrist_y = 0.13
shoulder_s = elbow_s + (S - E_) * ((elbow_s[1]) / max(1e-6, E_[1]))  # плечевая кость образца — того же наклона, длина по росту локтя
print('образец: брюхо %.3f, локоть %s, плечо %s' % (belly, np.round(elbow_s, 3), np.round(shoulder_s, 3)))

# ── культя и кольцо шва (ещё в осях образца) ──
bm = bmesh.new(); bm.from_mesh(me)
edges = [e for e in bm.edges if e.is_boundary and abs(e.verts[0].co.z - plane_z) < 1e-3]
loopv = list({v for e in edges for v in e.verts})
cen = np.mean([from_b(v.co) for v in loopv], axis=0)
dir_u = (shoulder_s - cen) / np.linalg.norm(shoulder_s - cen)       # к телу
fwd = np.array([0.0, 0.0, 1.0]); fwd = fwd - dir_u * fwd.dot(dir_u); fwd /= np.linalg.norm(fwd)
lat = np.cross(dir_u, fwd)
lp = np.array([from_b(v.co) for v in loopv]) - cen
ax_f = float(np.percentile(np.abs(lp @ fwd), 90)); ax_l = float(np.percentile(np.abs(lp @ lat), 90))


def ring(center, k):
    vs = []
    for j in range(N_RING):        # вершина 0 — перёд; против часовой при взгляде вдоль +dir (из детали к телу)
        th = 2 * math.pi * j / N_RING
        vs.append(bm.verts.new(to_b(center + k * (fwd * ax_f * math.cos(th) - lat * ax_l * math.sin(th)))))
    return vs


r_mid = ring(cen + dir_u * 0.02, 0.95)
r_seam = ring(shoulder_s, 0.85)
for j in range(N_RING):
    bm.faces.new([r_mid[j], r_mid[(j + 1) % N_RING], r_seam[(j + 1) % N_RING], r_seam[j]])
bm.edges.ensure_lookup_table()
bmesh.ops.bridge_loops(bm, edges=edges + [bm.edges.get((r_mid[j], r_mid[(j + 1) % N_RING])) for j in range(N_RING)])
bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
bm.verts.index_update()
seam_idx = [v.index for v in r_seam]
bm.to_mesh(me); bm.free()

# ── пересадка на суставы волка ──
ys = [0.0, wrist_y, elbow_s[1], shoulder_s[1]]
yo = [0.0, wrist_y, E_[1], S[1]]


def axis_o(y):
    if y <= W[1]:
        return np.array([W[0], y, W[2]])
    if y <= E_[1]:
        t = (y - W[1]) / (E_[1] - W[1]); return W + (E_ - W) * t
    t = (y - E_[1]) / (S[1] - E_[1]); return E_ + (S - E_) * t


def retarget(p):
    y2 = float(np.interp(p[1], ys, yo, right=S[1] + (p[1] - shoulder_s[1])))
    off = p - axis_s(p[1]); off[1] = 0
    return axis_o(y2) + off + np.array([0, y2 - axis_o(y2)[1], 0])


for v in me.vertices:
    v.co = to_b(retarget(from_b(v.co)))
seam_m = np.array([from_b(me.vertices[i].co) for i in seam_idx])
print('кольцо шва: центр %s (плечо графа %s), средний радиус %.4f м' % (np.round(seam_m.mean(0), 3), np.round(S, 3),
      np.mean(np.linalg.norm(seam_m - seam_m.mean(0), axis=1))))

# ── арматура — граф волка в позе покоя ──
arm_d = bpy.data.armatures.new('Волк'); arm = bpy.data.objects.new('Волк', arm_d)
bpy.context.collection.objects.link(arm); bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode='EDIT')
for n in G.values():
    b = arm_d.edit_bones.new(n['name']); b.head = to_b(n['a']); b.tail = to_b(n['b'])
for n in G.values():
    if n['parent']:
        arm_d.edit_bones[n['name']].parent = arm_d.edit_bones[n['parent']]
bpy.ops.object.mode_set(mode='OBJECT')
ob.parent = arm
mod = ob.modifiers.new('Armature', 'ARMATURE'); mod.object = arm
vg_s = ob.vertex_groups.new(name='плечо'); vg_f = ob.vertex_groups.new(name='предплечье')
for v in me.vertices:
    y = from_b(v.co)[1]
    w = float(np.clip((y - (E_[1] - 0.03)) / 0.06, 0, 1))         # 1 — плечо, 0 — предплечье
    if w > 0: vg_s.add([v.index], w, 'REPLACE')
    if w < 1: vg_f.add([v.index], 1 - w, 'REPLACE')

# ── форма-ключ «двуногий» ──
ob.shape_key_add(name='Basis', from_mix=False)
key = ob.shape_key_add(name='двуногий', from_mix=False)
seam_set = set(seam_idx)


def thick(y):        # мощнее плечо и предплечье; у кольца шва и у кисти — как было
    return float(np.interp(y, [W[1] - 0.05, W[1], (W[1] + E_[1]) / 2, E_[1], (E_[1] + S[1]) / 2, S[1] - 0.04, S[1]],
                           [1.05, 1.15, 1.30, 1.25, 1.40, 1.15, 1.0]))


for i, kv in enumerate(key.data):
    if i in seam_set:
        continue
    p = from_b(kv.co)
    if p[1] > W[1] - 0.05:
        ax = axis_o(p[1]); off = p - ax; off[1] = 0
        p = ax + off * thick(p[1]) + np.array([0, p[1] - ax[1], 0])
    else:            # кисть: пальцы длиннее вперёд, лапа выше и уже — рука-лапа оборотня
        rel = p - np.array([W[0], wrist_y, W[2]])
        rel[2] *= 1.5 if rel[2] > 0 else 1.0
        rel[1] *= 1.2
        p = np.array([W[0], wrist_y, W[2]]) + rel * np.array([1.1, 1, 1])
    kv.co = to_b(p)

tris = sum(len(p.vertices) - 2 for p in me.polygons)
print('нога: %d → %d тр (с культёй)' % (n0, tris))

# ── кадр для проверки: профиль, оба ключа ──
if SHOT:
    sc = bpy.context.scene; sc.render.engine = 'BLENDER_WORKBENCH'
    sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'SINGLE'; sc.display.shading.single_color = (0.7, 0.7, 0.72)
    sc.world = bpy.data.worlds.new('w'); sc.render.resolution_x, sc.render.resolution_y = 900, 900
    for p in me.polygons: p.use_smooth = False
    cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); bpy.context.collection.objects.link(cam)
    cam.data.type = 'ORTHO'; cam.data.ortho_scale = 1.0; sc.camera = cam
    # камера по умолчанию смотрит вдоль −Z: поворот (90°, 0, 90°) — вдоль −X (бок), (90°, 0, 0) — вдоль +Y (на морду)
    for vw, rot, loc in (('bok', (math.radians(90), 0, math.radians(90)), Vector((5, -0.45, 0.45))),
                         ('pered', (math.radians(90), 0, 0), Vector((-0.09, -5, 0.45)))):
        cam.rotation_euler = rot; cam.location = loc
        for kk, val in (('basis', 0.0), ('dvunogij', 1.0)):
            key.value = val
            sc.render.filepath = SHOT.replace('.png', '-%s-%s.png' % (vw, kk)); bpy.ops.render.render(write_still=True)
    key.value = 0.0

# ── экспорт ──
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=fbx, use_selection=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z',
                         axis_up='Y', object_types={'ARMATURE', 'MESH'}, add_leaf_bones=False, bake_anim=False,
                         mesh_smooth_type='OFF', use_armature_deform_only=False)
cen_m = seam_m.mean(0)
json.dump(dict(species='Волк', slot='Руки', plan='четвероногий',
               seam=dict(type='плечо', ring=seam_idx, ring_m=[[round(c, 4) for c in p] for p in seam_m],
                         center_m=[round(c, 4) for c in cen_m],
                         ellipse_m=[round(ax_f * 0.85, 4), round(ax_l * 0.85, 4)],
                         note='метры тела волка, оси Unity; вершина 0 — перёд тела, обход против часовой при взгляде из детали к телу; '
                              'индексы — порядок вершин в Blender (Unity при плоских гранях делит вершины — сверять по ring_m)'),
               bones=['плечо', 'предплечье'], armature='граф волка в позе покоя (все узлы)', keys=['двуногий'],
               tris=tris, source='Anatomy/species/wolf/ref/mv/volk_mv_A.glb (лист volk-prirodnyj_meshy), выровнен obrazec_v_obj.py',
               generator='Tools/Blender/detali/noga_iz_obrazca.py'),
          open(passport, 'w', encoding='utf-8'), ensure_ascii=False, indent=2)
print('ГОТОВО: %d тр' % tris)
