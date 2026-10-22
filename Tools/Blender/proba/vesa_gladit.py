# СГЛАДИТЬ ВЕСА КОСТЕЙ В ПОЛОСЕ ТОРСА (22.10, критик ряда: «тёмный пояс» ст.2): кант под грудью в позе ступени — складка скиннинга
# (грудь и бок под рукой на разных костях с резкой границей весов), а не формы ключа; веса усредняются с соседями в полосе базовых
# высот z0..z1 впереди y<ymax на всех объектах шасси, затем совпадающим вершинам швов — общие веса (до 4 костей)
#   blender -b --factory-startup -P vesa_gladit.py -- вход.blend выход.blend z0 z1 ymax шагов
import bpy, sys
a = sys.argv[sys.argv.index('--') + 1:]
src, out, z0, z1, ymax, n = a[0], a[1], float(a[2]), float(a[3]), float(a[4]), int(a[5])
bpy.ops.wm.open_mainfile(filepath=src)
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]; bones = {b.name for b in arm.data.bones}
tot = 0
for o in [x for x in bpy.data.objects if x.type == 'MESH' and '@' not in x.name]:
    gn = {g.index: g.name for g in o.vertex_groups}
    W = [{gn[g.group]: g.weight for g in v.groups if gn[g.group] in bones} for v in o.data.vertices]
    nb = [[] for _ in o.data.vertices]
    for e in o.data.edges: nb[e.vertices[0]].append(e.vertices[1]); nb[e.vertices[1]].append(e.vertices[0])
    band = [i for i, v in enumerate(o.data.vertices) if z0 < (o.matrix_world @ v.co).z < z1 and (o.matrix_world @ v.co).y < ymax and not any(len(nb[i]) == 0 for _ in [0])]
    bs = set(band)
    edge_v = set()
    for e in o.data.edges:
        pass
    for _ in range(n):
        Wn = list(W)
        for i in band:
            acc = dict(W[i]); c = 1.0
            for j in nb[i]:
                for k, w in W[j].items(): acc[k] = acc.get(k, 0) + w
                c += 1
            acc = {k: w / c for k, w in acc.items()}
            top = sorted(acc.items(), key=lambda kv: -kv[1])[:4]; s = sum(w for _, w in top)
            Wn[i] = {k: w / s for k, w in top}
        W = Wn
    for i in band:
        for g in list(o.data.vertices[i].groups):
            if gn[g.group] in bones: o.vertex_groups[g.group].remove([i])
        for k, w in W[i].items():
            g = o.vertex_groups.get(k) or o.vertex_groups.new(name=k); g.add([i], w, 'REPLACE')
    tot += len(band)
# швы: совпадающие вершины разных объектов — общие веса (среднее, затем 4 старших)
pos = {}
objs = [x for x in bpy.data.objects if x.type == 'MESH' and '@' not in x.name]
for o in objs:
    gn = {g.index: g.name for g in o.vertex_groups}
    for v in o.data.vertices:
        w_ = o.matrix_world @ v.co; k = (round(w_.x, 4), round(w_.y, 4), round(w_.z, 4))
        pos.setdefault(k, []).append((o, v.index, {gn[g.group]: g.weight for g in v.groups if gn[g.group] in bones}))
ns = 0
for k, lst in pos.items():
    if len(lst) < 2: continue
    acc = {}
    for _, _, ws in lst:
        for b, w in ws.items(): acc[b] = acc.get(b, 0) + w / len(lst)
    top = sorted(acc.items(), key=lambda kv: -kv[1])[:4]; sm = sum(w for _, w in top)
    for o, i, ws in lst:
        gn = {g.index: g.name for g in o.vertex_groups}
        for g in list(o.data.vertices[i].groups):
            if gn[g.group] in bones: o.vertex_groups[g.group].remove([i])
        for b, w in top:
            g = o.vertex_groups.get(b) or o.vertex_groups.new(name=b); g.add([i], w / sm, 'REPLACE')
    ns += 1
print('ВЕСА-ГЛАДЬ: швов %d' % ns)
print('ВЕСА-ГЛАДЬ: вершин в полосе %d, шагов %d' % (tot, n))
bpy.ops.wm.save_as_mainfile(filepath=out)
