"""Исходник рисунка. Anaconda + Pillow; координаты сняты вручную с актуального листа."""
from pathlib import Path
import json, hashlib, math
from PIL import Image, ImageDraw, ImageFont

OUT = Path(__file__).resolve().parent
ROOT = OUT.parents[3]
SRC = ROOT.parent / 'ChimeraEvolution/Референсы/витрина/человек'
DATA = {}
def view(name, box, figura):
    DATA[name] = dict(box=box, figura=figura, linii=[])
def line(v, name, pts, secondary=False):
    DATA[v]['linii'].append(dict(imya=name, tochki=pts, punktir=secondary))

view('front',[40,25,380,920],[52,37,368,907])
view('profile',[465,25,640,920],[475,33,629,907])
view('three_quarter',[760,25,1070,920],[770,36,1058,910])
view('back',[1140,25,1500,920],[1156,37,1482,907])
# Грудная: от грудины вниз, затем вверх к подмышке; дуга ниже, отдельная.
for pts in ([[201,261],[184,268],[158,267],[140,253],[132,241]],
            [[220,261],[239,269],[263,265],[280,251],[288,240]]):
    line('front','Г1 — нижний край грудной',pts)
line('front','Г2 — рёберная дуга Λ',[[151,313],[171,298],[192,282],[210,278],[230,285],[249,300],[269,314]])
line('front','Г3 — паховая V слева, видимое',[[157,365],[174,379],[191,388]])
line('front','Г3 — паховая V справа, видимое',[[265,365],[249,379],[232,388]])
for pts in ([[106,218],[116,238],[130,252]],[[312,216],[306,238],[292,254]]):
    line('front','Г4 — дельта / бицепс',pts)
line('front','В1 — белая линия',[[210,283],[210,318],[210,347],[211,360]],True)
for y in (311,338):
    line('front','В2 — поперечный излом пресса',[[183,y+2],[197,y],[205,y+1]],True)
    line('front','В2 — поперечный излом пресса',[[217,y+1],[229,y],[240,y+3]],True)
for pts in ([[87,331],[96,351],[89,380],[74,409]],[[331,331],[322,351],[330,381],[345,409]]):
    line('front','В3 — плечелучевая масса',pts,True)

# Профиль: лицо влево. Рука скрывает бок, невидимые линии не проводим по руке.
line('profile','Г1 — нижний край грудной, видимое',[[504,260],[516,268],[530,266],[544,257]])
line('profile','Г2 — рёберная дуга, видимое',[[505,301],[521,311],[538,319]])
line('profile','Г3 — паховая V, видимое',[[506,368],[518,378],[532,384]])
line('profile','Г4 — дельта / бицепс',[[552,217],[565,240],[584,257],[602,238],[613,217]])
line('profile','В3 — плечелучевая масса',[[606,331],[587,350],[576,381],[560,414]],True)

line('three_quarter','Г1 — ближний край грудной',[[901,263],[924,270],[946,265],[965,251],[975,240]])
line('three_quarter','Г1 — дальний край грудной',[[854,260],[869,269],[887,266]])
line('three_quarter','Г2 — рёберная дуга Λ',[[864,311],[880,292],[895,279],[917,288],[940,303],[955,315]])
line('three_quarter','Г3 — паховая V, ближняя',[[958,364],[938,378],[917,388]])
line('three_quarter','Г3 — паховая V, дальняя',[[862,369],[875,383]])
line('three_quarter','Г4 — ближняя дельта / бицепс',[[978,218],[990,241],[1008,255],[1020,237]])
line('three_quarter','Г4 — дальняя дельта / бицепс',[[837,221],[845,243],[851,251]])
line('three_quarter','В1 — белая линия',[[896,284],[893,317],[891,346],[891,360]],True)
for y in (311,338):
    line('three_quarter','В2 — излом пресса, дальний',[[871,y+1],[882,y],[888,y+1]],True)
    line('three_quarter','В2 — излом пресса, ближний',[[898,y],[915,y+1],[929,y+5]],True)
line('three_quarter','В3 — ближняя плечелучевая масса',[[1030,333],[1020,355],[1026,383],[1039,411]],True)
line('three_quarter','В3 — дальняя плечелучевая масса',[[818,338],[824,354],[813,385],[801,410]],True)

# Спина: три разных семейства; лопатка не замыкается в пластину.
for pts in ([[1311,151],[1280,173],[1248,190],[1291,218],[1305,251],[1321,281]],
            [[1333,151],[1363,173],[1396,190],[1353,218],[1339,251],[1327,281]]):
    line('back','Г5 — боковой край ромба трапеции',pts)
for pts in ([[1236,218],[1260,225],[1284,231]],[[1409,218],[1385,225],[1361,231]]):
    line('back','Г6 — ость лопатки',pts)
for pts in ([[1286,238],[1287,255],[1279,274]],[[1359,238],[1358,255],[1366,274]]):
    line('back','Г7 — медиальный край лопатки',pts)
for pts in ([[1240,272],[1252,299],[1265,329],[1275,360]],[[1403,272],[1392,299],[1379,329],[1370,360]]):
    line('back','Г8 — наружный край широчайшей',pts)
for pts in ([[1213,219],[1224,238],[1237,250]],[[1430,219],[1420,239],[1407,252]]):
    line('back','Г4 — задняя граница дельты',pts)
for pts in ([[1196,335],[1205,355],[1193,385],[1180,411]],[[1445,335],[1437,355],[1448,384],[1461,411]]):
    line('back','В3 — плечелучевая масса, боковой край',pts,True)
line('back','В4 — ось разгибателей, слева',[[1312,286],[1305,321],[1307,366]],True)
line('back','В4 — ось разгибателей, справа',[[1333,286],[1340,321],[1338,366]],True)

FONT='C:/Windows/Fonts/arial.ttf'
def font(n): return ImageFont.truetype(FONT,n)
ORANGE='#db6416'; BLUE='#007bba'; RED='#b72865'
def stroke(draw, pts, color, width, dashed=False):
    if not dashed:
        draw.line(pts, fill=color,width=width,joint='curve'); return
    phase=0
    for a,b in zip(pts,pts[1:]):
        length=math.dist(a,b)
        for i in range(math.ceil(length)):
            if (phase+i)%13<8:
                t=i/length; u=min(i+1,length)/length
                draw.line([(a[0]+(b[0]-a[0])*t,a[1]+(b[1]-a[1])*t),(a[0]+(b[0]-a[0])*u,a[1]+(b[1]-a[1])*u)],fill=color,width=width)
        phase+=math.ceil(length)

def main():
    source=Image.open(SRC/'вид.png').convert('RGB')
    assert source.size==(1536,1024), source.size
    json_path=OUT/'linii-lista.json'
    json_path.write_text(json.dumps(DATA,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    # PNG использует сохранённый JSON, а не отдельный набор координат.
    data=json.loads(json_path.read_text(encoding='utf-8'))
    sheet=source.copy(); d=ImageDraw.Draw(sheet)
    for name,v in data.items():
        x0,y0,x1,y1=v['box']
        for l in v['linii']:
            assert type(l['punktir']) is bool and len(l['tochki'])>=2
            assert all(x0<=x<=x1 and y0<=y<=y1 for x,y in l['tochki'])
            pts=[tuple(p) for p in l['tochki']]
            stroke(d,pts,BLUE if l['punktir'] else ORANGE,3,l['punktir'])
    sheet.save(OUT/'01-lines-sheet.png')
    for name,v in data.items():
        crop=sheet.crop(v['box']); crop.resize((crop.width*2,crop.height*2)).save(OUT/f'{name}.png')
    # Крупный торс: те же линии, читабельные идентификаторы с выносками.
    board=Image.new('RGB',(1600,900),'#f6f5f1'); bd=ImageDraw.Draw(board)
    bd.text((30,18),'ПОДЖАРЫЙ · направления перелома формы',font=font(30),fill='#222222')
    for n,(name,rect) in enumerate([('front',(90,175,325,395)),('back',(1195,145,1448,390))]):
        tile=sheet.crop(rect).resize(((rect[2]-rect[0])*2,(rect[3]-rect[1])*2))
        x=40+n*580; board.paste(tile,(x,90))
        bd.text((x,600),'Анфас' if n==0 else 'Спина',font=font(25),fill='#222222')
    bd.text((1180,100),'Оранжевый — главные',font=font(23),fill=ORANGE)
    bd.text((1180,145),'Голубой пунктир —',font=font(23),fill=BLUE)
    bd.text((1180,178),'второстепенные',font=font(23),fill=BLUE)
    for i,t in enumerate(['Г1 край грудной','Г2 рёберная Λ','Г3 паховая V','Г4 граница дельты','Г5 ромб трапеции','Г6 ость лопатки','Г7 медиальный край','Г8 край широчайшей']):
        bd.text((1180,245+i*39),t,font=font(23),fill=ORANGE)
    for i,t in enumerate(['В1 белая линия','В2 два излома пресса','В3 плечелучевая масса','В4 разгибатели спины']):
        bd.text((40,675+i*40),t,font=font(23),fill=BLUE)
    bd.text((620,675),'Линия = смена плоскости,',font=font(25),fill='#222222')
    bd.text((620,714),'не борозда и не накладная пластина.',font=font(25),fill='#222222')
    bd.text((620,775),'Г3 обрывается у одежды.',font=font(25),fill='#222222')
    board.save(OUT/'02-torso-detail.png')
    no=Image.new('RGB',(1500,850),'#f6f5f1'); nd=ImageDraw.Draw(no)
    nd.text((30,20),'ГДЕ НЕ ДОБАВЛЯТЬ РЁБРА · розовый — запрещённый рисунок',font=font(29),fill=RED)
    for i in range(2):
        tile=source.crop((125,220,295,395)).resize((510,525)); no.paste(tile,(80+i*740,100))
    def bad(points): stroke(nd,points,RED,7)
    bad([(105,230),(320,300),(565,230)])
    bad([(105,230),(130,430),(320,535),(540,430),(565,230)])
    nd.text((60,655),'НЕТ: грудная + рёбра одним замкнутым щитом',font=font(23),fill=RED)
    bad([(860,420),(1015,420),(1280,420)])
    for y in (490,560):
        bad([(945,y),(1050,y),(1050,y+45),(945,y+45),(945,y)])
        bad([(1080,y),(1185,y),(1185,y+45),(1080,y+45),(1080,y)])
    nd.text((800,655),'НЕТ: полка поперёк живота, закрытые кубики',font=font(23),fill=RED)
    nd.text((60,725),'Не обводить каждую мышцу. Не соединять края лопаток в бронепластины.',font=font(26),fill='#222222')
    nd.text((60,770),'Не переносить случайные треугольники исходного рендера в сетку.',font=font(26),fill='#222222')
    no.save(OUT/'03-no-armor.png')
    sources={p.name:dict(path=str(p),sha256=hashlib.sha256(p.read_bytes()).hexdigest(),size=list(Image.open(p).size)) for p in (SRC/'вид.png',SRC/'мышцы.png')}
    (OUT/'sources.json').write_text(json.dumps(sources,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(f'OK: {len(data)} views, {sum(len(v["linii"]) for v in data.values())} polylines; PNG rendered from JSON; all coordinates within boxes.')
if __name__=='__main__': main()
