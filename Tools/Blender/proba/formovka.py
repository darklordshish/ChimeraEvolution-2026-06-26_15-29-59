"""ФОРМОВКА КОНТУРОМ (22.10, разбор дизайнера по r13, через ГеймБосса: «в тумане внутренний рельеф пропадёт — три отношения должны
работать контуром, простое укрупнение треугольников этого не даст»). Правит форму-ключ `--imya` в покое по областям шаблона К2:

  1. мощное бедро → лёгкая голень: сечение голени сжимается к её оси от колена к скакательному (`--golen` — масштаб у
     скакательного, у колена 1); масса икры — в верхней трети (до трети длины сжатия нет); узлы колен сглаживаются полосой
     у колена (`--koleno` шагов Таубина);
  2. грудная клетка → втянутый живот: сечение торса между низом грудной клетки и тазом сжимается к оси хребта (`--talia` — в
     самом узком месте, плавно к краям полосы `--talia_z низ:середина:верх`, высоты базы К2);
  3. (лопатки и боковые клинья к талии — следующим шагом).

Область — по главной кости вершины и её БАЗОВОМУ положению (до ключа): швы объектов (общие базовые положения) двигаются одинаково.
Оси — суставы покоя цепи цели (`--sustavy`, файл `podgonka_v_poze.py`), в них лежит форма-ключ.

  blender -b --factory-startup -P formovka.py -- вход.blend выход.blend --sustavy f.json [--imya Волк] [--golen 0.75] [--koleno 6]
      [--talia 0.88] [--talia_z 0.98:1.10:1.22] [--grud 1.04:1.24:12] [--raspravit 40]
"""
import sys, json
import bpy
import numpy as np
from mathutils import Vector

a = sys.argv[sys.argv.index('--') + 1:]
src, out = a[0], a[1]
opt = dict(imya='Волк', golen='0.75', koleno='6', talia='0.88', talia_z='0.98:1.10:1.22')
i = 2
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
NAME = opt['imya']
SJ = {b['name']: b for b in json.load(open(opt['sustavy'], encoding='utf-8'))['bones']}

bpy.ops.wm.open_mainfile(filepath=src)
body = [o for o in bpy.data.objects if o.type == 'MESH' and not o.name.startswith('@')
        and o.data.shape_keys and NAME in o.data.shape_keys.key_blocks]


def os_(bn):
    h, t = np.array(SJ[bn]['head']), np.array(SJ[bn]['tail']); return h, t


def k_osi(p, h, t, k):
    """сжать точку к оси кости h→t в k раз (вдоль оси не двигать)"""
    ax = t - h; u = np.clip((p - h) @ ax / max(ax @ ax, 1e-9), 0, 1); c = h + ax * u
    return c + (p - c) * k, u


# единая сетка по базовым положениям (как в огранке): соседи для сглаживания колен
uid, P, B, vmap, vbone = {}, [], [], {}, []
for o in body:
    Mw = o.matrix_world; kb = o.data.shape_keys.key_blocks[NAME]; gn = {g.index: g.name for g in o.vertex_groups}
    for vi, v in enumerate(o.data.vertices):
        bp = Mw @ v.co; k = (round(bp.x, 5), round(bp.y, 5), round(bp.z, 5))
        if k not in uid:
            uid[k] = len(P); P.append(list(Mw @ kb.data[vi].co)); B.append(list(bp))
            vbone.append(gn[max(v.groups, key=lambda g: g.weight).group] if v.groups else '')
        vmap[(o.name, vi)] = uid[k]
P = np.array(P); B = np.array(B); P0 = P.copy()
nb = [set() for _ in P]
for o in body:
    for e in o.data.edges:
        x_, y_ = vmap[(o.name, e.vertices[0])], vmap[(o.name, e.vertices[1])]; nb[x_].add(y_); nb[y_].add(x_)
nb = [sorted(s) for s in nb]

# 1. голень: сжатие к оси от колена (1) к скакательному (golen), первая треть — без сжатия (масса икры)
G = float(opt['golen']); n_g = 0
for v, bn in enumerate(vbone):
    if bn.split('.')[0] != 'голень' or bn not in SJ: continue
    h, t = os_(bn)
    ax = t - h; u = float(np.clip((P[v] - h) @ ax / max(ax @ ax, 1e-9), 0, 1))
    k = 1.0 if u < 0.33 else 1.0 + (G - 1.0) * (u - 0.33) / 0.67
    P[v], _ = k_osi(P[v], h, t, k); n_g += 1
# узлы колен: Таубин на полосе ±8 см от колена по высоте базы
kol = [v for v, bn in enumerate(vbone) if bn.split('.')[0] in ('голень', 'бедро')]
kz = {s: SJ['голень' + s]['head'] for s in ('', '.L') if 'голень' + s in SJ}
band = np.zeros(len(P), bool)
for v in kol:
    s = '.L' if vbone[v].endswith('.L') else ''
    kb_ = SJ['голень' + s]['head']
    if abs(P0[v][2] - kb_[2]) < 0.08: band[v] = True
for _ in range(int(opt['koleno'])):
    for lam in (0.5, -0.53):
        L = np.array([P[nb[v]].mean(0) - P[v] if nb[v] else np.zeros(3) for v in range(len(P))])
        P = np.where(band[:, None], P + lam * L, P)
# 2. талия: сжатие торса к оси хребта (по горизонтали), полоса по базовой высоте
T = float(opt['talia']); z0, zm, z1 = (float(x) for x in opt['talia_z'].split(':'))
h, t = os_('хребет'); n_t = 0
for v, bn in enumerate(vbone):
    if bn.split('.')[0] not in ('живот', 'хребет', 'таз', 'грудная клетка'): continue
    zb = B[v][2]
    if not (z0 < zb < z1): continue
    w = (zb - z0) / (zm - z0) if zb < zm else (z1 - zb) / (z1 - zm)
    k = 1.0 + (T - 1.0) * w
    ax = t - h; u = np.clip((P[v] - h) @ ax / max(ax @ ax, 1e-9), -1, 2); c = h + ax * u
    d = P[v] - c; d[2] = 0.0
    P[v] = P[v] - d * (1.0 - k); n_t += 1
# 3. ГРУДЬ → ЖИВОТ КЛИНОМ (`--grud z0:z1:итераций`, дизайн-линия 22.10, FEEDBACK-2026-10-22-codex-grud-st2): поворот поверхности под
# грудью был сжат в тёмную полосу 0.05–0.08 H с нависающим кантом («нагрудник»); переход растягивается на 0.15–0.20 H —
# сглаживание Таубина (без усадки) на передней и боковой поверхности торса в полосе базовых высот z0..z1, вес — колокол
# по высоте (на краях полосы 0: объём груди выше и живот ниже не трогаются); ширину талии не менять
if opt.get('grud'):
    g0, g1, gn = opt['grud'].split(':'); g0, g1, gn = float(g0), float(g1), int(gn)
    wg = np.zeros(len(P))
    for v in range(len(P)):
        x_, y_, z_ = B[v]
        if g0 < z_ < g1 and y_ < float(opt.get('grud_y', '0.02')) and abs(x_) < float(opt.get('grud_x', '0.4')) and vbone[v].split('.')[0] in ('хребет', 'живот', 'грудная клетка', 'таз', 'лопатка', 'плечо'):
            t_ = (z_ - g0) / (g1 - g0); wg[v] = np.sin(np.pi * t_) ** 2
    # `--grud_lap 1`: простой лапласиан (с усадкой) — Таубин сохраняет форму вместе с кантом, а кант и надо снять
    LAMS = (0.5,) if opt.get('grud_lap', '0') == '1' else (0.5, -0.53)
    for _ in range(gn):
        for lam in LAMS:
            L = np.array([P[nb[v]].mean(0) - P[v] if nb[v] else np.zeros(3) for v in range(len(P))])
            P = P + (lam * wg)[:, None] * L
    print('ФОРМОВКА: грудь → живот — %d вершин в полосе z %.2f–%.2f, %d шагов' % (int((wg > 0).sum()), g0, g1, gn))
# 4. РАСПРАВИТЬ СКЛАДКИ (`--raspravit 40`, 22.10, критик ряда: «тёмный пояс» ст.2 и «яма на груди» ст.1): подгонка заворачивает часть
# граней — нормаль ключа смотрит против базовой (Сердце 89, хребет 91, Руки 321 грань); в игре это тёмные провалы. Вершины
# перевёрнутых граней и их соседи сглаживаются к среднему соседей, пока складок не останется (или `--raspravit` шагов)
if opt.get('raspravit'):
    Fb = []
    for o in body:
        for pl in o.data.polygons: Fb.append([vmap[(o.name, vi)] for vi in pl.vertices])
    def perevernuty(Pk):
        out_ = []
        for f in Fb:
            nb_ = np.cross(B[f[1]] - B[f[0]], B[f[2]] - B[f[0]]); nk_ = np.cross(Pk[f[1]] - Pk[f[0]], Pk[f[2]] - Pk[f[0]])
            lb, lk = np.linalg.norm(nb_), np.linalg.norm(nk_)
            if lb > 0 and lk > 0 and np.dot(nb_, nk_) / (lb * lk) < 0.0: out_.append(f)
        return out_
    n0_ = len(perevernuty(P))
    for it in range(int(opt['raspravit'])):
        bad = perevernuty(P)
        if not bad: break
        vs = set(v for f in bad for v in f)
        vs |= set(u for v in list(vs) for u in nb[v])
        for v in vs:
            if nb[v]: P[v] = 0.5 * P[v] + 0.5 * P[nb[v]].mean(0)
    print('ФОРМОВКА: складки — перевёрнутых граней %d → %d' % (n0_, len(perevernuty(P))))
dv = np.linalg.norm(P - P0, axis=1)
for o in body:
    kb = o.data.shape_keys.key_blocks[NAME]; inv = o.matrix_world.inverted()
    for vi in range(len(o.data.vertices)):
        kb.data[vi].co = inv @ Vector(P[vmap[(o.name, vi)]])
print('ФОРМОВКА: голень %d вершин (×%.2f к скакательному), колени %d вершин, талия %d вершин (×%.2f); сдвиг средн. %.4f, макс %.4f м' % (
    n_g, G, int(band.sum()), n_t, T, dv.mean(), dv.max()))
bpy.ops.wm.save_as_mainfile(filepath=out)
