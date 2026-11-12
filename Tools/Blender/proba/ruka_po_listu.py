"""РУКА ПО ЛИСТУ (03.11, критик r2: руки толще листа на 1.5–2.4 см; маска анфас подтверждает: k = 0.72–0.90). Подгонка шла по реконструкции
Hunyuan, а она руку раздула; мерить — по маске листа. Сечение руки по высоте z: ширина и центр по x у модели (пересечение рёбер с
плоскостью) и у маски (первая слева полоса строки, пока она отделена от торса) → вершина: x' = c_лист + (x − c_модель)·k, y' = cy + (y − cy)·k,
k по z сглажен; выше `--z_verh` рука слита с торсом на маске — гашение к 1 до `--z_gas`. Правая и левая — зеркально (рука симметрична, меряется
правая). Вершины, общие с другими объектами (швы подмышки), стоят, их сосед гасится разглаживанием смещения. Ниже `--z_kist` — как на
запястье (кисть строится отдельно).

Таблица: первый прогон пишет `--tablica файл.json`; второй с тем же файлом (`--tablica_iz`) применяет те же смещения к другому файлу
(к МОСТУ: мост обязан двигаться теми же правками, иначе перенос ключей теряет его). Правится ключ `--imya`, и база, если база = ключ.

  blender -b вход.blend --factory-startup -P ruka_po_listu.py -- выход.blend --maska vid-front.png [--tablica t.json | --tablica_iz t.json] [--imya Поджарый]
      [--z_kist 0.96] [--z_verh 1.32] [--z_gas 1.40] [--k_y 1 — множитель к k по глубине]
"""
import sys, json
import bpy, bmesh
import numpy as np
try:
    from PIL import Image
except ImportError:
    Image = None

a = sys.argv[sys.argv.index('--') + 1:]
out = a[0]
opt = dict(imya='Поджарый', z_kist='0.96', z_verh='1.32', z_gas='1.40', k_y='1')
i = 1
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
ZK, ZV, ZG = float(opt['z_kist']), float(opt['z_verh']), float(opt['z_gas'])
o = bpy.data.objects['Руки']; kb = o.data.shape_keys.key_blocks[opt['imya']]; Mw = o.matrix_world
n = len(kb.data); P = np.array([tuple(Mw @ k.co) for k in kb.data]); E = np.array([tuple(e.vertices) for e in o.data.edges])
same = np.allclose(np.array([tuple(k.co) for k in kb.data]), np.array([tuple(v.co) for v in o.data.vertices]), atol=1e-6)


def sechenie(z, P):
    za, zb = P[E[:, 0], 2], P[E[:, 1], 2]; m = (za - z) * (zb - z) < 0
    t = ((z - za[m]) / (zb[m] - za[m]))[:, None]; q = P[E[m, 0]] + (P[E[m, 1]] - P[E[m, 0]]) * t
    q = q[q[:, 0] < -0.15]
    return q


if opt.get('tablica_iz'):
    TB = json.load(open(opt['tablica_iz'], encoding='utf-8'))
else:
    im_ = bpy.data.images.load(opt['maska'] if ':' in opt['maska'] else __import__('os').path.abspath(opt['maska'])); w_, h_ = im_.size; px_ = np.array(im_.pixels[:]).reshape(h_, w_, -1)[::-1]; M = px_[:, :, 0] > 0.5
    if M[0, 0]: M = ~M
    ys = np.nonzero(M.any(1))[0]; top, bot = ys.min(), ys.max(); s = 1.85 / (bot - top)
    xs_ = np.nonzero(M.any(0))[0]; cx = (xs_.min() + xs_.max() + 1) / 2
    TB = []
    for z in np.arange(ZK, ZV + 0.001, 0.02):
        q = sechenie(z, P)
        if len(q) < 3: continue
        xs = np.nonzero(M[int(round(bot - z / s))])[0]; runs = np.split(xs, np.nonzero(np.diff(xs) > 1)[0] + 1)
        if len(runs) < 2: continue
        r = runs[0]; w, c = (r[-1] - r[0] + 1) * s, ((r[0] + r[-1] + 1) / 2 - cx) * s
        x0, x1 = q[:, 0].min(), q[:, 0].max(); TB.append(dict(z=round(float(z), 3), k=w / (x1 - x0), cl=c, cm=(x0 + x1) / 2, cy=float((q[:, 1].min() + q[:, 1].max()) / 2)))
    # сглаживание по высоте: маска в 4 мм на пиксель и изломы контура листа не должны стать ступенями сетки
    for f in ('k', 'cl', 'cm', 'cy'):
        v = np.array([t[f] for t in TB]); v2 = np.convolve(np.pad(v, 2, mode='edge'), np.ones(5) / 5, mode='valid')
        for t, q_ in zip(TB, v2): t[f] = float(q_)
    if opt.get('tablica'): json.dump(TB, open(opt['tablica'], 'w', encoding='utf-8'), ensure_ascii=False, indent=0)
    print('РУКА: k по z: ' + ' '.join('%.2f:%.2f' % (t['z'], t['k']) for t in TB))
Z = np.array([t['z'] for t in TB]); fld = {f: np.array([t[f] for t in TB]) for f in ('k', 'cl', 'cm', 'cy')}
g = lambda f, z: np.interp(z, Z, fld[f])
sel = (np.abs(P[:, 0]) > 0.15) & (P[:, 2] < ZG)
sg = np.sign(P[:, 0]); D = np.zeros_like(P); z_ = np.clip(P[:, 2], Z[0], Z[-1])
k, cl, cm, cy = g('k', z_), g('cl', z_), g('cm', z_), g('cy', z_)
# гашение к плечу: выше z_verh смещение уходит к нулю
w = np.clip((ZG - P[:, 2]) / max(ZG - ZV, 1e-6), 0, 1); w = w * w * (3 - 2 * w)
xr = P[:, 0] * sg * -1     # всё в осях правой руки (x < 0)
D[:, 0] = (cl + (xr - cm) * k - xr) * (-sg)
ky = 1 + (k - 1) * float(opt['k_y'])
D[:, 1] = (cy + (P[:, 1] - cy) * ky - P[:, 1])
D *= (w * sel)[:, None]
# швы подмышки стоят; их окрестность гасится разглаживанием смещения
bm = bmesh.new(); bm.from_mesh(o.data); kraj = np.zeros(n, bool); nb = [[] for _ in range(n)]
for e in bm.edges:
    x, y = e.verts[0].index, e.verts[1].index; nb[x].append(y); nb[y].append(x)
    if e.is_boundary: kraj[x] = kraj[y] = True
bm.free()
D[kraj] = 0
mz = P[:, 2] > 1.15
for _ in range(12):
    L = np.array([D[q].mean(0) if q else D[j] for j, q in enumerate(nb)]); D[mz] = 0.5 * D[mz] + 0.5 * L[mz]; D[kraj] = 0
d = np.linalg.norm(D, axis=1); print('РУКА: вершин сдвинуто %d, средн %.1f мм, макс %.1f мм' % (int((d > 1e-4).sum()), d[d > 1e-4].mean() * 1000, d.max() * 1000))
iw = Mw.inverted(); from mathutils import Vector
for vi in range(n):
    p = iw @ Vector(P[vi] + D[vi]); kb.data[vi].co = p
    if same: o.data.vertices[vi].co = p; o.data.shape_keys.key_blocks[0].data[vi].co = p
bpy.ops.wm.save_as_mainfile(filepath=out)
