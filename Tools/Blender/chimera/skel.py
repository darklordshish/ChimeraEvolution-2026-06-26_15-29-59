# -*- coding: utf-8 -*-
"""КОСТЬ В КОНВЕНЦИИ ИГРЫ — общая для Blender и для проверок под обычным Python.

Модуль намеренно НЕ импортирует `bpy`: расстановка костей должна считаться и без Blender, иначе
проверить её нечем, кроме как открыв редактор. Та же арифметика лежит в игре
(`Assets/_Chimera/Scripts/Player/SkeletonBuilder.cs`), и расходиться им нельзя — иначе модель
покажет одно, а игра построит другое.

КОНВЕНЦИЯ (повторяет Unity дословно):
  • кость растёт по СВОЕМУ +Y от начала к концу;
  • `Quaternion.Euler(x,y,z)` = Ry·Rx·Rz — порядок ZXY;
  • ребёнок стартует в точке `attach` вдоль родителя и НАСЛЕДУЕТ его поворот;
  • у корня цепи есть `origin` — метры от земли; у остальных своих координат нет по построению.
"""
import math

# ── МАТРИЦЫ 3×3 НА СПИСКАХ. numpy в Blender есть, но модуль зовётся и из скриптов проверки,
# где тянуть зависимость незачем: арифметики здесь на двадцать строк
def _mul(A, B):
    return [[sum(A[i][k] * B[k][j] for k in range(3)) for j in range(3)] for i in range(3)]

def _mv(A, v):
    return [sum(A[i][k] * v[k] for k in range(3)) for i in range(3)]

def Rx(a):
    c, s = math.cos(a), math.sin(a); return [[1, 0, 0], [0, c, -s], [0, s, c]]

def Ry(a):
    c, s = math.cos(a), math.sin(a); return [[c, 0, s], [0, 1, 0], [-s, 0, c]]

def Rz(a):
    c, s = math.cos(a), math.sin(a); return [[c, -s, 0], [s, c, 0], [0, 0, 1]]

def euler(d):
    """Углы в градусах → матрица. Порядок ZXY, как `Quaternion.Euler` в Unity."""
    x, y, z = (math.radians(v) for v in d)
    return _mul(_mul(Ry(y), Rx(x)), Rz(z))

IDENT = [[1, 0, 0], [0, 1, 0], [0, 0, 1]]


# ── СЛОИ ТЕЛА. Порядок сборки И порядок приёмки: слой не начинается, пока предыдущий не принят
SKELETON, MUSCLE, FEATURE, CUT = 0, 1, 2, 3
LAYER_NAME = {SKELETON: 'кости', MUSCLE: 'мышцы', FEATURE: 'признаки', CUT: 'резы'}


class Bone:
    """Поля — один в один `Bone.cs`, чтобы данные уезжали в `SpeciesSO` без перевода.

    `r0`/`r1` — толщина у начала и у конца. РАЗНЫЕ радиусы дают веретено вместо капсулы; равные
    возвращают ту самую «шариковость», ради ухода от которой менялось ядро.
    `section` — множитель радиуса ПОПЕРЁК (уже/шире), `depth` — ВДОЛЬ ВЗГЛЯДА (тоньше/толще).
    Двух множителей мало для чего угодно, но ровно их хватает: ухо плоское по толщине, грудь узкая
    по ширине, и одним числом эти два случая не различить."""

    __slots__ = ('name', 'parent', 'socket', 'layer', 'origin', 'attach', 'length', 'dir',
                 'r0', 'r1', 'section', 'depth', 'blend', 'chain', 'mirrorX',
                 'endBone', 'endAttach', 'note', 'profile', 'bend', 'cut', 'shell')

    def __init__(self, name, parent='', socket='', layer=SKELETON, origin=(0.0, 0.0, 0.0),
                 attach=1.0, length=0.1, dir=(0.0, 0.0, 0.0), r0=0.05, r1=None,
                 section=1.0, depth=1.0, blend=0.0, chain=0, mirrorX=False,
                 endBone='', endAttach=1.0, note=''):
        self.name, self.parent, self.socket, self.layer = name, parent, socket, layer
        self.origin, self.attach, self.length, self.dir = tuple(origin), attach, length, tuple(dir)
        self.r0 = r0
        self.r1 = r0 if r1 is None else r1
        self.section, self.depth, self.blend = section, depth, blend
        self.chain, self.mirrorX = chain, mirrorX
        self.endBone, self.endAttach = endBone, endAttach
        self.profile, self.bend = 'long', None   # чем обрастает ось и есть ли прогиб
        # ВЫЧИТАНИЕ — ВИД ВКЛАДА, А НЕ СТАДИЯ СБОРКИ. Глазница и височная яма принадлежат СЛОЮ
        # КОСТЕЙ: без них череп — гладкий батон, сколько объёмов на него ни налепи. Щель нельзя
        # изобразить наростом, её можно только прорезать, и это верно на каждом слое, а не на
        # последнем. Поэтому признак живёт отдельно от `layer`
        self.cut = False
        # ОБОЛОЧКА ПО СТАНЦИЯМ вместо оси с радиусом. Труба описывает кость и мышцу, но не череп:
        # у черепа свод, яма и дуга, и приблизить их набором капсул нельзя в принципе. Станции
        # (верх, низ, полуширина в долях длины) СНИМАЮТСЯ С ДВУХ ОРТОГРАФИЙ и дают настоящую форму
        self.shell = None
        self.note = note          # зачем эта кость нужна — читается в отчёте, в игру не едет


def place(bones):
    """Позиция и поворот каждой кости. Возвращает {имя: (начало, матрица, конец)}.

    ИНВАРИАНТ СУСТАВА: начало ребёнка ЕСТЬ точка на родителе, отдельной координаты у него нет.
    Поэтому «деталь висит в воздухе» и «уступ на стыке» здесь невозможны не как редкий случай,
    а по построению — их нечем задать."""
    by = {}
    for b in bones:
        if b.name in by:
            raise ValueError('ДУБЛЬ ИМЕНИ КОСТИ: %r. Имя — адрес: при повторе словарь молча оставит '
                             'последнюю, а дети первой уедут в чужое место' % b.name)
        by[b.name] = b

    done = {}

    def go(b, depth=0):
        if b.name in done:
            return done[b.name]
        rot = euler(b.dir)
        pos = list(b.origin)
        if b.parent and depth < 24:
            par = by.get(b.parent)
            if par is None:
                raise ValueError('кость %r ссылается на несуществующего родителя %r' % (b.name, b.parent))
            ppos, prot, _ = go(par, depth + 1)
            step = _mv(prot, [0.0, par.length * b.attach, 0.0])
            pos = [ppos[i] + step[i] for i in range(3)]
            rot = _mul(prot, rot)
        elif b.parent:
            raise ValueError('цикл в родстве костей у %r' % b.name)
        tip = [pos[i] + _mv(rot, [0.0, b.length, 0.0])[i] for i in range(3)]
        done[b.name] = (pos, rot, tip)
        return done[b.name]

    for b in bones:
        go(b)
    return by, done


def aim(parent_rot, a, b):
    """Обратная задача: какой `dir` и `length` нужны, чтобы кость из точки `a` смотрела в `b`.

    Так данные задаются АНАТОМИЧЕСКИМИ ТОЧКАМИ (сустав → сустав), которые снимаются с референса,
    а углы считает машина. Задавать углы руками — значит подгонять то, чего на картинке нет."""
    v = [b[i] - a[i] for i in range(3)]
    L = math.sqrt(sum(c * c for c in v))
    if L < 1e-9:
        return (0.0, 0.0, 0.0), 0.0
    inv = [[parent_rot[j][i] for j in range(3)] for i in range(3)]   # поворот ортогонален: обратная = транспонированная
    vl = _mv(inv, [c / L for c in v])
    z = -math.degrees(math.asin(max(-1.0, min(1.0, vl[0]))))
    x = math.degrees(math.atan2(vl[2], vl[1]))
    return (round(x, 2), 0.0, round(z, 2)), round(L, 4)


# ── СБОРКА ПО АНАТОМИЧЕСКИМ ТОЧКАМ ────────────────────────────────────────────────────────────────
def from_points(defs, P, verbose=True):
    """Список описаний «кость от точки A до точки B» → кости с посчитанными углами и длинами.

    ЗАЧЕМ ТАК. Углы и `attach` руками не задаются: их на референсе не видно, видно СУСТАВЫ. Машина
    считает то, что выводимо, человек снимает то, что видно, — и подгонять углы становится нечем.

    ЧТО ЭТО ЛОВИТ. `attach` вычисляется проекцией начала кости на ось родителя, а перпендикулярный
    остаток печатается предупреждением: если начало ребёнка НЕ лежит на родителе, значит одна из
    двух точек снята неверно. Прежде такое всплывало на скриншоте как «деталь оторвалась» и стоило
    итераций гадания; здесь это число до сборки."""
    bones, out, warn, by_name = [], {}, [], {}
    for d in defs:
        pname = d.get('parent', '')
        # НАЧАЛО кости: либо именованный сустав, либо доля вдоль родителя. Второе нужно рёбрам и
        # остистым — они растут из ТОЧКИ НА ПОЗВОНКЕ, у которой своего имени в анатомии нет
        if 'at' in d:
            if not pname:
                raise ValueError('%r: `at` без родителя' % d['name'])
            ppos, prot, _ = out[pname]
            plen = bones[[b.name for b in bones].index(pname)].length
            step = _mv(prot, [0.0, plen * d['at'], 0.0])
            A = tuple(ppos[i] + step[i] for i in range(3))
        else:
            A = P[d['a']]
        # КОНЕЦ. Три способа, и третий — про мышцу.
        #   `b`   — именованный сустав;
        #   `d`   — смещение в мировых осях (рёбра, отростки);
        #   `end` — ВТОРОЕ КРЕПЛЕНИЕ на другой кости: («имя кости», доля вдоль неё).
        # У мышцы длину и угол НЕ ЗАДАЮТ: она натянута между двумя точками и следует за обеими.
        # Пока мясо лепилось костями, оно висело на одном креплении — в анимации такая «мышца»
        # поехала бы жёстко, а в данных её было не отличить от анатомии
        if 'end' in d:
            en, ef = d['end']
            if en not in out:
                raise ValueError('%r: мышца крепится к %r, а та ещё не поставлена' % (d['name'], en))
            epos, erot, _ = out[en]
            elen = by_name[en].length
            step3 = _mv(erot, [0.0, elen * ef, 0.0])
            B = tuple(epos[i] + step3[i] for i in range(3))
        elif 'd' in d:
            B = tuple(A[i] + d['d'][i] for i in range(3))
        else:
            B = P[d['b']]
        if pname:
            if pname not in out:
                raise ValueError('кость %r объявлена раньше родителя %r' % (d['name'], pname))
            ppos, prot, _ = out[pname]
            plen = bones[[b.name for b in bones].index(pname)].length
            v = [A[i] - ppos[i] for i in range(3)]
            axis = _mv(prot, [0.0, 1.0, 0.0])
            along = sum(v[i] * axis[i] for i in range(3))
            perp = math.sqrt(max(0.0, sum(c * c for c in v) - along * along))
            attach = along / plen if plen > 1e-9 else 1.0
            # `lat` — крепление НА БОКУ родителя, а не в суставе: так сидят скуловая дуга и лопатка.
            # Без этой пометки сторож кричал бы на намеренное, а валидатор, кричащий на намеренное,
            # приучает игнорировать красное
            if perp > 0.006 and not d.get('lat'):
                warn.append('  %-16s начало в %.1f мм от оси родителя %r — промер одной из точек врёт'
                            % (d['name'], perp * 1000, pname))
            if attach < -0.02 or attach > 1.35:
                warn.append('  %-16s attach %.2f вне кости-родителя %r' % (d['name'], attach, pname))
            # ЦЕЛИТЬСЯ НАДО ИЗ ТОЙ ЖЕ ТОЧКИ, В КОТОРОЙ КОСТЬ ОКАЖЕТСЯ. Начало ребёнка задаёт РОДИТЕЛЬ
            # (сустав общий — в этом весь смысл), поэтому угол считается от проекции на его ось, а не
            # от снятой точки. Иначе боковое крепление уводит НЕ саму кость, а всю ветку под ней:
            # лопатка вставала на 55 мм внутрь, и за ней уезжали плечо, локоть, запястье и лапа
            step2 = _mv(prot, [0.0, plen * attach, 0.0])
            A = tuple(ppos[i] + step2[i] for i in range(3))
            dr, L = aim(prot, A, B)
        else:
            attach, (dr, L) = 1.0, aim(IDENT, A, B)

        b = Bone(d['name'], pname, d.get('socket', ''), d.get('layer', SKELETON),
                 origin=(0, 0, 0) if pname else A, attach=attach, length=L, dir=dr,
                 r0=d.get('r0', 0.03), r1=d.get('r1'), section=d.get('section', 1.0),
                 depth=d.get('depth', 1.0), chain=d.get('chain', 0),
                 mirrorX=d.get('mirrorX', False), endBone=d.get('endBone', ''),
                 endAttach=d.get('endAttach', 1.0), note=d.get('note', ''))
        b.profile = d.get('profile', 'long')
        b.bend = d.get('bend')
        b.cut = bool(d.get('cut'))
        b.shell = d.get('shell')
        bones.append(b)
        by_name[b.name] = b
        _, out2 = place(bones)
        out = out2
    if verbose and warn:
        print('ПРОМЕР — РАСХОЖДЕНИЯ (%d):' % len(warn))
        print('\n'.join(warn))
    return bones, out, warn
