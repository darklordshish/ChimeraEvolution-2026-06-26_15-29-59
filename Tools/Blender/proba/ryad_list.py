"""ЛИСТЫ РЯДА ПО БЮДЖЕТУ (03.11): кадры и отчёты `ryad_byudzheta.py` в столбцы по бюджету. `ryad-krupno.png` — ракурсы крупно, `ryad-distanciya.png` — с дистанции игровой камеры в настоящих пикселях (фигура ~200 px), и в позах из цикла, если есть кадры `<префикс>_poza/`.

  python ryad_list.py папка_кадров выход x100 x080 x075 x067 x050 x040
"""
import sys, os, json
from PIL import Image, ImageDraw, ImageFont

IN, OUT, PR = sys.argv[1], sys.argv[2], sys.argv[3:]; os.makedirs(OUT, exist_ok=True)
F = ImageFont.truetype('C:/Windows/Fonts/arial.ttf', 18); Fs = ImageFont.truetype('C:/Windows/Fonts/arial.ttf', 14)
O = {p: json.load(open(os.path.join(IN, p + '.json'), encoding='utf-8')) for p in PR}


def podpis(p, o): return '×%.2f · %d тр' % (int(p[1:]) / 100, o['tr']), 'IoU %.3f / %.3f' % (o['iou_front'], o['iou_profile'])


def list_(ryady, w, h, naz, fnt=F, sh=46):
    L = Image.new('RGB', (w * len(PR), sh + h * len(ryady)), 'white'); d = ImageDraw.Draw(L)
    for i, p in enumerate(PR):
        a, b = podpis(p, O[p]); d.text((i * w + 6, 3), a, fill=(0, 0, 0), font=fnt); d.text((i * w + 6, 24), b, fill=(70, 70, 70), font=fnt)
        for r, f in enumerate(ryady):
            fp = os.path.join(IN, f % p)
            if os.path.exists(fp): L.paste(Image.open(fp).convert('RGB').resize((w, h), Image.LANCZOS), (i * w, sh + r * h))
    L.save(os.path.join(OUT, naz))


list_(['%s_q34back.png', '%s_back.png', '%s_front.png', '%s_q34.png', '%s_profile.png'], 300, 620, 'ryad-krupno.png')
list_(['%s_dal_q34back.png', '%s_dal_front.png', '%s_dal_profile.png'], 150, 222, 'ryad-distanciya.png', Fs, 44)
list_(['%s_poza/q_065.png', '%s_poza/q_130.png', '%s_poza/f_130.png'], 300, 400, 'ryad-v-pozah.png')
print('ЛИСТЫ:', OUT)
