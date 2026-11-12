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
edges[round(source.height * .81):] = False
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

# Кропы задаются по просмотренным листам; тень и подписи не считаются телом.
workspace = ROOT.parents[3]
main = workspace.parent / 'ChimeraEvolution'
refpath = main / 'Референсы/витрина/ёж/вид.png'
ref = Image.open(refpath).convert('RGB')
views = [
    ('FRONT', (804, 77, 1169, 502), (33, 176, 310, 528)),
    ('PROFILE', (39, 76, 751, 504), (322, 193, 851, 528)),
    ('FRONT 3/4', (22, 565, 497, 888), (852, 188, 1300, 528)),
    ('BACK', (1260, 73, 1635, 505), (1310, 176, 1586, 528))]

def candidate_box(box):
    return tuple(round(v*(source.width/2048 if i%2==0 else source.height/683))
                 for i,v in enumerate(box))

views = [(name,rb,candidate_box(nb)) for name,rb,nb in views]

def fit_height(im, height=290):
    return im.resize((round(im.width*height/im.height), height))

comparison = Image.new('RGB', (1300, len(views)*350), 'white')
cd = ImageDraw.Draw(comparison)
overlay = Image.new('RGB', (650, len(views)*350), 'white')
od = ImageDraw.Draw(overlay)
metrics = []
for i, (name, rb, nb) in enumerate(views):
    r, n = fit_height(ref.crop(rb)), fit_height(source.crop(nb))
    y = i*350
    cd.text((15, y+8), name+' | VITRINA (left) / LOWPOLY (right)', fill='black')
    comparison.paste(r, ((640-r.width)//2, y+35))
    comparison.paste(n, (650+(640-n.width)//2, y+35))
    # Удаление фона — порог + крупнейшая связная компонента, затем заполнение дыр.
    def silhouette(im):
        m = np.asarray(im.convert('L')) < 205
        lab, _ = ndi.label(m)
        sizes = np.bincount(lab.ravel()); sizes[0] = 0
        body = lab == sizes.argmax()
        holes, _ = ndi.label(~body)
        hs = np.bincount(holes.ravel())
        small = hs < 80; small[0] = False
        return body | small[holes]
    rm, nm = silhouette(r), silhouette(n)
    rw = np.zeros((300, 620), bool); nw = rw.copy()
    rw[:290, (620-r.width)//2:(620-r.width)//2+r.width] = rm
    nw[:290, (620-n.width)//2:(620-n.width)//2+n.width] = nm
    pixels = np.full((300,620,3),255,np.uint8)
    pixels[rw] = (70,155,220); pixels[nw] = (235,135,70)
    pixels[rw & nw] = (85,85,85)
    overlay.paste(Image.fromarray(pixels),(15,y+30))
    od.text((15,y+8), name+' | blue=vitrina orange=lowpoly gray=overlap',fill='black')
    metrics.append({'view':name,'reference_crop':rb,'candidate_crop':nb,
        'width_ratio_at_equal_total_height':round(n.width/r.width,3),
        'approximate_mask_iou':round(float((rw&nw).sum()/(rw|nw).sum()),3)})
comparison.save(ROOT/'03-compare-vitrina.png')
overlay.save(ROOT/'04-silhouette-overlay.png')
for name, box in {'profile-torso':(555,346,687,446),
                  'front-torso':(82,412,260,485),
                  'back-torso':(1330,277,1564,469),
                  'front-head':(105,311,239,424)}.items():
    crop=source.crop(candidate_box(box))
    crop.resize((crop.width*4,crop.height*4)).save(ROOT/(name+'.png'))
assert np.array_equal(result[~edges],a[~edges])
files=['01-flat-sheet.png','02-edges-sheet.png','03-compare-vitrina.png',
       '04-silhouette-overlay.png','edges-mask.png']
for filename in files:
    with Image.open(ROOT/filename) as im: im.verify()
report={'size':list(source.size),'edge_pixels':int(edges.sum()),
        'unchanged_outside_edges':True,'png_verified':files,
        'silhouette_method':'manual crops; equal total height incl launcher; centered; largest component L<205, fill holes smaller than 80 px only; leg gaps retained; ground shadows may remain; approximate only',
        'silhouette_comparison':metrics,
        'sha256':{f:hashlib.sha256((ROOT/f).read_bytes()).hexdigest() for f in files}}
(ROOT/'verification.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
paths=[refpath,main/'Референсы/витрина/ёж/паспорт.md',
       main/'Референсы/работа/дизайн/lowpoly-2k-chelovek-2026-11-04/01-flat-sheet.png']
(ROOT/'sources.json').write_text(json.dumps([{'path':str(p),
    'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in paths],
    ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(report,indent=2))



