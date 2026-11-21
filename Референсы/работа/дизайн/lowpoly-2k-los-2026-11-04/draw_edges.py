"""Границы видимых плоскостей исходного растра, не реконструкция сетки.

Запуск из любой папки: C:/ProgramData/Anaconda3/python.exe <этот файл>
"""
from pathlib import Path
import hashlib
import json
import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage as ndi

ROOT = Path(__file__).resolve().parent
source = Image.open(ROOT / '01-flat-sheet.png').convert('RGB')
a = np.asarray(source)
gray = np.asarray(source.convert('L'), dtype=float)
smooth = ndi.gaussian_filter(gray, 0.65)
gx = ndi.sobel(smooth, axis=1) / 8
gy = ndi.sobel(smooth, axis=0) / 8
mag = np.hypot(gx, gy)
angle = (np.degrees(np.arctan2(gy, gx)) + 180) % 180
thin = np.zeros_like(mag, dtype=bool)
for lo, hi, shift in [(0, 22.5, (0, 1)), (22.5, 67.5, (1, 1)),
                      (67.5, 112.5, (1, 0)), (112.5, 157.5, (1, -1)),
                      (157.5, 180, (0, 1))]:
    sector = (angle >= lo) & (angle < hi)
    p = np.roll(mag, shift, axis=(0, 1))
    n = np.roll(mag, (-shift[0], -shift[1]), axis=(0, 1))
    thin |= sector & (mag >= p) & (mag >= n)
weak = thin & (mag > 2.1)
strong = thin & (mag > 4.5)
edges = ndi.binary_propagation(strong, mask=weak, structure=np.ones((3, 3)))
# Текст и линия земли не являются рёбрами фигуры.
edges[round(source.height * .814):] = False
edges[:2] = False
edges[:, :2] = False
edges[:, -2:] = False
labels, count = ndi.label(edges, structure=np.ones((3, 3)))
sizes = np.bincount(labels.ravel())
keep = sizes >= 7
keep[0] = False
edges = keep[labels]
result = a.copy()
result[edges] = [42, 42, 42]
Image.fromarray(result).save(ROOT / '02-edges-sheet.png')
mask = Image.fromarray((edges * 255).astype('uint8'))
mask.save(ROOT / 'edges-mask.png')

# Убираем рамки панелей: это оформление, не грани.
# Основной фильтр рамок выполняется ниже перед повторным сохранением.
for fx in [12, 391, 416, 829, 854, 1224, 1248, 1607, 1633, 2038]:
    x = round(fx / 2048 * source.width)
    edges[:, max(0,x-2):x+3] = False
edges[:round(55/683*source.height)] = False
result = a.copy()
result[edges] = [42,42,42]
Image.fromarray(result).save(ROOT/'02-edges-sheet.png')
Image.fromarray(np.uint8(edges)*255).save(ROOT/'edges-mask.png')
assert np.array_equal(result[~edges], a[~edges])
for name, box in {'profile-torso':(.25,.267,.396,.535), 'front-head':(.08,.22,.12,.407)}.items():
    box=tuple(round(v*(source.width if i%2==0 else source.height)) for i,v in enumerate(box))
    crop=source.crop(box); crop.resize((crop.width*3,crop.height*3)).save(ROOT/(name+'.png'))
report={'size':list(source.size),'edge_pixels':int(edges.sum()),'unchanged_outside_edges':True,'method':'Sobel + nonmaximum suppression + hysteresis; visible tone boundaries only','thresholds':{'weak':2.1,'strong':4.5,'sigma':.65},'sha256':{f.name:hashlib.sha256(f.read_bytes()).hexdigest() for f in [ROOT/'01-flat-sheet.png',ROOT/'02-edges-sheet.png']}}
for filename in ['01-flat-sheet.png','02-edges-sheet.png','edges-mask.png']:
    with Image.open(ROOT/filename) as im: im.verify()
(ROOT/'verification.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps(report,indent=2))
