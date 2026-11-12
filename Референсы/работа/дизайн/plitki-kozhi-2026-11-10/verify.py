"""Read-only measurements of delivered texture tiles; no image modification."""
import json
from pathlib import Path
import numpy as np
from PIL import Image

root = Path(__file__).resolve().parent
report = {}
for name in ('волк', 'лось', 'ёж', 'змея'):
    for kind in ('cvet', 'rel'):
        p = root / f'{name}-{kind}.png'
        im = Image.open(p).convert('RGB')
        a = np.asarray(im).astype(float)
        v = a.mean(axis=2)
        edge_x = abs(v[:, 0] - v[:, -1])
        edge_y = abs(v[0] - v[-1])
        interior_x = abs(np.diff(v, axis=1)).mean()
        interior_y = abs(np.diff(v, axis=0)).mean()
        suffix = '-3x3.png' if kind == 'cvet' else '-rel-3x3.png'
        tiled = np.asarray(Image.open(root / f'{name}{suffix}').convert('RGB'))
        exact_repeat = np.array_equal(tiled, np.tile(a.astype(np.uint8), (3, 3, 1)))
        report[p.name] = {
            'size': im.size,
            'mean': round(float(v.mean()), 2),
            'min_max': [int(v.min()), int(v.max())],
            'max_rgb_channel_difference': int((a.max(axis=2)-a.min(axis=2)).max()),
            'edge_x_mean_max': [round(float(edge_x.mean()), 2), round(float(edge_x.max()), 2)],
            'edge_y_mean_max': [round(float(edge_y.mean()), 2), round(float(edge_y.max()), 2)],
            'interior_x_y_mean': [round(float(interior_x), 2), round(float(interior_y), 2)],
            'repeat_is_exact_3x3': exact_repeat,
        }
        assert im.size == (1024, 1024)
        assert exact_repeat
print(json.dumps(report, ensure_ascii=False, indent=2))
(root / 'verification.json').write_text(json.dumps(report, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
