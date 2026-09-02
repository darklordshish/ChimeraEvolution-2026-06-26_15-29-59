# -*- coding: utf-8 -*-
"""СБОРКА В BLENDER: кости → арматура + геометрия слоя, ортографические превью.

ЗЕРКАЛО — ОТРАЖЕНИЕМ ПОЗЫ, А НЕ ОТРИЦАТЕЛЬНЫМ МАСШТАБОМ. Минус в scale выворачивает нормали
наизнанку, и левая половина зверя чернеет ровно так же убедительно, как «работает». Здесь поворот
отражается матрицей M·R·M (M = diag(-1,1,1)): определитель остаётся +1, то есть поворот честный.
Та же арифметика в игре (`BoneMesher.Build`), и расходиться им нельзя.
"""
import math, os, sys
import bpy
from mathutils import Vector, Matrix

from . import mesh as gm
from .skel import from_points, SKELETON, MUSCLE, FEATURE, CUT, LAYER_NAME

MIRROR = Matrix(((-1, 0, 0), (0, 1, 0), (0, 0, 1)))

# ── ПЕРЕВОД ОСЕЙ НА ГРАНИЦЕ. Данные вида живут в конвенции ИГРЫ (X вправо, Y вверх, Z вперёд к носу),
# мир Blender — Z вверх, и зверь в нём смотрит в −Y (конвенция, установленная прошлым пайплайном и
# записанная в `Models/README.md`). Перевод делается ОДИН РАЗ здесь, а не размазан по коду: смешение
# двух систем даёт ошибку ровно на одном стыке, и выглядит она как «отвалилась именно эта деталь».
#   (x, y, z)игра → (x, −z, y)Blender
AXIS = Matrix(((1, 0, 0), (0, 0, -1), (0, 1, 0)))
AXIS_T = AXIS.transposed()


def to_b(v):
    """Точка из осей игры в оси Blender."""
    return AXIS @ Vector(v)


def rot_b(m):
    """Поворот из осей игры в оси Blender — умножением СЛЕВА, без сопряжения.

    Сопряжение `AXIS·R·AXISᵀ` переводит обе стороны сразу: и мир, и ЛОКАЛЬНЫЙ кадр кости. А локальный
    кадр менять нельзя — по нему кость растёт (всегда +Y) и по нему же считается сечение. Поймано на
    первой сборке: крестец уходил концом вверх вместо «вперёд», и весь скелет рассыпался веером."""
    return AXIS @ m


def clear():
    """Чистая сцена. Пересборка идёт десятками раз — мусор от прошлой копится молча."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    for coll in (bpy.data.meshes, bpy.data.objects, bpy.data.armatures, bpy.data.materials):
        for it in list(coll):
            coll.remove(it)


def _mat(rot3):
    return Matrix(((rot3[0][0], rot3[0][1], rot3[0][2]),
                   (rot3[1][0], rot3[1][1], rot3[1][2]),
                   (rot3[2][0], rot3[2][1], rot3[2][2])))


def sides(b, pos, rot):
    """Экземпляры кости: одна запись даёт одну или обе стороны (`mirrorX`)."""
    out = [(b.name, to_b(pos), rot_b(_mat(rot)), +1)]
    if b.mirrorX:
        out.append((b.name + '.L', to_b((-pos[0], pos[1], pos[2])),
                    MIRROR @ rot_b(_mat(rot)) @ MIRROR, -1))
    return out


def armature(bones, placed, name='Rig'):
    """Арматура по костям: имена — контракт, иерархия — та же, что в данных.

    Кость Blender задаётся головой и хвостом, то есть ровно суставами — тем же, чем задана наша.
    Перевода не требуется, и разойтись им нечем."""
    arm = bpy.data.armatures.new(name)
    ob = bpy.data.objects.new(name, arm)
    bpy.context.scene.collection.objects.link(ob)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.mode_set(mode='EDIT')

    made = {}
    for b in bones:
        pos, rot, tip = placed[b.name]
        for nm, p, m, side in sides(b, pos, rot):
            eb = arm.edit_bones.new(nm)
            eb.head = p
            eb.tail = p + (m @ Vector((0.0, b.length, 0.0)))
            eb.roll = 0.0
            made[nm] = eb
    for b in bones:
        if not b.parent:
            continue
        for suffix in ('', '.L'):
            nm, pn = b.name + suffix, b.parent + suffix
            if nm in made and pn in made:
                made[nm].parent = made[pn]
                made[nm].use_connect = False
    bpy.ops.object.mode_set(mode='OBJECT')
    return ob


def geometry(bones, placed, layers, sockets_split=True, grow=1.0):
    """Меш слоя. Режется ПО СЛОТАМ — слот есть единица химеризации, значит и единица меша."""
    made = []
    groups = {}
    cutters = {}
    for b in bones:
        if b.layer not in layers:
            continue
        pos, rot, tip = placed[b.name]
        if b.cut:
            cutters.setdefault(b.socket or 'прочее', []).append((b, pos, rot, tip))
            continue
        key = b.socket or 'прочее'
        groups.setdefault(key, []).append((b, pos, rot, tip))

    for slot, items in groups.items():
        name = slot if sockets_split else 'Тело'
        ob, me = gm.new_mesh(name)
        import bmesh
        bm = bmesh.new()
        for b, pos, rot, tip in items:
            for nm, p, m, side in sides(b, pos, rot):
                t = p + (m @ Vector((0.0, b.length, 0.0)))
                rot3 = [[m[i][j] for j in range(3)] for i in range(3)]
                if b.shell:
                    gm.shell_geo(bm, b.shell, p, rot3, b.length, sides=26, grow=grow)
                    continue
                segs = max(2, b.chain) * 4 if b.chain else None
                gm.bone_geo(bm, b, p, rot3, t, profile=getattr(b, 'profile', 'long'),
                            segs=segs, sides=12, bend=getattr(b, 'bend', None), grow=grow)
        gm.finish(ob, me, bm)
        made.append(ob)

    for slot, items in cutters.items():
        target = [o for o in made if o.name == slot]
        if target:
            _subtract(target, items)
    return made


def _subtract(objs, cutters):
    """ВЫРЕЗАТЬ объём: глазница, височная яма, линия рта.

    Почему не «положить сверху ещё одну форму». Углубление и нарост — разные операции, и подменить
    одно другим нельзя в принципе: сколько объёмов ни добавь, дырки не появится, морда только
    заплывёт. Ровно этим кончался прошлый заход — череп оставался гладким батоном.

    РЕЖЕТ ТОЛЬКО СВОЙ СЛОТ. Первый прогон применял общий резак ко всем слотам сразу и срезал морду
    целиком: конус глазницы, выйдя за череп, попадал в соседние объекты. Рез принадлежит слоту так
    же, как объём, — иначе правка головы молча портит грудную клетку."""
    import bmesh
    cob, cme = gm.new_mesh('__cutter')
    bm = bmesh.new()
    for b, pos, rot, tip in cutters:
        for nm, p, m, side in sides(b, pos, rot):
            t = p + (m @ Vector((0.0, b.length, 0.0)))
            rot3 = [[m[i][j] for j in range(3)] for i in range(3)]
            gm.bone_geo(bm, b, p, rot3, t, profile=getattr(b, 'profile', 'plain'), sides=14)
    gm.finish(cob, cme, bm, smooth=False)

    for ob in objs:
        mod = ob.modifiers.new('cut', 'BOOLEAN')
        mod.operation = 'DIFFERENCE'
        mod.object = cob
        mod.solver = 'EXACT'
        # ОПЕРАНД САМОПЕРЕСЕКАЮЩИЙСЯ, И ЭТО НАДО ОБЪЯВИТЬ. Меш слота — не цельное тело, а склад
        # перекрывающихся труб: соседние кости нарочно входят друг в друга на суставе. Точный солвер
        # без `use_self` считает такую внутренность «снаружи» и сносит оболочки целиком — череп
        # исчезал, оставляя скуловые дуги, и выглядело это как «булево не работает»
        mod.use_self = True
        mod.use_hole_tolerant = True
        bpy.context.view_layer.objects.active = ob
        bpy.ops.object.select_all(action='DESELECT')
        ob.select_set(True)
        bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.data.objects.remove(cob)


# ── МАТЕРИАЛЫ-ЗАГЛУШКИ. Цвет в игре перебьёт состав, здесь он нужен только чтобы РАЗЛИЧАТЬ слои
PALETTE = {'кости': (0.86, 0.83, 0.74, 1), 'мышцы': (0.62, 0.24, 0.22, 1),
           'признаки': (0.55, 0.52, 0.50, 1), 'покров': (0.50, 0.50, 0.52, 1)}


# ЦВЕТ ПО СЛОТАМ — ДИАГНОСТИКА, а не оформление. На однотонном рендере «где хвост» и «докуда
# грудная клетка» приходится угадывать, а угадывание по картинке — ровно то, чем прошлый заход
# перевёл десяток итераций. Раскрашенные слоты отвечают на это без единого промера
SLOT_COLOR = {
    'хребет': (0.90, 0.86, 0.72, 1), 'шея': (0.95, 0.72, 0.30, 1),
    'голова': (0.35, 0.72, 0.95, 1), 'Пасть': (0.20, 0.45, 0.85, 1),
    'Сердце': (0.90, 0.35, 0.35, 1), 'Руки': (0.45, 0.85, 0.45, 1),
    'Ноги': (0.30, 0.65, 0.35, 1), 'Хвост': (0.80, 0.45, 0.85, 1),
    'прочее': (0.6, 0.6, 0.6, 1),
}


def paint_slots(objs):
    for ob in objs:
        m = bpy.data.materials.get('slot_' + ob.name) or bpy.data.materials.new('slot_' + ob.name)
        m.use_nodes = False
        m.diffuse_color = SLOT_COLOR.get(ob.name, (0.6, 0.6, 0.6, 1))
        ob.data.materials.clear()
        ob.data.materials.append(m)


def paint(objs, kind='кости'):
    m = bpy.data.materials.get(kind) or bpy.data.materials.new(kind)
    m.use_nodes = False
    m.diffuse_color = PALETTE[kind]
    for ob in objs:
        ob.data.materials.clear()
        ob.data.materials.append(m)


def bounds(objs):
    """Габарит сборки В ОСЯХ BLENDER: (центр x, y, z, наибольший размер).

    ИМЕННО BLENDER, а не игры: `views.py` записан в осях рендера (Z вверх, зверь в −Y), и габарит
    обязан быть в той же системе. Перевод в оси игры здесь однажды увёл кадр за край — тело уехало
    в правый верхний угол, потому что «верх» габарита сложился с «глубиной» камеры."""
    lo = [1e9] * 3
    hi = [-1e9] * 3
    for ob in objs:
        for c in ob.bound_box:
            p = ob.matrix_world @ Vector(c)
            for i in range(3):
                lo[i] = min(lo[i], p[i]); hi[i] = max(hi[i], p[i])
    if lo[0] > hi[0]:
        return None
    return [(lo[i] + hi[i]) / 2.0 for i in range(3)] + [max(hi[i] - lo[i] for i in range(3))]


# ── ПРЕВЬЮ. Кадры вынесены в `views.py`: их читает и детектор, которому bpy недоступен
from .views import VIEWS, frame



def camera(view, W=None, centre=None):
    pos, look, size = frame(view, W, centre) if W else VIEWS[view]
    cam = bpy.data.cameras.new('Cam')
    cam.type = 'ORTHO'
    cam.ortho_scale = size
    ob = bpy.data.objects.new('Cam', cam)
    bpy.context.scene.collection.objects.link(ob)
    ob.location = Vector(pos)
    d = Vector(look) - Vector(pos)
    ob.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
    bpy.context.scene.camera = ob
    return ob


def render(path, view='profile', res=1500, transparent=True, W=None, centre=None):
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_WORKBENCH'
    sh = sc.display.shading
    sh.light = 'STUDIO'
    sh.color_type = 'MATERIAL'
    sh.show_cavity = True          # полости: без них форма читается плоским пятном
    sh.cavity_type = 'BOTH'
    sh.curvature_ridge_factor = 1.6
    sh.curvature_valley_factor = 1.4
    sc.render.film_transparent = transparent
    sc.render.image_settings.file_format = 'PNG'
    sc.render.image_settings.color_mode = 'RGBA'
    for ob in list(bpy.data.objects):
        if ob.type == 'CAMERA':
            bpy.data.objects.remove(ob)
    camera(view, W, centre)
    sc.render.resolution_x = res
    sc.render.resolution_y = int(res * 0.75)
    sc.render.resolution_percentage = 100
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    return path


def skin(objs, rig):
    """ПРИВЯЗКА К АРМАТУРЕ автоматическими весами.

    Слот привязывается ЦЕЛИКОМ к общей иерархии костей, а не к своим костям: графт меняет меш
    одного слота, но двигается он вместе со всем телом. Один `SkinnedMeshRenderer` на слот — так же,
    как это делает игра (`BoneMesher.Build`), и имя рендерера совпадает с именем слота."""
    bpy.ops.object.select_all(action='DESELECT')
    for ob in objs:
        ob.select_set(True)
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.parent_set(type='ARMATURE_AUTO')


def export_fbx(path, objs, rig):
    """FBX для Unity: масштаб 1, метры, оси конвертирует сам экспортёр.

    Корень приезжает в Unity с поворотом (−90,0,0) — это ШТАТНОЕ поведение экспортёра для любой
    модели с арматурой, а не дефект. Обнулить его значит поставить зверя на нос; если нужен чистый
    трансформ — галочка `Bake Axis Conversion` в инспекторе модели (см. `Models/README.md`)."""
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    for ob in objs:
        ob.select_set(True)
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, apply_unit_scale=True,
                             global_scale=1.0, apply_scale_options='FBX_SCALE_NONE',
                             object_types={'ARMATURE', 'MESH'}, use_mesh_modifiers=True,
                             add_leaf_bones=False, bake_anim=False, path_mode='COPY')
    return path


# ── РАДИУС ЗАМЫКАНИЯ У КАЖДОГО СЛОТА СВОЙ ─────────────────────────────────────────────────────────
# Один радиус на всё тело не работает, и это тот же урок, что с раздуванием: щель между рёбрами
# 45 мм, а между черепом и челюстью — 10. Замкнув тело радиусом клетки, мы утраиваем морду: её
# костная полувысота 20–25 мм, а прибавка идёт 2r к высоте. Морда и читалась моськой.
#     Числа — из масштабов ЩЕЛЕЙ в каждой части, а не из вкуса
CLOSE = {'Сердце': 0.030, 'хребет': 0.022, 'шея': 0.018, 'Ноги': 0.016, 'Руки': 0.016,
         'Хвост': 0.014, 'голова': 0.005, 'Пасть': 0.005, 'Чутьё': 0.002}
FUR = {'Сердце': 0.010, 'хребет': 0.012, 'шея': 0.012, 'Ноги': 0.006, 'Руки': 0.006,
       'Хвост': 0.020, 'голова': 0.005, 'Пасть': 0.004, 'Чутьё': 0.001}


def _mod(ob, kind, **kw):
    m = ob.modifiers.new(kind.lower(), kind)
    for k, v in kw.items():
        setattr(m, k, v)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.select_all(action='DESELECT')
    ob.select_set(True)
    bpy.ops.object.modifier_apply(modifier=m.name)


def _remesh(ob, size):
    _mod(ob, 'REMESH', mode='VOXEL', voxel_size=size, adaptivity=0.0)


def _offset(ob, r):
    if r:
        _mod(ob, 'DISPLACE', direction='NORMAL', mid_level=0.0, strength=r)


def hide(objs, cell=0.009, smooth=3, fur=0.010, close=0.022):
    """ПОКРОВ: оболочка поверх костей и мышц, нарезанная обратно ПО СЛОТАМ.

    Так же устроен `BoneMesher` в игре: поле по объёмам → изоповерхность → по одному
    `SkinnedMeshRenderer` на слот. Здесь то же самое делает воксельный ремеш.

    ЗАМЫКАНИЕ («катящийся шар»: раздуть на r, затем сжать на r) идёт ПОСЛОТНО, каждому свой радиус:
    щели у частей разного масштаба, и общий радиус либо оставляет клетку сквозной, либо раздувает
    морду. Выпуклая оболочка тут не годится вовсе — силуэт зверя сильно вогнутый.

    ПОЧЕМУ НАРЕЗКА ОБРАТНО, А НЕ ОДИН МЕШ. Слот — единица химеризации: графт перестраивает меш
    СВОЕГО модуля, а не всю тушу. Сшить всё в один объект значило бы сломать ровно ту фичу, ради
    которой тело разбирается на части."""
    import bmesh
    from mathutils import kdtree

    seeds = []
    for ob in objs:
        for v in ob.data.vertices:
            seeds.append((ob.matrix_world @ v.co, ob.name))
    kd = kdtree.KDTree(len(seeds))
    for i, (co, _) in enumerate(seeds):
        kd.insert(co, i)
    kd.balance()

    # 1. КАЖДЫЙ СЛОТ ЗАМЫКАЕТСЯ СВОИМ РАДИУСОМ — до слияния, пока части ещё различимы
    prepped = []
    for ob in objs:
        c = ob.copy(); c.data = ob.data.copy()
        bpy.context.scene.collection.objects.link(c)
        r = CLOSE.get(ob.name, close)
        g = FUR.get(ob.name, fur)
        _remesh(c, cell)
        if r:
            _offset(c, r)
            _remesh(c, cell * 1.4)
            _offset(c, -r)
            _remesh(c, cell * 1.2)
        _offset(c, g)
        prepped.append(c)

    # 2. Слить и сшить швы между слотами одним общим ремешем — БЕЗ сдвига
    bpy.ops.object.select_all(action='DESELECT')
    for c in prepped:
        c.select_set(True)
    bpy.context.view_layer.objects.active = prepped[0]
    bpy.ops.object.join()
    skin = bpy.context.view_layer.objects.active
    skin.name = 'Покров'
    # ФИНАЛЬНЫЙ РЕМЕШ — ШАГОМ КЛЕТКИ, НЕ КРУПНЕЕ. Ухо у волка 10 мм толщиной, и при шаге 11 мм
    # оно исчезало целиком: на рендере оставался огрызок в треть длины. Тонкая деталь
    # пропадает не «плохо выглядит», а БЕССЛЕДНО — и в данных при этом всё верно
    _remesh(skin, cell)
    _mod(skin, 'SMOOTH', factor=1.0, iterations=smooth)

    # 3. Разрезать обратно по слотам: полигон уходит к ближайшему ИСХОДНОМУ объёму
    bm = bmesh.new(); bm.from_mesh(skin.data)
    bm.faces.ensure_lookup_table()
    by_slot = {}
    for f in bm.faces:
        _, idx, _ = kd.find(f.calc_center_median())
        by_slot.setdefault(seeds[idx][1], []).append(f.index)

    made = []
    for slot, idxs in by_slot.items():
        nb = bm.copy()
        nb.faces.ensure_lookup_table()
        keep = set(idxs)
        bmesh.ops.delete(nb, geom=[f for f in nb.faces if f.index not in keep], context='FACES')
        ob, me = gm.new_mesh(slot)
        nb.to_mesh(me); nb.free()
        for pl in me.polygons:
            pl.use_smooth = True
        made.append(ob)
    bm.free()
    bpy.data.objects.remove(skin)
    for ob in objs:
        bpy.data.objects.remove(ob)
    return made
