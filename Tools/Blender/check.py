# -*- coding: utf-8 -*-
"""ДЕТЕКТОР СЛОЯ КОСТЕЙ: считает скелет БЕЗ Blender и меряет его против натуры.

Зачем отдельно от сборки. «Двадцать численных проверок ok при демоне на экране» вышло оттого, что
мерилось лишь то, что догадались померить. Здесь проверок немного, но каждая — из своей могилы:
длины против остеометрии, высоты суставов против фотографии, замкнутость графа, дубли имён.

Запуск:  python check.py [вид]
"""
import sys, io, os, importlib

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

from chimera.skel import from_points

SPECIES = sys.argv[1] if len(sys.argv) > 1 else 'wolf'
sp = importlib.import_module('species.' + SPECIES)
W = sp.W

# ── ОСТЕОМЕТРИЯ Canis lupus В ДОЛЯХ ХОЛКИ. Не «примерно»: по этим числам ловится СИСТЕМНАЯ ошибка.
# В прошлый заход проксимальные кости были коротки на треть, а лопатка на треть длинна — глазом это
# читается как «ноги-ходули» и не диагностируется вовсе, а в долях холки видно сразу
NORM = {'лопатка': (.26, .03), 'плечо': (.26, .03), 'предплечье': (.26, .03), 'пясть': (.15, .03),
        'бедро': (.28, .03), 'голень': (.29, .03), 'плюсна': (.20, .03),
        'подвздошная': (.22, .04)}
# СОСТАВНЫЕ ОТДЕЛЫ мерятся СУММОЙ: разбиение на звенья — дело рига и гладкости, а норма
# относится к отделу целиком. Иначе каждое дробление кости ломало бы проверку
GROUPS = {'шейный отдел': (['шея', 'шея_2', 'шея_3', 'шея_в'], .41, .05),
          'грудной отдел': (['грудной', 'холка'], .40, .05)}
# ВЫСОТЫ СУСТАВОВ НАД ЗЕМЛЁЙ, снятые с фотографии живого зверя (доли холки)
JOINTS = {'плечевой': (.735, .04), 'локоть': (.523, .03), 'запястье': (.282, .03),
          'тазобедр': (.740, .04), 'колено': (.510, .03), 'скакат': (.283, .03)}


def main():
    bones, out, warn = from_points(sp.DEFS, sp.P)
    by = {b.name: b for b in bones}
    bad = list(warn)

    print('ВИД: %s · холка %.3f м · записей костей %d (из них зеркальных пар %d)'
          % (SPECIES, W, len(bones), sum(1 for b in bones if b.mirrorX)))

    print('\n── ДЛИНЫ ПРОТИВ ОСТЕОМЕТРИИ (доли холки) ──')
    for n, (want, tol) in NORM.items():
        if n not in by:
            bad.append('  нет кости %r' % n); continue
        got = by[n].length / W
        ok = abs(got - want) <= tol
        print('  %-13s %.3f  норма %.2f ±%.2f  %s' % (n, got, want, tol, 'ok' if ok else '← ВРЁТ'))
        if not ok:
            bad.append('  %s: %.3f против нормы %.2f' % (n, got, want))

    for gn, (names, want, tol) in GROUPS.items():
        miss = [n for n in names if n not in by]
        if miss:
            bad.append('  %s: нет костей %s' % (gn, miss)); continue
        got = sum(by[n].length for n in names) / W
        ok = abs(got - want) <= tol
        print('  %-14s %.3f  норма %.2f ±%.2f  %s  (%d звеньев)'
              % (gn, got, want, tol, 'ok' if ok else '← ВРЁТ', len(names)))
        if not ok:
            bad.append('  %s: %.3f против нормы %.2f' % (gn, got, want))

    print('\n── ВЫСОТЫ СУСТАВОВ ПРОТИВ ФОТО (доли холки) ──')
    for pt, (want, tol) in JOINTS.items():
        got = sp.P[pt][1] / W
        ok = abs(got - want) <= tol
        print('  %-13s %.3f  с фото %.3f ±%.2f  %s' % (pt, got, want, tol, 'ok' if ok else '← ВРЁТ'))
        if not ok:
            bad.append('  сустав %s на %.3f, с фото %.3f' % (pt, got, want))

    print('\n── ГАБАРИТ И ПОСАДКА ──')
    lo = min(min(out[b.name][0][1], out[b.name][2][1]) - max(b.r0, b.r1) for b in bones)
    hi = max(max(out[b.name][0][1], out[b.name][2][1]) + max(b.r0, b.r1) for b in bones)
    zs = [out[b.name][i][2] for b in bones for i in (0, 2)]
    xs = [abs(out[b.name][i][0]) + b.r0 * b.section for b in bones for i in (0, 2)]
    print('  низ кости %.3f (лапа должна касаться земли: 0.000±0.02)' % lo)
    print('  верх %.3f · холка задана %.3f' % (hi, W))
    print('  длина по Z %.3f (%.2f холки)· полуширина %.3f' % (max(zs) - min(zs), (max(zs) - min(zs)) / W, max(xs)))
    if abs(lo) > 0.02:
        bad.append('  ЗВЕРЬ НЕ НА ЗЕМЛЕ: низ кости %.3f' % lo)

    print('\n── СЛОТЫ (единица химеризации = единица скелета) ──')
    per = {}
    for b in bones:
        per.setdefault(b.socket or '—', []).append(b.name)
    for s in sorted(per):
        print('  %-9s %2d: %s' % (s, len(per[s]), ', '.join(per[s])[:96]))

    print('\n%s' % ('ЧИСТО' if not bad else 'РАСХОЖДЕНИЙ: %d\n%s' % (len(bad), '\n'.join(bad))))
    return 0 if not bad else 1


if __name__ == '__main__':
    sys.exit(main())
