# -*- coding: utf-8 -*-
"""ОБМЕРЩИК: снять клетки вида с болвана и записать таблицей.

Запуск:  python cagerun.py wolf
Пишет `species/<вид>_cage.py` — РУКАМИ НЕ ПРАВИТЬ, правка потеряется при следующем прогоне.
Правится болван (кости и мышцы), а клетка снимается с него.
"""
import sys, io, os, importlib, time

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

from chimera.skel import from_points
from chimera import cage as cg, volume

SPECIES = sys.argv[1] if len(sys.argv) > 1 else 'wolf'
sp = importlib.import_module('species.' + SPECIES)
bones, placed, warn = from_points(sp.DEFS, sp.P)
by = {b.name: b for b in bones}

vol = volume.Volume(bones, placed, cg.SURFACE_OF, cg.PAIRED, surface_bone=cg.SURFACE_BONE)
print('ВИД: %s · холка %.3f м · сетка %s' % (SPECIES, sp.W, vol.dim))
print()

# РОДИТЕЛЬ СЛОТА — ИЗ ГРАФА КОСТЕЙ, а не из отдельного списка: чей родитель у первой кости цепи,
# тот и родитель места. Отдельный список тут был бы вторым источником правды и однажды разошёлся бы
# с телом — правило проекта «список имён в коде есть признак неверной иерархии»
DEF = {d['name']: d for d in sp.DEFS}
PARENT = {}
for _slot, _axis in sp.AXES.items():
    _p = DEF.get(_axis[0], {}).get('parent')
    _own = by[_p].socket if _p in by else None
    _own = cg.SURFACE_OF.get(_own, _own)
    if _own and _own != _slot and _own in sp.AXES:
        PARENT[_slot] = _own

out, t0 = {}, time.time()
for slot, axis in sp.AXES.items():
    miss = [n for n in axis if n not in placed]
    if miss:
        print('  %-8s ПРОПУЩЕН: нет костей %s' % (slot, miss)); continue
    c = cg.measure(slot, axis, placed, vol)
    out[slot] = c
    rr = [r for _, radii in c['stations'] for r in radii]
    print('  %-8s %d×%d  ось %.3f м  радиус %.3f…%.3f  (пустых секторов %d)'
          % (slot, c['m'], c['n'], c['length'], min(rr), max(rr), sum(1 for r in rr if r < 1e-6)))

path = os.path.join(HERE, 'species', '%s_cage.py' % SPECIES)
HEAD = (
    '# -*- coding: utf-8 -*-\n'
    '"""КЛЕТКИ ВИДА — СНЯТЫ С ОТЛИВКИ БОЛВАНА, не выведены из общих соображений.\n\n'
    'Сгенерировано `Tools/Blender/cagerun.py` — РУКАМИ НЕ ПРАВИТЬ: правка потеряется при следующем\n'
    'прогоне. Правится болван (кости и мышцы в `%s.py`), клетка снимается с него.\n\n'
    'Станция: (доля вдоль оси, радиусы по секторам). Сектор 0 — ВВЕРХ, дальше по кругу к боку.\n'
    'Радиусы В МЕТРАХ этого вида; носитель нормирует их своим калибром. M и N фиксированы в\n'
    '`chimera/cage.py` и одинаковы у всех видов — иначе смешение невыразимо.\n\n'
    '`centers` — мировые середины станций ЭТОГО вида: по ним строится меш и сверяются стыки.\n'
    'Радиус отсчитывается ОТ ЦЕНТРА СТАНЦИИ, а не от кости: кость слота в середине своей формы\n'
    'не лежит — позвоночник идёт по крыше бочки, и луч вбок от него мерит головку ребра.\n\n'

    'РАДИУС 0 — НЕ ОШИБКА. На корневой станции слота плоть принадлежит родителю (у хвоста —\n'
    'крестцу, у ноги — тазу), а под мордой её нет вовсе, потому что челюсть отдельная и пасть\n'
    'открывается. Такие станции закрывает ОБЩЕЕ КОЛЬЦО соседей при сборке, а не обмер."""\n\n'
) % SPECIES

with io.open(path, 'w', encoding='utf-8') as f:
    f.write(HEAD)
    f.write('CAGES = {\n')
    for slot, c in out.items():
        f.write("    %r: {\n        'm': %d, 'n': %d, 'length': %.4f,\n" % (slot, c['m'], c['n'], c['length']))
        f.write("        'centers': [%s],\n" % ', '.join('(%.4f, %.4f, %.4f)' % t for t in c['centers']))
        f.write("        'dirs': [%s],\n" % ', '.join('(%.4f, %.4f, %.4f)' % t for t in c['dirs']))
        f.write("        'caps': (%.4f, %.4f),\n" % c['caps'])
        f.write("        'sink': %.4f,\n" % c['sink'])
        f.write("        'stations': [\n")
        for u, radii in c['stations']:
            f.write('            (%.4f, (%s)),\n' % (u, ', '.join('%.4f' % r for r in radii)))
        f.write('        ]},\n')
    f.write('}\n')
print('\nзаписано: %s  (%.1f с)' % (os.path.relpath(path, HERE), time.time() - t0))
