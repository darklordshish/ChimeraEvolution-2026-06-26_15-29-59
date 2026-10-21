"""ПОЗА СТУПЕНИ ПО ЛИСТУ (21.10). Файл позы `podgonka_v_poze.py --poza` снят с цели Hunyuan, а цель сгорблена сильнее листа
(оборотень ст.2: хребет цели 46° от вертикали, на профиле листа ~31°, голова цели клюёт, у листа морда горизонтально).
Ключ — форма в покое, поза — отдельным файлом (вариант Б), поэтому позу можно править отдельно от формы: кость
поворачивается вокруг оси X мира (сагиттальная плоскость) через свою голову до заданного наклона, дети едут за ней.

  blender -b --factory-startup -P poza_po_listu.py -- шасси.blend поза.json выход.json --naklon хребет:31,шея:50,голова:20 [--derzhat таз]
`--derzhat` — кости, которые остаются на месте в мире, когда поворачивается родитель (таз с ногами: хребет — корень, и
без этого наклон спины опрокидывал бы ноги).
Наклон — градусы вперёд от вертикали (вперёд = −Y Blender), как в листе на профиле. Печатает «ПОЗА: …».
"""
import sys, json, math
import bpy
from mathutils import Vector, Matrix

a = sys.argv[sys.argv.index('--') + 1:]
src, p_in, p_out = a[:3]
opt = {}
i = 3
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
DERZHAT = [x for x in opt.get('derzhat', 'таз').split(',') if x]
NAKLON = {kv.split(':')[0]: float(kv.split(':')[1]) for kv in opt.get('naklon', '').split(',') if kv}

bpy.ops.wm.open_mainfile(filepath=src)
pz = json.load(open(p_in, encoding='utf-8'))
PZ = {b['name']: b for b in pz['bones']}
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]; AW = arm.matrix_world
bpy.context.view_layer.objects.active = arm; bpy.ops.object.mode_set(mode='POSE')
order = []
def walk(b):
    order.append(b.name)
    for c in b.children: walk(c)
for b in arm.data.bones:
    if b.parent is None: walk(b)


def put(pb, h, t, z):
    y = (t - h).normalized(); x = y.cross(z).normalized(); z2 = x.cross(y)
    pb.matrix = AW.inverted() @ (Matrix.Translation(h) @ Matrix((x, y, z2)).transposed().to_4x4())


# 1. поза из файла
for bn in order:
    if bn in PZ:
        put(arm.pose.bones[bn], Vector(PZ[bn]['head']), Vector(PZ[bn]['tail']), Vector(PZ[bn]['z_axis']))
        bpy.context.view_layer.update()


def naklon(pb):
    d = (AW @ pb.tail) - (AW @ pb.head)
    return math.degrees(math.atan2(-d.y, d.z))


# 2. правка наклонов — по иерархии, дети едут за родителем, кроме `--derzhat`
bpy.context.view_layer.update()
HOLD = {bn: AW @ arm.pose.bones[bn].matrix for bn in DERZHAT if bn in arm.pose.bones}
for bn in order:
    if bn not in NAKLON: continue
    pb = arm.pose.bones[bn]; bpy.context.view_layer.update()
    was = naklon(pb); dlt = math.radians(NAKLON[bn] - was)
    hw = AW @ pb.head
    # поворот вокруг +X на +θ кладёт ось Z вперёд, в −Y
    R = Matrix.Translation(hw) @ Matrix.Rotation(dlt, 4, 'X') @ Matrix.Translation(-hw)
    pb.matrix = AW.inverted() @ (R @ (AW @ pb.matrix))
    bpy.context.view_layer.update()
    for hb, Mh in HOLD.items():
        arm.pose.bones[hb].matrix = AW.inverted() @ Mh; bpy.context.view_layer.update()
    print('ПОЗА: %s %.0f° → %.0f°' % (bn, was, naklon(pb)))

# 3. запись в том же формате
out = []
for b in pz['bones']:
    pb = arm.pose.bones.get(b['name'])
    if pb is None: continue
    M = AW @ pb.matrix
    z = (M.to_3x3() @ Vector((0, 0, 1))).normalized()
    out.append(dict(b, head=[round(c, 5) for c in AW @ pb.head], tail=[round(c, 5) for c in AW @ pb.tail], z_axis=[round(c, 5) for c in z]))
pz['bones'] = out
pz['note'] = pz.get('note', '') + '; наклоны по листу: ' + ', '.join('%s %g°' % kv for kv in NAKLON.items()) + ' (poza_po_listu.py)'
json.dump(pz, open(p_out, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
print('ПОЗА: готово →', p_out)
