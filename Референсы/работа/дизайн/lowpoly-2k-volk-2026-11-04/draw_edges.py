"""Границы видимых плоскостей исходного растра, не реконструкция сетки.

Запуск из любой папки: C:/ProgramData/Anaconda3/python.exe <этот файл>
"""
from pathlib import Path
import hashlib
import json
import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage as ndi

ROOT = Path(__file__).resolve().parent
source = Image.open(ROOT / '01-flat-sheet.png').convert('RGB')
a = np.asarray(source)
gray = np.asarray(source.convert('L'), dtype=float)
smooth = ndi.gaussian_filter(gray, 0.65)
gx = ndi.sobel(smooth, axis=1) / 8
gy = ndi.sobel(smooth, axis=0) / 8
mag = np.hypot(gx, gy)
angle = (np.degrees(np.arctan2(gy, gx)) + 180) % 180
thin = np.zeros_like(mag, dtype=bool)
for lo, hi, shift in [(0, 22.5, (0, 1)), (22.5, 67.5, (1, 1)),
                      (67.5, 112.5, (1, 0)), (112.5, 157.5, (1, -1)),
                      (157.5, 180, (0, 1))]:
    sector = (angle >= lo) & (angle < hi)
    p = np.roll(mag, shift, axis=(0, 1))
    n = np.roll(mag, (-shift[0], -shift[1]), axis=(0, 1))
    thin |= sector & (mag >= p) & (mag >= n)
weak = thin & (mag > 2.1)
strong = thin & (mag > 4.5)
edges = ndi.binary_propagation(strong, mask=weak, structure=np.ones((3, 3)))
# Текст и линия земли не являются рёбрами фигуры.
edges[round(source.height * .844):] = False
edges[:2] = False
edges[:, :2] = False
edges[:, -2:] = False
labels, count = ndi.label(edges, structure=np.ones((3, 3)))
sizes = np.bincount(labels.ravel())
keep = sizes >= 7
keep[0] = False
edges = keep[labels]
result = a.copy()
result[edges] = [42, 42, 42]
Image.fromarray(result).save(ROOT / '02-edges-sheet.png')
mask = Image.fromarray((edges * 255).astype('uint8'))
mask.save(ROOT / 'edges-mask.png')

# Сравнение ракурсов и силуэтов: только равномерный масштаб по высоте.
workspace = ROOT.parents[3]
main = workspace.parent / 'ChimeraEvolution'
refpath = main / 'Референсы/витрина/волк/вид.png'
vitrina = Image.open(refpath).convert('RGB')
def crop(im, box):
    return im.crop(tuple(round(v * (im.width if i % 2 == 0 else im.height)) for i,v in enumerate(box)))
views = {
 'front': ((.025,.045,.135,.848), (.712,.08,.832,.55)),
 'profile': ((.145,.045,.458,.848), (.039,.075,.399,.55)),
 'front34': ((.443,.045,.651,.848), (.033,.59,.262,.95)),
 'back': ((.665,.045,.765,.848), (.863,.08,.977,.55)),
}
def silhouette(im, threshold):
    m = np.asarray(im.convert('L')) < threshold
    # Убираем тонкие линии сетки/земли до связных компонент.
    m = ndi.binary_opening(m, structure=np.ones((3,3)))
    labels,n = ndi.label(m)
    sizes = np.bincount(labels.ravel()); sizes[0] = 0
    m = labels == sizes.argmax()
    # Не заливать большие просветы между лапами, даже если тень их замкнула.
    holes=ndi.binary_fill_holes(m)&~m
    hl,hn=ndi.label(holes); hs=np.bincount(hl.ravel())
    small=hs<64; small[0]=False; m|=small[hl]
    yy,xx = np.where(m)
    box = (int(xx.min()),int(yy.min()),int(xx.max()+1),int(yy.max()+1))
    clean=np.asarray(im).copy(); clean[~m]=255
    return Image.fromarray(clean).crop(box), Image.fromarray((m*255).astype('uint8')).crop(box)
def fit(im, height=450):
    return im.resize((round(im.width*height/im.height),height),Image.Resampling.NEAREST)
comparison = Image.new('RGB',(1680,1080),'white')
overlays = Image.new('RGB',(1680,560),'white')
dc,do = ImageDraw.Draw(comparison),ImageDraw.Draw(overlays)
metrics = {}
for i,(name,(newbox,oldbox)) in enumerate(views.items()):
    new, nm = silhouette(crop(source,newbox),235)
    old, om = silhouette(crop(vitrina,oldbox),210)
    new,nm,old,om = [fit(im) for im in [new,nm,old,om]]
    # Profile needs more width; all panels use same scale and remain uncropped.
    canvasw = 800 if name=='profile' else 400
    row = 1 if i>=2 else 0
    col = i%2
    for j,(panel,title) in enumerate([(old,'VITRINA'),(new,'LOWPOLY')]):
        # Общая высота для обоих изображений пары даже в широком профиле.
        pairheight=min(450,round(390*450/max(old.width,new.width)))
        panel=fit(panel,pairheight)
        x=col*840+j*420+(420-panel.width)//2; y=row*540+50
        comparison.paste(panel,(x,y)); dc.text((col*840+j*420+15,row*540+20),name+' '+title,fill='black')
    # Overlay at full 450 px height, larger profile panel saved separately.
    w=max(nm.width,om.width)+40
    aa=np.zeros((490,w),bool); bb=aa.copy()
    aa[20:470,(w-om.width)//2:(w-om.width)//2+om.width]=np.asarray(om)>0
    bb[20:470,(w-nm.width)//2:(w-nm.width)//2+nm.width]=np.asarray(nm)>0
    rgb=np.full((490,w,3),255,np.uint8)
    rgb[aa & ~bb]=[0,170,205]; rgb[bb & ~aa]=[225,65,150]; rgb[aa & bb]=[75,75,85]
    overlay=Image.fromarray(rgb); overlay.save(ROOT/(name+'-overlay.png'))
    overlay.thumbnail((410,490)); overlays.paste(overlay,(i*420+(420-overlay.width)//2,45))
    do.text((i*420+10,12),name+' / cyan ref / magenta new',fill='black')
    metrics[name]={'iou':round(float((aa&bb).sum()/(aa|bb).sum()),4),
      'width_ratio_at_equal_height':round(nm.width/om.width,4)}
comparison.save(ROOT/'03-compare-vitrina.png')
overlays.save(ROOT/'04-silhouette-overlays.png')
# Витрина вид.png не содержит заднего 3/4: показываем отдельный утверждённый источник.
rearpath=main/'Референсы/витрина/волк/сзади-3-4.png'
rear,_=silhouette(Image.open(rearpath).convert('RGB'),235)
rear=fit(rear,620).transpose(Image.Transpose.FLIP_LEFT_RIGHT)
pair=Image.new('RGB',(1500,740),'white'); pair.paste(rear,((750-rear.width)//2,45))
newrear,_=silhouette(crop(source,(.78,.045,.99,.848)),235); newrear=fit(newrear,620)
pair.paste(newrear,(750+(750-newrear.width)//2,45))
pd=ImageDraw.Draw(pair); pd.text((20,15),'VITRINA REAR 3/4 (mirrored, different camera)',fill='black'); pd.text((770,15),'LOWPOLY REAR 3/4',fill='black')
pair.save(ROOT/'05-compare-rear34.png')
for name,box in {'profile-torso':(.238,.27,.401,.52),'front-head':(.04,.055,.126,.30)}.items():
    panel=crop(source,box); panel.resize((panel.width*3,panel.height*3)).save(ROOT/(name+'.png'))
assert np.array_equal(result[~edges],a[~edges])
for f in ROOT.glob('*.png'):
    with Image.open(f) as im: im.verify()
refs=[refpath,main/'Референсы/витрина/волк/орто.png',rearpath,
 main/'Референсы/работа/дизайн/lowpoly-2k-chelovek-2026-11-04/01-flat-sheet.png']
(ROOT/'sources.json').write_text(json.dumps([{'path':str(p),'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'size':list(Image.open(p).size)} for p in refs],ensure_ascii=False,indent=2),encoding='utf-8')
report={'size':list(source.size),'edge_pixels':int(edges.sum()),'unchanged_outside_edges':True,
 'silhouette_metrics':metrics,'registration':'uniform scale to mask height 450; bbox centers aligned; no nonuniform warp',
 'thresholds':{'new':235,'vitrina':210},'limitation':'image threshold masks approximate, not mesh measurements; front34 cameras differ',
 'sha256':{p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in ROOT.glob('*.png')}}
(ROOT/'verification.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in report.items() if k!='sha256'},indent=2))

