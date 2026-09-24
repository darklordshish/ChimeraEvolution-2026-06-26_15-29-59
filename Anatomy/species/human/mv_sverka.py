# -*- coding: utf-8 -*-
"""3D-ОБРАЗЕЦ ПРОТИВ ЛИСТА — силуэты меша из multiview-генератора (Hunyuan3D-2mv по ракурсам листа) в ортопроекциях,
поверх силуэтов самого листа в тех же метрах.

Образец собирается из анфаса, спины и двух профилей листа (`ref/mv/`). Генератор примиряет расхождения ракурсов знанием
формы, поэтому сначала проверяется, что он не выдумал: силуэт образца в каждой ортопроекции против силуэта листа. Совпало —
образцу можно верить и в ракурсах, которых на листе нет (3/4 сзади — главный для камеры игрока).

Меш читается из GLB без сторонних пакетов (JSON-чанк + бинарный буфер), нормируется ростом листа (`ref_contours.json`),
стопы — на землю. Оси: Y вверх; перед образца — по +Z, после разворота `--yaw`.

Запуск:  python mv_sverka.py ref/mv/образец.glb [yaw]   → расхождение по поясам и out/mv-sverka.png
"""
import json
import os
import struct
import sys

import numpy as np
from PIL import Image, ImageDraw

import list_soglasie as S
import ref_trace as R

HERE = os.path.dirname(os.path.abspath(__file__))
PX = 0.004            # метров на пиксель растра образца — как у листа (3.9 мм)


def load_glb(path):
    """Все треугольники GLB в мировых координатах сцены (узлы с матрицами/TRS не ожидаются у генератора — проверяем)."""
    data = open(path, 'rb').read()
    n = struct.unpack_from('<I', data, 12)[0]
    doc = json.loads(data[20:20 + n])
    bin0 = 20 + n + 8
    buf = data[bin0:]

    def acc(i):
        a = doc['accessors'][i]
        v = doc['bufferViews'][a['bufferView']]
        off = v.get('byteOffset', 0) + a.get('byteOffset', 0)
        dt = {5126: np.float32, 5125: np.uint32, 5123: np.uint16}[a['componentType']]
        k = {'SCALAR': 1, 'VEC3': 3}[a['type']]
        return np.frombuffer(buf, dt, a['count'] * k, off).reshape(-1, k) if k > 1 else np.frombuffer(buf, dt, a['count'], off)

    Vs, Ts, base = [], [], 0
    for node in doc.get('nodes', []):
        if 'mesh' not in node:
            continue
        for p in doc['meshes'][node['mesh']]['primitives']:
            v = acc(p['attributes']['POSITION']).astype(float)
            if 'matrix' in node:
                M = np.array(node['matrix']).reshape(4, 4).T
                v = v @ M[:3, :3].T + M[:3, 3]
            elif 'rotation' in node or 'scale' in node or 'translation' in node:
                q = node.get('rotation', [0, 0, 0, 1]); x, y, z, w = q
                Rm = np.array([[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
                               [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
                               [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])
                v = (v * node.get('scale', [1, 1, 1])) @ Rm.T + node.get('translation', [0, 0, 0])
            t = acc(p['indices']).reshape(-1, 3).astype(int)
            Vs.append(v); Ts.append(t + base); base += len(v)
    return np.vstack(Vs), np.vstack(Ts)


def normalize(V, yaw):
    a = np.radians(yaw)
    Rm = np.array([[np.cos(a), 0, np.sin(a)], [0, 1, 0], [-np.sin(a), 0, np.cos(a)]])
    V = V @ Rm.T
    V = V - [0.5 * (V[:, 0].min() + V[:, 0].max()), V[:, 1].min(), 0.5 * (V[:, 2].min() + V[:, 2].max())]
    return V * (S.H / V[:, 1].max())


def raster(V, T, axis_u, sign):
    """Ортосилуэт: u = sign·V[axis_u] по горизонтали, y вверх. Маска и её начало в метрах."""
    u, y = sign * V[:, axis_u], V[:, 1]
    W = int((u.max() - u.min()) / PX) + 5
    Hh = int(y.max() / PX) + 5
    img = Image.new('L', (W, Hh), 0)
    d = ImageDraw.Draw(img)
    px = np.stack([(u - u.min()) / PX + 2, (y.max() - y) / PX + 2], 1)
    for t in T:
        d.polygon([tuple(px[i]) for i in t], fill=255)
    return np.asarray(img) > 0, u.min() - 2 * PX, y.max() + 2 * PX


def extents(mask, u0, ytop, h):
    row = int(round((ytop - h) / PX))
    if not 0 <= row < mask.shape[0]:
        return None
    xs = np.where(mask[row])[0]
    return (u0 + xs.min() * PX, u0 + (xs.max() + 1) * PX) if len(xs) else None


def main(path, yaw=0.0):
    V, T = load_glb(path)
    V = normalize(V, yaw)
    print('образец: %d вершин, %d треугольников; габарит X %.3f, Z %.3f, рост %.3f м'
          % (len(V), len(T), np.ptp(V[:, 0]), np.ptp(V[:, 2]), V[:, 1].max()))
    L = R.load()
    sheet = {n: S.view(L, n) for n in ('front', 'side')}
    fc = S.center(sheet['front'])
    ank = float(np.mean([(r[0][0] + r[-1][1] + 1) / 2 for h, r in sheet['side']['rows'].items() if 0.07 <= h <= 0.10 and r]))

    # анфас: камера с +Z, справа в кадре −X образца (как у листа: правая рука фигуры слева) → u = −X
    fm = raster(V, T, 0, -1)
    # профиль листа SIDE LEFT — лицом влево: вперёд (+Z) = влево → u = −Z; ось — середина голеностопа образца
    sm = raster(V, T, 2, -1)
    rows_ank = [extents(*sm, h) for h in np.arange(0.07, 0.10, PX)]
    oz = float(np.mean([(a + b) / 2 for a, b in rows_ank if a is not None]))

    out = {}
    for name, m, c_sheet, c_m in (('анфас', fm, fc, 0.0), ('профиль', sm, ank, oz)):
        v = sheet['front' if name == 'анфас' else 'side']
        rows = []
        for h in np.arange(0.02, S.H - 0.01, 0.01):
            e1 = S.extents(v, c_sheet, h, 1)
            e2 = extents(*m, h)
            if e1 and e2:
                rows.append((h, e1[0] - (e2[0] - c_m), e1[1] - (e2[1] - c_m)))
        out[name] = rows

    print('\nобразец против листа, расхождение крайних точек, см (среднее / макс):')
    print('%-14s %-18s %-18s' % ('пояс', 'анфас', 'профиль'))
    for lo, hi, name in S.BANDS:
        a, b = S.stats(out['анфас'], lo, hi), S.stats(out['профиль'], lo, hi)
        print('%-14s %5.1f / %-10.1f %5.1f / %-10.1f' % (name, a[0], a[1], b[0], b[1]))
    draw(L, sheet, fc, ank, fm, sm, oz)


def draw(L, sheet, fc, ank, fm, sm, oz):
    """Силуэт образца серым, контур листа цветом поверх: анфас слева, профиль справа."""
    Sz = 2
    img = Image.new('RGB', (2 * 260 * Sz, 520 * Sz), (38, 40, 46))
    d = ImageDraw.Draw(img)
    k = 480 * Sz / S.H

    def put_mask(m, u0, ytop, c, ox):
        ys, xs = np.nonzero(m)
        for y, x in zip(ys[::3], xs[::3]):
            u, h = u0 + x * PX - c, ytop - y * PX
            d.point((ox + u * k, 20 * Sz + (S.H - h) * k), fill=(120, 124, 132))

    def put_sheet(v, c, ox, col):
        for h, segs in v['rows'].items():
            for seg in segs:
                for x in (seg[0], seg[1] + 1):
                    d.point((ox + (x - c) * v['k'] * k, 20 * Sz + (S.H - h) * k), fill=col)

    put_mask(*fm, 0.0, 130 * Sz)
    put_mask(*sm, oz, 390 * Sz)
    put_sheet(sheet['front'], fc, 130 * Sz, (255, 90, 70))
    put_sheet(sheet['side'], ank, 390 * Sz, (80, 200, 255))
    img.save(os.path.join(HERE, 'out', 'mv-sverka.png'))


if __name__ == '__main__':
    main(sys.argv[1], float(sys.argv[2]) if len(sys.argv) > 2 else 0.0)
