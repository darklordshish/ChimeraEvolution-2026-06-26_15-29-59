"""CHIMERA — генератор вервольфа: человеческое шасси + полный волчий комплект.

Это не третья модель «с нуля», а ПРОВЕРКА КОНСТРУКТОРА. В коде игры вервольф уже
собран как `chassis = Человек`, `donors = [Волк]`, `installAllBeast = true`,
`expression = 2` — то есть игрок на максимуме химеризации. Модель обязана
собираться так же: человеческий торс и руки + волчьи голова, лапы, хвост, когти.

Волчья голова берётся ИЗ `wolf_gen` целиком (череп, челюсть, нос, клыки, уши) и
переносится на плечи одной матрицей. Если бы части волка нельзя было переиспользовать,
это значило бы, что слоты нарезаны неправильно.

Отличия от игрока — гротеск, а не другая анатомия:
  * рост 2.6 против 1.9;
  * сутулость: горб-загривок выше макушки, голова вынесена ВПЕРЁД;
  * перевёрнутый треугольник — огромная грудь, узкая талия;
  * ноги digitigrade (собачий излом), руки до колен, кисти → когти.

Запуск:
    blender --background --python werewolf_gen.py -- --fbx --render --pose
"""

import bpy
import math
import os
import sys
from mathutils import Vector, Matrix

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import chimera_rig as rig
import wolf_gen                      # источник волчьих частей

# =============================================================================
#  TUNING
# =============================================================================

PALETTE = {
    "WerewolfFur":   ((0.44, 0.42, 0.44, 1.0), 0.88),   # темнее волка — босс читается массой
    "WerewolfNose":  ((0.055, 0.05, 0.058, 1.0), 0.30),
    "WerewolfTeeth": ((0.88, 0.86, 0.79, 1.0), 0.45),
    "WerewolfClaws": ((0.16, 0.15, 0.16, 1.0), 0.35),   # когти тёмные, не костяные
}
MATERIAL_MAP = {
    "Nose": "WerewolfNose",
    "Fangs_Upper": "WerewolfTeeth", "Fangs_Lower": "WerewolfTeeth",
    "ClawsL": "WerewolfClaws", "ClawsR": "WerewolfClaws",
}

# --- Торс: ПЕРЕВЁРНУТЫЙ ТРЕУГОЛЬНИК (канон вервольф-рефов) ---
# (z, полуширина, полуглубина, смещение вперёд)
CHEST_RINGS = [
    (2.310, 0.300, 0.210, 0.050),   # основание шеи, у горба
    (2.230, 0.380, 0.235, 0.020),   # плечевой пояс — огромный
    (2.110, 0.410, 0.260, -0.020),  # грудные
    (1.980, 0.392, 0.256, -0.030),  # ШИРОЧАЙШИЕ — масса верха
    (1.830, 0.320, 0.226, -0.010),  # резкий сброс к талии
    (1.720, 0.262, 0.196, 0.000),
]
ABDOMEN_RINGS = [
    (1.780, 0.256, 0.192, -0.006),  # заходит В ГРУДЬ
    (1.640, 0.222, 0.172, -0.014),  # ХИЩНАЯ ТАЛИЯ — узкая и подтянутая
    (1.520, 0.232, 0.180, -0.006),
    (1.430, 0.252, 0.196, 0.004),
]
PELVIS_RINGS = [
    (1.480, 0.248, 0.196, 0.006),   # заходит В ЖИВОТ
    (1.380, 0.272, 0.216, 0.020),
    (1.300, 0.266, 0.224, 0.034),   # ягодичная масса — противовес наклону
    (1.230, 0.228, 0.196, 0.024),
]

# --- Горб-загривок: он ВЫШЕ макушки, из-за него читается сутулость в профиль ---
HUMP_RINGS = [
    (2.240, 0.230, 0.200, 0.100),
    (2.340, 0.266, 0.216, 0.126),
    (2.430, 0.240, 0.196, 0.140),
    (2.500, 0.160, 0.140, 0.132),
]

# --- Шея: короткая и наклонная ВПЕРЁД-ВНИЗ, голова вынесена перед грудью ---
NECK_BASE = Vector((0.0, 0.040, 2.240))
NECK_TIP = Vector((0.0, -0.190, 2.300))
NECK_RADII = [0.205, 0.190, 0.172, 0.156]   # толстая: несёт тяжёлую волчью голову

# --- Волчья голова: куда и в каком масштабе сажать ---
# Волчий череп растёт ВПЕРЁД от затылка, поэтому затылок должен стоять ПОЗАДИ
# конца шеи (у NECK_TIP y = −0.19), иначе шея до него не достаёт и голова висит
# в воздухе. Здесь −0.15 > −0.19: шея входит в череп на 4 см.
HEAD_ORIGIN = Vector((0.0, -0.150, 2.300))   # НИЖЕ горба — сутулость
HEAD_SCALE = 1.32                            # босс крупнее природного волка, но не крокодил
# Знак: положительный угол опускает морду. Отрицательный задирает её к небу —
# получается ящер, а не волк.
HEAD_PITCH = 16.0

# --- Руки: длинные, до колен; кисть заменена лапой с когтями ---
ARM_JOINTS = [
    Vector((0.256, 0.010, 2.220)),   # плечевой сустав (внутри плечевого пояса)
    Vector((0.400, 0.030, 1.760)),   # локоть
    Vector((0.432, -0.020, 1.320)),  # запястье — ниже таза
]
ARM_SEGMENTS = [
    ("Arm",     0, 1, [0.070, 0.166, 0.184, 0.130, 0.096]),   # узкий вход → ДЕЛЬТА → бицепс
    ("Forearm", 1, 2, [0.104, 0.132, 0.086, 0.062]),          # мощное предплечье
]
PAW_HAND = dict(half=(0.072, 0.052, 0.096), spread=0.052, claw=(0.030, 0.020, 0.118))

# --- Ноги DIGITIGRADE: бедро вперёд-вниз, голень назад-вниз, длинная стопа ---
LEG_JOINTS = [
    Vector((0.170, 0.020, 1.310)),   # тазобедренный (внутри таза)
    Vector((0.186, -0.170, 0.860)),  # КОЛЕНО вынесено вперёд
    Vector((0.190, 0.140, 0.470)),   # СКАКАТЕЛЬНЫЙ уведён назад — обратный излом
    Vector((0.192, 0.040, 0.120)),   # плюсне-фаланговый сустав
]
LEG_SEGMENTS = [
    ("Thigh",      0, 1, [0.108, 0.190, 0.164, 0.110], 1.25),   # бедро-лопасть
    ("Shin",       1, 2, [0.116, 0.140, 0.098, 0.074], 1.20),
    ("Metatarsus", 2, 3, [0.078, 0.086, 0.062, 0.050], 1.15),   # плюсна — «вторая голень»
]
FOOT = dict(half=(0.078, 0.150, 0.056), toe_y=-0.130, claw=(0.026, 0.018, 0.090))

# --- Хвост: волчий, но длиннее и толще ---
TAIL_JOINTS = [
    Vector((0.0, 0.210, 1.310)),
    Vector((0.0, 0.330, 1.090)),
    Vector((0.0, 0.400, 0.830)),
    Vector((0.0, 0.410, 0.560)),
]
TAIL_RADII = [0.098, 0.116, 0.088, 0.038]

SIDES_TORSO = 8
SIDES_LIMB = 6

# =============================================================================
#  Сборка
# =============================================================================


def place_wolf_parts(parts, add):
    """Строит волчью голову из `wolf_gen` и переносит её на плечи вервольфа.

    Одна матрица на все детали: перенос в затылок вервольфа, наклон морды, масштаб,
    и сдвиг так, чтобы волчье затылочное сочленение легло в HEAD_ORIGIN. Пивоты
    едут той же матрицей — иначе части будут вращаться вокруг чужих точек.
    """
    m = (Matrix.Translation(HEAD_ORIGIN)
         @ Matrix.Rotation(math.radians(HEAD_PITCH), 4, 'X')
         @ Matrix.Scale(HEAD_SCALE, 4)
         @ Matrix.Translation(-wolf_gen.SKULL_JOINT))

    built = [
        ("Head", wolf_gen.build_head("Head"), wolf_gen.SKULL_JOINT),
        ("Jaw", wolf_gen.build_jaw("Jaw"), wolf_gen.JAW_JOINT),
        ("Nose", wolf_gen.build_nose("Nose"), wolf_gen.SKULL_JOINT),
        ("Fangs_Upper", wolf_gen.build_teeth("Fangs_Upper", wolf_gen.FANGS_UPPER, -1),
         wolf_gen.SKULL_JOINT),
        ("Fangs_Lower", wolf_gen.build_teeth("Fangs_Lower", wolf_gen.FANGS_LOWER, +1),
         wolf_gen.JAW_JOINT),
    ]
    for side, tag in ((1, "R"), (-1, "L")):
        ear = wolf_gen.build_ear(f"Ear_{tag}", side)
        root = Vector((wolf_gen.EAR_ROOT.x * side, wolf_gen.EAR_ROOT.y, wolf_gen.EAR_ROOT.z))
        ear.data.transform(ear.matrix_basis)      # запечь наклон уха в данные
        ear.matrix_basis = Matrix.Identity(4)
        built.append((f"Ear_{tag}", ear, root))

    for name, obj, pivot in built:
        obj.data.transform(m)
        add(name, obj, m @ pivot)


def build_claws(name, wrist, side):
    """Лапа-коготь вместо кисти: подушка и три когтя-пирамидки вниз-вперёд."""
    import bmesh
    bm = bmesh.new()
    wx, wy, wz = wrist.x * side, wrist.y, wrist.z
    ph = PAW_HAND["half"]
    palm_z = wz - ph[2] + 0.030
    _add_box(bm, (wx, wy, palm_z), ph)

    cw, cd, cl = PAW_HAND["claw"]
    for i in (-1, 0, 1):
        cx = wx + i * PAW_HAND["spread"] * side
        base_z = palm_z - ph[2] + 0.016
        _pyramid(bm, (cx, wy - 0.012, base_z), cw, cd, -cl)
    return rig._finish(bm, name)


def _add_box(bm, center, half):
    cx, cy, cz = center
    hx, hy, hz = half
    v = []
    for sz in (-1, 1):
        for sy in (-1, 1):
            for sx in (-1, 1):
                v.append(bm.verts.new(Vector((cx + sx * hx, cy + sy * hy, cz + sz * hz))))
    for a, b, c, d in ((0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1),
                       (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)):
        bm.faces.new((v[a], v[b], v[c], v[d]))


def _pyramid(bm, center, width, depth, height):
    cx, cy, cz = center
    hw, hd = width * 0.5, depth * 0.5
    base = [Vector((cx - hw, cy - hd, cz)), Vector((cx + hw, cy - hd, cz)),
            Vector((cx + hw, cy + hd, cz)), Vector((cx - hw, cy + hd, cz))]
    vb = [bm.verts.new(p) for p in base]
    va = bm.verts.new(Vector((cx, cy - 0.030, cz + height)))
    bm.faces.new(tuple(vb))
    for i in range(4):
        bm.faces.new((vb[i], vb[(i + 1) % 4], va))


def build_foot(name, ankle, side):
    """Стопа-лапа digitigrade: длинная подошва + когти вперёд."""
    import bmesh
    bm = bmesh.new()
    ax, ay, az = ankle.x * side, ankle.y, ankle.z
    fh = FOOT["half"]
    _add_box(bm, (ax, ay + FOOT["toe_y"], fh[2]), fh)

    # Когти смотрят ВПЕРЁД из носка. Пирамидка вниз здесь не нужна: с нулевой
    # высотой она давала вырожденные грани.
    cw, _cd, cl = FOOT["claw"]
    for i in (-1, 0, 1):
        cx = ax + i * 0.048 * side
        _claw_forward(bm, (cx, ay + FOOT["toe_y"] - fh[1] + 0.010, fh[2] * 0.55), cw, cl)
    return rig._finish(bm, name)


def _claw_forward(bm, center, width, length):
    cx, cy, cz = center
    hw = width * 0.5
    base = [Vector((cx - hw, cy, cz - hw)), Vector((cx + hw, cy, cz - hw)),
            Vector((cx + hw, cy, cz + hw)), Vector((cx - hw, cy, cz + hw))]
    vb = [bm.verts.new(p) for p in base]
    va = bm.verts.new(Vector((cx, cy - length, cz - hw * 0.6)))
    bm.faces.new(tuple(reversed(vb)))
    for i in range(4):
        bm.faces.new((vb[i], vb[(i + 1) % 4], va))


def build_werewolf():
    parts = {}

    def add(name, obj, pivot):
        rig.set_pivot(obj, pivot)
        parts[name] = (obj, pivot)
        return obj

    add("Chest", rig.build_column("Chest", CHEST_RINGS, SIDES_TORSO), Vector((0, 0, 1.780)))
    add("Abdomen", rig.build_column("Abdomen", ABDOMEN_RINGS, SIDES_TORSO), Vector((0, 0, 1.480)))
    add("Pelvis", rig.build_column("Pelvis", PELVIS_RINGS, SIDES_TORSO), Vector((0, 0, 1.380)))
    add("Hump", rig.build_column("Hump", HUMP_RINGS, SIDES_TORSO), Vector((0, 0.10, 2.240)))

    neck_pts = [NECK_BASE.lerp(NECK_TIP, i / 3.0) for i in range(4)]
    add("Neck", rig.build_tube("Neck", neck_pts, NECK_RADII, SIDES_LIMB), NECK_BASE)

    place_wolf_parts(parts, add)

    for i in range(3):
        seg = rig.build_tube(f"Tail{i + 1}", [TAIL_JOINTS[i], TAIL_JOINTS[i + 1]],
                             [TAIL_RADII[i], TAIL_RADII[i + 1]], SIDES_LIMB)
        add(f"Tail{i + 1}", seg, TAIL_JOINTS[i])

    for side, tag in ((1, "R"), (-1, "L")):
        aj = ARM_JOINTS if side > 0 else rig.mirror_joints(ARM_JOINTS)
        lj = LEG_JOINTS if side > 0 else rig.mirror_joints(LEG_JOINTS)
        rig.build_segments(tag, aj, ARM_SEGMENTS, SIDES_LIMB, add)
        rig.build_segments(tag, lj, LEG_SEGMENTS, SIDES_LIMB, add)

        add(f"Claws{tag}", build_claws(f"Claws{tag}", ARM_JOINTS[2], side), Vector(aj[2]))
        add(f"Foot{tag}", build_foot(f"Foot{tag}", LEG_JOINTS[3], side), Vector(lj[3]))

    return parts


# --- скелет -----------------------------------------------------------------

BONE_TREE = [
    ("root",   None,     Vector((0, 0.14, 0.02)),  Vector((0, -0.18, 0.02)), None),
    ("pelvis", "root",   Vector((0, 0, 1.310)),    Vector((0, 0, 1.480)),    "Pelvis"),
    ("spine",  "pelvis", Vector((0, 0, 1.480)),    Vector((0, 0, 1.780)),    "Abdomen"),
    ("chest",  "spine",  Vector((0, 0, 1.780)),    NECK_BASE,                "Chest"),
    ("hump",   "chest",  Vector((0, 0.10, 2.240)), Vector((0, 0.14, 2.480)), "Hump"),
    ("neck",   "chest",  NECK_BASE,                NECK_TIP,                 "Neck"),
    ("head",   "neck",   HEAD_ORIGIN,              HEAD_ORIGIN + Vector((0, -0.55, -0.10)), "Head"),
]

EXTRA_ATTACH = {
    "head": ["Nose", "Fangs_Upper", "Ear_R", "Ear_L"],
    "jaw": ["Fangs_Lower"],
}


def bone_specs():
    specs = list(BONE_TREE)
    specs.append(("jaw", "head", HEAD_ORIGIN + Vector((0, -0.10, -0.09)),
                  HEAD_ORIGIN + Vector((0, -0.52, -0.16)), "Jaw"))

    for i in range(3):
        parent = "pelvis" if i == 0 else f"tail{i}"
        specs.append((f"tail{i + 1}", parent, TAIL_JOINTS[i], TAIL_JOINTS[i + 1], f"Tail{i + 1}"))

    for side, tag in ((1, "R"), (-1, "L")):
        aj = ARM_JOINTS if side > 0 else rig.mirror_joints(ARM_JOINTS)
        lj = LEG_JOINTS if side > 0 else rig.mirror_joints(LEG_JOINTS)

        prev = "chest"
        for base, a, b, _r in ARM_SEGMENTS:
            bone = f"{base.lower()}{tag}"
            specs.append((bone, prev, aj[a], aj[b], f"{base}{tag}"))
            prev = bone
        specs.append((f"claws{tag}", prev, aj[2], aj[2] + Vector((0, -0.05, -0.24)), f"Claws{tag}"))

        prev = "pelvis"
        for spec in LEG_SEGMENTS:
            base, a, b = spec[:3]
            bone = f"{base.lower()}{tag}"
            specs.append((bone, prev, lj[a], lj[b], f"{base}{tag}"))
            prev = bone
        specs.append((f"foot{tag}", prev, lj[3], lj[3] + Vector((0, -0.26, -0.05)), f"Foot{tag}"))
    return specs


TEST_POSE = {
    "neck": (-10.0, 0.0, 12.0),
    "head": (14.0, 0.0, 16.0),
    "jaw": (30.0, 0.0, 0.0),          # рёв
    "ear_R": (46.0, 0.0, 0.0), "ear_L": (46.0, 0.0, 0.0),
    "armR": (-34.0, 0.0, 0.0), "forearmR": (-46.0, 0.0, 0.0),
    "armL": (26.0, 0.0, 0.0), "forearmL": (-30.0, 0.0, 0.0),
    "thighL": (22.0, 0.0, 0.0), "shinL": (-26.0, 0.0, 0.0),
    "thighR": (-14.0, 0.0, 0.0),
    "tail1": (-16.0, 0.0, 10.0), "tail2": (-12.0, 0.0, 0.0),
}

BODY = Vector((0.0, 0.0, 1.30))
VIEWS = {
    "wolfman_front":  (Vector((0.0, -5.0, 1.35)), BODY, 3.6),
    "wolfman_side":   (Vector((5.0, 0.0, 1.35)), BODY, 3.6),
    "wolfman_threeq": (Vector((3.2, -3.8, 2.30)), BODY, 3.6),
    "wolfman_head":   (Vector((1.2, -1.8, 2.75)), Vector((0.0, -0.42, 2.34)), 0.95),
}


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out_dir = os.path.dirname(os.path.abspath(__file__))
    preview_dir = os.path.join(out_dir, "preview")
    os.makedirs(preview_dir, exist_ok=True)

    rig.purge_scene()
    parts = build_werewolf()
    rig.assign_materials(parts, MATERIAL_MAP, PALETTE, "WerewolfFur")
    arm = rig.build_armature("WerewolfRig", parts, bone_specs(), EXTRA_ATTACH)

    blend_path = os.path.join(out_dir, "Werewolf.blend")
    rig.archive_previous(blend_path)
    bpy.ops.wm.save_as_mainfile(filepath=blend_path)
    print(f"[werewolf_gen] частей: {len(parts)}, треугольников: {rig.count_tris(parts)}")

    if "--fbx" in argv:
        fbx_path = os.path.join(rig.models_dir(), "Werewolf.fbx")
        rig.archive_previous(fbx_path)
        rig.export_fbx(fbx_path)
        print(f"[werewolf_gen] FBX: {fbx_path}")

    if "--render" in argv:
        rig.render_previews(preview_dir, VIEWS, PALETTE["WerewolfFur"][0])
        print(f"[werewolf_gen] превью: {preview_dir}")

    if "--pose" in argv:
        rig.apply_pose(arm, TEST_POSE)
        rig.render_previews(preview_dir, VIEWS, PALETTE["WerewolfFur"][0], suffix="_posed")
        print("[werewolf_gen] тест-поза отрисована")


if __name__ == "__main__":
    main()
