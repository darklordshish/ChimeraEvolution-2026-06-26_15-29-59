from pathlib import Path
from PIL import Image, ImageDraw
import math, json

p = Path(__file__).resolve().parent
sets = {
    'proba1': [
        dict(stage=1, hip=(510,490), withers=(420,171), hand=(430,659), knee=(478,638), support=(396,548), ground=930),
        dict(stage=2, hip=(1204,483), withers=(1071,129), hand=(1095,699), knee=(1204,657), support=(1068,1270), ground=930)],
    'korrekciya': [
        dict(stage=1, hip=(467,302), withers=(420,116), hand=(427,398), knee=(452,382), support=(403,490), ground=535),
        dict(stage=2, hip=(1222,300), withers=(1100,92), hand=(1160,423), knee=(1208,389), support=(1146,1266), ground=535)]}
for name, rows in sets.items():
    im = Image.open(p / f'chelovek-los-r6-{name}.png').convert('RGB')
    assert im.size == (1536,1024)
    d = ImageDraw.Draw(im)
    for r in rows:
        x,y = r['hip']; wx,wy = r['withers']
        r['angle_deg'] = round(math.degrees(math.atan2(abs(x-wx), abs(y-wy))),2)
        r['hip_above_support'] = r['support'][0] <= x <= r['support'][1]
        r['fingertips_below_knee_px'] = r['hand'][1] - r['knee'][1]
        r['hand_height_above_ground_px'] = r['ground'] - r['hand'][1]
        d.line([r['hip'],r['withers']], fill='red',width=3)
        d.line([(x,wy),(x,r['ground'])],fill='blue',width=2)
        d.line([(r['support'][0],r['ground']),(r['support'][1],r['ground'])],fill='green',width=4)
        d.line([(r['hand'][0]-25,r['knee'][1]),(r['knee'][0]+25,r['knee'][1])],fill='orange',width=2)
        for key in ('hip','withers','hand','knee'):
            a,b=r[key]; d.ellipse((a-4,b-4,a+4,b+4),fill='red')
        d.text((x+12,y),f"{r['angle_deg']} deg",fill='black')
    im.save(p / f'chelovek-los-r6-{name}-zamery.png')
(p/'r6-measurements.json').write_text(json.dumps(sets,ensure_ascii=False,indent=2),encoding='utf-8')
prompts=json.loads((p/'r6-prompts.json').read_text(encoding='utf-8'))
with (p/'PROMPTS.md').open('a',encoding='utf-8') as f:
    f.write('\n\n# Лось r6 · 30.10.2026 · анатомия и равновесие\n\nВстроенный image_gen. Одна проба двух профилей и одна корректировка с дополнительным 3/4. Полный лист не собран: критерий кистей не пройден.\n')
    f.write('\n## Проба\n\nВход 1 — `../ChimeraEvolution/Референсы/витрина/человек-лось/тело.png` (поза); вход 2 — `chelovek-los-r5.png` (анфас, мышцы). Образ r4 взят из текста выше. Выход — `chelovek-los-r6-proba1.png`.\n\n```text\n'+prompts['probe']+'\n```\n')
    f.write('\n## Единственная корректировка\n\nВход 1 — `chelovek-los-r6-proba1.png`; вход 2 — то же витринное тело. Выход — `chelovek-los-r6-korrekciya.png`. Уточнены диагональная колонна ст.2 и разные уровни кистей.\n\n```text\n'+prompts['correction']+'\n```\n')
print(json.dumps(sets,ensure_ascii=False,indent=2))
