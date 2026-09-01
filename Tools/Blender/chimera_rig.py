"""CHIMERA — общее ядро генераторов моделей (шарнирные куклы).

Здесь нет ничего видового: только примитивы (лофт, труба, пирамидка, зубы),
сборка скелета, материалы, рендер превью, архив и экспорт. Форма конкретного
зверя живёт в его генераторе (`wolf_gen.py`, `player_gen.py`).

Вынесено после ВТОРОГО непохожего примера — квадрупед и бипед (правило трёх).

Оси: Blender Z-вверх, метры, модель смотрит в -Y → в Unity +Z.
"""

import bpy
import bmesh
import datetime
import math
import os
import shutil
from mathutils import Vector

ARCHIVE_DIR = "Archive~"     # тильда в конце = Unity игнорирует папку целиком


# ─────────────────────────────── сцена ───────────────────────────────

def purge_scene():
    """Чистый старт: убиваем всё, включая осиротевшие данные."""
    bpy.ops.wm.read_factory_settings(use_empty=True)


# ─────────────────────────────── геометрия ───────────────────────────

def unit_ring(sides, phase=0.0):
    """Единичное кольцо: список (u, v) на окружности. u — вбок, v — вверх."""
    return [
        (math.cos(2.0 * math.pi * i / sides + phase),
         math.sin(2.0 * math.pi * i / sides + phase))
        for i in range(sides)
    ]


def frame_from_dir(direction):
    """Ортонормированный базис (right, up) для кольца поперёк direction.

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

    rings — список (center, right, up, rx, rz[, rx_bot]).
    rx_bot — полуширина У НИЗА кольца: даёт каплевидное сечение (рёбра широкие
    сверху, сходятся к грудине). Ровный эллипс читается бочкой.

    flat_bottom — порог по v: всё ниже прижимается к порогу (срезанный низ черепа).
    ВНИМАНИЕ: порог задаётся в ДОЛЯХ полувысоты, поэтому «верх» кольца со срезом
    равен z + flat_bottom*rz, а не z + rz.
    """
    bm = bmesh.new()
    loops = []
    unit = unit_ring(sides)

    for ring in rings:
        center, right, up, rx, rz = ring[:5]
        rx_bot = ring[5] if len(ring) > 5 else rx
        loop = []
        for u, v in unit:
            vv = v
            if flat_bottom is not None:
                vv = max(v, flat_bottom) if flat_bottom < 0 else min(v, flat_bottom)
            t = (vv + 1.0) * 0.5                     # 0 у низа кольца, 1 у верха
            rxx = rx_bot + (rx - rx_bot) * t
            loop.append(bm.verts.new(center + right * (u * rxx) + up * (vv * rz)))
        loops.append(loop)

    for a, b in zip(loops, loops[1:]):
        for i in range(sides):
            j = (i + 1) % sides
            bm.faces.new((a[i], a[j], b[j], b[i]))

    if cap_start:
        bm.faces.new(tuple(reversed(loops[0])))
    if cap_end:
        bm.faces.new(tuple(loops[-1]))

    return _finish(bm, name)


def _finish(bm, name):
    """Общий хвост построения: нормали, flat shading, объект в сцене."""
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
    obj.data.transform(__import__("mathutils").Matrix.Translation(-pivot))
    obj.location = pivot


def build_body_part(name, rings_spec, sides):
    """Корпусная часть из спеки (y, полуширина_верха, верх_Z, низ_Z, полуширина_низа).

    Кольца стоят вертикально поперёк оси Y — корпусу не нужен честный фрейм-транспорт.
    """
    right = Vector((1.0, 0.0, 0.0))
    up = Vector((0.0, 0.0, 1.0))
    rings = []
    for y, hw, top, bot, hw_bot in rings_spec:
        center = Vector((0.0, y, (top + bot) * 0.5))
        rings.append((center, right, up, hw, (top - bot) * 0.5, hw_bot))
    return build_loft(name, rings, sides)


def build_column(name, rings_spec, sides):
    """То же, но для ВЕРТИКАЛЬНОГО тела (человек): кольца поперёк оси Z.

    Спека кольца: (z, полуширина_вбок, полуглубина_вперёд[, смещение_по_Y]).
    """
    right = Vector((1.0, 0.0, 0.0))
    up = Vector((0.0, 1.0, 0.0))
    rings = []
    for spec in rings_spec:
        z, hx, hy = spec[:3]
        offset_y = spec[3] if len(spec) > 3 else 0.0
        rings.append((Vector((0.0, offset_y, z)), right, up, hx, hy))
    return build_loft(name, rings, sides)


def build_tube(name, joints, radii, sides, squash=1.0):
    """Труба вдоль ломаной joints с радиусами radii (len == len(joints)).

    squash > 1 растягивает сечение вдоль `up` фрейма (для конечности — вперёд-назад),
    оставляя ширину вбок равной радиусу. Так делается плоское бедро-лопасть:
    круглое сечение того же объёма вываливается за габарит корпуса.
    """
    assert len(joints) == len(radii), f"{name}: точек {len(joints)}, радиусов {len(radii)}"
    rings = []
    for i, center in enumerate(joints):
        if i == 0:
            d = joints[1] - joints[0]
        elif i == len(joints) - 1:
            d = joints[-1] - joints[-2]
        else:
            d = joints[i + 1] - joints[i - 1]
        right, up = frame_from_dir(d)
        rings.append((center, right, up, radii[i], radii[i] * squash))
    return build_loft(name, rings, sides)


def build_segments(prefix, joints, segments, sides, add):
    """Цепочка сегментов конечности.

    segments — список (имя, i_от, i_до, [профиль радиусов][, squash]).
    Профиль из 3+ значений даёт «луковицу»: узко у корпуса, мускул ниже, сужение
    к суставу. Труба с плоским широким торцом торчит из тела отворотом сапога.
    """
    for spec in segments:
        base, a, b, radii = spec[:4]
        squash = spec[4] if len(spec) > 4 else 1.0
        steps = len(radii) - 1
        pts = [joints[a].lerp(joints[b], i / steps) for i in range(len(radii))]
        name = f"{base}{prefix}"
        add(name, build_tube(name, pts, radii, sides, squash=squash), joints[a])


def build_pyramid(name, width, depth, height, apex_shift=0.0):
    """Четырёхгранная пирамидка на прямоугольном основании (ухо, зуб, рог).

    Плоская пластина сбоку вырождается в рог — объём читается с любого ракурса.
    """
    bm = bmesh.new()
    hw, hd = width * 0.5, depth * 0.5
    base = [Vector((-hw, -hd, 0.0)), Vector((+hw, -hd, 0.0)),
            Vector((+hw, +hd, 0.0)), Vector((-hw, +hd, 0.0))]
    vb = [bm.verts.new(p) for p in base]
    va = bm.verts.new(Vector((0.0, apex_shift, height)))
    bm.faces.new(tuple(reversed(vb)))
    for i in range(4):
        bm.faces.new((vb[i], vb[(i + 1) % 4], va))
    return _finish(bm, name)


def build_box(name, center, half, ):
    """Скруглённый блок-кирпич (ладонь, стопа, глаз). half — половины габаритов."""
    bm = bmesh.new()
    cx, cy, cz = center
    hx, hy, hz = half
    verts = []
    for sz in (-1, 1):
        for sy in (-1, 1):
            for sx in (-1, 1):
                verts.append(bm.verts.new(Vector((cx + sx * hx, cy + sy * hy, cz + sz * hz))))
    bm.verts.ensure_lookup_table()
    q = [(0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)]
    for a, b, c, d in q:
        bm.faces.new((verts[a], verts[b], verts[c], verts[d]))
    return _finish(bm, name)


def build_teeth(name, specs, direction):
    """Ряд зубов одной челюсти в едином меше.

    specs — (x, y, z_корня, длина, полуоснование) для ПРАВОЙ стороны, левая зеркалом.
    direction: -1 зубы растут вниз, +1 вверх.
    """
    bm = bmesh.new()
    for side in (1, -1):
        for x, y, z_root, length, hb in specs:
            px = x * side
            base = [Vector((px - hb, y - hb, z_root)), Vector((px + hb, y - hb, z_root)),
                    Vector((px + hb, y + hb, z_root)), Vector((px - hb, y + hb, z_root))]
            vb = [bm.verts.new(p) for p in base]
            va = bm.verts.new(Vector((px, y, z_root + length * direction)))
            bm.faces.new(tuple(vb))
            for i in range(4):
                bm.faces.new((vb[i], vb[(i + 1) % 4], va))
    return _finish(bm, name)


def mirror_joints(joints):
    return [Vector((-p.x, p.y, p.z)) for p in joints]


# ─────────────────────────────── скелет ──────────────────────────────

def build_armature(rig_name, parts, specs, extra_attach=None):
    """Скелет: кость на каждую часть, часть висит на кости ЖЁСТКО (без весов).

    specs — (кость, родитель, head, tail, имя_меша).
    extra_attach — {кость: [меши]} для деталей без своего сустава (нос, зубы).
    """
    arm_data = bpy.data.armatures.new(rig_name)
    arm = bpy.data.objects.new(rig_name, arm_data)
    bpy.context.collection.objects.link(arm)

    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode='EDIT')
    for name, parent, head, tail, _mesh in specs:
        eb = arm_data.edit_bones.new(name)
        eb.head = head
        eb.tail = tail
        if (tail - head).length < 1e-4:              # страховка от нулевой кости
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
        obj.matrix_world = world                      # вернуть на место

    for name, _p, _h, _t, mesh_name in specs:
        if mesh_name:
            attach(mesh_name, name)
    for bone_name, mesh_names in (extra_attach or {}).items():
        for mesh_name in mesh_names:
            attach(mesh_name, bone_name)
    return arm


def apply_pose(arm, pose):
    """Крутит кости по словарю {кость: (rx, ry, rz) в градусах}."""
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode='POSE')
    for bone_name, (rx, ry, rz) in pose.items():
        pb = arm.pose.bones.get(bone_name)
        if pb is None:
            print(f"[rig] ВНИМАНИЕ: кости '{bone_name}' нет — поза неполная")
            continue
        pb.rotation_mode = 'XYZ'
        pb.rotation_euler = (math.radians(rx), math.radians(ry), math.radians(rz))
    bpy.ops.object.mode_set(mode='OBJECT')
    bpy.context.view_layer.update()


# ─────────────────────────────── материалы ───────────────────────────

def make_material(name, color, roughness):
    mat = bpy.data.materials.get(name)
    if mat:
        return mat
    mat = bpy.data.materials.new(name)
    bsdf = mat.node_tree.nodes.get("Principled BSDF") if mat.node_tree else None
    if bsdf:
        bsdf.inputs["Base Color"].default_value = color
        if "Roughness" in bsdf.inputs:
            bsdf.inputs["Roughness"].default_value = roughness
    mat.diffuse_color = color
    return mat


def assign_materials(parts, material_map, palette, default_mat):
    """material_map: {деталь: имя_материала}. palette: {имя: (цвет, roughness)}."""
    for part_name, (obj, _pivot) in parts.items():
        mat_name = material_map.get(part_name, default_mat)
        color, roughness = palette[mat_name]
        obj.data.materials.append(make_material(mat_name, color, roughness))


# ─────────────────────────────── вывод ───────────────────────────────

def look_at(cam, target):
    direction = target - cam.location
    cam.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()


def render_previews(out_dir, views, body_color, suffix=""):
    """views: {имя: (позиция камеры, точка интереса, ширина кадра в метрах)}.

    Силуэтный проход (чёрное на белом) — главная проверка стиля: он вскрывает то,
    что в затенённом рендере читается как тень.
    """
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.render.resolution_x = 1000
    scene.render.resolution_y = 750

    shading = scene.display.shading
    shading.light = 'STUDIO'
    shading.color_type = 'MATERIAL'          # иначе детали сольются с телом
    shading.show_cavity = True
    shading.cavity_type = 'BOTH'
    if scene.world is None:
        scene.world = bpy.data.worlds.new("W")
    scene.world.color = (0.16, 0.17, 0.19)

    cam = scene.camera
    if cam is None:
        cam_data = bpy.data.cameras.new("Cam")
        cam_data.type = 'ORTHO'
        cam = bpy.data.objects.new("Cam", cam_data)
        bpy.context.collection.objects.link(cam)
        scene.camera = cam

    for name, (pos, target, scale) in views.items():
        cam.location = pos
        cam.data.ortho_scale = scale
        look_at(cam, target)
        scene.render.filepath = os.path.join(out_dir, f"{name}{suffix}.png")
        bpy.ops.render.render(write_still=True)

    shading.light = 'FLAT'
    shading.color_type = 'SINGLE'
    shading.single_color = (0.03, 0.03, 0.04)
    shading.show_cavity = False
    scene.world.color = (1.0, 1.0, 1.0)
    for name in list(views)[:2]:
        pos, target, scale = views[name]
        cam.location = pos
        cam.data.ortho_scale = scale
        look_at(cam, target)
        scene.render.filepath = os.path.join(out_dir, f"{name}_silhouette{suffix}.png")
        bpy.ops.render.render(write_still=True)

    shading.light = 'STUDIO'                 # вернуть, иначе следующий вызов плоский
    shading.color_type = 'MATERIAL'
    shading.show_cavity = True
    scene.world.color = (0.16, 0.17, 0.19)


def archive_previous(*paths):
    """Складывает предыдущие версии в `Archive~/` с меткой времени.

    Генератор перезаписывает модель на каждом прогоне, а «было лучше» выясняется
    через несколько итераций — к тому моменту откатывать уже нечего. Метка берётся
    из времени изменения файла, а не из «сейчас».
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
            print(f"[rig] прошлая версия сохранена: {ARCHIVE_DIR}/{os.path.basename(dst)}")


def export_fbx(path):
    """FBX под Unity: метры, ось Y вверх, морда уезжает в +Z.

    Корневой объект приезжает с поворотом (-90, 0, 0) — так экспортёр записывает
    конвертацию осей. Это штатно: пока поворот не обнуляют, модель стоит правильно.
    Если мешает — галочка `Bake Axis Conversion` в импортёре Unity. Чинить на
    стороне Blender не стоит: `bake_space_transform` ломает иерархию, а
    предкомпенсация поворотом корня — хрупкий трюк (оба проверены, оба разваливают).
    """
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        apply_scale_options='FBX_SCALE_ALL',
        axis_forward='-Z',
        axis_up='Y',
        object_types={'ARMATURE', 'MESH', 'EMPTY'},
        add_leaf_bones=False,
        bake_anim=False,
    )


def models_dir():
    """`Assets/_Chimera/Models` относительно `Tools/Blender/` — модель едет сразу
    в проект: копировать руками значит завести две расходящиеся версии.

    ТРИ подъёма: файл лежит в <репо>/Tools/Blender/, значит Blender → Tools → репо.
    """
    here = os.path.dirname(os.path.abspath(__file__))          # <репо>/Tools/Blender
    repo = os.path.dirname(os.path.dirname(here))              # <репо>
    path = os.path.join(repo, "Assets", "_Chimera", "Models")
    os.makedirs(path, exist_ok=True)
    return path


def count_tris(parts):
    total = 0
    for obj, _ in parts.values():
        obj.data.calc_loop_triangles()
        total += len(obj.data.loop_triangles)
    return total
