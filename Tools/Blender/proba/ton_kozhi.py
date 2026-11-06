"""РИСУНОК КОЖИ ОРГАНА ГРАНЯМИ (06.11, спека `2026-11-06-shkura-obrabotka-kozhi.md`, вариант А). Объекты кожи шасси (`--obj`) копируются целиком со всеми формами-ключами и весами, копия режется косой сеткой ромбов в области органа (спина, шея, плечи: нормаль назад, `--z`), грани раскладываются по тонам: полоса вдоль линий сетки — кромка (тёмный тон), середина ромба — светлый, вне области — тон 1.0 (как тело). Каждый тон — объект `<слот>.тон<N>@<вид>` со своим материалом: цвет материала — множитель к цвету тела. Орган снимает исходные объекты записью в `organy/<шасси>.json`, поэтому копии обязаны покрыть их полностью. Накладки этого органа (`--snyat`) удаляются.

  blender -b сборка.blend --factory-startup --python-exit-code 1 -P ton_kozhi.py -- выход.blend Шкура Змея [--obj хребет,шея,Сердце] [--shag 0.10] [--kromka 0.012] [--z 1.06:1.66] [--tony 1.0,0.86,0.70] [--snyat Шкура.чешуя@Змея,Шкура.край@Змея]
"""
import sys, math
import bpy, bmesh
from mathutils import Vector

a = sys.argv[sys.argv.index('--') + 1:]; OUT, SL, VID = a[:3]; opt = dict(obj='хребет,шея,Сердце', shag='0.10', kromka='0.012', z='1.06:1.66', tony='1.0,0.86,0.70', ug='0.62'); i = 3
while i < len(a): opt[a[i].lstrip('-')] = a[i + 1]; i += 2
SH, KR, UG = float(opt['shag']), float(opt['kromka']), float(opt['ug']); Z0, Z1 = map(float, opt['z'].split(':')); TONY = [float(x) for x in opt['tony'].split(',')]
N1, N2 = Vector((math.sin(UG), 0, math.cos(UG))), Vector((-math.sin(UG), 0, math.cos(UG)))
for n in filter(None, opt.get('snyat', '').split(',')):
    if bpy.data.objects.get(n): bpy.data.objects.remove(bpy.data.objects[n], do_unlink=True)
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
mats = []
for k, t in enumerate(TONY):
    m = bpy.data.materials.new('тон%d' % (k + 1)); c = (t, t, t, 1); m.diffuse_color = c; m.use_nodes = True; m.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = c; mats.append(m)
v_obl = lambda c, n: Z0 < c.z < Z1 and n.y > .2
dist = lambda c, N: abs((N.dot(c) / SH) - round(N.dot(c) / SH)) * SH
itog = {}; SBOR = {}
for nm in opt['obj'].split(','):
    src = bpy.data.objects[nm]; M = src.matrix_world; Mi = M.inverted()
    bm = bmesh.new(); bm.from_mesh(src.data); bm.normal_update()
    # резка: линии сетки и полосы кромок по обе стороны, только в области
    obl = [f for f in bm.faces if v_obl(M @ f.calc_center_median(), (M.to_3x3() @ f.normal).normalized())]
    geom = list({v for f in obl for v in f.verts}) + list({e for f in obl for e in f.edges}) + obl
    for N in (N1, N2):
        for k in range(-30, 31):
            for d in (-KR, 0.0, KR):
                co = Mi @ (N * (k * SH + d)); no = (Mi.to_3x3() @ N).normalized()
                r = bmesh.ops.bisect_plane(bm, geom=[g for g in geom if g.is_valid], dist=1e-6, plane_co=co, plane_no=no)
                geom = [g for g in geom if g.is_valid] + r['geom_cut']
                geom = list({g for g in geom if g.is_valid} | {f for g in r['geom_cut'] if isinstance(g, bmesh.types.BMEdge) for f in g.link_faces})
    bmesh.ops.triangulate(bm, faces=bm.faces[:]); bm.normal_update()
    TL = bm.faces.layers.int.new('тон')
    for f in bm.faces:
        c = M @ f.calc_center_median(); n = (M.to_3x3() @ f.normal).normalized()
        if not v_obl(c, n): f[TL] = 0; continue
        f[TL] = 2 if min(dist(c, N1), dist(c, N2)) < KR else 1
    SK = [L for L in bm.verts.layers.shape.values()]; DF = bm.verts.layers.deform.verify(); gn = {g.index: g.name for g in src.vertex_groups}
    for f in bm.faces:   # копится по тонам в мировых координатах: веса по именам костей, ключи по именам
        T = SBOR.setdefault(f[TL], dict(V=[], F=[], W=[], K={}, ix={}))
        q = []
        for v in f.verts:
            key = (nm, v.index)
            if key not in T['ix']:
                T['ix'][key] = len(T['V']); T['V'].append(M @ v.co); T['W'].append({gn[g]: w for g, w in v[DF].items() if g in gn})
                for L in SK:
                    T['K'].setdefault(L.name, {})[T['ix'][key]] = M @ v[L]
            q.append(T['ix'][key])
        T['F'].append(q)
    bm.free()
for k, T in sorted(SBOR.items()):
    on = '%s.тон%d@%s' % (SL, k + 1, VID); st = bpy.data.objects.get(on)
    if st: bpy.data.objects.remove(st, do_unlink=True)
    me = bpy.data.meshes.new(on); me.from_pydata([tuple(v) for v in T['V']], [], T['F']); me.update(); me.materials.append(mats[k])
    ob = bpy.data.objects.new(on, me); bpy.context.scene.collection.objects.link(ob); ob.parent = arm; m_ = ob.modifiers.new('ск', 'ARMATURE'); m_.object = arm
    kosti = {b.name for b in arm.data.bones}
    for j, w in enumerate(T['W']):   # резка смешала веса соседей: не больше 4 костей, сумма 1 (импорт Unity режет до 4); служебные группы как есть
        kw = sorted(((n_, x) for n_, x in w.items() if n_ in kosti and x > 0), key=lambda q: -q[1])[:4]; s_ = sum(x for _, x in kw) or 1
        w = {**{n_: x for n_, x in w.items() if n_ not in kosti}, **{n_: x / s_ for n_, x in kw}}
        for n_, x in w.items(): (ob.vertex_groups.get(n_) or ob.vertex_groups.new(name=n_)).add([j], x, 'REPLACE')
    imena = list(T['K'].keys()); baz = imena[0] if imena else None
    if baz:
        ob.shape_key_add(name=baz)
        for kn in imena[1:]:
            kb = ob.shape_key_add(name=kn, from_mix=False); D = T['K'][kn]; B0 = T['K'][baz]
            for j in range(len(T['V'])): kb.data[j].co = D.get(j, B0.get(j, T['V'][j]))
for o in bpy.data.objects:
    if '.тон' in o.name:
        for p in o.data.polygons: p.use_smooth = False
        o.data.calc_loop_triangles(); itog[o.name] = len(o.data.loop_triangles)
print('ТОН %s@%s: %s, всего %d тр' % (SL, VID, itog, sum(itog.values())))
bpy.ops.wm.save_as_mainfile(filepath=OUT)
