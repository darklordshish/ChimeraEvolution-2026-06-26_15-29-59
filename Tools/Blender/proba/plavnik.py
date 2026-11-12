"""ПЛАВНИК (05.11): насколько подъём руки выше горизонтали лечится весами. Правая рука, Y 110°, лопатка 18°. Варианты весов зоны дельты: как есть; дельта целиком на плече; ступень плечо/лопатка мягче. Меры: растяжение площади граней зоны относительно покоя (макс и сумма), толщина дельты поперёк кости на 8 см от сустава.

  blender -b тело.blend --factory-startup -P plavnik.py
"""
import bpy, math
import numpy as np
from mathutils import Quaternion, Vector
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE'); O = [o for o in bpy.data.objects if o.type == 'MESH' and '@' not in o.name]
for pb in arm.pose.bones: pb.rotation_mode = 'QUATERNION'
REST = {b.name: b.matrix_local.to_quaternion() for b in arm.data.bones}; Y = Vector((0, 1, 0))
def poz(P):
    for pb in arm.pose.bones:
        q = P.get(pb.name, Quaternion()); r = REST[pb.name]; pb.rotation_quaternion = r.inverted() @ q @ r
    bpy.context.view_layer.update()
def snim():
    d = bpy.context.evaluated_depsgraph_get(); R = {}
    for o in O:
        e = o.evaluated_get(d); m = e.to_mesh(); R[o.name] = np.array([tuple(o.matrix_world @ v.co) for v in m.vertices]); e.to_mesh_clear()
    return R
R0 = None
poz({}); R0 = snim(); Zn = {o.name: [p.index for p in o.data.polygons if (R0[o.name][list(p.vertices)][:, 0].mean() < -0.14 and 1.24 < R0[o.name][list(p.vertices)][:, 2].mean() < 1.56)] for o in O}
def plosh(R):
    Rz = {}
    for o in O:
        for i in Zn[o.name]:
            q = R[o.name][list(o.data.polygons[i].vertices)]; Rz[(o.name, i)] = np.linalg.norm(np.cross(q[1] - q[0], q[2] - q[0])) / 2
    return Rz
A0 = plosh(R0)
def mera(naz):
    poz({'плечо': Quaternion(Y, math.radians(110)), 'лопатка': Quaternion(Y, math.radians(18))}); R = snim(); A1 = plosh(R); st = np.array([A1[k] / max(A0[k], 1e-7) for k in A0])
    h = arm.matrix_world @ arm.pose.bones['плечо'].head; t = arm.matrix_world @ arm.pose.bones['плечо'].tail; ax = (t - h).normalized(); c = h + ax * 0.08; c_ = np.array(tuple(c)); a_ = np.array(tuple(ax))
    Rk = R['Руки']; d = (Rk - c_).dot(a_); m = np.abs(d) < 0.025; pr = Rk[m] - np.outer(d[m], a_); r_ = np.linalg.norm(pr - c_, axis=1)
    print('П %s: площадь зоны %.2f к покою, грань макс ×%.1f, граней ×2+ %d из %d; радиус у дельты до %.1f см, вершин %d' % (naz, sum(A1.values()) / sum(A0.values()), st.max(), (st > 2).sum(), len(st), r_.max() * 100, m.sum())); poz({})
mera('как есть')
o_ = bpy.data.objects['Руки']
def nabor(v): return {o_.vertex_groups[x.group].name: x.weight for x in v.groups}
def zapis(v, W):
    s = sum(W.values())
    for x in list(v.groups): o_.vertex_groups[x.group].remove([v.index])
    for n, w in W.items():
        (o_.vertex_groups.get(n) or o_.vertex_groups.new(name=n)).add([v.index], w / s, 'REPLACE')
SNI = {v.index: nabor(v) for v in o_.data.vertices}
def tochki(k_, p):
    for v in o_.data.vertices:
        p_ = R0['Руки'][v.index]
        if p_[0] < -0.17 and 1.3 < p_[2] < 1.56:
            W = dict(SNI[v.index]); s = sum(W.get(n, 0) for n in ('лопатка', 'трапеция', 'хребет', 'шея', 'опора_подмышки')); t_ = k_ * min(1, max(0, (-p_[0] - 0.17) / 0.06))
            for n in ('лопатка', 'трапеция', 'хребет', 'шея', 'опора_подмышки'):
                if n in W: W[n] *= (1 - t_)
            W['плечо'] = W.get('плечо', 0) + s * t_; zapis(v, W)
for k in (0.5, 1.0):
    tochki(k, None); mera('дельта на плече, доля %.1f' % k)
    for v in o_.data.vertices: zapis(v, dict(SNI[v.index]))
