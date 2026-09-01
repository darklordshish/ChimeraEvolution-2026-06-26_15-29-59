# -*- coding: utf-8 -*-
"""НАЛОЖЕНИЕ РЕНДЕРА НА НАТУРУ — единственная проверка, которой можно верить.

Численные проверки меряют лишь то, что догадались померить: «двадцать из двадцати ok» уживается
с демоном на экране. Здесь модель кладётся поверх фотографии В ЕЁ ЖЕ ПИКСЕЛЯХ, и расхождение видно
целиком, а не по тем меркам, которые я выбрал.

Перевод один и тот же в обе стороны и записан ЯВНО: ортокамера профиля покрывает известный кусок
мировых координат, фотография — известный кусок через землю и холку. Оба перевода линейны, поэтому
совмещение не подгоняется, а вычисляется.

Запуск:  python compare.py out/wolf_L1_profile.png [--out out/wolf_L1_vs_photo.png]
"""
import sys, os, io, argparse
import numpy as np
from PIL import Image

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
PHOTO = os.path.join(ROOT, 'Anatomy', 'species', 'wolf', 'ref', 'photo', 'wolf_standing_1.jpg')

# ── КАЛИБРОВКА ФОТО (снята сегментацией, не на глаз) ─────────────────────────────────────────────
GROUND_PX, WITHERS_PX, WITHERS_X_PX = 1452.0, 381.0, 1180.0
PX_PER_M = (GROUND_PX - WITHERS_PX) / 1.170          # 915.4 px в метре

# ── КАДР ОРТОКАМЕРЫ ПРОФИЛЯ (`build.VIEWS['profile']`) в координатах ИГРЫ ─────────────────────────
CAM_Y, CAM_Z, ORTHO, ASPECT = -0.10, 0.78, 2.30, 0.75   # центр по Z-игры, по Y-игры, ширина кадра

AP = argparse.ArgumentParser()
AP.add_argument('render')
AP.add_argument('--out', default='')
AP.add_argument('--alpha', type=float, default=0.62)
AP.add_argument('--joints', default='', help='вид: нанести СУСТАВЫ модели точками поверх фото')
A = AP.parse_args()

ph = Image.open(PHOTO).convert('RGB')
W, H = ph.size
rd = Image.open(A.render).convert('RGBA')
rw, rh = rd.size

# Кадр рендера в метрах игры: экран вправо = +Z, экран вверх = +Y
z0, z1 = CAM_Y - ORTHO / 2, CAM_Y + ORTHO / 2
y1, y0 = CAM_Z + ORTHO * ASPECT / 2, CAM_Z - ORTHO * ASPECT / 2
# Тот же кусок в пикселях фотографии
px0 = WITHERS_X_PX + z0 * PX_PER_M
px1 = WITHERS_X_PX + z1 * PX_PER_M
py0 = GROUND_PX - y1 * PX_PER_M
py1 = GROUND_PX - y0 * PX_PER_M
tw, th = int(round(px1 - px0)), int(round(py1 - py0))
print('кадр рендера в пикселях фото: x %.0f..%.0f, y %.0f..%.0f  (%dx%d, было %dx%d)'
      % (px0, px1, py0, py1, tw, th, rw, rh))

rd = rd.resize((tw, th), Image.LANCZOS)
canvas = Image.new('RGBA', (W, H), (0, 0, 0, 0))
canvas.paste(rd, (int(round(px0)), int(round(py0))))

# ЗЕЛЁНЫМ — модель, чтобы её край не путался с серой шерстью
a = np.asarray(canvas).astype(float)
mask = a[..., 3:4] / 255.0 * A.alpha
tint = np.zeros_like(a[..., :3]); tint[..., 1] = 255; tint[..., 0] = 40; tint[..., 2] = 90
base = np.asarray(ph).astype(float)
out = base * (1 - mask) + tint * mask
res = Image.fromarray(out.astype(np.uint8))

from PIL import ImageDraw
d = ImageDraw.Draw(res)
d.line([(0, GROUND_PX), (W, GROUND_PX)], fill=(255, 210, 0), width=3)
d.line([(0, WITHERS_PX), (W, WITHERS_PX)], fill=(255, 210, 0), width=2)
d.text((12, WITHERS_PX + 6), 'холка 1.170', fill=(255, 210, 0))
d.text((12, GROUND_PX - 22), 'земля 0.000', fill=(255, 210, 0))

# ── СУСТАВЫ ТОЧКАМИ. Пятно геометрии показывает «примерно там», а точка с именем — «этот сустав
# на столько-то мимо». Разница между «плохо» и «где и насколько» ровно в этом
if A.joints:
    sys.path.insert(0, HERE)
    import importlib
    sp = importlib.import_module('species.' + A.joints)
    for nm, (x, y, z) in sorted(sp.P.items()):
        if nm.startswith('__'):
            continue
        px, py = WITHERS_X_PX + z * PX_PER_M, GROUND_PX - y * PX_PER_M
        d.ellipse([px - 8, py - 8, px + 8, py + 8], fill=(255, 40, 40), outline=(0, 0, 0), width=2)
        d.rectangle([px + 10, py - 10, px + 13 + 7 * len(nm), py + 9], fill=(0, 0, 0, 200))
        d.text((px + 13, py - 8), nm, fill=(255, 255, 100))

out_path = A.out or os.path.splitext(A.render)[0] + '_vs_photo.png'
res.save(out_path)
print('записано:', out_path)
