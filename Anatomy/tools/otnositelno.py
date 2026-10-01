# -*- coding: utf-8 -*-
"""БУГРЫ И СЕЧЕНИЯ ЛОФТА — В ДОЛЯХ РОДИТЕЛЯ (спека двух слоёв §4.1, письмо механик 29f §1 п. 2).

Зачем: аугмент вынимает цепь носителя от метки и вставляет свою в калибре носителя (решение 13). Бугры и сечения едут
со своей цепью, и в метрах они при растяжке сегмента остались бы на старом месте — оторвались бы или разъехались
щелями. Поэтому их положение и толщина записываются долями родителя.

ПРЕДЛАГАЕМАЯ ЗАПИСЬ у узла без метки, чья цепь совпадает с цепью родителя (бугор, сечение, масса туши):
    "rel": {"at": 0.42, "off": [0.10, 0.85], "len": 0.61, "r": [0.72, 0.88]}
  at   — где начало: доля вдоль оси родителя от его начала (у сустава на конце — 1.0, как `attach`);
  off  — смещение начала поперёк оси: X — в долях полуширины родителя (r·section), Z — в долях полуглубины (r·depth),
         r — радиус родителя в точке `at` (линейно r0 → r1, за концами — по краю);
  len  — длина в длинах родителя;
  r    — r0, r1 в долях того же радиуса родителя в точке `at`.
  `dir`, `section`, `depth`, `blend` не меняются: они уже относительные.
Кадр кости — как у `SkeletonBuilder`: +Y вдоль кости, X — ширина, Z — глубина.

Запуск:  python otnositelno.py [файл-графа …]   — пересчёт, обратная сборка в метры и расхождение (должно быть 0).
С поставки 31 генераторы пишут `rel` сами (`razmetka.rel_all`); скрипт годен для черновика с метрами.
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
H = os.path.normpath(os.path.join(HERE, '..', '..', 'Docs', 'models', 'handoff'))


def radius_at(p, at):
    t = min(1.0, max(0.0, at))
    return p['r0'] + (p['r1'] - p['r0']) * t


def is_relative(n, by):
    """Бугор, сечение или масса туши: без метки и в цепи родителя. Узел цепи с меткой и начало новой цепи — нет."""
    p = by.get(n['parent'])
    return p is not None and not n.get('mark') and n.get('limb') == p.get('limb')


def to_rel(n, p):
    L = p['length']
    if n['freeOrigin']:
        o = n['origin']
        at = o['y'] / L
        rp = radius_at(p, at)
        off = [o['x'] / (rp * p['section']), o['z'] / (rp * p['depth'])]
    else:
        at = n['attach']
        rp = radius_at(p, at)
        off = [0.0, 0.0]
    return dict(at=round(at, 4), off=[round(v, 4) for v in off], len=round(n['length'] / L, 4),
                r=[round(n['r0'] / rp, 4), round(n['r1'] / rp, 4)])


def from_rel(rel, p):
    """Обратно в метры — так, как будет читать `SpeciesHandoff`."""
    L = p['length']
    rp = radius_at(p, rel['at'])
    origin = dict(x=rel['off'][0] * rp * p['section'], y=rel['at'] * L, z=rel['off'][1] * rp * p['depth'])
    return origin, rel['len'] * L, rel['r'][0] * rp, rel['r'][1] * rp


def check(path):
    doc = json.load(open(path, encoding='utf-8'))
    by = {n['name']: n for n in doc['nodes']}
    worst, rows = 0.0, []
    for n in doc['nodes']:
        if not is_relative(n, by):
            continue
        p = by[n['parent']]
        rel = to_rel(n, p)
        origin, length, r0, r1 = from_rel(rel, p)
        want = n['origin'] if n['freeOrigin'] else dict(x=0.0, y=n['attach'] * p['length'], z=0.0)
        err = max(abs(origin[k] - want[k]) for k in 'xyz')
        err = max(err, abs(length - n['length']), abs(r0 - n['r0']), abs(r1 - n['r1']))
        worst = max(worst, err)
        rows.append((n['name'], p['name'], rel))
    return doc['species'], rows, worst


def main(paths):
    for path in paths or [os.path.join(H, f + '-graph.json') for f in ('volk', 'los', 'ezh', 'zmeya', 'chelovek')]:
        if any('rel' in n for n in json.load(open(path, encoding='utf-8'))['nodes']):
            print('%s: уже в долях (поставка 31) — сверка идёт в Unity, `BodyChains.ResolveRel`' % os.path.basename(path))
            continue
        sp, rows, worst = check(path)
        print('%s: в долях %d узлов, обратная сборка расходится на %.2g м' % (sp, len(rows), worst))
        for name, parent, rel in rows:
            print('   %-13s на %-11s at %-7s off %-18s len %-7s r %s' % (name, parent, rel['at'], rel['off'], rel['len'], rel['r']))


if __name__ == '__main__':
    main(sys.argv[1:])
