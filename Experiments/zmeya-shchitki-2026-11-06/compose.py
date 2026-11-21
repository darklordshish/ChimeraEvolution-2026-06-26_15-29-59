from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
P=Path(__file__).parent;O=P.parent.parent/'Референсы/работа/дизайн/zmeya-shchitki-2026-11-06'
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',25)
sheet=Image.new('RGB',(1040,778),'#edf0f2');d=ImageDraw.Draw(sheet)
for col,(folder,title) in enumerate([('pattern-renders','Рисунок на коже'),('renders','Щитки · проба')]):
    d.text((col*520+16,14),title,font=font,fill='#24303a');sheet.paste(Image.open(P/folder/'rear34.png').convert('RGB'),(col*520,54))
sheet.save(O/'comparison.png')
sheet=Image.new('RGB',(2080,778),'#edf0f2');d=ImageDraw.Draw(sheet)
for col,(name,title) in enumerate([('back','Сзади'),('rear34','3/4 сзади'),('side','Профиль'),('front34','3/4 спереди')]):
    d.text((col*520+16,14),title,font=font,fill='#24303a');sheet.paste(Image.open(P/'renders'/(name+'.png')).convert('RGB'),(col*520,54))
sheet.save(O/'views.png')
Image.open(P/'renders/detail.png').save(O/'detail.png')
