# -*- coding: utf-8 -*-
"""ЧЕРЕП НА ЧЕРЕП: рендер головы поверх ортографии черепа, в пикселях самой пластины.

ЗАЧЕМ ОТДЕЛЬНО ОТ ТЕЛА. Голова — десятая часть пикселей профиля, и её ошибки в метрику тела не
попадают вовсе: «двадцать проверок ok» уживалось с мордой-демоном именно так. Правило методики —
часть мерится В МАСШТАБЕ ЧАСТИ, своим контуром и своим порогом.

ПОЧЕМУ КОСТЬ С КОСТЬЮ, А НЕ С ФОТО ЗВЕРЯ. Между черепом и шерстью лежит несколько сантиметров мяса,
и на живом волке толщину черепной коробки не увидеть в принципе. Сравнивать надо с тем же, из чего
череп построен, — тогда «коробки почти нет» становится числом, а не впечатлением.

Запуск (наклон черепа при этом СНИМАЕТСЯ, чтобы совпасть с лежащей пластиной):
    CHIMERA_SKULL_FLAT=1 blender -b -P make.py -- --species wolf --layer 1 --views skull
    python compare_skull.py out/wolf_L1_skull.png
"""
import sys, os, io, argparse
import numpy as np
from PIL import Image, ImageDraw

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
os.environ['CHIMERA_SKULL_FLAT'] = '1'
ROOT = os.path.dirname(os.path.dirname(HERE))
PLATE = os.path.join(ROOT, 'Anatomy', 'species', 'wolf', 'ref', 'skeleton', 'wolf_skull_plate.jpg')

AP = argparse.ArgumentParser()
AP.add_argument('render')
AP.add_argument('--species', default='wolf')
AP.add_argument('--out', default='')
AP.add_argument('--alpha', type=float, default=0.55)
A = AP.parse_args()

import importlib
from chimera.views import VIEWS
sp = importlib.import_module('species.' + A.species)
assert sp.SK_FLAT, 'вид собран с наклонённым черепом — задай CHIMERA_SKULL_FLAT=1 и пересобери'

s = sp.SK_S                                   # метров на пиксель пластины
camY, camZ, ortho = VIEWS["skull"][0][1], VIEWS["skull"][0][2], VIEWS["skull"][2]
gz, gy = -camY, camZ                          # центр кадра в координатах игры
z0, z1 = gz - ortho / 2, gz + ortho / 2
y1, y0 = gy + ortho * 0.75 / 2, gy - ortho * 0.75 / 2

# Игра → пиксель пластины (обратная к `SK` при нулевом наклоне)
def to_plate(z, y):
    return 52 + (z - sp.SK_O[1]) / s, 193 - (y - sp.SK_O[0]) / s

px0, py_top = to_plate(z0, y1)
px1, py_bot = to_plate(z1, y0)
tw, th = int(round(px1 - px0)), int(round(py_bot - py_top))
print('кадр рендера в пикселях пластины: x %.0f..%.0f, y %.0f..%.0f (%dx%d)'
      % (px0, px1, py_top, py_bot, tw, th))

plate = Image.open(PLATE).convert('RGB')
PW, PH = plate.size
K = 4                                          # пластина мелкая (440 px), смотреть надо крупно
rd = Image.open(A.render).convert('RGBA').resize((tw, th), Image.LANCZOS)
canvas = Image.new('RGBA', (PW, PH), (0, 0, 0, 0))
canvas.paste(rd, (int(round(px0)), int(round(py_top))))

a = np.asarray(canvas).astype(float)
mask = a[..., 3:4] / 255.0 * A.alpha
tint = np.zeros_like(a[..., :3]); tint[..., 0] = 255; tint[..., 1] = 60; tint[..., 2] = 200
out = np.asarray(plate).astype(float) * (1 - mask) + tint * mask
res = Image.fromarray(out.astype(np.uint8)).crop((0, 40, PW, 280)).resize((PW * K, 240 * K), Image.LANCZOS)

d = ImageDraw.Draw(res)
for gx in range(0, PW, 50):                    # линейка в пикселях пластины
    d.line([(gx * K, 0), (gx * K, 12)], fill=(0, 255, 255), width=3)
    d.text((gx * K + 4, 4), str(gx), fill=(0, 255, 255))
out_path = A.out or os.path.splitext(A.render)[0] + '_vs_plate.png'
res.save(out_path)
print('записано:', out_path)
