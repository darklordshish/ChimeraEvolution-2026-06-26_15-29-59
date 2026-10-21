"""ПОДГОНКА В ПОЗЕ ЦЕЛИ (20.10, консилиум формы, направление Б: «сперва поза скелетом, потом форма в позе покоя»).
Колонка ст.2 листа оборотня сгорблена; выпрямлять цель тонкой пластиной по суставам нельзя — она гнётся как резина (шея
дугой, торс сплющен), и тело, подогнанное к такой цели, выходит рваным. Поэтому наоборот: скелет шасси ставится в позу
цели ВРАЩЕНИЯМИ костей (длины свои), сетка подгоняется к цели в этой позе по частям тела, и результат возвращается в позу
покоя обратным скиннингом (rest = (Σ wᵢ Mᵢ)⁻¹ · posed). Пропорции цели остаются в форме (длины костей — свои), поза — в
скелете.

  1. ПОЗА: кость за костью от корня — поворот, совмещающий направление кости шасси с направлением той же кости цели
     (по её ориентирам в позе листа); корень ещё и переносится в таз цели;
  2. ГРУБО: остаток (суставы шасси в позе → суставы цели) — тонкая пластина;
  3. ТОЧНО: `--iter` раз — вершина к ближайшей точке СВОЕЙ части цели (часть шасси — по наибольшему весу кости, грань
     цели — по ближайшему отрезку кости цели; грани дальше `--dal` от всех костей — хвост, пряди — не в счёт), сдвиги
     сглаживаются по связности (общие вершины швов — одна точка);
  4. обратно в покой: вершина умножается на обратную матрицу своего скина; результат — форма-ключ `--imya`.

  blender -b --factory-startup -P podgonka_v_poze.py -- шасси.blend цель.obj ориентиры_цели.json скелет_шасси.json выход.blend
      [--imya Волк] [--zhestkost 30,15,8,4] [--normal 0.5] [--maxd 0.25] [--dal 0.12] [--bez голова,Чутьё] [--poza_obj файл.obj] [--poza файл.json] [--sustavy файл.json] [--cel_pokoj файл.obj] [--vstrechnye 1 [--vstr_maxd 0.05|0.25,предплечье:0.05]]
Ориентиры цели — имена как у `kluch_vida.py` (сустав:head/tail, носок, плюсна_вн/нар…), оси Blender, метры тела.
"""
import sys, json
import bpy, bmesh
import numpy as np
from mathutils import Vector, Matrix, Quaternion
from mathutils.bvhtree import BVHTree

a = sys.argv[sys.argv.index('--') + 1:]
src, tgt, lm_p, sk_p, out = a[:5]
opt = dict(imya='Волк', iter='10', smooth='8', maxd='0.25', dal='0.12', bez='голова,Чутьё')
i = 5
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
NAME = opt['imya']; BEZ = set(opt['bez'].split(',')) if opt['bez'] else set()
LT = {k: Vector(v) for k, v in json.load(open(lm_p, encoding='utf-8')).items()}
SK = {b['name']: b for b in json.load(open(sk_p, encoding='utf-8'))['bones']}

bpy.ops.wm.open_mainfile(filepath=src)
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
OBJ = [o for o in bpy.data.objects if o.type == 'MESH' and not o.name.startswith('@')]
for o in OBJ:
    if o.data.shape_keys:
        for kb in o.data.shape_keys.key_blocks: kb.value = 0.0


def mid(*ks):
    return sum((LT[k] for k in ks), Vector()) / len(ks)


# ---------- кость → направление цели (голова, хвост), по стороне ----------
def tgt_dir(bn):
    s = '.L' if bn.endswith('.L') else ''
    base = bn[:-2] if s else bn
    T = {
        'хребет': ('таз:head', 'шея:head'), 'шея': ('шея:head', 'голова:head'), 'голова': ('голова:head', 'голова:tail'),
        'плечо': ('плечо%s:head', 'предплечье%s:head'), 'предплечье': ('предплечье%s:head', 'предплечье%s:tail'),
        'бедро': ('бедро%s:head', 'голень%s:head'), 'голень': ('голень%s:head', 'голень%s:tail'),
    }
    if base == 'стопа':
        return LT['голень%s:tail' % s], mid('плюсна_вн%s' % s, 'плюсна_нар%s' % s)
    if base not in T: return None
    h, t = T[base]
    h, t = (h % s if '%s' in h else h), (t % s if '%s' in t else t)
    if h not in LT or t not in LT: return None
    return LT[h], LT[t]


# ---------- 1. поза ----------
bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode='POSE')
for pb in arm.pose.bones:
    pb.rotation_mode = 'QUATERNION'; pb.rotation_quaternion = Quaternion(); pb.location = (0, 0, 0)
order = []
def walk(b):
    order.append(b.name)
    for c in b.children: walk(c)
for b in arm.data.bones:
    if b.parent is None: walk(b)
AW = arm.matrix_world
root = arm.pose.bones[order[0]]
# корень в таз цели
bpy.context.view_layer.update()
d_ = LT['таз:head'] - AW @ root.head
root.matrix = Matrix.Translation(AW.inverted().to_3x3() @ d_) @ root.matrix
bpy.context.view_layer.update()
n_rot = 0
for bn in order:
    td = tgt_dir(bn)
    if td is None: continue
    pb = arm.pose.bones[bn]
    bpy.context.view_layer.update()
    cur = (AW @ pb.tail) - (AW @ pb.head)
    want = td[1] - td[0]
    if cur.length < 1e-6 or want.length < 1e-6: continue
    q = cur.normalized().rotation_difference(want.normalized())
    hw = AW @ pb.head
    Mw = AW @ pb.matrix
    Mw2 = Matrix.Translation(hw) @ q.to_matrix().to_4x4() @ Matrix.Translation(-hw) @ Mw
    pb.matrix = AW.inverted() @ Mw2
    n_rot += 1
bpy.context.view_layer.update()
bpy.ops.object.mode_set(mode='OBJECT')
print('ПОЗА: повёрнуто костей %d' % n_rot)

# ---------- общие вершины, части, позированные точки ----------
dg = bpy.context.evaluated_depsgraph_get()
key_of = lambda p: (round(p.x, 5), round(p.y, 5), round(p.z, 5))
uid, R0, owner, vmap = {}, [], [], {}
for o in OBJ:
    for vi, v in enumerate(o.data.vertices):
        p = o.matrix_world @ v.co; k = key_of(p)
        if k not in uid:
            uid[k] = len(R0); R0.append(p); owner.append(set())
        vmap[(o.name, vi)] = uid[k]; owner[uid[k]].add(o.name)
N = len(R0)
P = [None] * N
for o in OBJ:
    e = o.evaluated_get(dg); me = e.to_mesh()
    for vi, v in enumerate(me.vertices):
        P[vmap[(o.name, vi)]] = e.matrix_world @ v.co
    e.to_mesh_clear()
nb = [set() for _ in range(N)]
for o in OBJ:
    for ed in o.data.edges:
        x, y = vmap[(o.name, ed.vertices[0])], vmap[(o.name, ed.vertices[1])]
        nb[x].add(y); nb[y].add(x)
PART = {'хребет': 'корпус', 'грудная клетка': 'корпус', 'живот': 'корпус', 'таз': 'корпус', 'лопатка': 'корпус', 'лопатка.L': 'корпус',
        'шея': 'шея', 'голова': 'голова', 'плечо': 'плечо', 'плечо.L': 'плечо.L', 'предплечье': 'предплечье', 'предплечье.L': 'предплечье.L',
        'бедро': 'бедро', 'бедро.L': 'бедро.L', 'голень': 'голень', 'голень.L': 'голень.L', 'стопа': 'голень', 'стопа.L': 'голень.L'}
vpart = [None] * N
free = [True] * N
W = [dict() for _ in range(N)]
for o in OBJ:
    gn = {g.index: g.name for g in o.vertex_groups}
    for vi, v in enumerate(o.data.vertices):
        k = vmap[(o.name, vi)]
        for ge in v.groups:
            W[k][gn[ge.group]] = W[k].get(gn[ge.group], 0.0) + ge.weight
        best = max(((gn[ge.group], ge.weight) for ge in v.groups if PART.get(gn[ge.group])), key=lambda x: x[1], default=None)
        if best: vpart[k] = PART[best[0]]
        if o.name in BEZ: free[k] = False

# ---------- цель: части ----------
bpy.ops.wm.obj_import(filepath=tgt, forward_axis='NEGATIVE_Z', up_axis='Y')
tg = [o for o in bpy.context.selected_objects if o.type == 'MESH'][0]
bm = bmesh.new(); bm.from_mesh(tg.data); bm.transform(tg.matrix_world); bm.verts.ensure_lookup_table(); bm.faces.ensure_lookup_table()
segs = []
for bn in PART:
    td = tgt_dir(bn)
    if td: segs.append((PART[bn], td[0], td[1]))
def seg_d(p, a_, b_):
    ab = b_ - a_; u = max(0.0, min(1.0, (p - a_).dot(ab) / max(ab.length_squared, 1e-9))); return (p - (a_ + ab * u)).length
# ---------- 2. остаток суставов — пластина ----------
Pa = np.array([list(p) for p in P])
SRC, DST = [], []
bpy.context.view_layer.update()
for bn in PART:
    td = tgt_dir(bn)
    if td and bn in arm.pose.bones:
        pb = arm.pose.bones[bn]
        SRC += [list(AW @ pb.head), list(AW @ pb.tail)]; DST += [list(td[0]), list(td[1])]
S_, D_ = np.array(SRC), np.array(DST)
n = len(S_); Kk = np.linalg.norm(S_[:, None] - S_[None], axis=2) + 1e-4 * np.eye(n)
Pm = np.hstack([np.ones((n, 1)), S_]); A = np.zeros((n + 4, n + 4)); A[:n, :n] = Kk; A[:n, n:] = Pm; A[n:, :n] = Pm.T
Bm = np.zeros((n + 4, 3)); Bm[:n] = D_; Wt = np.linalg.solve(A, Bm)
X = np.linalg.norm(Pa[:, None] - S_[None], axis=2) @ Wt[:n] + np.hstack([np.ones((N, 1)), Pa]) @ Wt[n:]

DAL = float(opt['dal'])
# РАЗМЕТКА ЦЕЛИ ПО ТЕЛУ В ПОЗЕ: грань берёт часть ближайшей вершины шасси (после позы и пластины). По ближайшей КОСТИ
# грудь цели уходила к плечевой кости (она ближе позвоночника), корпусу доставалось 932 грани из 30 тыс., и тело рвалось
# на границах частей. Грани дальше `--dal` от тела (хвост, пряди) — не в счёт
from mathutils import kdtree
KD = kdtree.KDTree(N)
for k in range(N): KD.insert(Vector(X[k]), k)
KD.balance()
fpart = {}
for f_ in bm.faces:
    c_ = f_.calc_center_median(); co_, k_, d_ = KD.find(c_)
    # голова цели — кусок (черта ступени), к ней тело не тянется: грань, ближайшая к кости «голова» цели, не в счёт
    if min(segs, key=lambda s_: seg_d(c_, s_[1], s_[2]))[0] == 'голова': continue
    if d_ <= DAL and vpart[k_]: fpart.setdefault(vpart[k_], []).append(f_)
BVH = {}
for pn, fl in fpart.items():
    vs = {}; polys = []
    for f_ in fl:
        ids = []
        for v_ in f_.verts:
            if v_.index not in vs: vs[v_.index] = len(vs)
            ids.append(vs[v_.index])
        polys.append(ids)
    co = [None] * len(vs)
    for vi_, j in vs.items(): co[j] = bm.verts[vi_].co.copy()
    BVH[pn] = BVHTree.FromPolygons(co, polys)
print('ЧАСТИ ЦЕЛИ: ' + ', '.join('%s %d' % (pn, len(fl)) for pn, fl in sorted(fpart.items())))

# ---------- 3. притяжка по частям ----------
MAXD = float(opt['maxd'])
# РЕГУЛЯРИЗАЦИЯ ЛАПЛАСИАНОМ (как NICP Амберга — жёсткость убывает): min Σ s·|L X − L X₀|² + Σ m_k |X_k − C_k|². Пара (вершина →
# ближайшая точка своей части) берётся, только если нормали смотрят в одну сторону (cos > `--normal`): иначе верх трапеции
# цеплялся за бок дельты цели и вытягивался «крылом». Решение — сопряжёнными градиентами на numpy (scipy в Blender нет)
NBI = [np.array(sorted(nb[k]), dtype=np.int64) for k in range(N)]
DEG = np.array([max(1, len(x)) for x in NBI], dtype=float)
FLAT = np.concatenate([x for x in NBI]); OWN = np.concatenate([np.full(len(x), k) for k, x in enumerate(NBI)])
def lap(Y):
    s = np.zeros_like(Y); np.add.at(s, OWN, Y[FLAT]); return Y - s / DEG[:, None]
def lapT(Y):
    r = Y.copy(); w = Y / DEG[:, None]; np.subtract.at(r, FLAT, w[OWN]); return r
def normals(Y):
    nrm = np.zeros_like(Y)
    for o in OBJ:
        for pl in o.data.polygons:
            ids = [vmap[(o.name, vi)] for vi in pl.vertices]
            a_, b_, c_ = Y[ids[0]], Y[ids[1]], Y[ids[2]]
            n_ = np.cross(b_ - a_, c_ - a_)
            for j in ids: nrm[j] += n_
    l = np.linalg.norm(nrm, axis=1); l[l == 0] = 1; return nrm / l[:, None]
X0 = X.copy(); D0 = lap(X0)
FREE = np.array(free)
COSN = float(opt.get('normal', '0.5'))
# ВСТРЕЧНЫЕ ПАРЫ (`--vstrechnye 1`, 21.10, критик лося: «голень — труба без скакательного сустава» при среднем расстоянии
# тела до цели 0.45 см): пара «вершина → ближайшая точка цели» не требует, чтобы цель была ПОКРЫТА — вершины голени ложились
# на переднюю стенку, а бугор скакательного и копыто не доставались никому. Точка цели (центр грани своей части) тянет к
# себе ближайшую вершину той же части; цель вершины — среднее всех её пар (как двусторонний NICP)
VSTR = opt.get('vstrechnye', '0') == '1'
# `--vstr_maxd` — свой предел встречной пары (критики r3/r2: когти цели вытягивали из кулаков шипы 15–20 см, низ головы — лоскуты на шее)
# число — для всех частей, `часть:число` — своё (волку шерсть дальше 10 см от тела на ногах, а когти — только у кистей)
VMAXD, VMAXP = float(opt['maxd']), {}
for x_ in opt.get('vstr_maxd', opt['maxd']).split(','):
    if ':' in x_: VMAXP[x_.split(':')[0]] = float(x_.split(':')[1])
    else: VMAXD = float(x_)
TS = {}
if VSTR:
    for pn, fl in fpart.items():
        TS[pn] = (np.array([list(f_.calc_center_median()) for f_ in fl]), np.array([list(f_.normal) for f_ in fl]))
for s_ in [float(v) for v in opt.get('zhestkost', '30,15,8,4').split(',')]:
    for _ in range(2):
        NX = normals(X)
        C = X.copy(); M = np.zeros(N)
        for k in range(N):
            if not FREE[k] or vpart[k] not in BVH: continue
            hit = BVH[vpart[k]].find_nearest(Vector(X[k]))
            if hit[0] is None or hit[3] > MAXD: continue
            if np.dot(NX[k], np.array(hit[1])) < COSN: continue
            C[k] = np.array(hit[0]); M[k] = 1.0
        if VSTR:
            acc = np.zeros_like(X); cnt = np.zeros(N)
            acc[M == 1] = C[M == 1]; cnt[M == 1] = 1
            for pn, (cs, ns) in TS.items():
                ids = [k for k in range(N) if FREE[k] and vpart[k] == pn]
                if not ids: continue
                kd_ = kdtree.KDTree(len(ids))
                for j, k in enumerate(ids): kd_.insert(Vector(X[k]), j)
                kd_.balance()
                for c_, n_ in zip(cs, ns):
                    co_, j, d_ = kd_.find(Vector(c_))
                    k = ids[j]
                    if d_ > VMAXP.get(pn, VMAXD) or np.dot(NX[k], n_) < COSN: continue
                    acc[k] += c_; cnt[k] += 1
            has = cnt > 0
            C[has] = acc[has] / cnt[has][:, None]; M[has] = 1.0
        # несвободные (голова) держатся на месте жёстко
        M[~FREE] = 10.0; C[~FREE] = X0[~FREE]
        A = lambda Y: s_ * lapT(lap(Y)) + M[:, None] * Y
        b = s_ * lapT(D0) + M[:, None] * C
        Y = X.copy(); r = b - A(Y); p = r.copy(); rs = np.sum(r * r, axis=0)
        for _cg in range(200):
            Ap = A(p); al = rs / np.maximum(np.sum(p * Ap, axis=0), 1e-12)
            Y += p * al; r -= Ap * al; rn = np.sum(r * r, axis=0)
            if np.all(rn < 1e-10): break
            p = r + p * (rn / np.maximum(rs, 1e-20)); rs = rn
        X = Y
    print('ПОДГОНКА: жёсткость %.1f, пар %d из %d' % (s_, int((M == 1).sum()), int(FREE.sum())))
res = [BVH[vpart[k]].find_nearest(Vector(X[k]))[3] for k in range(N) if free[k] and vpart[k] in BVH]
print('ПОДГОНКА: до своей части цели — средн. %.4f м, 95%% %.4f' % (float(np.mean(res)), float(np.percentile(res, 95))))
# обратная мера — ПОКРЫТИЕ: от точки цели (центр грани своей части) до ближайшей вершины тела той же части; пара «тело → цель»
# мала и тогда, когда тело легло на одну стенку цели
cov = {}
for pn, fl in fpart.items():
    ids = [k for k in range(N) if vpart[k] == pn]
    if not ids: continue
    kd_ = kdtree.KDTree(len(ids))
    for j, k in enumerate(ids): kd_.insert(Vector(X[k]), j)
    kd_.balance()
    cov[pn] = [kd_.find(f_.calc_center_median())[2] for f_ in fl]
allc = np.concatenate([np.array(v) for v in cov.values()])
print('ПОКРЫТИЕ: от цели до тела — средн. %.4f м, 95%% %.4f; по частям 95%%: %s' % (float(allc.mean()), float(np.percentile(allc, 95)),
      ', '.join('%s %.3f' % (pn, float(np.percentile(v, 95))) for pn, v in sorted(cov.items()))))

if opt.get('poza_obj'):
    # тело в позе цели (до возврата в покой) — для кадра «подгонка против цели»; `--poza_obj файл.obj` (оси Blender)
    with open(opt['poza_obj'], 'w') as fo:
        for p in X: fo.write('v %.5f %.5f %.5f' % (p[0], p[2], -p[1]) + chr(10))
        for o in OBJ:
            for pl in o.data.polygons: fo.write('f ' + ' '.join(str(vmap[(o.name, vi)] + 1) for vi in pl.vertices) + chr(10))
# ---------- 4. обратно в покой ----------
if opt.get('proverka', '0') == '1':
    X = Pa.copy()
# ЦЕПЬ НА ДЛИНАХ ЦЕЛИ (ГеймБосс 21.10: поза, посчитанная на длинах К2, роняла оборотня вперёд, стопа висела в 0.27–0.39 м над
# землёй). Игра ставит кость поворотом D = рамка позы · покой К2⁻¹ (по весу ключа сферически), а смещения берёт из суставов
# покоя: голова ребёнка = голова родителя + D_родителя·(покой ребёнка − покой родителя), хвост = голова + D·(покой хвоста −
# покой головы). Суставы покоя выводятся так, чтобы эта цепь давала РОВНО суставы цели: смещение цели, повёрнутое назад
# поворотом родителя. Кость без ориентиров жёстко едет с родителем: смещение К2 от его головы или хвоста (что ближе).
# Корень: покой — К2, поза — таз цели; стопа ложится на землю цели (плюсна z ≈ 0)
bpy.context.view_layer.update()
D, RH, RT, PH, PT = {}, {}, {}, {}, {}
for bn in order:
    pb = arm.pose.bones[bn]; par = pb.parent
    D[bn] = (AW @ pb.matrix).to_3x3() @ (AW @ pb.bone.matrix_local).to_3x3().inverted()
    h0, t0 = AW @ pb.bone.head_local, AW @ pb.bone.tail_local
    td = tgt_dir(bn)
    if par is None:
        RH[bn] = h0; PH[bn] = Vector(td[0]) if td else AW @ pb.head
    else:
        pn = par.name
        if td:
            RH[bn] = RH[pn] + D[pn].inverted() @ (Vector(td[0]) - PH[pn])
        else:
            # без ориентиров — жёстко к родителю: смещение К2 от его головы или хвоста (что ближе в покое К2)
            ph0, pt0 = AW @ par.bone.head_local, AW @ par.bone.tail_local
            RH[bn] = (RT[pn] + (h0 - pt0)) if (h0 - pt0).length < (h0 - ph0).length else (RH[pn] + (h0 - ph0))
        PH[bn] = PH[pn] + D[pn] @ (RH[bn] - RH[pn])
    RT[bn] = RH[bn] + (D[bn].inverted() @ (Vector(td[1]) - Vector(td[0])) if td else (t0 - h0))
    PT[bn] = PH[bn] + D[bn] @ (RT[bn] - RH[bn])
TGT = {bn: (Vector(tgt_dir(bn)[0]), Vector(tgt_dir(bn)[1])) for bn in order if tgt_dir(bn)}
err = max(max((PH[bn] - TGT[bn][0]).length, (PT[bn] - TGT[bn][1]).length) for bn in TGT)
niz = min(min(PH[bn].z, PT[bn].z) for bn in order if bn.startswith('стопа'))
print('ЦЕПЬ: длины цели, прямая кинематика игры до цели — макс %.4f м; низ стопы z %.3f м' % (err, niz))
# скин — как в игре: кость в позе = голова цепи цели PH, поворот D, привязка — покой К2 (без этого форма в покое снималась
# цепью на длинах К2 и в игре съезжала бы на разницу длин)
skin = {bn: (Matrix.Translation(PH[bn]) @ D[bn].to_4x4() @ Matrix.Translation(-(AW @ arm.pose.bones[bn].bone.head_local))) for bn in order}
if opt.get('proverka', '0') == '1':     # проверка туда-обратно — на той же цепи К2, которой тело ставилось в позу
    skin = {pb.name: (AW @ pb.matrix @ pb.bone.matrix_local.inverted() @ AW.inverted()) for pb in arm.pose.bones}
REST = np.zeros_like(X)
for k in range(N):
    ws = W[k]; tot = sum(w for g, w in ws.items() if g in skin)
    if tot <= 0: REST[k] = X[k]; continue
    M = sum((skin[g] * (w / tot) for g, w in ws.items() if g in skin), Matrix.Diagonal((0, 0, 0, 0)))
    REST[k] = np.array(M.inverted() @ Vector(X[k]))
# ЦЕЛЬ В ПОКОЕ (`--cel_pokoj файл.obj`, 21.10): голова и прочие черты ступени режутся из цели `kusok_celi.py`, а он ждёт цель
# поверх тела с ключом на весе 1, то есть в ПОКОЕ. Вершина цели снимается из позы обратной матрицей скина ближайшей
# вершины подогнанного тела; у головы цели (ближайшая кость цели — «голова») — матрицей одной кости «голова», иначе на
# стыке шеи весы смешивания мнут морду и рога
if opt.get('cel_pokoj'):
    KDX = kdtree.KDTree(N)
    for k in range(N): KDX.insert(Vector(X[k]), k)
    KDX.balance()
    # вдали от тела (дальше `--dal`: хвост, пряди гривы) — по ближайшей вершине КОРПУСА, шеи или головы: хвост цели у
    # икры, и по ближайшей вершине он ехал бы с голенью (в покое — доской вдоль ноги, при шаге — маятником)
    KDC_ids = [k for k in range(N) if vpart[k] in ('корпус', 'шея', 'голова')]
    KDC = kdtree.KDTree(len(KDC_ids))
    for j, k in enumerate(KDC_ids): KDC.insert(Vector(X[k]), j)
    KDC.balance()
    def ves(k):
        ws = {g: w for g, w in W[k].items() if g in skin}
        top = sorted(ws.items(), key=lambda kv: -kv[1])[:4]; tot = sum(w for _, w in top)
        return {g: w / tot for g, w in top} if tot > 0 else {}
    # ВЕСА ЦЕЛИ — тем же правилом, что и матрица снятия (файл `<cel_pokoj>.vesa.json`, вершина за вершиной): кусок,
    # вырезанный `kusok_celi.py --vesa`, в позе ступени встаёт ровно туда, где был у цели. С весами, перенесёнными с тела
    # в покое, хвост и пряди рвались — другое правило давало другую матрицу
    cache, VES = {}, []
    with open(opt['cel_pokoj'], 'w') as fo:
        for v_ in bm.verts:
            p = v_.co
            if min(segs, key=lambda s_: seg_d(p, s_[1], s_[2]))[0] == 'голова':
                ws = {'голова': 1.0}
            else:
                _, k_, d_ = KDX.find(p)
                if d_ > DAL: k_ = KDC_ids[KDC.find(p)[1]]
                ws = ves(k_)
            key_ = tuple(sorted(ws.items()))
            if key_ not in cache:
                cache[key_] = sum((skin[g] * w for g, w in ws.items()), Matrix.Diagonal((0, 0, 0, 0))).inverted() if ws else Matrix.Identity(4)
            q = cache[key_] @ p
            VES.append({g: round(w, 4) for g, w in ws.items()})
            fo.write('v %.5f %.5f %.5f' % (q.x, q.z, -q.y) + chr(10))
        for f_ in bm.faces: fo.write('f ' + ' '.join(str(v_.index + 1) for v_ in f_.verts) + chr(10))
    json.dump(VES, open(opt['cel_pokoj'] + '.vesa.json', 'w', encoding='utf-8'), ensure_ascii=False)
    print('ФАЙЛ: цель в покое →', opt['cel_pokoj'], '(+ .vesa.json)')
# ФАЙЛЫ ДЛЯ ИГРЫ (решение ГеймБосса 21.10, вариант Б: ключ — форма в покое, позу ступени даёт слой кода):
#  `--poza файл` — кости шасси В ПОЗЕ ЦЕЛИ (head/tail/z_axis, оси Blender, длины свои — только повороты), игра выводит поворот
#  кости разностью рамок покоя и позы и ставит его по весу ключа сферически, после привязки скина;
#  `--sustavy файл` — суставы цели В ПОКОЕ под новую форму: голова и хвост кости — в её рамке позы, переведённые обратно в
#  покой обратной матрицей той же кости (пропорции цели без позы)
def frames(src_bones):
    out_ = []
    for b in SK.values():
        pb = arm.pose.bones.get(b['name'])
        if pb is None: continue
        M = AW @ pb.matrix
        out_.append(src_bones(b, pb, M))
    return out_
if opt.get('poza'):
    def posed(b, pb, M):
        z = (M.to_3x3() @ Vector((0, 0, 1))).normalized()
        return dict(name=b['name'], parent=b.get('parent', ''), head=[round(c, 5) for c in PH[b['name']]], tail=[round(c, 5) for c in PT[b['name']]],
                    z_axis=[round(c, 5) for c in z])
    sk0 = json.load(open(sk_p, encoding='utf-8'))
    pz = dict(species=sk0.get('species', 'Человек'), kluch=NAME, axes=sk0.get('axes', ''), bones=frames(posed),
              note='кости в позе цели «%s» на ступени 2 (вес 1): рамка (z_axis, направление) — поворот; head/tail — суставы цели '
                   '(цепь на длинах суставов покоя *-sustavy.json); podgonka_v_poze.py' % NAME)
    json.dump(pz, open(opt['poza'], 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    print('ФАЙЛ: поза →', opt['poza'])
if opt.get('sustavy'):
    def rest_j(b, pb, M):
        return dict(name=b['name'], parent=b.get('parent', ''), head=[round(c, 5) for c in RH[b['name']]], tail=[round(c, 5) for c in RT[b['name']]],
                    z_axis=b.get('z_axis', [0, 0, 1]))
    sk0 = json.load(open(sk_p, encoding='utf-8'))
    sj = dict(sk0); sj['bones'] = frames(rest_j)
    sj['note'] = ('суставы цели «%s» в ПОКОЕ шасси: смещения цели, повёрнутые назад поворотом родителя из *-poza.json — прямая '
                  'кинематика игры (D·смещение покоя) даёт суставы цели; podgonka_v_poze.py' % NAME)
    json.dump(sj, open(opt['sustavy'], 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    print('ФАЙЛ: суставы покоя →', opt['sustavy'])
# поза сбрасывается, форма-ключ
bpy.context.view_layer.objects.active = arm; bpy.ops.object.mode_set(mode='POSE')
for pb in arm.pose.bones:
    pb.rotation_quaternion = Quaternion(); pb.location = (0, 0, 0)
bpy.ops.object.mode_set(mode='OBJECT')
bpy.data.objects.remove(tg, do_unlink=True)
for o in OBJ:
    if o.data.shape_keys is None: o.shape_key_add(name='Basis')
    if NAME in o.data.shape_keys.key_blocks: o.shape_key_remove(o.data.shape_keys.key_blocks[NAME])
    kb = o.shape_key_add(name=NAME, from_mix=False); inv = o.matrix_world.inverted()
    for vi in range(len(o.data.vertices)):
        kb.data[vi].co = inv @ Vector(REST[vmap[(o.name, vi)]])
err = np.linalg.norm(REST - np.array([list(p) for p in R0]), axis=1)
print('ОБРАТНО: отклонение покоя от исходного К2 — средн. %.4f, макс %.4f м (при --proverka 1 должно быть ~0)' % (err.mean(), err.max()))
bpy.ops.wm.save_as_mainfile(filepath=out)
print('ПОДГОНКА: готово →', out)
