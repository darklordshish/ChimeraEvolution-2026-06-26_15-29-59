from pathlib import Path
import json, hashlib
import numpy as np
from PIL import Image
from scipy import ndimage as ndi

ROOT=Path(__file__).resolve().parent
REF=ROOT.parents[3].parent/'ChimeraEvolution/Референсы/витрина/ёж/вид.png'
ref=Image.open(REF).convert('RGB')
boxes=[(30,25,398,480),(402,30,1026,480),(1027,30,1535,480),(30,510,414,980),(450,510,1000,980)]
refs=[(804,77,1169,502),(39,76,751,504),(22,565,497,888),(1260,73,1635,505)]
names=['front','profile','front34','rear']
def segment(im,t=225):
    lab,n=ndi.label(np.asarray(im.convert('L'))<t)
    sizes=np.bincount(lab.ravel());sizes[0]=0
    m=lab==sizes.argmax()
    yy,xx=np.where(m)
    return m[yy.min():yy.max()+1,xx.min():xx.max()+1]
def norm(m):
    h,w=m.shape
    a=np.asarray(Image.fromarray(m).resize((round(w*360/h),360),Image.Resampling.NEAREST))
    c=np.zeros((380,800),bool);x=(800-a.shape[1])//2;c[10:370,x:x+a.shape[1]]=a
    return c,w/h
def tile(im,w=800,h=420):
    im=im.copy();im.thumbnail((w-20,h-20),Image.Resampling.LANCZOS)
    c=Image.new('RGB',(w,h),'white');c.paste(im,((w-im.width)//2,h-10-im.height));return c
report={'reference_sha256':hashlib.sha256(REF.read_bytes()).hexdigest(),'method':'Largest connected L<225 component in manual crop, tight bounding box, uniform scaling to total height 360 including tubes, horizontal centering and ground alignment. No anisotropic stretching. Approximate masks include residual contact shadows. Threshold sensitivity 205/215/225.','reference_crops':refs,'candidate_crops':boxes,'variants':{}}
for v in ['A','B']:
    d=ROOT/v;src=Image.open(d/'01-flat-sheet.png').convert('RGB');assert src.size==(1536,1024)
    # Diagnostic tonal boundaries, not mesh topology.
    g=ndi.gaussian_filter(np.asarray(src.convert('L'),float),.65)
    gx,gy=ndi.sobel(g,axis=1)/8,ndi.sobel(g,axis=0)/8
    mag=np.hypot(gx,gy);ang=np.degrees(np.arctan2(gy,gx))%180;thin=np.zeros(g.shape,bool)
    for lo,hi,s in [(0,22.5,(0,1)),(22.5,67.5,(1,1)),(67.5,112.5,(1,0)),(112.5,157.5,(1,-1)),(157.5,180,(0,1))]:
        thin|=(ang>=lo)&(ang<hi)&(mag>=np.roll(mag,s,(0,1)))&(mag>=np.roll(mag,(-s[0],-s[1]),(0,1)))
    edges=ndi.binary_propagation(thin&(mag>4.5),mask=thin&(mag>2.1))
    a=np.asarray(src);out=a.copy();out[edges]=35
    assert np.array_equal(out[~edges],a[~edges])
    Image.fromarray(out).save(d/'02-edges-sheet.png');Image.fromarray(np.uint8(edges)*255).save(d/'edges-mask.png')
    comp=Image.new('RGB',(1600,1680),'white');over=Image.new('RGB',(800,1520),'white');rows=[]
    for i,(rb,nb,name) in enumerate(zip(refs,boxes,names)):
        ri,ni=ref.crop(rb),src.crop(nb);r,ra=norm(segment(ri));n,na=norm(segment(ni))
        iou=float((r&n).sum()/(r|n).sum());sensitivity={}
        for t in [205,215,225]:
            rt,_=norm(segment(ri,t));nt,_=norm(segment(ni,t));sensitivity[t]=round(float((rt&nt).sum()/(rt|nt).sum()),4)
        comp.paste(tile(ri),(0,i*420));comp.paste(tile(ni),(800,i*420))
        p=np.full((380,800,3),255,np.uint8);p[r]=(50,150,225);p[n]=(245,140,55);p[r&n]=70;over.paste(Image.fromarray(p),(0,i*380))
        Image.fromarray(np.where(r,0,255).astype('uint8')).save(d/f'mask-vitrina-{name}.png')
        Image.fromarray(np.where(n,0,255).astype('uint8')).save(d/f'mask-{v}-{name}.png')
        rows.append({'view':name,'iou':round(iou,4),'width_ratio_equal_total_height':round(na/ra,4),'width_difference_percent':round((na/ra-1)*100,2),'sensitivity':sensitivity})
    comp.save(d/'03-compare-vitrina.png');over.save(d/'04-silhouette-overlay.png')
    for name,box in {'head':(408,210,574,330),'foreleg':(540,238,715,472),'flank':(695,266,830,390),'quills':(570,105,1020,417),'rear-coat':(90,610,365,950)}.items():
        im=src.crop(box);im.resize((im.width*3,im.height*3)).save(d/f'detail-{name}.png')
    report['variants'][v]={'size':src.size,'edges_pixels':int(edges.sum()),'unchanged_outside_edges':True,'comparisons':rows,'rear34_iou':None}
compare=Image.new('RGB',(2400,840),'white')
for row,i in enumerate([1,2]):
    compare.paste(tile(ref.crop(refs[i])),(0,row*420))
    for col,v in enumerate(['A','B'],1):compare.paste(tile(Image.open(ROOT/v/'01-flat-sheet.png').crop(boxes[i])),(col*800,row*420))
compare.save(ROOT/'00-a-vs-b.png')
report['png_sha256']={str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in ROOT.rglob('*.png')}
for p in ROOT.rglob('*.png'):
    with Image.open(p) as im:im.verify()
assert hashlib.sha256(REF.read_bytes()).hexdigest()==report['reference_sha256']
(ROOT/'verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
print(json.dumps(report['variants'],indent=2))
