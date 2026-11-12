"""МЕТКИ ГРАНЕЙ С ЛИСТА ОГРАНКИ (04.11, `витрина/человек/огранка-рёбра.png`, r10). Рёбра листа — тёмные линии; связные куски между ними — области-грани. Каждый ракурс (анфас, сзади, профиль) режется по коробке в свою карту меток; куски мельче `--min` пикселей растворяются в соседях. Пишет `metki.npz` (карты, коробки и медианный тон каждой области по `огранка.png`) и `metki.png` для проверки глазом. Линии не копируются вершина к вершине: по ним режется тело (`ogranka_telo.py`).

  python ogranka_metki.py огранка-рёбра.png папка [--porog 70] [--min 40]
"""
import sys, os
import numpy as np
from PIL import Image
from scipy import ndimage as ndi

a = sys.argv[1:]; SRC, OUT = a[0], a[1]; os.makedirs(OUT, exist_ok=True)
POR = int(a[a.index('--porog') + 1]) if '--porog' in a else 70; MN = int(a[a.index('--min') + 1]) if '--min' in a else 40
g = np.asarray(Image.open(SRC).convert('L')).astype(int)
TON = np.asarray(Image.open(os.path.join(os.path.dirname(SRC), 'огранка.png')).convert('L')).astype(float) / 255  # тон грани — без линий чертежа
# коробки ракурсов по x: столбцы листа; низ — линия земли, верх — макушка
KOR = dict(front=(40, 400), profile=(450, 640), back=(1050, 1420))
R = {}; vis = np.zeros(g.shape + (3,), np.uint8) + 255
rng = np.random.default_rng(3)
for k, (x0, x1) in KOR.items():
    s = g[:826, x0:x1]; fig = ndi.binary_fill_holes(ndi.binary_closing(s < 235, iterations=2)); ln = (s < POR) & fig
    ln = ndi.binary_dilation(ln, iterations=1); L, n = ndi.label(fig & ~ln)
    sz = ndi.sum(np.ones_like(L), L, np.arange(n + 1)); mal = np.isin(L, np.nonzero(sz < MN)[0]) & (L > 0)
    if mal.any() or (fig & (L == 0)).any():
        # линии и мелочь уходят ближайшей большой метке
        L[mal] = 0; ind = ndi.distance_transform_edt(L == 0, return_distances=False, return_indices=True); L = np.where(fig, L[tuple(ind)], 0)
    R[k + '_ton'] = ndi.median(TON[:826, x0:x1], L, np.arange(L.max() + 1)); ys, xs = np.nonzero(fig); R[k] = L; R[k + '_kor'] = np.array([x0, x1, ys.min(), ys.max(), xs.min() + x0, xs.max() + x0])
    cv = rng.integers(60, 255, (L.max() + 1, 3)); cv[0] = 255; vis[:826, x0:x1] = cv[L]
    print('МЕТКИ %s: областей %d, фигура y %d…%d, x %d…%d' % (k, len(np.unique(L)) - 1, ys.min(), ys.max(), xs.min() + x0, xs.max() + x0))
np.savez_compressed(os.path.join(OUT, 'metki.npz'), **R); Image.fromarray(vis).save(os.path.join(OUT, 'metki.png'))
