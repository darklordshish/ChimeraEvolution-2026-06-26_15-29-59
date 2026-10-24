from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import numpy as np,json
root=Path(__file__).resolve().parent
out=root.parents[1]/'Референсы/работа/дизайн/facets-2026-10-24'; out.mkdir(parents=True,exist_ok=True)
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',23); small=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',18)
cols=[('input','До огранки'),('old','Старая огранка · локальная правка'),('planes','Области + предел сдвига 8 мм')]
for name,views,close in [('torso',['front','34','side'],True),('body',['front','34','side'],False)]:
    w,h=600,667; im=Image.new('RGB',(w*3,110+h*3+95),'#f5f5f5'); d=ImageDraw.Draw(im)
    d.text((15,15),'CHIMERA · Опыт огранки груди и боков · 24.10.2026',font=font,fill='black')
    for j,(v,label) in enumerate(cols):
        d.text((j*w+12,65),label,font=font,fill='black')
        for i,view in enumerate(views):
            src=Image.open(root/'renders'/f'{v}_{view}{"_torso" if close else ""}.png')
            im.paste(src.convert('RGB').resize((w,h),Image.Resampling.LANCZOS),(j*w,110+i*h))
    d.text((15,110+h*3+10),'Одинаковые исходная форма, веса, поза, камера и свет. Меняется только выбранный участок торса.',font=small,fill='black')
    d.text((15,110+h*3+40),'Границы областей — гипотеза опыта. Сетка прежняя. Результат не принят: рябь и полоса под грудью остаются.',font=small,fill='black')
    im.save(out/f'{name}.png')
metrics={}
for view in ['front','side','34']:
    base=np.array(Image.open(root/'masks'/f'input_{view}.png').convert('RGB')).min(2)<80
    metrics[view]={}
    for v in ['old','planes']:
        mask=np.array(Image.open(root/'masks'/f'{v}_{view}.png').convert('RGB')).min(2)<80
        metrics[view][v]=dict(iou=float((base&mask).sum()/(base|mask).sum()),changed_pixels=int((base^mask).sum()))
(root/'silhouettes.json').write_text(json.dumps(metrics,indent=2),encoding='utf-8')
print(json.dumps(metrics))
