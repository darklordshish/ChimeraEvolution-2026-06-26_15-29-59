"""Deterministic review diagram: untouched crops plus separate line overlay."""
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import json,hashlib,base64,io,html
out=Path(__file__).resolve().parent
root=out.parents[3]
reference=Path('C:/Users/semion/Documents/Chimera_game/ChimeraEvolution/Референсы/витрина/человек/вид.png')
model=root/'Experiments/shoulder-local-2026-10-31/review_rest_front.png'
font=lambda n:ImageFont.truetype('C:/Windows/Fonts/arial.ttf',n)
canvas=Image.new('RGB',(1800,900),'#f7f7f5');d=ImageDraw.Draw(canvas)
svg=['<svg xmlns="http://www.w3.org/2000/svg" width="1800" height="900" viewBox="0 0 1800 900"><rect width="1800" height="900" fill="#f7f7f5"/>']
def txt(x,y,text,size=25,color='#222222'):
    d.text((x,y),text,font=font(size),fill=color)
    svg.append(f'<text x="{x}" y="{y+size}" font-family="Arial" font-size="{size}" fill="{color}">{html.escape(text)}</text>')
txt(22,14,'Грудь и живот: сравнение формы и предложение линий',32)
modelbox=(100,240,565,680);refbox=(85,175,337,410)
for i,(path,box,title) in enumerate([(reference,refbox,'1. Витрина · поджарый, 30.10'),(model,modelbox,'2. Модель · текущая проба'),(model,modelbox,'3. Предлагаемые переломы')]):
    im=Image.open(path).convert('RGB').crop(box)
    scale=min(578/im.width,550/im.height);size=(round(im.width*scale),round(im.height*scale));im=im.resize(size,Image.Resampling.LANCZOS)
    x=i*600+(600-size[0])//2;y=108
    canvas.paste(im,(x,y));txt(i*600+15,66,title,25)
    b=io.BytesIO();im.save(b,format='PNG');encoded=base64.b64encode(b.getvalue()).decode()
    svg.append(f'<image x="{x}" y="{y}" width="{size[0]}" height="{size[1]}" href="data:image/png;base64,{encoded}"/>')
    if i==2:origin=(x,y);factor=scale
orange='#e88b1c';blue='#168ba5'
lines=[('1',orange,False,[(182,350),(208,382),(242,401),(285,399),(326,387)]),('1',orange,False,[(339,387),(380,401),(415,395),(445,370),(472,342)]),('2',orange,False,[(218,468),(260,447),(302,422),(330,414),(360,430),(400,450),(432,472)]),('3',blue,True,[(332,438),(331,480),(330,525),(329,570),(329,622)]),('3',blue,True,[(294,482),(309,480),(321,482)]),('3',blue,True,[(340,482),(356,480),(375,484)]),('3',blue,True,[(296,526),(309,524),(320,526)]),('3',blue,True,[(339,526),(355,524),(369,528)])]
for label,color,dashed,pts in lines:
    q=[(origin[0]+(x-modelbox[0])*factor,origin[1]+(y-modelbox[1])*factor) for x,y in pts]
    if not dashed:d.line(q,fill=color,width=4,joint='curve')
    else:
        import math
        for a,b in zip(q,q[1:]):
            length=math.dist(a,b)
            for t in range(0,int(length),12):
                u=t/max(length,1);v=min(t+7,length)/max(length,1)
                d.line([(a[0]+(b[0]-a[0])*u,a[1]+(b[1]-a[1])*u),(a[0]+(b[0]-a[0])*v,a[1]+(b[1]-a[1])*v)],fill=color,width=3)
    dash=' stroke-dasharray="7 5"' if dashed else ''
    svg.append(f'<polyline points="{" ".join(f"{x:.2f},{y:.2f}" for x,y in q)}" fill="none" stroke="{color}" stroke-width="{3 if dashed else 4}"{dash}/>')
txt(22,680,'Оранжевый — главные переломы. Голубой пунктир — рельеф вблизи.',26)
txt(22,724,'1  Нижний край грудных: собрать непрерывный поворот плоскостей, без острых зубцов.',25)
txt(22,762,'2  Рёберная дуга: отделить грудную клетку от живота; не превращать в край нагрудника.',25)
txt(22,800,'3  Пресс: центральная линия и два коротких поперечных излома, без глубоких прорезей.',25)
txt(22,850,'Это разметка направлений, не новый меш. Кадры увеличены для чтения, свет и масштаб источников различаются.',21,'#555555')
canvas.save(out/'comparison.png');svg.append('</svg>');(out/'comparison.svg').write_text('\n'.join(svg),encoding='utf-8')
(out/'lines.json').write_text(json.dumps(dict(model_crop=modelbox,reference_crop=refbox,lines=[dict(label=l,color=c,dashed=d,points=p) for l,c,d,p in lines],sources={str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in (reference,model)}),ensure_ascii=False,indent=2),encoding='utf-8')
print(out/'comparison.png')
