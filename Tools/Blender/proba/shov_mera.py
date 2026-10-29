"""СТУПЕНЬКА НА ШВЕ ЛАПЫ (29.10, критик новой ст.2: «ботфорт — верх куска шире бедра над ним на 10 %»; мера стенда по корзинам длины
кости смешивала участки выше и ниже шва). В покое ключа: шов — открытый край куска `--kusok` на кости `--kost` (и `.L`); радиус до оси
кости — у вершин куска в полосе `--pol` м ниже шва и у вершин тела (форма-ключ `--imya`, главная кость та же) в полосе выше; ступенька
— отношение средних радиусов. Шов — нижние 30 % краевых вершин куска по длине вдоль кости (у косого шва — его ширина по длине).

  blender -b --factory-startup -P shov_mera.py -- вход.blend [--kusok @Волк] [--imya Волк] [--kost бедро] [--pol 0.03] [--poza поза.json]
"""
import sys
import bpy, bmesh
import numpy as np

a = sys.argv[sys.argv.index('--') + 1:]
src = a[0]; opt = dict(kusok='@Волк', imya='Волк', kost='бедро', pol='0.03')
i = 1
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
POL = float(opt['pol'])
bpy.ops.wm.open_mainfile(filepath=src)
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]


import json
from mathutils import Matrix, Vector
SKIN = None
if opt.get('poza'):   # `--poza файл` — мера в позе ступени (скин как в игре и vesa_final.py): шов виден в позе, а в покое его нет
    PZ = {b['name']: b for b in json.load(open(opt['poza'], encoding='utf-8'))['bones']}; SKIN = {}
    for bone in arm.data.bones:
        if bone.name not in PZ: continue
        q = PZ[bone.name]; hh, tt, zz = (Vector(q[k_]) for k_ in ('head', 'tail', 'z_axis'))
        y = (tt - hh).normalized(); x = y.cross(zz).normalized(); zz = x.cross(y)
        rest = arm.matrix_world @ bone.matrix_local
        dd = Matrix((x, y, zz)).transposed() @ rest.to_3x3().inverted()
        SKIN[bone.name] = np.array(Matrix.Translation(hh) @ dd.to_4x4() @ Matrix.Translation(-rest.translation))


def v_pozu(o, P):
    if SKIN is None: return P
    gn = {g.index: g.name for g in o.vertex_groups}; out = np.zeros_like(P)
    for j, v in enumerate(o.data.vertices):
        ws = {gn[g.group]: g.weight for g in v.groups if gn[g.group] in SKIN}; sm = sum(ws.values()) or 1
        m = sum((SKIN[n] * (w / sm) for n, w in ws.items()), np.zeros((4, 4))) if ws else np.eye(4)
        out[j] = (m @ np.r_[P[j], 1])[:3]
    return out


def glav(o):
    gn = {g.index: g.name for g in o.vertex_groups}
    return [gn[max(v.groups, key=lambda g: g.weight).group] if v.groups else '' for v in o.data.vertices]


k = bpy.data.objects[opt['kusok']]; Mk = k.matrix_world; gk = glav(k)
bm = bmesh.new(); bm.from_mesh(k.data); kraj = {v.index for v in bm.verts if any(e.is_boundary for e in v.link_edges)}; bm.free()
for s_ in ('', '.L'):
    kn = opt['kost'] + s_; b = arm.data.bones[kn]
    h = np.array(arm.matrix_world @ b.head_local); t = np.array(arm.matrix_world @ b.tail_local)
    if SKIN is not None: h, t = np.array(PZ[kn]['head']), np.array(PZ[kn]['tail'])
    L = np.linalg.norm(t - h); u = (t - h) / L
    def tr(P): s = (P - h) @ u; R = P - h - np.outer(s, u); return s, np.linalg.norm(R, axis=1), R
    Pk = v_pozu(k, np.array([list(Mk @ v.co) for v in k.data.vertices])); idx = np.array([j for j in range(len(Pk)) if gk[j] == kn])
    ki = np.array(sorted(kraj & set(idx.tolist())))
    if not len(ki): print('ШОВ-МЕРА: %s — края куска нет' % kn); continue
    sk, rk, Rk = tr(Pk[idx]); se, _, Re = tr(Pk[ki])
    # верхний край куска (шов) — нижние 30 % краевых вершин по длине вдоль кости (ниже по ноге — другие открытые края куска)
    s0 = float(np.percentile(se, 15)); s1 = float(np.percentile(se[se <= np.percentile(se, 30)], 100))
    sel_k = (sk > s1) & (sk < s1 + POL)                                  # кусок — ниже шва (дальше по кости)
    T = []
    for o in bpy.data.objects:
        if o.type != 'MESH' or '@' in o.name or not o.data.shape_keys or opt['imya'] not in o.data.shape_keys.key_blocks: continue
        g = glav(o); kb = o.data.shape_keys.key_blocks[opt['imya']]
        Po = v_pozu(o, np.array([list(o.matrix_world @ d.co) for d in kb.data])); T += [list(p_) for p_, gg in zip(Po, g) if gg == kn]
    T = np.array(T); st, rt, Rt = tr(T)
    sel_t = (st < s0) & (st > s0 - POL)                                  # тело — выше шва
    rk_m, rt_m = rk[sel_k].mean(), rt[sel_t].mean()
    print('ШОВ-МЕРА: %s — радиус куска ниже шва %.1f см (%d), тела выше шва %.1f см (%d): ступенька %+.0f %%; шов по кости t %.2f–%.2f' % (
        kn, 100 * rk_m, sel_k.sum(), 100 * rt_m, sel_t.sum(), 100 * (rk_m / rt_m - 1), s0 / L, s1 / L))
