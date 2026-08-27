# -*- coding: utf-8 -*-
"""САМОПРОВЕРКА ДЕТЕКТОРА: подсовываем заведомо испорченный вид и требуем, чтобы он заругался.

Зачем. Аудит, который на всех пяти видах говорит «чисто», ничем не отличается от аудита, который
сломан и молчит всегда. Отличить их можно единственным способом — заставить его найти то, что мы
сами спрятали. Первая же попытка это подтвердила: «порча» габарита оказалась безвредной, потому что
у места задан `sizeRel` и `baseSize` не читается — детектор был прав, что промолчал, а тест врал.

Запуск:  python audit_selftest.py
"""
import os
import re
import shutil
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
DATA = os.path.join(ROOT, 'Assets', '_Chimera', 'Data')
TMP = os.path.join(HERE, '_selftest~')


def load(p):
    return open(p, encoding='utf-8', newline='').read()


def save(p, t):
    open(p, 'w', encoding='utf-8', newline='').write(t)


def prepare():
    if os.path.isdir(TMP):
        shutil.rmtree(TMP)
    os.makedirs(TMP)
    src = None
    for f in os.listdir(DATA):
        if f.endswith('.asset'):
            shutil.copy(os.path.join(DATA, f), os.path.join(TMP, f))
            if src is None:
                src = os.path.join(TMP, f)
    return src


def spoil(path):
    """Три порчи, каждая — известная мина проекта. Работаем по СТРУКТУРЕ строк, а не по кириллице:
    имена в ассете записаны escape-последовательностями, и сравнивать с ними русский текст бесполезно."""
    lines = load(path).split('\n')
    done = []

    # 1. ОПЕЧАТКА В РОДИТЕЛЕ: место повиснет, примыкать будет не к чему
    for i, ln in enumerate(lines):
        if re.match(r'^    parent: ".+"$', ln):
            lines[i] = '    parent: "NOPE"'
            done.append('опечатка в родителе')
            break

    # 2. ЗАПАС ДЛИННОЙ ОСИ: две стороны почти равны — ось молча переключится и развернёт ветку детей.
    #    Портить надо ТО ЧИСЛО, КОТОРОЕ РЕАЛЬНО ЧИТАЕТСЯ. Первая попытка меняла `sizeRel`, и мина не
    #    срабатывала: доли умножаются на габарит родителя, поэтому близкие доли не дают близких сторон.
    #    Значит ищем место, у которого доля НЕ задана, — там в дело идёт `baseSize`
    blocks = []
    start = None
    for i, ln in enumerate(lines):
        if re.match(r'^  - name: ', ln):
            if start is not None:
                blocks.append((start, i))
            start = i
        elif start is not None and re.match(r'^  \w+:', ln):
            blocks.append((start, i))
            start = None
    for a, b in blocks:
        chunk = lines[a:b]
        has_rel = any(re.match(r'^    sizeRel: \{x: 0, y: 0, z: 0\}$', x) for x in chunk)
        idx = next((j for j, x in enumerate(chunk) if x.startswith('    baseSize:')), None)
        if has_rel and idx is not None:
            lines[a + idx] = '    baseSize: {x: 0.62, y: 0.61, z: 0.2}'
            done.append('запас длинной оси ~2%')
            break

    # 3. ОПЕЧАТКА В СЛОТЕ: орган не найдёт своё место и станет невидимым
    for i, ln in enumerate(lines):
        if re.match(r'^    slot: ".+"$', ln):
            lines[i] = '    slot: "NOPE"'
            done.append('опечатка в слоте')
            break

    save(path, '\n'.join(lines))
    return done


def main():
    src = prepare()
    if src is None:
        print('нет ассетов для проверки')
        return 1
    spoiled = spoil(src)
    print('испорчен %s:' % os.path.basename(src))
    for d in spoiled:
        print('   - ' + d)

    out = subprocess.run([sys.executable, os.path.join(HERE, 'audit.py'), '--data', TMP],
                         capture_output=True, text=True, encoding='utf-8')
    text = (out.stdout or '') + (out.stderr or '')
    print('\n--- что сказал детектор ---')
    for ln in text.split('\n'):
        if ln.strip() and not ln.startswith('== ') and 'чисто' not in ln:
            print(ln)

    need = [('родитель', 'опечатка в родителе'),
            ('запас длинной оси', 'запас длинной оси'),
            ('вне словаря', 'опечатка в слоте')]
    missed = [human for key, human in need if key not in text]
    shutil.rmtree(TMP, ignore_errors=True)

    print()
    if missed:
        print('ПРОВАЛ: детектор не увидел — %s' % ', '.join(missed))
        return 1
    print('ДЕТЕКТОР ЛОВИТ: все три подсунутые мины найдены')
    return 0


sys.exit(main())
