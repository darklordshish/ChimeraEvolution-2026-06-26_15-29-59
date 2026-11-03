"""ПЛЕЧО ПО ЛИСТУ (03.11, критик r2: «плечи шире листа на ~2 см, шея короче»; карта силуэта подтверждает: верх плеча и склон трапеции снаружи листа на 1–2 см). По высотам `--z` от маски
анфас: полуширина фигуры листа против полуширины тела (пересечение рёбер всех объектов с плоскостью) → вершина: x' = x·(1 + (k − 1)·t), t растёт от 0 у |x| = `--x0` до 1 у края тела, k =
полуширина листа / полуширина тела, сглажено по z, гаснет к концам полосы. Смещение — функция только положения, поэтому общие вершины объектов не расходятся и его можно приложить к мосту
(`--tablica` пишет, `--tablica_iz` читает). Ключ и база правятся вместе (база = ключ).

  blender -b вход.blend --factory-startup -P plecho_po_listu.py -- выход.blend --maska vid-front.png [--tablica t.json | --tablica_iz t.json] [--z 1.30:1.62] [--x0 0.05] [--imya Поджарый]
"""
import sys, json, os
import bpy
import numpy as np
from mathutils import Vector

a = sys.argv[sys.argv.index('--') + 1:]
out = a[0]
opt = dict(z='1.30:1.62', x0='0.05', imya='Поджарый')
i = 1
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
z0, z1 = (float(q) for q in opt['z'].split(':')); X0 = float(opt['x0'])
OBJ = [o for o in bpy.data.objects if o.type == 'MESH' and '@' not in o.name and o.data.shape_keys and opt['imya'] in o.data.shape_keys.key_blocks]


def poz(o):
    return np.array([tuple(o.matrix_world @ k.co) for k in o.data.shape_keys.key_blocks[opt['imya']].data])


if opt.get('tablica_iz'):
    TB = json.load(open(opt['tablica_iz'], encoding='utf-8'))
else:
    im = bpy.data.images.load(os.path.abspath(opt['maska'])); w_, h_ = im.size; px = np.array(im.pixels[:]).reshape(h_, w_, -1)[::-1]; M = px[:, :, 0] > 0.5
    if M[0, 0]: M = ~M
    ys = np.nonzero(M.any(1))[0]; bot = ys.max(); s = 1.85 / (ys.max() - ys.min()); xs_ = np.nonzero(M.any(0))[0]; cx = (xs_.min() + xs_.max() + 1) / 2
    TB = []
    for z in np.arange(z0, z1 + 1e-6, 0.02):
        r = np.nonzero(M[int(round(bot - z / s))])[0]
        hl = max(abs(r.min() - cx), abs(r.max() + 1 - cx)) * s; hm = 0.0
        for o in OBJ:
            P = poz(o); E = np.array([tuple(e.vertices) for e in o.data.edges]); za, zb = P[E[:, 0], 2], P[E[:, 1], 2]; m = (za - z) * (zb - z) < 0
            if m.any():
                t = ((z - za[m]) / (zb[m] - za[m])); q = P[E[m, 0], 0] + (P[E[m, 1], 0] - P[E[m, 0], 0]) * t; hm = max(hm, float(np.abs(q).max()))
        if hm > 0: TB.append(dict(z=round(float(z), 3), k=hl / hm, hl=hl, hm=hm))
    v = np.array([t['k'] for t in TB]); v2 = np.convolve(np.pad(v, 2, mode='edge'), np.ones(5) / 5, mode='valid')
    for t, q in zip(TB, v2): t['k'] = float(q)
    # гашение к концам полосы
    for t in TB:
        g = min(1.0, (t['z'] - z0) / 0.04, (z1 - t['z']) / 0.04); t['k'] = 1 + (t['k'] - 1) * max(g, 0)
    if opt.get('tablica'): json.dump(TB, open(opt['tablica'], 'w', encoding='utf-8'), ensure_ascii=False, indent=0)
    print('ПЛЕЧО: ' + ' '.join('%.2f: лист %.1f тело %.1f k %.2f' % (t['z'], t['hl'] * 100, t['hm'] * 100, t['k']) for t in TB[::2]))
Z = np.array([t['z'] for t in TB]); K = np.array([t['k'] for t in TB]); HM = np.array([t['hm'] for t in TB])
for o in OBJ:
    P = poz(o); zz = P[:, 2]; inb = (zz > z0) & (zz < z1); k = np.interp(zz, Z, K); hm = np.interp(zz, Z, HM); ax = np.abs(P[:, 0])
    t = np.clip((ax - X0) / np.maximum(hm - X0, 1e-3), 0, 1.2)
    dx = P[:, 0] * (k - 1) * t * inb
    # выше плеча шея: ширина уже x0 — нарастание по доле ширины, сдвиг мал
    Q = P.copy(); Q[:, 0] += dx
    kb = o.data.shape_keys.key_blocks[opt['imya']]; iw = o.matrix_world.inverted()
    same = all((kb.data[j].co - o.data.vertices[j].co).length < 1e-6 for j in range(len(o.data.vertices)))
    for vi in range(len(Q)):
        p = iw @ Vector(Q[vi]); kb.data[vi].co = p
        if same: o.data.vertices[vi].co = p; o.data.shape_keys.key_blocks[0].data[vi].co = p
    if np.abs(dx).max() > 1e-4: print('ПЛЕЧО: %s — вершин %d, сдвиг макс %.1f мм' % (o.name, int((np.abs(dx) > 1e-4).sum()), float(np.abs(dx).max()) * 1000))
bpy.ops.wm.save_as_mainfile(filepath=out)
