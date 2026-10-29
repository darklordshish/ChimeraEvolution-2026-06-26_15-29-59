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
      [--bez @Волк] [--dyry 1] [--dvustoronnie_organy 0] [--st1 0.5] [--kluchi Волк·Руки:0.5] [--poza_obj тело.obj] [--kadr префикс] [--otchet отчёт.json]
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
skryt, cherta, skryt_st = set(), None, set()
BEZ = set(opt['bez'].split(',')) if opt.get('bez') else set()
if opt.get('stupen'):
    st = json.load(open(opt['stupen'], encoding='utf-8')); cherta = st.get('object'); skryt |= set(st.get('skryvaet', []))
# черта ступени снимает и органы своего слота (`BodyMesh.Place`: stageHides по SlotOf, 29.10 — стенд показывал Чутьё@/Шкура@Волк
# под головой и гривой ступени, игра их снимает); правило органа (`skryt_organy`) органы не снимает
skryt_st = set(skryt)
ORG = dict((k, float(v)) for k, v in (x.split(':') for x in opt.get('organy', '').split(',') if x))
if opt.get('skryt_organy'):
    for r in json.load(open(opt['skryt_organy'], encoding='utf-8')).get('organy', []):
        if r['object'] in ORG and ORG[r['object']] > 0: skryt |= set(r.get('skryvaet', []))
# ВЕСА КЛЮЧЕЙ — КАК В ИГРЕ (main f18b93e, 28.10): есть у тела ключ «<КЛЮЧ>1» (тело ступени 1) — вес рисуется ломаной база →
# <КЛЮЧ>1 → <КЛЮЧ>: на весе ступени 1 (`--st1` 0.5) ровно <КЛЮЧ>1, на 1 ровно <КЛЮЧ>. `--kluchi имя:вес,…` — добавочные ключи
# (ключи областей «Волк·Руки» поверх тела ступени — проверка перебора при одном органе)
ST1W = float(opt.get('st1', '0.5')); VES = {}
if KEY:
    imena = {kb.name for o in MESH if o.data.shape_keys for kb in o.data.shape_keys.key_blocks}
    if KEY + '1' in imena:
        if W <= ST1W: VES[KEY + '1'] = W / ST1W
        else: VES[KEY + '1'] = (1 - W) / (1 - ST1W); VES[KEY] = (W - ST1W) / (1 - ST1W)
    else: VES[KEY] = W
for x in opt.get('kluchi', '').split(','):
    if x: VES[x.split(':')[0]] = float(x.split(':')[1])
vid = []
for o in MESH:
    slot = o.name.split('@')[0] if '@' in o.name and not o.name.startswith('@') else o.name
    show = (o.name == cherta) if o.name.startswith('@') else (o.name in ORG and slot not in skryt_st) if '@' in o.name else (o.name.split('.')[0] not in skryt)
    if o.name in BEZ: show = False   # `--bez @Волк` — кадр без куска (сверка тела под гривой, дизайн-линия 27.10)
    o.hide_render = o.hide_viewport = not show
    if show: vid.append(o)
    if o.data.shape_keys:
        for kb in o.data.shape_keys.key_blocks:
            kb.value = VES.get(kb.name, ORG.get(o.name, 0.0) if kb.name == 'рост' else 0.0)
otchet['vidno'] = [o.name for o in vid]; otchet['kluchi'] = {k: round(v, 3) for k, v in VES.items() if v}


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
    # IoU В ПОЛОСЕ «колено — скакательный» (`--polosa_nog 1`, 28.10, координатор: отделить форму колена от расстановки ног —
    # анфас целиком падал при любом сужении голени): высоты суставов в позе (голень:head, стопа:head, среднее двух ног) → доли
    # роста фигуры → строки нормированного силуэта; IoU анфаса и профиля только по этим строкам
    if opt.get('polosa_nog'):
        am = [x for x in bpy.data.objects if x.type == 'ARMATURE'][0]
        zk = np.mean([(am.matrix_world @ am.pose.bones['голень' + s_].head).z for s_ in ('', '.L')])
        zh = np.mean([(am.matrix_world @ am.pose.bones['стопа' + s_].head).z for s_ in ('', '.L')])
        Z = np.concatenate([geom(o, True)[0][:, 2] for o in vid]); z0, z1 = float(Z.min()), float(Z.max())
        def stroki(m_):
            ys = np.nonzero(m_.any(1))[0]; t, b = ys.min(), ys.max()
            return int(t + (z1 - zk) / (z1 - z0) * (b - t)), int(t + (z1 - zh) / (z1 - z0) * (b - t))
        for nm, s_, m_ in (('front', sf, Mf), ('profile', sp, Mp)):
            a0, a1 = stroki(s_); c0, c1 = stroki(m_); r0, r1 = min(a0, c0), max(a1, c1)
            otchet['iou_%s_koleno' % nm] = round(iou(s_[r0:r1 + 1], m_[r0:r1 + 1]), 3)
        print('ПОЛОСА НОГ: колено %.2f м, скакательный %.2f м; IoU анфас %.3f, профиль %.3f' % (zk, zh, otchet['iou_front_koleno'], otchet['iou_profile_koleno']))
    if opt.get('kadr'):   # сверка: красное — только тело, синее — только лист, чёрное — совпало
        for nm, s_, m_ in (('front', sf, Mf), ('profile', sp, Mp)):
            rgb = np.ones((512, 512, 3)); both = s_ & m_
            rgb[s_ & ~m_] = (0.9, 0.2, 0.2); rgb[m_ & ~s_] = (0.3, 0.5, 0.95); rgb[both] = (0.15, 0.15, 0.15)
            sohranit(opt['kadr'] + '_iou_' + nm + '.png', rgb)

# ДЫРЫ (`--dyry 1`, 28.10, геймдизайнер: «в модели дырки»). Как в игре: черты `@` и органы на шаблоне `<слот>@<вид>` двусторонние
# (BodyMesh.TwoSided; органы — с main `bfd723e`), тело шасси одностороннее (`--dvustoronnie_organy 0` — органы по-старому). Два прохода:
#  1. ЛУЧИ по ракурсам кадра сеткой 5 мм: первое попадание луча — в изнанку одностороннего объекта → видна изнанка (дыра насквозь
#     или вывернутая грань); попадания кучкуются в пятна по соседству ячеек сетки → «объект · ракурс · высота · размер, мм»;
#  2. ОТКРЫТЫЕ КРАЯ односторонних объектов: цепочки рёбер с одной гранью; край шасси, вершины которого в позе совпадают (< 1 мм) с
#     вершинами другого видимого объекта шасси, — шов, не дыра
# РАСТЯЖКА (`--rastyazhka 1`, 28.10, критик r2 п.8 «тонкая палочка под подбородком» ст.2): рёбра видимых кусков `@`, которые в позе
# длиннее, чем в покое ключа, больше чем в 3 раза, — столбики между костями (вершины одной грани на разных костях)
if opt.get('rastyazhka'):
    for o in [x for x in vid if '@' in x.name]:
        P0, F0 = geom(o, False); P1, _ = geom(o, True); gn_ = {g.index: g.name for g in o.vertex_groups}
        ed = {tuple(sorted((f[j], f[(j + 1) % len(f)]))) for f in F0 for j in range(len(f))}
        r_ = sorted(((np.linalg.norm(P1[u] - P1[v]) / max(np.linalg.norm(P0[u] - P0[v]), 1e-5), u, v) for u, v in ed), reverse=True)
        big = [t for t in r_ if t[0] > 3 and np.linalg.norm(P1[t[1]] - P1[t[2]]) > 0.02]
        glv = lambda k: gn_[max(o.data.vertices[k].groups, key=lambda g: g.weight).group] if o.data.vertices[k].groups else ''
        print('РАСТЯЖКА: %s — рёбер ×3 и длиннее 2 см: %d' % (o.name, len(big)))
        for t in big[:12]: print('РАСТЯЖКА: ×%.1f %d(%s) %d(%s) длина %.0f мм, середина %s' % (t[0], t[1], glv(t[1]), t[2], glv(t[2]),
                                 1000 * np.linalg.norm(P1[t[1]] - P1[t[2]]), ((P1[t[1]] + P1[t[2]]) / 2).round(2)))
    # тонкие длинные грани в позе (палочки): длина > 5 см, длина / высота > 8
    for o in [x for x in vid if '@' in x.name]:
        P1, F1 = geom(o, True); gn_ = {g.index: g.name for g in o.vertex_groups}; th = []
        glv = lambda k: gn_[max(o.data.vertices[k].groups, key=lambda g: g.weight).group] if o.data.vertices[k].groups else ''
        for f in F1:
            if len(f) != 3: continue
            A_, B_, C_ = P1[f[0]], P1[f[1]], P1[f[2]]; L = max(np.linalg.norm(A_ - B_), np.linalg.norm(B_ - C_), np.linalg.norm(C_ - A_))
            ar = np.linalg.norm(np.cross(B_ - A_, C_ - A_)) / 2; h = 2 * ar / max(L, 1e-9)
            if L > 0.05 and L / max(h, 1e-6) > 8: th.append((L, f, ((A_ + B_ + C_) / 3).round(2)))
        th.sort(key=lambda t: -t[0])
        print('ПАЛОЧКИ: %s — тонких граней %d' % (o.name, len(th)))
        for L, f, c in th[:15]: print('ПАЛОЧКИ: %.0f мм %s %s' % (1000 * L, c, [(k, glv(k)) for k in f]))
# НОГИ (`--nogi 1`, 28.10, критик r3 п.1: «порог толщины бедро → лапа у колена 8–10 %»): в позе — радиус до оси кости (по
# позным суставам) у вершин тела `Ноги` и кусков `@`, по главной кости; бедро — у колена (t 0.75–1), голень — полосами от колена
if opt.get('nogi'):
    am = [x for x in bpy.data.objects if x.type == 'ARMATURE'][0]
    for st_ in ('', '.L'):
        rez = {}
        for o in [x for x in vid if x.name == 'Ноги' or x.name.startswith('@')]:
            P1, _ = geom(o, True); gn_ = {g.index: g.name for g in o.vertex_groups}
            for kost in ('бедро' + st_, 'голень' + st_):
                pb = am.pose.bones[kost]; h = np.array(am.matrix_world @ pb.head); t = np.array(am.matrix_world @ pb.tail)
                u = (t - h) / np.linalg.norm(t - h); L = np.linalg.norm(t - h)
                idx = [v.index for v in o.data.vertices if v.groups and gn_[max(v.groups, key=lambda g: g.weight).group] == kost]
                if not idx: continue
                Q = P1[idx] - h; s_ = Q @ u / L; r_ = np.linalg.norm(Q - np.outer(Q @ u, u), axis=1)
                for a0, a1 in ((0.75, 1.0), (0.0, 0.15), (0.15, 0.3), (0.3, 0.45)) if kost.startswith('бедро') else ((0.0, 0.1), (0.1, 0.2), (0.2, 0.35), (0.35, 0.5)):
                    m_ = (s_ >= a0) & (s_ < a1)
                    if m_.any(): rez[(kost, a0, o.name)] = (float(np.percentile(r_[m_], 90)), int(m_.sum()))
        for k_ in sorted(rez): print('НОГИ: %s t %.2f %-6s r90 %.1f см (%d)' % (k_[0], k_[1], k_[2], 100 * rez[k_][0], rez[k_][1]))
if opt.get('dyry'):
    from mathutils.bvhtree import BVHTree
    DVU_ORG = opt.get('dvustoronnie_organy', '1') == '1'   # органы `<слот>@<вид>` двусторонние, как черты (игра с bfd723e)
    TRIS, OWN, OSN = [], [], []
    for o in vid:
        P_, F_ = geom(o, True)
        for f in F_:
            for j in range(1, len(f) - 1): TRIS.append((P_[f[0]], P_[f[j]], P_[f[j + 1]])); OWN.append(o.name)
        OSN.append((o.name, P_, F_))
    allv = [Vector(p) for t in TRIS for p in t]; bv = BVHTree.FromPolygons(allv, [[3 * i, 3 * i + 1, 3 * i + 2] for i in range(len(TRIS))])
    NRM = [np.cross(t[1] - t[0], t[2] - t[0]) for t in TRIS]
    lo_, hi_ = np.min(allv, axis=0), np.max(allv, axis=0); c_ = (lo_ + hi_) / 2; SH = 0.005; pyatna = []
    for nm, ang in (('front', 0), ('profile', 90), ('34', 35), ('back', 180), ('34back', 145)):
        r_ = math.radians(ang); d_ = np.array([math.sin(r_), -math.cos(r_), 0.0]); up = np.array([0, 0, 1.0]); rt = np.cross(d_, up)
        R_ = float(np.linalg.norm(hi_ - lo_)) / 2 + 0.05; cells = {}
        for iu in range(int(2 * R_ / SH)):
            for iz in range(int((hi_[2] - lo_[2] + 0.1) / SH)):
                org = Vector(c_ + d_ * 5 + rt * (iu * SH - R_)); org.z = lo_[2] - 0.05 + iz * SH
                hit = bv.ray_cast(org, Vector(-d_), 10.0)
                if hit[0] is None: continue
                nmo = OWN[hit[2]]
                if nmo.startswith('@') or (DVU_ORG and '@' in nmo): continue
                if float(NRM[hit[2]] @ (-d_)) > 0: cells[(iu, iz)] = (nmo, hit[0])
        seen_ = set()
        for k0 in cells:
            if k0 in seen_: continue
            st_, grp = [k0], []; seen_.add(k0)
            while st_:
                k_ = st_.pop(); grp.append(k_)
                for du in (-1, 0, 1):
                    for dz in (-1, 0, 1):
                        kk = (k_[0] + du, k_[1] + dz)
                        if kk in cells and kk not in seen_ and cells[kk][0] == cells[k0][0]: seen_.add(kk); st_.append(kk)
            if len(grp) < 2: continue
            pts = np.array([list(cells[g][1]) for g in grp])
            pyatna.append(dict(rakurs=nm, obekt=cells[k0][0], vysota_m=round(float(pts[:, 2].mean()), 2), x_m=round(float(pts[:, 0].mean()), 2),
                               razmer_mm=round(float(np.max(pts.max(0) - pts.min(0))) * 1000), ploshchad_sm2=round(len(grp) * SH * SH * 1e4, 1)))
    # открытые края
    VS = {nm_: P_ for nm_, P_, _ in OSN if '@' not in nm_}
    from mathutils import kdtree as _kd
    kraya = []
    for nm_, P_, F_ in OSN:
        if nm_.startswith('@') or (DVU_ORG and '@' in nm_): continue
        ec = {}
        for f in F_:
            for j in range(len(f)):
                e = tuple(sorted((f[j], f[(j + 1) % len(f)]))); ec[e] = ec.get(e, 0) + 1
        be = [e for e, c in ec.items() if c == 1]
        if not be: continue
        drug = np.concatenate([P2 for n2, P2 in VS.items() if n2 != nm_]) if len(VS) > 1 else np.zeros((0, 3))
        kd_ = _kd.KDTree(len(drug))
        for k_, p_ in enumerate(drug): kd_.insert(Vector(p_), k_)
        kd_.balance()
        adj = {}
        for a_, b_ in be: adj.setdefault(a_, []).append(b_); adj.setdefault(b_, []).append(a_)
        seen_ = set()
        for v0 in adj:
            if v0 in seen_: continue
            st_, grp = [v0], []; seen_.add(v0)
            while st_:
                v_ = st_.pop(); grp.append(v_)
                for w_ in adj[v_]:
                    if w_ not in seen_: seen_.add(w_); st_.append(w_)
            pts = P_[grp]
            otkr = [g for g in grp if len(drug) == 0 or kd_.find(Vector(P_[g]))[2] > 0.001]
            if len(otkr) < 3: continue
            po = P_[otkr]
            kraya.append(dict(obekt=nm_, vershin=len(otkr), iz=len(grp), vysota_m=round(float(po[:, 2].mean()), 2), x_m=round(float(po[:, 0].mean()), 2),
                              razmer_mm=round(float(np.max(po.max(0) - po.min(0))) * 1000)))
    # 3. ОКНА (дизайн-линия 28.10: «окно в гриве над плечом — видно фон», «щель между тазом и корнем хвоста»): в каждом ракурсе
    # силуэт всех видимых объектов растрируется (2 мм), фон, не связанный с краем кадра, — окно сквозь фигуру
    from collections import deque
    okna, vyemki = [], []; HH = float(hi_[2] - lo_[2]); PX = 0.002
    for nm, ang in (('front', 0), ('profile', 90), ('34', 35), ('back', 180), ('34back', 145)):
        r_ = math.radians(ang); d_ = np.array([math.sin(r_), -math.cos(r_), 0.0]); rt = np.cross(d_, np.array([0, 0, 1.0]))
        T3 = np.array([[np.array(p) for p in t] for t in TRIS]); u = T3 @ rt; v = T3[..., 2]
        u0, v0 = u.min() - 0.02, v.min() - 0.02; W_ = int((u.max() - u0 + 0.02) / PX) + 1; H_ = int((v.max() - v0 + 0.02) / PX) + 1
        im = rastr(np.stack([(u - u0) / PX, (v.max() + 0.02 - v) / PX], -1), max(W_, H_))[:H_, :W_]
        fon = ~im; vis = np.zeros_like(fon); dq = deque()
        for y in range(H_):
            for x in (0, W_ - 1):
                if fon[y, x] and not vis[y, x]: vis[y, x] = True; dq.append((y, x))
        for x in range(W_):
            for y in (0, H_ - 1):
                if fon[y, x] and not vis[y, x]: vis[y, x] = True; dq.append((y, x))
        while dq:
            y, x = dq.popleft()
            for yy, xx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)):
                if 0 <= yy < H_ and 0 <= xx < W_ and fon[yy, xx] and not vis[yy, xx]: vis[yy, xx] = True; dq.append((yy, xx))
        ok_ = fon & ~vis; seen2 = np.zeros_like(ok_)
        # 4. ВЫЕМКИ (дизайн-линия, повтор осмотра 28.10c: «клин между концом гривы и плечом, открытый к контуру — красным окном не
        # отмечен»): узкий фон, открытый наружу, — то, что закрывает морфологическое замыкание силуэта диском радиуса `VR`
        # (щель уже 2·VR = 6 см), минус окна. Ловит и просветы позы (рука ↔ торс) — их отличают по месту, как окна
        VR = int(0.03 / PX); yy_, xx_ = np.mgrid[-VR:VR + 1, -VR:VR + 1]; disk = [(a_, b_) for a_, b_ in zip(yy_.ravel(), xx_.ravel()) if a_ * a_ + b_ * b_ <= VR * VR]
        def sdvig(m, a_, b_, fill):
            r = np.full_like(m, fill); H2, W2 = m.shape
            r[max(a_, 0):H2 + min(a_, 0), max(b_, 0):W2 + min(b_, 0)] = m[max(-a_, 0):H2 + min(-a_, 0), max(-b_, 0):W2 + min(-b_, 0)]; return r
        dil = np.zeros_like(im)
        for a_, b_ in disk: dil |= sdvig(im, a_, b_, False)
        clo = np.ones_like(im)
        for a_, b_ in disk: clo &= sdvig(dil, a_, b_, True)
        vy_ = clo & fon & vis
        if opt.get('kadr'):   # карта окон: фигура серая, окна красные, выемки оранжевые
            rgb = np.ones((H_, W_, 3)); rgb[im] = (0.55, 0.55, 0.55); rgb[ok_] = (0.9, 0.1, 0.1); rgb[vy_] = (1.0, 0.6, 0.1)
            sohranit(opt['kadr'] + '_okna_' + nm + '.png', rgb)
        seen3 = np.zeros_like(vy_)
        for y0, x0 in zip(*np.nonzero(vy_)):
            if seen3[y0, x0]: continue
            dq = deque([(y0, x0)]); seen3[y0, x0] = True; cl = []
            while dq:
                y, x = dq.popleft(); cl.append((y, x))
                for yy, xx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)):
                    if 0 <= yy < H_ and 0 <= xx < W_ and vy_[yy, xx] and not seen3[yy, xx]: seen3[yy, xx] = True; dq.append((yy, xx))
            if len(cl) * PX * PX * 1e4 < 2: continue
            cl = np.array(cl); hgt = (cl[:, 0].max() - cl[:, 0].min() + 1) * PX; wd = (cl[:, 1].max() - cl[:, 1].min() + 1) * PX
            if v.max() + 0.02 - cl[:, 0].max() * PX < 0.05: continue   # у пола — между стопами, не выемка формы
            vyemki.append(dict(rakurs=nm, vysota_m=round(float(v.max() + 0.02 - cl[:, 0].mean() * PX), 2), poperek_m=round(float(u0 + cl[:, 1].mean() * PX), 2),
                               vys_mm=round(hgt * 1000), shir_mm=round(wd * 1000), vys_H=round(hgt / HH, 3), shir_H=round(wd / HH, 3), ploshchad_sm2=round(len(cl) * PX * PX * 1e4, 1)))
        for y0, x0 in zip(*np.nonzero(ok_)):
            if seen2[y0, x0]: continue
            dq = deque([(y0, x0)]); seen2[y0, x0] = True; cl = []
            while dq:
                y, x = dq.popleft(); cl.append((y, x))
                for yy, xx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)):
                    if 0 <= yy < H_ and 0 <= xx < W_ and ok_[yy, xx] and not seen2[yy, xx]: seen2[yy, xx] = True; dq.append((yy, xx))
            if len(cl) < 4: continue
            cl = np.array(cl); hgt = (cl[:, 0].max() - cl[:, 0].min() + 1) * PX; wd = (cl[:, 1].max() - cl[:, 1].min() + 1) * PX
            okna.append(dict(rakurs=nm, vysota_m=round(float(v.max() + 0.02 - cl[:, 0].mean() * PX), 2), poperek_m=round(float(u0 + cl[:, 1].mean() * PX), 2),
                             vys_mm=round(hgt * 1000), shir_mm=round(wd * 1000), vys_H=round(hgt / HH, 3), shir_H=round(wd / HH, 3), ploshchad_sm2=round(len(cl) * PX * PX * 1e4, 1)))
    otchet['dyry_okna'] = sorted(okna, key=lambda x: -x['ploshchad_sm2'])
    print('ДЫРЫ: окон сквозь фигуру %d' % len(okna))
    for x in otchet['dyry_okna'][:10]: print('ДЫРА окно: %(rakurs)s · высота %(vysota_m).2f · %(vys_mm)d×%(shir_mm)d мм (%(vys_H).2fH×%(shir_H).2fH) · %(ploshchad_sm2).1f см²' % x)
    otchet['dyry_vyemki'] = sorted(vyemki, key=lambda x: -x['ploshchad_sm2'])
    print('ДЫРЫ: выемок контура уже 6 см (≥ 2 см²) %d' % len(vyemki))
    for x in otchet['dyry_vyemki'][:10]: print('ДЫРА выемка: %(rakurs)s · высота %(vysota_m).2f поперёк %(poperek_m).2f · %(vys_mm)d×%(shir_mm)d мм · %(ploshchad_sm2).1f см²' % x)
    otchet['dyry_izanka'] = sorted(pyatna, key=lambda x: -x['ploshchad_sm2'])
    otchet['dyry_kraya'] = sorted(kraya, key=lambda x: -x['razmer_mm'])
    print('ДЫРЫ: изнанка видна — пятен %d (крупных ≥ 2 см² — %d); открытых краёв не на шве — %d' % (
        len(pyatna), sum(1 for x in pyatna if x['ploshchad_sm2'] >= 2), len(kraya)))
    for x in otchet['dyry_izanka'][:12]: print('ДЫРА изнанка: %(obekt)s · %(rakurs)s · высота %(vysota_m).2f x %(x_m).2f · %(razmer_mm)d мм · %(ploshchad_sm2).1f см²' % x)
    for x in otchet['dyry_kraya'][:12]: print('ДЫРА край: %(obekt)s · высота %(vysota_m).2f x %(x_m).2f · %(razmer_mm)d мм (%(vershin)d вершин)' % x)

# кадры: Workbench, отсечение задних граней (как в игре)
if opt.get('kadr'):
    # ЧЕРТЫ `@` И ОРГАНЫ `<слот>@<вид>` В ИГРЕ ДВУСТОРОННИЕ (материал черт 22.10, органы — main bfd723e): отсечение задних граней на
    # них — артефакт стенда («дыра в хвосте сзади», критик 27.10). Кускам добавляется копия с обращёнными гранями, как в игре
    dvu = opt.get('dvustoronnie_organy', '1') == '1'
    for o in [x for x in vid if x.name.startswith('@') or (dvu and '@' in x.name)]:
        dup = o.copy(); dup.data = o.data.copy(); bpy.context.scene.collection.objects.link(dup)
        bm_ = bmesh.new(); bm_.from_mesh(dup.data); bmesh.ops.reverse_faces(bm_, faces=bm_.faces[:]); bm_.to_mesh(dup.data); bm_.free()
        dup.hide_render = False
    sc = bpy.context.scene; sc.render.engine = 'BLENDER_WORKBENCH'
    sh = sc.display.shading; sh.light = 'STUDIO'; sh.color_type = 'SINGLE'; sh.single_color = (0.72, 0.68, 0.62)
    sh.show_backface_culling = True; sh.show_object_outline = False   # контур объекта рисовал швы шасси зубцами («пила» 27.10) — игра контуров не рисует
    sc.render.resolution_x, sc.render.resolution_y = 900, 1200; sc.render.film_transparent = False
    w_ = bpy.data.worlds.new('w'); w_.color = (1, 1, 1); sc.world = w_
    for me in {o.data for o in vid}:
        for p in me.polygons: p.use_smooth = False
    P = np.concatenate([geom(o, True)[0] for o in vid]); c = (P.min(0) + P.max(0)) / 2; H = (P.max(0) - P.min(0)).max() * 1.1
    cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); sc.collection.objects.link(cam); sc.camera = cam
    cam.data.type = 'ORTHO'; cam.data.ortho_scale = H
    for nm, ang in (('front', 0), ('profile', 90), ('34', 35), ('back', 180), ('34back', 145)):
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
