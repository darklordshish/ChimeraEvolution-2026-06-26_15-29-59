"""ИДЕНТИЧНОСТЬ КЛЮЧОМ (опыт 14.10, решение геймдизайнера «давай попробуем»): форма цели (оборотень по листу, тракт
Hunyuan) переносится на ТОПОЛОГИЮ меша шасси (человек К2: те же объекты, число и порядок вершин) и ложится в каждый объект
формой-ключом с именем вида-донора (`Волк`). Игра ставит вес ключа по идентичности; кости едут за суставами цели.

Перенос по ориентирам (как Wrap / Landmark Warp):
  1. ОРИЕНТИРЫ — суставы скелета шасси (`*-skelet.json`) и точки кожи (макушка, нос, подбородок, кончики пальцев,
     носки, пятки). У шасси считаются здесь, у цели приходят файлом `ориентиры.json` (имя → точка в метрах тела, оси
     Blender) — снятые с цели замером (`--istochnik` печатает имена и точки шасси как образец формата);
  2. ГРУБО — тонкая пластина (TPS, ядро r) по парам ориентиров тянет все вершины шасси к цели;
  3. ТОЧНО — `--iter` раз: каждая вершина к ближайшей точке поверхности цели (не дальше `--maxd`), поле сдвигов
     сглаживается по связности (`--smooth` проходов) — сетка не рвётся и не слипается. Швы между объектами (кольца шеи,
     ушей) — общие вершины: сдвиг считается один на точку, стыки не расходятся;
  4. объекты-исключения (`--bez`, по умолчанию уши `Чутьё`: у цели их форма — кусок-черта) едут только пластиной;
  5. СУСТАВЫ ЦЕЛИ — те же суставы шасси, перенесённые пластиной, — файлом рядом (формат `*-skelet.json`).

  blender -b --factory-startup -P kluch_vida.py -- шасси.blend цель.obj ориентиры.json выход.fbx скелет_шасси.json суставы_цели.json
      [--imya Волк] [--iter 4] [--smooth 6] [--maxd 0.08] [--maxd_obj голова:0.025,шея:0.025 [--zatylok y]] [--bez Чутьё] [--lam 1e-4] [--gladit N [--voxel 0.012]] [--rasput 60] [--polosa Ноги:0.40:0.80:20] [--skuly k:yc:zc:r] [--bez_zony Ноги:0.14] [--plechi k] [--grud kd] [--taz kt] [--golova_zhestko 1] [--mesta паспорт.json,…] [--kupol f:zb:yb]
  blender -b --factory-startup -P kluch_vida.py -- шасси.blend - - - скелет_шасси.json - --istochnik ориентиры_шасси.json
"""
import sys, json, math
import bpy, bmesh
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

a = sys.argv[sys.argv.index('--') + 1:]
src_blend, tgt_obj, lm_path, out_fbx, skel_path, joints_out = a[:6]
opt = dict(imya='Волк', iter='4', smooth='6', maxd='0.08', bez='Чутьё', lam='1e-4')
i = 6
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
NAME = opt['imya']; BEZ = set(opt['bez'].split(',')) if opt['bez'] else set()

bpy.ops.wm.open_mainfile(filepath=src_blend)
OBJ = [o for o in bpy.data.objects if o.type == 'MESH']
sk = json.load(open(skel_path, encoding='utf-8'))
BN = {b['name']: b for b in sk['bones']}


def world(o):
    return [o.matrix_world @ v.co for v in o.data.vertices]


# ---------- ориентиры шасси ----------
def landmarks():
    L = {}
    J = (('таз', 'head'), ('хребет', 'head'), ('шея', 'head'), ('голова', 'head'), ('голова', 'tail'))
    for b, e in J:
        if b in BN: L['%s:%s' % (b, e)] = Vector(BN[b][e])
    for s in ('', '.L'):
        for b, e in (('плечо', 'head'), ('предплечье', 'head'), ('предплечье', 'tail'), ('бедро', 'head'), ('голень', 'head'), ('голень', 'tail')):
            if b + s in BN: L['%s%s:%s' % (b, s, e)] = Vector(BN[b + s][e])
    V = [p for o in OBJ for p in world(o)]
    hd = [p for p in V if p.z > 1.55 and abs(p.x) < 0.10]
    L['макушка'] = max(hd, key=lambda p: p.z)
    L['нос'] = min((p for p in hd if 1.62 < p.z < 1.75), key=lambda p: p.y)
    L['подбородок'] = min((p for p in hd if 1.56 < p.z < 1.63 and abs(p.x) < 0.02), key=lambda p: p.y)
    L['затылок'] = max((p for p in hd if 1.62 < p.z < 1.75 and abs(p.x) < 0.03), key=lambda p: p.y)
    for sg, s in ((-1, ''), (1, '.L')):          # без суффикса — сторона −X Blender
        side = [p for p in V if p.x * sg > 0.05]
        L['пальцы' + s] = min((p for p in side if p.z > 0.4 and abs(p.x) > 0.26), key=lambda p: p.z)     # кисть — дальше 0.26 от оси: ближе стоит бедро
        foot = [p for p in side if p.z < 0.12]
        L['носок' + s] = min(foot, key=lambda p: p.y)
        L['пятка' + s] = max(foot, key=lambda p: p.y)
        # ЛАПА (14.10, «стопа плохо переделана»): ширина по головкам плюсны и свод — хищник ступает на пальцы и головки
        # плюсны, пятка поднята, плюсна удлинена (источник — «Пластическая анатомия», задняя конечность)
        yb = L['носок' + s].y + 0.25 * (L['пятка' + s].y - L['носок' + s].y)
        ball = [p for p in foot if abs(p.y - yb) < 0.02 and p.z < 0.04]
        L['плюсна_вн' + s] = min(ball, key=lambda p: abs(p.x)); L['плюсна_нар' + s] = max(ball, key=lambda p: abs(p.x))
        ym = L['носок' + s].y + 0.55 * (L['пятка' + s].y - L['носок' + s].y)
        arch = [p for p in foot if abs(p.y - ym) < 0.02 and p.z < 0.03]
        L['свод' + s] = Vector((sum(p.x for p in arch) / len(arch), ym, min(p.z for p in arch)))
    return L


LS = landmarks()
if 'istochnik' in opt:
    json.dump({k: [round(c, 4) for c in v] for k, v in LS.items()}, open(opt['istochnik'], 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    print('КЛЮЧ: ориентиров шасси %d → %s' % (len(LS), opt['istochnik']))
    sys.exit(0)

LT = {k: Vector(v) for k, v in json.load(open(lm_path, encoding='utf-8')).items()}
names = [k for k in LS if k in LT]
miss = [k for k in LS if k not in LT]
P = np.array([LS[k] for k in names]); Q = np.array([LT[k] for k in names])

# ---------- 2. тонкая пластина ----------
n = len(P)
K = np.linalg.norm(P[:, None] - P[None], axis=2) + float(opt['lam']) * np.eye(n)
Pm = np.hstack([np.ones((n, 1)), P])
A = np.zeros((n + 4, n + 4)); A[:n, :n] = K; A[:n, n:] = Pm; A[n:, :n] = Pm.T
B = np.zeros((n + 4, 3)); B[:n] = Q
W = np.linalg.solve(A, B)


def tps(X):
    X = np.asarray(X)
    return np.linalg.norm(X[:, None] - P[None], axis=2) @ W[:n] + np.hstack([np.ones((len(X), 1)), X]) @ W[n:]


# ---------- общие вершины всех объектов (швы) ----------
key_of = lambda p: (round(p.x, 5), round(p.y, 5), round(p.z, 5))
uid, U0, owner = {}, [], []
vmap = {}
for o in OBJ:
    for vi, p in enumerate(world(o)):
        k = key_of(p)
        if k not in uid:
            uid[k] = len(U0); U0.append(p); owner.append(set())
        vmap[(o.name, vi)] = uid[k]; owner[uid[k]].add(o.name)
U0 = np.array(U0)
nb = [set() for _ in U0]
for o in OBJ:
    for e in o.data.edges:
        x, y = vmap[(o.name, e.vertices[0])], vmap[(o.name, e.vertices[1])]
        nb[x].add(y); nb[y].add(x)
free = np.array([not (own & BEZ) for own in owner])       # вершины ушей (и их кольца) — только пластиной
# `--bez_zony объект:zmax` — вершины объекта ниже zmax (в шасси) едут только пластиной: у лапы форму задают ориентиры,
# а не цель (у цели Hunyuan подошва лежит на земле до пятки — стопоходящая, вопреки анатомии хищника)
for spec in [s_ for s_ in opt.get('bez_zony', '').split(',') if s_]:
    on, zm = spec.split(':'); zm = float(zm)
    for k_ in range(len(U0)):
        if on in owner[k_] and U0[k_][2] < zm:
            free[k_] = False
# ПРЕДЕЛ ПРИТЯЖКИ ПО ОБЪЕКТАМ (`--maxd_obj голова:0.025,шея:0.025`): у цели вокруг головы и шеи — грива (кусок-черта, из
# кожи её не тянут); с общим пределом затылок шасси прилипал к гриве и вытягивался гребнем. Вершина на стыке объектов
# берёт меньший предел
MAXD_OBJ = {kv.split(':')[0]: float(kv.split(':')[1]) for kv in opt.get('maxd_obj', '').split(',') if ':' in kv}

X = tps(U0)
# ---------- 3. к поверхности цели ----------
bpy.ops.wm.obj_import(filepath=tgt_obj, forward_axis='NEGATIVE_Z', up_axis='Y')
tg = [o for o in bpy.context.selected_objects if o.type == 'MESH'][0]
# ЦЕЛЬ СГЛАЖЕННАЯ (`--gladit N`, 14.10): притяжка к сырой цели ловила её мелочь — шипы гривы на груди, складки колена и
# стопы, пальцы — и выворачивала треугольники шасси (изломы до 179°, в игре — тёмные полосы на коленях и под ключицами).
# Ключу лоуполи-тела нужна форма масс, а не рельеф: цель пересобирается вокселями и сглаживается N проходами
if int(opt.get('gladit', '0')):
    bpy.context.view_layer.objects.active = tg
    m_ = tg.modifiers.new('vox', 'REMESH'); m_.mode = 'VOXEL'; m_.voxel_size = float(opt.get('voxel', '0.012'))
    bpy.ops.object.modifier_apply(modifier=m_.name)
    m_ = tg.modifiers.new('gl', 'SMOOTH'); m_.factor = 1.0; m_.iterations = int(opt['gladit'])
    bpy.ops.object.modifier_apply(modifier=m_.name)
bm = bmesh.new(); bm.from_mesh(tg.data); bm.transform(tg.matrix_world)
bvh = BVHTree.FromBMesh(bm)
maxd = float(opt['maxd'])
# предел объекта действует только за плоскостью `--zatylok` (y шасси, Blender: морда в −Y) — лицо тянется к цели полностью
YZ = float(opt.get('zatylok', '-9'))
vmax = np.array([min([MAXD_OBJ.get(nm, maxd) if U0[k_][1] > YZ else maxd for nm in own]) for k_, own in enumerate(owner)])
for it in range(int(opt['iter'])):
    D = np.zeros_like(X)
    for k_ in range(len(X)):
        if not free[k_]:
            continue
        hit = bvh.find_nearest(Vector(X[k_]))
        if hit[0] is not None and hit[3] < vmax[k_]:
            D[k_] = np.array(hit[0]) - X[k_]
    for _ in range(int(opt['smooth'])):
        D = np.array([0.5 * D[k_] + 0.5 * (np.mean([D[j] for j in nb[k_]], axis=0) if nb[k_] else D[k_]) if free[k_] else D[k_] for k_ in range(len(D))])
    X = X + D
    print('КЛЮЧ: проход %d, средний сдвиг %.4f м, наибольший %.4f' % (it + 1, np.linalg.norm(D, axis=1).mean(), np.linalg.norm(D, axis=1).max()))
# ПОЛОСА СГЛАЖИВАНИЯ (`--polosa объект:z0:z1:N`, через «;»): у цели над коленом мышца нависает над тонкой голенью, и ряд
# вершин шасси ложится туда почти горизонтальным карнизом — в игре тёмная полоса поперёк ноги. В полосе форма ключа
# сглаживается N проходами: карниз становится скатом
X0 = tps(U0)
for spec in [s_ for s_ in opt.get('polosa', '').split(';') if s_]:
    on, z0_, z1_, nn = spec.split(':'); z0_, z1_, nn = float(z0_), float(z1_), int(nn)
    zone = [k_ for k_ in range(len(X)) if on in owner[k_] and z0_ <= U0[k_][2] <= z1_ and free[k_]]
    # сглаживается САМА ФОРМА (положения), а не сдвиг: ступень сидит в сетке шасси (едва заметная на базе кромка над
    # коленом), сдвиг её только раздувает — сглаживание сдвига полосу не трогало
    for _ in range(nn):
        Xn = X.copy()
        for k_ in zone:
            Xn[k_] = 0.6 * X[k_] + 0.4 * np.mean([X[j] for j in nb[k_]], axis=0)
        X = Xn
    print('КЛЮЧ: полоса %s z %.2f–%.2f: вершин %d, проходов %d' % (on, z0_, z1_, len(zone), nn))
# РАСПУТЫВАНИЕ (14.10, проход 3): где цель резко сужается (бедро нависает над голенью, пальцы), притяжка перехлёстывает
# соседние ряды вершин — грань в ключе разворачивается против своей нормали в базе, в игре это тёмный залом поперёк ноги
# уже на весе 0.35. Такие грани находятся по знаку (нормаль ключа · нормаль базы) и их вершины расслабляются к среднему
# соседей, пока перевёрнутых не останется (`--rasput N` проходов, 0 — выкл.)
FACES = [tuple(vmap[(o.name, vi)] for vi in p.vertices) for o in OBJ for p in o.data.polygons]
def flipped(Xk):
    out = set()
    for f_ in FACES:
        a0, a1, a2 = U0[f_[0]], U0[f_[1]], U0[f_[2]]; b0, b1, b2 = Xk[f_[0]], Xk[f_[1]], Xk[f_[2]]
        nb_ = np.cross(a1 - a0, a2 - a0); nk = np.cross(b1 - b0, b2 - b0)
        if np.dot(nb_, nk) < 0.2 * np.linalg.norm(nb_) * np.linalg.norm(nk):
            out.update(f_)
    return out
n_flip0 = len(flipped(X))
for it in range(int(opt.get('rasput', '60'))):
    bad = flipped(X)
    if not bad:
        break
    ring = set(bad) | {j for k_ in bad for j in nb[k_]}          # с соседями: одна вершина не распутывает складку
    Xn = X.copy()
    for k_ in ring:
        if nb[k_] and free[k_]:
            Xn[k_] = 0.5 * X[k_] + 0.5 * np.mean([X[j] for j in nb[k_]], axis=0)
    X = Xn
print('КЛЮЧ: распутывание — вершин у развёрнутых граней %d → %d' % (n_flip0, len(flipped(X))))
# ПОПРАВКИ ПО АНАТОМИИ (14.10, вердикт геймдизайнера «направление верное, числа подправить»; источник — «Пластическая
# анатомия», млекопитающие в сравнении с человеком, выжимка в `Референсы/КАТАЛОГ.md`). Работают по форме ключа, в метрах
# тела, плавной колоколообразной весовой функцией — без ступеней:
#   --skuly k:yc:zc:r — ширина головы хищника — скуловые дуги и жевательные/височные мышцы: X раздвигается в k раз у
#     центра (yc, zc) и затухает к радиусу r (по y и z; объекты головы, шеи и ушей — кольца швов едут вместе);
#   --kupol f:zb:yb — мозговой купол у зверя ниже, лицевой отдел преобладает: всё, что выше zb и позади yb, сжимается по
#     высоте к zb в f раз (переход позади yb на  м, 0.08 по умолчанию)
sm_ = lambda u: 0.0 if u <= 0 else (1.0 if u >= 1 else u * u * (3 - 2 * u))
HEADS = ('голова', 'шея', 'Чутьё')
if 'skuly' in opt:
    k_s, yc, zc, rr = map(float, opt['skuly'].split(':'))
    for k_ in range(len(X)):
        if owner[k_] & set(HEADS):
            d2 = ((X[k_][1] - yc) ** 2 + (X[k_][2] - zc) ** 2) / rr ** 2
            X[k_][0] *= 1 + (k_s - 1) * max(0.0, 1 - d2) ** 2
if 'kupol' in opt:
    f_k, zb, yb = map(float, opt['kupol'].split(':'))
    for k_ in range(len(X)):
        if owner[k_] & set(HEADS) and X[k_][2] > zb:
            w_ = sm_((X[k_][1] - yb) / float(opt.get('kupol_shag', '0.08')))
            X[k_][2] = zb + (X[k_][2] - zb) * (1 - (1 - f_k) * w_)
# КОРПУС (15.10, ряд «лист / середина / анатомия» для геймдизайнера; источник: у хищника ключиц нет, лопатки на боках
# клетки — плечи у́же, клетка у́же и глубже, таз у́же). Работают по форме ключа, плавно по высоте:
#   --plechi k — ширина плеч и груди (полоса z 1.15–1.60), руки сдвигаются к оси целиком на тот же сдвиг, что плечо;
#   --grud kd  — глубина груди (та же полоса, у Сердца и хребта) от средней линии корпуса;
#   --taz kt   — ширина таза и верха бёдер (полоса z 0.80–1.08, к 0.65 затухает)
def band(z, z0, z1, w0, w1):
    return sm_((z - w0) / (z0 - w0)) * sm_((w1 - z) / (w1 - z1)) if w0 < z0 else 0.0
TORSO = ('хребет', 'Сердце', 'шея')   # шея — с затуханием к кольцу головы (общие вершины с головой не двигаются)
if any(k in opt for k in ('plechi', 'grud', 'taz')):
    kp, kd, kt = float(opt.get('plechi', 1)), float(opt.get('grud', 1)), float(opt.get('taz', 1))
    # сдвиг руки = сдвиг корпуса НА ШВЕ «рука — корпус» (общие вершины), иначе шов плеча разъезжается складками трапеции
    seam_ = [k_ for k_ in range(len(X)) if 'Руки' in owner[k_] and owner[k_] & set(TORSO)]
    XS = float(np.mean([abs(X[k_][0]) for k_ in seam_])) if seam_ else 0.2
    WS = float(np.mean([band(U0[k_][2], 1.25, 1.60, 1.10, 1.75) for k_ in seam_])) if seam_ else 1.0
    XA = abs(float(tps([BN['плечо.L']['head']])[0][0]))      # плечевой сустав в ключе
    yc = np.mean([X[k_][1] for k_ in range(len(X)) if owner[k_] & set(TORSO) and abs(X[k_][2] - 1.35) < 0.05])
    for k_ in range(len(X)):
        z = U0[k_][2]
        if 'Руки' in owner[k_] and not owner[k_] & set(TORSO):
            # рука: внутренняя часть (у шва) сжимается как корпус, всё дальше плечевого сустава XA — сдвигом на ту же величину,
            # что сустав: силуэт плеч задаёт дельта, она на объекте рук
            X[k_][0] -= np.sign(X[k_][0]) * (1 - kp) * min(abs(X[k_][0]), XA)
        elif owner[k_] & set(TORSO + ('Ноги',)) and not owner[k_] & set(HEADS[:1]):
            wp = band(z, 1.25, 1.60, 1.10, 1.75)
            if 'шея' in owner[k_]:                        # к кольцу головы (z шасси 1.616) сжатие гаснет до нуля
                wp *= sm_((1.60 - z) / 0.16)
            X[k_][0] *= 1 + (kp - 1) * wp
            if owner[k_] & set(TORSO):
                X[k_][1] = yc + (X[k_][1] - yc) * (1 + (kd - 1) * wp)
            wt = band(z, 0.85, 1.02, 0.65, 1.15)
            X[k_][0] *= 1 + (kt - 1) * wt
# ГОЛОВА ЖЁСТКО (`--golova_zhestko 1`, 15.10, геймдизайнер: «голова обезьяны-инопланетянина, а не волка» — на лице К2 мало
# вершин под волчью морду): голову даёт кусок волчьего меша (черта «голова» по шву «основание черепа»), а ключ ведёт шею и
# ниже. Голова и уши шасси в ключе только ПЕРЕЕЗЖАЮТ целиком — сдвигом кольца шеи (среднее по его 12 вершинам); кольцо
# держит форму, поэтому шов куска на ключевом теле — то же кольцо со сдвигом. Шея к кольцу переходит плавно (8 см)
RING_SHIFT = None
if opt.get('golova_zhestko', '0') == '1':
    ring_ = [k_ for k_ in range(len(X)) if 'голова' in owner[k_] and 'шея' in owner[k_]]
    RING_SHIFT = np.mean([X[k_] - U0[k_] for k_ in ring_], axis=0)
    RING_SHIFT[0] = 0.0                                   # тело симметрично: боковой сдвиг — шум притяжки
    zr = float(np.mean([U0[k_][2] for k_ in ring_]))
    for k_ in range(len(X)):
        if owner[k_] & {'голова', 'Чутьё'}:
            X[k_] = U0[k_] + RING_SHIFT
        elif 'шея' in owner[k_]:
            w_ = sm_((U0[k_][2] - (zr - 0.08)) / 0.08)
            X[k_] = (1 - w_) * X[k_] + w_ * (U0[k_] + RING_SHIFT)
    print('КЛЮЧ: голова жёстко — сдвиг кольца шеи на весе 1: (%.4f %.4f %.4f) м, вершин кольца %d' % (*RING_SHIFT, len(ring_)))
res = [bvh.find_nearest(Vector(X[k_]))[3] for k_ in range(len(X)) if free[k_]]
print('КЛЮЧ: до поверхности цели — средн. %.4f м, 95%% %.4f, макс %.4f; ориентиров %d, нет у цели: %s' % (
    float(np.mean(res)), float(np.percentile(res, 95)), float(np.max(res)), n, ', '.join(miss) or '—'))
bpy.data.objects.remove(tg, do_unlink=True)

# ---------- места под ключом (`--mesta паспорт.json,паспорт.json`) ----------
# В паспорт шва (`seam`) пишется `pod_klyuchom: [{klyuch: ИМЯ, ...}]` — центр, ось, «верх», `ring_m` и эллипс на весе 1, оси Unity.
# Точка едет сдвигом кожи: среднее сдвигов 8 ближайших вершин шасси с весом 1/расстояние (место может лежать под кожей).
# Игра смешивает базу и вес 1 линейно (ось и «верх» — slerp)
if opt.get('mesta'):
    from mathutils import kdtree as _kd
    KD = _kd.KDTree(len(U0))
    for k_, p in enumerate(U0):
        KD.insert(Vector(p), k_)
    KD.balance()
    Dsp = X - U0
    def moved(pb):
        hits = KD.find_n(Vector(pb), 8)
        w_ = np.array([1 / max(d_, 1e-4) for _, _, d_ in hits]); w_ /= w_.sum()
        return np.array(pb) + sum(w__ * Dsp[i_] for w__, (_, i_, _) in zip(w_, hits))
    UB = lambda u: np.array([-u[0], -u[2], u[1]]); BU = lambda b: [round(float(-b[0]), 5), round(float(b[2]), 5), round(float(-b[1]), 5)]
    for pth in opt['mesta'].split(','):
        P_ = json.load(open(pth, encoding='utf-8')); s_ = P_['seam']
        c0 = UB(s_['center_m']); c1 = moved(c0)
        ax1 = moved(c0 + 0.05 * UB(s_['axis_out'])) - c1; ax1 /= np.linalg.norm(ax1)
        up1 = moved(c0 + 0.05 * UB(s_['up'])) - c1; up1 = up1 - ax1 * up1.dot(ax1); up1 /= np.linalg.norm(up1)
        if abs(c0[0]) < 1e-3:                             # шов на средней линии — симметричен: боковой снос — шум притяжки
            c1[0] = 0.0; ax1[0] = 0.0; ax1 /= np.linalg.norm(ax1); up1[0] = 0.0; up1 = up1 - ax1 * up1.dot(ax1); up1 /= np.linalg.norm(up1)
        ring1 = [moved(UB(q)) for q in s_['ring_m']] if 'ring_m' in s_ else None
        if ring1:
            side1 = np.cross(up1, ax1)
            el = [round(float(max(abs((q - c1).dot(side1)) for q in ring1)), 4), round(float(max(abs((q - c1).dot(up1)) for q in ring1)), 4)]
        else:
            el = s_['ellipse_m']
        rec = {'center_m': BU(c1), 'axis_out': [round(v, 5) for v in BU(ax1)], 'up': [round(v, 5) for v in BU(up1)], 'ellipse_m': el,
               'note': 'шов под ключом «%s» на весе 1 (kluch_vida.py --mesta); между базой и весом 1 — линейно, ось и «верх» — slerp' % NAME}
        if ring1:
            rec['ring_m'] = [BU(q) for q in ring1]
        rec = dict({'klyuch': NAME}, **rec)              # JsonUtility не читает словари — массив записей с полем `klyuch`
        lst = [r_ for r_ in (s_.get('pod_klyuchom') or []) if isinstance(r_, dict) and r_.get('klyuch') != NAME]
        s_['pod_klyuchom'] = lst + [rec]
        json.dump(P_, open(pth, 'w', encoding='utf-8'), ensure_ascii=False, indent=2)
        print('КЛЮЧ: место «%s» под ключом: центр %s → %s, эллипс %s → %s' % (s_['type'], s_['center_m'], rec['center_m'], s_['ellipse_m'], el))

# ---------- ключи ----------
for o in OBJ:
    if o.data.shape_keys is None:
        o.shape_key_add(name='Basis')
    kb = o.shape_key_add(name=NAME, from_mix=False)
    inv = o.matrix_world.inverted()
    for vi in range(len(o.data.vertices)):
        kb.data[vi].co = inv @ Vector(X[vmap[(o.name, vi)]])
    print('КЛЮЧ: «%s» у объекта `%s`: %d вершин' % (NAME, o.name, len(o.data.vertices)))

# ---------- 5. суставы цели ----------
out = dict(sk); bones = []
for b in sk['bones']:
    h, t = tps([b['head'], b['tail']])
    if RING_SHIFT is not None and b['name'].split('.')[0] in ('голова', 'челюсть', 'морда', 'надбровье'):
        h, t = np.array(b['head']) + RING_SHIFT, np.array(b['tail']) + RING_SHIFT     # голова жёсткая — её кости тоже
    bones.append(dict(b, head=[round(float(c), 5) for c in h], tail=[round(float(c), 5) for c in t]))
out['bones'] = bones
out['note'] = 'суставы цели «%s» на шасси: суставы шасси, перенесённые тонкой пластиной по ориентирам (kluch_vida.py); оси как у скелета шасси' % NAME
json.dump(out, open(joints_out, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)

ao = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
bpy.ops.object.select_all(action='SELECT')
bpy.context.view_layer.objects.active = ao
bpy.ops.export_scene.fbx(filepath=out_fbx, use_selection=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z',
                         axis_up='Y', object_types={'ARMATURE', 'MESH'}, add_leaf_bones=False, bake_anim=False,
                         mesh_smooth_type='FACE', use_armature_deform_only=False)
if 'blend' in opt:
    bpy.ops.wm.save_as_mainfile(filepath=opt['blend'])
print('КЛЮЧ: готово → %s, суставы → %s' % (out_fbx, joints_out))
