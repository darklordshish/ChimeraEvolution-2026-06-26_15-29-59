# -*- coding: utf-8 -*-
"""СЕТКА НА РЕФЕРЕНС — чтобы снимать точки числами, а не на глаз.

Зачем. Правило методики «эталон снимается с натуры, а не собирается из общих соображений» требует
инструмента: без разметки чтение координат с картинки — то же самое гадание, только с референсом
в руках. С сеткой сустав называется парой чисел, и эти числа воспроизводимы.

Как. Кладём линии с подписями поверх копии изображения (оригинал не трогаем) и по ним читаем.
Дальше пиксели переводятся в метры по ДВУМ известным точкам вида — обычно земля и холка.

Запуск:  python grid.py <путь к картинке> [--step 100] [--out файл]
"""
import argparse
import os
from PIL import Image, ImageDraw

AP = argparse.ArgumentParser()
AP.add_argument('image')
AP.add_argument('--step', type=int, default=100, help='шаг крупной линии, пикселей')
AP.add_argument('--out', default='', help='куда писать (по умолчанию рядом, с суффиксом _grid)')
A = AP.parse_args()

im = Image.open(A.image).convert('RGB')
W, H = im.size
d = ImageDraw.Draw(im, 'RGBA')

# Мелкая сетка — половина шага, без подписей: помогает делить на глаз, но не засоряет числами
for x in range(0, W, A.step // 2):
    d.line([(x, 0), (x, H)], fill=(255, 90, 60, 60), width=1)
for y in range(0, H, A.step // 2):
    d.line([(0, y), (W, y)], fill=(255, 90, 60, 60), width=1)

for x in range(0, W, A.step):
    d.line([(x, 0), (x, H)], fill=(255, 90, 60, 150), width=1)
    d.rectangle([x + 1, 1, x + 40, 15], fill=(0, 0, 0, 170))
    d.text((x + 3, 3), str(x), fill=(255, 210, 120))
for y in range(0, H, A.step):
    d.line([(0, y), (W, y)], fill=(255, 90, 60, 150), width=1)
    d.rectangle([1, y + 1, 42, y + 15], fill=(0, 0, 0, 170))
    d.text((3, y + 3), str(y), fill=(255, 210, 120))

out = A.out or os.path.splitext(A.image)[0] + '_grid.png'
# Сетка ЧИТАЕТСЯ, а не хранится: держим её в производной папке рядом с остальным временным
im.save(out)
print('размер %dx%d, шаг %d -> %s' % (W, H, A.step, out))
