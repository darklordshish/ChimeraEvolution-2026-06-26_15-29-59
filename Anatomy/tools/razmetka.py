# -*- coding: utf-8 -*-
"""РАЗМЕТКА ЦЕПЕЙ — смысл узлов графа для соответствия тел разных видов (спека `2026-09-27-smeshenie-siluetnogo-grafa.md`
§4.1, формат — письмо механик `Docs/models/FEEDBACK-2026-09-29c-format-razmetki.md`).

У каждого узла: `limb` — цепь (`хребет`, `шея`, `голова`, `хвост`, `перед`, `зад`; сторону даёт `mirrorX`) и `mark` —
суставы-метки на концах узла, `{"a": …, "b": …}`, любой может отсутствовать. Холку и крестец НЕ пишем: механика берёт
проекцию начала первого узла передней/задней цепи на ось хребта. Бугры и сечения лофта — `limb` родителя, без метки.

МЕТКИ — МЕСТА РАЗРЕЗА (письмо 29e): аугмент вынимает участок графа носителя от метки и вставляет свою цепь. Поэтому метка
стоит там, где чужая часть насаживается на тело: `плечо` — плечевой сустав (конец лопатки), `бедро` — тазобедренный, а не
середина мышцы. По гомологии: скакательный волка = щиколотка человека (решение 29c).

Таблица вида — `{узел: (limb, метка a, метка b)}` в его `graph.py`; `apply` вписывает её в кости и проверяет:
каждый узел размечен, метки из словаря, каждая стоит не больше одного раза, у бугра (узла без метки) цепь родителя."""

LIMBS = ('хребет', 'шея', 'голова', 'хвост', 'перед', 'зад')
MARKS = ('плечо', 'локоть', 'запястье', 'бедро', 'колено', 'скакательный',
         'основание шеи', 'основание черепа', 'корень хвоста')


def apply(bones, table, species):
    by = {b['name']: b for b in bones}
    missing = [n for n in by if n not in table]
    extra = [n for n in table if n not in by]
    if missing or extra:
        raise SystemExit('%s: разметка не сходится с графом — нет разметки у %s, лишние %s' % (species, missing, extra))
    seen = {}
    for b in bones:
        limb, ma, mb = table[b['name']]
        if limb not in LIMBS:
            raise SystemExit('%s: у «%s» цепь «%s» не из словаря %s' % (species, b['name'], limb, LIMBS))
        mark = {}
        for end, m in (('a', ma), ('b', mb)):
            if m is None:
                continue
            if m not in MARKS:
                raise SystemExit('%s: у «%s» метка «%s» не из словаря %s' % (species, b['name'], m, MARKS))
            if m in seen:
                raise SystemExit('%s: метка «%s» дважды — у «%s» и «%s»' % (species, m, seen[m], b['name']))
            seen[m] = b['name']
            mark[end] = m
        # НАЧАЛО ЦЕПИ без своей метки законно в двух случаях: сустав стоит на конце родителя (голова после
        # `основание черепа` шеи) или это конечность — её холку/крестец берёт проекцией механика. Иначе это бугор
        # в чужой цепи — ошибка разметки
        parent = table.get(b['parent'])
        if not mark and parent and parent[0] != limb and parent[2] is None and limb not in ('перед', 'зад'):
            raise SystemExit('%s: бугор «%s» в цепи «%s», а родитель «%s» — в «%s»'
                             % (species, b['name'], limb, b['parent'], parent[0]))
        b['limb'] = limb
        b['mark'] = mark
    return sorted(seen)


def rel_all(bones):
    """УЗЛЫ В ДОЛЯХ РОДИТЕЛЯ (поставка 30, формат механик 29g): бугор, сечение лофта, масса туши — узел без метки в цепи
    родителя — пишется `rel` ВМЕСТО метровых `origin`, `length`, `r0`, `r1`; метры выводит `BodyChains.ResolveRel`.
    Доли считаются от метров родителя ДО того, как у него самого метры заменятся (сечения на сечениях)."""
    import otnositelno as O
    by = {b['name']: b for b in bones}
    rels = {}
    for b in bones:
        if O.is_relative(b, by):
            q = O.to_rel(b, by[b['parent']])
            rels[b['name']] = dict(at=q['at'], offX=q['off'][0], offZ=q['off'][1], len=q['len'], r0=q['r'][0], r1=q['r'][1])
    for b in bones:
        if b['name'] in rels:
            for k in ('origin', 'length', 'r0', 'r1'):
                b.pop(k, None)
            b['rel'] = rels[b['name']]
    return len(rels)
