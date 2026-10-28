"""ПОЛОСА КОРНЯ КУСКА — НА ТЕЛО (28.10, скилл `privyazka-poverhnosti`; швы — дизайн-линия FEEDBACK-2026-10-28b-codex-shvy-kuskov;
дыры — детектор окон стенда: ст.2 щель таз ↔ корень хвоста 326×140 мм, просвет под пластиной гривы 52×186 мм; ст.1 окно гребня над
плечом 100×132 мм). Кусок вырезан из цели и лежит в покое ключа своей ступени, но полоса у корня парит над телом — между ней и телом
видно фон. Кусок скинится теми же костями, что тело, поэтому достаточно посадить полосу на тело ключа ЭТОЙ ступени (Shrinkwrap с
затуханием); связь — только в полосе, дальше кусок свободен (хвост 27.10 стянуло на 21 см, когда тянули по всей длине).

  корень — вершины куска (в области `--oblast`) ближе `--koren` к телу; полоса — вершины не дальше `--polosa` от корня;
  вершина полосы садится на ближайшую точку тела (форма-ключ `--imya` шасси, `--otstup` наружу по нормали) с весом
  (1 − d/полоса)², d — расстояние до корня. `--kluch рост` — правится форма-ключ куска (орган на шаблоне), иначе база.
  `--oblast` — фильтр в покое: «hvost» (сзади y > 0.06, ниже z 1.05), «griva» (не голова/кисти/лапы по главной кости и не хвост), «vse».

  blender -b --factory-startup -P privyazat_polosu.py -- вход.blend выход.blend --kusok @Волк --imya Волк --oblast hvost --polosa 0.05
      [--koren 0.015] [--otstup 0.002] [--kluch рост] [--vesa 1 [--vesa_polosa 0.25]]
"""
import sys
import bpy
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from mathutils import kdtree

a = sys.argv[sys.argv.index('--') + 1:]
src, out = a[0], a[1]
opt = dict(kusok='@Волк', imya='Волк', oblast='vse', polosa='0.05', koren='0.015', otstup='0.002', kluch='')
i = 2
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
bpy.ops.wm.open_mainfile(filepath=src)
o = bpy.data.objects[opt['kusok']]; Mw = o.matrix_world; inv = Mw.inverted()
T, TF, TW = [], [], []
for b in bpy.data.objects:
    if b.type != 'MESH' or '@' in b.name: continue
    kb = b.data.shape_keys.key_blocks.get(opt['imya']) if b.data.shape_keys else None
    if kb is None: continue
    gb = {g.index: g.name for g in b.vertex_groups}
    b0 = len(T); T += [b.matrix_world @ d.co for d in kb.data]
    TW += [{gb[g.group]: g.weight for g in v.groups if g.weight > 0} for v in b.data.vertices]
    for p_ in b.data.polygons:
        vv = list(p_.vertices)
        for j in range(1, len(vv) - 1): TF.append([b0 + vv[0], b0 + vv[j], b0 + vv[j + 1]])
BV = BVHTree.FromPolygons(T, TF)
dst = o.data.shape_keys.key_blocks[opt['kluch']].data if opt['kluch'] else o.data.vertices
P = np.array([list(Mw @ d.co) for d in dst])
gn = {g.index: g.name for g in o.vertex_groups}
NT = {'голова', 'предплечье', 'предплечье.L', 'голень', 'голень.L', 'стопа', 'стопа.L'}
glav = [gn[max(v.groups, key=lambda g: g.weight).group] if v.groups else '' for v in o.data.vertices]
B0 = np.array([list(Mw @ v.co) for v in o.data.vertices])       # область — по покою базы


def v_oblasti(k):
    y, z = B0[k][1], B0[k][2]
    hv = y > 0.06 and z < 1.05
    if opt['oblast'] == 'hvost': return hv
    if opt['oblast'] == 'griva': return glav[k] not in NT and not hv
    return True


ob = np.array([v_oblasti(k) for k in range(len(P))])
KO, POL, OT = float(opt['koren']), float(opt['polosa']), float(opt['otstup']); VPOL = float(opt.get('vesa_polosa', '0'))
near = [BV.find_nearest(Vector(p)) for p in P]
koren = [k for k in range(len(P)) if ob[k] and near[k][0] is not None and near[k][3] <= KO]
if not koren: raise SystemExit('ПОЛОСА: корня нет (ни одной вершины ближе %.0f мм к телу в области %s)' % (KO * 1000, opt['oblast']))
kd = kdtree.KDTree(len(koren))
for j, k in enumerate(koren): kd.insert(Vector(P[k]), j)
kd.balance()
n_p, sd = 0, []
for k in range(len(P)):
    if not ob[k] or near[k][0] is None: continue
    d = kd.find(Vector(P[k]))[2]
    if d > max(POL, VPOL): continue
    q, nrm = near[k][0], near[k][1]
    if d <= POL:
        w = (1 - d / POL) ** 2
        tgt = np.array(q + nrm.normalized() * OT); p2 = P[k] + (tgt - P[k]) * w
        sd.append(float(np.linalg.norm(p2 - P[k]))); dst[k].co = inv @ Vector(p2); n_p += 1
    else: w = 0.0
    # `--vesa_polosa м` (28.10c, грива ст.2: «щель под пластиной на загривке» открывается наклоном шеи ступени — пластина дальше
    # полосы держала веса цели): веса кожи шире геометрической полосы, с затуханием 1 − (d/VPOL)⁴, форма там не двигается
    if VPOL > 0: w = max(w, 1 - (d / VPOL) ** 4) if d <= VPOL else w
    # ВЕСА ТЕЛА В ПОЛОСЕ (`--vesa 1`): кусок гнётся вместе с кожей под собой — грива на «хребте» и «лопатке» оставалась на месте,
    # когда шея ступени 2 гнулась вперёд, и на загривке открывалось окно (детектор окон 28.10). Веса ближайшего треугольника тела
    # (барицентрически по точке q), смешанные с весами куска по весу полосы w; не больше 4 костей
    if opt.get('vesa') == '1':
        t_ = TF[near[k][2]]; A_, B_, C_ = (np.array(T[x]) for x in t_); qq = np.array(q)
        v0, v1, v2 = B_ - A_, C_ - A_, qq - A_; d00, d01, d11 = v0 @ v0, v0 @ v1, v1 @ v1; d20, d21 = v2 @ v0, v2 @ v1
        den = d00 * d11 - d01 * d01 or 1e-12; bb = (d11 * d20 - d01 * d21) / den; cc = (d00 * d21 - d01 * d20) / den; aa = 1 - bb - cc
        wt = {}
        for ww, x in ((aa, t_[0]), (bb, t_[1]), (cc, t_[2])):
            for g_, gw in TW[x].items(): wt[g_] = wt.get(g_, 0) + max(ww, 0) * gw
        sv = {gn[g.group]: g.weight for g in o.data.vertices[k].groups}
        mix = {g_: (1 - w) * sv.get(g_, 0) + w * wt.get(g_, 0) for g_ in set(sv) | set(wt)}
        top = sorted(((g_, x) for g_, x in mix.items() if x > 1e-4), key=lambda t: -t[1])[:4]; sm = sum(x for _, x in top) or 1
        for g in list(o.data.vertices[k].groups): o.vertex_groups[g.group].remove([k])
        for g_, x in top: (o.vertex_groups.get(g_) or o.vertex_groups.new(name=g_)).add([k], x / sm, 'REPLACE')
o.data.update()
print('ПОЛОСА: %s (%s%s) — корень %d вершин, в полосе %.0f мм посажено %d (сдвиг средн. %.1f, макс %.1f мм)' % (
    o.name, opt['oblast'], ', ключ ' + opt['kluch'] if opt['kluch'] else '', len(koren), POL * 1000, n_p,
    1000 * float(np.mean(sd)) if sd else 0, 1000 * max(sd, default=0)))
bpy.ops.wm.save_as_mainfile(filepath=out)
