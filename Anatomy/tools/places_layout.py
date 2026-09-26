# -*- coding: utf-8 -*-
"""РАСКЛАДКА ГНЁЗД ПЯТИ ВИДОВ — `Docs/models/handoff/<вид>-places-layout.json` (формат — письмо механик
`FEEDBACK-2026-09-26c-format-gnezd.md` §3, спека `2026-09-26-adresaciya-detaley-himery.md` П1–П5).

Гнездо — кадр места на теле шасси: начало `pos` (метры тела: Y от земли, Z к носу, X вправо), направление `dir`,
единица `unit` (метры), кость-хозяин `host` (узел графа вида; у цепи змеи — `звено:N`, N от головы). Парные места —
`mirror: true`, `pos` — правая сторона. Места, которых у вида нет, — `proposed: true`: гнездо стоит (тотальность П2),
детектор его считает, но не ругает.

ОТКУДА ЧИСЛА. Таблица `Docs/models/GNEZDA-2026-09-26-pyat-vidov.md`: детали замерены на собранных телах (стенд
`Kadr.Shot`), концы конечностей и корень хвоста — с узлов графа поставки. Места шасси (`хребет`, `шея`, `голова`,
`Сердце`, `Чутьё`) — начало и ось узла графа, у змеи — звено цепи. Здесь числа не пересчитываются: таблица — источник.

Запуск:  python places_layout.py        → пять файлов в Docs/models/handoff/
"""
import json
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '..', '..'))
OUT = os.path.join(ROOT, 'Docs', 'models', 'handoff')
sys.path.insert(0, os.path.join(ROOT, 'Tools', 'Blender', 'kritik'))
import faza  # noqa: E402  — кинематика графа (`SkeletonBuilder.Place`)

FILES = {'Волк': 'volk', 'Лось': 'los', 'Ёж': 'ezh', 'Змея': 'zmeya', 'Человек': 'chelovek'}
# СЛОВАРЬ МЕСТ — все, что есть хоть у одного вида (`SpeciesSO.sockets` на 26.09). У каждого вида — каждое
PLACES = ['хребет', 'шея', 'голова', 'Пасть', 'нос', 'глаза', 'уши', 'ямки', 'Рога', 'Чутьё', 'Сердце', 'горб',
          'Игломёт', 'Шкура', 'Руки', 'Ноги', 'Хвост', 'Наконечник']

UP, DOWN, FWD = (0, 1, 0), (0, -1, 0), (0, 0, 1)


def unit(v):
    n = math.sqrt(sum(x * x for x in v))
    return [round(x / n, 3) for x in v]


def deg_fwd_down(a):
    """Вперёд и вниз на a градусов."""
    t = math.radians(a)
    return (0.0, -math.sin(t), math.cos(t))


def P(name, host, pos, dir_, unit_m, mirror=False, proposed=False, note=''):
    d = dict(name=name, host=host, pos=[round(x, 3) for x in pos], dir=unit(dir_), unit=round(unit_m, 3))
    if mirror:
        d['mirror'] = True
    if proposed:
        d['proposed'] = True
    if note:
        d['note'] = note
    return d


def S(name, host, z0, z1, unit_m, proposed=False, note=''):
    d = dict(name=name, host=host, surface=dict(z0=z0, z1=z1), unit=round(unit_m, 3))
    if proposed:
        d['proposed'] = True
    if note:
        d['note'] = note
    return d


def node_frames(file):
    doc = json.load(open(os.path.join(OUT, file + '-graph.json'), encoding='utf-8'))
    w = faza.world(doc)
    out = {}
    for n in doc['nodes']:
        pos, rot = w[n['name']]
        out[n['name']] = (pos, faza.apply(rot, [0, 1, 0]), n)
    return out


def chassis(g, spine, neck, head, chest, head_w):
    """Места шасси — по узлам графа: начало и ось узла, единица — диаметр узла (голова — ширина головы)."""
    sp, sd, sn = g[spine]
    nk, nd, nn = g[neck]
    hd, hdd, hn = g[head]
    cp, cd, cn = g[chest]
    return [P('хребет', spine, sp, sd, 2 * sn['r0']),
            P('шея', neck, nk, nd, 2 * nn['r0']),
            P('голова', head, hd, hdd, head_w),
            P('Сердце', chest, cp, cd, 2 * cn['r1'], note='внутреннее'),
            P('Чутьё', head, hd, hdd, head_w, note='внутреннее: роли Eye/Ear/Nose/Pit — в местах головы')]


def volk():
    g = node_frames('volk')
    hw = 0.27
    return chassis(g, 'хребет', 'шея', 'голова', 'грудь', hw) + [
        P('Пасть', 'голова', (0, 1.08, 0.86), deg_fwd_down(22), hw),
        P('нос', 'голова', (0, 1.01, 1.15), FWD, hw),
        P('глаза', 'голова', (0.06, 1.20, 0.99), (0.5, 0, 0.87), hw, mirror=True),
        P('уши', 'голова', (0.08, 1.35, 0.86), UP, hw, mirror=True),
        P('ямки', 'голова', (0.06, 1.06, 1.08), (1, 0, 0), hw, mirror=True, proposed=True),
        P('Рога', 'голова', (0.07, 1.30, 0.86), (0, 0.8, -0.6), hw, mirror=True, note='у основания ушей'),
        P('горб', 'загривок', (0, 1.25, 0.30), UP, 0.35, proposed=True, note='загривок'),
        P('Игломёт', 'загривок', (0, 1.25, 0.30), UP, 0.35),
        S('Шкура', 'хребет', -0.60, 0.42, 1.02),
        P('Руки', 'предплечье', (0.09, 0.50, 0.42), DOWN, 0.104, mirror=True),
        P('Ноги', 'голень', (0.08, 0.42, -0.61), DOWN, 0.110, mirror=True),
        P('Хвост', 'крестец', (0, 0.93, -0.60), (0, -0.5, -0.87), 0.19),
        P('Наконечник', 'хвост_кисть', (0, 0.36, -0.97), (0, -0.3, -0.95), 0.116),
    ]


def los():
    g = node_frames('los')
    hw = 0.44
    return chassis(g, 'хребет', 'шея', 'голова', 'грудь', hw) + [
        P('Пасть', 'голова', (0, 2.22, 1.80), deg_fwd_down(20), hw),
        P('нос', 'переносье', (0, 1.97, 2.46), FWD, hw),
        P('глаза', 'голова', (0.14, 2.42, 1.97), (0.5, 0, 0.87), hw, mirror=True),
        P('уши', 'голова', (0.20, 2.73, 1.70), UP, hw, mirror=True),
        P('ямки', 'переносье', (0.12, 2.00, 2.30), (1, 0, 0), hw, mirror=True, proposed=True),
        P('Рога', 'голова', (0.15, 2.52, 1.72), (0.4, 0.9, -0.2), hw, mirror=True, note='у основания рогов'),
        P('горб', 'горб', (0, 2.85, 0.45), UP, 1.00, note='вершина горба'),
        P('Игломёт', 'горб', (0, 2.85, 0.45), UP, 1.00, proposed=True),
        S('Шкура', 'хребет', -1.20, 0.78, 1.98),
        P('Руки', 'предплечье', (0.19, 0.80, 0.49), DOWN, 0.170, mirror=True),
        P('Ноги', 'голень', (0.21, 1.12, -1.36), DOWN, 0.200, mirror=True),
        P('Хвост', 'хвост', (0, 2.26, -1.33), (0, -0.7, -0.7), 0.15),
        P('Наконечник', 'хвост', (0, 2.08, -1.52), (0, -0.7, -0.7), 0.13),
    ]


def ezh():
    g = node_frames('ezh')
    hw = 0.36
    return chassis(g, 'хребет', 'шея', 'голова', 'грудь', hw) + [
        P('Пасть', 'голова', (0, 0.52, 0.66), deg_fwd_down(25), hw),
        P('нос', 'голова', (0, 0.46, 0.93), FWD, hw),
        P('глаза', 'голова', (0.14, 0.61, 0.78), (0.5, 0, 0.87), hw, mirror=True),
        P('уши', 'голова', (0.12, 0.74, 0.66), (0.5, 0.7, -0.5), hw, mirror=True),
        P('ямки', 'голова', (0.09, 0.47, 0.86), (1, 0, 0), hw, mirror=True, proposed=True),
        P('Рога', 'голова', (0.10, 0.77, 0.63), (0.3, 0.8, -0.5), hw, mirror=True, proposed=True, note='по бокам темени'),
        P('горб', 'козырёк', (0, 0.66, 0.33), UP, 0.44, proposed=True),
        P('Игломёт', 'козырёк', (0, 0.661, 0.325), (0, 0.98, 0.17), 0.44, note='загривок, ось вверх-вперёд'),
        S('Шкура', 'хребет', -0.50, 0.60, 1.10),
        P('Руки', 'предплечье', (0.27, 0.09, 0.46), DOWN, 0.124, mirror=True),
        P('Ноги', 'голень', (0.24, 0.08, -0.42), DOWN, 0.100, mirror=True),
        P('Хвост', 'хвост', (0, 0.40, -0.50), (0, -0.84, -0.54), 0.11),
        P('Наконечник', 'хвост', (0, 0.29, -0.57), (0, -0.84, -0.54), 0.084, proposed=True, note='кончик обрубка'),
    ]


def zmeya():
    """Тело змеи — цепь: шея 4 звена (Z −0.18…−1.62), туловище 5 (−1.64…−3.44), хвост 6 (−3.44…−4.88), по 0.36/0.36/0.24 м;
    погремушка −4.90…−5.24 (замер стендом 25.09).
    Звено N — от головы, с 1. Хозяин гнезда на цепи — `звено:N`: гнездо едет со звеном (`SnakeBodyChain`)."""
    g = node_frames('zmeya')
    hd, hdd, _ = g['голова']
    hw = 0.34
    back = (0, 0, -1)
    return [
        P('хребет', 'звено:5', (0, 0.15, -1.64), back, 0.30, note='начало туловища (звено 5)'),
        P('шея', 'звено:1', (0, 0.15, -0.18), back, 0.215),
        P('голова', 'голова', hd, hdd, hw),
        P('Сердце', 'звено:6', (0, 0.15, -2.00), back, 0.22, note='внутреннее'),
        P('Чутьё', 'голова', hd, hdd, hw, note='внутреннее'),
        P('Пасть', 'голова', (0, 0.14, -0.08), FWD, hw),
        P('нос', 'голова', (0, 0.12, 0.30), FWD, hw, proposed=True, note='кончик рыла'),
        P('глаза', 'голова', (0.12, 0.22, 0.11), (0.6, 0.2, 0.77), hw, mirror=True),
        P('уши', 'голова', (0.14, 0.24, -0.06), UP, hw, mirror=True, proposed=True, note='за глазом'),
        P('ямки', 'голова', (0.12, 0.13, 0.17), (1, 0, 0.3), hw, mirror=True),
        P('Рога', 'голова', (0.08, 0.27, -0.02), (0.3, 0.8, -0.5), hw, mirror=True, proposed=True),
        P('горб', 'звено:5', (0, 0.30, -1.80), UP, 0.30, proposed=True),
        P('Игломёт', 'звено:5', (0, 0.30, -1.80), UP, 0.30, proposed=True, note='на первом звене туловища'),
        dict(name='Шкура', host='звено:*', surface=dict(z0=-4.88, z1=-0.18, perLink=True), unit=0.36,
             note='покров по цепи: кадр Шкуры на каждом звене (доля звена × угол от хребта)'),
        P('Руки', 'звено:4', (0.15, 0.08, -1.60), (0.7, -0.7, 0), 0.10, mirror=True, proposed=True, note='у шеи, низко'),
        P('Ноги', 'звено:9', (0.15, 0.08, -3.30), (0.7, -0.7, 0), 0.10, mirror=True, proposed=True, note='перед хвостом'),
        P('Хвост', 'звено:10', (0, 0.15, -3.44), back, 0.27),
        P('Наконечник', 'звено:15', (0, 0.15, -4.90), back, 0.12, note='погремушка'),
    ]


def chelovek():
    g = node_frames('chelovek')
    hw = 0.15
    return chassis(g, 'хребет', 'шея', 'голова', 'грудь2', hw) + [
        P('Пасть', 'голова', (0, 1.63, 0.09), FWD, hw),
        P('нос', 'голова', (0, 1.68, 0.10), FWD, hw),
        P('глаза', 'голова', (0.03, 1.70, 0.08), FWD, hw, mirror=True),
        P('уши', 'голова', (0.07, 1.69, 0.00), (1, 0, 0), hw, mirror=True),
        P('ямки', 'голова', (0.02, 1.66, 0.10), (1, 0, 0.5), hw, mirror=True, proposed=True),
        P('Рога', 'голова', (0.05, 1.79, 0.03), (0.3, 0.9, -0.2), hw, mirror=True, proposed=True, note='по бокам темени'),
        P('горб', 'хребет', (0, 1.42, -0.14), (0, 0.5, -0.87), 0.30, proposed=True),
        P('Игломёт', 'хребет', (0, 1.42, -0.14), (0, 0.5, -0.87), 0.30, proposed=True, note='между лопатками'),
        S('Шкура', 'хребет', 0.95, 1.50, 0.55, note='у человека спина вертикальна: z0/z1 — высоты Y по спине (Z −0.12)'),
        P('Руки', 'предплечье', (0.32, 0.89, 0.01), DOWN, 0.046, mirror=True),
        P('Ноги', 'голень', (0.18, 0.09, -0.06), DOWN, 0.066, mirror=True),
        P('Хвост', 'пояс1', (0, 0.98, -0.12), (0, -0.5, -0.87), 0.10, proposed=True, note='копчик'),
        P('Наконечник', 'пояс1', (0, 0.98, -0.12), (0, -0.5, -0.87), 0.10, proposed=True,
          note='без хвоста — на корне (письмо механик 26.09c §2 п. 4)'),
    ]


def main():
    for sp, fn in (('Волк', volk), ('Лось', los), ('Ёж', ezh), ('Змея', zmeya), ('Человек', chelovek)):
        places = fn()
        names = [p['name'] for p in places]
        missing = [n for n in PLACES if n not in names]
        extra = [n for n in names if n not in PLACES]
        if missing or extra or len(set(names)) != len(names):
            raise SystemExit('%s: нет %s, лишние %s, дубли %s' % (sp, missing, extra, len(names) - len(set(names))))
        path = os.path.join(OUT, FILES[sp] + '-places-layout.json')
        with open(path, 'w', encoding='utf-8') as f:
            json.dump(dict(species=sp, places=places), f, ensure_ascii=False, indent=2)
        print('%-8s мест %d (предложено %d) → %s' % (sp, len(places), sum(1 for p in places if p.get('proposed')),
                                                     os.path.relpath(path, ROOT)))


if __name__ == '__main__':
    main()
