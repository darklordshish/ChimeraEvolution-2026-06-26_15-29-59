# -*- coding: utf-8 -*-
"""СИЛУЭТНЫЙ ГРАФ ЗМЕИ — поставка 7 модельной линии. ТОЛЬКО ГОЛОВА, и это не недоделка.

ПОЧЕМУ ТОЛЬКО ГОЛОВА. Тело змеи — цепь звеньев-мест (`шея` ×4, `хребет` ×5, `Хвост` ×6), и каждый кадр её расставляет
по пути головы `SnakeBodyChain` в мировых координатах. Поле графа неподвижно относительно корня: погасить цепь и отдать
тело полю значит получить жёсткую палку, потерять плотные звенья (поверхность попаданий) и `BodyPoint`, по которому
змею рвут волки. Перевод цепи на узлы графа — решение механик (письмо `OTVET-2026-09-17-skeletonhides.md` §4.3,
поставка 7 §4). ГОЛОВА же стоит на корне и едет вместе с ним, поэтому её форма полем законна уже сейчас.

КАЛИБР — ИГРОВОЙ, пропорции — со снимков. Голова стоит там же, где стояли её части по местам (карта тел 17.09:
Z −0.19…0.39, ось на высоте цепи 0.30, ширина 0.24): голова не должна отрываться от шеи, которую ставит код.
Форма — `ref/photo/rattlesnake_head_front.jpg` (строго анфас, открыт 17.09): ширина по челюстным железам сзади 1.0,
у глаз 0.59, у рыла 0.55 — треугольник ямкоголовых; `rattlesnake_head_lateral.jpg` (профиль): плоское темя, высота
головы ~0.47 длины, тупое рыло. Калибр 0.30 × 0.19 × 0.56 (см. ниже). Рыло — деталь Пасти (блок), в графе его нет.

Запуск:  python graph.py [--out путь]
"""
import argparse
import importlib.util
import json
import os

HERE = os.path.dirname(os.path.abspath(__file__))


def _wolf_graph():
    spec = importlib.util.spec_from_file_location('wolf_graph', os.path.join(HERE, '..', 'wolf', 'graph.py'))
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


to_bones = _wolf_graph().to_bones

AXIS_Y = 0.30                 # высота оси цепи (`SnakeBodyChain.height`)
HEAD_BACK_Z = -0.17           # затылок: шея, которую ставит код, кончается на −0.20
HEAD_TIP_Z = 0.39             # кончик рыла
# КАЛИБР ГОЛОВЫ КРУПНЕЕ ПРЕЖНЕГО (0.24 × 0.163): на клетке 0.084 узел такой толщины худеет до пластины в 0.11 м
# (итерация 1), а у гремучника голова и так в ~1.5 раза шире шеи (шея у головы 0.215 → затылок 0.30)
HEAD_W, HEAD_H = 0.30, 0.19
FRONT = {'сзади': 1.00, 'у_глаз': 0.59, 'у_рыла': 0.55}


def node(name, socket, parent, a, b, r0, r1, section=1.0, depth=1.0, blend=0.0, mirror=False):
    return dict(name=name, socket=socket, parent=parent, a=a, b=b, r0=r0, r1=r1,
                section=section, depth=depth, blend=blend, mirror=mirror)


def build_nodes():
    # ЧЕРЕП: широкий затылок с железами (0.24) сходит к глазам (0.59 → 0.14). Приплюснут: высота ~0.68 ширины.
    # Кончается у глаз — дальше рыло блоком
    a = (0.0, AXIS_Y, HEAD_BACK_Z)
    b = (0.0, AXIS_Y - 0.005, 0.10)
    r0 = HEAD_W * FRONT['сзади'] / 2
    r1 = HEAD_W * FRONT['у_глаз'] / 2 + 0.005
    return [node('голова', 'голова', '', a, b, round(r0, 3), round(r1, 3), section=1.0, depth=HEAD_H / HEAD_W * 1.1, blend=0.06)]


HIDES = ['голова']


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--out', default=os.path.join(HERE, 'out', 'zmeya-graph-draft.json'))
    args = ap.parse_args()
    nodes = build_nodes()
    bones = to_bones(nodes)
    os.makedirs(os.path.dirname(args.out), exist_ok=True)
    with open(args.out, 'w', encoding='utf-8') as f:
        json.dump(dict(species='Змея', hides=HIDES, nodes=bones), f, ensure_ascii=False, indent=2)
    print('узлов', len(bones), '→', args.out)
    for n in nodes:
        print('  %-7s (%.2f %.2f %.2f) → (%.2f %.2f %.2f)  r %.3f→%.3f' % ((n['name'],) + tuple(n['a']) + tuple(n['b']) + (n['r0'], n['r1'])))


if __name__ == '__main__':
    main()
