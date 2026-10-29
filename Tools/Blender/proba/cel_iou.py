"""IoU 3D-ЦЕЛИ С МАСКАМИ ЛИСТА (29.10, выбор сида цели новой ст.2 мерой, а не на глаз). Цель (OBJ в осях тракта: z вверх, лицо к −y)
проецируется ортогонально — анфас (x, z), профиль (y, z), сзади (−x, z) — и укладывается в квадрат, как маски `Docs/tools/sheet_mask.py`
(фигура по центру по ширине, низ на земле, сторона 1.1 × больший размер, 512 px). Профиль сравнивается в обе стороны.

  python cel_iou.py цель.obj --front f.png --profile p.png [--back b.png] [--kadr префикс]
"""
import sys
import numpy as np
from PIL import Image, ImageDraw

a = sys.argv[1:]
src = a[0]; opt = {}
i = 1
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
V, F = [], []
for ln in open(src, encoding='utf-8', errors='ignore'):
    if ln.startswith('v '): V.append([float(x) for x in ln.split()[1:4]])
    elif ln.startswith('f '):
        ix = [int(t.split('/')[0]) - 1 for t in ln.split()[1:]]
        F += [[ix[0], ix[k], ix[k + 1]] for k in range(1, len(ix) - 1)]
V = np.array(V); F = np.array(F)
# OBJ тракта пишется Y-вверх (obj_import forward −Z, up Y): x, y_obj = z тракта, z_obj = −y тракта
P = np.c_[V[:, 0], -V[:, 2], V[:, 1]]


def silujet(u, v):
    S = 2048; lo, hi = np.array([u.min(), v.min()]), np.array([u.max(), v.max()])
    k = (S - 1) / (hi - lo).max(); im = Image.new('L', (S, S), 0); d = ImageDraw.Draw(im)
    X = (u - lo[0]) * k; Y = (hi[1] - v) * k
    for t in F: d.polygon([(X[j], Y[j]) for j in t], fill=255)
    m = np.asarray(im) > 0; ys, xs = np.nonzero(m); m = m[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
    h, w = m.shape; side = int(max(h, w) * 1.1); c = np.zeros((side, side), bool)
    c[side - h - (side - h) // 20: side - (side - h) // 20, (side - w) // 2:(side - w) // 2 + w] = m
    return np.asarray(Image.fromarray(c).resize((512, 512), Image.NEAREST))


def maska(p): return np.asarray(Image.open(p).convert('L')) < 128
def iou(s, m): return float((s & m).sum() / max((s | m).sum(), 1))


rez = {}
for nm, (u, v) in (('front', (P[:, 0], P[:, 2])), ('profile', (P[:, 1], P[:, 2])), ('back', (-P[:, 0], P[:, 2]))):
    if nm not in opt: continue
    s, m = silujet(u, v), maska(opt[nm])
    if nm == 'profile' and iou(s[:, ::-1], m) > iou(s, m): s = s[:, ::-1]
    rez[nm] = round(iou(s, m), 3)
    if opt.get('kadr'):
        rgb = np.ones((512, 512, 3)); rgb[s & ~m] = (0.9, 0.2, 0.2); rgb[m & ~s] = (0.3, 0.5, 0.95); rgb[s & m] = (0.15, 0.15, 0.15)
        Image.fromarray((rgb * 255).astype(np.uint8)).save('%s_%s.png' % (opt['kadr'], nm))
print('IoU ЦЕЛИ: %s — %s' % (src.replace('\\', '/').split('/')[-1], ', '.join('%s %.3f' % kv for kv in rez.items())))
