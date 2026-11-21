"""Сравнения растров: единый изотропный масштаб, без деформаций силуэта."""
from pathlib import Path
import json, hashlib
import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage as ndi

ROOT = Path(__file__).resolve().parent
MAIN = ROOT.parents[3].parent / 'ChimeraEvolution'
ref = MAIN / 'Референсы/витрина/лось/вид.png'
style = MAIN / 'Референсы/работа/дизайн/lowpoly-2k-chelovek-2026-11-04/01-flat-sheet.png'
old = Image.open(ref).convert('RGB')
new = Image.open(ROOT/'01-flat-sheet.png').convert('RGB')
# Координаты по просмотру, в долях листа; нижние подписи исключены.
oldboxes = [(680/1672,55/941,1163/1672,545/941),
            (65/1672,55/941,642/1672,545/941),
            (25/1672,572/941,438/1672,904/941),
            (1210/1672,55/941,1618/1672,545/941)]
newboxes = [(25/2048,55/683,383/2048,559/683),
            (423/2048,55/683,825/2048,559/683),
            (861/2048,55/683,1203/2048,559/683),
            (1254/2048,55/683,1590/2048,559/683)]

def cut(im, box):
    return im.crop(tuple(round(v * (im.width if i%2==0 else im.height)) for i,v in enumerate(box)))

def extract(im):
    g=np.asarray(im.convert('L'))
    raw=g<210
    # Линия земли иначе замыкает промежутки между ногами при fill_holes.
    line=ndi.binary_opening(raw,structure=np.ones((1,max(30,im.width//2))))
    line[:round(im.height*.96)]=False
    raw &= ~line
    mask=ndi.binary_opening(raw, iterations=1)
    lab,n=ndi.label(mask)
    sizes=np.bincount(lab.ravel()); sizes[0]=0
    mask=lab==sizes.argmax()
    mask=ndi.binary_fill_holes(ndi.binary_closing(mask, iterations=1))
    ys,xs=np.where(mask)
    box=(xs.min(),ys.min(),xs.max()+1,ys.max()+1)
    return im.crop(box),Image.fromarray(np.uint8(mask)*255).crop(box)

def norm(im,mask):
    size=(round(im.width*500/im.height),500)
    return im.resize(size),mask.resize(size,Image.Resampling.NEAREST)

comp=Image.new('RGB',(2400,1150),'white'); d=ImageDraw.Draw(comp)
over=Image.new('RGB',(2400,620),'white'); od=ImageDraw.Draw(over)
report=[]
for i,name in enumerate(['FRONT','PROFILE','FRONT 3/4','BACK']):
    a,ma=norm(*extract(cut(old,oldboxes[i])))
    b,mb=norm(*extract(cut(new,newboxes[i])))
    x=i*600
    for row,(im,m,title) in enumerate([(a,ma,'VITRINA'),(b,mb,'LOWPOLY')]):
        panel=Image.new('RGB',im.size,'white'); panel.paste(im,(0,0),m)
        comp.paste(panel,(x+(600-im.width)//2,35+row*560))
        d.text((x+15,12+row*560),name+' / '+title,fill='black')
    am=Image.new('L',(600,500)); bm=am.copy()
    am.paste(ma,((600-ma.width)//2,0)); bm.paste(mb,((600-mb.width)//2,0))
    aa=np.asarray(am)>0; bb=np.asarray(bm)>0
    rgb=np.full((500,600,3),255,dtype=np.uint8)
    rgb[aa]=[80,175,220]; rgb[bb]=[230,105,90]; rgb[aa&bb]=[90,80,110]
    over.paste(Image.fromarray(rgb),(x,55))
    od.text((x+15,15),name+' / full height + center',fill='black')
    iou=float((aa&bb).sum()/(aa|bb).sum())
    report.append({'view':name,'silhouette_iou':round(iou,4),
                   'width_ratio_lowpoly_to_vitrina':round(b.width/a.width,4)})
od.text((20,580),'BLUE: VITRINA   RED: LOWPOLY   DARK: OVERLAP. Isotropic full silhouette height; automatic approximate masks.',fill='black')
comp.save(ROOT/'03-compare-vitrina.png'); over.save(ROOT/'04-silhouette-overlays.png')
(ROOT/'silhouette-verification.json').write_text(json.dumps({'method':'Threshold <210, opening, largest component, closing, fill holes; equal total height including antlers, centered; no warping. Illustrative 3/4 poses differ; no rear 3/4 in vitrina.','views':report},indent=2),encoding='utf-8')
(ROOT/'sources.json').write_text(json.dumps([{'path':str(p),'size':list(Image.open(p).size),'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in [ref,style]],indent=2,ensure_ascii=False),encoding='utf-8')
print(json.dumps(report,indent=2))
