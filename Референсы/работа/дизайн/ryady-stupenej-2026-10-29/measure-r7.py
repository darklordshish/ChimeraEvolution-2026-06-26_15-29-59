from pathlib import Path
from PIL import Image, ImageDraw
import math, json

p = Path(__file__).resolve().parent
# Manually selected visible raster landmarks; uncertainty roughly 3-5 degrees.
sets = {
    'proba1': [dict(stage=1, hip=(540,523), withers=(460,198), knee=(535,655), hand=(470,691), support=(427,600), ground=938),
               dict(stage=2, hip=(1286,523), withers=(1131,163), knee=(1285,655), hand=(1194,701), support=(1149,1365), ground=938)],
    'korrekciya': [dict(stage=1, hip=(540,523), withers=(460,198), knee=(535,655), hand=(470,687), support=(427,600), ground=938),
                   dict(stage=2, hip=(1286,523), withers=(1131,163), knee=(1285,655), hand=(1180,698), support=(1149,1365), ground=938)]}
for name, rows in sets.items():
    im = Image.open(p / f'chelovek-los-r7-{name}.png').convert('RGB')
    assert im.size == (1536,1024)
    d = ImageDraw.Draw(im)
    for r in rows:
        x,y=r['hip']; wx,wy=r['withers']
        r['angle_deg']=round(math.degrees(math.atan2(abs(x-wx),abs(y-wy))),2)
        r['fingertips_below_knee_px']=r['hand'][1]-r['knee'][1]
        r['hand_height_px']=r['ground']-r['hand'][1]
        r['mid_thigh_y']=(y+r['knee'][1])/2
        r['hip_above_support']=r['support'][0] <= x <= r['support'][1]
        d.line([r['hip'],r['withers']], fill='red',width=3)
        d.line([(x,wy),(x,r['ground'])],fill='blue',width=2)
        d.line([(r['support'][0],r['ground']),(r['support'][1],r['ground'])],fill='green',width=4)
        d.line([(r['hand'][0]-35,r['knee'][1]),(r['knee'][0]+35,r['knee'][1])],fill='orange',width=2)
        for key in ('hip','withers','hand','knee'):
            a,b=r[key];d.ellipse((a-4,b-4,a+4,b+4),fill='red')
        d.text((x+12,y),f"{r['angle_deg']} deg",fill='black')
    im.save(p/f'chelovek-los-r7-{name}-zamery.png')
(p/'r7-measurements.json').write_text(json.dumps(sets,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(sets,ensure_ascii=False,indent=2))
