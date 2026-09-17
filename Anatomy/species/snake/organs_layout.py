# -*- coding: utf-8 -*-
"""РАСКЛАДКА КУСКОВ ОРГАНОВ ЗМЕИ — погремушка и привитой хвост ригблоками.

ПОГРЕМУШКА (орган «Погремушка», место `Наконечник`, калибр куб 0.13). Было 14 цилиндров-примитивов «шейка + ободок»;
стало 7 блоков `чашка`, каждый — пара шейка–ободок одним куском. Центры, ширины ободков и цвет роговых колец — из
прежних данных органа (там же довод «еловой шишки»: веретено 0.80 → 1.00 → 0.68). Погремушка сплюснута с боков —
у гремучника она выше, чем шире (`rattlesnake_coiled_REFERENCE.jpg`), поэтому по X 0.8 от высоты.

ХВОСТ (орган «Хвост», форма ГРАФТА на чужом шасси, `visualSegments` 3). Было «капсула + шар» на звено, стало одно
`звено` на звено: блок вдоль тела, торцы заходят в соседей на 10 %. У самой змеи хвост строит цепь места, орган
его форму не рисует — эта раскладка только для химер.

Запуск:  python organs_layout.py [--out путь]
"""
import argparse
import json
import os

HERE = os.path.dirname(os.path.abspath(__file__))

HORN = [0.87, 0.82, 0.62, 1.0]
# прежние пары (шейка z, ободок z, ширина ободка) в калибрах места
RATTLE = [(1.297, 1.099, 0.800), (0.901, 0.703, 0.950), (0.505, 0.307, 1.000), (0.109, -0.089, 0.980),
          (-0.287, -0.485, 0.920), (-0.683, -0.881, 0.820), (-1.079, -1.277, 0.680)]


def rattle():
    parts = []
    for neck_z, rim_z, w in RATTLE:
        parts.append(dict(block='чашка', offset=[0.0, 0.0, round((neck_z + rim_z) / 2, 3)],
                          scale=[round(w * 0.8, 3), round(w, 3), 0.44], euler=[0, 0, 0], color=HORN))
    return parts


def tail():
    return [dict(block='звено', offset=[0, 0, 0], scale=[0.95, 0.95, 1.10], euler=[0, 0, 0])]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--out', default=os.path.join(HERE, 'out', 'zmeya-organs-layout-draft.json'))
    args = ap.parse_args()
    organs = [dict(slot='Наконечник', organ='Погремушка', parts=rattle()),
              dict(slot='Хвост', organ='Хвост', parts=tail())]
    os.makedirs(os.path.dirname(args.out), exist_ok=True)
    with open(args.out, 'w', encoding='utf-8') as f:
        json.dump(dict(organs=organs), f, ensure_ascii=False, indent=2)
    for o in organs:
        print('%s (%s): кусков %d' % (o['organ'], o['slot'], len(o['parts'])))


if __name__ == '__main__':
    main()
