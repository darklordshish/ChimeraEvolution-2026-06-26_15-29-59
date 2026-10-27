"""ВЫРЕЗКИ РАКУРСОВ ЛИСТА ДЛЯ ГЕНЕРАТОРА (перенесено из черновиков 27.10; так снята цель голого тела ст.2 21.10). Лист из N панелей
по ширине (анфас, профиль влево, 3/4 …) → фигура каждой панели по маске (темнее `--porog` на светлом фоне, крупные связные,
мелкие дыры залиты) → RGBA-кроп с альфой, вписанный в квадрат `--razmer`; профиль влево зеркалится в `right.png` (Hunyuan3D-2mv
берёт front/left/right).

  python cel_vyrezki.py <лист.png> <папка> [--imena front,left,34] [--porog 225] [--razmer 900] [--temnyj_fon 0]
  (`--temnyj_fon 1` — фигура светлее фона: порог по «светлее 50», как у листа человека `витрина/человек/вид.png`)
"""
import sys, os
import numpy as np
from PIL import Image, ImageOps
from scipy.ndimage import label

a = sys.argv[1:]
src, D = a[0], a[1]
opt = dict(imena='front,left,34', porog='225', razmer='900', temnyj_fon='0')
i = 2
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
IM = opt['imena'].split(','); S = int(opt['razmer'])
os.makedirs(D, exist_ok=True)
ref = Image.open(src).convert('RGB'); W, H = ref.size
g = np.asarray(ref.convert('L')).astype(int)
for c, nm in enumerate(IM):
    x0, x1 = c * W // len(IM) + 5, (c + 1) * W // len(IM) - 5
    mm = (g[:, x0:x1] > 50) if opt['temnyj_fon'] == '1' else (g[:, x0:x1] < int(opt['porog']))
    lab, n = label(mm); sizes = np.bincount(lab.ravel()); sizes[0] = 0
    mm = (sizes > 0.02 * sizes.max())[lab]
    hl, hn = label(~mm); hs = np.bincount(hl.ravel())
    border = set(np.unique(np.concatenate([hl[0], hl[-1], hl[:, 0], hl[:, -1]])))
    for j in range(1, hn + 1):
        if j not in border and hs[j] < 3000: mm[hl == j] = True
    ys, xs = np.nonzero(mm)
    crop = ref.crop((x0 + xs.min(), ys.min(), x0 + xs.max() + 1, ys.max() + 1)); mm = mm[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
    rgba = crop.convert('RGBA'); rgba.putalpha(Image.fromarray((mm * 255).astype(np.uint8)))
    k = 0.92 * S / max(rgba.size); rgba = rgba.resize((round(rgba.width * k), round(rgba.height * k)), Image.LANCZOS)
    out = Image.new('RGBA', (S, S), (255, 255, 255, 0)); out.paste(rgba, ((S - rgba.width) // 2, (S - rgba.height) // 2), rgba)
    out.save(os.path.join(D, nm + '.png'))
    if nm == 'left': ImageOps.mirror(out).save(os.path.join(D, 'right.png'))
    print('ВЫРЕЗКА: %s — фигура %d×%d px' % (nm, xs.max() - xs.min() + 1, ys.max() - ys.min() + 1))
