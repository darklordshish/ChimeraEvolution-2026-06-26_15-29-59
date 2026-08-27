# -*- coding: utf-8 -*-
"""РАЗВОРОТ ОСИ ОТ ГОЛОВЫ — считает числа и проверяет их до применения.

Спека `2026-08-27-edinyj-plan-tela.md`: корень графа у всех видов — `голова`, ось идёт назад
(голова → шея → хребет → Хвост). Разворот обязан оставить тело НА МЕСТЕ (инвариант И5).

КАК ЭТО РАБОТАЕТ. Позиции и повороты МЕСТ считаются портом `MorphBuilder.Place` (см. speciesdata):
плоской формулой обойтись нельзя — смещение поворачивается на поворот родителя, а сам поворот
наследуется вниз по ветке. У ежа наклонов нет и плоский расчёт сходился; у человека шея наклонена,
и он врал на 5 см.

ПОЧЕМУ НЕ СВЕРЯЕМСЯ С КАРТОЙ ТЕЛ. Карта меряет границы НАРИСОВАННЫХ ДЕТАЛЕЙ, а нам нужны позиции
МЕСТ — это разные вещи: деталь может не заполнять своё место. Модель проверена иначе, на еже: она
воспроизвела позиции, которые я задал при его развороте, с точностью 0.2 мм.

САМОПРОВЕРКА. Посчитав новые числа, скрипт собирает по ним виртуальные данные и прогоняет `place`
заново. Если хоть одно место уехало — числа не выдаются.

Запуск:  python reroot.py --species Человек
"""
import argparse
import copy
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from speciesdata import (parse_asset, index, size_of, place, offset_for,      # noqa: E402
                         euler_for, matrix_to_euler, vec)

AP = argparse.ArgumentParser()
AP.add_argument('--species', required=True)
A = AP.parse_args()

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
DATA = os.path.join(ROOT, 'Assets', '_Chimera', 'Data')

V = lambda t: '(%.3ff, %.3ff, %.3ff)' % t


def main():
    path = os.path.join(DATA, A.species + '.asset')
    if not os.path.exists(path):
        sys.exit('нет ассета %s' % path)
    _organs, sockets, _bones = parse_asset(path)
    by = index(sockets)
    for need in ('голова', 'шея', 'хребет'):
        if need not in by:
            sys.exit('у вида «%s» нет места «%s» — схема разворота не подходит' % (A.species, need))

    # ЭТАЛОН: где всё стоит СЕЙЧАС
    cache = {}
    before = {n: place(s, by, cache) for n, s in by.items()}
    sizes = {n: size_of(s, by) for n, s in by.items()}

    if not by['голова'].get('parent') or 'голова' == by['голова'].get('_name') and not vec(by['голова'].get('localPos')) == (0, 0, 0) and by['голова'].get('parent', '').strip() == '':
        pass
    if by['хребет'].get('parent', '').strip() == '' :
        pass

    hp, hr = before['голова']
    np_, nr = before['шея']
    sp_, sr = before['хребет']
    hs, ns = sizes['голова'], sizes['шея']

    print('== %s ==' % A.species)
    print('   сейчас: голова %s | шея %s | хребет %s'
          % (tuple(round(x, 3) for x in hp), tuple(round(x, 3) for x in np_), tuple(round(x, 3) for x in sp_)))

    # ── ОБРАТНАЯ ЗАДАЧА ───────────────────────────────────────────────────────────────────────────
    head_euler = matrix_to_euler(hr)
    neck_off = offset_for(np_, hp, hr, hs, 0.0)
    neck_euler = euler_for(nr, hr)
    neck_rel = tuple(ns[i] / hs[i] if hs[i] > 0 else 0.0 for i in range(3))
    spine_off = offset_for(sp_, np_, nr, ns, 0.0)
    spine_euler = euler_for(sr, nr)

    # ── САМОПРОВЕРКА: собираем НОВЫЕ данные и считаем заново ──────────────────────────────────────
    test = {n: copy.deepcopy(s) for n, s in by.items()}
    t_head, t_neck, t_spine = test['голова'], test['шея'], test['хребет']
    t_head['parent'] = ''
    t_head['localPos'] = '{x: %f, y: %f, z: %f}' % hp
    t_head['baseEuler'] = '{x: %f, y: %f, z: %f}' % head_euler
    t_head['sizeRel'] = '{x: 0, y: 0, z: 0}'
    t_head['baseSize'] = '{x: %f, y: %f, z: %f}' % hs
    t_neck['parent'] = 'голова'
    t_neck['attach'] = '0'
    t_neck['attachOffset'] = '{x: %f, y: %f, z: %f}' % neck_off
    t_neck['baseEuler'] = '{x: %f, y: %f, z: %f}' % neck_euler
    t_neck['sizeRel'] = '{x: %f, y: %f, z: %f}' % neck_rel
    t_spine['parent'] = 'шея'
    t_spine['attach'] = '0'
    t_spine['attachOffset'] = '{x: %f, y: %f, z: %f}' % spine_off
    t_spine['baseEuler'] = '{x: %f, y: %f, z: %f}' % spine_euler
    t_spine['sizeRel'] = '{x: 0, y: 0, z: 0}'
    t_spine['baseSize'] = '{x: %f, y: %f, z: %f}' % sizes['хребет']

    cache2 = {}
    worst, who = 0.0, ''
    for n, s in test.items():
        p, _r = place(s, test, cache2)
        e = max(abs(p[i] - before[n][0][i]) for i in range(3))
        if e > worst:
            worst, who = e, n
    print('\n   САМОПРОВЕРКА: пересчёт по новым числам, макс. смещение %.4f м (%s)' % (worst, who))
    if worst > 0.005:
        print('   !! места уезжают — числа НЕ выдаю, схема для этого вида не подходит')
        return 1
    print('   тело остаётся на месте, числа годны')

    print('\nЧИСЛА ДЛЯ РАЗВОРОТА:')
    print('   ГОЛОВА — КОРЕНЬ')
    print('      localPos  = %s' % V(hp))
    print('      baseEuler = %s' % V(head_euler))
    print('      baseSize  = %s   sizeRel и parent убрать' % V(hs))
    print('   ШЕЯ на ГОЛОВЕ')
    print('      attach = 0.000f   attachOffset = %s' % V(neck_off))
    print('      baseEuler = %s   sizeRel = %s' % (V(neck_euler), V(neck_rel)))
    print('   ХРЕБЕТ на ШЕЕ')
    print('      attach = 0.000f   attachOffset = %s' % V(spine_off))
    print('      baseEuler = %s   baseSize = %s   sizeRel убрать'
          % (V(spine_euler), V(sizes['хребет'])))
    if max(abs(x) for x in spine_off) > 1.5:
        print('\n   ! смещение хребта %.2f калибра шеи — правка шеи двинет хребет с этим коэффициентом'
              % max(abs(x) for x in spine_off))
    return 0


sys.exit(main())
