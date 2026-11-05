"""КОЛЬЦО ЧЕРЕПА НА ШЕЕ ПОСЛЕ ОГРАНКИ (05.11, Б6). Голова донора забирает объект `голова`, поэтому `PartPieces.UnderKey` ищет вершины кольца на оставшихся объектах: нет их на шее — голова не едет и не растёт за ключом, а ошибки нет (сторож `PartPieces.PieceScale_FollowsSeamUnderKey`). Огранка оставила 12 вершин кольца только в голове, крышка шеи проще. Здесь кольцо заводится на шее: чётные точки — ближайшие вершины крышки (не дальше `--dop`), нечётные: 3 и 9 — внутренняя вершина крышки на своём месте (вынос на хорду соседей выводит стенку крышки из-под челюсти в ключе «Волк»), 1, 5, 7, 11 — ребро делится пополам. Крышка лежит внутри головы. Паспорт: кольцо, центр, эллипс; вершина 0 на верху.

  blender -b сборка.blend --factory-startup --python-exit-code 1 -P kolco_cherepa.py -- выход.blend паспорт.json [--dop 0.012]
"""
import sys, json
import bpy, bmesh
import numpy as np

a = sys.argv[sys.argv.index('--') + 1:]; out, pas = a[:2]; DOP = float(a[a.index('--dop') + 1]) if '--dop' in a else 0.012
o = bpy.data.objects['шея']; d = json.load(open(pas, encoding='utf-8')); s = d['seam']
M = o.matrix_world; Mi = M.inverted()
R0 = np.array([[-u[0], -u[2], u[1]] for u in s['ring_m']])
bm = bmesh.new(); bm.from_mesh(o.data); bm.verts.ensure_lookup_table(); L = list(bm.verts.layers.shape.values())
P = lambda v: np.array(M @ v.co)


def blizh(i):
    v = min(bm.verts, key=lambda q: np.linalg.norm(P(q) - R0[i])); dd = np.linalg.norm(P(v) - R0[i])
    if dd > DOP: sys.exit('КОЛЬЦО: точка %d — ближайшая вершина шеи %.1f мм' % (i, dd * 1000))
    return v


K = {i: blizh(i) for i in (0, 2, 4, 6, 8, 10)}
if len({v.index for v in K.values()}) != 6: sys.exit('КОЛЬЦО: чётные точки совпали')
def deli(a_, b_):
    e = bm.edges.get((K[a_], K[b_]))
    if e is None: sys.exit('КОЛЬЦО: нет ребра %d-%d' % (a_, b_))
    _, nv = bmesh.utils.edge_split(e, K[a_], 0.5); return nv
K[1] = deli(0, 2); K[5] = deli(4, 6); K[7] = deli(6, 8); K[11] = deli(10, 0)
for i, a_, b_ in ((3, 2, 4), (9, 8, 10)):
    ob = {e.other_vert(K[a_]) for e in K[a_].link_edges} & {e.other_vert(K[b_]) for e in K[b_].link_edges}
    v = max(ob, key=lambda q: q.co.z)
    K[i] = v
bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 3])
R1 = np.array([P(K[i]) for i in range(12)]); bm.to_mesh(o.data); bm.free(); o.data.update()
# вершина кольца, совпавшая в базе с вершиной головы, идёт с ней в каждом ключе: иначе голова на родном и на чужом куске видит разный рост кольца (№6 расходилась на 13 мм)
h = bpy.data.objects['голова']; Mn = np.array(M); Mh = h.matrix_world; Hb = np.array([tuple(Mh @ k.co) for k in h.data.shape_keys.key_blocks[0].data]); Ki = o.data.shape_keys.key_blocks; Hk = h.data.shape_keys.key_blocks; n_sv = 0
for i in range(12):
    dd = np.linalg.norm(Hb - R1[i], axis=1); j = int(dd.argmin())
    if dd[j] > 2e-4: continue
    jn = int(np.linalg.norm(np.array([tuple(M @ k.co) for k in Ki[0].data]) - R1[i], axis=1).argmin())
    for kb in Ki:
        if kb.name in Hk and kb.name != Ki[0].name:
            nov = Mi @ (Mh @ Hk[kb.name].data[j].co); n_sv += (nov - kb.data[jn].co).length > 1e-4; kb.data[jn].co = nov
print('КОЛЬЦО: сведено с головой значений ключей %d' % n_sv)
sd = np.array(s.get('side', [1, 0, 0]), float); up = np.array(s['up'], float); Ru = R1 * np.array([-1, 1, 1]); Ru = np.array([[-q[0], q[2], -q[1]] for q in R1]); c = Ru.mean(0)
el = [round(float((Ru - c).dot(sd).max()), 4), round(float((Ru - c).dot(up).max()), 4)]
print('КОЛЬЦО: на шее, сдвиг от прежнего макс %.1f мм; центр %s → %s, эллипс %s → %s' % (np.linalg.norm(R1 - R0, axis=1).max() * 1000, s['center_m'], [round(float(q), 4) for q in c], s['ellipse_m'], el))
s['ring_m'] = [[round(float(-q[0]), 5), round(float(q[2]), 5), round(float(-q[1]), 5)] for q in R1]; s['center_m'] = [round(float(q), 5) for q in c]; s['ellipse_m'] = el
json.dump(d, open(pas, 'w', encoding='utf-8'), ensure_ascii=False, indent=2)
bpy.ops.wm.save_as_mainfile(filepath=out)
