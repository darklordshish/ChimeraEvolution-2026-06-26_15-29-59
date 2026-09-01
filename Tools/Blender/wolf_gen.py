"""
CHIMERA — генератор low-poly волка (шарнирная кукла из частей).

Философия та же, что у Editor-генераторов префабов: модель НЕ лепится руками, а
пересобирается скриптом из чисел. Хочешь длиннее морду — правишь одно число в
блоке TUNING ниже и гоняешь заново.

Запуск (из папки Tools/Blender):
    blender --background --python wolf_gen.py -- --render

Что делает:
  * строит 24 отдельных меша (голова, челюсть, уши, шея, грудь, круп, хвост×3, ноги)
  * пивот каждой части — В СУСТАВЕ (проксимальный конец), не в центре
  * строит скелет из 24 костей; каждая часть жёстко висит на своей кости (rigid,
    без весов) — «шарнирная кукла»
  * сохраняет Wolf.blend и (с --render) четыре превью + силуэт

Оси: Blender Z-вверх, метры. Морда смотрит в -Y → при экспорте FBX
(-Z Forward, Y Up) в Unity станет +Z. Числа наследованы из WolfPrefab.cs,
пересчёт Unity(x,y,z) → Blender(x, -z, y).
"""

import bpy
import bmesh
import datetime
import math
import os
import shutil
import sys
from mathutils import Vector, Matrix

# =============================================================================
#  TUNING — вся форма волка живёт здесь
# =============================================================================

# --- Материалы --------------------------------------------------------------
# Текстур нет и не планируется: цвет несут отдельные материалы на деталях.
FUR_COLOR = (0.50, 0.50, 0.52, 1.0)   # = Волк.tint из WolfPrefab.cs (серый)
NOSE_COLOR = (0.055, 0.050, 0.058, 1.0)   # мочка — почти чёрная, ловит блик
TOOTH_COLOR = (0.88, 0.86, 0.79, 1.0)     # кость, не белила: белое «выжигает» на солнце

# --- Корпус ---
# Кольцо корпуса задаётся как (y, полуширина, верх_Z, низ_Z).
# Раздельные «верх» и «низ» — это раздельные линия спины и линия живота:
# так холка задирается, а пах поднимается, не трогая друг друга.
#
# АНАТОМИЯ, ради которой числа именно такие: волк УЗКИЙ и ГЛУБОКИЙ. Глубина
# груди ≈ 46% высоты холки, ширина ≈ вдвое меньше глубины. Сделаешь шире —
# получишь лося; проверено первым проходом.
WITHERS = 1.060                      # высота холки: главный масштаб всего зверя

# Формат: (y, полуширина ВВЕРХУ, верх_Z, низ_Z, полуширина ВНИЗУ).
# Разница верхней и нижней полуширины и есть каплевидность рёбер.
CHEST_RINGS = [
    # y      hw_top  top    bottom  hw_bot
    (+0.16, 0.168, 0.994, 0.618, 0.124),   # заходит В КРУП и ЦЕЛИКОМ внутри его сечения:
                                           # торчащий торец даёт ступеньку на животе
    (-0.04, 0.192, 1.042, 0.545, 0.128),   # самая глубокая точка — за лопатками
    (-0.24, 0.186, 1.074, 0.562, 0.126),   # ХОЛКА (максимум спины) — приподнята над линией шеи
    (-0.44, 0.158, 1.028, 0.612, 0.114),   # плечи
    (-0.58, 0.122, 0.970, 0.690, 0.098),   # переход в шею
]
RUMP_RINGS = [
    (+0.02, 0.184, 1.012, 0.570, 0.128),   # заходит В ГРУДЬ
    (+0.24, 0.176, 0.992, 0.630, 0.132),   # поясница — живот подтягивается
    (+0.44, 0.170, 0.962, 0.690, 0.146),   # таз: маклоки, книзу сужается слабее рёбер
    (+0.60, 0.142, 0.920, 0.742, 0.124),   # седалищный бугор — зад ОКРУГЛЫЙ, не клин
    (+0.68, 0.092, 0.878, 0.792, 0.082),   # корень хвоста
]

# Суставные точки корпуса и головы: они же — пивоты соответствующих частей.
PELVIS = Vector((0.0, 0.50, 0.870))         # пивот крупа = центр таза
SPINE_JOINT = Vector((0.0, 0.02, 0.880))    # пивот груди = поясничный стык
SKULL_JOINT = Vector((0.0, -0.790, 1.096))  # затылочное сочленение
JAW_JOINT = Vector((0.0, -0.825, 1.068))    # челюстной сустав

# --- Шея (конус: толстый загривок → тонкий затылок) ---
# База утоплена в грудь на ~8 см, кончик — в затылок: суставы должны ПЕРЕКРЫВАТЬСЯ,
# иначе при повороте головы вылезет дырка.
# База уведена ГЛУБОКО в грудь (до -0.40): при повороте шеи на 15° мелкий нахлёст
# выпускает наружу плоский торец. Глубина нахлёста = запас на угол поворота.
NECK_BASE = Vector((0.0, -0.40, 0.870))
NECK_TIP = Vector((0.0, -0.80, 1.090))
NECK_RADII = [0.172, 0.158, 0.136, 0.120]   # загривок → затылок
NECK_SQUASH = 1.10                          # >1 = шея выше, чем шире (гривастее)

# --- Голова (череп с плоским низом; челюсть — отдельная деталь) ---
# Кольца вдоль морды: (y, z, полуширина, полувысота). Затылок утоплен в шею.
SKULL_RINGS = [
    (-0.700, 1.082, 0.104, 0.106),   # затылок — уведён ВГЛУБЬ шеи на запас поворота
    (-0.848, 1.132, 0.152, 0.144),   # черепная коробка / скулы — шире всего
    (-0.930, 1.118, 0.134, 0.130),   # СТОП (перелом лба в морду)
    (-1.020, 1.086, 0.106, 0.108),   # переносица — морда идёт ВНИЗ
    (-1.108, 1.062, 0.088, 0.092),   # мочка носа: короткая и ТУПАЯ, не клюв
]
SKULL_FLAT_BOTTOM = -0.35            # доля полувысоты, ниже которой низ срезан

# Челюсть заметно короче носа — у волка мочка нависает над нижней губой.
# Длинная плоская челюсть вровень с носом = утиный клюв, проверено.
# ВНИМАНИЕ: верх челюсти срезан на 0.35 ПОЛУВЫСОТЫ, а не на полную — поэтому
# «верх» кольца = z + 0.35*rz. Считать по z + rz значит оставить пасть приоткрытой.
JAW_RINGS = [
    (-0.828, 1.070, 0.112, 0.074),
    (-0.925, 1.058, 0.100, 0.070),
    (-1.005, 1.046, 0.080, 0.060),
    (-1.072, 1.036, 0.062, 0.050),
]

# --- Мочка носа (кольца вдоль морды: y, rx, rz) ---
# Мочка занимает почти весь кончик морды (полуширина морды там 0.088) — иначе
# читается пуговицей, приклеенной к плоскому торцу.
NOSE_Z = 1.082
NOSE_RINGS = [
    (-1.078, 0.062, 0.042),   # утоплена в морду
    (-1.118, 0.072, 0.052),   # самое широкое
    (-1.146, 0.046, 0.034),   # скруглённый передний край
]

# --- Зубы (четырёхгранные пирамидки) ---
# Зуб = (x, y, z_корня, длина, полуоснование).
# Геометрия рассчитана так, чтобы ВЕРХНИЕ клыки шли снаружи от челюсти
# (x=0.093 > полуширины челюсти 0.080, но внутри черепа 0.111) — тогда они
# торчат из-под губы даже с закрытой пастью и зверь читается хищником.
# Нижние клыки растут внутри челюсти: их видно, когда пасть открыта.
FANGS_UPPER = [
    (0.093, -1.005, 1.060, 0.062, 0.017),   # КЛЫК
    (0.088, -0.950, 1.072, 0.030, 0.011),
    (0.082, -0.900, 1.080, 0.026, 0.010),
]
FANGS_LOWER = [
    (0.068, -1.040, 1.030, 0.058, 0.016),   # КЛЫК
    (0.064, -0.985, 1.038, 0.026, 0.010),
    (0.060, -0.930, 1.050, 0.024, 0.009),
]

# --- Уши (объёмные пирамидки) ---
# Плоская пластина сбоку вырождается в рог — ухо должно иметь глубину, тогда
# читается с любого ракурса. 6 треугольников, дешевле некуда.
EAR_ROOT = Vector((0.080, -0.842, 1.232))   # правое; левое — зеркалом
EAR_HEIGHT = 0.200
EAR_WIDTH = 0.088      # поперёк (по X)
EAR_DEPTH = 0.108      # вдоль морды (по Y) — то, что даёт силуэт сбоку
EAR_APEX_BACK = 0.022  # вершина сдвинута назад — ухо чуть заваливается
EAR_TILT_OUT = 20.0    # градусы наружу
EAR_TILT_FWD = 4.0     # градусы вперёд

# --- Хвост (3 сегмента; толстый посередине = пушистый) ---
# Нейтральная поза волка — хвост ОПУЩЕН. Задранный = доминантный, а «вниз» у нас
# уже зарезервировано под боди-лэнгвидж страха.
TAIL_JOINTS = [
    Vector((0.0, 0.600, 0.845)),    # корень утоплен в круп
    Vector((0.0, 0.762, 0.716)),
    Vector((0.0, 0.878, 0.540)),
    Vector((0.0, 0.938, 0.352)),    # висит вниз-назад, кончик не достаёт земли
]
TAIL_RADII = [0.076, 0.090, 0.070, 0.030]   # корень, стык1, стык2, кончик

# --- Ноги. Суставы даны для ПРАВОЙ стороны (+X), левая — зеркалом ---
# Сегмент = (имя, сустав_от, сустав_до, радиус_от, радиус_до) — толщина каждого
# звена задаётся явно, чтобы лапа могла быть толще запястья.
# Передняя нога псовых: ЛОПАТКА лежит наклонно по рёбрам (даёт покатое плечо и
# переход от холки к ноге), плечевая кость идёт вниз-НАЗАД, и только предплечье —
# вертикальный столб. Локоть впереди плечевого сустава — типовая ошибка, из-за неё
# зверь выглядит ходулистым.
FRONT_JOINTS = [
    Vector((0.126, -0.318, 0.972)),   # верх лопатки — у холки, внутри грудной клетки
    Vector((0.150, -0.442, 0.702)),   # плечевой сустав (вынесен вперёд-вниз)
    Vector((0.155, -0.396, 0.428)),   # ЛОКОТЬ — назад под грудь, но лишь на ~4 см:
                                      # больше — и зверь в стойке выглядит шагающим
    Vector((0.158, -0.410, 0.140)),   # запястье
    # ВЫСОТА НОСКА = 0.866 * радиус, а не радиус: шестигранник касается земли ГРАНЬЮ,
    # его нижняя вершина не на -r, а на -cos(30°)*r. Иначе лапа парит над землёй.
    Vector((0.158, -0.442, 0.054)),   # носок передней лапы
]
# Радиусы задаются ПРОФИЛЕМ вдоль сегмента, а не парой «начало-конец».
# Зачем: труба с широким плоским торцом наверху торчит из корпуса отворотом сапога.
# Профиль-«луковица» (узко у корпуса → мускул → сужение к суставу) прячет стык
# внутри туши, а масса мышцы оказывается там, где она у зверя и есть.
FRONT_SEGMENTS = [
    ("Scapula",    0, 1, [0.048, 0.086, 0.082, 0.070]),   # лопатка: сходит на нет у холки
    ("LegF_Upper", 1, 2, [0.078, 0.098, 0.086, 0.070]),   # плечевая кость с трицепсом
    ("LegF_Lower", 2, 3, [0.068, 0.056, 0.050]),          # предплечье и пясть
    ("PawF",       3, 4, [0.062, 0.060]),                 # лапа-подушка
]

REAR_JOINTS = [
    Vector((0.134, 0.408, 0.768)),    # тазобедренный — ГЛУБОКО в крупе, чтобы бедро
                                      # сливалось с ягодичной массой, а не приставлялось
    Vector((0.140, 0.250, 0.400)),    # КОЛЕНО — ниже и дальше вперёд: бедро вытянулось
    Vector((0.140, 0.428, 0.228)),    # СКАКАТЕЛЬНЫЙ (уведён назад) — собачий излом
    Vector((0.140, 0.382, 0.052)),    # пятка
    Vector((0.140, 0.272, 0.045)),    # носок задней лапы
]
REAR_SEGMENTS = [
    # Бедро — ПЛОСКОЕ вбок (радиусы малые) и вытянутое спереди-назад (squash 1.6).
    # Круглое сечение того же объёма вываливалось за габарит крупа на 12 см.
    ("Thigh",      0, 1, [0.058, 0.100, 0.090, 0.064], 1.60),
    ("Shin",       1, 2, [0.064, 0.052, 0.046], 1.35),    # голень тоже приплюснута
    ("Metatarsus", 2, 3, [0.048, 0.042], 1.20),
    ("PawR",       3, 4, [0.062, 0.058], 0.85),   # лапа наоборот ПРИПЛЮСНУТА сверху
]

# --- Плотность сетки (грани = стиль; больше не значит лучше) ---
SIDES_BODY = 8
SIDES_NECK = 8
SIDES_HEAD = 8
SIDES_LEG = 6
SIDES_TAIL = 6

# =============================================================================
#  Инфраструктура
# =============================================================================


def purge_scene():
    """Чистый старт: убиваем всё, включая осиротевшие данные."""
    bpy.ops.wm.read_factory_settings(use_empty=True)


def unit_ring(sides, phase=0.0):
    """Единичное кольцо: список (u, v) на окружности. u — вбок, v — вверх."""
    return [
        (math.cos(2.0 * math.pi * i / sides + phase),
         math.sin(2.0 * math.pi * i / sides + phase))
        for i in range(sides)
    ]


def frame_from_dir(direction):
    """Ортонормированный базис (right, up) для кольца, перпендикулярного direction.

    Опорная вертикаль выбирается по наклону: для почти вертикальных частей (ноги)
    берём мировое «вперёд», иначе — мировое «вверх». Иначе базис вырождается.
    """
    d = direction.normalized()
    hint = Vector((0.0, 1.0, 0.0)) if abs(d.z) > 0.7 else Vector((0.0, 0.0, 1.0))
    right = hint.cross(d)
    if right.length < 1e-6:
        right = Vector((1.0, 0.0, 0.0))
    right.normalize()
    up = d.cross(right).normalized()
    return right, up


def build_loft(name, rings, sides, flat_bottom=None, cap_start=True, cap_end=True):
    """Сшивает трубу по кольцам.

    rings — список (center: Vector, right: Vector, up: Vector, rx: float, rz: float).
    flat_bottom — если задан, все вершины с v < порога прижимаются к порогу
                  (даёт срезанный низ: череп без челюсти, челюсть без верха).
    Возвращает объект с пивотом в МИРОВОМ нуле (позиционируется вызывающим).
    """
    bm = bmesh.new()
    loops = []
    unit = unit_ring(sides)

    for ring in rings:
        center, right, up, rx, rz = ring[:5]
        # шестой элемент — полуширина У НИЗА кольца. Позволяет каплевидное сечение:
        # у волка рёбра широкие сверху и сходятся к грудине, ровный эллипс читается
        # бочкой и ноги выглядят приставленными.
        rx_bot = ring[5] if len(ring) > 5 else rx
        loop = []
        for u, v in unit:
            vv = v
            if flat_bottom is not None:
                if flat_bottom < 0:
                    vv = max(v, flat_bottom)
                else:
                    vv = min(v, flat_bottom)
            t = (vv + 1.0) * 0.5                     # 0 у низа кольца, 1 у верха
            rxx = rx_bot + (rx - rx_bot) * t
            pos = center + right * (u * rxx) + up * (vv * rz)
            loop.append(bm.verts.new(pos))
        loops.append(loop)

    for a, b in zip(loops, loops[1:]):
        for i in range(sides):
            j = (i + 1) % sides
            bm.faces.new((a[i], a[j], b[j], b[i]))

    if cap_start:
        bm.faces.new(tuple(reversed(loops[0])))
    if cap_end:
        bm.faces.new(tuple(loops[-1]))

    bm.normal_update()
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])

    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    for poly in mesh.polygons:
        poly.use_smooth = False          # flat shading — грани должны читаться

    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return obj


def set_pivot(obj, pivot):
    """Переносит origin объекта в точку pivot, не двигая геометрию в мире."""
    mat = Matrix.Translation(-pivot)
    obj.data.transform(mat)
    obj.location = pivot


def build_body_part(name, rings_spec, sides):
    """Корпусная часть из спеки (y, полуширина, верх_Z, низ_Z).

    Кольца стоят вертикально поперёк оси Y — корпус не изгибается, ему не нужен
    честный фрейм-транспорт.
    """
    right = Vector((1.0, 0.0, 0.0))
    up = Vector((0.0, 0.0, 1.0))
    rings = []
    for y, hw, top, bot, hw_bot in rings_spec:
        center = Vector((0.0, y, (top + bot) * 0.5))
        rings.append((center, right, up, hw, (top - bot) * 0.5, hw_bot))
    return build_loft(name, rings, sides)


def build_tube(name, joints, radii, sides, squash=1.0):
    """Труба вдоль ломаной joints с радиусами radii (len == len(joints)).

    squash > 1 делает сечение выше, чем шире.
    """
    assert len(joints) == len(radii), f"{name}: точек {len(joints)}, радиусов {len(radii)}"
    rings = []
    for i, center in enumerate(joints):
        if i == 0:
            d = joints[1] - joints[0]
        elif i == len(joints) - 1:
            d = joints[-1] - joints[-2]
        else:
            d = (joints[i + 1] - joints[i - 1])
        right, up = frame_from_dir(d)
        rings.append((center, right, up, radii[i], radii[i] * squash))
    return build_loft(name, rings, sides)


def build_head(name):
    """Череп: лофт вдоль морды с плоско срезанным низом (челюсть — отдельно)."""
    right = Vector((1.0, 0.0, 0.0))
    up = Vector((0.0, 0.0, 1.0))
    rings = []
    for y, z, rx, rz in SKULL_RINGS:
        rings.append((Vector((0.0, y, z)), right, up, rx, rz))
    return build_loft(name, rings, SIDES_HEAD, flat_bottom=SKULL_FLAT_BOTTOM)


def build_jaw(name):
    """Нижняя челюсть: та же схема, но срезан ВЕРХ (прилегает к черепу)."""
    right = Vector((1.0, 0.0, 0.0))
    up = Vector((0.0, 0.0, 1.0))
    rings = []
    for y, z, rx, rz in JAW_RINGS:
        rings.append((Vector((0.0, y, z)), right, up, rx, rz))
    return build_loft(name, rings, SIDES_HEAD, flat_bottom=0.35)


def build_nose(name):
    """Мочка носа — маленький скруглённый объём, выступающий за кончик морды."""
    right = Vector((1.0, 0.0, 0.0))
    up = Vector((0.0, 0.0, 1.0))
    rings = [(Vector((0.0, y, NOSE_Z)), right, up, rx, rz)
             for y, rx, rz in NOSE_RINGS]
    return build_loft(name, rings, 6)


def build_teeth(name, specs, direction):
    """Ряд зубов одной челюсти в едином меше.

    specs — список (x, y, z_корня, длина, полуоснование) для ПРАВОЙ стороны,
    левая добавляется зеркалом. direction: -1 зубы растут вниз, +1 вверх.
    """
    bm = bmesh.new()
    for side in (1, -1):
        for x, y, z_root, length, hb in specs:
            px = x * side
            base = [Vector((px - hb, y - hb, z_root)),
                    Vector((px + hb, y - hb, z_root)),
                    Vector((px + hb, y + hb, z_root)),
                    Vector((px - hb, y + hb, z_root))]
            apex = Vector((px, y, z_root + length * direction))
            vb = [bm.verts.new(p) for p in base]
            va = bm.verts.new(apex)
            bm.faces.new(tuple(vb))
            for i in range(4):
                bm.faces.new((vb[i], vb[(i + 1) % 4], va))

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    for poly in mesh.polygons:
        poly.use_smooth = False

    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return obj


def build_ear(name, side):
    """Ухо — четырёхгранная пирамидка на прямоугольном основании.

    side: +1 правое, -1 левое.
    """
    bm = bmesh.new()
    hw, hd, h = EAR_WIDTH * 0.5, EAR_DEPTH * 0.5, EAR_HEIGHT

    base = [Vector((-hw, -hd, 0.0)),
            Vector((+hw, -hd, 0.0)),
            Vector((+hw, +hd, 0.0)),
            Vector((-hw, +hd, 0.0))]
    apex = Vector((0.0, EAR_APEX_BACK, h))

    vb = [bm.verts.new(p) for p in base]
    va = bm.verts.new(apex)
    bm.faces.new(tuple(reversed(vb)))                 # донце (утоплено в череп)
    for i in range(4):
        bm.faces.new((vb[i], vb[(i + 1) % 4], va))

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    for poly in mesh.polygons:
        poly.use_smooth = False

    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)

    root = Vector((EAR_ROOT.x * side, EAR_ROOT.y, EAR_ROOT.z))
    obj.location = root
    obj.rotation_euler = (math.radians(EAR_TILT_FWD),
                          math.radians(-EAR_TILT_OUT * side),
                          0.0)
    return obj


def mirror_joints(joints):
    return [Vector((-p.x, p.y, p.z)) for p in joints]


# =============================================================================
#  Сборка волка
# =============================================================================

def build_wolf():
    """Строит все части. Возвращает {имя_части: (объект, точка_сустава)}."""
    parts = {}

    def add(name, obj, pivot):
        set_pivot(obj, pivot)
        parts[name] = (obj, pivot)
        return obj

    # --- корпус ---
    chest = build_body_part("Chest", CHEST_RINGS, SIDES_BODY)
    add("Chest", chest, SPINE_JOINT)          # пивот — поясничный стык, не центр меша

    rump = build_body_part("Rump", RUMP_RINGS, SIDES_BODY)
    add("Rump", rump, PELVIS)

    # --- шея (3 звена одной трубы, чтобы легла конусом) ---
    neck_pts = []
    for i in range(4):
        t = i / 3.0
        neck_pts.append(NECK_BASE.lerp(NECK_TIP, t))
    neck = build_tube("Neck", neck_pts, NECK_RADII, SIDES_NECK, squash=NECK_SQUASH)
    add("Neck", neck, NECK_BASE)

    # --- голова ---
    add("Head", build_head("Head"), SKULL_JOINT)
    add("Jaw", build_jaw("Jaw"), JAW_JOINT)

    # нос и верхние зубы едут с черепом, нижние — с челюстью
    add("Nose", build_nose("Nose"), SKULL_JOINT)
    add("Fangs_Upper", build_teeth("Fangs_Upper", FANGS_UPPER, -1), SKULL_JOINT)
    add("Fangs_Lower", build_teeth("Fangs_Lower", FANGS_LOWER, +1), JAW_JOINT)

    for side, tag in ((1, "R"), (-1, "L")):
        ear = build_ear(f"Ear_{tag}", side)
        parts[f"Ear_{tag}"] = (ear, ear.location.copy())     # пивот уже в основании

    # --- хвост: 3 отдельных сегмента, пивот каждого в своём стыке ---
    for i in range(3):
        seg = build_tube(f"Tail{i + 1}",
                         [TAIL_JOINTS[i], TAIL_JOINTS[i + 1]],
                         [TAIL_RADII[i], TAIL_RADII[i + 1]],
                         SIDES_TAIL)
        add(f"Tail{i + 1}", seg, TAIL_JOINTS[i])

    # --- ноги ---
    for side, tag in ((1, "R"), (-1, "L")):
        fj = FRONT_JOINTS if side > 0 else mirror_joints(FRONT_JOINTS)
        rj = REAR_JOINTS if side > 0 else mirror_joints(REAR_JOINTS)

        for joints, segments in ((fj, FRONT_SEGMENTS), (rj, REAR_SEGMENTS)):
            for spec in segments:
                base, a, b, radii = spec[:4]
                # пятый элемент — сплющенность: радиус задаёт ширину ВБОК, а squash
                # растягивает сечение спереди-назад. Бедро у волка именно такое —
                # плоская лопасть, а не круглый окорок.
                squash = spec[4] if len(spec) > 4 else 1.0
                steps = len(radii) - 1
                pts = [joints[a].lerp(joints[b], i / steps) for i in range(len(radii))]
                seg = build_tube(f"{base}_{tag}", pts, radii, SIDES_LEG, squash=squash)
                add(f"{base}_{tag}", seg, joints[a])

    return parts


# =============================================================================
#  Скелет: кость на каждую часть, часть висит на кости жёстко
# =============================================================================

BONE_TREE = [
    # (кость, родитель, head, tail, деталь-меш)
    ("root",      None,     Vector((0, 0.30, 0.02)),  Vector((0, -0.10, 0.02)), None),
    ("hips",      "root",   PELVIS,                   SPINE_JOINT,              "Rump"),
    ("spine",     "hips",   SPINE_JOINT,              NECK_BASE,                "Chest"),
    ("neck",      "spine",  NECK_BASE,                NECK_TIP,                 "Neck"),
    ("head",      "neck",   SKULL_JOINT,              Vector((0, -1.19, 1.048)), "Head"),
    ("jaw",       "head",   JAW_JOINT,                Vector((0, -1.17, 0.982)), "Jaw"),
]


# Детали без собственного сустава: едут с чужой костью (кость : [меши])
EXTRA_ATTACH = {
    "head": ["Nose", "Fangs_Upper"],
    "jaw": ["Fangs_Lower"],
}


def bone_specs(parts):
    specs = list(BONE_TREE)

    for side, tag in ((1, "R"), (-1, "L")):
        root = Vector((EAR_ROOT.x * side, EAR_ROOT.y, EAR_ROOT.z))
        tip = root + Vector((0.05 * side, 0.02, EAR_HEIGHT))
        specs.append((f"ear_{tag}", "head", root, tip, f"Ear_{tag}"))

    for i in range(3):
        parent = "hips" if i == 0 else f"tail{i}"
        specs.append((f"tail{i + 1}", parent, TAIL_JOINTS[i], TAIL_JOINTS[i + 1],
                      f"Tail{i + 1}"))

    for side, tag in ((1, "R"), (-1, "L")):
        fj = FRONT_JOINTS if side > 0 else mirror_joints(FRONT_JOINTS)
        rj = REAR_JOINTS if side > 0 else mirror_joints(REAR_JOINTS)

        # цепочка костей повторяет цепочку сегментов: кость_i родитель кости_i+1
        for joints, segments, root_bone in ((fj, FRONT_SEGMENTS, "spine"),
                                            (rj, REAR_SEGMENTS, "hips")):
            prev = root_bone
            for spec in segments:
                base, a, b = spec[:3]
                bone = f"{base.lower()}_{tag}"
                specs.append((bone, prev, joints[a], joints[b], f"{base}_{tag}"))
                prev = bone

    return specs


def build_armature(parts):
    """Скелет: кость на каждую часть, часть висит на кости ЖЁСТКО (без весов)."""
    arm_data = bpy.data.armatures.new("WolfRig")
    arm = bpy.data.objects.new("WolfRig", arm_data)
    bpy.context.collection.objects.link(arm)

    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode='EDIT')

    specs = bone_specs(parts)
    for name, parent, head, tail, _mesh in specs:
        eb = arm_data.edit_bones.new(name)
        eb.head = head
        eb.tail = tail
        if (tail - head).length < 1e-4:                  # страховка от нулевой кости
            eb.tail = head + Vector((0, 0, 0.05))
    for name, parent, _h, _t, _m in specs:
        if parent:
            arm_data.edit_bones[name].parent = arm_data.edit_bones[parent]

    bpy.ops.object.mode_set(mode='OBJECT')

    def attach(mesh_name, bone_name):
        if mesh_name not in parts:
            return
        obj = parts[mesh_name][0]
        world = obj.matrix_world.copy()
        obj.parent = arm
        obj.parent_type = 'BONE'
        obj.parent_bone = bone_name
        obj.matrix_world = world                          # вернуть на место

    for name, _p, _h, _t, mesh_name in specs:
        if mesh_name:
            attach(mesh_name, name)
    for bone_name, mesh_names in EXTRA_ATTACH.items():
        for mesh_name in mesh_names:
            attach(mesh_name, bone_name)
    return arm


# =============================================================================
#  Материал / превью / экспорт
# =============================================================================

MATERIAL_MAP = {                       # деталь → материал; остальное = мех
    "Nose": "WolfNose",
    "Fangs_Upper": "WolfTeeth",
    "Fangs_Lower": "WolfTeeth",
}
MATERIAL_COLORS = {
    "WolfFur": (FUR_COLOR, 0.85),
    "WolfNose": (NOSE_COLOR, 0.30),    # мокрый нос — низкая шероховатость, блик
    "WolfTeeth": (TOOTH_COLOR, 0.45),
}


def make_material(name):
    mat = bpy.data.materials.get(name)
    if mat:
        return mat
    color, roughness = MATERIAL_COLORS[name]
    mat = bpy.data.materials.new(name)
    bsdf = mat.node_tree.nodes.get("Principled BSDF") if mat.node_tree else None
    if bsdf:
        bsdf.inputs["Base Color"].default_value = color
        if "Roughness" in bsdf.inputs:
            bsdf.inputs["Roughness"].default_value = roughness
    mat.diffuse_color = color
    return mat


def assign_materials(parts):
    for part_name, (obj, _pivot) in parts.items():
        mat_name = MATERIAL_MAP.get(part_name, "WolfFur")
        obj.data.materials.append(make_material(mat_name))


def look_at(cam, target):
    direction = target - cam.location
    cam.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()


def render_previews(out_dir, suffix=""):
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.render.resolution_x = 1000
    scene.render.resolution_y = 750
    scene.render.film_transparent = False

    shading = scene.display.shading
    shading.light = 'STUDIO'
    shading.color_type = 'MATERIAL'            # иначе нос и зубы сольются с мехом
    shading.show_cavity = True                 # подчёркивает грани — видно фасетки
    shading.cavity_type = 'BOTH'
    if scene.world is None:
        scene.world = bpy.data.worlds.new("W")
    scene.world.color = (0.16, 0.17, 0.19)

    cam = scene.camera
    if cam is None:
        cam_data = bpy.data.cameras.new("Cam")
        cam_data.type = 'ORTHO'
        cam_data.ortho_scale = 3.0
        cam = bpy.data.objects.new("Cam", cam_data)
        bpy.context.collection.objects.link(cam)
        scene.camera = cam

    body = Vector((0.0, 0.0, 0.62))
    head = Vector((0.0, -0.95, 1.09))
    # имя: (позиция камеры, точка интереса, ширина кадра в метрах)
    views = {
        "side":     (Vector((4.0, 0.0, 0.70)), body, 3.0),
        "front":    (Vector((0.0, -4.0, 0.85)), body, 3.0),
        "threeq":   (Vector((2.8, -3.0, 1.70)), body, 3.0),
        "top":      (Vector((0.01, 0.0, 4.0)), body, 3.0),
        "rear34":   (Vector((-2.6, 3.0, 1.50)), body, 3.0),
        # крупные планы: мелочь вроде клыков на общем плане не читается
        "head34":   (Vector((1.30, -1.70, 1.55)), head, 0.85),
        "headside": (Vector((2.20, -0.95, 1.12)), head, 0.85),
    }
    for name, (pos, target, scale) in views.items():
        cam.location = pos
        cam.data.ortho_scale = scale
        look_at(cam, target)
        scene.render.filepath = os.path.join(out_dir, f"wolf_{name}{suffix}.png")
        bpy.ops.render.render(write_still=True)

    # силуэтный проход — главная проверка стиля: контур должен читаться как волк
    shading.light = 'FLAT'
    shading.color_type = 'SINGLE'
    shading.single_color = (0.03, 0.03, 0.04)
    shading.show_cavity = False
    scene.world.color = (1.0, 1.0, 1.0)
    for name in ("side", "threeq"):
        pos, target, scale = views[name]
        cam.location = pos
        cam.data.ortho_scale = scale
        look_at(cam, target)
        scene.render.filepath = os.path.join(out_dir, f"wolf_silhouette_{name}{suffix}.png")
        bpy.ops.render.render(write_still=True)

    # вернуть освещение, иначе следующий вызов отрендерит плоские силуэты
    shading.light = 'STUDIO'
    shading.color_type = 'MATERIAL'
    shading.show_cavity = True
    scene.world.color = (0.16, 0.17, 0.19)


# Тест-поза: рычит, уши прижаты, хвост поджат, идёт. Это не анимация, а ПРОВЕРКА —
# если в суставе разъезжается, значит нахлёста не хватило и надо чинить геометрию.
TEST_POSE = {
    "neck": (-14.0, 0.0, 0.0),
    "head": (18.0, 0.0, 12.0),
    "jaw": (34.0, 0.0, 0.0),          # пасть открыта
    "ear_R": (52.0, 0.0, 0.0),        # уши прижаты назад — «злой»
    "ear_L": (52.0, 0.0, 0.0),
    "tail1": (-32.0, 0.0, 0.0),       # хвост поджат
    "tail2": (-26.0, 0.0, 0.0),
    "tail3": (-20.0, 0.0, 0.0),
    "legf_upper_R": (26.0, 0.0, 0.0),  # шаг
    "legf_lower_R": (-18.0, 0.0, 0.0),
    "legf_upper_L": (-20.0, 0.0, 0.0),
    "thigh_L": (24.0, 0.0, 0.0),
    "shin_L": (-22.0, 0.0, 0.0),
    "thigh_R": (-16.0, 0.0, 0.0),
}


# Стресс-поза: заведомо БОЛЬШИЕ углы. Нужна не для красоты, а чтобы найти предел,
# за которым части расходятся в суставах — эти числа идут в инструкцию аниматору.
STRESS_POSE = {
    "neck": (-45.0, 0.0, 35.0),
    "head": (45.0, 0.0, 45.0),
    "jaw": (60.0, 0.0, 0.0),
    "ear_R": (90.0, 0.0, 0.0),
    "ear_L": (90.0, 0.0, 0.0),
    "tail1": (-60.0, 0.0, 40.0),
    "tail2": (-60.0, 0.0, 0.0),
    "tail3": (-60.0, 0.0, 0.0),
    "scapula_R": (30.0, 0.0, 0.0),
    "legf_upper_R": (60.0, 0.0, 0.0),
    "legf_lower_R": (-60.0, 0.0, 0.0),
    "scapula_L": (-30.0, 0.0, 0.0),
    "legf_upper_L": (-45.0, 0.0, 0.0),
    "thigh_L": (55.0, 0.0, 0.0),
    "shin_L": (-55.0, 0.0, 0.0),
    "thigh_R": (-40.0, 0.0, 0.0),
    "metatarsus_R": (40.0, 0.0, 0.0),
}


def apply_test_pose(arm, pose=None):
    pose = pose if pose is not None else TEST_POSE
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode='POSE')
    for bone_name, (rx, ry, rz) in pose.items():
        pb = arm.pose.bones.get(bone_name)
        if pb is None:
            print(f"[wolf_gen] ВНИМАНИЕ: кости '{bone_name}' нет — поза неполная")
            continue
        pb.rotation_mode = 'XYZ'
        pb.rotation_euler = (math.radians(rx), math.radians(ry), math.radians(rz))
    bpy.ops.object.mode_set(mode='OBJECT')
    bpy.context.view_layer.update()


ARCHIVE_DIR = "Archive~"     # тильда в конце = Unity игнорирует папку целиком


def archive_previous(*paths):
    """Складывает предыдущие версии файлов в `Models/Archive~/` с меткой времени.

    Генератор перезаписывает модель на каждом прогоне, а «было лучше» выясняется
    через несколько итераций — к тому моменту откатывать уже нечего. Метка берётся
    из времени изменения самого файла, а не из «сейчас»: так в имени стоит дата,
    когда версия была сделана.

    Папка называется с тильдой на конце — Unity такие папки не импортирует, поэтому
    архив лежит рядом с моделью, но не превращается в десяток лишних ассетов.
    """
    for path in paths:
        if not os.path.exists(path):
            continue
        archive = os.path.join(os.path.dirname(path), ARCHIVE_DIR)
        os.makedirs(archive, exist_ok=True)
        stamp = datetime.datetime.fromtimestamp(os.path.getmtime(path))
        base, ext = os.path.splitext(os.path.basename(path))
        dst = os.path.join(archive, f"{base}_{stamp:%Y-%m-%d_%H%M%S}{ext}")
        if not os.path.exists(dst):
            shutil.copy2(path, dst)
            print(f"[wolf_gen] прошлая версия сохранена: {os.path.relpath(dst, os.path.dirname(path))}")


def export_fbx(path):
    """FBX под Unity: метры, ось Y вверх, морда уезжает в +Z.

    Корневой объект приезжает в Unity с поворотом (-90, 0, 0) — так экспортёр
    записывает конвертацию осей. Это штатно: пока поворот не обнуляют, модель
    стоит правильно. Если он мешает, в импортёре Unity есть галочка
    `Bake Axis Conversion`. Чинить это на стороне Blender не стоит:
    `bake_space_transform` ломает иерархию, а предкомпенсация поворотом корня —
    хрупкий трюк (проверено, оба варианта разваливают модель).
    """
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        apply_scale_options='FBX_SCALE_ALL',
        axis_forward='-Z',
        axis_up='Y',
        object_types={'ARMATURE', 'MESH'},
        add_leaf_bones=False,
        bake_anim=False,
    )


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out_dir = os.path.dirname(os.path.abspath(__file__))
    if "--out" in argv:
        out_dir = argv[argv.index("--out") + 1]
    preview_dir = os.path.join(out_dir, "preview")
    os.makedirs(preview_dir, exist_ok=True)

    purge_scene()
    parts = build_wolf()
    assign_materials(parts)
    arm = build_armature(parts)

    tris = sum(len(o.data.loop_triangles) if o.data.loop_triangles else 0
               for o, _ in parts.values())
    if tris == 0:
        for obj, _ in parts.values():
            obj.data.calc_loop_triangles()
        tris = sum(len(o.data.loop_triangles) for o, _ in parts.values())

    blend_path = os.path.join(out_dir, "Wolf.blend")
    archive_previous(blend_path)          # до перезаписи — иначе архивировать нечего
    bpy.ops.wm.save_as_mainfile(filepath=blend_path)
    print(f"[wolf_gen] частей: {len(parts)}, треугольников: {tris}")
    print(f"[wolf_gen] сохранено: {blend_path}")

    if "--fbx" in argv:
        # FBX едет СРАЗУ в проект: копировать руками — лишний шаг и лишний способ
        # разъехаться (в Assets одна версия, в Tools другая).
        # файл лежит в <repo>/Tools/Blender/ — до корня репо ТРИ уровня вверх
        repo = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
        models_dir = os.path.join(repo, "Assets", "_Chimera", "Models")
        os.makedirs(models_dir, exist_ok=True)
        fbx_path = os.path.join(models_dir, "Wolf.fbx")
        archive_previous(fbx_path)
        export_fbx(fbx_path)
        print(f"[wolf_gen] FBX: {fbx_path}")

    if "--render" in argv:
        render_previews(preview_dir)
        print(f"[wolf_gen] превью: {preview_dir}")

    # позы идут ПОСЛЕ сохранения .blend — в файл они не попадают
    if "--pose" in argv:
        apply_test_pose(arm)
        render_previews(preview_dir, suffix="_posed")
        print("[wolf_gen] тест-поза отрисована")

    if "--stress" in argv:
        apply_test_pose(arm, STRESS_POSE)
        render_previews(preview_dir, suffix="_stress")
        print("[wolf_gen] стресс-поза отрисована — ищи расхождения в суставах")


if __name__ == "__main__":
    main()
