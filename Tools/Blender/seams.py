# -*- coding: utf-8 -*-
"""ДЕТЕКТОР ШВОВ СОБРАННОГО ТЕЛА: меряет РАССТОЯНИЕ МЕЖДУ ЧАСТЯМИ, а не числа в таблице.

ЗАЧЕМ ОН ПОЯВИЛСЯ. Обмерщик докладывал «ноль пустых секторов, стыки чисты», и это принималось за
готовность. Слепая оценка рендера дала 2/10 с диагнозом «части висят в воздухе». Оба утверждения
были верны: «пустой сектор» говорит о ТАБЛИЦЕ, а не о том, соприкасаются ли построенные оболочки.
Между ними никакой связи нет — таблица может быть полна, а части разъехаться.

Правило проекта «стык считается по БЛИЖАЙШЕЙ ПАРЕ ДЕТАЛЕЙ» (карта тел) здесь применено к клетке:
берутся вершины двух соседних слотов и ищется минимальное расстояние. Меряется В ДОЛЯХ КАЛИБРА,
потому что 10 мм у волка и у ежа — разные вещи.

Запуск:  python seams.py wolf
"""
import sys, io, os, math, importlib

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

from chimera import cage as cg

SPECIES = sys.argv[1] if len(sys.argv) > 1 else 'wolf'
sp = importlib.import_module('species.' + SPECIES)
CAGES = importlib.import_module('species.%s_cage' % SPECIES).CAGES
W = sp.W


def rings(slot):
    c = CAGES[slot]
    out = []
    for (u, rad), ctr, (up, side) in zip(c['stations'], c['centers'], cg.frames(c['dirs'])):
        for j, r in enumerate(rad):
            a = 2.0 * math.pi * j / c['n']
            d = [up[k] * math.cos(a) + side[k] * math.sin(a) for k in range(3)]
            out.append(tuple(ctr[k] + d[k] * r for k in range(3)))
    if slot in cg.PAIRED:
        out += [(-x, y, z) for x, y, z in out]
    return out

V = {s: rings(s) for s in CAGES}

# РОДСТВО СЛОТОВ — ИЗ ГРАФА КОСТЕЙ, а не из списка: чей родитель у первой кости оси, тот и сосед
DEF = {d['name']: d for d in sp.DEFS}
SOCK = {d['name']: cg.SURFACE_BONE.get(d['name'], cg.SURFACE_OF.get(d.get('socket'), d.get('socket')))
        for d in sp.DEFS}
PAIRS = []
for slot, axis in sp.AXES.items():
    p = DEF.get(axis[0], {}).get('parent')
    while p and SOCK.get(p) == slot:
        p = DEF.get(p, {}).get('parent')
    par = SOCK.get(p)
    if par and par in CAGES and par != slot:
        PAIRS.append((slot, par))


def gap(a, b):
    best, pa, pb = 1e9, None, None
    for p in V[a]:
        for q in V[b]:
            d = (p[0] - q[0]) ** 2 + (p[1] - q[1]) ** 2 + (p[2] - q[2]) ** 2
            if d < best:
                best, pa, pb = d, p, q
    return math.sqrt(best), pa, pb


print('ШВЫ СОБРАННОГО ТЕЛА — вид %s, калибр %.3f м' % (SPECIES, W))
print()
print('  стык                     зазор, мм   в долях калибра   вердикт')
bad = 0
for a, b in PAIRS:
    g, pa, pb = gap(a, b)
    frac = g / W
    # ПОРОГ ИЗ СМЫСЛА, А НЕ ИЗ ВКУСА: части входят друг в друга, значит на стыке ждём ПЕРЕКРЫТИЕ.
    # Ноль — уже плохо (касание в одной точке), а всё, что больше сотой доли калибра, глаз читает
    # как отдельный предмет. У волка сотая — 12 мм, и голова висела именно на таком порядке
    verdict = 'ok' if frac < 0.004 else ('ЩЕЛЬ' if frac < 0.02 else 'ОТОРВАНО')
    if verdict != 'ok':
        bad += 1
    print('  %-10s ← %-10s %8.1f       %.4f        %s' % (a, b, g * 1000, frac, verdict))
print()
print('РАЗОРВАННЫХ СТЫКОВ: %d из %d' % (bad, len(PAIRS)))
