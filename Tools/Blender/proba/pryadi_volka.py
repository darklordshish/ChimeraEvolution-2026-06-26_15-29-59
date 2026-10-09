"""ПРОБА (б): КОНТУРНЫЕ ПРЯДИ ГРИВЫ (критик, проход 2: «в тумане на 4–15 м гаснет светотень, остаётся контур, а его делает
рваный край»). 15–25 крупных прядей ПО СИЛУЭТУ, без лепки внутри массы: гребень затылок → холка, воротник на щеках,
манишка, край кисти хвоста. Прядь — плоский клин (основание w × t, длина l, 5 вершин, 6 тр), утоплен основанием в
поверхность на `sink`, смотрит по направлению своей линии. Точки линий снимаются с самого меша (верх по средней линии,
край по бокам), поэтому шаг переносим на любой проход доводки.

  blender -b --factory-startup -P pryadi_volka.py -- вход.obj выход.obj
Оси OBJ — как у `blender_pyat_kamer.py` (вперёд −Z, вверх Y); в Blender морда в −Y.
"""
import sys, math
import bpy, bmesh
from mathutils import Vector

a = sys.argv[sys.argv.index('--') + 1:]
src, out = a[0], a[1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.wm.obj_import(filepath=src, forward_axis='NEGATIVE_Z', up_axis='Y')
ob = [o for o in bpy.data.objects if o.type == 'MESH'][0]
bpy.context.view_layer.objects.active = ob; ob.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)   # импорт OBJ кладёт оси в поворот объекта
me = ob.data
from mathutils import Matrix
V = [v.co.copy() for v in me.vertices]
y0 = min(v.y for v in V); y1 = max(v.y for v in V); L = y1 - y0
FLIP = max(v.z for v in V if v.y > y1 - 0.25 * L) > max(v.z for v in V if v.y < y0 + 0.25 * L)
if FLIP:                                  # морда в +Y — развернуть, на выходе вернуть как было
    me.transform(Matrix.Rotation(math.pi, 4, 'Z'))
V = [v.co.copy() for v in me.vertices]
y0 = min(v.y for v in V); y1 = max(v.y for v in V); L = y1 - y0
SINK = 0.025


def sl(d0, d1):
    return [v for v in V if d0 <= (v.y - y0) / L < d1]


def top_mid(d):
    s = [v for v in sl(d - 0.02, d + 0.02) if abs(v.x) < 0.07]
    return max(s, key=lambda v: v.z) if s else None


def side_edge(d, z0, z1):
    s = [v for v in sl(d - 0.02, d + 0.02) if z0 < v.z < z1 and v.x > 0]
    return max(s, key=lambda v: v.x) if s else None


def front_mid(d0, d1, z):
    s = [v for v in sl(d0, d1) if abs(v.x) < 0.04 and abs(v.z - z) < 0.03]
    return min(s, key=lambda v: v.y) if s else None


strands = []          # (основание, направление, ширина-ось, w, l, t)


def add(p, d, side, w, l, t=0.018, mirror=False):
    print('  прядь у (%.2f %.2f %.2f) d=%.2f' % (p.x, p.y, p.z, (p.y - y0) / L))
    d = d.normalized(); side = (side - d * side.dot(d)).normalized()
    strands.append((p - d * SINK, d, side, w, l, t))
    if mirror:
        strands.append((Vector((-p.x, p.y, p.z)) - Vector((-d.x, d.y, d.z)) * SINK, Vector((-d.x, d.y, d.z)),
                        Vector((-side.x, side.y, side.z)), w, l, t))


X = Vector((1, 0, 0))
# 1. ГРЕБЕНЬ затылок → холка: 8 прядей вверх-назад, длиннее к холке
for i, d in enumerate([0.17, 0.20, 0.23, 0.26, 0.29, 0.32, 0.35, 0.38]):
    p = top_mid(d)
    if p is None:
        continue
    up = Vector((0, 1.0, 0.45))                   # ЛЕЖИТ назад по шее, чуть вверх (вверх — иглы динозавра, проба 09.10)
    add(p, up, X, 0.12 + 0.02 * (i % 2), 0.15 + 0.01 * i, t=0.035)
# 2. ВОРОТНИК на щеках: по 3 пряди с каждой стороны, вбок-назад-вниз
for d, z0, z1 in ((0.17, 1.05, 1.25), (0.20, 1.0, 1.2), (0.23, 0.95, 1.15)):
    p = side_edge(d, z0, z1)
    if p is not None:
        add(p, Vector((0.7, 0.8, -0.5)), Vector((0, 0, 1)), 0.11, 0.14, t=0.03, mirror=True)
# 3. МАНИШКА: 4 пряди вниз по переду груди
for z in (0.95, 0.85, 0.75, 0.66):
    p = front_mid(0.24, 0.40, z)          # за мордой: на 0.15 передняя точка — подбородок
    if p is not None:
        add(p, Vector((0, 0.35, -1.0)), X, 0.12, 0.13, t=0.03)
# 4. КРАЙ КИСТИ ХВОСТА: 4 пряди назад-вниз по заднему краю
for d in (0.90, 0.93, 0.96, 0.985):
    s = [v for v in sl(d - 0.01, d + 0.01) if abs(v.x) < 0.05]
    if not s:
        continue
    p = max(s, key=lambda v: v.y)                 # задний край
    add(p, Vector((0, 1.0, -0.8)), X, 0.10, 0.12, t=0.03)

bm = bmesh.new(); bm.from_mesh(me)
for p, d, side, w, l, t in strands:
    n = d.cross(side).normalized()
    base = [p + side * sx * w / 2 + n * sn * t / 2 for sx, sn in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    vs = [bm.verts.new(q) for q in base] + [bm.verts.new(p + d * l)]
    bm.faces.new(vs[3::-1])
    for k in range(4):
        bm.faces.new((vs[k], vs[(k + 1) % 4], vs[4]))
bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
bmesh.ops.triangulate(bm, faces=bm.faces[:])
bm.to_mesh(me); bm.free()
for f in me.polygons:
    f.use_smooth = False
if FLIP:
    me.transform(Matrix.Rotation(math.pi, 4, 'Z'))
print('ПРЯДИ: %d прядей, всего %d тр' % (len(strands), len(me.polygons)))
bpy.ops.wm.obj_export(filepath=out, export_selected_objects=False, forward_axis='NEGATIVE_Z', up_axis='Y',
                      export_materials=False, export_normals=False, export_uv=False)
