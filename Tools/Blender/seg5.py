# -*- coding: utf-8 -*-
"""И5 ПОИМЁННО: тождественность после переноса проверяется по КАЖДОЙ кости, а не по габариту.

ЗАЧЕМ ИМЕННО ТАК. Габаритный вариант этой проверки 08.09 пропустил сдвиг в 374 мм: крайние точки
тела держали другие кости, и коробка совпала при двадцати уехавших. Габарит — не инвариант, а
следствие; сверять надо то, что переносится.

Разворот цепи МЕНЯЕТ МЕСТАМИ концы кости — это и есть суть переноса, — поэтому отрезок сравнивается
как НЕУПОРЯДОЧЕННАЯ ПАРА: {начало, конец}. Всё остальное обязано совпасть до десятой доли миллиметра.

Запуск:  python seg5.py wolf путь/к/старому/wolf.py
"""
import sys, io, os, importlib, math
import importlib.util as iu

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from chimera.skel import from_points

NAME, OLD = sys.argv[1], sys.argv[2]
spec = iu.spec_from_file_location(NAME + '_old', OLD)
old = iu.module_from_spec(spec)
spec.loader.exec_module(old)
new = importlib.import_module('species.' + NAME)

_, po, _ = from_points(old.DEFS, old.P, verbose=False)
_, pn, _ = from_points(new.DEFS, new.P, verbose=False)


def seg(pl, n):
    p, r, t = pl[n]
    return (tuple(p), tuple(t))


def dist(a, b):
    """Расхождение отрезков как НЕУПОРЯДОЧЕННЫХ пар — перебором двух сопоставлений.

    СОРТИРОВАТЬ КОНЦЫ НЕЛЬЗЯ, и это стоило ложной тревоги: у голени координата X отличалась
    в пятом знаке (шум с плавающей точкой), сортировка по первой координате дала РАЗНЫЙ порядок
    до и после, и сравнивались разные концы — детектор показал сдвиг 309 мм на неподвижной кости.
    Перебор двух вариантов от порядка не зависит вовсе."""
    prm = max(math.dist(a[0], b[0]), math.dist(a[1], b[1]))
    rev = max(math.dist(a[0], b[1]), math.dist(a[1], b[0]))
    return min(prm, rev)


# ПОРОГ = КВАНТ САМИХ ДАННЫХ, а не круглое число. `w()` округляет доли калибра до 0.1 мм, и
# перестроенная цепь набирает расхождение ровно этого порядка — не «почти ноль», а буквально один
# квант. Поэтому порог абсолютный (полтора кванта) и лишь у крупных видов растёт с калибром:
# у ежа при калибре 0.32 м доля 2e-4 дала бы 0.06 мм, то есть НИЖЕ кванта, и сторож кричал бы
# на округление. Порог, кричащий на неизбежное, приучает игнорировать красное
EPS = max(1.5e-4, 2e-4 * getattr(new, 'W', 1.0))

bad = []
for n in po:
    if n not in pn:
        bad.append((9.9, n, 'ИСЧЕЗЛА'))
        continue
    d = dist(seg(po, n), seg(pn, n))
    if d > EPS:
        bad.append((d, n, 'сдвиг %.1f мм' % (d * 1000)))
for n in pn:
    if n not in po:
        bad.append((9.9, n, 'ПОЯВИЛАСЬ'))

bad.sort(reverse=True)
print('И5 ПОИМЁННО · %s · костей было %d, стало %d · порог %.2f мм'
      % (NAME, len(po), len(pn), EPS * 1000))
if not bad:
    print('  ✅ ВСЕ КОСТИ НА МЕСТЕ — перенос тождественен')
else:
    print('  ✗ РАСХОЖДЕНИЙ: %d' % len(bad))
    for d, n, why in bad[:15]:
        print('     %-22s %s' % (n, why))
sys.exit(1 if bad else 0)
