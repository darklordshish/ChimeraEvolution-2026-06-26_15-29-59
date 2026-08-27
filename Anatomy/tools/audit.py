# -*- coding: utf-8 -*-
"""АУДИТ ДАННЫХ ВИДОВ — детектор мин по ассетам, без запуска Unity.

Зачем. Волка вылизывали неделями, остальные четыре вида столько внимания не получали. Все известные
мины проекта молчаливы: ошибок в консоли нет, просто силуэт врёт, и ловится это глазами через три
итерации подгонки. Здесь они проверяются по данным разом.

Как. Габарит места считается РОВНО ТЕМ ЖЕ способом, что в игре: `SizeOf` (доля родителя рекурсивно)
и `ChainDiameter` (диаметр наследуется по цепи) воспроизводят `MorphBuilder`, строки 467-520.
Проверять данные своей формулой — значит мерить фантом.

Запуск:  python audit.py [--species Волк]
"""
import argparse
import glob
import os
import re
import sys

AP = argparse.ArgumentParser()
AP.add_argument('--species', default='', help='проверить один вид')
AP.add_argument('--data', default='', help='другая папка с ассетами (для самопроверки детектора)')
A = AP.parse_args()

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
DATA = A.data or os.path.join(ROOT, 'Assets', '_Chimera', 'Data')
SLOTS_CS = os.path.join(ROOT, 'Assets', '_Chimera', 'Scripts', 'Player', 'BodySlots.cs')


def unesc(s):
    """Unity пишет кириллицу escape-последовательностями — возвращаем читаемый вид."""
    s = (s or '').strip().strip('"')
    return re.sub(r'\\u([0-9a-fA-F]{4})', lambda m: chr(int(m.group(1), 16)), s)


def vec(s):
    m = re.findall(r'-?[\d.]+(?:e-?\d+)?', s or '')
    return tuple(float(x) for x in m[:3]) if len(m) >= 3 else (0.0, 0.0, 0.0)


def load_dictionary():
    t = open(SLOTS_CS, encoding='utf-8').read()
    slots = set(re.findall(r'public const string \w+ = "([^"]+)"', t))
    places = set(re.findall(r'"([^"]+)"', t.split('Places = new[]')[1].split('};')[0]))
    return slots, places


def parse_asset(path):
    """Разбор YAML-ассета: формат плоский, хватает построчного чтения."""
    organs, sockets, bones, cur, mode = [], [], [], None, None
    for ln in open(path, encoding='utf-8').read().split('\n'):
        head = re.match(r'^  (\w+):', ln)
        if head:
            mode = {'organs': 'o', 'sockets': 's', 'bones': 'b'}.get(head.group(1))
            cur = None
            continue
        if mode is None:
            continue
        m = re.match(r'^  - (\w+): ?(.*)$', ln)
        if m:
            cur = {m.group(1): m.group(2)}
            {'o': organs, 's': sockets, 'b': bones}[mode].append(cur)
            continue
        m = re.match(r'^    (\w+): ?(.*)$', ln)
        if m and cur is not None:
            cur[m.group(1)] = m.group(2)
    return organs, sockets, bones


def num(d, key):
    try:
        return float(d.get(key, 0) or 0)
    except (TypeError, ValueError):
        return 0.0


def audit(path, slots, places):
    name = unesc(os.path.basename(path).replace('.asset', ''))
    if A.species and A.species != name:
        return 0, 0
    organs, sockets, bones = parse_asset(path)
    bone_names = set(unesc(b.get('name')) for b in bones)
    by = {}
    for s in sockets:
        s['_name'] = unesc(s.get('name'))
        by[s['_name']] = s
    for o in organs:
        o['_name'] = unesc(o.get('organName'))
        o['_slot'] = unesc(o.get('slot'))

    issues = []

    def bad(where, text):
        issues.append((where, text))

    def parent_of(s):
        return by.get(unesc(s.get('parent')))

    # ── ГАБАРИТ КАК В ИГРЕ ────────────────────────────────────────────────────────────────────────
    def chain_diameter(s, depth=0):
        d = num(s, 'linkDiameter')
        if d > 0:
            return d
        par = parent_of(s)
        if depth >= 16 or par is None or par is s or num(par, 'linkLength') <= 0:
            return 0.0
        taper = num(par, 'linkTaper') or 1.0
        links = max(1, int(num(par, 'chain')))
        return chain_diameter(par, depth + 1) * (taper ** (links - 1))

    def raw(s, depth=0):
        if num(s, 'linkLength') <= 0:
            return vec(s.get('baseSize'))
        d = chain_diameter(s, depth)
        return (d, d, num(s, 'linkLength'))

    def size_of(s, depth=0):
        rel = vec(s.get('sizeRel'))
        par = parent_of(s)
        if rel == (0.0, 0.0, 0.0) or depth >= 16 or par is None or par is s:
            return raw(s, depth)
        p = size_of(par, depth + 1)
        return (p[0] * rel[0], p[1] * rel[1], p[2] * rel[2])

    # 1. СЛОВАРЬ СЛОТОВ
    for o in organs:
        if o['_slot'] in places:
            bad(o['_name'], 'орган на ТЕЛЕСНОМ МЕСТЕ «%s» — такого слота нет' % o['_slot'])
        elif o['_slot'] and o['_slot'] not in slots:
            bad(o['_name'], 'слот «%s» вне словаря BodySlots' % o['_slot'])
    for s in sockets:
        if s['_name'] not in slots and s['_name'] not in places:
            bad(s['_name'], 'имя места вне словаря BodySlots')

    # 2. ГРАФ
    roots = [s['_name'] for s in sockets if not unesc(s.get('parent'))]
    for s in sockets:
        p = unesc(s.get('parent'))
        if p and p not in by and p not in bone_names:
            bad(s['_name'], 'родитель «%s» не существует ни местом, ни костью' % p)
    if len(roots) != 1:
        bad('(граф)', 'корней %d: %s' % (len(roots), ', '.join(roots) or '—'))
    for s in sockets:
        seen, cur, guard = {s['_name']}, s, 0
        while guard < 32:
            p = parent_of(cur)
            if p is None:
                break
            if p['_name'] in seen:
                bad(s['_name'], 'ЦИКЛ через «%s»' % p['_name'])
                break
            seen.add(p['_name'])
            cur = p
            guard += 1

    # 3. ЗАПАС ДЛИННОЙ ОСИ — мина: ось выбирается по максимальной стороне и при близких числах
    #    молча переключается, разворачивая ВСЮ ветку детей (человек: шея 0.128Y при 0.132Z)
    for s in sockets:
        if num(s, 'linkLength') > 0:
            continue                      # цепь: ось = длина звена, толщины равны по построению
        if not any(unesc(x.get('parent')) == s['_name'] for x in sockets):
            continue
        b = sorted(size_of(s), reverse=True)
        if b[0] <= 0:
            continue
        if b[0] / max(1e-6, b[2]) - 1 < 0.05:      # изотропный: длинной оси нет по смыслу
            continue
        if b[1] > 0 and b[0] / b[1] - 1 < 0.05:
            bad(s['_name'], 'запас длинной оси %.1f%% (%.3f против %.3f)'
                % ((b[0] / b[1] - 1) * 100, b[0], b[1]))

    # 4. НУЛЕВОЙ КАЛИБР — деталь схлопнется в плоскость без единой ошибки
    for s in sockets:
        sz = size_of(s)
        if min(sz) <= 0:
            bad(s['_name'], 'нулевой калибр (%.3f, %.3f, %.3f)' % sz)

    # 5. ДОЛЯ РАЗОШЛАСЬ С ЗАПИСАННЫМ ГАБАРИТОМ. `baseSize` при заданной доле не читается игрой, но по
    #    договорённости (УСТРОЙСТВО_ТЕЛА §5.1) синхронизирован с ней и служит ДОКУМЕНТАЦИЕЙ. Значит
    #    расхождение — сигнал, и ловит он ровно тот класс ошибки, на котором я попался сам: пересадив
    #    голову ежа с хребта на шею, оставил прежнюю долю — и голова схлопнулась вчетверо вместе с
    #    пастью, носом, глазами и ушами. В игре ни ошибки, ни предупреждения
    for s in sockets:
        rel = vec(s.get('sizeRel'))
        base = vec(s.get('baseSize'))
        # ЕДИНИЦА — ЭТО ДЕФОЛТ ПОЛЯ (`baseSize = Vector3.one`), а не записанный габарит: у мест,
        # живущих целиком на доле (нос, Чутьё, ямки), его никто не заполнял. Сравнивать с ним
        # значит ругаться на норму у каждого вида
        if rel == (0.0, 0.0, 0.0) or min(base) <= 0 or num(s, 'linkLength') > 0:
            continue
        if base == (1.0, 1.0, 1.0):
            continue
        got = size_of(s)
        worst = max(abs(got[i] / base[i] - 1) for i in range(3))
        if worst > 0.25:
            bad(s['_name'], 'доля даёт %.3f x %.3f x %.3f, а записано %.3f x %.3f x %.3f (расхождение %.0f%%) — '
                            'проверь, не сменился ли родитель' % (got + base + (worst * 100,)))

    # 6. ОРГАН БЕЗ МЕСТА — механика работает, видно ничего не будет
    for o in organs:
        if o['_slot'] and o['_slot'] not in by:
            bad(o['_name'], 'нет места под слот «%s» — орган невидим' % o['_slot'])

    # 7. МЕТРЫ У ДОНОРА — правило спеки морфологии по идентичности
    for o in organs:
        off = vec(o.get('visualOffset'))
        if off != (0.0, 0.0, 0.0):
            bad(o['_name'], 'visualOffset в МЕТРАХ %s — донорские данные обязаны быть в долях' % (off,))

    # 8. МЕСТО БЕЗ ФОРМЫ — фикция. Пока хребет был служебным, несущую анатомию рисовал ПОКРОВ,
    #    и шесть мест из десяти висели на пустоте. Флага «не рисовать никогда» больше нет
    slots_worn = set(o['_slot'] for o in organs)
    hides = set()
    txt = open(path, encoding='utf-8').read()
    if 'skeletonHides:' in txt:
        block = txt.split('skeletonHides:')[1].split(chr(10) + '  ')[0]
        hides = set(unesc(x) for x in re.findall(r'- (.+)', block))
    for s2 in sockets:
        if s2['_name'] in hides:
            continue                                   # форму даёт скелет
        # ПУСТОЕ значение у `parts` значит БЛОК С СОДЕРЖИМЫМ (элементы идут ниже своими строками),
        # а «[]» — что частей нет. Спутать их значит объявить фикцией каждое место с формой
        has_parts = (s2.get('parts') or '').strip() != '[]'
        if has_parts or num(s2, 'linkLength') > 0:
            continue
        if s2['_name'] in slots_worn or num(s2, 'inner') > 0 or num(s2, 'graft') > 0:
            continue
        if unesc(s2.get('formFrom')):
            continue
        bad(s2['_name'], 'место без формы: ни частей, ни органа, ни цепи — фикция')

    # 9. ДУБЛИ ИМЁН МЕСТ
    count = {}
    for s in sockets:
        count[s['_name']] = count.get(s['_name'], 0) + 1
    for n, c in count.items():
        if c > 1:
            bad(n, 'место объявлено %d раза — имя это адрес' % c)

    bones = len(re.findall(r'^  - name:', open(path, encoding='utf-8').read(), re.M))
    print('\n== %s ==  органов %d · мест %d · корень: %s'
          % (name, len(organs), len(sockets), ', '.join(roots) or '—'))
    if not issues:
        print('   чисто')
    for w, t in issues:
        print('   %-16s %s' % (w[:16], t))
    return len(issues), len(sockets)


def main():
    slots, places = load_dictionary()
    print('словарь: %d слотов, %d телесных мест' % (len(slots), len(places)))
    total = 0
    for p in sorted(glob.glob(os.path.join(DATA, '*.asset'))):
        n, _ = audit(p, slots, places)
        total += n
    print('\nВСЕГО ЗАМЕЧАНИЙ: %d' % total)
    return 0


sys.exit(main())
