"""Compose an SVG scale diagram from unchanged reference-sheet viewports."""
from pathlib import Path
import base64, json, sys
from PIL import Image
import numpy as np
from scipy import ndimage
sys.stdout.reconfigure(encoding='utf-8')
OUT = Path(__file__).resolve().parent
ROOT = OUT.parents[3]
SRC = ROOT / 'Референсы/работа/дизайн/lowpoly-stupeni-2026-10-20'
items = [
    dict(id='human', file='chelovek-volk.png', box=(130,680,292,983), crown=684, foot=981, height=1.85, cx=1290, label='ЧЕЛОВЕК', note='база · 1,00×'),
    dict(id='wolf', file='chelovek-volk.png', box=(826,679,1070,983), crown=706, foot=981, height=1.92, cx=880, label='ОБОРОТЕНЬ', note='ст.2 · 1,04×'),
    dict(id='moose', file='chelovek-los.png', box=(752,730,955,1050), crown=760, foot=1048, height=3.10, cx=1800, label='ЧЕЛОВЕК–ЛОСЬ', note='ст.2 · 1,68×'),
    dict(id='hedge', file='chelovek-ezh.png', box=(745,677,1045,976), crown=778, foot=974, height=1.58/1.5, cx=390, label='ХЕДЖХАЛК', note='примерка: уменьшен в 1,5 раза'),
    dict(id='snake', file='chelovek-zmeya.png', box=(750,668,1066,957), crown=669, foot=955, height=1.75, cx=2370, label='ЧЕЛОВЕК–ЗМЕЯ', note='условная цель · 0,95×'),
]
def uri(file):
    return 'data:image/png;base64,'+base64.b64encode((SRC/file).read_bytes()).decode('ascii')
for item in items:
    im = Image.open(SRC/item['file']).convert('RGB')
    x0,y0,x1,y1=item['box']
    a=np.asarray(im)[y0:y1,x0:x1]
    mask=a.min(axis=2)<205
    lab,n=ndimage.label(mask)
    sizes=np.bincount(lab.ravel()); sizes[0]=0
    main=lab==sizes.argmax()
    ys,xs=np.nonzero(main)
    item['bounds']=[int(xs.min()+x0),int(ys.min()+y0),int(xs.max()+x0+1),int(ys.max()+y0+1)]
    item['foot']=item['bounds'][3]-1
    item['size']=im.size
    print(item['id'],item['bounds'],'crown',item['crown'])
parts=['<svg xmlns="http://www.w3.org/2000/svg" width="1800" height="560" viewBox="0 0 1800 560"><rect width="1800" height="560" fill="white"/>']
for i,item in enumerate(items):
    x0,y0,x1,y1=item['box']; w,h=item['size']
    k=min(350/(x1-x0),450/(y1-y0)); left=i*360
    parts += [f'<defs><clipPath id="p{i}"><rect x="{left}" y="50" width="350" height="450"/></clipPath></defs><g clip-path="url(#p{i})"><image x="{left-x0*k}" y="{50-y0*k}" width="{w*k}" height="{h*k}" href="{uri(item["file"])}"/>']
    for y in range(y0,y1,20):
        yy=50+(y-y0)*k
        parts += [f'<path d="M{left},{yy} h350" stroke="#ff0000" stroke-opacity=".3"/><text x="{left}" y="{yy}" font-size="12" fill="red">{y}</text>']
    parts += ['</g>',f'<text x="{left+15}" y="35" font-size="22">{item["id"]}</text>']
parts.append('</svg>')
(OUT/'preview.svg').write_text(''.join(parts),encoding='utf-8')
(OUT/'anchors.json').write_text(json.dumps(items,ensure_ascii=False,indent=2),encoding='utf-8')

# One common baseline and one scale. Viewport clipping never alters source pixels.
W,H,GROUND,PPM=2700,1450,1170,250
def text(x,y,s,size=25,fill='#26323e',anchor='start',weight='normal'):
    from html import escape
    return f'<text x="{x}" y="{y}" font-family="Arial,DejaVu Sans,sans-serif" font-size="{size}" font-weight="{weight}" text-anchor="{anchor}" fill="{fill}">{escape(s)}</text>'
parts=[f'<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" viewBox="0 0 {W} {H}"><rect width="100%" height="100%" fill="#fff"/>',text(100,75,'CHIMERA · СРАВНЕНИЕ РОСТА МОРФОВ',40,weight='bold'),text(100,120,'Одна линия земли · 250 пикселей = 1 метр · без перспективного уменьшения',26)]
for n in range(9):
    meters=n/2; y=GROUND-meters*PPM
    parts += [f'<path d="M85,{y} H2640" stroke="#dce2e8" stroke-width="{2 if n%2==0 else 1}"/>',text(70,y+8,f'{meters:g} м'.replace('.',','),21,anchor='end')]
parts += [f'<path d="M85,{GROUND-4*PPM} V{GROUND} H2640" fill="none" stroke="#445565" stroke-width="3"/>']
checks=[]
for idx,item in enumerate(sorted(items,key=lambda a:a['cx'])):
    x0,y0,x1,y1=item['box']; w,h=item['size']
    k=item['height']*PPM/(item['foot']-item['crown'])
    mid=(item['bounds'][0]+item['bounds'][2])/2
    ox=item['cx']-mid*k; oy=GROUND-item['foot']*k
    xx,yy=ox+x0*k,oy+y0*k
    parts += [f'<defs><clipPath id="f{idx}"><rect x="{xx}" y="{yy}" width="{(x1-x0)*k}" height="{(y1-y0)*k}"/></clipPath></defs>',f'<g clip-path="url(#f{idx})"><image x="{ox}" y="{oy}" width="{w*k}" height="{h*k}" href="{uri(item["file"])}"/></g>']
    cy=GROUND-item['height']*PPM
    bx0,by0,bx1,by1=item['bounds']
    left,right,top=ox+bx0*k,ox+bx1*k,oy+by0*k
    full_h=(GROUND-top)/PPM
    # Red marks bound the ENTIRE visible silhouette, not the hidden crown.
    # Blue is the independently specified body/crown height from project data.
    parts += [f'<path d="M{left-10},{GROUND} V{top} H{right+6} M{left-10},{GROUND} h16 M{right+6},{top} v16" fill="none" stroke="#bb6540" stroke-width="2.5" stroke-dasharray="8 5"/>',text((left+right)/2,top-15,f'Полный верх: {full_h:.2f} м'.replace('.',','),22,fill='#a24e2c',anchor='middle'),f'<path d="M{item["cx"]-55},{cy} h110" stroke="#2a6a9c" stroke-width="3" stroke-dasharray="4 4"/>',text(item['cx'],1230,item['label'],25,anchor='middle',weight='bold'),text(item['cx'],1270,f'Тело: {item["height"]:.2f} м'.replace('.',','),29,fill='#2a6a9c',anchor='middle',weight='bold'),text(item['cx'],1308,item['note'],22,anchor='middle')]
    if item['id']=='snake':
        parts += [text(item['cx'],1340,'ст.2 пока недостижима',21,fill='#9b603d',anchor='middle')]
    checks.append({'figure':item['id'],'height_m':item['height'],'full_silhouette_m':full_h,'full_top_y':top,'full_left_x':left,'full_right_x':right,'px_per_m':PPM,'crown_y':cy,'ground_y':GROUND,'drawing_scale':k,'source_crown':item['crown'],'source_foot':item['foot']})
    assert abs((GROUND-cy)/PPM-item['height'])<1e-9
    assert abs((oy+item['foot']*k)-GROUND)<1e-8
    assert abs((GROUND-top)/PPM-full_h)<1e-8
parts += [text(100,1380,'Красный контур — весь рисунок с рогами, ушами, иглами и трубками. Синяя отметка — макушка тела без них.',23),text(100,1414,'Хеджхалк: визуальная примерка ×2/3 по просьбе геймдизайнера. Остальные фигуры — прежний масштаб.',22,fill='#64717d'),text(100,1443,'Изменение примерки не внесено в игровые данные. Полный верх вычислен по рисунку.',22,fill='#64717d'),'</svg>']
(OUT/'comparison.svg').write_text(''.join(parts),encoding='utf-8')
(OUT/'scale-check.json').write_text(json.dumps(checks,ensure_ascii=False,indent=2),encoding='utf-8')
print('Scale verified: 5 figures, baseline',GROUND,'pixels/metre',PPM)
