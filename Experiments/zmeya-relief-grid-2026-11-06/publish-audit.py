from pathlib import Path
import json,shutil
from PIL import Image
P=Path(__file__).parent
O=P.parents[1]/'Референсы/работа/дизайн/zmeya-relief-grid-2026-11-06'
r=json.loads((P/'audit.json').read_text(encoding='utf8'))
for src,dest in [('renders/optimized-detail.png','optimized-detail.png'),('renders/optimized-rear34.png','optimized-rear34.png'),('audit.json','audit.json')]:shutil.copyfile(P/src,O/dest)
peak=max(r['contacts'],key=lambda row:row['max_depth_mm'])
note=f'''\n\n## Продолжение после одобрения направления\n\nГеймдизайнер: «да, направление верное». Одобрен художественный принцип, не приёмка деформации.\n\nПроверены {r['frames']} поз. Совпадающие во всех выборках вершины объединены: {r['vertices_before']} → {r['vertices_after']}; треугольников {r['triangles']}. Максимальная ошибка позиции после объединения {r['max_sampled_position_error_mm']:.6f} мм. Скаты, цвет и высота сохранены; вершины, слияние которых схлопнуло бы грань, оставлены раздельными. Это сокращение вершин, не полноценное уменьшение числа треугольников.\n\nНовая локальная проверка ближайшей ориентированной поверхности обнаружила погружение до {peak['max_depth_mm']:.3f} мм, кадр {peak['frame']}; в покое — {r['contacts'][0]['max_depth_mm']:.3f} мм. Порог диагностики 0.5 мм, точки с высотой менее 0.5 мм пропущены. Это приближённый поиск проникновения, не полный тест самопересечений и не доказательство отсутствия складок. Движение пока не принято.\n\n`optimized-detail.png`, `optimized-rear34.png` — новая сетка; `audit.json` — отчёт всех поз. Исходники: `audit.py`, `optimized-preview.blend`, `optimized-anchors.json` в папке эксперимента. В сцене по-прежнему нет автоматического пересчёта после изменения позы. Дальше — локализация и коррекция деформации возле плеч/складок, затем уменьшение треугольников.\n'''
with (O/'README.md').open('a',encoding='utf8') as f:f.write(note)
letter=P.parents[1]/'Docs/models/FEEDBACK-2026-11-06g-codex-relef-proverka.md'
letter.write_text('# ГеймБоссу — проверка одобренного рельефа\n'+note,encoding='utf8')
assert Image.open(O/'optimized-detail.png').size==(800,720)
assert r['frames']==49 and r['max_sampled_position_error_mm']<=.001
print(json.dumps({k:v for k,v in r.items() if k!='contacts'},indent=2));print('PEAK',peak)
