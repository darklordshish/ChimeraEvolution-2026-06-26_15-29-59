"""СКЕЛЕТ ПОД МЕШ (14.10, решение геймдизайнера по лосю: «подогнать скелет под меш»): скелет вида, снятый с прежнего
тела поля, переставляется внутрь меша по листу — теми же именами, иерархией и гнёздами. Места костей — форма.

Перенос — тонкой пластиной (TPS, ядро r) по парам «точка старого скелета → та же анатомическая точка меша»: нос, пасть,
глаза, уши, затылочный сустав, основание шеи, вершина горба, тазобедренный, крестец, хвост, днище груди, суставы ног и
копыта. Пары — таблица `PARY` ниже (метры тела, оси Blender, морда в −Y), меряны по срезам меша (`los_c1.obj`, 14.10).
Остальные кости и гнёзда едут той же пластиной. `--masshtab` — общий масштаб меша и скелета от земли (размер вида в игре,
решение геймдизайнера; 1 — длина нос→хвост как у прежнего скелета).

  blender -b --factory-startup -P skelet_po_meshu.py -- меш.obj старый_скелет.json новый_скелет.json [--masshtab 1] [--kadr префикс] [--mesta раскладка.json --mesta_v выход.json]
"""
import sys, json
import bpy
import numpy as np
from mathutils import Vector

a = sys.argv[sys.argv.index('--') + 1:]
mesh_p, old_p, new_p = a[:3]
opt = dict(masshtab='1')
i = 3
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
S = float(opt['masshtab'])

# (старый скелет → меш); пары с x — на стороне +X Blender (.L), вторая сторона — зеркалом
PARY = [
    ((0, -2.4585, 1.972), (0, -2.40, 2.30)),          # нос (гнездо)
    ((0, -2.1619, 2.0483), (0, -2.16, 2.24)),         # пасть (гнездо)
    ((0.141, -1.9654, 2.4246), (0.23, -1.93, 2.72)),  # глаз
    ((0.195, -1.7024, 2.7266), (0.30, -1.66, 3.08)),  # основание уха
    ((0, -1.6503, 2.3203), (0, -1.55, 2.58)),         # затылочный сустав (начало кости `голова`)
    ((0, -0.78, 2.2401), (0, -1.00, 2.58)),           # основание шеи
    ((0, -0.6, 2.6001), (0, -0.62, 3.18)),            # вершина горба (конец кости `горб`)
    ((0, 0.15, 2.4001), (0, 0.10, 2.78)),             # начало горба — середина спины
    ((0, 0.95, 2.0), (0, 0.98, 2.50)),                # тазобедренный (начало `хребет`)
    ((0, 1.2, 2.06), (0, 1.25, 2.45)),                # крестец
    ((0, 1.5199, 2.08), (0, 1.56, 2.30)),             # кончик хвоста
    ((0, 0.3, 1.76), (0, 0.30, 1.88)),                # днище груди сзади
    ((0, -1.1001, 1.84), (0, -1.05, 1.90)),           # днище груди спереди
    ((0, -1.74, 1.9501), (0, -1.72, 1.95)),           # серьга, начало
    ((0.19, -1.16, 1.92), (0.30, -1.06, 2.20)),       # плечевой сустав
    ((0.19, -0.67, 1.4021), (0.33, -0.76, 1.55)),     # локоть
    ((0.19, -0.4790, 0.5967), (0.36, -0.79, 0.60)),   # запястье (гнездо `Руки`)
    ((0.19, -0.4645, 0.1967), (0.38, -0.90, 0.20)),   # бабка
    ((0.19, -0.5952, 0.0650), (0.36, -1.02, 0.06)),   # копыто
    ((0.21, 1.12, 2.07), (0.30, 1.05, 2.40)),         # тазобедренный сустав
    ((0.21, 1.0174, 1.34), (0.33, 0.98, 1.58)),       # колено (скакательный сверху)
    ((0.21, 1.37649, 1.90316), (0.28, 1.30, 2.05)),  # ляжка — задняя масса бедра, начало
    ((0.21, 1.40371, 1.27994), (0.28, 1.36, 1.42)),  # ляжка, конец
    ((0.21, 1.3972, 1.0931), (0.29, 1.40, 1.10)),     # гнездо `Ноги` выше скакательного
    ((0.21, 1.5138, 0.6436), (0.30, 1.52, 0.64)),     # скакательный сустав
    ((0.21, 1.4747, 0.1945), (0.33, 1.35, 0.20)),     # бабка
    ((0.21, 1.3456, 0.0626), (0.34, 1.20, 0.06)),     # копыто
]
P, Q = [], []
for p, q in PARY:
    P.append(p); Q.append(q)
    if p[0]:
        P.append((-p[0], p[1], p[2])); Q.append((-q[0], q[1], q[2]))
P, Q = np.array(P, float), np.array(Q, float)
n = len(P)
K = np.linalg.norm(P[:, None] - P[None], axis=2) + 1e-4 * np.eye(n)
Pm = np.hstack([np.ones((n, 1)), P])
A = np.zeros((n + 4, n + 4)); A[:n, :n] = K; A[:n, n:] = Pm; A[n:, :n] = Pm.T
B = np.zeros((n + 4, 3)); B[:n] = Q
W = np.linalg.solve(A, B)


def tps(X):
    X = np.atleast_2d(np.asarray(X, float))
    Y = np.linalg.norm(X[:, None] - P[None], axis=2) @ W[:n] + np.hstack([np.ones((len(X), 1)), X]) @ W[n:]
    return Y * S                                       # общий масштаб от начала координат (земля под центром тела)


sk = json.load(open(old_p, encoding='utf-8'))
r = lambda v: [round(float(c), 5) for c in v]
for b in sk['bones']:
    h, t = tps([b['head'], b['tail']])
    b['head'], b['tail'] = r(h), r(t)
for s in sk['sockets']:
    s['at'] = r(tps([s['at']])[0])
sk['note'] = ('скелет переставлен внутрь меша по листу (skelet_po_meshu.py, 14.10): те же имена, иерархия и гнёзда; '
              'масштаб ×%g от длины прежнего скелета' % S)
json.dump(sk, open(new_p, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
print('СКЕЛЕТ: пар %d, костей %d, гнёзд %d, масштаб ×%g → %s' % (n, len(sk['bones']), len(sk['sockets']), S, new_p))

# ---------- места вида (`--mesta вход.json --mesta_v выход.json`): раскладка мест в метрах тела (оси Unity) — та же пластина ----------
# pos — точка, dir — направление (переносится разностью двух точек), surface z0/z1 — вдоль тела по средней линии.
# `unit` (калибр хозяина) не трогается: его единицы — у механик
if 'mesta' in opt:
    src_m, dst_m = opt['mesta'], opt['mesta_v']
    L = json.load(open(src_m, encoding='utf-8'))
    UB = lambda u: [-u[0], -u[2], u[1]]; BU = lambda b: [-b[0], b[2], -b[1]]
    moved = []
    for p in L['places']:
        if 'pos' in p:
            b0 = np.array(UB(p['pos'])); q0 = tps([b0])[0]
            p['pos'] = r(BU(q0))
            if 'dir' in p:
                b1 = b0 + 0.05 * np.array(UB(p['dir'])); d = tps([b1])[0] - q0; d = d / max(np.linalg.norm(d), 1e-9)
                p['dir'] = [round(float(c), 4) for c in BU(d)]
            moved.append(p['name'])
        if 'surface' in p:
            for k in ('z0', 'z1'):
                y_b = -p['surface'][k]; q = tps([[0, y_b, 2.0]])[0]; p['surface'][k] = round(float(-q[1]), 4)
            moved.append(p['name'])
    L['note'] = ('места переставлены под меш по листу той же пластиной, что скелет (skelet_po_meshu.py --mesta, 14.10), '
                 'масштаб ×%g; `unit` прежние' % S)
    json.dump(L, open(dst_m, 'w', encoding='utf-8'), ensure_ascii=False, indent=2)
    print('МЕСТА: переставлено %d → %s' % (len(moved), dst_m))

# ---------- кадр: меш на просвет и кости ----------
if 'kadr' in opt:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.wm.obj_import(filepath=mesh_p, forward_axis='NEGATIVE_Z', up_axis='Y')
    o = [o for o in bpy.data.objects if o.type == 'MESH'][0]; o.color = (0.75, 0.73, 0.70, 1); o.scale = (S, S, S)
    for b in sk['bones']:
        h, t = Vector(b['head']), Vector(b['tail']); d = t - h
        bpy.ops.mesh.primitive_cylinder_add(radius=0.03 * S, depth=max(d.length, 1e-3), location=(h + t) / 2)
        c = bpy.context.active_object; c.rotation_euler = d.to_track_quat('Z', 'Y').to_euler(); c.color = (0.9, 0.15, 0.1, 1)
    for s in sk['sockets']:
        bpy.ops.mesh.primitive_uv_sphere_add(radius=0.05 * S, location=s['at']); bpy.context.active_object.color = (0.2, 0.5, 0.95, 1)
    sc = bpy.context.scene; sc.render.engine = 'BLENDER_WORKBENCH'
    sh = sc.display.shading; sh.light = 'STUDIO'; sh.color_type = 'OBJECT'; sh.show_xray = True; sh.xray_alpha = 0.55
    sh.background_type = 'VIEWPORT'; sh.background_color = (0.15, 0.16, 0.18)
    sc.render.resolution_x, sc.render.resolution_y = 1000, 900
    cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); sc.collection.objects.link(cam); sc.camera = cam
    cam.data.type = 'ORTHO'; cam.data.ortho_scale = 5.0 * S; cam.data.clip_end = 100
    c0 = Vector((0, -0.45, 2.0)) * S
    for nm, d in (('side', (1, 0, 0)), ('front', (0, -1, 0)), ('top', (0, 0, 1))):
        e = Vector(d); cam.location = c0 + e * 20; cam.rotation_euler = (c0 - cam.location).to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = opt['kadr'] + '_' + nm + '.png'; bpy.ops.render.render(write_still=True)
