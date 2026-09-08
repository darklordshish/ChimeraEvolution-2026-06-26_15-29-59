# -*- coding: utf-8 -*-
"""МОСТ: скелет вида из модельной линии → блок `new Bone { … }` для `SpeciesBootstrap`.

Зачем инструмент. Скелеты пяти видов уже посчитаны в `Tools/Blender/species/*.py` — по анатомическим
точкам, снятым с референсов, с проверкой стыков (`from_points` ругается, если начало ребёнка не лежит
на оси родителя). В игре при этом кости есть у ОДНОГО волка: остальные четыре вида собираются
примитивами, потому что перенести числа было нечем. Это и есть весь разрыв — не «модели не готовы»,
а «готовое не доехало».

ПОЧЕМУ ПЕЧАТЬ, А НЕ ЗАПИСЬ В .asset. Данные видов живут в `SpeciesBootstrap.cs` и оттуда
пересоздаются командой — файл `.asset` производный. Впиши мы кости прямо в ассет, первая же
«Chimera → Создать дефолтные виды» их стёрла бы, и молча.

ЕДИНИЦЫ НЕ ПЕРЕВОДЯТСЯ. `chimera/skel.py` объявляет: «поля — один в один Bone.cs». Сверено: name,
parent, socket, layer, origin, attach, length, dir, r0, r1, section, depth, blend, chain, mirrorX,
endBone, endAttach — семнадцать полей совпадают по именам и смыслу. Поля `note/profile/bend/cut/shell`
остаются модельной линии: в Unity им соответствия нет и не должно быть.

Запуск:
    python tools/bones_to_cs.py wolf              # весь скелет вида
    python tools/bones_to_cs.py moose --layer 0   # только слой костей
    python tools/bones_to_cs.py human --out C:/tmp/human_bones.txt
"""
import io, os, sys, argparse

# Кодировку вывода правим ТОЛЬКО при прямом запуске (см. __main__): модуль, подменяющий stdout
# при импорте, ломает вывод тому, кто его импортирует — на этом уже потеряли прогон
HERE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))          # Anatomy/
BLENDER = os.path.join(os.path.dirname(HERE), 'Tools', 'Blender')           # модельная линия
LAYER_CS = {0: '', 1: 'BodyLayer.Muscle', 2: 'BodyLayer.Feature', 3: 'BodyLayer.Cut'}


def f(x):
    """Число в C#-литерал: короткое, но без потери третьего знака (миллиметры на калибре зверя)."""
    return ('%.3f' % float(x)).rstrip('0').rstrip('.') + 'f' if x else '0f'


def bone_cs(b):
    """Одна кость строкой. Печатаются ТОЛЬКО непустые поля: блок и так на сотню строк,
    а `attach = 1f, blend = 0f` у каждой второй кости читать невозможно."""
    p = ['name = "%s"' % b.name]
    if b.socket:              p.append('socket = "%s"' % b.socket)
    if b.parent:              p.append('parent = "%s"' % b.parent)
    if any(b.origin):         p.append('origin = new Vector3(%s, %s, %s)' % tuple(f(v) for v in b.origin))
    if abs(b.attach - 1.0) > 1e-6: p.append('attach = %s' % f(b.attach))
    p.append('length = %s' % f(b.length))
    p.append('dir = new Vector3(%s, %s, %s)' % tuple(f(v) for v in b.dir))
    p.append('r0 = %s' % f(b.r0))
    if abs(b.r1 - b.r0) > 1e-6:    p.append('r1 = %s' % f(b.r1))
    if abs(b.section - 1.0) > 1e-6: p.append('section = %s' % f(b.section))
    if abs(b.depth - 1.0) > 1e-6:   p.append('depth = %s' % f(b.depth))
    if b.blend:               p.append('blend = %s' % f(b.blend))
    if b.chain:               p.append('chain = %d' % b.chain)
    if b.mirrorX:             p.append('mirrorX = true')
    if b.endBone:             p.append('endBone = "%s"' % b.endBone)
    if b.endAttach != 1.0:    p.append('endAttach = %s' % f(b.endAttach))
    if LAYER_CS.get(b.layer): p.append('layer = %s' % LAYER_CS[b.layer])
    note = ('   // ' + b.note) if getattr(b, 'note', '') else ''
    return 'new Bone { %s },%s' % (', '.join(p), note)


def build(species, layer=None):
    if BLENDER not in sys.path:
        sys.path.insert(0, BLENDER)
    import importlib
    from chimera.skel import from_points
    m = importlib.import_module('species.' + species)
    bones, _, warn = from_points(m.DEFS, m.P, verbose=False)
    if warn:
        # НЕ МОЛЧА: предупреждение здесь значит, что точка снята неверно и стык поедет уже в игре
        print('// ВНИМАНИЕ, предупреждений от from_points: %d' % len(warn))
        for w in warn[:5]:
            print('//   ' + str(w))
    if layer is not None:
        bones = [b for b in bones if b.layer == layer]
    return bones, getattr(m, 'W', 0.0)


if __name__ == '__main__':
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
    ap = argparse.ArgumentParser()
    ap.add_argument('species')
    ap.add_argument('--layer', type=int, default=None, help='0 кости · 1 мышцы · 2 признаки · 3 резы')
    ap.add_argument('--out', default=None)
    a = ap.parse_args()

    bones, W = build(a.species, a.layer)
    lines = ['// СКЕЛЕТ ВИДА «%s» — ВЫГРУЖЕНО из Tools/Blender/species/%s.py (tools/bones_to_cs.py).'
             % (a.species, a.species),
             '// Правится ТАМ, по анатомическим точкам; здесь только результат. Костей: %d, калибр (холка) %.3f м'
             % (len(bones), W)]
    lines += [bone_cs(b) for b in bones]
    text = '\n'.join(lines) + '\n'
    if a.out:
        open(a.out, 'w', encoding='utf-8').write(text)
        print('записано: %s (%d костей)' % (a.out, len(bones)))
    else:
        print(text)


# ── НОРМАЛИЗАЦИЯ ГРАФА К КОРНЮ-ГОЛОВЕ ────────────────────────────────────────────────────────────
# Спека `2026-08-27-edinyj-plan-tela.md`: корень тела у ВСЕХ видов — голова, ось идёт назад. В игре
# волк так и стоит (корень `коробка`), а модельная линия считает осевую цепь от КРЕСТЦА — «на нём
# сходятся таз, хвост и поясница». Довод анатомически честный, но крестца нет у змеи, а голова есть
# у всех пятерых: корнем может быть только то, что есть у каждого.
#     Разворот делается ЗДЕСЬ, а не правкой модельных данных: их парадигма — дело их линии, а игре
# нужен единый план. Расхождение записано в `Docs/models/SPEC-telo-kak-graf.md`, чтобы не стало миной.
#
# ПОЧЕМУ ЭТО ДЁШЕВО. Кость задана двумя ТОЧКАМИ (a→b), а углы и attach считает `from_points`.
# Значит развернуть цепь = поменять концы местами и переставить родительство; ни одного угла руками.
AXIAL_SOCKETS = {'хребет', 'шея', 'голова'}


def _axial_chain(defs):
    """Осевая цепь от корня до черепа: список имён в ТЕКУЩЕМ порядке (от хвоста к голове)."""
    # ТОЛЬКО СЛОЙ КОСТЕЙ И ТОЛЬКО ЗАДАННЫЕ ДВУМЯ ТОЧКАМИ: на хребте висят и мышцы (брюшина), а они
    # описаны иначе — развернуть их перестановкой концов нельзя, да и незачем: ось несёт скелет
    def axial(d):
        return (d.get('socket') in AXIAL_SOCKETS and d.get('layer', 0) == 0
                and 'a' in d and 'b' in d)

    by = {d['name']: d for d in defs}
    kids = {}
    for d in defs:
        if axial(d):
            kids.setdefault(d.get('parent', ''), []).append(d['name'])
    roots = [d['name'] for d in defs if axial(d) and not d.get('parent')]
    if not roots:
        return []
    chain, cur = [], roots[0]
    while cur:
        chain.append(cur)
        nxt = [k for k in kids.get(cur, []) if axial(by[k])]
        cur = nxt[0] if nxt else None
    return chain


def reroot_to_head(defs, P, strict=True):
    """Вернуть копию defs с корнем-головой. Геометрия обязана остаться прежней.

    СОСТОЯНИЕ: РАБОТАЕТ НЕ ДЛЯ ВСЕХ КОСТЕЙ, и потому по умолчанию отказывается отдавать результат.
    Кости описаны ТРЕМЯ способами, и разворот у каждого свой:
      • `a`/`b` — две точки: перестановка концов, углы пересчитает `from_points`. Это работает;
      • `at`+`d` — доля вдоль родителя плюс вектор в ЕГО локальной системе (остистые отростки,
        рёбра): при развороте родителя переворачивается и доля, и система координат вектора;
      • `end` — натяжение между двумя костями (мышцы): следует за обоими концами сразу.
    На волке второй и третий случай дают 87 расхождений из 130 — то есть скелет разъезжается.

    ПОЧЕМУ НЕ ДОДЕЛЫВАЮ ЗДЕСЬ. Это не постобработка, а смена парадигмы данных: осевую цепь надо
    ОПИСАТЬ от головы в `Tools/Blender/species/*.py`, где у каждого числа есть анатомический смысл
    и референс. Пересчёт вслепую поверх чужих данных — ровно та подгонка, которую проект запрещает.
    Правильный адрес работы — модельная линия; требование записано в `SPEC-telo-kak-graf.md`.
    """
    import copy
    out = copy.deepcopy(defs)
    by = {d['name']: d for d in out}
    chain = _axial_chain(out)
    if len(chain) < 2:
        return out, chain

    # 1) ЦЕПЬ РАЗВОРАЧИВАЕТСЯ: каждая кость смотрит в обратную сторону, родителем становится сосед
    #    со стороны головы. Голова (последняя в старом порядке) остаётся без родителя — это новый корень
    rev = list(reversed(chain))
    for i, name in enumerate(rev):
        d = by[name]
        d['a'], d['b'] = d['b'], d['a']          # кость та же, описана с другого конца
        d['parent'] = rev[i - 1] if i else ''    # первый в новом порядке — корень

    # 2) ДЕТИ ЦЕПИ ЕДУТ ЗА РОДИТЕЛЕМ. Кость, висевшая на осевом звене, крепилась к его КОНЦУ; после
    #    разворота этот конец стал началом, и attach обязан перевернуться, иначе рёбра съедут на позвонок
    inchain = set(chain)
    for d in out:
        if d['name'] in inchain or d.get('parent') not in inchain:
            continue
        if 'attach' in d:
            d['attach'] = round(1.0 - float(d['attach']), 4)

    # 3) ПОРЯДОК В СПИСКЕ: `from_points` требует, чтобы кость шла ПОСЛЕ тех, на кого ссылается, и
    #    падает иначе. Ссылок две: `parent` (чей я ребёнок) и `endBone` (к чьему концу тянусь —
    #    так заданы мышцы). Разворот поменял направление стрелок, порядок строк остался прежним,
    #    поэтому пересобираем его сортировкой по ОБЕИМ зависимостям сразу.
    #        Заодно это проверка целостности: не разобранное к концу — цикл или ссылка в пустоту.
    names = {d['name'] for d in out}
    def refs(d):
        # `parent` — чей я ребёнок; `end` — к чьему концу тянусь (так заданы мышцы, ключ в DEFS
        # называется `end` и хранит пару «кость, доля вдоль неё», а не голое имя)
        r = {d.get('parent')}
        e = d.get('end')
        if isinstance(e, (tuple, list)) and e:
            r.add(e[0])
        return {x for x in r if x and x in names}

    deps = {d['name']: refs(d) for d in out}
    ordered, done = [], set()
    while len(ordered) < len(out):
        ready = [d for d in out if d['name'] not in done and deps[d['name']] <= done]
        if not ready:
            stuck = [d['name'] for d in out if d['name'] not in done]
            raise ValueError('порядок не строится для %d костей (цикл или ссылка в пустоту): %s'
                             % (len(stuck), ', '.join(stuck[:6])))
        for d in ready:
            ordered.append(d)
            done.add(d['name'])

    # 4) САМОПРОВЕРКА: кость та же, описана с другого конца — значит пара «начало, конец» обязана
    #    совпасть с исходной. Инструмент, молча отдающий разъехавшийся скелет, хуже отсутствующего
    if strict:
        from chimera.skel import from_points
        _, o0, _ = from_points(defs, P, verbose=False)
        _, o1, _ = from_points(ordered, P, verbose=False)

        def ends(o):
            return {tuple(round(v, 4) for v in o[0]), tuple(round(v, 4) for v in o[2])}

        moved = [n for n in o0 if n not in o1 or ends(o0[n]) != ends(o1[n])]
        if moved:
            raise ValueError(
                'разворот сдвинул %d костей из %d — данные описаны не только парами точек '
                '(см. докстроку). Первые: %s' % (len(moved), len(o0), ', '.join(moved[:6])))
    return ordered, rev
