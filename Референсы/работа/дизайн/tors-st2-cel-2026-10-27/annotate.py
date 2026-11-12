"""Explanatory raster diagram over the unchanged clean image; Anaconda + Pillow."""
from pathlib import Path
import hashlib
import json
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent
src = Image.open(ROOT / '01-chistyj.png').convert('RGB')
w, h = src.size
# Coordinates are on the 1774 x 887 clean reference; scale annotation only.
sx, sy = w / 1774, h / 887
TOP, BOT = 85, 165
out = Image.new('RGB', (w, h + TOP + BOT), '#16202a')
out.paste(src, (0, TOP))
draw = ImageDraw.Draw(out)
font_path = 'C:/Windows/Fonts/arial.ttf'
font = ImageFont.truetype(font_path, 22)
small = ImageFont.truetype(font_path, 20)
title = ImageFont.truetype('C:/Windows/Fonts/arialbd.ttf', 27)
colors = {'В2': '#ec9b38', 'В3': '#41cddd', 'В4': '#9cc67a'}

def point(p):
    return round(p[0]*sx), TOP + round(p[1]*sy)

def line(points, color):
    pts = [point(p) for p in points]
    draw.line(pts, fill='#16202a', width=7, joint='curve')
    draw.line(pts, fill=color, width=3, joint='curve')

def tag(label, p):
    x, y = point(p)
    box = draw.textbbox((x, y), label, font=font)
    draw.rectangle((box[0]-5, box[1]-4, box[2]+5, box[3]+4), fill='#16202a')
    draw.text((x,y), label, font=font, fill=colors[label])

# Pair of independent pectoral edges, not a connecting transverse stripe.
for pts in [[(194,330),(236,372),(279,389),(329,379)],
            [(349,379),(397,393),(451,365),(491,326)],
            [(1244,341),(1291,379),(1336,392),(1385,382)],
            [(1404,380),(1456,395),(1503,374),(1537,339)]]:
    line(pts, colors['В2'])
# Open rib arch: center above ends, endpoints do not close onto abdominal borders.
for pts in [[(232,429),(268,414),(341,398),(410,416),(460,434)],
            [(1318,431),(1352,415),(1405,406),(1461,427),(1490,441)]]:
    line(pts, colors['В3'])
# Long abdominal transitions stop above clothing; never join into a pointed tip.
for pts in [[(263,447),(272,492),(280,535),(295,559)],
            [(422,447),(414,492),(406,535),(391,559)],
            [(1360,451),(1370,492),(1378,535),(1390,554)],
            [(1486,456),(1473,497),(1462,538),(1447,558)]]:
    line(pts, colors['В4'])
# Profile: only visible pectoral edge; do not invent the hidden far side.
line([(704,352),(724,374),(747,382)], colors['В2'])
for label, p in [('В2',(283,343)),('В3',(350,415)),('В4',(334,508)),
                 ('В2',(720,318)),('В2',(1440,344)),('В3',(1422,427)),('В4',(1404,510))]:
    tag(label,p)

draw.text((26,13),'CHIMERA / СТ.2 — РАБОЧАЯ ЦЕЛЬ ТОРСА, НЕ НОВАЯ ВИТРИНА',font=title,fill='#edf1f4')
for x, label in [(26,'АНФАС'),(720,'ПРОФИЛЬ ВЛЕВО'),(1220,'3/4')]:
    draw.text((round(x*sx),49),label,font=font,fill='#c4d0da')
y = TOP+h+14
for text_, color in [
    ('В2 — два наклонных края грудных; не соединять поперечной полкой.', colors['В2']),
    ('В3 — раскрытая дуга: центр выше концов, на боках переход затухает.', colors['В3']),
    ('В4 — продольные края широкой передней поверхности; внизу без острия.', colors['В4']),
    ('Профиль: скрытое рукой не размечено. Линии — переходы поверхности, не канавки и не швы.', '#c4d0da'),
    ('Ориентиры: талия 0,60–0,70 клетки; отступ живота 0,20–0,30 D. Не промер 3D и не ортопроекции.', '#c4d0da')]:
    draw.text((26,y),text_,font=small,fill=color)
    y += 28
out.save(ROOT / '02-linii.png')
manifest = {}
for name in ['01-chistyj.png','02-linii.png']:
    p = ROOT / name
    with Image.open(p) as im:
        im.verify()
    manifest[name] = {'sha256':hashlib.sha256(p.read_bytes()).hexdigest(), 'bytes':p.stat().st_size}
manifest['clean_size'] = [w,h]
manifest['overlay_body_offset'] = [0,TOP]
manifest['note'] = 'Clean pixels preserved exactly outside explicitly drawn annotation strokes.'
(ROOT/'verification.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(manifest,ensure_ascii=False,indent=2))
