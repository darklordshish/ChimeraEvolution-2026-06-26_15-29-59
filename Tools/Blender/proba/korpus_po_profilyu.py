"""КОРПУС ПО ПРОФИЛЮ (03.11, наложение r6, `profil_mera.py`): спереди модель выступает за лист на 1.2–2.2 см, сзади на лопатке и верхе груди до 1.7 см. Перед и зад корпуса по высоте сводятся к листу:
вершина корпуса (|x| < `--x0`, спад до `--x1`, рука не трогается) y' = y + Df·t при y < yc, y − Db·t при y > yc; t растёт от 0 у середины глубины к 1 у контура, Df и Db — излишек из таблицы, сглажен, только положительный, умножен на `--k`.
**Зад на z < `--zad_ot` не берётся:** на талии контур листа сзади образует локоть, а не корпус (рука на листе отведена назад; это поза, не форма). Смещение — функция положения: общие вершины не расходятся, мост двигается той же таблицей (`--tablica_iz`).

  blender -b вход.blend --factory-startup -P korpus_po_profilyu.py -- выход.blend --mera pm.json [--tablica t.json | --tablica_iz t.json] [--k 0.8] [--x0 0.15] [--x1 0.20] [--z0 0.90] [--z1 1.60] [--zad_ot 1.30] [--imya Поджарый]
"""
import sys, json
import bpy
import numpy as np
from mathutils import Vector

a = sys.argv[sys.argv.index('--') + 1:]
out = a[0]
opt = dict(k='0.8', x0='0.15', x1='0.20', z0='0.90', z1='1.60', zad_ot='1.30', imya='Поджарый')
i = 1
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
K, X0, X1, Z0, Z1, ZO = (float(opt[q]) for q in ('k', 'x0', 'x1', 'z0', 'z1', 'zad_ot'))
OBJ = [o for o in bpy.data.objects if o.type == 'MESH' and '@' not in o.name and o.data.shape_keys and opt['imya'] in o.data.shape_keys.key_blocks]
poz = lambda o: np.array([tuple(o.matrix_world @ k.co) for k in o.data.shape_keys.key_blocks[opt['imya']].data])
ss = lambda t: (lambda q: q * q * (3 - 2 * q))(np.clip(t, 0, 1))
if opt.get('tablica_iz'):
    T = json.load(open(opt['tablica_iz'], encoding='utf-8'))
else:
    R = json.load(open(opt['mera'], encoding='utf-8')); Z = np.array([r['z'] for r in R])
    Df = np.clip(np.array([r['pered'] for r in R]), 0, 0.03) * K
    Db = np.where(Z >= ZO, np.clip(np.array([r['zad'] for r in R]), 0, 0.03), 0) * K
    gl = lambda v: np.convolve(np.pad(v, 1, mode='edge'), np.ones(3) / 3, mode='valid')
    T = dict(z=Z.tolist(), df=gl(Df).tolist(), db=gl(Db).tolist())
    if opt.get('tablica'): json.dump(T, open(opt['tablica'], 'w', encoding='utf-8'))
Z, Df, Db = (np.array(T[q]) for q in ('z', 'df', 'db'))
ALL = np.vstack([poz(o) for o in OBJ]); ALL = ALL[np.abs(ALL[:, 0]) < X0]
for o in OBJ:
    P = poz(o); zz = P[:, 2]
    # середина и полуглубина корпуса на высоте вершины — по коридору ±2 см
    yc = np.zeros(len(P)); hw = np.ones(len(P)) * 0.1
    for zv in np.unique(np.round(zz, 2)):
        s = ALL[np.abs(ALL[:, 2] - zv) < 0.02]
        if len(s):
            m = np.round(zz, 2) == zv; yc[m] = (s[:, 1].min() + s[:, 1].max()) / 2; hw[m] = max((s[:, 1].max() - s[:, 1].min()) / 2, 0.02)
    tt = ss(np.abs(P[:, 1] - yc) / hw)
    g = ss((X1 - np.abs(P[:, 0])) / (X1 - X0)) * ss((zz - Z0 + 0.04) / 0.04) * ss((Z1 + 0.02 - zz) / 0.04)
    if o.name == 'голова': g = g * (P[:, 1] > -0.03)
    dy = np.where(P[:, 1] < yc, np.interp(zz, Z, Df), -np.interp(zz, Z, Db)) * tt * g
    if np.abs(dy).max() < 1e-4: continue
    kb = o.data.shape_keys.key_blocks[opt['imya']]; iw = o.matrix_world.inverted()
    same = all((kb.data[j].co - o.data.vertices[j].co).length < 1e-6 for j in range(len(o.data.vertices)))
    Q = P.copy(); Q[:, 1] += dy
    for vi in range(len(Q)):
        p = iw @ Vector(Q[vi]); kb.data[vi].co = p
        if same: o.data.vertices[vi].co = p; o.data.shape_keys.key_blocks[0].data[vi].co = p
    print('КОРПУС: %s — вершин %d, сдвиг макс %.1f мм' % (o.name, int((np.abs(dy) > 1e-4).sum()), float(np.abs(dy).max()) * 1000))
bpy.ops.wm.save_as_mainfile(filepath=out)
