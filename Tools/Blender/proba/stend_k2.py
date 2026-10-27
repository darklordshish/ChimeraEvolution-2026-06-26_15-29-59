"""СТЕНД В BLENDER, РАВНЫЙ ИГРЕ, С ДЕТЕКТОРАМИ (27.10, пункт Б разбора тракта: цикл «правка → Unity → критик» дорог, а Blender-кадры
врали игре — в Blender было до 8 костей на вершину и поза ставилась не по игровому правилу). Стенд берёт то, что получает игра, и
ставит его так, как ставит игра; меряет числами то, что раньше ловил глаз критика.

Как в игре:
  - ВЫГРУЖЕННЫЙ FBX (оси как у eksport_k2.py), у костей снят `use_connect` (иначе Blender держит начало кости на конце родителя и
    игнорирует сустав позы — находка дизайн-линии 24.10); веса проверяются: не больше 4 костей (Unity режет до 4);
  - поза — `KeyPose.Apply` (Assets/_Chimera/Scripts/Player/KeyPose.cs): сверху вниз, поворот D = рамка позы · рамка покоя рига⁻¹
    (`chelovek-pokoj.json`) поверх покоя кости, начало кости — в сустав позы; своя поза ступени ставится целиком (вес 1);
  - ключ вида — на вес ступени; черта `@<ключ>` ступени и то, что она снимает (`skryvaet`), — из записи ступени поставки
    (`klyuchi/<шасси>-<вид>-st<N>.json`); органы на шаблоне `<слот>@<вид>` с формой «рост» — по `--organy`.

Детекторы (всё в мировых метрах Blender, отчёт — JSON и строка СТЕНД):
  - РАЗВОРОТ КЛЮЧА: грани, у которых нормаль в покое с ключом смотрит против базовой (тёмные провалы в игре);
  - РАЗВОРОТ В ПОЗЕ: грани шасси, у которых нормаль в позе против БАЗОВОЙ нормали шаблона, повёрнутой главной костью грани, —
    то, что игра покажет тёмным провалом; не зависит от того, как ключ разложен между покоем и весами (перевязка его не меняет);
  - СКЛАДКА В ПОЗЕ: грани, у которых нормаль в позе против нормали покоя, повёрнутой главной костью грани (кожу смяло скиннингом),
    и грани, сжатые в позе меньше чем на 25 % площади — в сумме по ШАССИ; у кусков `@` покой — обратный скиннинг смятой цели, их
    числа в `obekty` справочные (материал черт двусторонний);
  - ЗАЗОР ЧЕРТЫ: кусок `@` и орган на шаблоне в позе — от куска до ближайшей вершины шасси, мм (больше 0 — кусок висит в воздухе);
  - ШОВ: вершины разных объектов в одной базовой точке, разошедшиеся в позе больше 1 мм (щель на шве);
  - ПОХОЖЕСТЬ НА ЛИСТ: IoU силуэта анфас и профиль с маской листа (`--maski front.png,profile.png`, маски — `Docs/tools/sheet_mask.py`;
    силуэт нормируется тем же правилом: фигура по ширине в центре, низ на земле, 512×512). Профиль меряется в обе стороны.
Кадры (`--kadr префикс`): Workbench, отсечение задних граней, плоские грани — анфас, профиль, 3/4.

  blender -b --factory-startup -P stend_k2.py -- chelovek_mesh.fbx --pokoj klyuchi/chelovek-pokoj.json
      [--kluch Волк --ves 1 --poza klyuchi/chelovek-volk-poza.json --stupen klyuchi/chelovek-volk-st2.json]
      [--organy Чутьё@Волк:1,Шкура@Волк:1] [--skryt_organy klyuchi/../organy/chelovek.json] [--maski f.png,p.png]
      [--bez @Волк] [--poza_obj тело.obj] [--kadr префикс] [--otchet отчёт.json]
"""
import sys, json, math
import bpy, bmesh
import numpy as np
from mathutils import Matrix, Vector

a = sys.argv[sys.argv.index('--') + 1:]
FBX = a[0]
opt = {}
i = 1
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
KEY, W = opt.get('kluch'), float(opt.get('ves', '1'))
import os
for k_ in ('maski', 'kadr', 'otchet', 'poza_obj'):   # Blender читает и пишет картинки не от рабочей папки — пути абсолютные
    if opt.get(k_): opt[k_] = ','.join(os.path.abspath(x) for x in opt[k_].split(','))

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=FBX, axis_forward='-Z', axis_up='Y', automatic_bone_orientation=False)
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
MESH = [o for o in bpy.data.objects if o.type == 'MESH']
BONES = {b.name for b in arm.data.bones}
otchet = dict(fbx=FBX, kluch=KEY, ves=W, poza=opt.get('poza'))

# веса — как получит игра
over = sum(1 for o in MESH for v in o.data.vertices
           if sum(1 for g in v.groups if o.vertex_groups[g.group].name in BONES and g.weight > 0) > 4)
otchet['vesa_bolshe_4'] = over

# что видно на ступени
skryt, cherta = set(), None
BEZ = set(opt['bez'].split(',')) if opt.get('bez') else set()
if opt.get('stupen'):
    st = json.load(open(opt['stupen'], encoding='utf-8')); cherta = st.get('object'); skryt |= set(st.get('skryvaet', []))
ORG = dict((k, float(v)) for k, v in (x.split(':') for x in opt.get('organy', '').split(',') if x))
if opt.get('skryt_organy'):
    for r in json.load(open(opt['skryt_organy'], encoding='utf-8')).get('organy', []):
        if r['object'] in ORG and ORG[r['object']] > 0: skryt |= set(r.get('skryvaet', []))
vid = []
for o in MESH:
    slot = o.name.split('@')[0] if '@' in o.name and not o.name.startswith('@') else o.name
    show = (o.name == cherta) if o.name.startswith('@') else (o.name in ORG) if '@' in o.name else (o.name not in skryt)
    if o.name in BEZ: show = False   # `--bez @Волк` — кадр без куска (сверка тела под гривой, дизайн-линия 27.10)
    o.hide_render = o.hide_viewport = not show
    if show: vid.append(o)
    if o.data.shape_keys:
        for kb in o.data.shape_keys.key_blocks:
            kb.value = (W if kb.name == KEY else ORG.get(o.name, 0.0) if kb.name == 'рост' else 0.0)
otchet['vidno'] = [o.name for o in vid]


def ramka(p):
    h, t, z = (Vector(p[k]) for k in ('head', 'tail', 'z_axis'))
    y = (t - h).normalized(); x = y.cross(z).normalized(); z = x.cross(y)
    return h, Matrix((x, y, z)).transposed()


# поза — по правилу игры
bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode='EDIT')
for eb in arm.data.edit_bones: eb.use_connect = False
bpy.ops.object.mode_set(mode='POSE')
AW = arm.matrix_world
if opt.get('poza'):
    PZ = {b['name']: b for b in json.load(open(opt['poza'], encoding='utf-8'))['bones']}
    RP = {b['name']: b for b in json.load(open(opt['pokoj'], encoding='utf-8'))['bones']} if opt.get('pokoj') else {}
    order = []
    def walk(b):
        order.append(b.name)
        for c in b.children: walk(c)
    for b in arm.data.bones:
        if b.parent is None: walk(b)
    rest = {bn: (AW @ arm.data.bones[bn].matrix_local) for bn in order}
    for bn in order:
        if bn not in PZ: continue
        h, F = ramka(PZ[bn]); R0 = rest[bn].to_3x3().normalized()
        R = (F @ ramka(RP[bn])[1].inverted() @ R0) if bn in RP else F
        arm.pose.bones[bn].matrix = AW.inverted() @ (Matrix.Translation(h) @ R.to_4x4())
        bpy.context.view_layer.update()
bpy.ops.object.mode_set(mode='OBJECT')
dg = bpy.context.evaluated_depsgraph_get()


def geom(o, posed):
    mods = [m for m in o.modifiers if m.type == 'ARMATURE']
    for m in mods: m.show_viewport = posed
    bpy.context.view_layer.update(); d = bpy.context.evaluated_depsgraph_get()
    e = o.evaluated_get(d); me = e.to_mesh()
    P = np.array([o.matrix_world @ v.co for v in me.vertices]); F = [tuple(p.vertices) for p in me.polygons]
    e.to_mesh_clear()
    for m in mods: m.show_viewport = True
    return P, F


def normali(P, F):
    N = np.zeros((len(F), 3)); A = np.zeros(len(F))
    for k, f in enumerate(F):
        n = np.zeros(3)
        for j in range(1, len(f) - 1): n += np.cross(P[f[j]] - P[f[0]], P[f[j + 1]] - P[f[0]])
        A[k] = np.linalg.norm(n) / 2; N[k] = n / (np.linalg.norm(n) or 1)
    return N, A


skin = {pb.name: np.array((AW @ pb.matrix) @ (AW @ pb.bone.matrix_local).inverted())[:3, :3] for pb in arm.pose.bones}
razv, razp, skl, szh, per_obj, POS, BASE = 0, 0, 0, 0, {}, [], []
for o in vid:
    gn = {g.index: g.name for g in o.vertex_groups}
    base = np.array([o.matrix_world @ v.co for v in o.data.vertices])
    rest_k, F = geom(o, False); pose_, _ = geom(o, True)
    Nb, _ = normali(base, F); Nr, Ar = normali(rest_k, F); Np, Ap = normali(pose_, F)
    glav = [max(((gn[g.group], g.weight) for g in v.groups if gn[g.group] in skin), key=lambda x: x[1], default=(None, 0))[0]
            for v in o.data.vertices]
    r_ = s_ = c_ = p_ = 0
    for k, f in enumerate(F):
        if Ar[k] < 1e-10: continue
        if Nb[k] @ Nr[k] < 0: r_ += 1
        b = glav[f[0]]; R_ = skin[b] if b else np.eye(3)
        if Np[k] @ (R_ @ Nb[k]) < 0: p_ += 1
        if Np[k] @ (R_ @ Nr[k]) < 0: s_ += 1
        elif Ap[k] / Ar[k] < 0.25: c_ += 1
    per_obj[o.name] = dict(granej=len(F), razvorot=r_, razvorot_v_poze=p_, skladka=s_, szhatie=c_)
    razv += r_
    if '@' not in o.name: skl += s_; szh += c_; razp += p_   # у кусков покой — обратный скиннинг смятой цели: складка «поза против покоя» не определена
    if '@' not in o.name: POS.append(pose_); BASE.append(base)
# швы: совпадающие базовые точки разных объектов шасси
seen, shov = {}, []
for P_, B_ in zip(POS, BASE):
    for p, b in zip(P_, B_):
        k = tuple(np.round(b, 4))
        if k in seen: shov.append(float(np.linalg.norm(seen[k] - p)))
        else: seen[k] = p
otchet.update(razvorot_klyucha=razv, razvorot_v_poze=razp, skladka_v_poze=skl, szhatie_v_poze=szh, obekty=per_obj,
              shov_vershin=len(shov), shov_razoshlos_1mm=int(sum(d > 1e-3 for d in shov)), shov_maks_mm=round(max(shov, default=0) * 1000, 3))


# ЧЕРТА НА ТЕЛЕ: кусок `@` и орган `<слот>@<вид>` в позе обязаны касаться тела шасси — зазор = от куска до ближайшей вершины
# шасси (27.10: хвост @Волк1 на ст.1 висел в 15 см за спиной и в игре, и на стенде, а ни одна мера его не ловила)
from mathutils import kdtree
_ch = np.concatenate(POS) if POS else np.zeros((0, 3))
_kd = kdtree.KDTree(len(_ch))
for k_, p_ in enumerate(_ch): _kd.insert(Vector(p_), k_)
_kd.balance(); otchet['zazor_mm'] = {}
for o in vid:
    if '@' not in o.name: continue
    P_, _ = geom(o, True)
    otchet['zazor_mm'][o.name] = round(min(_kd.find(Vector(p_))[2] for p_ in P_) * 1000, 1)


# силуэт: свои треугольники в орто-проекции → нормировка как sheet_mask.py → IoU с маской (без PIL: в Python Blender'а его нет)
def rastr(T, n):
    """заливка треугольников (n×n), T — [k,3,2] в пикселях"""
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
    for o in vid:
        P, F = geom(o, True)
        for f in F:
            for j in range(1, len(f) - 1): T.append([P[f[0]], P[f[j]], P[f[j + 1]]])
    T = np.array(T); u = T[..., ax_] * sgn; v = T[..., 2]
    S = 1000 / max(u.max() - u.min(), v.max() - v.min())
    return norm512(rastr(np.stack([(u - u.min()) * S + 10, (v.max() - v) * S + 10], -1), 1024))


def maska(path):
    im = bpy.data.images.load(path); w, h = im.size
    px = np.array(im.pixels[:]).reshape(h, w, im.channels)[::-1, :, 0]
    idx_y = (np.arange(512) * h / 512).astype(int); idx_x = (np.arange(512) * w / 512).astype(int)
    return px[idx_y][:, idx_x] < 0.5


def sohranit(path, rgb):
    h, w, _ = rgb.shape; im = bpy.data.images.new('s', w, h)
    im.pixels[:] = np.concatenate([rgb[::-1].astype(float), np.ones((h, w, 1))], -1).ravel()
    im.filepath_raw = path; im.file_format = 'PNG'; im.save()


if opt.get('maski'):
    mf, mp = opt['maski'].split(',')
    def iou(s, m): return float((s & m).sum() / max((s | m).sum(), 1))
    Mf, Mp = maska(mf), maska(mp)
    sf = silujet(0, -1); sp = silujet(1, 1)
    otchet['iou_front'] = round(iou(sf, Mf), 3)
    otchet['iou_profile'] = round(max(iou(sp, Mp), iou(sp[:, ::-1], Mp)), 3)
    if iou(sp[:, ::-1], Mp) > iou(sp, Mp): sp = sp[:, ::-1]
    if opt.get('kadr'):   # сверка: красное — только тело, синее — только лист, чёрное — совпало
        for nm, s_, m_ in (('front', sf, Mf), ('profile', sp, Mp)):
            rgb = np.ones((512, 512, 3)); both = s_ & m_
            rgb[s_ & ~m_] = (0.9, 0.2, 0.2); rgb[m_ & ~s_] = (0.3, 0.5, 0.95); rgb[both] = (0.15, 0.15, 0.15)
            sohranit(opt['kadr'] + '_iou_' + nm + '.png', rgb)

# кадры: Workbench, отсечение задних граней (как в игре)
if opt.get('kadr'):
    sc = bpy.context.scene; sc.render.engine = 'BLENDER_WORKBENCH'
    sh = sc.display.shading; sh.light = 'STUDIO'; sh.color_type = 'SINGLE'; sh.single_color = (0.72, 0.68, 0.62)
    sh.show_backface_culling = True; sh.show_object_outline = True
    sc.render.resolution_x, sc.render.resolution_y = 900, 1200; sc.render.film_transparent = False
    w_ = bpy.data.worlds.new('w'); w_.color = (1, 1, 1); sc.world = w_
    for me in {o.data for o in vid}:
        for p in me.polygons: p.use_smooth = False
    P = np.concatenate([geom(o, True)[0] for o in vid]); c = (P.min(0) + P.max(0)) / 2; H = (P.max(0) - P.min(0)).max() * 1.1
    cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); sc.collection.objects.link(cam); sc.camera = cam
    cam.data.type = 'ORTHO'; cam.data.ortho_scale = H
    for nm, ang in (('front', 0), ('profile', 90), ('34', 35)):
        r = math.radians(ang); d = Vector((math.sin(r), -math.cos(r), 0))
        cam.location = Vector(c) + d * 10; cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = opt['kadr'] + '_' + nm + '.png'; bpy.ops.render.render(write_still=True)

# `--poza_obj файл.obj` — тело шасси в позе, как его покажет игра (без кусков `@`) — для меры шума поверхности (shum.py)
if opt.get('poza_obj'):
    with open(opt['poza_obj'], 'w') as fo:
        base_ = 1
        for o in vid:
            if '@' in o.name: continue
            Pp, Fp = geom(o, True)
            for p in Pp: fo.write('v %.6f %.6f %.6f\n' % (p[0], p[2], -p[1]))
            for f in Fp: fo.write('f ' + ' '.join(str(base_ + j) for j in f) + '\n')
            base_ += len(Pp)
print('СТЕНД: ' + json.dumps({k: v for k, v in otchet.items() if k != 'obekty'}, ensure_ascii=False))
if opt.get('otchet'):
    json.dump(otchet, open(opt['otchet'], 'w', encoding='utf-8'), ensure_ascii=False, indent=2)
