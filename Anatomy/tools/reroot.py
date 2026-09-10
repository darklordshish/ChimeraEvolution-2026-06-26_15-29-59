# -*- coding: utf-8 -*-
"""РАЗВОРОТ ОСИ К ХРЕБТУ — считает числа и проверяет их до применения.

НАПРАВЛЕНИЕ СМЕНИЛОСЬ 11.09. Прежде инструмент разворачивал ось К ГОЛОВЕ (И1 спеки 27.08); теперь
корень — `хребет` у всех пяти видов (спека `2026-09-11-edinyj-nabor-mest.md`, И1 отменена).
    Довод короткий: выбор корня есть выбор ЗНАМЕНАТЕЛЯ. `MorphBuilder.SizeOf` при пустом `parent`
берёт `baseSize`, минуя `sizeRel`, — у корня пропорция не выражается вовсе. Значит корнем обязано
быть НЕИЗМЕННОЕ: хребет `chassisOnly` и запрещён к смешению инвариантом И3, а голова наоборот —
самое подвижное место, на ней по правилу локальности донор только и проступает.
    Обратный разворот больше не нужен и не поддерживается: корень зафиксирован.

Цепь становится `хребет → шея → голова`. Разворот обязан оставить тело НА МЕСТЕ (инвариант Е5).

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
    ss = sizes['хребет']
    spine_euler = matrix_to_euler(sr)                       # хребет — корень: своя поза в мире
    neck_off = offset_for(np_, sp_, sr, ss, 0.0)            # шея садится на хребет
    neck_euler = euler_for(nr, sr)
    neck_rel = tuple(ns[i] / ss[i] if ss[i] > 0 else 0.0 for i in range(3))
    head_off = offset_for(hp, np_, nr, ns, 0.0)             # голова — на шею
    head_euler = euler_for(hr, nr)
    head_rel = tuple(hs[i] / ns[i] if ns[i] > 0 else 0.0 for i in range(3))

    # ── САМОПРОВЕРКА: собираем НОВЫЕ данные и считаем заново ──────────────────────────────────────
    test = {n: copy.deepcopy(s) for n, s in by.items()}
    t_head, t_neck, t_spine = test['голова'], test['шея'], test['хребет']
    t_spine['parent'] = ''
    t_spine['localPos'] = '{x: %f, y: %f, z: %f}' % sp_
    t_spine['baseEuler'] = '{x: %f, y: %f, z: %f}' % spine_euler
    t_spine['sizeRel'] = '{x: 0, y: 0, z: 0}'
    t_spine['baseSize'] = '{x: %f, y: %f, z: %f}' % ss
    t_neck['parent'] = 'хребет'
    t_neck['attach'] = '0'
    t_neck['attachOffset'] = '{x: %f, y: %f, z: %f}' % neck_off
    t_neck['baseEuler'] = '{x: %f, y: %f, z: %f}' % neck_euler
    t_neck['sizeRel'] = '{x: %f, y: %f, z: %f}' % neck_rel
    t_head['parent'] = 'шея'
    t_head['attach'] = '0'
    t_head['attachOffset'] = '{x: %f, y: %f, z: %f}' % head_off
    t_head['baseEuler'] = '{x: %f, y: %f, z: %f}' % head_euler
    t_head['sizeRel'] = '{x: %f, y: %f, z: %f}' % head_rel
    t_head['localPos'] = '{x: 0, y: 0, z: 0}'

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
    print('   ХРЕБЕТ — КОРЕНЬ')
    print('      localPos  = %s' % V(sp_))
    print('      baseEuler = %s' % V(spine_euler))
    print('      baseSize  = %s   sizeRel и parent убрать' % V(ss))
    print('   ШЕЯ на ХРЕБТЕ')
    print('      attach = 0.000f   attachOffset = %s' % V(neck_off))
    print('      baseEuler = %s   sizeRel = %s' % (V(neck_euler), V(neck_rel)))
    print('   ГОЛОВА на ШЕЕ')
    print('      attach = 0.000f   attachOffset = %s' % V(head_off))
    print('      baseEuler = %s   sizeRel = %s   localPos и baseSize убрать'
          % (V(head_euler), V(head_rel)))
    if max(abs(x) for x in head_off) > 1.5:
        print('\n   ! смещение головы %.2f калибра шеи — правка шеи двинет голову с этим коэффициентом'
              % max(abs(x) for x in head_off))
    return 0


sys.exit(main())
