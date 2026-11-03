"""РЯД ПО БЮДЖЕТУ (03.11, правило геймдизайнера на вехи формы: ×1 · ×0.8 · ×0.75 · ×0.67, для проверки низа ещё ×0.5 · ×0.4). Вариант прореживанием базы вида без ключей: квадратичное схлопывание по X-симметрии, по областям объекта. Вершины края объекта (кольца швов) держатся всегда, линии перелома — флагом `--linii 1` (на низких бюджетах пресс и ости не спасаем, критерий 03.11: контур и крупные массы). Кисть и голова идут своей долей `--dolya_kg`; остальное тело получает, что остаётся до `--cel` (три итерации, потому что удержанное не схлопывается). Веса — 4 кости с нормировкой. Печатает РЯД: треугольники, сдвиг края, IoU анфас и профиль (правило `stend_k2.py`), снимает кадры.

  blender -b база.blend --factory-startup --python-exit-code 1 -P ryad_byudzheta.py -- выход.blend папка_кадров префикс --cel 3436 --dolya_kg 1 --linii 1 --maski front.png,profile.png --pasporta шов.json,…
"""
import sys, json, math, os
import bpy, bmesh
import numpy as np
from mathutils import Vector

a = sys.argv[sys.argv.index('--') + 1:]
OUT, KADR, PR = [os.path.abspath(a[0]), os.path.abspath(a[1]), a[2]]
opt = {a[i][2:]: a[i + 1] for i in range(3, len(a) - 1, 2)}
CEL = int(opt['cel']); DKG = float(opt.get('dolya_kg', 1)); LIN = int(opt.get('linii', 1)); ZKIST = 0.9
VHOD = bpy.data.filepath; os.makedirs(KADR, exist_ok=True)
KG = ('голова', 'Чутьё')


def vidimye(): return [o for o in bpy.data.objects if o.type == 'MESH' and '@' not in o.name]
def tr(): return sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in vidimye())


def tochki(o): return np.array([tuple(o.matrix_world @ v.co) for v in o.data.vertices])


KOLCA = np.zeros((0, 3))
for f_ in filter(None, opt.get('pasporta', '').split(',')):
    s_ = json.load(open(f_, encoding='utf-8'))['seam']; KOLCA = np.concatenate([KOLCA, np.array([[-u[0], -u[2], u[1]] for u in s_['ring_m']])])


def pary():
    O = vidimye(); P = [tochki(x) for x in O]; n = 0
    for i in range(len(O)):
        for j in range(i + 1, len(O)): n += int((np.linalg.norm(P[i][:, None] - P[j][None], axis=2) < 2e-4).sum())
    return n


def oblast(o, f, kisti):
    """кисть и голова — отдельная область: у них своя доля"""
    if o.name in KG: return kisti
    if o.name == 'Руки': return (o.matrix_world @ f.calc_center_median()).z < ZKIST if kisti else (o.matrix_world @ f.calc_center_median()).z >= ZKIST
    return not kisti


def prorezhivanie(o, kisti, r):
    if r >= 0.999: return
    for x in bpy.context.view_layer.objects: x.select_set(False)
    bpy.context.view_layer.objects.active = o; o.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT'); bm = bmesh.from_edit_mesh(o.data); bm.faces.ensure_lookup_table()
    drz = {v.index for v in bm.verts if any(e.is_boundary for e in v.link_edges)}
    # пристроенное к коже изнутри объекта: уши и ноздри на голове, кольцо «основание черепа» — не край, но шов
    M = o.matrix_world; Vo = np.array([tuple(M @ v.co) for v in bm.verts])
    for T_, por in ((np.concatenate([tochki(x) for x in vidimye() if x != o]), 2e-4), (KOLCA, 2.5e-3)):
        if len(T_): drz |= set(np.nonzero(np.linalg.norm(Vo[:, None] - T_[None], axis=2).min(1) < por)[0].tolist())
    if '~опора' in o.vertex_groups:
        dl = bm.verts.layers.deform.active; gi = o.vertex_groups['~опора'].index
        drz |= {v.index for v in bm.verts if gi in v[dl]}
    al = bm.edges.layers.int.get('перелом')
    if LIN and al is not None: drz |= {v.index for e in bm.edges if e[al] > 0 for v in e.verts}
    bpy.ops.mesh.select_all(action='DESELECT'); n = 0
    for f in bm.faces:
        s = oblast(o, f, kisti); f.select_set(s); n += s
    bm.select_flush(True); bmesh.update_edit_mesh(o.data)
    bpy.ops.object.mode_set(mode='OBJECT')
    if n:
        g = o.vertex_groups.new(name='~держать'); g.add(list(drz), 1.0, 'REPLACE'); o.vertex_groups.active = g
        bpy.ops.object.mode_set(mode='EDIT')
        bpy.ops.mesh.decimate(ratio=r, use_vertex_group=True, vertex_group_factor=1000, invert_vertex_group=True, use_symmetry=True, symmetry_axis='X')
        bpy.ops.object.mode_set(mode='OBJECT'); o.vertex_groups.remove(o.vertex_groups['~держать'])


def krai():
    R = {}
    for o in vidimye():
        bm = bmesh.new(); bm.from_mesh(o.data); R[o.name] = np.array(sorted(tuple(round(float(q), 5) for q in v.co) for v in bm.verts if any(e.is_boundary for e in v.link_edges))); bm.free()
    return R


K0 = krai(); N0 = tr(); PARY0 = pary()
# доля тела: цель минус кисть и голова; итерации — удержанное не схлопывается
rb = None; nz = None
bm0 = {}
for o in vidimye():
    bm = bmesh.new(); bm.from_mesh(o.data); bm.faces.ensure_lookup_table()
    bm0[o.name] = (sum(1 for f in bm.faces if oblast(o, f, True)), sum(1 for f in bm.faces if oblast(o, f, False))); bm.free()
Nkg = sum(x[0] for x in bm0.values()); Nt = sum(x[1] for x in bm0.values())
rb = (CEL - DKG * Nkg) / Nt
for it in range(0 if opt.get('bez_prorezh') else 4):
    bpy.ops.wm.open_mainfile(filepath=VHOD)
    for o in vidimye():
        prorezhivanie(o, True, DKG); prorezhivanie(o, False, rb)
        bpy.context.view_layer.objects.active = o
    nz = tr()
    print('РЯД итерация %d: доля тела %.3f → %d тр' % (it, rb, nz))
    if abs(nz - CEL) / CEL < 0.012 or CEL >= N0: break
    # схлопывается только свободное: поправка от фактически полученной доли
    r_fakt = (nz - DKG * Nkg) / Nt
    rb = max(0.03, min(1.0, rb * (CEL - DKG * Nkg) / max(r_fakt * Nt, 1)))
for o in vidimye():
    for x in bpy.context.view_layer.objects: x.select_set(False)
    bpy.context.view_layer.objects.active = o; o.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT'); bpy.ops.mesh.quads_convert_to_tris(); bpy.ops.object.mode_set(mode='OBJECT')
    if '~опора' in o.vertex_groups: o.vertex_groups.remove(o.vertex_groups['~опора'])
    bpy.ops.object.vertex_group_limit_total(group_select_mode='ALL', limit=4)
    bpy.ops.object.vertex_group_normalize_all(group_select_mode='ALL', lock_active=False)


def ostrye():
    """острая грань: длинное ребро больше 8 высот при площади от 2 см² — читается шипом, а не плоскостью"""
    n = 0; pl = 0.0
    for o in vidimye():
        bm = bmesh.new(); bm.from_mesh(o.data); bmesh.ops.triangulate(bm, faces=bm.faces[:])
        for f in bm.faces:
            s = f.calc_area(); L = max(e.calc_length() for e in f.edges)
            if s > 2e-4 and L / max(2 * s / L, 1e-6) > 8: n += 1; pl += s
        bm.free()
    return n, round(pl * 1e4, 1)


def vyvernutye():
    """рёбра с несогласованным обходом граней: на них граница вывернутого участка"""
    n = 0
    for o in vidimye():
        bm = bmesh.new(); bm.from_mesh(o.data); n += sum(1 for e in bm.edges if len(e.link_faces) == 2 and not e.is_contiguous); bm.free()
    return n


def rebra_po_cepyam():
    """цепи опорных точек становятся рёбрами поворотом ребра; плоское выравнивается без смены положения вершин: тонкие треугольники на ровном месте уходят"""
    CH = json.load(open(opt['cepi'], encoding='utf-8')); est = net = 0
    for o in vidimye():
        bm = bmesh.new(); bm.from_mesh(o.data); bm.verts.ensure_lookup_table(); V_ = {tuple(round(float(q), 4) for q in o.matrix_world @ v.co): v for v in bm.verts}; derzh = set()
        for ch in CH:
            P = [V_.get(tuple(round(q, 4) for q in p)) for p in ch]
            for u, v in zip(P, P[1:]):
                if u is None or v is None or u is v: continue
                e = next((x for x in u.link_edges if x.other_vert(u) is v), None)
                if e is None:
                    for f in u.link_faces:
                        kr = next((x for x in f.edges if u not in x.verts), None)
                        if kr is not None and len(kr.link_faces) == 2 and v in [q for g in kr.link_faces for q in g.verts] and not kr.is_boundary and kr not in derzh:
                            r_ = bmesh.ops.rotate_edges(bm, edges=[kr], use_ccw=False)['edges']
                            if r_ and {u, v} == set(r_[0].verts): e = r_[0]
                            elif r_: bmesh.ops.rotate_edges(bm, edges=[r_[0]], use_ccw=True)
                            if e is not None: break
                if e is None: net += 1
                else: est += 1; derzh.add(e)
        bm.normal_update()
        ploskie = [e for e in bm.edges if len(e.link_faces) == 2 and e not in derzh and e.calc_face_angle(9) < math.radians(12)]
        if ploskie: bmesh.ops.beautify_fill(bm, faces=bm.faces[:], edges=ploskie, method='ANGLE')
        bm.to_mesh(o.data); bm.free(); o.data.update()
    print('РЯД: цепи рёбрами %d, не соединились %d' % (est, net)); return [est, net]


def soglasovat():
    """поворот ребра и нарезка могут вывернуть грань: она не рисуется, в теле видно окно. Обход согласуется внутри острова, а сторона острова — по большинству площади: чистый recalc на открытом объекте выворачивает его наружу целиком"""
    n = 0
    for o in vidimye():
        bm = bmesh.new(); bm.from_mesh(o.data); bm.normal_update(); ost = set(bm.faces)
        while ost:
            st = [next(iter(ost))]; ch = set()
            while st:
                f_ = st.pop()
                if f_ in ch: continue
                ch.add(f_); st += [g for e in f_.edges for g in e.link_faces if g not in ch]
            ost -= ch; old = {f_: (f_.normal.copy(), f_.calc_area()) for f_ in ch}
            bmesh.ops.recalc_face_normals(bm, faces=list(ch)); bm.normal_update()
            if sum(a_ * f_.normal.dot(n_) for f_, (n_, a_) in old.items()) < 0:
                bmesh.ops.reverse_faces(bm, faces=list(ch)); bm.normal_update()
            n += sum(1 for f_, (n_, _) in old.items() if f_.normal.dot(n_) < 0)
        bm.to_mesh(o.data); bm.free(); o.data.update()
    print('РЯД: согласовано вывернутых граней %d' % n); return n


CEPI_OT = rebra_po_cepyam() if opt.get('cepi') else None
SOGL = soglasovat() if opt.get('cepi') or opt.get('bez_prorezh') else None
K1 = krai(); sd = []
for n, A in K0.items():
    B = K1[n]; sd.append(float(np.linalg.norm(A[:, None] - B[None], axis=2).min(1).max()) if len(A) else 0.0)
bpy.ops.wm.save_as_mainfile(filepath=OUT)


# IoU: то же правило, что в stend_k2.py
def rastr(T, n):
    im = np.zeros((n, n), bool)
    for t in T:
        x0, y0 = np.floor(t.min(0)).astype(int); x1, y1 = np.ceil(t.max(0)).astype(int)
        x0, y0 = max(x0, 0), max(y0, 0); x1, y1 = min(x1, n - 1), min(y1, n - 1)
        if x1 < x0 or y1 < y0: continue
        X_, Y_ = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
        (ax, ay), (bx, by), (cx, cy) = t
        d = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
        if abs(d) < 1e-12: continue
        l1 = ((by - cy) * (X_ - cx) + (cx - bx) * (Y_ - cy)) / d; l2 = ((cy - ay) * (X_ - cx) + (ax - cx) * (Y_ - cy)) / d
        im[y0:y1 + 1, x0:x1 + 1] |= (l1 >= 0) & (l2 >= 0) & (1 - l1 - l2 >= 0)
    return im


def norm512(fig):
    ys, xs = np.where(fig); fig = fig[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
    h, w = fig.shape; side = int(max(h, w) * 1.1); c = np.zeros((side, side), bool)
    c[side - h - (side - h) // 20: side - (side - h) // 20, (side - w) // 2:(side - w) // 2 + w] = fig
    idx = (np.arange(512) * side / 512).astype(int)
    return c[idx][:, idx]


def silujet(ax_, sgn):
    T = []
    for o in vidimye():
        M = o.matrix_world; P = np.array([tuple(M @ v.co) for v in o.data.vertices]); o.data.calc_loop_triangles()
        T.append(P[np.array([tuple(t.vertices) for t in o.data.loop_triangles])])
    T = np.concatenate(T); u = T[..., ax_] * sgn; v = T[..., 2]
    S = 1000 / max(u.max() - u.min(), v.max() - v.min())
    return norm512(rastr(np.stack([(u - u.min()) * S + 10, (v.max() - v) * S + 10], -1), 1024))


def maska(path):
    im = bpy.data.images.load(os.path.abspath(path)); w, h = im.size
    px = np.array(im.pixels[:]).reshape(h, w, im.channels)[::-1, :, 0]
    return px[(np.arange(512) * h / 512).astype(int)][:, (np.arange(512) * w / 512).astype(int)] < 0.5


def iou(s, m): return float((s & m).sum() / max((s | m).sum(), 1))


mf, mp = opt['maski'].split(','); Mf, Mp = maska(mf), maska(mp); sf, sp = silujet(0, -1), silujet(1, 1)
If, Ip = iou(sf, Mf), max(iou(sp, Mp), iou(sp[:, ::-1], Mp))
otch = dict(tr=tr(), cel=CEL, dolya_telo=round(rb, 3), dolya_kg=DKG, linii=LIN, sdvig_kraya_mm=round(max(sd) * 1000, 2), pary_shvov=[PARY0, pary()], ostrye=ostrye(), vyvernutye=vyvernutye(), cepi=CEPI_OT, iou_front=round(If, 3), iou_profile=round(Ip, 3), obekty={o.name: len(o.data.polygons) for o in vidimye()})
json.dump(otch, open(os.path.join(KADR, PR + '.json'), 'w', encoding='utf-8'), ensure_ascii=False)
print('РЯД: %s' % json.dumps(otch, ensure_ascii=False))

# кадры: крупно и с дистанции (фигура ~200 px: 297 px на 2.75 м)
sc = bpy.context.scene
for o in bpy.data.objects:
    if o.type == 'MESH':
        vid = '@' not in o.name; o.hide_render = not vid
        if vid:
            o.color = (0.72, 0.66, 0.60, 1)
            for p in o.data.polygons: p.use_smooth = False
w = sc.display; sc.render.engine = 'BLENDER_WORKBENCH'; w.shading.light = 'STUDIO'; w.shading.color_type = 'OBJECT'; w.shading.show_backface_culling = True; w.shading.show_shadows = False; w.shading.show_cavity = False; w.shading.show_object_outline = False
sc.world = sc.world or bpy.data.worlds.new('w'); sc.world.color = (0.82, 0.83, 0.84); sc.render.film_transparent = False; sc.render.image_settings.file_format = 'PNG'


def cam(naz, yaw, nakl):
    d = bpy.data.cameras.new(naz); d.type = 'ORTHO'; d.ortho_scale = 2.05; o = bpy.data.objects.new(naz, d); sc.collection.objects.link(o)
    y, n = math.radians(yaw), math.radians(nakl); nap = Vector((math.sin(y) * math.cos(n), -math.cos(y) * math.cos(n), math.sin(n)))
    o.location = Vector((0, 0, 0.93)) + nap * 6; o.rotation_euler = (-nap).to_track_quat('-Z', 'Y').to_euler(); return o


K = dict(front=cam('front', 0, 4), q34=cam('q34', 40, 6), profile=cam('profile', 90, 0), back=cam('back', 180, 4), q34back=cam('q34back', 218, 8))
sc.render.resolution_x, sc.render.resolution_y = 420, 620
for k, c in K.items():
    sc.camera = c; sc.render.filepath = os.path.join(KADR, '%s_%s.png' % (PR, k)); bpy.ops.render.render(write_still=True)
sc.render.resolution_x, sc.render.resolution_y = 150, 222   # 1.85 м на 2.05 орто-кадра: 222 px по высоте → фигура ~200 px
for k in ('q34back', 'front', 'profile'):
    sc.camera = K[k]; sc.render.filepath = os.path.join(KADR, '%s_dal_%s.png' % (PR, k)); bpy.ops.render.render(write_still=True)
