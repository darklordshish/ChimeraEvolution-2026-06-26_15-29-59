# -*- coding: utf-8 -*-
"""РАСКЛАДКА НИЖНИХ НОГ ЛОСЯ — пясть, плюсна, путо и копыта ригблоками на концах узлов.

Контракт крепления — тот же, что у волка (`../wolf/organs_layout.py`, письмо механик 17.09c §4): часть называет узел,
кадр — «тело узла» (+Z вдоль узла, +Y перёд узла, +X вбок), X/Y в диаметрах конца узла, Z в его длинах. Помощники
кадра берутся оттуда же, а не копируются.

ОТКУДА ЧИСЛА — `ref/photo/moose_side_REFERENCE.jpg`, опоры `graph.py`:
  передняя (ближняя, отвесная): ширина 0.17 на Y 0.86, 0.136 на 0.73, 0.119 на 0.61, 0.111 на 0.48; путовый сустав Y 0.24;
  задняя (отставленная, плюсна отвесна): 0.21 под скакательным (с пяточным сухожилием), 0.16 на Y 0.73, 0.136 на 0.48.
  Копыто: переднее ~0.23 длины и ~0.13 высоты при холке 2.886 (натура 15–17 см × 1.46), путо наклонено вперёд.

ЧЕМ ЛОСЬ ОТЛИЧАЕТСЯ ОТ ВОЛКА. Пясть лося длинная и ПОЧТИ РОВНАЯ по толщине от запястья до путового сустава, поэтому
передняя нога ниже поля — ОДИН брусок от середины предплечья до путового (шишка бруска на −0.3 его длины встаёт ровно на
запястье), а не два. Задняя — как у волка: низ голени к скакательному и плюсна, угол между ними и есть скакательный сустав.

Запуск:  python organs_layout.py [--out путь]
"""
import argparse
import importlib.util
import json
import math
import os

import graph as G

HERE = os.path.dirname(os.path.abspath(__file__))


def _wolf_legs():
    spec = importlib.util.spec_from_file_location('wolf_legs', os.path.join(HERE, '..', 'wolf', 'organs_layout.py'))
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


_W = _wolf_legs()
sub, add, mul, unit, part, segment = _W.sub, _W.add, _W.mul, _W.unit, _W.part, _W.segment

FETLOCK_Y = 0.25          # путовый сустав над землёй
PASTERN_DEG = 35.0        # путо от отвеса вперёд


def node_by_name(name):
    for n in G.build_nodes():
        if n['name'] == name:
            return n
    raise KeyError(name)


def fetlock_under(p, deg=0.0):
    """Путовый сустав под точкой p (сегмент наклонён вперёд от отвеса на deg)."""
    return (p[0], FETLOCK_Y, p[2] + (p[1] - FETLOCK_Y) * math.tan(math.radians(deg)))


def hoof(n, fetlock, size):
    """Путо брусок от путового вперёд-вниз к венчику + копыто подошвой на земле. Венчик — на −0.3 длины копыта, где
    у блока самый высокий верх: туда и входит путо."""
    w, h, l = size
    coronet = (fetlock[0], h * 0.85, fetlock[2] + (fetlock[1] - h * 0.85) * math.tan(math.radians(PASTERN_DEG)))
    pastern = segment(n, fetlock, coronet, w * 0.62, w * 0.66, sub(coronet, fetlock), 0.04)
    centre = (fetlock[0], h * 0.5, coronet[2] + 0.30 * l)
    foot = part(n, 'копыто', centre, (w, h, l), (0.0, 0.0, 1.0))
    return pastern, foot


def front_leg():
    """ПЕРЕДНЯЯ: поле кончается на Y 0.80 (`graph.FORE_END`). Ниже — пясть одним бруском до путового, путо, копыто."""
    n = node_by_name('предплечье')
    d = sub(n['b'], n['a'])
    fet = fetlock_under(G.WRIST, -4.0)            # на снимке пясть чуть отклонена назад от запястья
    # шов перекрыт глубоко: конец поля скругляется, и при заходе 0.08 торец бруска торчал «манжетой» (итерация 5, три четверти)
    cannon = segment(n, n['b'], fet, 0.150, 0.160, d, 0.15)
    pastern, foot = hoof(n, fet, (0.170, 0.130, 0.235))
    return dict(slot='Руки', organ='Копыто', parts=[cannon, pastern, foot])


def hind_leg():
    """ЗАДНЯЯ: поле кончается над скакательным (`graph.HIND_END`). Ниже — низ голени до скакательного, плюсна отвесно
    до путового, путо, копыто."""
    n = node_by_name('голень')
    d = sub(n['b'], n['a'])
    # ...и сзади: угол бруска выходил из-под ляжки
    low = segment(n, n['b'], add(G.HOCK, mul(unit(d), 0.08)), 0.170, 0.200, d, 0.17)
    fet = fetlock_under(G.HOCK, 0.0)
    # верхний торец плюсны не выше сустава: при заходе 0.07 он торчал из-за низа голени плоской полкой (три четверти)
    shank = segment(n, G.HOCK, fet, 0.150, 0.170, sub(fet, G.HOCK), 0.02)
    pastern, foot = hoof(n, fet, (0.160, 0.125, 0.225))
    return dict(slot='Ноги', organ='Лосиные ноги', parts=[low, shank, pastern, foot])


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--out', default=os.path.join(HERE, 'out', 'los-organs-layout-draft.json'))
    args = ap.parse_args()

    import antlers
    # РОГА — тоже «куски органа», тем же форматом: слот, орган, части (на месте, без узла)
    horns = dict(slot='Рога', organ='Рога', parts=antlers.antler())
    legs = [front_leg(), hind_leg(), horns]
    os.makedirs(os.path.dirname(args.out), exist_ok=True)
    with open(args.out, 'w', encoding='utf-8') as f:
        json.dump(dict(organs=legs), f, ensure_ascii=False, indent=2)
    for lg in legs:
        print('%s (%s):' % (lg['organ'], lg['slot']))
        for p in lg['parts']:
            print('  %-7s на «%s»: offset %-24s scale %-22s euler %s   (центр %s м, габарит %s м)' %
                  (p['block'], p.get('node', 'месте'), p['offset'], p['scale'], p['euler'],
                   p['metres'].get('centre', p['metres'].get('start')), p['metres']['size']))  # у рогов — начало куска


if __name__ == '__main__':
    main()
