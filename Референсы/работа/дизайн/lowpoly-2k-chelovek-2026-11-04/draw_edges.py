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
edges[round(source.height * .93):] = False
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
# Крупно для ручной проверки границ и подсчёта.
boxes = {'front-torso': (95, 156, 336, 373),
         'back-torso': (1110, 151, 1350, 368),
         'front-head': (177, 20, 260, 140)}
for name, box in boxes.items():
    source.crop(box).resize(((box[2]-box[0])*3, (box[3]-box[1])*3)).save(ROOT / (name + '.png'))
    Image.fromarray(result).crop(box).resize(((box[2]-box[0])*3, (box[3]-box[1])*3)).save(ROOT / (name + '-edges.png'))
assert np.array_equal(result[~edges], a[~edges])
for filename in ['01-flat-sheet.png', '02-edges-sheet.png', 'edges-mask.png']:
    with Image.open(ROOT / filename) as im:
        im.verify()
report = {'size': list(source.size), 'edge_pixels': int(edges.sum()),
          'unchanged_outside_edges': True,
          'sha256': {f.name: hashlib.sha256(f.read_bytes()).hexdigest()
                     for f in [ROOT / '01-flat-sheet.png', ROOT / '02-edges-sheet.png']},
          'method': 'Sobel + nonmaximum suppression + hysteresis; visible tone boundaries only',
          'thresholds': {'weak': 2.1, 'strong': 4.5, 'sigma': .65}}
(ROOT / 'verification.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
# Сравнение крупных масс при одинаковой высоте фигуры.
workspace = ROOT.parents[3]
main = workspace.parent / 'ChimeraEvolution'
refs = [main / 'Референсы/витрина/человек/вид.png',
        main / 'Референсы/работа/дизайн/linii-podzharyj-2026-10-30/01-lines-sheet.png',
        workspace.parent / 'ChimeraEvolution-modeli/Референсы/работа/модельная/2026-11-04-r9/ryad-krupno.png']
manifest = [{'path': str(p), 'sha256': hashlib.sha256(p.read_bytes()).hexdigest(),
             'size': list(Image.open(p).size)} for p in refs]
(ROOT / 'sources.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
vitrina = Image.open(refs[0]).convert('RGB')
comparison = Image.new('RGB', (1080, 740), 'white')
draw = ImageDraw.Draw(comparison)
for index, (im, box, title) in enumerate([
        (vitrina, (48, 34, 376, 906), 'VITRINA FRONT'),
        (source, (55, 22, 386, 831), 'LOWPOLY FRONT'),
        (vitrina, (1146, 34, 1490, 906), 'VITRINA BACK'),
        (source, (1060, 22, 1405, 831), 'LOWPOLY BACK')]):
    panel = im.crop(box)
    panel = panel.resize((round(panel.width * 670 / panel.height), 670))
    comparison.paste(panel, (index * 270 + (270-panel.width)//2, 35))
    draw.text((index * 270 + 20, 10), title, fill='black')
comparison.save(ROOT / '03-compare-vitrina.png')
print(json.dumps(report, indent=2))
