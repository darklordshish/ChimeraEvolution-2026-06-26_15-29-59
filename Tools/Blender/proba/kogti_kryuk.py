"""КОГТИ РУК КРЮКАМИ (30.10, критик новой ст.2 n3: «когти рук — прямые спицы парами, лезвия Росомахи; нужны изогнутые крюки на пальцах,
~1/4 длины кисти»). Когти цели (Hunyuan) — тонкие прямые пластины: изгибом их крюком не сделать (проба: перекрещиваются). Шаг в покое:
  1. у куска `--kusok` кисть — вершины с главной костью `предплечье`/`.L` ниже `--z` покоя; грани, у которых вершина дальше квантиля
     `--kvantil` расстояния от запястья (хвост кости), — когти цели, снимаются;
  2. концы пальцев — `--paltsy` кластеров (k-средних) по дальним вершинам оставшейся кисти (дальше квантиля `--konec`);
  3. на каждом конце — крюк: конус из `--segm` колец по 3 вершины, длина `--dlina`·(длина кисти), основание `--tolsh`·длины крюка,
     ось — от запястья через конец пальца, загиб к центру кисти на `--izgib` рад к кончику; веса — с ближайшей вершины пальца.

  blender -b --factory-startup -P kogti_kryuk.py -- вход.blend выход.blend [--kusok @Волк] [--z 0.9] [--kvantil 85] [--konec 70]
      [--paltsy 4] [--dlina 0.25] [--tolsh 0.22] [--izgib 1.0] [--segm 4]
"""
import sys, math
import bpy, bmesh
import numpy as np
from mathutils import Vector

a = sys.argv[sys.argv.index('--') + 1:]
src, out = a[0], a[1]
opt = dict(kusok='@Волк', z='0.9', kvantil='85', konec='70', paltsy='4', dlina='0.25', tolsh='0.22', izgib='1.0', segm='4')
i = 2
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
bpy.ops.wm.open_mainfile(filepath=src)
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
o = bpy.data.objects[opt['kusok']]; M = o.matrix_world; Mi = M.inverted(); gn = {g.index: g.name for g in o.vertex_groups}
bm = bmesh.new(); bm.from_mesh(o.data); bm.verts.ensure_lookup_table(); dl = bm.verts.layers.deform.verify()


def glav(v):
    d = v[dl]; return gn[max(d.items(), key=lambda kv: kv[1])[0]] if d else ''


itog, rng = [], np.random.default_rng(30)
for s_ in ('', '.L'):
    kn = 'предплечье' + s_; b = arm.data.bones[kn]; wr = np.array(arm.matrix_world @ b.tail_local)
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
        ax = t0 - wr; ax /= np.linalg.norm(ax); to_c = ck - t0; bd = to_c - ax * (to_c @ ax); bd /= (np.linalg.norm(bd) or 1)
        side = np.cross(ax, bd); L = float(opt['dlina']) * Lk; R0 = float(opt['tolsh']) * L; SG = int(opt['segm'])
        ws = dict(kist[tip_i][dl])
        # 3. крюк: ось загибается к центру кисти, кольца сужаются к острию
        rings, pt, dirv = [], t0 - ax * 0.25 * R0, ax.copy()
        for j in range(SG + 1):
            u = j / SG; r = R0 * (1 - u) + 0.0005
            ring = [pt + r * (math.cos(2 * math.pi * q / 3) * bd + math.sin(2 * math.pi * q / 3) * side) for q in range(3)] if j < SG else [pt]
            rings.append([bm.verts.new(Mi @ Vector(p_)) for p_ in ring])
            th = float(opt["izgib"]) / SG; dirv = math.cos(th) * dirv + math.sin(th) * bd; dirv /= np.linalg.norm(dirv)
            pt = pt + dirv * (L / SG)
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
