"""ПЕРЕНОС КЛЮЧЕЙ НА НОВУЮ БАЗУ ЧЕРЕЗ МОСТ (03.11, ГеймБосс: «проекция по ближайшей поверхности дала 50.9 мм на кисти — главный риск»).
Прямая проекция «новая база → старая база» промахивается там, где формы разошлись: поджарая кисть ложится не на свои пальцы атлета.
Мост — К2 с ключом подгонки: та же топология, что К2-атлет с ключами ступеней, а поверхность лежит на новой базе (новая база из неё и
получена: прореживание + правки).

  вершина новой базы → ближайшая точка МОСТА (своего объекта) → (треугольник, барицентрика) → та же точка в ключе источника

Режимы цели: `--rezhim abs` (по умолчанию) — вершина ключа = точка на ключе источника: ключ ступени — цель по листу вида, она не зависит от
базы; `--rezhim delta` — база + смещение ключа источника в этой точке. Сравнение — `--kontrol 1`: тот же перенос прямой проекцией на
атлета (как у дизайн-линии). Меры (расстояние «вершина нового ключа → поверхность ключа» бессмысленно: любой способ кладёт на неё):
  `sootv` — ОШИБКА СООТВЕТСТВИЯ на вершинах К2, где истина известна: вершина i моста обязана попасть в вершину i ключа; мост даёт 0 по
  построению, значит здесь мерится прямая проекция, и как вторая строка — мост через саму новую базу (i → новая база → ключ);
  `pokrytie` — от вершин ключа источника до поверхности нового ключа (дыры и схлопнутые места видны здесь);
  `vyvoroty` — грани нового ключа, чья нормаль смотрит против поверхности ключа источника в этой точке (> 90°).
Всё по объектам и отдельно кисть (`Руки`, z < `--z_kist`). Вывороты разделены: `skladka` — ключ источника и сам вывернут здесь относительно своей базы
(законная складка); `shchepka` — грань меньше 2 см²; `nastoyashchie` — остальные.
`--kist_zhestko Z` (03.11): кисть новой базы (`Руки`, z < Z) построена заново и от моста ушла: она идёт жёстко — поворот и масштаб с кисти источника, опора на кольцо над запястьем, переход 3 см.
`--raspravit N` (03.11, стенд: изнанки на ключах больше контроля): крупные грани новой базы садятся на складки формы ключа и переворачиваются. Грань считается перевёрнутой, если её нормаль в ключе смотрит против нормали ближайшей поверхности ключа источника; смещение ключа в её вершинах и их соседях сглаживается (Таубин, N заходов). Остальная форма стоит. Общие вершины объектов после всего сводятся к одной цели, иначе шов открывается.

  blender -b новая.blend --factory-startup -P perenos_klyuchej.py -- выход.blend источник.fbx мост.blend [--klyuchi Волк,Волк1,Лось,…|vse]
      [--most_kluch Поджарый] [--rezhim abs] [--kontrol 1] [--z_kist 0.98] [--otchet файл.json]
Источник и мост обязаны совпадать по числу вершин и порядку в объектах (проверяется по базе: расхождение > 1 мм — отказ).
"""
import sys, json
import bpy, bmesh
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

a = sys.argv[sys.argv.index('--') + 1:]
out, src_fbx, most_p = a[:3]
opt = dict(klyuchi='vse', most_kluch='Поджарый', rezhim='abs', kontrol='1', z_kist='0.98')
i = 3
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
ZK = float(opt['z_kist'])


def tris(o):
    me = o.data; me.calc_loop_triangles(); return np.array([list(t.vertices) for t in me.loop_triangles], dtype=np.int64)


# ---------- новая база: объекты текущего файла ----------
NEW = {o.name: o for o in bpy.data.objects if o.type == 'MESH' and '@' not in o.name}
# ---------- мост: координаты подгонки ----------
with bpy.data.libraries.load(most_p) as (s_, d_): d_.objects = [n for n in s_.objects]
MOST = {}
for ob in d_.objects:
    if ob is None or ob.type != 'MESH' or '@' in ob.name: continue
    nm = ob.name.split('.')[0]
    if ob.name in NEW and ob.name == nm:      # одноимённые объекты библиотеки получают суффикс — ниже разбор по data
        pass
    kb = ob.data.shape_keys.key_blocks.get(opt['most_kluch']) if ob.data.shape_keys else None
    if kb is None: continue
    n_ = len(kb.data); B = np.zeros(n_ * 3); K = np.zeros(n_ * 3)
    kb.data.foreach_get('co', K); ob.data.vertices.foreach_get('co', B)
    MOST[nm] = dict(K=K.reshape(-1, 3), B=B.reshape(-1, 3), T=tris(ob))
# ---------- источник ----------
keep = set(bpy.data.objects)
bpy.ops.import_scene.fbx(filepath=src_fbx, axis_forward='-Z', axis_up='Y', automatic_bone_orientation=False)
SRC = {}
imported = [o for o in bpy.data.objects if o not in keep]
for ob in imported:
    if ob.type != 'MESH' or '@' in ob.name or not ob.data.shape_keys: continue
    nm = ob.name.split('.')[0]; Mw = np.array(ob.matrix_world)
    d = {}
    for kb in ob.data.shape_keys.key_blocks:
        c = np.zeros(len(kb.data) * 3); kb.data.foreach_get('co', c); c = c.reshape(-1, 3)
        d[kb.name] = c @ Mw[:3, :3].T + Mw[:3, 3]
    SRC[nm] = dict(k=d, T=tris(ob))
for ob in imported: bpy.data.objects.remove(ob)

bary = lambda p, A, B, C: _bary(p, A, B, C)


def _bary(p, A, B, C):
    v0, v1, v2 = B - A, C - A, p - A
    d00, d01, d11, d20, d21 = v0 @ v0, v0 @ v1, v1 @ v1, v2 @ v0, v2 @ v1
    den = d00 * d11 - d01 * d01
    if abs(den) < 1e-14: return np.array([1.0, 0, 0])
    v = (d11 * d20 - d01 * d21) / den; w = (d00 * d21 - d01 * d20) / den
    return np.clip(np.array([1 - v - w, v, w]), 0, 1)


def bvh_of(P, T):
    return BVHTree.FromPolygons([Vector(p) for p in P], [list(t) for t in T.tolist()], all_triangles=True)


def perenos(V, P_most, T, Kl):
    """V (n×3) — вершины новой базы; P_most — поверхность, на которую проецируем; Kl — тот же меш в ключе"""
    b = bvh_of(P_most, T); R = np.zeros_like(V); D = np.zeros(len(V))
    for k, p in enumerate(V):
        loc, _, fi, d = b.find_nearest(Vector(p))
        t = T[fi]; w = _bary(np.array(loc), P_most[t[0]], P_most[t[1]], P_most[t[2]])
        R[k] = w[0] * Kl[t[0]] + w[1] * Kl[t[1]] + w[2] * Kl[t[2]]; D[k] = d
    return R, D


otchet = {}; vse = []
for nm, o in NEW.items():
    if nm not in SRC or nm not in MOST: print('ПЕРЕНОС: %s — нет в источнике или мосте' % nm); continue
    S, M_ = SRC[nm], MOST[nm]
    kb_o = o.data.shape_keys.key_blocks.get(opt['most_kluch']) if o.data.shape_keys else None
    n_ = len(o.data.vertices); V = np.zeros(n_ * 3)
    if kb_o is not None: kb_o.data.foreach_get('co', V)
    else: o.data.vertices.foreach_get('co', V)
    V = V.reshape(-1, 3)
    Bs = S['k']['Basis']
    if len(Bs) != len(M_['K']): sys.exit('ПЕРЕНОС: %s — источник %d вершин, мост %d' % (nm, len(Bs), len(M_['K'])))
    r_ = float(np.linalg.norm(Bs - M_['B'], axis=1).max())
    if r_ > 0.001:
        # объект не подгонялся и у источника другая база (`Чутьё`: уши в FBX правились позже): мостом служит сама база источника
        print('ПЕРЕНОС: %s — база источника и моста расходятся на %.1f мм, мост = база источника' % (nm, r_ * 1000)); M_ = dict(M_, K=Bs, B=Bs); T = S['T']
    T = M_.get('T') if r_ <= 0.001 else S['T']; names = [k for k in S['k'] if k != 'Basis'] if opt['klyuchi'] == 'vse' else [k for k in opt['klyuchi'].split(',') if k in S['k']]
    z_rest = V[:, 2]; kist = (nm == 'Руки') & (z_rest < ZK) if nm == 'Руки' else np.zeros(n_, bool)
    for kn in names:
        Kl = S['k'][kn]
        R, dm = perenos(V, M_['K'], T, Kl)
        if opt['rezhim'] == 'delta':
            R0, _ = perenos(V, M_['K'], T, Bs); R = V + (R - R0)
        tri3 = lambda x: [round(float(x.mean()) * 1000, 1), round(float(np.percentile(x, 95)) * 1000, 1), round(float(x.max()) * 1000, 1)]
        if opt.get('kist_zhestko') and nm == 'Руки':
            # кисть новой базы идёт жёстко: поворот и масштаб снимаются с кисти источника (Procrustes база → ключ), опора — кольцо над запястьем по цели. Смещения с кулака старой формы переворачивали всю кисть (пятно 20 см²)
            ZW = float(opt['kist_zhestko'])
            for sd_ in (-1, 1):
                hs = (Bs[:, 2] < ZW) & (Bs[:, 0] * sd_ > .18); hn = (V[:, 2] < ZW + .03) & (V[:, 0] * sd_ > .18); ringn = (V[:, 2] > ZW + .005) & (V[:, 2] < ZW + .06) & (V[:, 0] * sd_ > .18)
                A_, B_ = Bs[hs], Kl[hs]; ca, cb = A_.mean(0), B_.mean(0); U_, S_, Vt_ = np.linalg.svd((A_ - ca).T @ (B_ - cb))
                Dg = np.diag([1, 1, np.sign(np.linalg.det(Vt_.T @ U_.T))]); Rot = Vt_.T @ Dg @ U_.T; sc = float((S_ * np.diag(Dg)).sum() / max(((A_ - ca) ** 2).sum(), 1e-12))
                for ok_ in (.10, .15, .20):   # на низком бюджете кольца вершин на предплечье реже окна
                    if ringn.any(): break
                    ringn = (V[:, 2] > ZW + .005) & (V[:, 2] < ZW + ok_) & (V[:, 0] * sd_ > .18); print('ПЕРЕНОС: окно над запястьем расширено до %d см' % (ok_ * 100))
                if not ringn.any(): sys.exit('ПЕРЕНОС: нет вершин над запястьем')
                vw, kw = V[ringn].mean(0), R[ringn].mean(0); Rg = kw + ((V[hn] - vw) * sc) @ Rot.T
                t_ = np.clip((ZW + .03 - V[hn, 2]) / .03, 0, 1)[:, None]; t_ = t_ * t_ * (3 - 2 * t_); R[hn] = R[hn] * (1 - t_) + Rg * t_
        Tn = tris(o)
        if int(opt.get('raspravit', 0)):
            NB_ = [set() for _ in range(n_)]
            for t_ in Tn:
                for a_, b_ in ((0, 1), (1, 2), (2, 0)): NB_[t_[a_]].add(t_[b_]); NB_[t_[b_]].add(t_[a_])
            NB_ = [list(q) for q in NB_]; bo_ = bvh_of(Kl, T); n_fl0 = None
            for _ in range(int(opt['raspravit'])):
                A_ = Rx_ = R; e1 = A_[Tn[:, 1]] - A_[Tn[:, 0]]; e2 = A_[Tn[:, 2]] - A_[Tn[:, 0]]; Nn = np.cross(e1, e2); C_ = A_[Tn].mean(1); fl = []
                for i_ in range(len(Tn)):
                    if np.linalg.norm(Nn[i_]) < 1e-10: continue
                    _, no_, _, _ = bo_.find_nearest(Vector(C_[i_]))
                    if Nn[i_] @ np.array(no_) < 0: fl.append(i_)
                if n_fl0 is None: n_fl0 = len(fl)
                if not fl: break
                W_ = np.zeros(n_)
                for i_ in fl:
                    for v_ in Tn[i_]:
                        W_[v_] = 1
                        for q_ in NB_[v_]: W_[q_] = max(W_[q_], .5)
                D_ = R - V
                for lam in (.5, -.53):
                    D_ = D_ + lam * W_[:, None] * np.array([D_[q].mean(0) - D_[k_] if q else np.zeros(3) for k_, q in enumerate(NB_)])
                R = V + D_
            print('РАСПРАВЛЕНИЕ: %s · %s — перевёрнуто %s → %d' % (nm, kn, n_fl0, len(fl)))
        zk = M_['K'][:, 2] < ZK if nm == 'Руки' else np.zeros(len(Kl), bool)
        rec = dict(ob=nm, kluch=kn, most_mm=tri3(dm))
        def mery(Rx, tag):
            # соответствие: i моста → (новая поверхность в покое) → новый ключ; истина — Kl[i]
            bn = bvh_of(V, Tn); er = np.zeros(len(Kl))
            for k_, p_ in enumerate(M_['K']):
                loc, _, fi, _ = bn.find_nearest(Vector(p_)); t_ = Tn[fi]; w_ = _bary(np.array(loc), V[t_[0]], V[t_[1]], V[t_[2]])
                er[k_] = np.linalg.norm(w_[0] * Rx[t_[0]] + w_[1] * Rx[t_[1]] + w_[2] * Rx[t_[2]] - Kl[k_])
            bk = bvh_of(Rx, Tn); po = np.array([bk.find_nearest(Vector(p_))[3] for p_ in Kl])
            bo = bvh_of(Kl, T); fl = dict(skladka=0, shchepka=0, nastoyashchie=0); mesta = []
            for t_ in Tn:
                c_ = Rx[t_].mean(0); nn_ = np.cross(Rx[t_[1]] - Rx[t_[0]], Rx[t_[2]] - Rx[t_[0]]); ar = np.linalg.norm(nn_) / 2
                _, n_old, fo, _ = bo.find_nearest(Vector(c_))
                if ar > 1e-10 and nn_ @ np.array(n_old) < 0:
                    to = T[fo]; n_s0 = np.cross(Bs[to[1]] - Bs[to[0]], Bs[to[2]] - Bs[to[0]]); n_s1 = np.cross(Kl[to[1]] - Kl[to[0]], Kl[to[2]] - Kl[to[0]])
                    if n_s0 @ n_s1 < 0: fl['skladka'] += 1
                    elif ar < 2e-4: fl['shchepka'] += 1
                    else: fl['nastoyashchie'] += 1; mesta.append([round(float(q), 3) for q in c_])
            rec[tag] = dict(sootv_mm=tri3(er), pokrytie_mm=tri3(po), vyvoroty=fl, mesta=mesta[:12])
            if zk.any(): rec[tag]['kist'] = dict(sootv_mm=tri3(er[zk]), pokrytie_mm=tri3(po[zk]))
        mery(R, 'most')
        if opt['kontrol'] == '1':
            Rn, _ = perenos(V, Bs, T, Kl); mery(Rn, 'pryamo')
        print('ПЕРЕНОС: ' + json.dumps(rec, ensure_ascii=False)); vse.append(rec)
        if not np.isfinite(R).all(): sys.exit('ПЕРЕНОС: %s · %s — NaN' % (nm, kn))
        kb = o.data.shape_keys.key_blocks.get(kn) or o.shape_key_add(name=kn, from_mix=False); kb.data.foreach_set('co', R.ravel())
        kb.value = 0.0
# ШВЫ: общие вершины объектов (одно место в покое) получали цель от своего объекта; шов раскрывался — сводим к средней
for kn in {r['kluch'] for r in vse}:
    pts = {}
    for nm, o in NEW.items():
        kb = o.data.shape_keys.key_blocks.get(kn) if o.data.shape_keys else None
        bb = (o.data.shape_keys.key_blocks.get(opt['most_kluch']) or o.data.shape_keys.key_blocks[0]) if o.data.shape_keys else None     # после `baza_iz_klyucha.py` база = Basis
        if kb is None or bb is None: continue
        for vi in range(len(kb.data)): pts.setdefault(tuple(np.round(np.array(bb.data[vi].co), 4)), []).append((kb, vi))
    n_sv = 0; mx = 0.0
    for lst in pts.values():
        if len(lst) < 2: continue
        c_ = np.mean([np.array(k_.data[i_].co) for k_, i_ in lst], axis=0); n_sv += 1
        for k_, i_ in lst:
            mx = max(mx, float(np.linalg.norm(np.array(k_.data[i_].co) - c_))); k_.data[i_].co = c_
    print('ПЕРЕНОС: %s — швов сведено точек %d, макс сдвиг %.1f мм' % (kn, n_sv, mx * 1000))
if opt.get('otchet'): json.dump(vse, open(opt['otchet'], 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
bpy.ops.wm.save_as_mainfile(filepath=out)
