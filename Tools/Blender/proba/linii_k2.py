"""ЛИНИИ ПЕРЕЛОМА НА ШАБЛОНЕ К2 (27.10, пункт Г разбора тракта; лист дизайн-линии `Референсы/работа/дизайн/linii-pereloma-k2-2026-10-27/`,
записка `Docs/models/FEEDBACK-2026-10-27-codex-linii-pereloma.md`). Чистые крупные грани требуют рёбер вдоль линий перелома, а не
алгоритма поверх случайной сетки: огранка k-средними резала грудь «нагрудником», потому что рёбер по рисунку масс в сетке не было.

Линии берутся с листа как есть — ломаные дизайн-линии в пикселях `витрина/человек/вид.png` (`draw_sheet.py`, DATA → `linii-lista.json`):
  1. ракурс листа совмещается с покоем К2 по габариту фигуры (высота — масштаб, середина — центр); анфас — лучи спереди,
     спина — сзади, профиль (снят с левого бока: лицо влево, Г1 у переднего края, Г4 у заднего) — слева и зеркально справа; 3/4 не берётся: проекция неоднозначна;
  2. точки ломаной через ~1 см бросаются лучом на поверхность; попадания соединяются КРАТЧАЙШИМ ПУТЁМ ПО РЁБРАМ сетки (путь
     длиннее тройного прямого — разрыв: линия ушла за край тела);
  3. вершины пути едут ВДОЛЬ ПОВЕРХНОСТИ на саму линию (ближайшая точка ломаной попаданий, затем на исходную поверхность), сдвиг
     не больше половины кратчайшего ребра вершины; вершина, чей сдвиг выворачивает соседнюю грань, остаётся. Неподвижны вершины
     швов (общие у разных объектов — кольца швов не меняются) и открытого края;
  4. ребро пути получает атрибут `перелом` (2 — главная Г, 1 — второстепенная В): огранка не связывает области через него.
Топология не меняется (число вершин и граней то же, кольца швов те же) — меняется положение вершин базы вдоль поверхности.
Лист — направление перехода, не канава: глубину и остроту ставит огранка, а не этот шаг.

  blender -b --factory-startup -P linii_k2.py -- K2.blend выход.blend --linii linii-lista.json [--vidy front,back,profile]
      [--tolko Г1,Г2] [--profil Г4] [--kadr префикс] [--otchet отчёт.json]
"""
import sys, json, math, heapq
import bpy, bmesh
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

a = sys.argv[sys.argv.index('--') + 1:]
src, out = a[0], a[1]
opt = dict(vidy='front,back,profile', profil='Г4')
i = 2
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
import os
for k_ in ('linii', 'kadr', 'otchet'):
    if opt.get(k_): opt[k_] = os.path.abspath(opt[k_])
L = json.load(open(opt['linii'], encoding='utf-8'))
TOLKO = set(opt['tolko'].split(',')) if opt.get('tolko') else None
# ИЗ ПРОФИЛЯ — только то, чего нет в анфас и со спины (`--profil`, по умолчанию Г4 — верхненаружный поворот таза): линии у края
# силуэта (Г1 на боку клетки, Г2, В5) луч сбоку встречает вскользь, и они ложились двойниками на грудь и пресс (проба 27.10)
PROFIL = set(opt['profil'].split(','))

bpy.ops.wm.open_mainfile(filepath=src)
OBJ = [o for o in bpy.data.objects if o.type == 'MESH' and '@' not in o.name]
for o in OBJ:
    if o.data.shape_keys:
        for kb in o.data.shape_keys.key_blocks: kb.value = 0.0

# ---------- тело одной сеткой по базовым положениям ----------
uid, P, vmap, owners = {}, [], {}, []
for o in OBJ:
    for vi, v in enumerate(o.data.vertices):
        p = o.matrix_world @ v.co; k = (round(p.x, 5), round(p.y, 5), round(p.z, 5))
        if k not in uid: uid[k] = len(P); P.append(list(p)); owners.append(set())
        vmap[(o.name, vi)] = uid[k]; owners[uid[k]].add(o.name)
P = np.array(P); N_ = len(P)
F = [[vmap[(o.name, vi)] for vi in pl.vertices] for o in OBJ for pl in o.data.polygons]
nb = [set() for _ in range(N_)]; ec = {}
for f in F:
    for j in range(len(f)):
        x_, y_ = f[j], f[(j + 1) % len(f)]; nb[x_].add(y_); nb[y_].add(x_)
        e = (min(x_, y_), max(x_, y_)); ec[e] = ec.get(e, 0) + 1
fixed = np.array([len(s) > 1 for s in owners])
for (x_, y_), c_ in ec.items():
    if c_ != 2: fixed[x_] = fixed[y_] = True
vf = [[] for _ in range(N_)]
for fi, f in enumerate(F):
    for v in f: vf[v].append(fi)
TRI = [(f[0], f[j], f[j + 1]) for f in F for j in range(1, len(f) - 1)]
BVH0 = BVHTree.FromPolygons([Vector(p) for p in P], [list(t) for t in TRI])
tri_v = np.array(TRI)
lo, hi = P.min(0), P.max(0); H = hi[2] - lo[2]
print('К2: вершин %d, граней %d, неподвижных (швы и край) %d; габарит x %.3f y %.3f z %.3f м' % (N_, len(F), int(fixed.sum()), *(hi - lo)))


# ---------- лист → мир ----------
def v_mir(vid, px, py):
    """точка листа → (начало луча, направление); масштаб — высота фигуры к высоте К2, низ — на земле К2"""
    fx0, fy0, fx1, fy1 = L[vid]['figura']; s = H / (fy1 - fy0); z = lo[2] + (fy1 - py) * s
    cx = (fx0 + fx1) / 2
    if vid == 'front': return [Vector(((px - cx) * s, lo[1] - 1, z))], Vector((0, 1, 0))
    if vid == 'back': return [Vector((-(px - cx) * s, hi[1] + 1, z))], Vector((0, -1, 0))
    yc = (lo[1] + hi[1]) / 2; y = (px - cx) * s + yc   # профиль смотрит влево: правее на листе — дальше назад (+Y)
    return [Vector((hi[0] + 1, y, z)), Vector((lo[0] - 1, y, z))], None   # профиль: слева и зеркально справа


def luchi(vid, pts):
    """ломаная листа → по стороне: список попаданий (точка, вершина)"""
    fx0, fy0, fx1, fy1 = L[vid]['figura']; s = H / (fy1 - fy0)
    dense = []
    for p0, p1 in zip(pts, pts[1:]):
        n = max(2, int(math.dist(p0, p1) * s / 0.01))
        dense += [(p0[0] + (p1[0] - p0[0]) * t / n, p0[1] + (p1[1] - p0[1]) * t / n) for t in range(n)]
    dense.append(tuple(pts[-1]))
    storony = {}
    for px, py in dense:
        org, d = v_mir(vid, px, py)
        for si, o_ in enumerate(org):
            dd = d or Vector((-1 if si == 0 else 1, 0, 0))
            hit = BVH0.ray_cast(o_, dd, 5.0)
            if hit[0] is None: continue
            t = tri_v[hit[2]]; v = min(t, key=lambda q: (P[q] - np.array(hit[0])) @ (P[q] - np.array(hit[0])))
            storony.setdefault(si, []).append((np.array(hit[0]), int(v)))
    return list(storony.values())


def put(u, w, lim):
    """кратчайший путь по рёбрам u→w (Дейкстра), None — если длиннее lim"""
    dist = {u: 0.0}; prev = {}; pq = [(0.0, u)]
    while pq:
        d, x = heapq.heappop(pq)
        if x == w: break
        if d > dist.get(x, 1e9) or d > lim: continue
        for y in nb[x]:
            nd = d + float(np.linalg.norm(P[x] - P[y]))
            if nd < dist.get(y, 1e9): dist[y] = nd; prev[y] = x; heapq.heappush(pq, (nd, y))
    if w not in dist or dist[w] > lim: return None
    p_ = [w]
    while p_[-1] != u: p_.append(prev[p_[-1]])
    return p_[::-1]


REB = {}            # ребро (u, w) → сорт (2 Г / 1 В)
REB_ID = {}         # ребро → номер линии по коду имени (Г6 → 6, В2 → 102): атрибут `перелом_линия`, для излома с углом по линии
CEL = {}            # вершина → точки линии рядом (для сдвига)
stat = {}
for vid in opt['vidy'].split(','):
    for ln in L[vid]['linii']:
        if TOLKO and ln['imya'] not in TOLKO: continue
        if vid == 'profile' and ln['imya'] not in PROFIL: continue
        sort = 2 if ln['imya'][0] == 'Г' else 1
        for hits in luchi(vid, ln['tochki']):
            chain = [h[1] for h in hits]; poly = np.array([h[0] for h in hits]); n_reb = 0
            for (pa, va), (pb, vb) in zip(hits, hits[1:]):
                if va == vb: continue
                pth = put(va, vb, 3 * float(np.linalg.norm(P[va] - P[vb])) + 0.02)
                if pth is None: continue
                for x_, y_ in zip(pth, pth[1:]):
                    e = (min(x_, y_), max(x_, y_)); REB[e] = max(REB.get(e, 0), sort); n_reb += 1
                    kod = ln['imya'].split()[0]; REB_ID.setdefault(e, (100 if kod[0] == 'В' else 0) + int(''.join(c for c in kod[1:] if c.isdigit()) or 0))
                for v in pth: CEL.setdefault(v, []).append(poly)
            st = stat.setdefault(ln['imya'], [0, 0]); st[0] += 1; st[1] += n_reb
print('ЛИНИИ: ' + ', '.join('%s %d прох. %d рёбер' % (k, v[0], v[1]) for k, v in sorted(stat.items())))


# ---------- сдвиг вершин пути на линию вдоль поверхности ----------
def blizh_na_lomanoj(p, poly):
    best, bd = None, 1e9
    for q0, q1 in zip(poly, poly[1:]):
        ab = q1 - q0; t = float(np.clip((p - q0) @ ab / max(ab @ ab, 1e-12), 0, 1)); c = q0 + ab * t; d = float(np.linalg.norm(p - c))
        if d < bd: bd, best = d, c
    return best, bd


def fnorm(Pp, fi):
    f = F[fi]; return np.cross(Pp[f[1]] - Pp[f[0]], Pp[f[2]] - Pp[f[0]])


P1 = P.copy(); sdv = []; ostalos = 0
for v, polys in CEL.items():
    if fixed[v]: continue
    c, d = min((blizh_na_lomanoj(P[v], pl) for pl in polys if len(pl) > 1), key=lambda x: x[1], default=(None, 1e9))
    if c is None: continue
    lim = 0.5 * min(float(np.linalg.norm(P[v] - P[u])) for u in nb[v])
    step = c - P[v]; ln_ = float(np.linalg.norm(step))
    if ln_ > lim: step *= lim / ln_
    q = BVH0.find_nearest(Vector(P[v] + step))[0]
    if q is None: continue
    old = P1[v].copy(); P1[v] = np.array(q)
    if any(fnorm(P1, fi) @ fnorm(P, fi) <= 0 for fi in vf[v]): P1[v] = old; ostalos += 1; continue
    sdv.append(float(np.linalg.norm(P1[v] - P[v])))
for o in OBJ:
    inv = o.matrix_world.inverted()
    for vi, v in enumerate(o.data.vertices):
        u = vmap[(o.name, vi)]
        if not np.allclose(P1[u], P[u]): v.co = inv @ Vector(P1[u])
    at = o.data.attributes.get('перелом') or o.data.attributes.new('перелом', 'INT', 'EDGE')
    vals = []
    for ed in o.data.edges:
        x_, y_ = vmap[(o.name, ed.vertices[0])], vmap[(o.name, ed.vertices[1])]
        vals.append(REB.get((min(x_, y_), max(x_, y_)), 0))
    at.data.foreach_set('value', vals)
    for ed, s_ in zip(o.data.edges, vals): ed.use_edge_sharp = s_ == 2
    ai = o.data.attributes.get('перелом_линия') or o.data.attributes.new('перелом_линия', 'INT', 'EDGE')
    ai.data.foreach_set('value', [REB_ID.get((min(vmap[(o.name, ed.vertices[0])], vmap[(o.name, ed.vertices[1])]), max(vmap[(o.name, ed.vertices[0])], vmap[(o.name, ed.vertices[1])])), 0) for ed in o.data.edges])
otchet = dict(reber_G=sum(1 for s_ in REB.values() if s_ == 2), reber_V=sum(1 for s_ in REB.values() if s_ == 1),
              vershin_sdvinuto=len(sdv), sdvig_sred_mm=round(float(np.mean(sdv)) * 1000, 2) if sdv else 0,
              sdvig_maks_mm=round(max(sdv, default=0) * 1000, 2), ne_sdvinuto_razvorot=ostalos, linii=stat)
print('ЛИНИИ: ' + json.dumps({k: v for k, v in otchet.items() if k != 'linii'}, ensure_ascii=False))
if opt.get('otchet'): json.dump(otchet, open(opt['otchet'], 'w', encoding='utf-8'), ensure_ascii=False, indent=2)
bpy.ops.wm.save_as_mainfile(filepath=out)

# ---------- кадр: тело и линии трубками (Г оранжевым, В голубым) — сверка с листом ----------
if opt.get('kadr'):
    sc = bpy.context.scene
    for srt, col, r in ((2, (1.0, 0.6, 0.2, 1), 0.004), (1, (0.3, 0.85, 0.9, 1), 0.0025)):
        cu = bpy.data.curves.new('л%d' % srt, 'CURVE'); cu.dimensions = '3D'; cu.bevel_depth = r
        for (x_, y_), s_ in REB.items():
            if s_ != srt: continue
            sp = cu.splines.new('POLY'); sp.points.add(1)
            for k, q in enumerate((P1[x_], P1[y_])): sp.points[k].co = (*q, 1)
        ob = bpy.data.objects.new('л%d' % srt, cu); sc.collection.objects.link(ob); ob.color = col
    for o in bpy.data.objects:
        if o.type == 'MESH': o.hide_render = '@' in o.name; o.color = (0.75, 0.75, 0.75, 1)
    sc.render.engine = 'BLENDER_WORKBENCH'; sh = sc.display.shading; sh.light = 'STUDIO'; sh.color_type = 'OBJECT'
    sh.show_backface_culling = True
    sc.render.resolution_x, sc.render.resolution_y = 700, 1400
    cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); sc.collection.objects.link(cam); sc.camera = cam
    cam.data.type = 'ORTHO'; cam.data.ortho_scale = H * 1.08; c = (lo + hi) / 2
    for nm, d in (('front', Vector((0, -1, 0))), ('profile', Vector((-1, 0, 0))), ('back', Vector((0, 1, 0)))):
        cam.location = Vector(c) + d * 10; cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = opt['kadr'] + '_' + nm + '.png'; bpy.ops.render.render(write_still=True)
