from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
P=Path(__file__).parent; O=P.parent.parent/'Референсы/работа/дизайн/shkura-po-granyam-2026-11-06'; O.mkdir(exist_ok=True,parents=True)
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',25)
small=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',19)
for sp,name in [('snake','Змея'),('wolf','Волк')]:
    sheet=Image.new('RGB',(1560,1550),'#edf0f2'); d=ImageDraw.Draw(sheet)
    for row,mode in enumerate(['original','proposal']):
        y=row*775
        d.text((20,y+12),name+' · '+('Исходник Модельного' if row==0 else 'Проба Codex'),font=font,fill='#24303a')
        for col,view in enumerate(['back','rear34','front34']):
            sheet.paste(Image.open(P/'renders'/f'{sp}-{mode}-{view}.png').convert('RGB'),(520*col,y+48))
    sheet.save(O/f'{sp}-comparison.png')
sheet=Image.new('RGB',(1560,630),'#edf0f2');d=ImageDraw.Draw(sheet)
for col,(sp,mode,title) in enumerate([('snake','original','Змея · исходник'),('snake','proposal','Змея · проба'),('wolf','original','Волк · исходник'),('wolf','proposal','Волк · проба')]):
    d.text((col*390+12,16),title,font=font,fill='#24303a')
    im=Image.open(P/'renders'/f'{sp}-{mode}-rear34.png').convert('RGB').resize((390,540),Image.Resampling.LANCZOS)
    sheet.paste(im,(col*390,56))
d.text((14,604),'Одна камера и свет. Только рисунок кожи; грива скрыта для сравнения. Не игровой кадр.',font=small,fill='#394954')
sheet.save(O/'comparison.png')
print(O)
