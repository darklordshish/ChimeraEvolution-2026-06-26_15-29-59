"""ЦИКЛ В ДВИЖЕНИИ НА НАСТОЯЩЕМ РИГЕ (03.11, просьба геймдизайнера через ГеймБосса: в игре нет скелетной анимации, а смотреть складки, плечо и спину надо в движении). Процедурно, без внешних данных: шаг (2 шага, 24 кадра: бёдра, колени, встречный ход рук, поворот корпуса), рука вперёд, рука в сторону, замах. Это смотр формы, а не анимация: пятка не прибита, только низ стопы держится на земле. Рендер Workbench, плоские грани, задние грани отсечены, как в игре: 3/4 сзади и анфас. Куски `@` скрыты, кости и веса файла не трогаются.

  blender -b сборка.blend --factory-startup --python-exit-code 1 -P cikl_dvizheniya.py -- папка_кадров [--shag 1]
Кадры: папка/q_NNN.png (3/4 сзади), папка/f_NNN.png (анфас). Склейка — `cikl_skleika.py`.
"""
import sys, math, os
import bpy
from mathutils import Quaternion, Vector

a = sys.argv[sys.argv.index('--') + 1:]
OUT = os.path.abspath(a[0]); os.makedirs(OUT, exist_ok=True); STEP = int(a[a.index('--shag') + 1]) if '--shag' in a else 1
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
X, Y, Z = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))
sc = bpy.context.scene
for o in bpy.data.objects:
    if o.type != 'MESH': continue
    vid = '@' not in o.name; o.hide_render = not vid; o.hide_viewport = not vid
    if vid:
        o.color = (0.72, 0.66, 0.60, 1)
        for p in o.data.polygons: p.use_smooth = False
        if o.data.shape_keys:
            for k in o.data.shape_keys.key_blocks[1:]: k.value = 0
arm.hide_render = True
arm.data.pose_position = 'POSE'
for pb in arm.pose.bones: pb.rotation_mode = 'QUATERNION'


def rot(*pary):
    q = Quaternion()
    for os_, deg in pary: q = Quaternion(os_, math.radians(deg)) @ q
    return q


def walk(t):
    """t — доля цикла из двух шагов. Минус по X — вперёд (нос смотрит в −Y), −X — правая сторона."""
    P = {}
    f = 2 * math.pi * t
    for side, sh in (('', 0.0), ('.L', math.pi)):
        fi = f + sh; th = 24 * math.sin(fi) + 4; kn = 6 + 52 * max(0.0, math.cos(fi - 0.35)) ** 1.6
        P['бедро' + side] = rot((X, -th)); P['голень' + side] = rot((X, kn)); P['стопа' + side] = rot((X, -kn * 0.55 + th * 0.55))
        ru = -20 * math.sin(fi); P['плечо' + side] = rot((X, -ru)); P['предплечье' + side] = rot((X, -(16 + 12 * max(0.0, -math.sin(fi)))))
        sg = 1 if side == '' else -1; P['лопатка' + side] = rot((Z, sg * 4 * math.sin(fi)))
    P['таз'] = rot((Z, 7 * math.sin(f)), (Y, 3 * math.cos(f)))
    P['хребет'] = rot((Z, -5 * math.sin(f)), (Y, -1.5 * math.cos(f)), (X, -2))
    P['шея'] = rot((Z, 3 * math.sin(f)))
    return P


NEUT = {}
VPERED = {'плечо': rot((X, -95)), 'предплечье': rot((X, -8)), 'лопатка': rot((Y, 14)), 'трапеция': rot((Y, 6))}
STORONA = {'плечо': rot((Y, 92)), 'предплечье': rot((X, -6)), 'лопатка': rot((Y, 18)), 'трапеция': rot((Y, 8))}
ZAMAH = {'хребет': rot((Z, 28), (X, 4)), 'таз': rot((Z, -14)), 'шея': rot((Z, -16)), 'плечо': rot((Y, 62), (X, 65)), 'предплечье': rot((X, -95)), 'лопатка': rot((Z, -16), (Y, 16)), 'плечо.L': rot((X, -35), (Y, -18)), 'предплечье.L': rot((X, -45)), 'бедро': rot((X, 10)), 'бедро.L': rot((X, -14)), 'голень.L': rot((X, 14))}
UDAR = {'хребет': rot((Z, -30), (X, -8)), 'таз': rot((Z, 14)), 'шея': rot((Z, 16)), 'плечо': rot((X, -80), (Z, -35)), 'предплечье': rot((X, -14)), 'лопатка': rot((Z, 18), (Y, 10)), 'плечо.L': rot((X, 25), (Y, -14)), 'предплечье.L': rot((X, -55)), 'бедро': rot((X, 10)), 'бедро.L': rot((X, -22)), 'голень.L': rot((X, 22))}


def mix(A, B, s):
    s = s * s * (3 - 2 * s); R = {}
    for k in set(A) | set(B): R[k] = A.get(k, Quaternion()).slerp(B.get(k, Quaternion()), s)
    return R


# план: (подпись, число кадров, функция доли → поза)
PLAN = [('шаг', 24, lambda s: walk(s)), ('шаг', 24, lambda s: walk(s)), ('к нейтрали', 6, lambda s: mix(walk(0), NEUT, s)),
        ('рука вперёд', 14, lambda s: mix(NEUT, VPERED, s)), ('держит', 3, lambda s: VPERED), ('опускает', 10, lambda s: mix(VPERED, NEUT, s)),
        ('рука в сторону', 14, lambda s: mix(NEUT, STORONA, s)), ('держит', 3, lambda s: STORONA), ('опускает', 10, lambda s: mix(STORONA, NEUT, s)),
        ('замах', 12, lambda s: mix(NEUT, ZAMAH, s)), ('держит', 3, lambda s: ZAMAH), ('удар', 5, lambda s: mix(ZAMAH, UDAR, s)), ('держит', 4, lambda s: UDAR), ('возврат', 10, lambda s: mix(UDAR, NEUT, s))]
REST = {b.name: b.matrix_local.to_quaternion() for b in arm.data.bones}
foot = [arm.pose.bones['стопа'], arm.pose.bones['стопа.L']]


def nizst():
    return min(min((arm.matrix_world @ p.head).z, (arm.matrix_world @ p.tail).z) for p in foot)


def postavit(P):
    for pb in arm.pose.bones:
        q = P.get(pb.name, Quaternion()); r = REST[pb.name]
        pb.rotation_quaternion = r.inverted() @ q @ r; pb.location = (0, 0, 0)
    bpy.context.view_layer.update()


postavit({}); Z0 = nizst()
# свет и кадр
w = sc.display; sc.render.engine = 'BLENDER_WORKBENCH'; w.shading.light = 'STUDIO'; w.shading.color_type = 'OBJECT'; w.shading.show_backface_culling = True; w.shading.show_shadows = False; w.shading.show_cavity = False; w.shading.show_object_outline = False
sc.world = sc.world or bpy.data.worlds.new('w'); sc.world.color = (0.82, 0.83, 0.84)
sc.render.resolution_x, sc.render.resolution_y = 480, 640; sc.render.film_transparent = False; sc.render.image_settings.file_format = 'PNG'
bpy.ops.mesh.primitive_plane_add(size=6, location=(0, 0, Z0 - 0.002)); pol = bpy.context.object; pol.color = (0.55, 0.56, 0.57, 1)


def cam(naz, yaw, nakl=8.0):
    d = bpy.data.cameras.new(naz); d.type = 'ORTHO'; d.ortho_scale = 2.75; o = bpy.data.objects.new(naz, d); sc.collection.objects.link(o)
    y, n = math.radians(yaw), math.radians(nakl); nap = Vector((math.sin(y) * math.cos(n), -math.cos(y) * math.cos(n), math.sin(n)))
    o.location = Vector((0, 0, 0.98)) + nap * 6; o.rotation_euler = (-nap).to_track_quat('-Z', 'Y').to_euler(); return o


# анфас: камера со стороны −Y; 3/4 сзади: со стороны +Y, со смещением к правой руке (−X)
K = dict(q=cam('q', 180 + 38), f=cam('f', 0, 4.0))
n = 0; opis = []
for naz, m, fn in PLAN:
    for i in range(m):
        s = (i + 0.5) / m if naz == 'шаг' else (i + 1) / m
        P = fn(s); postavit(P); dz = Z0 - nizst()
        rb = arm.pose.bones['хребет']; rb.location = REST['хребет'].inverted() @ (Z * dz); bpy.context.view_layer.update()
        if n % STEP == 0:
            for k, c in K.items():
                sc.camera = c; sc.render.filepath = os.path.join(OUT, '%s_%03d.png' % (k, n)); bpy.ops.render.render(write_still=True)
            opis.append('%03d\t%s' % (n, naz))
        n += 1
open(os.path.join(OUT, 'opis.tsv'), 'w', encoding='utf-8').write('\n'.join(opis))
print('ЦИКЛ: кадров %d' % n)
