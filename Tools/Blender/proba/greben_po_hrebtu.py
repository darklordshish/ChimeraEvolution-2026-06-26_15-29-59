"""ГРЕБЕНЬ ПО ХРЕБТУ (29.10, критик новой ст.2 п.4: «сзади на затылке корона, спина голая — читается человек с причёской»; решение
ГеймБосса: процедурно, по виду сзади ортопары r2 — ребро шипов идёт вниз по позвоночнику до ~0.4 спины). У 3D-цели гребня ниже
лопаток нет, вырезать нечего. Шаг в покое ключа `--imya`: по средней линии спины от `--z0` вниз до `--z1` (высоты покоя) с шагом
`--shag` м ставятся шипы-клинья (четыре грани: основание-треугольник на коже и вершина): основание шириной `--shir`·h поперёк
хребта и длиной `--dl`·h вдоль, вершина — над кожей на высоту h по нормали с наклоном `--naklon` градусов к голове (+z покоя);
h затухает от `--h0` до `--h1`. Точка кожи — луч сзади (x 0, +y → −y) на тело ключа (без головы и ушей шасси). Веса шипа —
барицентрически с треугольника кожи под ним (вершина — тем же весом), шип едет со спиной. Шипы добавляются в кусок `--kusok`.

  blender -b --factory-startup -P greben_po_hrebtu.py -- вход.blend выход.blend [--kusok @Волк] [--imya Волк] [--z0 1.36] [--z1 1.15]
      [--shag 0.035] [--h0 0.05] [--h1 0.015] [--shir 0.5] [--dl 0.9] [--naklon 35] [--bez голова,Чутьё] [--poza поза.json --t 0.0:0.45] [--ryad 3 --razbros 0.3] [--put шея:таз]
"""
import sys, math
import bpy, bmesh
import numpy as np
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

a = sys.argv[sys.argv.index('--') + 1:]
src, out = a[0], a[1]
opt = dict(kusok='@Волк', imya='Волк', z0='1.36', z1='1.15', shag='0.035', h0='0.05', h1='0.015', shir='0.5', dl='0.9', naklon='35',
           bez='голова,Чутьё')
i = 2
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
Z0, Z1, SH, H0, H1, SW, DL, NK = (float(opt[k]) for k in ('z0', 'z1', 'shag', 'h0', 'h1', 'shir', 'dl', 'naklon'))
RYAD, r_ = int(opt.get('ryad', '1')), float(opt.get('razbros', '0')); RNG = np.random.default_rng(29)
BEZ = set(opt['bez'].split(','))
bpy.ops.wm.open_mainfile(filepath=src)
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]; KOSTI = {b.name for b in arm.data.bones}
T, TF, TW = [], [], []
for b in bpy.data.objects:
    if b.type != 'MESH' or '@' in b.name or b.name in BEZ or not b.data.shape_keys: continue
    kb = b.data.shape_keys.key_blocks.get(opt['imya'])
    if kb is None: continue
    gb = {g.index: g.name for g in b.vertex_groups}; n0 = len(T)
    T += [b.matrix_world @ d.co for d in kb.data]
    TW += [{gb[g.group]: g.weight for g in v.groups if g.weight > 0 and gb[g.group] in KOSTI} for v in b.data.vertices]
    for p in b.data.polygons:
        vs = list(p.vertices); TF += [[n0 + vs[0], n0 + vs[k], n0 + vs[k + 1]] for k in range(1, len(vs) - 1)]
bvh = BVHTree.FromPolygons(T, TF)


def ves(q, fi):
    A, B, C = (np.array(T[k]) for k in TF[fi]); q = np.array(q)
    v0, v1, v2 = B - A, C - A, q - A; d00, d01, d11, d20, d21 = v0 @ v0, v0 @ v1, v1 @ v1, v2 @ v0, v2 @ v1
    den = d00 * d11 - d01 * d01 or 1e-12; bb = (d11 * d20 - d01 * d21) / den; cc = (d00 * d21 - d01 * d20) / den
    ws = {}
    for k, c in zip(TF[fi], (1 - bb - cc, bb, cc)):
        for n, w in TW[k].items(): ws[n] = ws.get(n, 0.0) + max(c, 0) * w
    top = sorted(ws.items(), key=lambda kv: -kv[1])[:4]; s = sum(w for _, w in top) or 1
    return {n: w / s for n, w in top}


o = bpy.data.objects[opt['kusok']]; Mi = o.matrix_world.inverted()
bm = bmesh.new(); bm.from_mesh(o.data); dl = bm.verts.layers.deform.verify()
gi = {g.name: g.index for g in o.vertex_groups}


def ship(p, n, al, h, ws, M=None, dnk=0.0):
    """шип в точке кожи p (нормаль n, «к голове» al); M — матрица кожи (поза → покой берёт M⁻¹), None — уже в покое"""
    sd = n.cross(al).normalized()
    apex = p + (n * math.cos(math.radians(NK + dnk)) + al * math.sin(math.radians(NK + dnk))) * h
    base = [p + al * (DL * h * 0.5), p - al * (DL * h * 0.5) + sd * (SW * h * 0.5), p - al * (DL * h * 0.5) - sd * (SW * h * 0.5)]
    pts = [q - n * 0.004 for q in base] + [apex]                          # основание чуть под кожей — без щели
    if M is not None:
        Minv = np.linalg.inv(M); pts = [Vector((Minv @ np.r_[list(q), 1])[:3]) for q in pts]
    vv = [bm.verts.new(Mi @ q) for q in pts]
    for v in vv:
        for nm, w in ws.items():
            if nm not in gi: gi[nm] = o.vertex_groups.new(name=nm).index
            v[dl][gi[nm]] = w
    b0, b1, b2, ap = vv
    for f in ((b0, b2, b1), (b0, b1, ap), (b1, b2, ap), (b2, b0, ap)):
        try: bm.faces.new(f)
        except ValueError: pass


n_sh = 0
if opt.get('poza'):
    # В ПОЗЕ СТУПЕНИ (`--poza файл --t 0.0:0.45`): загривок в покое сжат, в позе растянут — где окажется шип, поставленный по
    # высоте покоя, не угадать. Хребет — отрезок «шея:head → таз:head» позы, `--t` — доли этого отрезка; луч к хребту со
    # стороны спины на тело в позе; шип строится в позе и уходит в покой обратной матрицей кожи под ним (как перевязка)
    import json
    PZ = {b['name']: b for b in json.load(open(opt['poza'], encoding='utf-8'))['bones']}
    S = {}
    for bone in arm.data.bones:
        if bone.name not in PZ: continue
        q = PZ[bone.name]; hh, tt, zz = (Vector(q[k]) for k in ('head', 'tail', 'z_axis'))
        y = (tt - hh).normalized(); x = y.cross(zz).normalized(); zz = x.cross(y)
        rest = arm.matrix_world @ bone.matrix_local
        d = Matrix((x, y, zz)).transposed() @ rest.to_3x3().inverted()
        S[bone.name] = np.array(Matrix.Translation(hh) @ d.to_4x4() @ Matrix.Translation(-rest.translation))
    def mat(ws):
        s_ = sum(w for nm, w in ws.items() if nm in S)
        return sum((S[nm] * (w / s_) for nm, w in ws.items() if nm in S), np.zeros((4, 4))) if s_ else np.eye(4)
    TP = [Vector((mat(TW[k]) @ np.r_[list(T[k]), 1])[:3]) for k in range(len(T))]
    bvhp = BVHTree.FromPolygons(TP, TF)
    # `--put шея:голова` — путь по костям (веер на затылке); по умолчанию хребет «шея → таз»
    ka_, kb_ = opt.get('put', 'шея:таз').split(':'); A, Bp = Vector(PZ[ka_]['head']), Vector(PZ[kb_]['head']); s_ = (Bp - A).normalized()
    b = Vector((0, s_.z, -s_.y));  b = b if (b.y + b.z) > 0 else -b       # от живота: назад и вверх
    t0, t1 = (float(v) for v in opt.get('t', '0.0:0.45').split(':')); L = (Bp - A).length
    ts = np.arange(t0, t1 + 1e-6, SH / L)
    for j, t in enumerate(ts):
        c = A + (Bp - A) * t; hit = bvhp.ray_cast(c + b * 1.0, -b)
        if hit[0] is None: continue
        p, n, fi = hit[0], hit[1].normalized(), hit[2]
        if n.dot(b) < 0: n = -n
        al = (-s_ - n * (-s_).dot(n)).normalized()                          # к голове, в касательной
        A_, B_, C_ = (np.array(TP[k]) for k in TF[fi])
        # барицентрические веса — по треугольнику позы
        q = np.array(p); v0, v1, v2 = B_ - A_, C_ - A_, q - A_; d00, d01, d11, d20, d21 = v0 @ v0, v0 @ v1, v1 @ v1, v2 @ v0, v2 @ v1
        den = d00 * d11 - d01 * d01 or 1e-12; bb = (d11 * d20 - d01 * d21) / den; cc = (d00 * d21 - d01 * d20) / den
        ws = {}
        for k, c_ in zip(TF[fi], (1 - bb - cc, bb, cc)):
            for nm, w in TW[k].items(): ws[nm] = ws.get(nm, 0.0) + max(c_, 0) * w
        top = sorted(ws.items(), key=lambda kv: -kv[1])[:4]; sm = sum(w for _, w in top) or 1; ws = {nm: w / sm for nm, w in top}
        h = H0 + (H1 - H0) * (j / max(len(ts) - 1, 1))
        if RYAD <= 1:
            ship(p, n, al, h * (1 - r_ * RNG.random()), ws, mat(ws)); n_sh += 1; continue   # `--razbros` — высота ±, ряд не ровный
        # ПРЯДИ (`--ryad N --razbros r`, критик n2: «ровная пила — спинной плавник; пряди фактуры шеи, назад по росту шерсти, затухают
        # от холки»): в каждой точке N прядей рядом поперёк хребта (шаг `--shir`·h), высота и наклон с разбросом ±r (сид постоянный)
        sd_ = n.cross(al).normalized()
        for q_ in range(RYAD):
            off = (q_ - (RYAD - 1) / 2) * SW * h * 0.9
            hq = h * (1 - r_ * RNG.random()) * (1 - 0.25 * abs(q_ - (RYAD - 1) / 2))
            ship(p + sd_ * off, n, al, hq, ws, mat(ws), dnk=r_ * 30 * (RNG.random() - 0.5)); n_sh += 1
    print('ГРЕБЕНЬ-ХРЕБЕТ: в позе %s, доли хребта %.2f → %.2f (длина %.2f м)' % (opt['poza'].replace(chr(92), '/').split('/')[-1], t0, t1, L))
else:
    zs = np.arange(Z0, Z1 - 1e-6, -SH)
    for j, z in enumerate(zs):
        hit = bvh.ray_cast(Vector((0.0, 1.0, z)), Vector((0.0, -1.0, 0.0)))
        if hit[0] is None: continue
        p, n, fi = hit[0], hit[1].normalized(), hit[2]
        if n.y < 0: n = -n                                                # наружу — к лучу (грань могла прийти изнанкой)
        h = H0 + (H1 - H0) * (j / max(len(zs) - 1, 1))
        up = Vector((0, 0, 1)); al = (up - n * up.dot(n)).normalized()   # вдоль хребта к голове, в касательной плоскости
        ship(p, n, al, h, ves(p, fi)); n_sh += 1
bm.normal_update(); bm.to_mesh(o.data); bm.free(); o.data.update()
print('ГРЕБЕНЬ-ХРЕБЕТ: %s — шипов %d по хребту z %.2f → %.2f, шаг %.3f, высота %.3f → %.3f м' % (o.name, n_sh, Z0, Z1, SH, H0, H1))
bpy.ops.wm.save_as_mainfile(filepath=out)
