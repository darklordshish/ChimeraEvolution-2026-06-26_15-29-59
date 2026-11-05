"""КУСКИ КЛЮЧЕЙ ПО ЗАМЕЧАНИЯМ КРИТИКА (05.11, Б8; образцы `витрина/оборотень/ступени.png`, `тело-ст2.png`, `человек-лось/ступени.png`, `тело.png`). Черты @Волк1 и @Лось вырезаны из цели и в поставке шли как есть. Здесь правятся по островам, в покое рига.
@Волк1: хвост заменён цельным султаном (сечения звездой, кость таз) и отведён от тела; грива уже и прорежена в крупные грани; проём между ушами закрыт; передние кисти меньше.
@Лось: голова ниже и вперёд, шея тянется за ней; рога развёрнуты наружу, размах прежний.

  blender -b сборка.blend --factory-startup --python-exit-code 1 -P kuski_klyuchej.py -- выход.blend [--hvost 35] [--griva 0.67] [--griva_dec 0.2] [--kist 0.83] [--los_y 0.05] [--los_z 0.04] [--rog 20] [--pasport_v st1.json] [--pasport_l st2.json]
"""
import sys, math, json
import bpy, bmesh
import numpy as np
from mathutils import Vector, Matrix

a = sys.argv[sys.argv.index('--') + 1:]; OUT = a[0]; opt = dict(hvost='35', griva='0.67', griva_dec='0.2', kist='0.83', los_y='0.05', los_z='0.04', rog='20'); i = 1
while i < len(a): opt[a[i].lstrip('-')] = a[i + 1]; i += 2


def ostrova(bm):
    seen = set(); R = []
    for v in bm.verts:
        if v in seen: continue
        st = [v]; seen.add(v); L = []
        while st:
            u = st.pop(); L.append(u)
            for e in u.link_edges:
                q = e.other_vert(u)
                if q not in seen: seen.add(q); st.append(q)
        R.append(L)
    return R


def petli_kraya(vs):
    be = {e for v in vs for e in v.link_edges if e.is_boundary}; nb = {}
    for e in be:
        for v in e.verts: nb.setdefault(v, []).append(e)
    R = []; usl = set()
    for e0 in be:
        if e0 in usl: continue
        L = []; st = [e0]
        while st:
            e = st.pop()
            if e in usl: continue
            usl.add(e); L.append(e)
            for v in e.verts: st += [q for q in nb[v] if q not in usl]
        R.append(L)
    return R


# ── @Волк1 ──
o = bpy.data.objects['@Волк1']; M = o.matrix_world; Mi = M.inverted(); P = lambda v: M @ v.co; bm = bmesh.new(); bm.from_mesh(o.data); bm.verts.ensure_lookup_table(); dfl = bm.verts.layers.deform.verify(); gi = {g.name: g.index for g in o.vertex_groups}; n0 = len(bm.faces)
Is = ostrova(bm); ob = lambda L: (np.array([tuple(P(v)) for v in L]))
# проёмы черепа между ушами
gol = [L for L in Is if len(L) > 100 and ob(L)[:, 2].max() > 1.9 and ob(L)[:, 2].min() > 1.4]
n_zak = 0
for L in gol:
    for pet in petli_kraya(L):
        c = sum((P(v) for e in pet for v in e.verts), Vector()) / (2 * len(pet))
        if c.z > 1.8 and len(pet) >= 3:
            r_ = bmesh.ops.holes_fill(bm, edges=pet, sides=0); n_zak += len(r_.get('faces', []))
print('ВОЛК: проёмов между ушами закрыто граней %d' % n_zak)
griva = max(gol, key=lambda L: ob(L)[:, 0].max()); G_ = float(opt['griva'])
for v in griva:
    p = P(v); p.x *= G_; v.co = Mi @ p
# хвост: остров целиком заменяется
hv = max([L for L in Is if ob(L)[:, 1].max() > 0.5], key=len); A = ob(hv); R0 = Vector(A[A[:, 2] > A[:, 2].max() - 0.05].mean(0)); T0 = Vector(A[A[:, 1] > A[:, 1].max() - 0.05].mean(0)); dl = (T0 - R0).length; d0 = (T0 - R0).normalized()
ug = math.radians(float(opt['hvost'])); dn = (Matrix.Rotation(-ug, 3, 'X') @ d0).normalized()
if dn.z < d0.z - 0.5: dn = (Matrix.Rotation(ug, 3, 'X') @ d0).normalized()
if dn.z < d0.z: dn = (Matrix.Rotation(ug, 3, 'X') @ d0).normalized()
bmesh.ops.delete(bm, geom=hv, context='VERTS')
ver_ = Vector((1, 0, 0)).cross(dn).normalized(); bok = Vector((1, 0, 0))
# начало уходит в таз: иначе под ключом щель у корня
Sc = [(-.24, .03), (-.05, .055), (.14, .085), (.38, .12), (.62, .11), (.84, .06)]; NR = 6; Kol = []
tb = gi['таз']
for t, r in Sc:
    c = R0 + dn * (t * dl); K = []
    for j in range(NR):
        an = 2 * math.pi * j / NR; rr = r * (1.0 if j % 2 == 0 else 0.72)
        v = bm.verts.new(Mi @ (c + bok * (rr * math.cos(an)) + ver_ * (rr * .8 * math.sin(an)))); v[dfl][tb] = 1.0; K.append(v)
    Kol.append(K)
kon = bm.verts.new(Mi @ (R0 + dn * dl)); kon[dfl][tb] = 1.0; zad = bm.verts.new(Mi @ (R0 - dn * (.27 * dl))); zad[dfl][tb] = 1.0
nov_f = []
for K0, K1 in zip(Kol, Kol[1:]):
    for j in range(NR):
        k = (j + 1) % NR; nov_f.append(bm.faces.new((K0[j], K0[k], K1[k], K1[j])))
for j in range(NR):
    k = (j + 1) % NR; nov_f.append(bm.faces.new((Kol[-1][j], Kol[-1][k], kon))); nov_f.append(bm.faces.new((Kol[0][k], Kol[0][j], zad)))
bmesh.ops.recalc_face_normals(bm, faces=nov_f)
if sum(f.normal.dot(f.calc_center_median() - (Mi @ (R0 + dn * (dl * .4)))) for f in nov_f) < 0:
    for f in nov_f: f.normal_flip()
bmesh.ops.triangulate(bm, faces=nov_f)
print('ВОЛК: хвост %d вершин → султан %d граней, направление покоя %s → %s, длина %.2f' % (len(hv), len(nov_f) * 2 - 2 * NR, np.round(tuple(d0), 2), np.round(tuple(dn), 2), dl))
# кисти
K_ = float(opt['kist']); n_k = 0
for L in Is:
    A = ob(L) if all(v.is_valid for v in L) else None
    if A is None or len(L) < 100 or not (0.45 < A[:, 2].min() < 0.6 and A[:, 2].max() < 1.0 and np.abs(A[:, 0]).min() > 0.15): continue
    c0 = Vector(A[A[:, 2] > A[:, 2].max() - 0.03].mean(0))
    for v in L: p = P(v); v.co = Mi @ (c0 + (p - c0) * K_)
    n_k += 1
print('ВОЛК: кистей уменьшено %d' % n_k)
bm.normal_update(); bm.to_mesh(o.data); o.data.update(); bm.free()
# грива: прореживание по выбранным вершинам
bpy.ops.object.select_all(action='DESELECT'); bpy.context.view_layer.objects.active = o; o.select_set(True)
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='DESELECT'); bme = bmesh.from_edit_mesh(o.data); ch = 0
vs_g = [v for v in bme.verts if (M @ v.co).z > 1.26 and abs((M @ v.co).x) > 0.0 and (M @ v.co).y > -0.42]
bme.verts.ensure_lookup_table(); Is2 = ostrova(bme); gr2 = max([L for L in Is2 if (M @ L[0].co).z > 1.2 and len(L) > 100], key=lambda L: max(abs((M @ v.co).x) for v in L))
for v in gr2: v.select_set(True)
for f in bme.faces: f.select_set(all(v.select for v in f.verts))
bme.select_flush(True); bmesh.update_edit_mesh(o.data); bpy.ops.mesh.decimate(ratio=float(opt['griva_dec'])); bpy.ops.object.mode_set(mode='OBJECT')
o.data.calc_loop_triangles(); print('ВОЛК: граней куска %d → %d' % (n0, len(o.data.loop_triangles)))
if opt.get('pasport_v'):
    d = json.load(open(opt['pasport_v'], encoding='utf-8')); d['tris'] = len(o.data.loop_triangles); json.dump(d, open(opt['pasport_v'], 'w', encoding='utf-8'), ensure_ascii=False, indent=2)

# ── @Лось ──
o = bpy.data.objects['@Лось']; M = o.matrix_world; Mi = M.inverted(); g2 = {g.index: g.name for g in o.vertex_groups}; Dy, Dz = float(opt['los_y']), float(opt['los_z']); Ug = math.radians(float(opt['rog']))
Vp = [M @ v.co for v in o.data.vertices]; golv = [i_ for i_, p in enumerate(Vp) if p.z > 1.3]
Xm = max(abs(Vp[i_].x) for i_ in golv); n_r = 0
for i_ in golv:
    v = o.data.vertices[i_]; p = Vp[i_].copy(); w = {g2[g.group]: g.weight for g in v.groups}; wg = w.get('голова', 0) / max(sum(w.values()), 1e-6)
    if abs(p.x) > 0.13 and p.z > 1.72:   # рог: поворот вокруг оси вперёд-назад от основания, размах возвращается
        sx = 1 if p.x > 0 else -1; c0 = Vector((.13 * sx, p.y, 1.80)); q = p - c0; a_ = -Ug * sx
        p = c0 + Vector((q.x * math.cos(a_) + q.z * math.sin(a_), q.y, -q.x * math.sin(a_) + q.z * math.cos(a_))); n_r += 1
    p.y -= Dy * wg; p.z -= Dz * wg; v.co = Mi @ p
Vn = [M @ v.co for v in o.data.vertices]; Xn = max(abs(p.x) for p in Vn); k_ = Xm / Xn
for v in o.data.vertices:
    p = M @ v.co
    if abs(p.x) > 0.13 and p.z > 1.6: p.x = math.copysign(.13 + (abs(p.x) - .13) * (Xm - .13) / (Xn - .13), p.x); v.co = Mi @ p
o.data.update(); print('ЛОСЬ: голова −y %.3f −z %.3f, рог поворот %.0f°, вершин рога %d, размах %.3f → %.3f → %.3f' % (Dy, Dz, math.degrees(Ug), n_r, Xm, Xn, max(abs((M @ v.co).x) for v in o.data.vertices)))
bpy.ops.wm.save_as_mainfile(filepath=OUT)
