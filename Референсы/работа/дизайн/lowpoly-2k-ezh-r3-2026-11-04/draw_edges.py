"""Производные диагностические листы: рёбра растра и сравнение силуэтов.
Не меняет исходную генерацию и не реконструирует меш.
Запуск: C:/ProgramData/Anaconda3/python.exe <путь к этому файлу>
"""
from pathlib import Path
import hashlib
import json
import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage as ndi

ROOT = Path(__file__).resolve().parent
WORKSPACE = ROOT.parents[3]
REF = WORKSPACE.parent / 'ChimeraEvolution/Референсы/витрина/ёж/вид.png'
src = Image.open(ROOT/'01-flat-sheet.png').convert('RGB')
ref = Image.open(REF).convert('RGB')
assert src.size == (1619, 972)
views = [
    ('FRONT', (804,77,1169,502), (43,35,378,441)),
    ('PROFILE', (39,76,751,504), (430,26,1064,440)),
    ('FRONT 3/4', (22,565,497,888), (1110,36,1600,441)),
    ('REAR', (1260,73,1635,505), (90,501,445,940)),
]

def segment(im, threshold=225):
    mask = np.asarray(im.convert('L')) < threshold
    lab, count = ndi.label(mask)
    assert count
    sizes = np.bincount(lab.ravel()); sizes[0] = 0
    body = lab == sizes.argmax()
    holes, _ = ndi.label(~body)
    sizes = np.bincount(holes.ravel())
    small = sizes <= 12; small[0] = False
    body |= small[holes]
    return body

def tight(mask):
    yy, xx = np.where(mask)
    return mask[yy.min():yy.max()+1, xx.min():xx.max()+1]

def normalized(mask):
    mask = tight(mask)
    h,w = mask.shape
    width = round(w*360/h)
    assert width < 800
    resized = np.asarray(Image.fromarray(mask).resize((width,360), Image.Resampling.NEAREST))
    canvas = np.zeros((380,800),bool)
    canvas[10:370,(800-width)//2:(800-width)//2+width] = resized
    return canvas, w/h

# Тонкие границы тональных плоскостей, без случайной триангуляции.
a = np.asarray(src)
g = ndi.gaussian_filter(np.asarray(src.convert('L'),float), .65)
gx,gy = ndi.sobel(g,axis=1)/8,ndi.sobel(g,axis=0)/8
mag = np.hypot(gx,gy)
angle = np.degrees(np.arctan2(gy,gx)) % 180
thin = np.zeros(g.shape,bool)
for lo,hi,shift in [(0,22.5,(0,1)),(22.5,67.5,(1,1)),(67.5,112.5,(1,0)),(112.5,157.5,(1,-1)),(157.5,180,(0,1))]:
    thin |= (angle>=lo)&(angle<hi)&(mag>=np.roll(mag,shift,axis=(0,1)))&(mag>=np.roll(mag,(-shift[0],-shift[1]),axis=(0,1)))
edges = ndi.binary_propagation(thin&(mag>4.5),mask=thin&(mag>2.1))
body_area = np.zeros(g.shape,bool)
for box in [v[2] for v in views]+[(565,510,1060,940)]:
    x0,y0,x1,y1=box
    body_area[y0:y1,x0:x1] |= ndi.binary_dilation(segment(src.crop(box)),iterations=1)
edges &= body_area
lab,_ = ndi.label(edges,structure=np.ones((3,3)))
sizes=np.bincount(lab.ravel()); keep=sizes>=7; keep[0]=False
edges=keep[lab]
out=a.copy(); out[edges]=(35,35,35)
Image.fromarray(out).save(ROOT/'02-edges-sheet.png')
Image.fromarray(np.uint8(edges)*255).save(ROOT/'edges-mask.png')
assert np.array_equal(out[~edges],a[~edges])

compare=Image.new('RGB',(1640,4*440),'white'); dc=ImageDraw.Draw(compare)
over=Image.new('RGB',(820,4*420),'white'); do=ImageDraw.Draw(over)
metrics=[]
for i,(name,rb,nb) in enumerate(views):
    ri,ni=ref.crop(rb),src.crop(nb)
    rm,nm=segment(ri),segment(ni)
    r,ra=normalized(rm); n,na=normalized(nm)
    iou=float((r&n).sum()/(r|n).sum())
    sensitivity={}
    for t in [205,215,225,235]:
        rt,_=normalized(segment(ri,t)); nt,_=normalized(segment(ni,t))
        sensitivity[str(t)]=round(float((rt&nt).sum()/(rt|nt).sum()),4)
    for im,offset in [(ri,0),(ni,820)]:
        scale=min(790/im.width,380/im.height)
        im=im.resize((round(im.width*scale),round(im.height*scale)),Image.Resampling.LANCZOS)
        compare.paste(im,(offset+(820-im.width)//2,i*440+45))
    dc.text((12,i*440+10),f'{name} | VITRINA',fill='black')
    dc.text((832,i*440+10),f'{name} | R3',fill='black')
    pix=np.full((380,800,3),255,np.uint8)
    pix[r]=(50,150,225);pix[n]=(245,140,55);pix[r&n]=(70,70,70)
    over.paste(Image.fromarray(pix),(10,i*420+30))
    do.text((12,i*420+5),f'{name} | IoU {iou:.3f} | width ratio {na/ra:.3f}',fill='black')
    Image.fromarray(np.uint8(r)*255).save(ROOT/f'mask-vitrina-{i+1}.png')
    Image.fromarray(np.uint8(n)*255).save(ROOT/f'mask-r3-{i+1}.png')
    metrics.append({'view':name,'reference_crop':rb,'candidate_crop':nb,'iou':round(iou,4),'width_ratio_equal_total_height':round(na/ra,4),'threshold_sensitivity':sensitivity})
compare.save(ROOT/'03-compare-vitrina.png')
over.save(ROOT/'04-silhouette-overlay.png')
for name,box in {'profile-head':(433,185,610,303),'profile-foreleg':(540,209,721,438),'profile-flank':(706,223,860,347),'profile-quills':(601,70,1046,329),'rear-coat':(96,565,440,830)}.items():
    im=src.crop(box)
    im.resize((im.width*3,im.height*3)).save(ROOT/(name+'.png'))
files=['00-first-pass.png','01-flat-sheet.png','02-edges-sheet.png','03-compare-vitrina.png','04-silhouette-overlay.png','edges-mask.png']
for file in ROOT.glob('*.png'):
    with Image.open(file) as im: im.verify()
report={'size':src.size,'edge_pixels':int(edges.sum()),'unchanged_outside_edges':True,
    'method':'manual crops; largest connected L<225 component; holes <=12px filled; tight bounding box; uniform scaling to total height 360 including launchers; centered horizontally and ground-aligned; no nonuniform warp; approximate raster masks, possible residual shadow',
    'overlay_colors':{'blue':'vitrina','orange':'r3','dark_gray':'intersection'},
    'comparisons':metrics,'rear_three_quarter':'no matching view in vitrina',
    'sha256':{f:hashlib.sha256((ROOT/f).read_bytes()).hexdigest() for f in files},
    'reference_sha256':hashlib.sha256(REF.read_bytes()).hexdigest()}
(ROOT/'verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(report,ensure_ascii=False,indent=2))
