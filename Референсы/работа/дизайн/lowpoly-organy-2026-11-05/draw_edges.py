"""Requested tone-boundary overlays; no geometry reconstruction or repainting.

Run with C:/ProgramData/Anaconda3/python.exe <this file>.
"""
from pathlib import Path
import hashlib
import json
import numpy as np
from PIL import Image
from scipy import ndimage as ndi

ROOT = Path(__file__).resolve().parent
WORKSPACE = ROOT.parents[3]
NAMES = ('volk', 'los', 'ezh', 'zmeya')

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def outline(path):
    with Image.open(path) as im:
        source = im.convert('RGB')
    a = np.asarray(source)
    gray = np.asarray(source.convert('L'), dtype=float)
    smooth = ndi.gaussian_filter(gray, .65)
    gx = ndi.sobel(smooth, axis=1) / 8
    gy = ndi.sobel(smooth, axis=0) / 8
    mag = np.hypot(gx, gy)
    angle = (np.degrees(np.arctan2(gy, gx)) + 180) % 180
    thin = np.zeros_like(mag, dtype=bool)
    for lo, hi, shift in ((0, 22.5, (0, 1)), (22.5, 67.5, (1, 1)),
                          (67.5, 112.5, (1, 0)), (112.5, 157.5, (1, -1)),
                          (157.5, 180, (0, 1))):
        p = np.roll(mag, shift, axis=(0, 1))
        n = np.roll(mag, (-shift[0], -shift[1]), axis=(0, 1))
        thin |= (angle >= lo) & (angle < hi) & (mag >= p) & (mag >= n)
    edges = ndi.binary_propagation(thin & (mag > 4.5),
                                   mask=thin & (mag > 2.1),
                                   structure=np.ones((3, 3)))
    edges[:2] = edges[-2:] = False
    edges[:, :2] = edges[:, -2:] = False
    labels, count = ndi.label(edges, structure=np.ones((3, 3)))
    keep = np.bincount(labels.ravel()) >= 7
    keep[0] = False
    edges = keep[labels]
    result = a.copy()
    result[edges] = [42, 42, 42]
    target = path.with_stem(path.stem + '-rebra')
    mask = path.with_stem(path.stem + '-rebra-mask')
    Image.fromarray(result).save(target)
    Image.fromarray((edges * 255).astype('uint8')).save(mask)
    reloaded = np.asarray(Image.open(target).convert('RGB'))
    assert np.array_equal(reloaded[~edges], a[~edges])
    assert source.size == Image.open(target).size
    return {'sheet': path.name, 'size': list(source.size),
            'edge_pixels': int(edges.sum()), 'unchanged_outside_edges': True,
            'sha256': {p.name: digest(p) for p in (path, target, mask)}}

reports = [outline(ROOT / (name + '.png')) for name in NAMES]
pngs = sorted(ROOT.glob('*.png'))
for p in pngs:
    with Image.open(p) as im:
        im.verify()
vitrina = WORKSPACE.parent / 'ChimeraEvolution/Референсы/витрина'
sources = []
for species in ('человек', 'волк', 'лось', 'ёж', 'змея'):
    for filename in (('огранка.png',) if species == 'человек' else ('огранка.png', 'вид.png')):
        p = vitrina / species / filename
        with Image.open(p) as im:
            size = list(im.size)
        sources.append({'path': str(p), 'size': size, 'sha256': digest(p)})
(ROOT / 'sources.json').write_text(json.dumps(sources, ensure_ascii=False, indent=2), encoding='utf-8')
report = {'method': 'Sobel / nonmaximum suppression / hysteresis, visible tone boundaries only',
          'parameters': {'sigma': .65, 'weak': 2.1, 'strong': 4.5, 'min_component': 7},
          'sheets': reports, 'verified_png_count': len(pngs),
          'scope': 'File integrity and overlay identity only; not organ completeness, topology or 4m readability.'}
(ROOT / 'verification.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps(report, ensure_ascii=True, indent=2))
