from pathlib import Path
import json
from PIL import Image,ImageDraw,ImageFont
P=Path(__file__).parent;O=P.parent.parent/'Референсы/работа/дизайн/zmeya-relief-grid-2026-11-06'
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',25);small=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',17)
sheet=Image.new('RGB',(2400,778),'#edf0f2');d=ImageDraw.Draw(sheet)
for col,(mode,label) in enumerate([('flat','Прежний рисунок'),('mono','Объём · один цвет'),('pattern','Объём + тот же рисунок')]):
    d.text((col*800+15,14),label,font=font,fill='#24303a');sheet.paste(Image.open(P/'renders'/(mode+'-rear34.png')).convert('RGB'),(col*800,54))
sheet.save(O/'comparison.png')
sheet=Image.new('RGB',(1600,778),'#edf0f2');d=ImageDraw.Draw(sheet)
for col,(mode,label) in enumerate([('mono','Только геометрия, без обводки'),('pattern','Тот же рельеф с рисунком кожи')]):
    d.text((col*800+15,14),label,font=font,fill='#24303a');sheet.paste(Image.open(P/'renders'/(mode+'-detail.png')).convert('RGB'),(col*800,54))
sheet.save(O/'detail.png')
Image.open(P/'renders/pattern-side.png').save(O/'side.png')
rows=json.loads((P/'frames.json').read_text(encoding='utf8'));frames=[];duration=[]
for r in rows:
    im=Image.new('RGB',(600,664),'#edf0f2');d=ImageDraw.Draw(im);d.text((12,10),'Рельеф по сетке накожного рисунка',font=font,fill='#24303a')
    im.paste(Image.open(P/'renders'/('frame-%03d.png'%r['frame'])).convert('RGB'),(0,40))
    a,b,c,e,f=r['angles'];d.text((12,642),f'Наклон {a:+.0f}° · поворот {b:+.0f}° · руки {e:.0f}° / {f:.0f}°',font=small,fill='#24303a')
    frames.append(im);duration.append(350 if r['frame']%6==0 else 90)
frames[0].save(O/'motion.gif',save_all=True,append_images=frames[1:],duration=duration,loop=0,optimize=False,disposal=2)
(O/'geometry.json').write_text((P/'geometry.json').read_text(encoding='utf8'),encoding='utf8')
assert len(frames)==49
print('DONE',len(frames))
