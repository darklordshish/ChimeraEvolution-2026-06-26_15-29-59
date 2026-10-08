"""МАСКА С ЛИСТА ВИТРИНЫ (спека `2026-10-08-hranilishche-referensov.md`, решение 7: маски — производные листа).

Вырезает панель листа, отделяет фигуру от светлого фона порогом, оставляет самую крупную связную область и заливает
дырки. Сменился лист — маски пересняты этим же скриптом, руками не правятся.

    python Docs/tools/sheet_mask.py <лист.png> <x0,y0,x1,y1> <выход.png> [порог=195]
"""
import sys
import numpy as np
from PIL import Image
from scipy import ndimage

src, box, out = sys.argv[1], [int(v) for v in sys.argv[2].split(",")], sys.argv[3]
thr = int(sys.argv[4]) if len(sys.argv) > 4 else 195
g = np.array(Image.open(src).convert("L").crop(box)).astype(int)
fg = g < thr
lab, n = ndimage.label(fg)
if n == 0:
    sys.exit("фигура не найдена — проверь рамку и порог")
sizes = ndimage.sum(fg, lab, range(1, n + 1))
fig = lab == (1 + int(np.argmax(sizes)))
# ЗАЛИВАЮТСЯ ТОЛЬКО МЕЛКИЕ ДЫРЫ (блики на гранях): просвет под брюхом и между лап — часть силуэта, а тень на земле
# замыкает его в «дыру», и сплошная заливка сделала бы из волка параллелепипед
holes, k = ndimage.label(ndimage.binary_fill_holes(fig) & ~fig)
if k:
    hs = ndimage.sum(np.ones_like(fig), holes, range(1, k + 1))
    fig |= np.isin(holes, 1 + np.where(hs < 0.002 * fig.size)[0])
ys, xs = np.where(fig)
fig = fig[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
h, w = fig.shape
side = int(max(h, w) * 1.1)
canvas = np.zeros((side, side), bool)               # фигура по центру по ширине, низ — на земле (как маски дизайн-линии)
canvas[side - h - (side - h) // 20: side - (side - h) // 20, (side - w) // 2:(side - w) // 2 + w] = fig
Image.fromarray(np.where(canvas, 0, 255).astype(np.uint8)).resize((512, 512), Image.NEAREST).save(out)
print(out, "фигура", w, "x", h, "px")
