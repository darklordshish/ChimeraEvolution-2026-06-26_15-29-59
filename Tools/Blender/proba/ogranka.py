"""ОГРАНКА ПО ОБЪЁМАМ (21.10, шаг 6 Д консилиума; ГеймБосс по листу r8: «огранка мятая — мелкие треугольники вразнобой, у листа
крупные грани по объёмам»). Форма-ключ после подгонки несёт шум цели: соседние грани смотрят в разные стороны, и плоская
заливка рисует щепу. Огранка собирает грани в области с общей плоскостью и выпрямляет их:

  1. грани объекта делятся на области k-средних по признаку [центр / `--masshtab`, нормаль] (область — связный кусок одной
     ориентации; число областей — площадь объекта / `--ploshchad` м²);
  2. вершина тянется к своим плоскостям (среднее проекций на плоскости областей её граней) с силой `--sila`, а форма держится
     лапласианом — несколько итераций;
  3. край объекта (шов с соседом: шея–голова, рука–корпус) не двигается — куски и паспорта швов садятся как раньше.

Правится только форма-ключ `--imya` (базовая форма К2 — нет).

  blender -b --factory-startup -P ogranka.py -- вход.blend выход.blend [--imya Волк] [--ploshchad 0.006] [--masshtab 0.08] [--sila 0.7] [--iter 6] [--bez голова,Чутьё] [--rezhim kosti|myshcy] [--kuski 1 [--ploshchad_kuski 0.01]]
"""
import sys
import bpy, bmesh
import numpy as np

a = sys.argv[sys.argv.index('--') + 1:]
src, out = a[0], a[1]
opt = dict(imya='Волк', ploshchad_kuski='0.01', ploshchad='0.006', masshtab='0.08', sila='0.7', iter='6', bez='голова,Чутьё')
i = 2
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
NAME = opt['imya']; BEZ = set(opt['bez'].split(',')) if opt['bez'] else set()
rng = np.random.default_rng(7)

bpy.ops.wm.open_mainfile(filepath=src)
tot_r = 0
ARM = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]; AW = ARM.matrix_world
# участков по длине и секторов вокруг оси по кости (`--rezhim kosti`); кость без записи — «*». Стопа не трогается: ось её кости в
# покое К2 горизонтальна, а подогнанная форма там — пальцеходящая плюсна вверх, и сектора по горизонтальной оси раздували «сапог»
NE_TROGAT = {'стопа'}
TAB = {'бедро': (4, 8), 'голень': (4, 8), 'стопа': (3, 6), 'плечо': (4, 8), 'предплечье': (4, 7), 'лопатка': (3, 6),
       'грудная клетка': (4, 12), 'живот': (4, 10), 'таз': (3, 10), 'хребет': (4, 10), 'шея': (3, 8), '*': (3, 8)}
def u_ang(c, bn):
    """доля по длине кости и угол вокруг оси: 0 — вперёд, 90 — наружу, 270 — внутрь (зеркально по стороне)"""
    pb = ARM.data.bones.get(bn)
    h = np.array(list(AW @ pb.head_local)); t = np.array(list(AW @ pb.tail_local)); ax = t - h; L2 = max(ax @ ax, 1e-9)
    u = float(np.clip((c - h) @ ax / L2, 0, 0.999)); r = c - (h + ax * u); a_ = ax / np.sqrt(L2)
    fw = np.array([0, -1.0, 0]); fw = fw - a_ * (fw @ a_); fw /= max(np.linalg.norm(fw), 1e-9); sd = np.cross(a_, fw)
    sg = -1.0 if bn.endswith('.L') else 1.0
    return u, np.degrees((np.arctan2(sg * (r @ sd), r @ fw) + 2 * np.pi) % (2 * np.pi))


def sektor(ang, granicy):
    """номер сектора по списку границ в градусах (по кругу)"""
    for i, g in enumerate(granicy):
        if ang < g: return i
    return 0


def myshca(c, bn, obj):
    x, y, z = c; ax_ = abs(x); sd = 'L' if x > 0 else 'R'; b = bn.split('.')[0]
    front = y < 0.01
    if b in ('плечо',):
        u, a = u_ang(c, bn)
        if u < 0.3: return (sd, 'дельта', sektor(a, [50, 130, 360]))           # передний / средний / задний пучок
        s_ = sektor(a, [60, 120, 240, 300, 360])                               # бицепс / наружная / трицепс / внутр. / бицепс
        s_ = 0 if s_ == 4 else s_
        return (sd, 'плечо', s_, int(u > 0.62))
    if b == 'предплечье':
        u, a = u_ang(c, bn)
        return (sd, 'предплечье', sektor(a, [90, 270, 360]) % 2, int(u > 0.45))  # разгибатели снаружи / сгибатели внутри
    if b == 'лопатка':
        if front: return (sd, 'грудная', 'верх')
        return (sd, 'трапеция', int(z > 1.5))
    if b == 'бедро':
        u, a = u_ang(c, bn)
        if a < 40 or a > 330: return (sd, 'RF', int(u > 0.5))
        if a < 140: return (sd, 'VL' if u < 0.7 else 'VL_низ')
        if a < 220: return (sd, 'задняя', int(u > 0.5), int(a > 180))
        if u > 0.6: return (sd, 'VM')                                          # капля над коленом
        return (sd, 'приводящие', int(u > 0.3))
    if b == 'голень':
        u, a = u_ang(c, bn)
        if u < 0.12: return (sd, 'колено', int(90 < a < 270))
        if 120 < a < 240 and u < 0.55: return (sd, 'икра', int(a > 180))     # две головки
        return (sd, 'голень', sektor(a, [120, 240, 360]) % 2, int(u > 0.55))
    if b == 'таз' or (obj == 'Ноги' and b in ('хребет', 'живот')):
        if not front: return (sd, 'ягодица', int(z > 0.92))
        return (sd, 'пах')
    if obj == 'шея' or b == 'шея':
        if front: return (sd, 'шея', 'перед')
        return (sd, 'шея', 'бок' if ax_ > 0.04 else 'зад')
    # торс: грудная клетка, живот, хребет
    if front:
        z_grud = 1.27 + 0.55 * ax_                     # нижний край грудной — косо вверх к подмышке
        if z > z_grud and ax_ < 0.21: return (sd, 'грудная', int(z > z_grud + 0.07))
        if ax_ < 0.085:
            for k, zk in enumerate((1.05, 1.12, 1.19)):    # шевроны пресса
                if z < zk + 0.35 * ax_: return (sd, 'пресс', k)
            return (sd, 'пресс', 3)
        if z > 1.21: return (sd, 'зубчатая')
        return (sd, 'косая', int(z > 1.10))
    if z > 1.40: return (sd, 'трапеция', 0)
    if ax_ < 0.055: return (sd, 'разгибатели', int(z > 1.18))
    return (sd, 'широчайшая', int(z > 1.24), int(ax_ > 0.12))


# КУСКИ (`--kuski 1`, решение геймдизайнера 21.10: «у гривы и хвоста крупные грани по прядям, без мехового шума»): куски @ правятся
# по своей сетке (форм-ключа у них нет) обычным режимом областей с площадью `--ploshchad_kuski`
KUSKI = opt.get('kuski', '0') == '1'
obs = [o for o in bpy.data.objects if o.type == 'MESH' and not o.name.startswith('@') and o.name not in BEZ]
if KUSKI: obs += [o for o in bpy.data.objects if o.type == 'MESH' and o.name.startswith('@')]
for o in obs:
    kusok = o.name.startswith('@')
    if kusok:
        kb = None
        P = np.array([list(v.co) for v in o.data.vertices])
    else:
        ks = o.data.shape_keys
        if ks is None or NAME not in ks.key_blocks: continue
        kb = ks.key_blocks[NAME]
        P = np.array([list(d.co) for d in kb.data])
    bm = bmesh.new(); bm.from_mesh(o.data); bm.verts.ensure_lookup_table(); bm.faces.ensure_lookup_table()
    F = [[v.index for v in f.verts] for f in bm.faces]
    fixed = np.array([v.is_boundary for v in bm.verts])
    nb = [[e.other_vert(v).index for e in v.link_edges] for v in bm.verts]
    vf = [[f.index for f in v.link_faces] for v in bm.verts]
    bm.free()

    def geom(P):
        C = np.array([P[f].mean(0) for f in F])
        N = np.array([np.cross(P[f[1]] - P[f[0]], P[f[2]] - P[f[0]]) for f in F])
        A = np.linalg.norm(N, axis=1) / 2; N = N / np.maximum(2 * A, 1e-12)[:, None]
        return C, N, A
    C, N, A = geom(P)
    if opt.get('rezhim') == 'myshcy' and not kusok:
        # КАРТА МЫШЦ ШАБЛОНА К2 (критик r13: «сетка кость × участок × сектор режет мышцу поперёк — полосы на груди»): область —
        # мышца по анатомической границе в БАЗОВЫХ координатах К2 (до ключа: карта привязана к топологии и годится под любой
        # ключ). Сторона — знак x (слева и справа одинаково). Грани одной мышцы выпрямляются в 1–3 плоскости, на стыках мышц —
        # рёбра рисунка листа: нижний край грудной, шеврон пресса, «V» дельты, VM/VL над коленом, верх икроножной
        gn = {g.index: g.name for g in o.vertex_groups}
        Mw = o.matrix_world
        B0 = np.array([list(Mw @ v.co) for v in o.data.vertices])
        vb = []
        for v in o.data.vertices:
            b = max(v.groups, key=lambda g: g.weight) if v.groups else None
            vb.append(gn[b.group] if b else '')
        lab = np.zeros(len(F), dtype=np.int64); key_ = {}
        for fi, f in enumerate(F):
            c = B0[f].mean(0)
            bn = max(set(vb[v] for v in f), key=lambda n: sum(1 for v in f if vb[v] == n))
            kk = myshca(c, bn, o.name)
            lab[fi] = key_.setdefault(kk, len(key_))
        # нормали для разбиения — со СГЛАЖЕННОЙ копии (Таубин, 30 шагов): шум подгонки рассыпал группы по нормали на крошки
        # (1272 связных куска — та же щепа); выпрямляется исходная форма
        Ps = P.copy()
        for it_ in range(30):
            for lam_ in (0.5, -0.53):
                Ls = np.array([Ps[nb[v]].mean(0) - Ps[v] if nb[v] else np.zeros(3) for v in range(len(Ps))])
                Ps = np.where(fixed[:, None], Ps, Ps + lam_ * Ls)
        _, N, _ = geom(Ps)
        # внутри мышцы — 2–6 плоскостей по нормали (k-средних по нормали, число — площадь мышцы / `--ploshchad_ploskosti`):
        # целая мышца одной плоскостью сплющивала конечность (сдвиг до 30 см)
        lab2 = np.zeros_like(lab); nxt = 0
        for k in np.unique(lab):
            idx = np.nonzero(lab == k)[0]
            kk_ = int(np.clip(A[idx].sum() / float(opt.get('ploshchad_ploskosti', '0.008')), 1, 6))
            if kk_ == 1 or len(idx) < 4:
                lab2[idx] = nxt; nxt += 1; continue
            Nn = N[idx]; cen = Nn[rng.choice(len(idx), size=kk_, replace=False)]
            for _ in range(10):
                l_ = ((Nn[:, None] - cen[None]) ** 2).sum(2).argmin(1)
                for j in range(kk_):
                    m = l_ == j
                    if m.any(): cen[j] = (Nn[m] * A[idx][m, None]).sum(0)
                    cen[j] /= max(np.linalg.norm(cen[j]), 1e-9)
            lab2[idx] = nxt + l_; nxt += kk_
        lab = lab2
    if opt.get('rezhim') in ('myshcy', 'kosti') or True:
        # область — СВЯЗНЫЙ кусок: группа по нормали несвязна (грани с одной нормалью на разной глубине мышцы), и общая плоскость
        # через средний центр сдвигала их до 20 см
        ed = {}
        for fi, f in enumerate(F):
            for i_ in range(len(f)):
                e = tuple(sorted((f[i_], f[(i_ + 1) % len(f)]))); ed.setdefault(e, []).append(fi)
        adj = [[] for _ in F]
        for fs in ed.values():
            for a_ in fs:
                for b_ in fs:
                    if a_ != b_: adj[a_].append(b_)
        lab3 = -np.ones(len(F), dtype=np.int64); nxt = 0
        for f0 in range(len(F)):
            if lab3[f0] >= 0: continue
            st = [f0]; lab3[f0] = nxt
            while st:
                g = st.pop()
                for h_ in adj[g]:
                    if lab3[h_] < 0 and lab[h_] == lab[f0]: lab3[h_] = nxt; st.append(h_)
            nxt += 1
        # островки мельче `--min_oblast` м² сливаются с соседом, с которым больше общих рёбер (иначе на границах групп по
        # нормали остаётся щепа)
        amin = float(opt.get('min_oblast', '0.002'))
        for _ in range(6):
            ar = np.bincount(lab3, weights=A, minlength=nxt)
            small = set(np.nonzero(ar < amin)[0].tolist())
            if not small: break
            for f0 in range(len(F)):
                if lab3[f0] not in small: continue
                cnt = {}
                for h_ in adj[f0]:
                    if lab3[h_] != lab3[f0]: cnt[lab3[h_]] = cnt.get(lab3[h_], 0) + 1
                if cnt: lab3[f0] = max(cnt, key=cnt.get)
        _, lab3 = np.unique(lab3, return_inverse=True)
        lab = lab3
    elif opt.get('rezhim') == 'kosti' and not kusok:
        # ПО МЫШЦАМ (решение геймдизайнера 21.10 по r12: «крупные чистые грани, рельеф мышц гранями по объёмам»): область —
        # главная кость вершины × участок по длине кости × сектор вокруг её оси (перед / бок / зад / внутрь); у левой стороны
        # сектор зеркален — рельеф симметричен. Мышечных костей в весах К2 нет, поэтому мышца задаётся геометрией скелета
        gn = {g.index: g.name for g in o.vertex_groups}
        Mw = o.matrix_world
        vb = []
        for v in o.data.vertices:
            b = max(v.groups, key=lambda g: g.weight) if v.groups else None
            vb.append(gn[b.group] if b else '')
        Cw = np.array([list(Mw @ __import__('mathutils').Vector(c)) for c in C])
        lab = np.zeros(len(F), dtype=np.int64); key_ = {}
        for fi, f in enumerate(F):
            bn = max(set(vb[v] for v in f), key=lambda n: sum(1 for v in f if vb[v] == n))
            pb = ARM.data.bones.get(bn)
            if bn.split('.')[0] in NE_TROGAT:
                kk = ('свой', fi, 0)     # грань — своя область: не выпрямляется
            elif pb is None:
                kk = (bn, 0, 0)
            else:
                h = np.array(list(AW @ pb.head_local)); t = np.array(list(AW @ pb.tail_local)); ax = t - h; L2 = max(ax @ ax, 1e-9)
                u = float(np.clip((Cw[fi] - h) @ ax / L2, 0, 0.999))
                nt, ns = TAB.get(bn.split('.')[0], TAB['*'])
                r = Cw[fi] - (h + ax * u); a_ = ax / np.sqrt(L2)
                fw = np.array([0, -1.0, 0]); fw = fw - a_ * (fw @ a_)
                if np.linalg.norm(fw) < 1e-6: fw = np.array([0, 0, 1.0]) - a_ * a_[2]
                fw /= np.linalg.norm(fw); sd = np.cross(a_, fw)
                sg = -1.0 if bn.endswith('.L') else 1.0
                ang = (np.arctan2(sg * (r @ sd), r @ fw) + 2 * np.pi) % (2 * np.pi)
                kk = (bn, int(u * nt), int(ang / (2 * np.pi / ns) + 0.5) % ns)
            lab[fi] = key_.setdefault(kk, len(key_))
    else:
        K = max(4, int(A.sum() / float(opt['ploshchad_kuski'] if kusok else opt['ploshchad'])))
        feat = np.hstack([C / float(opt['masshtab']), N])
        cen = feat[rng.choice(len(F), size=min(K, len(F)), replace=False)]
        for _ in range(12):
            d = ((feat[:, None, :] - cen[None]) ** 2).sum(2); lab = d.argmin(1)
            for k in range(len(cen)):
                m = lab == k
                if m.any(): cen[k] = (feat[m] * A[m, None]).sum(0) / A[m].sum()
    P0 = P.copy()
    for _ in range(int(opt['iter'])):
        C, N, A = geom(P)
        # плоскость области: нормаль и точка — средние по площади
        PN, PC = {}, {}
        for k in np.unique(lab):
            m = lab == k; n_ = (N[m] * A[m, None]).sum(0); n_ /= max(np.linalg.norm(n_), 1e-12)
            PN[k] = n_; PC[k] = (C[m] * A[m, None]).sum(0) / A[m].sum()
        T = P.copy()
        for v in range(len(P)):
            if fixed[v] or not vf[v]: continue
            pr = [P[v] - PN[lab[f]] * np.dot(P[v] - PC[lab[f]], PN[lab[f]]) for f in vf[v]]
            T[v] = np.mean(pr, axis=0)
        s = float(opt['sila'])
        L = np.array([P[v] - P[nb[v]].mean(0) if nb[v] else np.zeros(3) for v in range(len(P))])
        L0 = np.array([P0[v] - P0[nb[v]].mean(0) if nb[v] else np.zeros(3) for v in range(len(P))])
        P = np.where(fixed[:, None], P, P + s * (T - P) - float(opt.get('lap', '0.3')) * (L - L0))
    for d_, p in zip(kb.data if kb else o.data.vertices, P): d_.co = p
    dv = np.linalg.norm(P - P0, axis=1)
    tot_r += len(np.unique(lab))
    print('ОГРАНКА: %s — областей %d, вершин %d (край не двигается: %d), сдвиг средн. %.4f, макс %.4f м' % (
        o.name, len(np.unique(lab)), len(P), int(fixed.sum()), dv.mean(), dv.max()))
print('ОГРАНКА: всего областей %d' % tot_r)
bpy.ops.wm.save_as_mainfile(filepath=out)
