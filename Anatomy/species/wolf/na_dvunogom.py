"""ВОЛК НА ДВУНОГОМ — тело волка на плане человека (спека `2026-10-08-vid-na-chuzhom-plane.md`, срез 1): числа 17 групп
шаблона двуногого и блок `head`, снятые с последней колонки листа `Docs/design/stupeni-oborotnya/` (маски там же).

Запуск (заготовка — вне handoff/, иначе импорт примет её как тело волка):
  unity command run_script --file Tools/Agent/PlanBodies.cs --entry PlanBodies.Export --args '["Человек","Волк","<заготовка>"]'
  python Anatomy/species/wolf/na_dvunogom.py <заготовка> Docs/models/handoff/volk-na-dvunogom.json
Сверка — Stand.Stages с g2 = 1 рядом с маской `volk-*.png`; IoU анфаса 08.10: шасси 0.440, ступень 2 — 0.459.

Числа человека × множители по группам.
Нормировка — по РОСТУ фигуры (скелет химеры человеческий, торс не удлиняется): у листа плечевой пояс ×1.36, плечо ×1.54,
предплечье ×1.75, бедро ×1.19, голень ×0.66, грудь анфас ×1.18, в профиль до ×1.65 (с гривой).
Веретена (r = полудлина) толщают через section/depth; капсулы торса, дельты, трапеция, грудная, ягодица — через r."""
import json, sys
src, dst = sys.argv[1], sys.argv[2]
d = json.load(open(src, encoding='utf-8'))
R = {'таз': 1.10, 'живот': 1.0, 'грудная клетка': 1.15, 'грудная': 0.80, 'ягодица': 1.10, 'трапеция': 1.50,
     'дельта.перед': 1.30, 'дельта': 1.30, 'дельта.зад': 1.30}
T = {'широчайшая': 1.8, 'бицепс': 2.2, 'трицепс': 2.4, 'разгибатели': 1.4, 'сгибатели': 1.4,
     'квадрицепс': 1.7, 'задняя бедра': 1.7, 'икра': 0.8}
DEPTH = {'грудная клетка': 1.25}
import os
B = float(os.environ.get('BOOST', '1.0'))          # общая сила масс; 1.6 давала IoU 0.476, но предплечья торчали вбок «ушами»
LONG = {'бицепс': 1.8, 'трицепс': 1.7, 'разгибатели': 1.6, 'сгибатели': 1.6, 'квадрицепс': 1.3, 'задняя бедра': 2.5}
for g in d['groups']:
    n = g['name']
    if n in R:
        k = 1 + (R[n] - 1) * B
        g['r0'] *= k; g['r1'] *= k
    if n in T:
        k = 1 + (T[n] - 1) * B
        L = LONG.get(n, 1.0)            # веретено длиннее: r — полудлина; толщина = r·section, поэтому делим на L
        g['r0'] *= L; g['r1'] *= L
        g['section'] *= k / L; g['depth'] *= k / L
    if n in DEPTH:
        g['depth'] *= DEPTH[n]
    for f in ('u', 'len', 'r0', 'r1', 'x', 'z', 'section', 'depth'):
        g[f] = round(g[f], 4)
h = d['head']
h.update(neckLen=round(h['neckLen'], 4), neckR0=round(h['neckR0'] * 1.6, 4), neckR1=round(h['neckR1'] * 1.45, 4),
         neckPitch=float(sys.argv[3]) if len(sys.argv) > 3 else 45.0,
         headLen=round(h['headLen'] * 1.5, 4), headR0=round(h['headR0'] * 1.15, 4), headR1=round(h['headR1'] * 1.15, 4),
         headPitch=float(sys.argv[4]) if len(sys.argv) > 4 else -45.0)
json.dump(d, open(dst, 'w', encoding='utf-8'), ensure_ascii=False, indent=2)
print('ok')
