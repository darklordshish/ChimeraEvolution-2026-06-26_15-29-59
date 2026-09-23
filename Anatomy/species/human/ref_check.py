# -*- coding: utf-8 -*-
"""СВЕРКА С ЛИСТОМ — контур референс-листа поверх ортокадра нашей оболочки, в одном масштабе.

Вторая половина метода «болванка по ортопроекциям» (первая — `ref_trace.py`): лоу-поли по референсу проверяют наложением
на ту же подложку. Кадры снимает Unity-стенд (`ProbeM.Shot`: ортокамера по оси, `half` — полувысота кадра в метрах,
центр — `cx, cy`), контур листа — в тех же метрах. Анфас (`nose`): справа в кадре −X; профиль (`profile`): справа +Z.

Руки и ноги по длине расходятся с листом НАМЕРЕННО (суставы по данным, `graph.py`): сверять их надо долей сегмента,
а не высотой. Торс и голова совпадают с листом по высоте — их контур прямая мера.

Запуск:  python ref_check.py кадр.png nose|profile cx cy half [out.png]
"""
import sys

from PIL import Image, ImageDraw

import graph as G


def to_px(img, cx, cy, half, u, y):
    s = img.height / (2 * half)
    return img.width / 2 + (u - cx) * s, img.height / 2 - (y - cy) * s


def draw(path, view, cx, cy, half, out):
    img = Image.open(path).convert('RGB')
    d = ImageDraw.Draw(img)
    v = G.views()['front' if view == 'nose' else 'side']
    for r in v['rows']:
        if view == 'nose':
            for a, b in r['x']:
                for x in (a, b):
                    d.point(to_px(img, -cx, cy, half, x, r['y']), fill=(255, 70, 50))   # x листа — вправо в кадре, как у нас
        else:
            for a, b in r['z']:
                for z in (a, b):
                    d.point(to_px(img, cx, cy, half, z + G.DZ, r['y']), fill=(60, 200, 255))
    img.save(out)
    print(out)


if __name__ == '__main__':
    p, view, cx, cy, half = sys.argv[1], sys.argv[2], float(sys.argv[3]), float(sys.argv[4]), float(sys.argv[5])
    draw(p, view, cx, cy, half, sys.argv[6] if len(sys.argv) > 6 else p.replace('.png', '-ref.png'))
