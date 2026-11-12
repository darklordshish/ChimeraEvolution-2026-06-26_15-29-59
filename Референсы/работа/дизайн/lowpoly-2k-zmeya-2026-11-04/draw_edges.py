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
edges[round(source.height * .93):] = False
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
# Exclude titles, labels and horizontal ground lines, retain only figure panels.
allowed=np.zeros_like(edges)
for x0,y0,x1,y1 in [(60,62,1710,387),(65,454,352,837),(407,480,915,837),(980,462,1239,837),(1320,465,1704,837)]:
    allowed[round(y0/887*source.height):round(y1/887*source.height),round(x0/1774*source.width):round(x1/1774*source.width)]=True
edges &= allowed
result=a.copy(); result[edges]=[42,42,42]
Image.fromarray(result).save(ROOT/'02-edges-sheet.png')
Image.fromarray(np.uint8(edges)*255).save(ROOT/'edges-mask.png')
assert np.array_equal(result[~edges],a[~edges])
workspace=ROOT.parents[3]
main=workspace.parent/'ChimeraEvolution'
refpath=main/'Референсы/витрина/змея/вид.png'
ref=Image.open(refpath).convert('RGB')
# Boxes in pixels of inspected source sheets. Uniform scaling only.
views=[('PROFILE',(28,155,1101,391),(75,68,1703,391)),('FRONT',(1145,118,1381,449),(68,454,351,837)),('FRONT 3/4',(30,547,508,858),(405,500,915,836)),('REAR',(1449,115,1635,450),(980,466,1238,836))]
def boxscale(im,box,base):
    return tuple(round(v*(im.width/base[0] if i%2==0 else im.height/base[1])) for i,v in enumerate(box))
def mask(im,threshold):
    ar=np.asarray(im.convert('L'))
    m=ar<threshold
    m=ndi.binary_opening(m,iterations=1)
    labs,n=ndi.label(m); sizes=np.bincount(labs.ravel()); sizes[0]=0
    m=labs==sizes.argmax()
    m=ndi.binary_fill_holes(m)
    return m
comparison=Image.new('RGB',(1500,1200),'white'); d=ImageDraw.Draw(comparison)
over=Image.new('RGB',(1500,1000),'white'); od=ImageDraw.Draw(over)
metrics={}
for i,(name,rb,sb) in enumerate(views):
    r=ref.crop(boxscale(ref,rb,(1672,941))); s=source.crop(boxscale(source,sb,(1774,887)))
    # For comparisons normalize by profile full length, other views by height.
    rm=mask(r,205); sm=mask(s,230)
    def trim(im,m):
        yy,xx=np.where(m); b=(int(xx.min()),int(yy.min()),int(xx.max()+1),int(yy.max()+1))
        return im.crop(b),Image.fromarray(np.uint8(m[b[1]:b[3],b[0]:b[2]])*255)
    r,rm=trim(r,rm); s,sm=trim(s,sm)
    if i==0:
        factor=650/max(r.width,s.width)
        # Both lengths normalized individually to 650 px.
        rr=r.resize((650,round(r.height*650/r.width))); ss=s.resize((650,round(s.height*650/s.width)))
        rmask=rm.resize(rr.size,Image.Resampling.NEAREST); smask=sm.resize(ss.size,Image.Resampling.NEAREST)
    else:
        rr=r.resize((round(r.width*225/r.height),225)); ss=s.resize((round(s.width*225/s.height),225))
        rmask=rm.resize(rr.size,Image.Resampling.NEAREST); smask=sm.resize(ss.size,Image.Resampling.NEAREST)
    y=i*300
    d.text((20,y+8),name+' / VITRINA',fill='black'); d.text((770,y+8),name+' / LOWPOLY',fill='black')
    comparison.paste(rr,(20,y+38)); comparison.paste(ss,(770,y+38))
    W=max(rmask.width,smask.width)+20; H=max(rmask.height,smask.height)+20
    aa=Image.new('L',(W,H)); bb=Image.new('L',(W,H))
    aa.paste(rmask,((W-rmask.width)//2,H-10-rmask.height)); bb.paste(smask,((W-smask.width)//2,H-10-smask.height))
    ma=np.asarray(aa)>0; mb=np.asarray(bb)>0
    rgb=np.full((H,W,3),255,dtype=np.uint8);rgb[ma]=[30,165,215];rgb[mb]=[225,70,125];rgb[ma&mb]=[55,65,75]
    oi=Image.fromarray(rgb); oi.save(ROOT/(name.lower().replace(' ','-').replace('/','')+'-overlay.png'))
    px=20+(i%2)*750; py=25+(i//2)*480
    od.text((px,py),name+' cyan=vitrina pink=candidate',fill='black'); over.paste(oi,(px,py+30))
    metrics[name]={'iou':round(float((ma&mb).sum()/(ma|mb).sum()),4),'reference_crop_size':list(r.size),'candidate_crop_size':list(s.size),'normalization':'equal full profile length' if i==0 else 'equal silhouette height'}
comparison.save(ROOT/'03-compare-vitrina.png');over.save(ROOT/'04-silhouette-overlays.png')
for name,box in {'profile-body':(430,265,1520,389),'profile-head':(119,68,351,181)}.items():
    crop=source.crop(boxscale(source,box,(1774,887)));crop.resize((crop.width*2,crop.height*2)).save(ROOT/(name+'.png'))
paths=[refpath,main/'Референсы/витрина/змея/паспорт.md',main/'Референсы/работа/дизайн/lowpoly-2k-chelovek-2026-11-04/01-flat-sheet.png']
(ROOT/'sources.json').write_text(json.dumps([{'path':str(p),'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in paths],ensure_ascii=False,indent=2),encoding='utf-8')
files=['01-flat-sheet.png','02-edges-sheet.png','03-compare-vitrina.png','04-silhouette-overlays.png','edges-mask.png','profile-body.png','profile-head.png']
for f in files:
    with Image.open(ROOT/f) as im:im.verify()
report={'size':list(source.size),'edge_pixels':int(edges.sum()),'unchanged_outside_edges':True,'silhouettes':metrics,'method':'Sobel NMS hysteresis; silhouette threshold largest component, uniform scale, aligned bbox center and base','sha256':{f:hashlib.sha256((ROOT/f).read_bytes()).hexdigest() for f in files}}
(ROOT/'verification.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps(report,indent=2))
