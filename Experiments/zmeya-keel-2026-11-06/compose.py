from pathlib import Path
import json
from PIL import Image,ImageDraw,ImageFont
P=Path(__file__).parent;O=P.parent.parent/'Референсы/работа/дизайн/zmeya-keel-2026-11-06'
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',24);small=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',17)
old=P.parent/'shkura-surface-bind-2026-11-06/renders-surface/rest-rear34.png'
sheet=Image.new('RGB',(1600,774),'#edf0f2');d=ImageDraw.Draw(sheet)
for col,(fn,title) in enumerate([(old,'Прежние плоские щитки'),(P/'renders-surface/rest-rear34.png','Киль, кромка и просветы')]):
    d.text((col*800+16,12),title,font=font,fill='#24303a');sheet.paste(Image.open(fn).convert('RGB'),(col*800,50))
sheet.save(O/'comparison.png')
im=Image.open(P/'renders-surface/rest-rear34.png').convert('RGB')
im.crop((180,150,570,590)).resize((780,880),Image.Resampling.LANCZOS).save(O/'detail.png')
rows=json.loads((P/'frames.json').read_text(encoding='utf8'));frames=[];duration=[]
for r in rows:
    im=Image.new('RGB',(600,664),'#edf0f2');d=ImageDraw.Draw(im);d.text((12,10),'Килеватая чешуя · проба рельефа',font=font,fill='#24303a')
    im.paste(Image.open(P/'renders-surface'/('frame-%03d.png'%r['frame'])).convert('RGB'),(0,40))
    a,b,c,e,f=r['angles'];d.text((12,642),f'Наклон {a:+.0f}° · поворот {b:+.0f}° · руки {e:.0f}° / {f:.0f}°',font=small,fill='#24303a')
    frames.append(im);duration.append(350 if r['frame']%6==0 else 90)
frames[0].save(O/'motion.gif',save_all=True,append_images=frames[1:],duration=duration,loop=0,optimize=False,disposal=2)
(O/'summary.json').write_text((P/'summary.json').read_text(encoding='utf8'),encoding='utf8')
assert len(frames)==49
print('GIF',len(frames))
