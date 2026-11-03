"""СКЛЕЙКА ЦИКЛА (03.11): кадры `cikl_dvizheniya.py` рядом (3/4 сзади и анфас) с подписью фазы — GIF (и MP4, если есть imageio_ffmpeg) и лист крайних поз.

  python cikl_skleika.py папка_кадров выходная_папка [--fps 15]
"""
import sys, os, glob
from PIL import Image, ImageDraw, ImageFont

a = sys.argv[1:]; IN, OUT = a[0], a[1]; FPS = int(a[a.index('--fps') + 1]) if '--fps' in a else 15; os.makedirs(OUT, exist_ok=True)
fnt = ImageFont.truetype('C:/Windows/Fonts/arial.ttf', 20)
faz = dict(l.split('\t') for l in open(os.path.join(IN, 'opis.tsv'), encoding='utf-8').read().splitlines())
kadry = []
for n, f in sorted(faz.items()):
    q, fr = [Image.open(os.path.join(IN, '%s_%s.png' % (k, n))).convert('RGB') for k in 'qf']
    c = Image.new('RGB', (q.width + fr.width, q.height), 'white'); c.paste(q, (0, 0)); c.paste(fr, (q.width, 0))
    d = ImageDraw.Draw(c); d.text((10, 8), '3/4 сзади · ' + f, fill=(20, 20, 20), font=fnt); d.text((q.width + 10, 8), 'анфас', fill=(20, 20, 20), font=fnt)
    kadry.append((n, f, c))
pal = [c.convert('P', palette=Image.ADAPTIVE, colors=64) for _, _, c in kadry]
pal[0].save(os.path.join(OUT, 'cikl.gif'), save_all=True, append_images=pal[1:], duration=int(1000 / FPS), loop=0, optimize=True)
try:
    import imageio_ffmpeg, numpy as np
    w = imageio_ffmpeg.write_frames(os.path.join(OUT, 'cikl.mp4'), kadry[0][2].size, fps=FPS, codec='libx264', pix_fmt_out='yuv420p', quality=8)
    w.send(None)
    for _, _, c in kadry: w.send(np.asarray(c).tobytes())
    w.close()
except ImportError:
    print('MP4 нет: imageio_ffmpeg не найден')
# лист: последний кадр каждой удержанной фазы и крайности шага
vyb = [k for i, k in enumerate(kadry) if k[1] == 'держит' and (i + 1 == len(kadry) or kadry[i + 1][1] != 'держит')] + [kadry[3], kadry[9], kadry[15], kadry[21]]
Wd, Hd = vyb[0][2].size; lst = Image.new('RGB', (Wd * 2, Hd * ((len(vyb) + 1) // 2)), 'white')
for i, (_, _, c) in enumerate(vyb): lst.paste(c, ((i % 2) * Wd, (i // 2) * Hd))
lst.save(os.path.join(OUT, 'krajnie-pozy.png'))
print('СКЛЕЙКА: кадров %d, %s' % (len(kadry), OUT))
