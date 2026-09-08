# -*- coding: utf-8 -*-
"""ДЕТЕКТОР ОБЩЕГО ГРАФА: вид либо покрывает узлы, либо смешение не определено.

Запуск:  python graphcheck.py              # волк и человек обязаны быть чисты
         python graphcheck.py moose        # следующий вид — красный, пока нет вложения
"""
import sys, io, os, importlib

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

from chimera import graph as G

MUST_PASS = ('wolf', 'human')


def load(name):
    return importlib.import_module('species.' + name)


def main(names):
    rc = 0
    if not names:
        names = list(MUST_PASS)
        # следующие виды — если файлы уже лежат: обязаны краснеть, пока GRAPH пуст
        for nxt in ('moose', 'hedgehog', 'snake'):
            p = os.path.join(HERE, 'species', nxt + '.py')
            if os.path.isfile(p):
                names.append(nxt)
    for name in names:
        try:
            sp = load(name)
        except Exception as e:
            print('ГРАФ %s: НЕ ЗАГРУЗИЛСЯ (%s)' % (name, e))
            rc = 1
            continue
        nbad = G.report(sp, name)
        print()
        if name in MUST_PASS and nbad:
            rc = 1
        if name not in MUST_PASS and nbad == 0 and not G.present_nodes(sp):
            rc = 1
        if name not in MUST_PASS and nbad == 0:
            # ещё не вложенный вид, прошедший check — подозрительно только если GRAPH пуст;
            # если вложение полное, это ок
            pass
        if name not in MUST_PASS and G.check(sp):
            # ожидаемо красный
            pass
    return rc


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
