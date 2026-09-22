# -*- coding: utf-8 -*-
"""КАНОН В ЧИСЛАХ — ANSUR II, выборка атлетов, приведённая к игровому росту 1.84.

ANSUR II — антропометрия армии США 2012 года: 93 промера на человека (ширины, обхваты, высоты суставов). Файл
`ANSUR II MALE Public.csv` (GitHub `senihberkay/US-Army-ANSUR-II`, первоисточник — Natick / openlab.psu.edu) в репо
не лежит: скачивается рядом и подаётся путём.

ПОЧЕМУ НЕ ПЕРЦЕНТИЛЬ. 95-й перцентиль каждого промера отдельно даёт толстяка: обхват талии растёт вместе с плечами
(у 95-го талия 0.38 × 0.30 при весе 112 кг). «Пик вида» — это сложение, а не размер: отбираются худые (талия к росту в
нижних 35 %) и одновременно мускулистые (плечевой пояс к талии — в верхней четверти, напряжённый бицепс к росту — в
верхних 30 %). Из 3 217 мужчин ростом 1.70–1.95 таких 78, ИМТ 26.5. Промеры берутся медианой, отнесённой к росту, × 1.84.

Запуск:  python ansur_atlet.py <путь к ANSUR II MALE Public.csv>
"""
import sys

import pandas as pd

H = 1840.0
COLS = [
    ('bideltoidbreadth', 'плечи по дельтам'), ('biacromialbreadth', 'плечи по акромионам'),
    ('chestbreadth', 'грудь, ширина'), ('chestdepth', 'грудь, глубина'), ('chestcircumference', 'грудь, обхват'),
    ('waistbreadth', 'талия, ширина'), ('waistdepth', 'талия, глубина'), ('waistcircumference', 'талия, обхват'),
    ('hipbreadth', 'таз, ширина'), ('buttockdepth', 'ягодицы, глубина'),
    ('headbreadth', 'голова, ширина'), ('headlength', 'голова, длина'), ('bizygomaticbreadth', 'скулы'),
    ('neckcircumference', 'шея, обхват'), ('neckcircumferencebase', 'шея у основания, обхват'),
    ('bicepscircumferenceflexed', 'бицепс напряжённый, обхват'), ('forearmcircumferenceflexed', 'предплечье, обхват'),
    ('wristcircumference', 'запястье, обхват'), ('handlength', 'кисть, длина'), ('handbreadth', 'кисть, ширина'),
    ('thighcircumference', 'бедро, обхват'), ('lowerthighcircumference', 'бедро над коленом, обхват'),
    ('calfcircumference', 'икра, обхват'), ('anklecircumference', 'лодыжка, обхват'),
    ('footlength', 'стопа, длина'), ('footbreadthhorizontal', 'стопа, ширина'), ('heelbreadth', 'пятка, ширина'),
    ('acromialheight', 'выс. акромиона'), ('axillaheight', 'выс. подмышки'), ('chestheight', 'выс. сосков'),
    ('waistheightomphalion', 'выс. пупка'), ('trochanterionheight', 'выс. вертела'), ('crotchheight', 'выс. промежности'),
    ('buttockheight', 'выс. ягодичной складки'), ('kneeheightmidpatella', 'выс. колена'), ('tibialheight', 'выс. щели колена'),
    ('lateralmalleolusheight', 'выс. лодыжки'), ('wristheight', 'выс. запястья'),
    ('shoulderelbowlength', 'плечо, акромион–локоть'), ('radialestylionlength', 'предплечье'),
]


def main():
    d = pd.read_csv(sys.argv[1], encoding='latin-1')
    d = d[(d.stature > 1700) & (d.stature < 1950)]
    whtr = d.waistcircumference / d.stature
    sw = d.shouldercircumference / d.waistcircumference
    arm = d.bicepscircumferenceflexed / d.stature
    a = d[(whtr < whtr.quantile(0.35)) & (sw > sw.quantile(0.75)) & (arm > arm.quantile(0.70))]
    bmi = lambda t: (t.weightkg / 10 / (t.stature / 1000) ** 2).median()
    print('всего %d, атлетов %d; ИМТ атлетов %.1f, всех %.1f' % (len(d), len(a), bmi(a), bmi(d)))
    print('%-30s %8s %8s %8s   мм при росте 1840' % ('промер', 'все p50', 'все p95', 'атлеты'))
    for c, n in COLS:
        print('%-30s %8.0f %8.0f %8.0f' % (n, (d[c] / d.stature).median() * H,
                                           d[c].quantile(.95) / d.stature.quantile(.95) * H, (a[c] / a.stature).median() * H))


if __name__ == '__main__':
    main()
