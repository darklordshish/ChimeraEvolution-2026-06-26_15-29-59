"""Assemble diagnostic renders with literal labels; no retouching of model images."""
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
root=Path(__file__).resolve().parent
out=root.parents[1]/'Референсы'/'работа'/'дизайн'/'morph-algorithm-2026-10-24'
out.mkdir(parents=True,exist_ok=True)
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',23)
small=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',19)
cols=[('current','A · Поставка f6bb133','Отдельный ориентир, иной проход'),
      ('fit','B · Исходная подгонка','Контрольная поверхность'),
      ('changed','C · Изменили веса','Без пересчёта формы'),
      ('corrected','D · Пересчитали форму','Те же веса, поверхность сохранена')]
for sheet,views,torso in [('comparison',['front','34','side'],False),('torso',['front','34'],True)]:
    w,h=520,578
    im=Image.new('RGB',(w*4,120+h*len(views)+90),'#f4f4f4'); d=ImageDraw.Draw(im)
    d.text((18,12),'CHIMERA · Проверка алгоритма на реальных данных · 24.10.2026',font=font,fill='#171717')
    for j,(v,title,caption) in enumerate(cols):
        d.text((j*w+12,51),title,font=font,fill='#171717')
        d.text((j*w+12,82),caption,font=small,fill='#444444')
        for i,view in enumerate(views):
            p=root/'renders'/f'{v}_{view}{"_torso" if torso else ""}.png'
            with Image.open(p) as src: im.paste(src.convert('RGB').resize((w,h),Image.Resampling.LANCZOS),(j*w,120+i*h))
    y=120+h*len(views)+10
    d.text((18,y),'B → C → D: контролируемый опыт. A → D: также новая подгонка, без прежней огранки и формовки.',font=small,fill='#333333')
    d.text((18,y+28),'Голова и органы скрыты. Камеры, масштаб и свет одинаковые. Рендер Blender; не кадр игры и не финальная модель.',font=small,fill='#333333')
    im.save(out/f'{sheet}.png')
print(out)
