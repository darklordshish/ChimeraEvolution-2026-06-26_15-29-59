# -*- coding: utf-8 -*-
"""ДЕТЕКТОР ЛИНИИ ПАРТИИ — проверяет, что у каждого решения ровно один действующий источник.

ЗАЧЕМ. 27.08 спека объявила корнем тела `голова`, гайд конструктора с 10.08 говорил `хребет`, и оба
документа числились действующими ПОЛТОРА МЕСЯЦА. Заметить это было нечем: оба текста выглядели
одинаково живыми, статус в шапке описывал ЖАНР документа («спека, к исполнению», «дизайн»), а не его
состояние. Из 34 спек ни одна не была помечена выполненной или отменённой, хотя половина закрыта
месяцем раньше.

ПОЧЕМУ СКРИПТ, А НЕ ПРАВИЛО В ДОКЕ. Правила у нас уже были — и протухли ровно так же: аудит проверял
отменённый инвариант, пять тестов сторожили отменённые формулы, карта тел теряла треть отчёта. Все
трое написаны добросовестно, все трое сгнили молча. Дисциплина забывается, детектор — нет.

Проверяет (спека `2026-09-11-odna-liniya-partii.md`):
  1. у каждой спеки статус ИЗ СЛОВАРЯ — жанр вместо состояния больше не проходит;
  2. `Отменена: X` → файл X существует и содержит встречное `Отменяет` — ссылка обязана быть
     двусторонней, потому что читают-то как раз старый документ;
  3. ни одна `ДЕЙСТВУЮТ` не отменена ничем — это и есть «две линии партии»;
  4. каждая спека упомянута в `УКАЗАТЕЛЬ.md` — ненайденная спека всё равно что мёртвая;
  5. ТОЧКИ ВХОДА (`СТАТУС.md`, `CLAUDE.md`, `CONSTRUCTOR_GUIDE.md`) не ссылаются на целиком отменённый
     документ без пометки об отмене. Добавлено 11.09: все четыре проверки выше смотрели только шапки спек,
     а СТАТУС тем временем держал клетку в таблице «решено — не переоткрывать» через месяц после её отмены;
  6. отмена «целиком» из действующего документа модельной линии сходится с шапкой самой спеки. Там же:
     `SPEC-konstruktor-formy.md` §9 отменил две наши спеки в ТЕЛЕ, а их шапки остались живыми.

Запуск:  python Docs/tools/spec_status.py
Выход:   0 — чисто, 1 — есть нарушения (годится для гейта перед коммитом).
"""
import io
import os
import re
import sys

# КОНСОЛЬ WINDOWS — cp1251, и первая же стрелка «→» в отчёте роняет скрипт UnicodeEncodeError.
# Отчёт на русском, обойтись ASCII нельзя (на этом уже терялся прогон `audit.py`)
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')

HERE = os.path.dirname(os.path.abspath(__file__))          # Docs/tools
DOCS = os.path.dirname(HERE)                               # Docs
ROOT = os.path.dirname(DOCS)                               # корень репо
SPECS = os.path.join(DOCS, 'superpowers', 'specs')
MODELS = os.path.join(DOCS, 'models')
INDEX = os.path.join(DOCS, 'УКАЗАТЕЛЬ.md')
ENTRY = [os.path.join(DOCS, 'СТАТУС.md'), os.path.join(ROOT, 'CLAUDE.md'), os.path.join(DOCS, 'CONSTRUCTOR_GUIDE.md')]

# ДВА НЕЗАВИСИМЫХ ИЗМЕРЕНИЯ, а не одна шкала. Спека 01.08 ВЫПОЛНЕНА (код написан), но её правило
# «имя сокета = Organ.slot» ДЕЙСТВУЕТ и обязательно для всех; пометь её просто «выполнена» — и
# читатель решит, что читать необязательно. Спека 27.08 наоборот: выполнена, но один инвариант из
# трёх отменён, а два живы. Одной шкалой это не выражается
WORK = ('ЧЕРНОВИК', 'В РАБОТЕ', 'ВЫПОЛНЕНА')
LIVE = ('ДЕЙСТВУЮТ', 'ОТМЕНЕН', 'ОТМЕНЁН', 'ОТМЕНЕНЫ')     # «отменён» ловим и с е, и с ё
CANCEL = ('ОТМЕНЕН', 'ОТМЕНЁН', 'ОТМЕНЕНЫ')
MARK = re.compile(r'⛔|отмен|~~', re.IGNORECASE)             # пометка «это отменено» рядом со ссылкой


def head(text):
    """ТОЛЬКО ШАПКА — до первого разделителя или до первого раздела. В ТЕЛЕ спеки слова «Отменяет» и
    «Отменена» законны: спека про линию партии их прямо объясняет, и первая же версия детектора
    приняла объяснение за поле. Сторож, спотыкающийся о собственный предмет, — плохой сторож."""
    for stop in ('\n---', '\n## '):
        i = text.find(stop)
        if i > 0:
            return text[:i]
    return '\n'.join(text.split('\n')[:15])


def field(text, name):
    """Значение поля шапки. Формат шапок по проекту РАЗНЫЙ — где-то список с дефисом, где-то строка
    через «·», — поэтому берём терпимо: от `**Имя:**` до конца строки."""
    m = re.search(r'\*\*' + name + r':\*\*\s*(.+)', head(text))
    return m.group(1).strip() if m else ''


def links(value):
    """Спеки, упомянутые в поле. Пишут по-разному: в бэктиках, с .md и без, иногда несколько."""
    return re.findall(r'(2026-\d{2}-\d{2}-[a-z0-9\-]+)', value)


def outer(value):
    """ОТМЕНИТЕЛЬ БЫВАЕТ ВНЕ ПАПКИ СПЕК. Решение по источнику формы принимает модельная линия, и её
    документы лежат в `Docs/models/` (`SPEC-konstruktor-formy.md` отменил сразу три наших). Требовать
    от них нашего формата шапки нельзя — это чужая зона; но проверить, что файл существует, обязаны:
    ссылка на несуществующий отменитель хуже отсутствия ссылки."""
    return re.findall(r'(SPEC-[a-z0-9\-]+)', value)


def whole_cancel(status):
    """ОТМЕНЕНА ЦЕЛИКОМ — отмена есть, а «остальное действует» нет. Частичная отмена ссылку не запрещает:
    спека 27.08 отменена в И1, но её И3 живой, и цитировать его законно."""
    return any(w in status for w in CANCEL) and 'действ' not in status.lower()


def units(text):
    """СМЫСЛОВЫЕ ЕДИНИЦЫ markdown: абзац, пункт списка с продолжением, строка таблицы, заголовок, строка
    врезки. Проверять построчно нельзя — пометка «⛔ отменено» стоит в начале абзаца, а ссылка на
    отменённое переносится строкой ниже; проверять весь раздел целиком тоже нельзя — одно слово «отмен»
    в разделе оправдало бы все ссылки в нём."""
    out, cur = [], []
    for line in text.split('\n'):
        s = line.strip()
        starts = (not s) or s.startswith(('- ', '* ', '|', '#', '>')) or re.match(r'\d+\.\s', s)
        if starts and cur:
            out.append('\n'.join(cur))
            cur = []
        if s:
            cur.append(line)
    if cur:
        out.append('\n'.join(cur))
    return out


def cancelled_outer():
    """Документы вне папки спек с шапкой «⛔ ОТМЕНЕНО» — так их помечает модельная линия."""
    names = []
    for folder in (MODELS, DOCS):
        for f in sorted(os.listdir(folder)):
            if f.endswith('.md'):
                top = '\n'.join(open(os.path.join(folder, f), encoding='utf-8').read().split('\n')[:3])
                if '⛔' in top and 'ОТМЕНЕН' in top:
                    names.append(f[:-3])
    return names


def main():
    if not os.path.isdir(SPECS):
        print('НЕТ ПАПКИ СПЕК: %s' % SPECS)
        return 1

    names = sorted(f for f in os.listdir(SPECS) if f.endswith('.md'))
    spec, bad = {}, []
    for f in names:
        t = open(os.path.join(SPECS, f), encoding='utf-8').read()
        spec[f[:-3]] = {
            'status': field(t, 'Статус'),
            'cancels': field(t, 'Отменяет'),
            'cancelled': field(t, 'Отменена'),
        }

    index = open(INDEX, encoding='utf-8').read() if os.path.exists(INDEX) else ''

    for key in sorted(spec):
        s = spec[key]
        st = s['status']

        # 1. СТАТУС ИЗ СЛОВАРЯ
        if not st:
            bad.append((key, 'нет поля «Статус» в шапке'))
            continue
        if not any(w in st for w in WORK):
            bad.append((key, 'статус описывает ЖАНР, а не состояние: «%s» (ждём одно из %s)'
                        % (st[:60], ', '.join(WORK))))
        elif 'ЧЕРНОВИК' not in st and not any(w in st for w in LIVE):
            bad.append((key, 'не сказано, действуют ли решения: «%s»' % st[:60]))

        # 2. ОТМЕНА ДВУСТОРОННЯЯ
        for name in outer(s['cancelled']) + outer(s['cancels']):
            if not os.path.exists(os.path.join(MODELS, name + '.md')):
                bad.append((key, 'ссылается на несуществующий документ «Docs/models/%s.md»' % name))

        for other in links(s['cancelled']):
            if other not in spec:
                bad.append((key, 'отменена несуществующей спекой «%s»' % other))
            elif key not in links(spec[other]['cancels']):
                bad.append((key, 'односторонняя отмена: «%s» её не признаёт (нет встречного «Отменяет»)' % other))
        for other in links(s['cancels']):
            if other not in spec:
                bad.append((key, 'отменяет несуществующую спеку «%s»' % other))
            elif key not in links(spec[other]['cancelled']):
                bad.append((key, 'односторонняя отмена: «%s» не знает, что отменена' % other))

        # 3. ДВЕ ЛИНИИ ПАРТИИ — ровно тот случай, ради которого всё затеяно
        if 'ДЕЙСТВУЮТ' in st and not any(w in st for w in CANCEL):
            if s['cancelled']:
                bad.append((key, 'решения объявлены действующими, но спека отменена: «%s»' % s['cancelled'][:50]))

        # 4. НАЙДЁТСЯ ЛИ
        if index and key not in index:
            bad.append((key, 'не упомянута в УКАЗАТЕЛЬ.md — её просто не найдут'))

    # 5. ТОЧКИ ВХОДА НЕ ДЕРЖАТ ОТМЕНЁННОЕ КАК ДЕЙСТВУЮЩЕЕ
    gone = [k for k in spec if whole_cancel(spec[k]['status'])] + cancelled_outer()
    for path in ENTRY:
        if not os.path.exists(path):
            continue
        doc = os.path.relpath(path, ROOT).replace('\\', '/')
        for u in units(open(path, encoding='utf-8').read()):
            for g in gone:
                if g in u and not MARK.search(u):
                    first = u.strip().split('\n')[0]
                    bad.append((doc, 'ссылка на отменённый «%s» без пометки об отмене: «%s»' % (g, first[:70])))

    # 6. ОТМЕНА «ЦЕЛИКОМ» ИЗ ЧУЖОГО ДОКУМЕНТА СХОДИТСЯ С ШАПКОЙ СПЕКИ
    for f in sorted(os.listdir(MODELS)):
        if not f.endswith('.md') or f[:-3] in gone:
            continue
        for u in units(open(os.path.join(MODELS, f), encoding='utf-8').read()):
            first = u.strip().split('\n')[0]
            if not first.startswith('**Отменено'):
                continue
            entire = 'целиком' in first
            for item in u.split('·'):
                partial = '§' in item or re.search(r'\bИ\d', item) or 'строка' in item
                for key in links(item):
                    st = spec.get(key, {}).get('status', '')
                    if key not in spec:
                        bad.append((key, '«%s» отменяет несуществующую спеку' % f))
                    elif not any(w in st for w in CANCEL):
                        bad.append((key, '«%s» её отменяет, а шапка спеки об отмене молчит: «%s»' % (f, st[:60])))
                    elif entire and not partial and not whole_cancel(st):
                        bad.append((key, '«%s» отменяет её ЦЕЛИКОМ, а шапка говорит «%s» — две линии партии, '
                                         'решает владелец решения' % (f, st[:55])))

    print('СПЕК: %d' % len(spec))
    live = [k for k in spec if 'ДЕЙСТВУЮТ' in spec[k]['status']]
    done = [k for k in spec if 'ВЫПОЛНЕНА' in spec[k]['status']]
    dead = [k for k in spec if any(w in spec[k]['status'] for w in CANCEL)]
    print('  решения действуют: %d · работы закончены: %d · есть отменённое: %d' % (len(live), len(done), len(dead)))
    print('  отменено целиком (спеки и документы с шапкой ⛔): %d · точек входа под проверкой: %d' % (len(gone), len(ENTRY)))

    if not bad:
        print('\nЧИСТО: у каждого решения ровно один действующий источник')
        return 0

    print('\nНАРУШЕНИЙ: %d' % len(bad))
    for key, why in bad:
        print('  %-44s %s' % (key[:44], why))
    return 1


if __name__ == '__main__':
    sys.exit(main())
