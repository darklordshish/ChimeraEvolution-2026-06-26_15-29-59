"""CHIMERA — генератор модели игрока (человек-учёный, шасси «Человек»).

Тело нарезано ПО СЛОТАМ ОРГАНОВ, а не «как удобно анатомически»: граница слота =
граница сменной детали. Химеризация меняет органы прямо в бою, и модель обязана
меняться вместе с ними (спека `2026-07-22-model-igroka-sloty.md`).

Слоты и детали:
    Пасть   — Jaw (+ зубы)
    Чутьё   — Nose, EyeL/EyeR, EarL/EarR
    Руки    — ArmL/R → ForearmL/R → HandL/R   (кисть — точка свапа на коготь)
    Ноги    — ThighL/R → ShinL/R → FootL/R
    Шкура   — пока материал
    Сердце  — невизуален
    Придаток — пустые сокеты Socket_Tail, Socket_Horns

Имена частей — КОНТРАКТ с кодом игры, менять нельзя:
  * Head/Nose прячет от первого лица `PlayerController.SetFirstPerson`;
  * Head/Muzzle/Nose/Jaw/Ear* красят эмоции (`Telegraph.IsHeadPart`);
  * лицо (EyeL/EyeR/BrowL/BrowR/Beard) исключено из тинта состава.

Запуск:
    blender --background --python player_gen.py -- --fbx --render --pose
"""

import bpy
import bmesh
import math
import os
import sys
from mathutils import Vector

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import chimera_rig as rig

# =============================================================================
#  TUNING — вся форма человека живёт здесь
# =============================================================================

HEIGHT = 1.90                 # макушка; главный масштаб фигуры

# --- Материалы ---
PALETTE = {
    "PlayerSkin":  ((0.70, 0.56, 0.46, 1.0), 0.75),   # тинт состава перекрасит в игре
    "PlayerEyes":  ((0.10, 0.10, 0.12, 1.0), 0.25),
    "PlayerHair":  ((0.38, 0.33, 0.27, 1.0), 0.85),   # седеющая борода учёного
    "PlayerTeeth": ((0.88, 0.86, 0.79, 1.0), 0.45),
}
MATERIAL_MAP = {
    "EyeL": "PlayerEyes", "EyeR": "PlayerEyes",
    "BrowL": "PlayerHair", "BrowR": "PlayerHair", "Beard": "PlayerHair",
    "Teeth": "PlayerTeeth",
}

# --- Торс. Кольца (z, полуширина, полуглубина[, смещение вперёд]) ---
# Атлет: плечи широкие, талия узкая — «перевёрнутый треугольник», но человеческий,
# без вервольфьего гротеска.
# Атлетичность делает не «толще везде», а РАЗНИЦА плеч и талии: V-образный торс.
# Здесь плечи 0.238 против талии 0.136 — отношение 1.75 (было 1.52).
CHEST_RINGS = [
    # ШИРИНУ ПЛЕЧ ДАЮТ ДЕЛЬТЫ, А НЕ ТОРС. Расширишь сам торс — он станет полкой,
    # из-под которой руки торчат отдельными наплечниками (видно на силуэте).
    # Четвёртое число — смещение кольца ВПЕРЁД (−Y). Им лепится «полка» грудных:
    # они выступают вперёд, а под ними живот западает. Без этого перепада грудь
    # читается просто бочкой.
    (1.612, 0.104, 0.092, 0.006),    # ТРАПЕЦИЯ у шеи: иначе шея выходит из плеч под прямым углом
    (1.570, 0.158, 0.106, 0.002),    # скос трапеции к плечу
    (1.524, 0.186, 0.122, -0.006),   # плечевой пояс, ключицы
    (1.470, 0.214, 0.150, -0.018),   # ГРУДНЫЕ — самая выступающая точка торса
    (1.424, 0.226, 0.142, -0.010),   # ШИРОЧАЙШИЕ — самое широкое место, ПОД подмышками
    (1.372, 0.208, 0.124, 0.004),    # ПОД грудными живот западает — граница мышцы
    (1.312, 0.190, 0.122, 0.002),    # крылья сходятся вниз
    (1.245, 0.158, 0.112, 0.000),    # рёберная дуга — резкое сужение к талии
    (1.180, 0.146, 0.104, -0.002),   # переход в живот (внутри Abdomen)
]
ABDOMEN_RINGS = [
    # ПРЕСС: прямые мышцы выступают вперёд (−Y), а бока втянуты. Разница между
    # полушириной и полуглубиной и создаёт «плиту» живота вместо цилиндра.
    (1.230, 0.152, 0.112, -0.008),   # заходит В ГРУДЬ; верх пресса
    (1.180, 0.144, 0.110, -0.010),   # выступ прямых мышц
    (1.120, 0.134, 0.102, -0.008),   # ТАЛИЯ — самое узкое место фигуры
    (1.060, 0.144, 0.106, -0.004),   # низ живота
    (0.990, 0.160, 0.118, 0.002),    # внутри таза
]
# Четвёртое число — смещение кольца НАЗАД (+Y): им лепится ягодичный объём.
# Без него фигура в профиль плоская, как доска.
PELVIS_RINGS = [
    (1.030, 0.160, 0.114, 0.004),   # заходит В ЖИВОТ
    (0.960, 0.174, 0.128, 0.012),   # гребни подвздошных костей
    (0.900, 0.170, 0.132, 0.020),   # ягодицы
    (0.850, 0.144, 0.114, 0.012),   # промежность
]

# --- Шея и голова ---
NECK_BASE = Vector((0.0, 0.010, 1.500))   # утоплена в грудь
NECK_TIP = Vector((0.0, -0.008, 1.690))
NECK_RADII = [0.090, 0.080, 0.072, 0.066]   # у атлета шея мощная, не карандаш

# Голова: кольца (z, полуширина, полуглубина, смещение вперёд).
# Лицо смотрит в -Y, поэтому смещение отрицательное = вперёд.
HEAD_RINGS = [
    (1.640, 0.066, 0.076, -0.010),   # низ черепа (утоплен в шею)
    (1.700, 0.090, 0.106, -0.012),   # челюстной уровень
    (1.762, 0.098, 0.114, -0.008),   # скулы, глаза
    (1.832, 0.094, 0.108, 0.002),    # лоб; затылок уходит назад
    (1.884, 0.064, 0.074, 0.008),    # макушка
]

JAW_RINGS = [
    (1.690, 0.070, 0.078, -0.020),   # угол челюсти
    (1.660, 0.060, 0.072, -0.030),   # подбородок
    (1.640, 0.044, 0.052, -0.032),
]

# Нос колонной, а не кубиком: узкая переносица между бровями → спинка → кончик.
# Лицо (перед головы) на y ≈ −0.118, нос выступает за него примерно на сантиметр.
NOSE_RINGS = [
    (1.800, 0.013, 0.018, -0.086),   # переносица, между бровями
    (1.768, 0.016, 0.026, -0.096),   # спинка
    (1.742, 0.021, 0.031, -0.100),   # кончик — самая выступающая точка
    (1.724, 0.019, 0.022, -0.090),   # основание, крылья носа
]
EYE = dict(x=0.037, y=-0.088, z=1.776, half=(0.019, 0.010, 0.011))
BROW = dict(x=0.039, y=-0.092, z=1.800, half=(0.030, 0.009, 0.007))
# Борода-лопата учёного: колонна, сужающаяся книзу. Кубом она читается коробкой,
# приклеенной к лицу.
# Смещение подобрано так, чтобы борода ЛЕЖАЛА НА лице (перед головы на y=-0.118),
# а не тонула в черепе: утопленная борода торчит наружу только рваными углами.
BEARD_RINGS = [
    (1.712, 0.078, 0.062, -0.056),   # заходит в щёки
    (1.672, 0.072, 0.064, -0.066),
    (1.640, 0.056, 0.054, -0.070),
    (1.612, 0.032, 0.034, -0.064),   # клин
]
EAR = dict(x=0.092, z=1.752, width=0.020, depth=0.052, height=0.058)

# Зубы видны только когда челюсть открыта. При закрытом рте они обязаны быть
# УТОПЛЕНЫ, иначе торчат наружу в щель между черепом и челюстью.
TEETH = [(0.020, -0.040, 1.698, 0.012, 0.007),
         (0.040, -0.032, 1.698, 0.010, 0.007)]

# --- Руки. Суставы для ПРАВОЙ стороны (+X), левая зеркалом ---
ARM_JOINTS = [
    # Сустав НИЖЕ линии плеч: дельта радиусом 0.086 поднимается над ним, и её верх
    # должен лишь слегка перекрывать плечевой пояс (1.560). Поставь выше — получишь
    # квадратные «погоны» поверх торса.
    # Сустав УТОПЛЕН в торс и поднят: рука входит в тело узким концом (см. профиль),
    # иначе её плоский торец торчит эполетом — та же болезнь, что «сапоги» у волка.
    Vector((0.150, 0.006, 1.545)),   # плечевой сустав, внутри плечевого пояса
    Vector((0.222, 0.014, 1.220)),   # локоть — разведён наружу
    Vector((0.246, -0.006, 0.930)),  # запястье; проходит СНАРУЖИ бедра (его край 0.198)
]
# Резкий перепад по профилю = рельеф. Плавная труба читается «просто толще».
ARM_SEGMENTS = [
    # узкий вход в торс → ДЕЛЬТА → бицепс → узкий локоть
    ("Arm",     0, 1, [0.045, 0.094, 0.106, 0.074, 0.052]),
    ("Forearm", 1, 2, [0.060, 0.080, 0.048, 0.036]),   # брюшко предплечья → тонкое запястье
]

# Кисть: ладонь + слитый блок четырёх пальцев + большой палец отдельно.
# От первого лица кисти ближе всего к камере, и слот «Руки» — главный трейдофф
# игры (коготь забирает оружие), поэтому подмена обязана читаться.
HAND = dict(
    wrist=(0.246, -0.006, 0.930),
    palm_half=(0.034, 0.026, 0.048),
    fingers_half=(0.031, 0.023, 0.046),
    thumb_half=(0.016, 0.019, 0.030),
    thumb_offset=0.030,      # насколько большой палец уходит ВНУТРЬ от оси кисти;
                             # больше — и он повисает отдельным кубиком рядом с ладонью
)

# --- Ноги ---
LEG_JOINTS = [
    Vector((0.086, 0.000, 0.930)),   # тазобедренный (внутри таза)
    Vector((0.098, -0.010, 0.520)),  # колено
    Vector((0.100, 0.006, 0.095)),   # лодыжка — ВНУТРИ стопы, иначе голень до неё не достаёт
]
LEG_SEGMENTS = [
    # Бедро сужено: радиусом 0.130 его край выходил на 0.222 и упирался в руку.
    ("Thigh", 0, 1, [0.088, 0.112, 0.094, 0.062], 1.20),   # квадрицепс → узкое колено
    ("Shin",  1, 2, [0.070, 0.096, 0.050, 0.040], 1.20),   # ИКРА высоко, к лодыжке тонко
]
# Стопа лежит НА ЗЕМЛЕ: центр по Z равен полувысоте, иначе фигура парит или тонет.
FOOT = dict(ankle=(0.100, 0.006, 0.095), half=(0.042, 0.112, 0.050), toe_y=-0.045)

# --- Сокеты придатков (пустышки для сменных частей) ---
SOCKETS = {
    "Socket_Tail": Vector((0.0, 0.104, 0.940)),    # копчик — сюда змеиный хвост
    "Socket_Horns": Vector((0.0, 0.010, 1.876)),   # темя — сюда лосиные рога
}

SIDES_TORSO = 8
SIDES_LIMB = 6
SIDES_HEAD = 8

# =============================================================================
#  Сборка
# =============================================================================


def add_box_to_bm(bm, center, half):
    cx, cy, cz = center
    hx, hy, hz = half
    verts = []
    for sz in (-1, 1):
        for sy in (-1, 1):
            for sx in (-1, 1):
                verts.append(bm.verts.new(Vector((cx + sx * hx, cy + sy * hy, cz + sz * hz))))
    for a, b, c, d in ((0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1),
                       (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)):
        bm.faces.new((verts[a], verts[b], verts[c], verts[d]))


def build_hand(name, side):
    """Кисть: ладонь + блок четырёх пальцев + большой палец."""
    bm = bmesh.new()
    wx, wy, wz = HAND["wrist"]
    wx *= side
    ph = HAND["palm_half"]
    fh = HAND["fingers_half"]
    th = HAND["thumb_half"]

    # ладонь ЗАХОДИТ в предплечье на 2.5 см: стык впритык при flat shading читается
    # уступом, и кисть выглядит приставленным кубиком
    palm_z = wz - ph[2] + 0.025
    add_box_to_bm(bm, (wx, wy, palm_z), ph)
    # пальцы ниже ладони, с ЗАМЕТНЫМ перекрытием — иначе на крупном плане видна щель
    add_box_to_bm(bm, (wx, wy - 0.003, palm_z - ph[2] - fh[2] + 0.020), fh)
    # большой палец — сбоку внутрь; он и читается как «рука человека», а не варежка
    add_box_to_bm(bm, (wx - HAND["thumb_offset"] * side, wy - 0.012, palm_z + 0.008), th)

    return rig._finish(bm, name)


def build_wolf_scientist():
    parts = {}

    def add(name, obj, pivot):
        rig.set_pivot(obj, pivot)
        parts[name] = (obj, pivot)
        return obj

    # --- торс тремя секциями: сгибается в пояснице и в груди ---
    add("Chest", rig.build_column("Chest", CHEST_RINGS, SIDES_TORSO), Vector((0, 0, 1.230)))
    add("Abdomen", rig.build_column("Abdomen", ABDOMEN_RINGS, SIDES_TORSO), Vector((0, 0, 1.020)))
    add("Pelvis", rig.build_column("Pelvis", PELVIS_RINGS, SIDES_TORSO), Vector((0, 0, 0.960)))

    # --- шея и голова ---
    neck_pts = [NECK_BASE.lerp(NECK_TIP, i / 3.0) for i in range(4)]
    add("Neck", rig.build_tube("Neck", neck_pts, NECK_RADII, SIDES_LIMB), NECK_BASE)

    add("Head", rig.build_column("Head", HEAD_RINGS, SIDES_HEAD), Vector((0, 0, 1.660)))
    add("Jaw", rig.build_column("Jaw", JAW_RINGS, SIDES_HEAD), Vector((0, -0.010, 1.700)))
    add("Teeth", rig.build_teeth("Teeth", TEETH, -1), Vector((0, -0.010, 1.700)))

    add("Nose", rig.build_column("Nose", NOSE_RINGS, SIDES_LIMB), Vector((0, 0, 1.752)))
    add("Beard", rig.build_column("Beard", BEARD_RINGS, SIDES_LIMB), Vector((0, 0, 1.664)))

    for side, tag in ((1, "R"), (-1, "L")):
        eye_c = (EYE["x"] * side, EYE["y"], EYE["z"])
        add(f"Eye{tag}", rig.build_box(f"Eye{tag}", eye_c, EYE["half"]), Vector(eye_c))
        brow_c = (BROW["x"] * side, BROW["y"], BROW["z"])
        add(f"Brow{tag}", rig.build_box(f"Brow{tag}", brow_c, BROW["half"]), Vector(brow_c))

        ear = rig.build_pyramid(f"Ear{tag}", EAR["width"], EAR["depth"], EAR["height"])
        ear.rotation_euler = (0.0, math.radians(-88.0 * side), 0.0)  # ухо лежит в профиль
        ear.location = Vector((EAR["x"] * side, 0.004, EAR["z"]))
        parts[f"Ear{tag}"] = (ear, ear.location.copy())

    # --- конечности ---
    for side, tag in ((1, "R"), (-1, "L")):
        aj = ARM_JOINTS if side > 0 else rig.mirror_joints(ARM_JOINTS)
        lj = LEG_JOINTS if side > 0 else rig.mirror_joints(LEG_JOINTS)
        rig.build_segments(tag, aj, ARM_SEGMENTS, SIDES_LIMB, add)
        rig.build_segments(tag, lj, LEG_SEGMENTS, SIDES_LIMB, add)

        add(f"Hand{tag}", build_hand(f"Hand{tag}", side), Vector(aj[2]))

        ax, ay, az = FOOT["ankle"]
        foot_c = (ax * side, ay + FOOT["toe_y"], FOOT["half"][2])
        add(f"Foot{tag}", rig.build_box(f"Foot{tag}", foot_c, FOOT["half"]),
            Vector((ax * side, ay, az)))

    return parts


# --- скелет -----------------------------------------------------------------

BONE_TREE = [
    ("root",    None,     Vector((0, 0.10, 0.02)),  Vector((0, -0.14, 0.02)), None),
    ("pelvis",  "root",   Vector((0, 0, 0.930)),    Vector((0, 0, 1.040)),    "Pelvis"),
    ("spine",   "pelvis", Vector((0, 0, 1.040)),    Vector((0, 0, 1.230)),    "Abdomen"),
    ("chest",   "spine",  Vector((0, 0, 1.230)),    NECK_BASE,                "Chest"),
    ("neck",    "chest",  NECK_BASE,                NECK_TIP,                 "Neck"),
    ("head",    "neck",   Vector((0, 0, 1.660)),    Vector((0, 0, 1.880)),    "Head"),
    ("jaw",     "head",   Vector((0, -0.010, 1.700)), Vector((0, -0.060, 1.646)), "Jaw"),
]

EXTRA_ATTACH = {
    "head": ["Nose", "EyeL", "EyeR", "BrowL", "BrowR", "Beard"],
    "jaw": ["Teeth"],
}


def bone_specs():
    specs = list(BONE_TREE)
    for side, tag in ((1, "R"), (-1, "L")):
        aj = ARM_JOINTS if side > 0 else rig.mirror_joints(ARM_JOINTS)
        lj = LEG_JOINTS if side > 0 else rig.mirror_joints(LEG_JOINTS)

        ear_root = Vector((EAR["x"] * side, 0.004, EAR["z"]))
        specs.append((f"ear{tag}", "head", ear_root,
                      ear_root + Vector((0.04 * side, 0, EAR["height"])), f"Ear{tag}"))

        prev = "chest"
        for base, a, b, _r in ARM_SEGMENTS:
            bone = f"{base.lower()}{tag}"
            specs.append((bone, prev, aj[a], aj[b], f"{base}{tag}"))
            prev = bone
        specs.append((f"hand{tag}", prev, aj[2], aj[2] + Vector((0, 0, -0.13)), f"Hand{tag}"))

        prev = "pelvis"
        for spec in LEG_SEGMENTS:
            base, a, b = spec[:3]
            bone = f"{base.lower()}{tag}"
            specs.append((bone, prev, lj[a], lj[b], f"{base}{tag}"))
            prev = bone
        specs.append((f"foot{tag}", prev, lj[2],
                      lj[2] + Vector((0, -0.14, -0.05)), f"Foot{tag}"))
    return specs


def build_sockets(arm):
    """Пустышки под сменные придатки: хвост змеи, рога лося.

    Пустой сокет ничего не стоит и не рендерится, зато скриптологу есть куда
    вешать деталь без подбора координат на глаз.
    """
    bone_of = {"Socket_Tail": "pelvis", "Socket_Horns": "head"}
    for name, pos in SOCKETS.items():
        empty = bpy.data.objects.new(name, None)
        empty.empty_display_type = 'PLAIN_AXES'
        empty.empty_display_size = 0.06
        bpy.context.collection.objects.link(empty)
        empty.location = pos
        world = empty.matrix_world.copy()
        empty.parent = arm
        empty.parent_type = 'BONE'
        empty.parent_bone = bone_of[name]
        empty.matrix_world = world


# --- позы -------------------------------------------------------------------

TEST_POSE = {
    "neck": (-8.0, 0.0, 14.0),
    "head": (6.0, 0.0, 10.0),
    "jaw": (12.0, 0.0, 0.0),
    "armR": (-28.0, 0.0, 0.0), "forearmR": (-42.0, 0.0, 0.0),
    "armL": (18.0, 0.0, 0.0), "forearmL": (-22.0, 0.0, 0.0),
    "thighL": (24.0, 0.0, 0.0), "shinL": (-30.0, 0.0, 0.0),
    "thighR": (-16.0, 0.0, 0.0), "shinR": (-8.0, 0.0, 0.0),
    "spine": (-4.0, 0.0, 6.0),
}

VIEWS_BODY = Vector((0.0, 0.0, 0.95))
VIEWS_HEAD = Vector((0.0, -0.02, 1.76))
# ortho_scale — ширина кадра; при 1000×750 высота = scale × 0.75. Для роста 1.9
# нужно ≥ 2.6, иначе макушку срезает.
VIEWS = {
    "player_front":  (Vector((0.0, -4.0, 0.95)), VIEWS_BODY, 2.8),
    "player_side":   (Vector((4.0, 0.0, 0.95)), VIEWS_BODY, 2.8),
    "player_threeq": (Vector((2.6, -3.0, 1.85)), VIEWS_BODY, 2.8),
    "player_back":   (Vector((-1.6, 3.4, 1.60)), VIEWS_BODY, 2.8),
    "player_head":   (Vector((1.0, -1.4, 1.92)), VIEWS_HEAD, 0.52),
    "player_hand":   (Vector((1.2, -0.8, 1.00)), Vector((0.25, -0.02, 0.87)), 0.30),
}


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out_dir = os.path.dirname(os.path.abspath(__file__))
    preview_dir = os.path.join(out_dir, "preview")
    os.makedirs(preview_dir, exist_ok=True)

    rig.purge_scene()
    parts = build_wolf_scientist()
    rig.assign_materials(parts, MATERIAL_MAP, PALETTE, "PlayerSkin")
    arm = rig.build_armature("PlayerRig", parts, bone_specs(), EXTRA_ATTACH)
    build_sockets(arm)

    blend_path = os.path.join(out_dir, "Player.blend")
    rig.archive_previous(blend_path)
    bpy.ops.wm.save_as_mainfile(filepath=blend_path)
    print(f"[player_gen] частей: {len(parts)}, треугольников: {rig.count_tris(parts)}")
    print(f"[player_gen] сохранено: {blend_path}")

    if "--fbx" in argv:
        fbx_path = os.path.join(rig.models_dir(), "Player.fbx")
        rig.archive_previous(fbx_path)
        rig.export_fbx(fbx_path)
        print(f"[player_gen] FBX: {fbx_path}")

    if "--render" in argv:
        rig.render_previews(preview_dir, VIEWS, PALETTE["PlayerSkin"][0])
        print(f"[player_gen] превью: {preview_dir}")

    if "--pose" in argv:
        rig.apply_pose(arm, TEST_POSE)
        rig.render_previews(preview_dir, VIEWS, PALETTE["PlayerSkin"][0], suffix="_posed")
        print("[player_gen] тест-поза отрисована")


if __name__ == "__main__":
    main()
