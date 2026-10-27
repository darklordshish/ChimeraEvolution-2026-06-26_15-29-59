"""ВКЛЕЙКА ТОРСА НОВОЙ ЦЕЛИ В СТАРУЮ (27.10, задача дизайн-линии ZADACHA-2026-10-27b-codex-cel-tors-st2: полка под грудью и клин
живота — в цели голого тела ст.2, правится только цель, а ключ снимается с картинки). Новая цель — по листу торса дизайнера через
cel_vyrezki → cel_gen → cel_norm; вклеивается только торс, остальное (руки, ноги, голова, позы и ориентиры) — от старой цели,
под которую сняты ориентиры и суставы.

  1. совмещение: новая цель (нормирована тем же cel_norm) подводится к старой сдвигом по торсу — точки торса новой к ближайшим
     старой, средний вектор, `--iter` раз (масштаб и поворот уже общие: рост, стопы); печатается остаток;
  2. торс — полоса между ориентирами `таз:head` и `шея:head` старой цели (с запасом `--zapas`), не дальше `--x` от средней
     линии; `--pered 1` — только передняя половина (впереди `хребет:head` по y): дизайн-линия правит грудь и живот;
  3. из старой цели грани торса удаляются, из новой берутся грани торса; сшивать не нужно — подгонка тянет к ближайшей точке.

  blender -b --factory-startup -P vkleit_tors.py -- старая.obj новая.obj orientiry-celi.json выход.obj [--x 0.24] [--zapas 0.02] [--pered 1] [--iter 6] [--kraj 0]
"""
import sys, json
import bpy, bmesh
import numpy as np
from mathutils import Vector
from mathutils import kdtree

a = sys.argv[sys.argv.index('--') + 1:]
old_p, new_p, lm_p, out = a[:4]
opt = dict(x='0.24', zapas='0.02', pered='1', iter='6')
i = 4
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
LM = {k: Vector(v) for k, v in json.load(open(lm_p, encoding='utf-8')).items()}
Z0, Z1 = LM['таз:head'].z - float(opt['zapas']), LM['шея:head'].z + float(opt['zapas'])
XM = float(opt['x']); YS = LM['хребет:head'].y if 'хребет:head' in LM else LM['таз:head'].y
bpy.ops.wm.read_factory_settings(use_empty=True)


def zagruzit(p):
    bpy.ops.wm.obj_import(filepath=p, forward_axis='NEGATIVE_Z', up_axis='Y')
    o = bpy.context.selected_objects[0]; bm = bmesh.new(); bm.from_mesh(o.data); bm.transform(o.matrix_world)
    bpy.data.objects.remove(o); return bm


def v_torse(p):
    return Z0 < p.z < Z1 and abs(p.x) < XM and (opt['pered'] != '1' or p.y < YS)


old = zagruzit(old_p); new = zagruzit(new_p)
# 1. совмещение сдвигом по торсу
To = [v.co.copy() for v in old.verts if v_torse(v.co)]
kd = kdtree.KDTree(len(To))
for k_, p in enumerate(To): kd.insert(p, k_)
kd.balance()
t = Vector()
for _ in range(int(opt['iter'])):
    Tn = [v.co + t for v in new.verts if v_torse(v.co + t)]
    d = [kd.find(p) for p in Tn]
    t += sum(((q - p) for p, (q, _, _) in zip(Tn, d)), Vector()) / max(len(Tn), 1)
ost = np.mean([kd.find(v.co + t)[2] for v in new.verts if v_torse(v.co + t)])
for v in new.verts: v.co += t
# ШОВ (`--kraj` м, по умолчанию 0 — выкл): у края зоны торс новой цели притягивается к поверхности старой. На чужом теле (проба
# 27.10: старое тело r2, остаток 41 мм) притяжка к ближайшей точке мяла край шипами и рвала дыры — жёсткая коробка лучше: ступеньку
# цели сглаживает подгонка (лапласиан шаблона), а у цели по тому же листу ступенька мала
from mathutils.bvhtree import BVHTree
BV = BVHTree.FromBMesh(old); KR = float(opt.get('kraj', '0'))
def do_kraya(p):
    d = min(p.z - Z0, Z1 - p.z, XM - abs(p.x))
    if opt['pered'] == '1': d = min(d, YS - p.y)
    return d
n_kr = 0
for v in new.verts:
    if not v_torse(v.co): continue
    if KR <= 0: break
    w = 1.0 - do_kraya(v.co) / KR
    if w <= 0: continue
    q = BV.find_nearest(v.co)[0]
    if q is not None: v.co = v.co.lerp(q, min(w, 1.0)); n_kr += 1
# 2–3. замена граней торса
ub = [f for f in old.faces if v_torse(f.calc_center_median())]
n_old = len(ub); bmesh.ops.delete(old, geom=ub, context='FACES')
bmesh.ops.delete(new, geom=[f for f in new.faces if not v_torse(f.calc_center_median())], context='FACES')
bmesh.ops.delete(old, geom=[v for v in old.verts if not v.link_faces], context='VERTS')
bmesh.ops.delete(new, geom=[v for v in new.verts if not v.link_faces], context='VERTS')
me = bpy.data.meshes.new('cel'); old.to_mesh(me)
me2 = bpy.data.meshes.new('tors'); new.to_mesh(me2); n_new = len(me2.polygons)
o1 = bpy.data.objects.new('cel', me); o2 = bpy.data.objects.new('tors', me2)
for o in (o1, o2): bpy.context.scene.collection.objects.link(o)
for o in bpy.data.objects: o.select_set(True)
bpy.context.view_layer.objects.active = o1; bpy.ops.object.join()
print('ВКЛЕЙКА: шов — притянуто вершин %d (полоса %.0f мм)' % (n_kr, KR * 1000))
print('ВКЛЕЙКА: сдвиг новой цели %s мм, остаток по торсу средн. %.1f мм; граней торса старой %d → новой %d (z %.2f–%.2f, |x| < %.2f%s)' % (
    [round(c * 1000, 1) for c in t], ost * 1000, n_old, n_new, Z0, Z1, XM, ', перед' if opt['pered'] == '1' else ''))
bpy.ops.wm.obj_export(filepath=out, export_selected_objects=False, forward_axis='NEGATIVE_Z', up_axis='Y', export_materials=False,
                      export_normals=False, export_uv=False)
