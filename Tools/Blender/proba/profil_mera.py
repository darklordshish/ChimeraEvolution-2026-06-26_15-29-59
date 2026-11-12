"""МЕРА ПРОФИЛЯ ПО ЛИСТУ (03.11, критик r6: «глубина корпуса распределена неверно; срезы не показывают, спереди это или сзади»). Контур модели в профиле (пересечения рёбер всех объектов с плоскостью высоты, включая руку) против маски профиля: перед и зад по высотам, см; плюс — модель выступает за лист. Совмещение по затылку на z 1.76, как в прочих сверках. Пишет таблицу в json для `korpus_po_profilyu.py`.

  blender -b вход.blend --factory-startup -P profil_mera.py -- vid-profile.png [выход.json] [--imya Поджарый]
"""
import sys, json, os
import bpy
import numpy as np

a = sys.argv[sys.argv.index('--') + 1:]
maska = a[0]; out = a[1] if len(a) > 1 and not a[1].startswith('--') else None
KEY = a[a.index('--imya') + 1] if '--imya' in a else 'Поджарый'
im = bpy.data.images.load(os.path.abspath(maska)); w_, h_ = im.size; px = np.array(im.pixels[:]).reshape(h_, w_, -1)[::-1]; M = px[:, :, 0] > 0.5
if M[0, 0]: M = ~M
ys = np.nonzero(M.any(1))[0]; bot = ys.max(); s = 1.85 / (ys.max() - ys.min())


def rw(z):
    r = np.nonzero(M[int(round(bot - z / s))])[0]; return r.min() * s, (r.max() + 1) * s


D = []
for o in bpy.data.objects:
    if o.type != 'MESH' or '@' in o.name or not o.data.shape_keys or KEY not in o.data.shape_keys.key_blocks: continue
    P = np.array([tuple(o.matrix_world @ k.co) for k in o.data.shape_keys.key_blocks[KEY].data]); E = np.array([tuple(e.vertices) for e in o.data.edges]); D.append((P, E))


def sec(z, xmax=None):
    q = []
    for P, E in D:
        za, zb = P[E[:, 0], 2], P[E[:, 1], 2]; m = (za - z) * (zb - z) < 0
        if m.any():
            t = ((z - za[m]) / (zb[m] - za[m]))[:, None]; c = P[E[m, 0]] + (P[E[m, 1]] - P[E[m, 0]]) * t
            if xmax: c = c[np.abs(c[:, 0]) < xmax]
            q.append(c[:, 1])
    q = np.concatenate(q) if q else np.zeros(0); return q.min(), q.max()


off = sec(1.76)[1] - rw(1.76)[1]; R = []
for z in np.arange(0.90, 1.62, 0.03):
    f, b = sec(z); lf, lb = rw(z); lf += off; lb += off; R.append(dict(z=round(float(z), 3), pered=float(lf - f), zad=float(b - lb)))
    print('P z %.2f (%2.0f%%) | лист %.3f…%.3f | модель %.3f…%.3f | перед %+.1f см, зад %+.1f см' % (z, (1 - z / 1.85) * 100, lf, lb, f, b, (lf - f) * 100, (b - lb) * 100))
if out: json.dump(R, open(out, 'w', encoding='utf-8'), ensure_ascii=False)
