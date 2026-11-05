"""ЛЕПКА КЛЮЧЕЙ ПОД МАССУ БАЗЫ (05.11, Б7; ГеймБосс: ключи грубее базы, мозаика, манжета рвётся полосами). Перенос мостом даёт каждой вершине свой сдвиг, и на крупной грани они спорят: плоскости базы ломаются в мозаику. Здесь сглаживается поле СДВИГОВ, а не форма: сдвиг вершины тянется к среднему соседей, поэтому большие плоскости базы остаются плоскостями и в ключе. Вершины, совпавшие в базе (швы объектов), идут одной группой: швы не расходятся. Кисть (`Руки`, z < `--z_kist`) держит жёсткий ход и служит краем поля, у запястья вес плавный: манжета не рвётся.
Правки формы по весам кости считаются здесь же: `--plech` — лишние проходы по плечам (острые углы), `--lok` — сдвиг окна по z (опустить плечи: голова «Лося» утоплена).

  blender -b сборка.blend --factory-startup --python-exit-code 1 -P klyuch_lepka.py -- выход.blend [--klyuchi Волк,Волк1,Лось] [--it 8] [--lam 0.5] [--z_kist 0.96] [--noga ключ:z;…] [--plech 10] [--lok ключ:x0:x1:z0:z1:dz;…]
"""
import sys
import bpy, bmesh
import numpy as np

a = sys.argv[sys.argv.index('--') + 1:]; OUT = a[0]; opt = dict(klyuchi='Волк,Волк1,Лось', it='8', lam='0.5', z_kist='0.96', stopa='0.5'); i = 1
while i < len(a): opt[a[i].lstrip('-')] = a[i + 1]; i += 2
IT, LAM, ZK = int(opt['it']), float(opt['lam']), float(opt['z_kist'])
O = [o for o in bpy.data.objects if o.type == 'MESH' and '@' not in o.name and o.data.shape_keys]
gr = {}; Gv = []   # группа совпавших вершин
for o in O:
    M = o.matrix_world; kb0 = o.data.shape_keys.key_blocks[0]
    for j, k in enumerate(kb0.data):
        p = M @ k.co; q = (round(p.x, 4), round(p.y, 4), round(p.z, 4))
        if q not in gr: gr[q] = len(gr); Gv.append([])
        Gv[gr[q]].append((o, j))
n = len(gr); Pb = np.zeros((n, 3))
for q, g in gr.items(): Pb[g] = q
sos = [set() for _ in range(n)]
for o in O:
    M = o.matrix_world; kb0 = o.data.shape_keys.key_blocks[0]; G_ = [gr[tuple(round(c, 4) for c in M @ k.co)] for k in kb0.data]
    for e in o.data.edges:
        x, y = G_[e.vertices[0]], G_[e.vertices[1]]
        if x != y: sos[x].add(y); sos[y].add(x)
ruki = np.zeros(n, bool)
for g, L in enumerate(Gv):
    ruki[g] = any(o.name == 'Руки' for o, _ in L)
ves = np.ones(n)
ves[ruki] = np.clip((Pb[ruki, 2] - ZK) / 0.06, 0, 1)
print('ЛЕПКА: групп %d, кисть вес 0: %d' % (n, int((ves == 0).sum())))
Is = [np.array(sorted(s)) if s else np.array([g]) for g, s in enumerate(sos)]
for kn in opt['klyuchi'].split(','):
    D = np.zeros((n, 3)); c_ = np.zeros(n)
    for g, L in enumerate(Gv):
        for o, j in L:
            kb = o.data.shape_keys.key_blocks.get(kn)
            if kb is None: continue
            M3 = o.matrix_world.to_3x3(); D[g] += np.array(M3 @ (kb.data[j].co - o.data.shape_keys.key_blocks[0].data[j].co)); c_[g] += 1
    c_[c_ == 0] = 1; D /= c_[:, None]; D0 = D.copy()
    vk = ves.copy()
    for s_ in filter(None, opt.get('noga', '').split(';')):   # ключ:z — голень и стопа остаются как перенесены (сглаживание складывает длинную голень)
        kl, z_ = s_.split(':')
        if kl == kn: vk = vk * np.clip((Pb[:, 2] - float(z_)) / 0.15, 0, 1)
    pl = (np.abs(Pb[:, 0]) > 0.09) & (Pb[:, 2] > 1.30) & (Pb[:, 2] < 1.66)
    for it_ in range(IT + int(opt.get('plech', '0'))):
        m_ = vk if it_ < IT else vk * pl
        Sr = np.array([D[s].mean(0) for s in Is]); D = D + (LAM * m_)[:, None] * (Sr - D)
    # правка масс: вверх — шея и голова, вниз — плечи (окно по z базы и |x|)
    for s_ in filter(None, opt.get('lok', '').split(';')):
        kl, x0, x1, z0, z1, dz = s_.split(':')
        if kl != kn: continue
        x0, x1, z0, z1, dz = map(float, (x0, x1, z0, z1, dz))
        wx = np.clip((np.abs(Pb[:, 0]) - x0) / 0.03, 0, 1) * np.clip((x1 - np.abs(Pb[:, 0])) / 0.03, 0, 1); wz = np.clip((Pb[:, 2] - z0) / 0.05, 0, 1) * np.clip((z1 - Pb[:, 2]) / 0.05, 0, 1)
        D[:, 2] += dz * wx * wz * ves
    for g, L in enumerate(Gv):
        for o, j in L:
            kb = o.data.shape_keys.key_blocks.get(kn)
            if kb is None: continue
            kb.data[j].co = o.data.shape_keys.key_blocks[0].data[j].co + o.matrix_world.to_3x3().inverted() @ __import__('mathutils').Vector(D[g])
    dd = np.linalg.norm(D - D0, axis=1) * 1000; print('ЛЕПКА: %s — сдвиг от сглаживания средн %.1f, макс %.1f мм; кисть %.1f мм' % (kn, dd.mean(), dd.max(), dd[ves == 0].max() if (ves == 0).any() else 0))
for o in O: o.data.update()
bpy.ops.wm.save_as_mainfile(filepath=OUT)
