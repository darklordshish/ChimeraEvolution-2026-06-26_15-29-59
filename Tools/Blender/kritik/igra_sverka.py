# -*- coding: utf-8 -*-
"""ИГРОВОЕ ТЕЛО ПРОТИВ 3D-ОБРАЗЦА — силуэт кадра стенда `Kadr.Shot` (настоящий `MorphBuilder`, ортокамера) против
ортосилуэта образца (GLB из multiview-генератора) С ТОЙ ЖЕ КАМЕРОЙ. Для ракурсов, которых на листе нет (3/4 сзади —
камера игрока), это единственная сверка.

Камера — как в `Kadr.cs`: profile — глаз +X; nose — глаз +Z; yaw:<угол>:<наклон> — глаз (sin·cos, sin наклона, cos·cos);
вперёд = −глаз, право = up × вперёд, верх = вперёд × право (Unity, левая система). Образец приводится к осям Unity:
X glTF → −X (импорт glTF в Unity зеркалит X), морда по +Z, Y вверх.

Масштаб: образец без метров, поэтому подбирается лучший в ±15 % от высоты фигуры в кадре; итог печатается — далёкий от 1
значит, что пропорции тела расходятся сильнее, чем высота. Кадр стенда — фон ровный, фигура — всё, что отличается от фона.

Запуск:  python igra_sverka.py образец.glb 'вид=кадр.png' ['вид=кадр.png' …] [out.png]
         вид: profile | nose | yaw:145:12
"""
import os
import sys

import numpy as np
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, '..', '..', '..', 'Anatomy', 'species', 'human'))
import soglasie as SG                                   # noqa: E402
from mv_sverka import load_glb                          # noqa: E402


def eye_of(view):
    if view == 'profile':
        return np.array([1.0, 0, 0])
    if view == 'nose':
        return np.array([0, 0, 1.0])
    if view == 'back':
        return np.array([0, 0, -1.0])
    p = view.split(':')
    y, t = np.radians(float(p[1])), np.radians(float(p[2]) if len(p) > 2 else 0.0)
    return np.array([np.sin(y) * np.cos(t), np.sin(t), np.cos(y) * np.cos(t)])


def basis(view):
    f = -eye_of(view)
    r = np.cross([0.0, 1.0, 0.0], f); r /= np.linalg.norm(r)
    return r, np.cross(f, r)


def silhouette(V, T, view, H):
    r, u = basis(view)
    x, y = V @ r, V @ u
    k = (H - 1) / (y.max() - y.min())
    im = Image.new('L', (int((x.max() - x.min()) * k) + 3, H + 2))
    d = ImageDraw.Draw(im)
    px = np.stack([(x - x.min()) * k + 1, (y.max() - y) * k + 1], 1)
    for t in T:
        d.polygon([tuple(px[i]) for i in t], fill=255)
    m = np.asarray(im) > 0
    ys = np.where(m.any(1))[0]
    return m[ys.min():ys.max() + 1]


def game_mask(path):
    a = np.asarray(Image.open(path).convert('RGB')).astype(int)
    m = np.abs(a - a[3, 3]).sum(2) > 25
    ys, xs = np.where(m)
    return m[ys.min():ys.max() + 1, xs.min():xs.max() + 1]


def main(glb, shots, out=None):
    V, T = load_glb(glb)
    V = V * [-1, 1, 1]
    img = []
    for view, path in shots:
        g = game_mask(path)
        best = None
        for sc in np.linspace(0.85, 1.15, 13):
            m = silhouette(V, T, view, int(round(g.shape[0] * sc)))
            r = SG.pair(g, m[:, ::-1])                  # pair зеркалит второй — здесь зеркало не нужно
            if best is None or r[0] > best[1][0]:
                best = (sc, r)
        sc, (iou, rows, P, Q) = best
        print('%-11s масштаб %.3f  IoU %.3f   ' % (view, sc, iou) + '  '.join(
            '%s %.1f / %.1f %%' % (n, 100 * a / len(P), 100 * b / len(P)) for n, a, b in SG.bands(rows, len(P))))
        rgb = np.zeros(P.shape + (3,), np.uint8) + 30
        rgb[P & ~Q] = (230, 80, 60); rgb[Q & ~P] = (240, 200, 60); rgb[P & Q] = (130, 134, 142)
        img.append(rgb)
    if out:
        h = max(i.shape[0] for i in img)
        can = np.zeros((h, sum(i.shape[1] for i in img) + 20 * len(img), 3), np.uint8) + 30
        x = 0
        for i in img:
            can[h - i.shape[0]:, x:x + i.shape[1]] = i; x += i.shape[1] + 20
        im = Image.fromarray(can)
        im.thumbnail((2400, 900))
        im.save(out)
        print('наложение: игра красным, образец жёлтым, общее серым →', out)


if __name__ == '__main__':
    a = sys.argv[2:]
    out = a.pop() if a and '=' not in a[-1] else None
    main(sys.argv[1], [tuple(s.split('=', 1)) for s in a], out)
