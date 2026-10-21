"""ОГРАНКА ПО ОБЪЁМАМ (21.10, шаг 6 Д консилиума; ГеймБосс по листу r8: «огранка мятая — мелкие треугольники вразнобой, у листа
крупные грани по объёмам»). Форма-ключ после подгонки несёт шум цели: соседние грани смотрят в разные стороны, и плоская
заливка рисует щепу. Огранка собирает грани в области с общей плоскостью и выпрямляет их:

  1. грани объекта делятся на области k-средних по признаку [центр / `--masshtab`, нормаль] (область — связный кусок одной
     ориентации; число областей — площадь объекта / `--ploshchad` м²);
  2. вершина тянется к своим плоскостям (среднее проекций на плоскости областей её граней) с силой `--sila`, а форма держится
     лапласианом — несколько итераций;
  3. край объекта (шов с соседом: шея–голова, рука–корпус) не двигается — куски и паспорта швов садятся как раньше.

Правится только форма-ключ `--imya` (базовая форма К2 — нет).

  blender -b --factory-startup -P ogranka.py -- вход.blend выход.blend [--imya Волк] [--ploshchad 0.006] [--masshtab 0.08] [--sila 0.7] [--iter 6] [--bez голова,Чутьё] [--rezhim kosti] [--kuski 1 [--ploshchad_kuski 0.01]]
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
    if opt.get('rezhim') == 'kosti' and not kusok:
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
