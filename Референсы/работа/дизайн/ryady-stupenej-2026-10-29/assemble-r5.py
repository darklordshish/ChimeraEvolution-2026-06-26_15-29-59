from pathlib import Path
from PIL import Image, ImageDraw
import json, math, shutil
import numpy as np

p = Path(__file__).resolve().parent
gen = Path('C:/Users/semion/.codex/generated_images/01a189cf-53d0-7dd0-b41b-0280d0bf7433')
shutil.copyfile(gen/'exec-150d9cc3-877d-47aa-8f4b-3dd8e048037f.png',p/'chelovek-los-r5-proba1.png')
shutil.copyfile(gen/'exec-3085f115-3c1f-4913-a785-c8e2e8fb38c1.png',p/'chelovek-los-r5-korrekciya.png')
base=Image.open(p/'chelovek-los-r2.png').convert('RGB')
r4=Image.open(p/'chelovek-los-r4.png').convert('RGB')
src=Image.open(p/'chelovek-los-r5-korrekciya.png').convert('RGB')
ImageDraw.Draw(src).rectangle((1035,0,1160,50),fill='white')
out=base.copy()
out.paste(r4.crop((416,38,1130,391)),(416,38))
d=ImageDraw.Draw(out)
d.rectangle((416,392,1129,1023),fill='white')
# Assembly only: uniform isotropic resize of each complete generated figure.
cells=[((300,48,565,580),614,699,.55),((758,20,1180,580),956,699,.52),
       ((330,585,565,1008),614,1004,.69),((805,575,1175,1008),956,1004,.69)]
transforms=[]
for box,cx,ground,s in cells:
    crop=src.crop(box)
    if box[1] == 575:
        ImageDraw.Draw(crop).rectangle((205,0,crop.width,15),fill='white')
    size=(round(crop.width*s),round(crop.height*s))
    crop=crop.resize(size,Image.Resampling.LANCZOS)
    xy=(round(cx-size[0]/2),ground-size[1])
    out.paste(crop,xy)
    transforms.append(dict(box=box,scale=s,xy=xy))
d=ImageDraw.Draw(out)
for y in (699,1004): d.line((416,y,1129,y),fill=(160,160,167))
d.line((770,392,770,1004),fill=(190,190,198))
out.save(p/'chelovek-los-r5.png')
# Visible raster landmarks, not skeleton coordinates.
landmarks=[dict(stage=1,pelvis=(516,316),withers=(461,114),hand=(466,402),knee=(513,419),ground=579),
           dict(stage=2,pelvis=(1098,318),withers=(1006,81),hand=(1015,442),knee=(1087,450),ground=580)]
for v in landmarks:
    dx=v['pelvis'][0]-v['withers'][0];dy=v['pelvis'][1]-v['withers'][1]
    v['angle_deg']=round(math.degrees(math.atan2(abs(dx),abs(dy))),2)
    v['hand_height_px']=v['ground']-v['hand'][1]
probe=[dict(stage=1,pelvis=(521,491),withers=(439,191)),dict(stage=2,pelvis=(1171,468),withers=(1038,120))]
for v in probe:
    v['angle_deg']=round(math.degrees(math.atan2(abs(v['pelvis'][0]-v['withers'][0]),abs(v['pelvis'][1]-v['withers'][1]))),2)
report=dict(probe=probe,correction=landmarks,assembly=transforms,
    endpoint_changed_pixels=[int(np.any(np.array(out)[:,a:b]!=np.array(base)[:,a:b],axis=2).sum()) for a,b in [(0,416),(1130,1536)]],
    r4_front_changed_pixels=int(np.any(np.array(out)[38:391,416:1130]!=np.array(r4)[38:391,416:1130],axis=2).sum()))
(p/'r5-measurements.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
qa=src.copy();qd=ImageDraw.Draw(qa)
for v in landmarks:
    qd.line([v['pelvis'],v['withers']],fill='red',width=3)
    qd.line([v['pelvis'],(v['pelvis'][0],v['withers'][1])],fill='blue',width=2)
    for key in ['pelvis','withers','hand','knee']:
        x,y=v[key];qd.ellipse((x-4,y-4,x+4,y+4),fill='red')
    qd.text((v['pelvis'][0]+10,v['pelvis'][1]),str(v['angle_deg'])+' deg',fill='black')
qa.save(p/'chelovek-los-r5-zamery.png')
prompts=json.loads((p/'r5-prompts.json').read_text(encoding='utf-8'))
prompt_file=p/'PROMPTS.md'
prompt_file.write_text(prompt_file.read_text(encoding='utf-8').split('\n\n# Лось r5 ·')[0],encoding='utf-8')
with prompt_file.open('a',encoding='utf-8') as f:
    f.write('\n\n# Лось r5 · 30.10.2026 · правка позы\n\nВстроенный image_gen. Одна проба профилей и одна корректировка с разворотом. Только утвердительный английский образ.\n')
    f.write('\n## Проба профилей\n\nВход 1 — витринное `../ChimeraEvolution/Референсы/витрина/человек-лось/тело.png` (главная опора позы); вход 2 — `chelovek-los-r4.png` (образ анфаса и мышц). Выход — `chelovek-los-r5-proba1.png`.\n\n```text\n'+prompts['probe']+'\n```\n')
    f.write('\n## Единственная корректировка\n\nВход 1 — проба r5; вход 2 — то же витринное тело; вход 3 — r4. Выход — `chelovek-los-r5-korrekciya.png`.\n\n```text\n'+prompts['correction']+'\n```\n')
print(json.dumps(report,ensure_ascii=False))
