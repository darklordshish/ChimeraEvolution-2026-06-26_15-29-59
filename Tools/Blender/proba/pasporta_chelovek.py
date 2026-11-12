"""ПАСПОРТА ЧЕЛОВЕКА НА НОВОЙ БАЗЕ (03.11, поставка поджарого, `Docs/models/PERESCHET-POSTAVKI-chelovek.md`). Положение швов берётся с меша:
  основание уха (`parts/chelovek-ushi.json`) — открытый край объекта `Чутьё` на стороне −X Unity: настоящие вершины, 8 штук, вершина 0 — на «верху», центр и эллипс — от них (прежний паспорт нёс идеальный эллипс: 16 мм до вершин, сварка молчала);
  корень хвоста — центр на коже спины на прежней высоте, кольцо — эллипс места, спроецированный на кожу;
  основание рога — `mesto_rogov.py`, основание черепа — `pasport_shva.py`: отдельно.
Правится на месте, только поля шва и счётчик граней. Тело — база файла.

  blender -b сборка.blend --factory-startup --python-exit-code 1 -P pasporta_chelovek.py -- папка_handoff
"""
import sys, json, math
import bpy, bmesh
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

H = sys.argv[sys.argv.index('--') + 1].rstrip('/') + '/'
U = lambda q: [round(float(-q[0]), 5), round(float(q[2]), 5), round(float(-q[1]), 5)]
Bk = lambda u: Vector((-u[0], -u[2], u[1]))
V, F = [], []
for o in bpy.data.objects:
    if o.type != 'MESH' or '@' in o.name: continue
    n0 = len(V); V += [o.matrix_world @ v.co for v in o.data.vertices]; o.data.calc_loop_triangles(); F += [[n0 + q for q in t.vertices] for t in o.data.loop_triangles]
bvh = BVHTree.FromPolygons(V, F, all_triangles=True)


def rd(f): return json.load(open(H + f, encoding='utf-8'))
def wr(f, d): json.dump(d, open(H + f, 'w', encoding='utf-8'), ensure_ascii=False, indent=2)


# УШИ
o = bpy.data.objects['Чутьё']; bm = bmesh.new(); bm.from_mesh(o.data)
kr = [e for e in bm.edges if e.is_boundary and all((o.matrix_world @ v.co).x > 0 for v in e.verts)]
adj = {}
for e in kr:
    for v in e.verts: adj.setdefault(v, []).append(e)
st = kr[0].verts[0]; loop = [st]; prev = None; cur = st
while True:
    nx = next((e.other_vert(cur) for e in adj[cur] if e.other_vert(cur) is not prev), None)
    if nx is None or nx is st: break
    loop.append(nx); prev, cur = cur, nx
if len(loop) != 8 or len(kr) != 8: sys.exit('ПАСПОРТА: край уха не 8: обход %d, рёбер %d' % (len(loop), len(kr)))
Pu = [o.matrix_world @ v.co for v in loop]; bm.free()
d = rd('parts/chelovek-ushi.json'); s = d['seam']; up = Bk(s['up']); side = Bk(s['side'])
i0 = max(range(8), key=lambda k: (Pu[k] - sum(Pu, Vector()) / 8).dot(up)); Pu = Pu[i0:] + Pu[:i0]
c = sum(Pu, Vector()) / 8
if (Pu[1] - c).dot(side) < 0: Pu = [Pu[0]] + Pu[:0:-1]
s['ring_m'] = [U(p) for p in Pu]; s['center_m'] = U(c); s['ellipse_m'] = [round(max((p - c).dot(side) for p in Pu), 4), round(max((p - c).dot(up) for p in Pu), 4)]
DOP_ = '; с 03.11 ring_m — настоящие вершины края объекта Чутьё (сварка ищет кольцо по близости 2 мм)'
s['note'] = s.get('note', '').split(DOP_)[0] + DOP_   # дописка одна: прежде каждая сборка добавляла её заново
wr('parts/chelovek-ushi.json', d); print('ПАСПОРТА: уши — центр %s, эллипс %s' % (s['center_m'], s['ellipse_m']))
# КОРЕНЬ ХВОСТА
d = rd('chelovek-koren-hvosta.json'); s = d['seam']; ax = Bk(s['axis_out']); up = Bk(s['up']); c0 = Bk(s['center_m'])
h = bvh.ray_cast(c0 + ax * 0.2, -ax); c = h[0]; xu = Vector((1, 0, 0)); R = []
for k in range(8):
    p = c + xu * (s['ellipse_m'][0] * math.sin(math.pi / 4 * k)) + up * (s['ellipse_m'][1] * math.cos(math.pi / 4 * k)); R.append(bvh.find_nearest(p)[0])
print('ПАСПОРТА: корень хвоста — центр сдвинут на %.1f мм' % ((c - c0).length * 1000))
s['center_m'] = U(c); s['ring_m'] = [U(p) for p in R]; wr('chelovek-koren-hvosta.json', d)
# ОСНОВАНИЕ МОРДЫ не пересчитывается здесь: центр — от хорды «переносица → подбородок», а не от кожи (луч попадает в кончик носа: сдвиг 35 мм, неверно). Лицо сдвинулось меньше 1.5 см, паспорт прежний.
