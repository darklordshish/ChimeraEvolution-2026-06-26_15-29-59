from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
root=Path(__file__).resolve().parent
out=root.parents[1]/'Референсы/работа/дизайн/chest-patch-2026-10-25';out.mkdir(parents=True,exist_ok=True)
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',25);small=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',19)
im=Image.new('RGB',(1440,1740),'#f5f5f5');d=ImageDraw.Draw(im)
d.text((20,15),'CHIMERA · Локальная проба плоскостей груди · 25.10.2026',font=font,fill='black')
for col,(variant,title) in enumerate([('before','До правки'),('patch','Проба плоскостей · не принято')]):
    d.text((col*720+20,55),title,font=font,fill='black')
    for row,view in enumerate(['front','34']):
        with Image.open(root/'renders'/f'{variant}_{view}.png') as src:
            im.paste(src.convert('RGB').resize((720,800),Image.Resampling.LANCZOS),(col*720,95+row*800))
d.text((20,1705),'Камера и свет одинаковые. Статическая геометрия: риг и ключи на новую сетку не перенесены.',font=small,fill='black')
im.save(out/'comparison.png')
