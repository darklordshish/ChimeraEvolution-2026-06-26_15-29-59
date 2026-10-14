"""ЦЕЛЬ В ПОЗУ ШАССИ (опыт «идентичность ключом», 14.10): цель (оборотень по листу) стоит в позе листа — колени согнуты,
корпус наклонён, руки вперёд. Ключ формы должен быть в позе шасси (К2): позу даёт скелет, а не ключ (письмо ГеймБосса
14.10: «поза цели — та же, что у К2, не сутулить»).

Выпрямление — той же тонкой пластиной, что перенос (`kluch_vida.py`): ориентиры цели в её позе → те же ориентиры в позе
шасси. Поза шасси для ориентира = цепь от таза: длина звена — ЦЕЛИ (её пропорции), направление звена — ШАССИ (его поза).
Точки кожи головы (нос, подбородок, макушка) едут с головой поворотом звена «голова»; носок и пятка — сдвигом с
голеностопом (стопа остаётся на земле); кончики пальцев — поворотом предплечья.

  blender -b --factory-startup -P vypryamit_cel.py -- цель.obj ориентиры_цели.json ориентиры_шасси.json цель_выпрямленная.obj ориентиры_выпрямленные.json [--korpus 1|0] [--vzglyad градусы] [--kadr префикс]
Ориентиры цели (оси Blender, метры тела) — файл; имена как у `kluch_vida.py --istochnik`.
"""
import sys, json, math
import bpy
import numpy as np
from mathutils import Vector, Quaternion

a = sys.argv[sys.argv.index('--') + 1:]
tgt, lm_t, lm_s, out_obj, out_lm = a[:5]
opt = {}
i = 5
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
T = {k: Vector(v) for k, v in json.load(open(lm_t, encoding='utf-8')).items()}
KORPUS = opt.get('korpus', '1') == '1'   # 1 — выпрямляется только корпус и шея («не сутулить»), 0 — и конечности
S = {k: Vector(v) for k, v in json.load(open(lm_s, encoding='utf-8')).items()}

# цепи (родитель → ребёнок)
CH = [('таз:head', 'хребет:head'), ('хребет:head', 'шея:head'), ('шея:head', 'голова:head'), ('голова:head', 'голова:tail')]
for s in ('', '.L'):
    CH += [('шея:head', 'плечо%s:head' % s), ('плечо%s:head' % s, 'предплечье%s:head' % s), ('предплечье%s:head' % s, 'предплечье%s:tail' % s),
           ('таз:head', 'бедро%s:head' % s), ('бедро%s:head' % s, 'голень%s:head' % s), ('голень%s:head' % s, 'голень%s:tail' % s)]
X = {'таз:head': T['таз:head']}
ROT = {}
for p, c in CH:
    if p not in X or c not in T:
        continue
    dt = T[c] - T[p]; ds = S[c] - S[p]
    if KORPUS and c.split(':')[0].rstrip('.L') not in ('хребет', 'шея', 'голова'):
        ds = dt                                           # конечности — в позе цели (излом пальцеходящей ноги — черта вида)
    X[c] = X[p] + ds.normalized() * dt.length
    ROT[c] = dt.normalized().rotation_difference(ds.normalized())
# точки кожи
hq = ROT.get('голова:tail', Quaternion())
# ВЗГЛЯД (`--vzglyad` градусов ниже горизонта; правило «взгляд по горизонту» 01.10): голова доворачивается вокруг
# затылочного сустава так, чтобы линия «сустав → нос» шла под этим углом
if 'vzglyad' in opt and 'нос' in T:
    d0 = hq @ (T['нос'] - T['голова:head'])
    cur = math.atan2(d0.z, -d0.y); want = -math.radians(float(opt['vzglyad']))
    hq = Quaternion(Vector((1, 0, 0)), -(want - cur)) @ hq        # поворот вокруг X Blender: морда в −Y, вверх +Z
for k in ('нос', 'подбородок', 'макушка', 'затылок'):
    if k in T:
        X[k] = X['голова:head'] + hq @ (T[k] - T['голова:head'])
for s in ('', '.L'):
    if 'пальцы' + s in T:
        X['пальцы' + s] = X['предплечье%s:tail' % s] + ROT['предплечье%s:tail' % s] @ (T['пальцы' + s] - T['предплечье%s:tail' % s])
    for k in ('носок', 'пятка'):
        if k + s in T:
            d = T[k + s] - T['голова' if False else 'голень%s:tail' % s]
            q = X['голень%s:tail' % s] + d
            X[k + s] = Vector((q.x, q.y, T[k + s].z))          # стопа на земле
# земля: самая низкая точка стопы — на нуле
z0 = min(v.z for k, v in X.items() if k.startswith(('носок', 'пятка')))
dz = min(T[k].z for k in T if k.startswith(('носок', 'пятка'))) - z0
for k in X:
    X[k] = X[k] + Vector((0, 0, dz))

names = [k for k in T if k in X]
P = np.array([T[k] for k in names]); Q = np.array([X[k] for k in names]); n = len(P)
K = np.linalg.norm(P[:, None] - P[None], axis=2) + 1e-4 * np.eye(n)
Pm = np.hstack([np.ones((n, 1)), P]); A = np.zeros((n + 4, n + 4)); A[:n, :n] = K; A[:n, n:] = Pm; A[n:, :n] = Pm.T
B = np.zeros((n + 4, 3)); B[:n] = Q; W = np.linalg.solve(A, B)
tps = lambda Y: np.linalg.norm(Y[:, None] - P[None], axis=2) @ W[:n] + np.hstack([np.ones((len(Y), 1)), Y]) @ W[n:]

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.wm.obj_import(filepath=tgt, forward_axis='NEGATIVE_Z', up_axis='Y')
ob = [o for o in bpy.data.objects if o.type == 'MESH'][0]
bpy.context.view_layer.objects.active = ob; ob.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
co = np.array([v.co for v in ob.data.vertices])
new = np.vstack([tps(co[j:j + 4000]) for j in range(0, len(co), 4000)])
for v, c in zip(ob.data.vertices, new):
    v.co = Vector(c)
bpy.ops.wm.obj_export(filepath=out_obj, export_selected_objects=False, forward_axis='NEGATIVE_Z', up_axis='Y',
                      export_materials=False, export_normals=False, export_uv=False)
json.dump({k: [round(c, 4) for c in X[k]] for k in names}, open(out_lm, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
H = max(c[2] for c in new)
print('ВЫПРЯМ: ориентиров %d, рост цели %.3f → %.3f м; голова %s → %s' % (n, max(c[2] for c in co), H,
      tuple(round(c, 3) for c in T['голова:tail']), tuple(round(c, 3) for c in X['голова:tail'])))

if 'kadr' in opt:
    ob.color = (0.75, 0.73, 0.70, 1)
    for p in ob.data.polygons:
        p.use_smooth = False
    for k in names:
        bpy.ops.mesh.primitive_uv_sphere_add(radius=0.02, location=X[k]); bpy.context.active_object.color = (0.95, 0.2, 0.15, 1)
    sc = bpy.context.scene; sc.render.engine = 'BLENDER_WORKBENCH'
    sh = sc.display.shading; sh.light = 'STUDIO'; sh.color_type = 'OBJECT'; sh.show_xray = True; sh.xray_alpha = 0.6
    sh.background_type = 'VIEWPORT'; sh.background_color = (0.15, 0.16, 0.18)
    sc.render.resolution_x, sc.render.resolution_y = 900, 1100
    cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); sc.collection.objects.link(cam); sc.camera = cam
    cam.data.type = 'ORTHO'; cam.data.ortho_scale = 2.3; cam.data.clip_end = 100
    c0 = Vector((0, 0, 1.05))
    for nm, d in (('front', (0, -1, 0)), ('side', (-1, 0, 0))):
        e = Vector(d); cam.location = c0 + e * 20; cam.rotation_euler = (c0 - cam.location).to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = opt['kadr'] + '_' + nm + '.png'; bpy.ops.render.render(write_still=True)
