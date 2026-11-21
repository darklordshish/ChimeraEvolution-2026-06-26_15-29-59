import json
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
P=Path(__file__).parent;O=P.parent.parent/'Референсы/работа/дизайн/zmeya-shchitki-motion-2026-11-06'
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',23);small=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',17)
rows=json.loads((P/'frames.json').read_text(encoding='utf8'));frames=[];dur=[]
for r in rows:
    im=Image.new('RGB',(600,664),'#edf0f2');d=ImageDraw.Draw(im)
    d.text((14,10),'Щитки + рисунок · пробное движение рига',font=font,fill='#24303a')
    im.paste(Image.open(P/'renders'/('frame-%03d.png'%r['frame'])).convert('RGB'),(0,40))
    flex,twist,side,lift,reach=r['angles']
    d.text((12,642),f'Наклон {flex:+.0f}° · поворот {twist:+.0f}° · руки вверх {lift:.0f}° / вперёд {reach:.0f}°',font=small,fill='#24303a')
    frames.append(im);dur.append(350 if r['frame']%6==0 else 90)
frames[0].save(O/'motion.gif',save_all=True,append_images=frames[1:],duration=dur,loop=0,optimize=False,disposal=2)
labels={'rest':'Покой','flex30':'Наклон 30°','extend20':'Разгибание 20°','twist40':'Скручивание 40°','side20':'Вбок 20°','arms90':'Руки в стороны 90°','reach70':'Руки вперёд 70°','combined':'Наклон + поворот + руки'}
rows=json.loads((P/'poses.json').read_text(encoding='utf8'))['poses']
for view in ['rear34','side']:
    sheet=Image.new('RGB',(1600,824),'#edf0f2');d=ImageDraw.Draw(sheet)
    for i,r in enumerate(rows):
        x=(i%4)*400;y=(i//4)*412
        d.text((x+10,y+8),labels[r['label']],font=font,fill='#24303a')
        im=Image.open(P/'renders'/(r['label']+'-'+view+'.png')).convert('RGB').resize((400,360),Image.Resampling.LANCZOS)
        sheet.paste(im,(x,y+37))
    sheet.save(O/('poses-'+view+'.png'))
summary=[]
for r in rows:
    summary.append(dict(pose=r['label'],max_distance_to_visible_skin_mm=r['max_nearest_skin_mm'],max_below_visible_skin_mm=r['max_below_skin_mm'],top_samples_below_0_5mm=r['below_skin_over_0_5mm'],top_top_crossing_pairs=len(r['top_top_crossing_plate_pairs']),all_crossing_pairs=len(r['noncoplanar_crossing_plate_pairs'])))
(O/'summary.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf8')
print('GIF',len(frames),'frames', (O/'motion.gif').stat().st_size)
