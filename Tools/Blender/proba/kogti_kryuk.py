"""КОГТИ РУК КРЮКАМИ (30.10, критик новой ст.2 n3: «когти рук — прямые спицы парами, лезвия Росомахи; нужны изогнутые крюки на пальцах,
~1/4 длины кисти»). Когти цели (Hunyuan) — тонкие прямые пластины: изгибом их крюком не сделать (проба: перекрещиваются). Шаг в покое:
  1. у куска `--kusok` кисть — вершины с главной костью `предплечье`/`.L` ниже `--z` покоя; грани, у которых вершина дальше квантиля
     `--kvantil` расстояния от запястья (хвост кости), — когти цели, снимаются;
  2. концы пальцев — `--paltsy` кластеров (k-средних) по дальним вершинам оставшейся кисти (дальше квантиля `--konec`);
  3. на каждом конце — крюк: конус из `--segm` колец по 3 вершины, длина `--dlina`·(длина кисти), основание `--tolsh`·длины крюка,
     ось — от запястья через конец пальца, загиб к центру кисти на `--izgib` рад к кончику; веса — с ближайшей вершины пальца.

  blender -b --factory-startup -P kogti_kryuk.py -- вход.blend выход.blend [--kusok @Волк] [--z 0.9] [--kvantil 85] [--konec 70]
      [--paltsy 4] [--dlina 0.25] [--tolsh 0.22] [--izgib 1.0] [--segm 4] [--kost стопа --z 9 --vniz 1 [--po_telu 1 [--imya Волк] [--poza поза.json]]]
"""
import sys, math
import bpy, bmesh
import numpy as np
from mathutils import Vector

a = sys.argv[sys.argv.index('--') + 1:]
src, out = a[0], a[1]
opt = dict(poza='', po_telu='0', imya='Волк', kost='предплечье', vniz='0', kusok='@Волк', z='0.9', kvantil='85', konec='70', paltsy='4', dlina='0.25', tolsh='0.22', izgib='1.0', segm='4')
i = 2
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
bpy.ops.wm.open_mainfile(filepath=src)
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
o = bpy.data.objects[opt['kusok']]; M = o.matrix_world; Mi = M.inverted(); gn = {g.index: g.name for g in o.vertex_groups}
bm = bmesh.new(); bm.from_mesh(o.data); bm.verts.ensure_lookup_table(); dl = bm.verts.layers.deform.verify()


def glav(v):
    d = v[dl]; return gn[max(d.items(), key=lambda kv: kv[1])[0]] if d else ''


# `--poza файл` (стопы: в позе ступени стопа наклонена иначе, чем в покое, — крюк, ограниченный подошвой в покое, в позе уходил под землю):
# точки тела и крюки — в позе (скин как в игре), ограничение «не ниже земли» — в позе, в покой — обратной матрицей кожи под крюком
import json
from mathutils import Matrix
SKIN = None
if opt.get('poza'):
    PZ = {b_['name']: b_ for b_ in json.load(open(opt['poza'], encoding='utf-8'))['bones']}; SKIN = {}
    for bone in arm.data.bones:
        if bone.name not in PZ: continue
        q = PZ[bone.name]; hh, tt, zz = (Vector(q[k_]) for k_ in ('head', 'tail', 'z_axis'))
        y_ = (tt - hh).normalized(); x_ = y_.cross(zz).normalized(); zz = x_.cross(y_)
        rest = arm.matrix_world @ bone.matrix_local
        dd = Matrix((x_, y_, zz)).transposed() @ rest.to_3x3().inverted()
        SKIN[bone.name] = np.array(Matrix.Translation(hh) @ dd.to_4x4() @ Matrix.Translation(-rest.translation))
def skinm(ws):
    sm = sum(w for n, w in ws.items() if n in SKIN) or 1
    return sum((SKIN[n] * (w / sm) for n, w in ws.items() if n in SKIN), np.zeros((4, 4)))


itog, rng = [], np.random.default_rng(30)
for s_ in ('', '.L'):
    # `--kost стопа --z 9 --vniz 1` (30.10, критик n4: «стопы — тапки без когтей; по 4 крюка на пальцах стопы загибом к земле»):
    # опора — голова кости стопы (скакательный), загиб — вниз
    kn = opt['kost'] + s_; b = arm.data.bones[kn]
    wr = np.array(arm.matrix_world @ (b.head_local if opt['kost'] == 'стопа' else b.tail_local))
    if opt['po_telu'] == '1':
        # `--po_telu 1` (30.10: лапы ст.2 рисует тело — кусок лап схлопывался прореживанием): концы пальцев — по вершинам ТЕЛА с формой-
        # ключом `--imya` и главной костью `--kost`; крюки ложатся в кусок, веса — с вершины тела
        P, WS = [], []
        for ob in bpy.data.objects:
            if ob.type != 'MESH' or '@' in ob.name or not ob.data.shape_keys or opt['imya'] not in ob.data.shape_keys.key_blocks: continue
            gb = {g.index: g.name for g in ob.vertex_groups}; kb = ob.data.shape_keys.key_blocks[opt['imya']]
            for v, dd_ in zip(ob.data.vertices, kb.data):
                if not v.groups or gb[max(v.groups, key=lambda g: g.weight).group] != kn: continue
                P.append(list(ob.matrix_world @ dd_.co)); WS.append({gb[g.group]: g.weight for g in v.groups})
        if len(P) < 20: continue
        P = np.array(P)
        if SKIN is not None:   # в позу: точки тела и опора (скакательный в позе)
            P = np.array([(skinm(w_) @ np.r_[p_, 1])[:3] for p_, w_ in zip(P, WS)]); wr = np.array(PZ[kn]['head'])
        d = np.linalg.norm(P - wr, axis=1); ck = P.mean(0); Lk = float(np.percentile(d, 98)); ud = []
        kist = None
    else:
        kist = [v for v in bm.verts if glav(v) == kn and (M @ v.co).z < float(opt['z'])]
        if len(kist) < 20: continue
        P = np.array([list(M @ v.co) for v in kist]); d = np.linalg.norm(P - wr, axis=1)
        thr = np.percentile(d, float(opt['kvantil'])); Lk = float(np.percentile(d, 98))
        # 1. когти цели — прочь
        dal = {v for v, dd in zip(kist, d) if dd > thr}
        ud = list({f for v in dal for f in v.link_faces})
        bmesh.ops.delete(bm, geom=ud, context='FACES'); bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
        bm.verts.ensure_lookup_table()
        kist = [v for v in bm.verts if glav(v) == kn and (M @ v.co).z < float(opt['z'])]
        P = np.array([list(M @ v.co) for v in kist]); d = np.linalg.norm(P - wr, axis=1); ck = P.mean(0)
    # 2. концы пальцев — k-средних по дальним вершинам
    sel = np.nonzero(d > np.percentile(d, float(opt['konec'])))[0]; Q = P[sel]; K = min(int(opt['paltsy']), len(sel))
    C = Q[rng.choice(len(Q), K, replace=False)]
    for _ in range(30):
        lab = np.argmin(np.linalg.norm(Q[:, None] - C[None], axis=2), axis=1)
        C = np.array([Q[lab == k].mean(0) if (lab == k).any() else C[k] for k in range(K)])
    n_k = 0
    for k in range(K):
        if not (lab == k).any(): continue
        grp = sel[lab == k]; tip_i = grp[np.argmax(d[grp])]; t0 = P[tip_i]           # конец пальца — дальняя точка кластера
        ax = t0 - wr; ax /= np.linalg.norm(ax); to_c = ck - t0
        if opt['vniz'] == '1':
            # стопа: коготь — вперёд по горизонтали от конца пальца, загиб вниз
            ax = np.array([ax[0], ax[1], 0.0]); ax /= (np.linalg.norm(ax) or 1); to_c = np.array([0.0, 0.0, -1.0])
        bd = to_c - ax * (to_c @ ax); bd /= (np.linalg.norm(bd) or 1)
        side = np.cross(ax, bd); L = float(opt['dlina']) * Lk; R0 = float(opt['tolsh']) * L; SG = int(opt['segm'])
        if kist is None:
            gi_ = {g.name: g.index for g in o.vertex_groups}
            ws = {}
            for nm_, w_ in WS[tip_i].items():
                if nm_ not in gi_: gi_[nm_] = o.vertex_groups.new(name=nm_).index
                ws[gi_[nm_]] = w_
        else: ws = dict(kist[tip_i][dl])
        # 3. крюк: ось загибается к центру кисти, кольца сужаются к острию
        rings_p, pt, dirv = [], t0 - ax * 0.25 * R0, ax.copy()
        for j in range(SG + 1):
            u = j / SG; r = R0 * (1 - u) + 0.0005
            ring = [pt + r * (math.cos(2 * math.pi * q / 3) * bd + math.sin(2 * math.pi * q / 3) * side) for q in range(3)] if j < SG else [pt]
            rings_p.append(ring)
            th = float(opt["izgib"]) / SG; dirv = math.cos(th) * dirv + math.sin(th) * bd; dirv /= np.linalg.norm(dirv)
            pt = pt + dirv * (L / SG)
        # стопа: крюк не ниже подошвы (иначе фигура вытягивается вниз — IoU падал 0.835 → 0.818); при надобности весь поднимается
        if opt['vniz'] == '1':
            zlo = min(q_[2] for ring in rings_p for q_ in ring); dz_ = max(0.0, float(P[:, 2].min()) - zlo)
            rings_p = [[q_ + np.array([0, 0, dz_]) for q_ in ring] for ring in rings_p]
        if SKIN is not None and kist is None:     # из позы в покой — матрицей кожи конца пальца
            Minv = np.linalg.inv(skinm(WS[tip_i])); rings_p = [[(Minv @ np.r_[q_, 1])[:3] for q_ in ring] for ring in rings_p]
        rings = [[bm.verts.new(Mi @ Vector(p_)) for p_ in ring] for ring in rings_p]
        for ring in rings:
            for v in ring:
                for gi_, w in ws.items(): v[dl][gi_] = w
        for j in range(SG):
            A_, B_ = rings[j], rings[j + 1]
            for q in range(3):
                try:
                    if len(B_) == 1: bm.faces.new((A_[q], A_[(q + 1) % 3], B_[0]))
                    else: bm.faces.new((A_[q], A_[(q + 1) % 3], B_[(q + 1) % 3], B_[q]))
                except ValueError: pass
        try: bm.faces.new(tuple(reversed(rings[0])))
        except ValueError: pass
        n_k += 1
    itog.append('%s: снято граней когтей цели %d, крюков %d (длина %.1f см)' % (kn, len(ud), n_k, 100 * float(opt['dlina']) * Lk))
bm.normal_update(); bm.to_mesh(o.data); bm.free(); o.data.update()
print('КОГТИ-КРЮК: ' + '; '.join(itog))
bpy.ops.wm.save_as_mainfile(filepath=out)
