# -*- coding: utf-8 -*-
"""ЧТЕНИЕ ДАННЫХ ВИДА — общее для инструментов мастерской.

Вынесено, когда один и тот же разбор ассета понадобился третий раз (аудит, разворот оси, и на
подходе сверка карты). Здесь же живёт расчёт габарита места — РОВНО такой, как в игре
(`MorphBuilder.SizeOf` и `ChainDiameter`, строки 467-520).

Почему это важнее, чем кажется. Габарит МЕСТА и габарит НАРИСОВАННЫХ В НЁМ ДЕТАЛЕЙ — разные вещи:
деталь может не заполнять своё место. Карта тел меряет второе, `attachOffset` считается в первом.
Спутать их — получить смещения, которые «почти сходятся» на одном виде и разъезжаются на другом.
"""
import re


def unesc(s):
    """Unity пишет кириллицу escape-последовательностями."""
    s = (s or '').strip().strip('"')
    return re.sub(r'\\u([0-9a-fA-F]{4})', lambda m: chr(int(m.group(1), 16)), s)


def vec(s):
    m = re.findall(r'-?[\d.]+(?:e-?\d+)?', s or '')
    return tuple(float(x) for x in m[:3]) if len(m) >= 3 else (0.0, 0.0, 0.0)


def num(d, key):
    try:
        return float(d.get(key, 0) or 0)
    except (TypeError, ValueError):
        return 0.0


def parse_asset(path):
    """Разбор YAML-ассета: (organs, sockets, bones). Формат плоский, хватает построчного чтения."""
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
    for s in sockets:
        s['_name'] = unesc(s.get('name'))
    for o in organs:
        o['_name'] = unesc(o.get('organName'))
        o['_slot'] = unesc(o.get('slot'))
    return organs, sockets, bones


def index(sockets):
    return {s['_name']: s for s in sockets}


def chain_diameter(s, by, depth=0):
    d = num(s, 'linkDiameter')
    if d > 0:
        return d
    par = by.get(unesc(s.get('parent')))
    if depth >= 16 or par is None or par is s or num(par, 'linkLength') <= 0:
        return 0.0
    taper = num(par, 'linkTaper') or 1.0
    links = max(1, int(num(par, 'chain')))
    return chain_diameter(par, by, depth + 1) * (taper ** (links - 1))


def size_of(s, by, depth=0):
    """Габарит МЕСТА — как `MorphBuilder.SizeOf`: доля родителя рекурсивно, иначе своё."""
    rel = vec(s.get('sizeRel'))
    par = by.get(unesc(s.get('parent')))
    if rel == (0.0, 0.0, 0.0) or depth >= 16 or par is None or par is s:
        if num(s, 'linkLength') <= 0:
            return vec(s.get('baseSize'))
        d = chain_diameter(s, by, depth)
        return (d, d, num(s, 'linkLength'))
    p = size_of(par, by, depth + 1)
    return (p[0] * rel[0], p[1] * rel[1], p[2] * rel[2])


def long_axis(size):
    """Индекс длинной оси: вдоль неё считается `attach` у детей. Выбирается по максимальной стороне —
    это задокументированная мина проекта: при близких числах ось молча переключается."""
    return list(size).index(max(size))


# ── ПОЗИЦИЯ И ПОВОРОТ — ПОРТ `MorphBuilder.Place` ─────────────────────────────────────────────────
# Плоская формула «центр родителя + смещение» верна только там, где нет наклонов. У ежа их нет, и она
# сходилась до нуля; у человека шея наклонена — и ошибка сразу 5 см. В игре смещение ПОВОРАЧИВАЕТСЯ
# на поворот родителя, а сам поворот наследуется вниз по ветке. Инструмент, считающий иначе, меряет
# фантом — тот же урок, что был с осями сечения в рендере.
import math                                                                    # noqa: E402


def _rx(a):
    c, s = math.cos(a), math.sin(a)
    return [[1, 0, 0], [0, c, -s], [0, s, c]]


def _ry(a):
    c, s = math.cos(a), math.sin(a)
    return [[c, 0, s], [0, 1, 0], [-s, 0, c]]


def _rz(a):
    c, s = math.cos(a), math.sin(a)
    return [[c, -s, 0], [s, c, 0], [0, 0, 1]]


def _mul(a, b):
    return [[sum(a[i][k] * b[k][j] for k in range(3)) for j in range(3)] for i in range(3)]


def _apply(m, v):
    return tuple(sum(m[i][k] * v[k] for k in range(3)) for i in range(3))


def euler(d):
    """Unity `Quaternion.Euler(x, y, z)` = Ry * Rx * Rz — тот же порядок, что в skel.py."""
    x, y, z = (math.radians(v) for v in d)
    return _mul(_mul(_ry(y), _rx(x)), _rz(z))


def transpose(m):
    return [[m[j][i] for j in range(3)] for i in range(3)]


def axis_vector(size):
    i = long_axis(size)
    return tuple(1.0 if k == i else 0.0 for k in range(3))


def place(s, by, cache=None, depth=0):
    """Мировые (в системе контейнера Morph) позиция и поворот места — как считает игра."""
    cache = {} if cache is None else cache
    if s['_name'] in cache:
        return cache[s['_name']]
    rot = euler(vec(s.get('baseEuler')))
    pos = vec(s.get('localPos'))
    par = by.get(unesc(s.get('parent')))
    if depth < 16 and par is not None and par is not s:
        ppos, prot = place(par, by, cache, depth + 1)
        b = size_of(par, by)
        ax = axis_vector(b)
        ln = abs(sum(b[i] * abs(ax[i]) for i in range(3)))
        off = vec(s.get('attachOffset'))
        att = num(s, 'attach')
        links = max(1, int(num(par, 'chain')))
        if links > 1:
            # ЦЕПЬ: доля отсчитывается от НАЧАЛА вереницы, длина — вся цепь
            taper = num(par, 'linkTaper') or 1.0
            total = sum(ln * (taper ** i) for i in range(links)) if taper != 1.0 else ln * links
            local = tuple(ax[i] * ((1.0 - att) * total) + b[i] * off[i] for i in range(3))
        else:
            local = tuple(ax[i] * ((att - 0.5) * ln) + b[i] * off[i] for i in range(3))
        moved = _apply(prot, local)
        pos = tuple(ppos[i] + moved[i] for i in range(3))
        rot = _mul(prot, rot)
    cache[s['_name']] = (pos, rot)
    return pos, rot


def matrix_to_euler(m):
    """Матрица → углы Unity (порядок Ry*Rx*Rz). Нужны для ОБРАТНОЙ задачи: при развороте оси
    поворот, который раньше наследовался сверху, приходится задать заново снизу."""
    x = math.asin(max(-1.0, min(1.0, -m[1][2])))
    if abs(m[1][2]) < 0.9999:
        z = math.atan2(m[1][0], m[1][1])
        y = math.atan2(m[0][2], m[2][2])
    else:                                   # вырожденный случай: ось смотрит вдоль, z и y сливаются
        z = 0.0
        y = math.atan2(-m[2][0], m[0][0])
    return tuple(round(math.degrees(v), 3) for v in (x, y, z))


def offset_for(child_pos, parent_pos, parent_rot, parent_size, attach, chain=1, taper=1.0):
    """attachOffset, дающий нужную мировую позицию ребёнка. Обратная к `place`: смещение задаётся
    в осях РОДИТЕЛЯ, поэтому разницу позиций сперва разворачиваем его обратным поворотом."""
    d = tuple(child_pos[i] - parent_pos[i] for i in range(3))
    local = _apply(transpose(parent_rot), d)
    ax = axis_vector(parent_size)
    ln = abs(sum(parent_size[i] * abs(ax[i]) for i in range(3)))
    if chain > 1:
        total = sum(ln * (taper ** i) for i in range(chain)) if taper != 1.0 else ln * chain
        along = (1.0 - attach) * total
    else:
        along = (attach - 0.5) * ln
    return tuple((local[i] - ax[i] * along) / parent_size[i] if parent_size[i] > 0 else 0.0
                 for i in range(3))


def euler_for(child_rot, parent_rot):
    """baseEuler ребёнка, дающий нужный мировой поворот под новым родителем."""
    return matrix_to_euler(_mul(transpose(parent_rot), child_rot))


# ── СКЕЛЕТ: порт `SkeletonBuilder.Place` ──────────────────────────────────────────────────────────
# Кость растёт по своему локальному +Y, начинается в точке `attach` вдоль родителя (от его НАЧАЛА),
# поворот наследуется. Из этого следует и обратная операция: развернуть кость значит поставить её
# начало в прежний конец и повернуть ось роста на 180 градусов.
UP = (0.0, 1.0, 0.0)


def bones_index(bones):
    out = {}
    for b in bones:
        b['_name'] = unesc(b.get('name'))
        b['_parent'] = unesc(b.get('parent'))
        out[b['_name']] = b
    return out


def bone_place(b, by, cache=None, depth=0):
    """Мировые позиция и поворот кости — как считает `SkeletonBuilder.Place`."""
    cache = {} if cache is None else cache
    if b['_name'] in cache:
        return cache[b['_name']]
    rot = euler(vec(b.get('dir')))
    pos = vec(b.get('origin'))
    par = by.get(b['_parent'])
    if depth < 16 and par is not None and par is not b:
        ppos, prot = bone_place(par, by, cache, depth + 1)
        step = _apply(prot, tuple(UP[i] * (num(par, 'length') * num(b, 'attach')) for i in range(3)))
        pos = tuple(ppos[i] + step[i] for i in range(3))
        rot = _mul(prot, rot)
    cache[b['_name']] = (pos, rot)
    return pos, rot


def bone_tip(b, pos, rot):
    d = _apply(rot, tuple(UP[i] * num(b, 'length') for i in range(3)))
    return tuple(pos[i] + d[i] for i in range(3))


def flip(rot):
    """Развернуть ось роста кости: поворот на 180 вокруг локального X даёт +Y -> -Y."""
    return _mul(rot, euler((180.0, 0.0, 0.0)))


def attach_for(child_pos, parent_pos, parent_rot, parent_len):
    """Доля вдоль родителя, дающая нужное начало ребёнка. Возвращает (attach, ошибка_поперёк):
    крепление идёт СТРОГО по оси родителя, поэтому поперечная составляющая обязана быть нулевой —
    если она велика, кость так прицепить нельзя, и число молча соврёт."""
    d = tuple(child_pos[i] - parent_pos[i] for i in range(3))
    local = _apply(transpose(parent_rot), d)
    att = local[1] / parent_len if parent_len > 0 else 0.0
    side = math.hypot(local[0], local[2])
    return att, side


def dir_for(child_rot, parent_rot):
    """dir ребёнка, дающий нужный мировой поворот под новым родителем."""
    return matrix_to_euler(_mul(transpose(parent_rot), child_rot))
