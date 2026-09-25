# -*- coding: utf-8 -*-
"""РАСКЛАДКА ГОЛОВЫ ЗМЕИ — калибр, рыло (Пасть), глаза, термоямки.

Кадр головы — узел `голова` графа: начало на затылке, ось к кончику рыла. Калибр: W × H прежние (0.24 × 0.163),
L — от затылка до кончика рыла (0.56). В `head` только `baseSize`: `sizeRel` у головы змеи нулевой (калибр читается
из `baseSize`, у цепного родителя долю не возьмёшь), и раскладка его не трогает.

Анфас `rattlesnake_head_front.jpg` (доли ширины затылка): у глаз 0.59, у рыла 0.55; глаза на верхних углах головы,
под нависающим щитком; термоямки — между ноздрёй и глазом, ниже линии глаза. Профиль `rattlesnake_head_lateral.jpg`:
глаз на 0.40 длины от кончика рыла, ямка — на 0.28 от кончика.

Запуск:  python head_layout.py [--out путь]
"""
import argparse
import json
import os

import graph as G

HERE = os.path.dirname(os.path.abspath(__file__))

W, H = G.HEAD_W, G.HEAD_H
L = round(G.HEAD_TIP_Z - G.HEAD_BACK_Z, 3)


def rel(abs_size, cal):
    return tuple(round(a / c, 4) for a, c in zip(abs_size, cal))


def place(name, attach, along, up, lateral, size, euler=(0.0, 0.0, 0.0), note='', parent='голова', cal=None):
    cw, ch, cl = cal or (W, H, L)
    return dict(name=name, parent=parent, attach=attach,
                attachOffset=[round(lateral / cw, 4), round(up / ch, 4), round(along / cl - attach, 4)],
                sizeRel=list(rel(size, (cw, ch, cl))), baseEuler=list(euler), note=note,
                metres=dict(along=round(along, 3), up=round(up, 3), lateral=round(lateral, 3), size=list(size)))


# ЗАМЕР ОБОЛОЧКИ (`ProbeM.SurfaceX`, окно ±4 см): поверхность узла `голова` вбок у глаза — 0.058 на клетке 0.084 (узел
# худел до пластины), 0.093 на 0.042. Глаз на верхнем углу головы выступает из-под щитка вбок: центр на 4 мм снаружи.
# Строка 0.084 — число, стоявшее в поставке 7 (0.090, глаз там выступал на 3 см: поле было тоньше расчёта)
MUZZLE_BLOCK = 'рыло'    # свой блок (25.09): `клин` давал иглу, `булава` (морда лося) — утиный клюв
MUZZLE_H = 0.95         # высота рыла к высоте головы: чуть ниже поля в начале, шов внутри (было 0.55, 0.75, 1.00)
MUZZLE_FROM = 0.10      # начало рыла от затылка, м: в толще щёк, торец утоплен в поле (итерация 6; было 0.22 — колпачок со ступенькой; 0.30, 0.38)
MUZZLE_W = 0.92         # ширина рыла к ширине щёк (было 0.55, 0.59, 0.85 — ступенька «щёки — рыло» сверху)
EYE_ALONG = 0.28        # глаз от затылка, м: на 0.40 длины от кончика рыла (`rattlesnake_head_lateral.jpg`)
GRAIN = 0.042          # клетка проекта (решение геймдизайнера 22.09)
SURFACE_AT_EYE = {0.084: 0.086, 0.042: 0.093}[GRAIN] + 0.004


def layout():
    # РЫЛО = ПАСТЬ: от глаз (заход в череп 5 см) до кончика; ширина у рыла 0.55 затылка, высота — 0.7 высоты головы
    # итерация 1: брусок 0.13 × 0.12 × 0.33 был крупнее черепа и висел отдельной коробкой ниже темени; итерация 2:
    # ровный брусок в профиль торчал трубой — клин короче, верх вровень с теменем, сходит к кончику
    # итерация 3 (25.09, сверка с 3D-образцом головы): клин сходит к кончику до 0.43 ширины — у рыла 0.165 это 7 см,
    # и голова в профиль и сверху читалась ИГЛОЙ. У гремучника рыло почти той же ширины, что у глаз (0.55 против 0.59
    # затылка по `rattlesnake_head_front.jpg`), торец тупой и закруглённый. `булава` держала ширину, но к кончику росла
    # вширь и плющилась по высоте — утиный клюв (геймдизайнер 25.09). Свой блок `рыло` (`Tools/Blender/blocks.py`)
    # ИТЕРАЦИЯ 5 (25.09): рыло — ПЕРЕДНЯЯ ПОЛОВИНА ГОЛОВЫ блоком, а не нос. Поле черепа — капсула, в профиль яйцо, и никакой
    # подгонкой чисел плоского темени и прямой челюсти гадюки из него не выходит. Блок от середины головы до кончика,
    # шириной и высотой во всю голову там, где начинается; поле черепа кончается внутри него. Змеиная Пасть, привитая
    # другому, принесёт змеиную морду — как волчья Пасть приносит волчью
    m_a0, m_a1 = MUZZLE_FROM, L
    m_len = m_a1 - m_a0
    m_w, m_h = round(W * MUZZLE_W, 3), round(H * MUZZLE_H, 3)
    m_a, m_b = (m_a0 + m_a1) / 2, 0.0
    # ГЛАЗА И ЯМКИ — НА ПОВЕРХНОСТИ БЛОКА, по его сечению (`blocks.py`: ширина 1.00 → 0.86 на t −0.10 → 0.70 на +0.25).
    # Прежний вынос глаза 0.097 был замерен на узкой голове 0.30 × 0.19 — на новой голове глаза утонули
    def half_w(along):
        t = (along - m_a0) / m_len - 0.5
        k = 1.00 + (0.86 - 1.00) * (t + 0.5) / 0.4 if t < -0.10 else 0.86 + (0.70 - 0.86) * (t + 0.10) / 0.35
        return m_w / 2 * k
    eye_a = EYE_ALONG
    eye_b = round(m_h * 0.30, 3)                           # верхний угол головы, под щитком
    pit_a, pit_b = L - 0.28 * L, -round(m_h * 0.05, 3)
    places = [
        place('Пасть', 1.0, m_a, m_b, 0.0, (m_w, m_h, round(m_len, 3)), note='рыло: блок `рыло`, тупое и закруглённое'),
        place('глаза', 0.5, eye_a, eye_b, round(half_w(eye_a) - 0.005, 3), (0.055, 0.055, 0.055), note='на верхнем углу головы, под щитком'),
        # ТЕРМОЯМКА — на боку рыла: вбок = полуширина бруска на этой длине (сужение бруска к торцу 0.72)
        place('ямки', 0.5, pit_a, pit_b, round(half_w(pit_a), 3), (0.035, 0.03, 0.03),
              note='ямка между ноздрёй и глазом, ниже линии глаза'),
    ]
    return places, (m_w, m_h, m_len)


def senses():
    return [
        dict(role='Eye', block='глаз', offset=[0, 0, 0], scale=[0.60, 0.80, 1.10], euler=[0, 0, 0]),
        dict(role='Pit', block='капля', offset=[0, 0, 0], scale=[0.50, 1.00, 1.00], euler=[0, 0, 0]),
    ]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--out', default=os.path.join(HERE, 'out', 'zmeya-head-layout-draft.json'))
    args = ap.parse_args()
    places, muzzle = layout()
    doc = dict(head=dict(baseSize=[W, H, L]), places=places,
               muzzle=dict(block=MUZZLE_BLOCK, offset=[0, 0, 0], scale=[1, 1, 1]), senses=senses())
    os.makedirs(os.path.dirname(args.out), exist_ok=True)
    with open(args.out, 'w', encoding='utf-8') as f:
        json.dump(doc, f, ensure_ascii=False, indent=2)
    print('калибр головы W×H×L = %.3f × %.3f × %.3f' % (W, H, L))
    for p in places:
        m = p['metres']
        print('  %-6s offset %-26s sizeRel %-26s (вдоль %.3f, к темени %.3f, вбок %.3f)' %
              (p['name'], p['attachOffset'], p['sizeRel'], m['along'], m['up'], m['lateral']))
    print('рыло %.3f × %.3f × %.3f' % muzzle)


if __name__ == '__main__':
    main()
