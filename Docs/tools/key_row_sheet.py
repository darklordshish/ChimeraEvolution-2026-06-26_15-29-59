"""Склейка ряда ключа (`Tools/key-row.sh`): кадры весов в профиль и анфас — одним листом, нога крупно — отдельным;
на обоих подпись «вид · ключ · коммит · дата». Подпись — не украшение: по ней видно, с какой версии снят кадр."""
import os
import sys
from datetime import datetime

import matplotlib
from PIL import Image, ImageDraw, ImageFont


def main(src, out, species, key, commit, last, *weights):
    font = ImageFont.truetype(os.path.join(matplotlib.get_data_path(), 'fonts/ttf/DejaVuSans.ttf'), 28)
    stamp = f'{species} · ключ «{key}» · {commit} · {datetime.now():%d.%m.%Y %H:%M}'
    rows = [[Image.open(os.path.join(src, f'{w}-{v}.png')) for w in weights] for v in ('profile', 'front')]
    W, H = rows[0][0].size
    sheet = Image.new('RGB', (W * len(weights), H * 2 + 100), 'white')
    d = ImageDraw.Draw(sheet)
    d.text((20, 10), stamp, fill='black', font=font)
    for i, w in enumerate(weights):
        d.text((i * W + W // 2 - 50, 55), f'вес {w}', fill='black', font=font)
    for r, row in enumerate(rows):
        for i, im in enumerate(row):
            sheet.paste(im, (i * W, 100 + r * H))
    tag = commit.replace('+', '-')
    a = os.path.join(out, f'ряд-{tag}.png')
    sheet.save(a)
    leg = Image.open(os.path.join(src, 'noga.png'))
    b = Image.new('RGB', (leg.width, leg.height + 50), 'white')
    ImageDraw.Draw(b).text((20, 10), f'{stamp} · нога, вес {last}', fill='black', font=font)
    b.paste(leg, (0, 50))
    p = os.path.join(out, f'нога-{tag}.png')
    b.save(p)
    print(a)
    print(p)


if __name__ == '__main__':
    main(*sys.argv[1:])
