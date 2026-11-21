from pathlib import Path
import json
from PIL import Image,ImageDraw,ImageFont
P=Path(__file__).parent;O=P.parent.parent/'Референсы/работа/дизайн/shkura-surface-bind-2026-11-06'
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',22);small=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',17)
rows=json.loads((P/'frames.json').read_text(encoding='utf8'));frames=[];dur=[]
for r in rows:
    im=Image.new('RGB',(600,664),'#edf0f2');d=ImageDraw.Draw(im)
    d.text((12,10),'Щитки · поверхностная привязка · проба',font=font,fill='#24303a')
    im.paste(Image.open(P/'renders-surface'/('frame-%03d.png'%r['frame'])).convert('RGB'),(0,40))
    a,b,c,e,f=r['angles'];d.text((12,642),f'Наклон {a:+.0f}° · поворот {b:+.0f}° · руки {e:.0f}° / {f:.0f}°',font=small,fill='#24303a')
    frames.append(im);dur.append(350 if r['frame']%6==0 else 90)
frames[0].save(O/'motion.gif',save_all=True,append_images=frames[1:],duration=dur,loop=0,optimize=False,disposal=2)
sheet=Image.new('RGB',(1600,1536),'#edf0f2');d=ImageDraw.Draw(sheet)
for row,(label,title) in enumerate([('flex30','Наклон вперёд 30°'),('extend20','Разгибание 20°')]):
    for col,(variant,text) in enumerate([('lbs','Обычные веса'),('surface','Привязка к коже')]):
        x=col*800;y=row*768;d.text((x+14,y+12),title+' · '+text,font=font,fill='#24303a')
        sheet.paste(Image.open(P/('renders-'+variant)/(label+'-rear34.png')).convert('RGB'),(x,y+45))
sheet.save(O/'comparison.png')
sheet=Image.new('RGB',(1600,824),'#edf0f2');d=ImageDraw.Draw(sheet)
labels={'rest':'Покой','flex30':'Наклон 30°','extend20':'Разгибание 20°','twist40':'Скручивание 40°','side20':'Вбок 20°','arms90':'Руки в стороны 90°','reach70':'Руки вперёд 70°','combined':'Наклон + поворот + руки'}
poses=json.loads((P/'poses.json').read_text(encoding='utf8'))
for i,r in enumerate(r for r in poses if r['variant']=='surface'):
    x=(i%4)*400;y=(i//4)*412;d.text((x+10,y+8),labels[r['label']],font=font,fill='#24303a')
    im=Image.open(P/'renders-surface'/(r['label']+'-rear34.png')).convert('RGB').resize((400,360),Image.Resampling.LANCZOS);sheet.paste(im,(x,y+37))
sheet.save(O/'poses.png')
(O/'summary.json').write_text((P/'summary.json').read_text(encoding='utf8'),encoding='utf8')
assert len(frames)==49
print('GIF',len(frames),'frames')
