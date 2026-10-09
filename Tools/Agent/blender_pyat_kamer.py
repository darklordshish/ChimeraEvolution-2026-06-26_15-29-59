"""ПЯТЬ КАМЕР ДЛЯ ЛЮБОГО МЕША — честное сравнение «наш волк против пробного меша» (проба (б), 09.10).

Один и тот же свет, материал и камеры для любого входа: тело из игры (`Tools/Agent/ExportBody.cs` → `*-field.obj` +
`*-parts.obj`) и пробный меш (`.obj` / `.glb` / `.fbx`). Меш выравнивается по земле и по центру, масштабируется к
высоте в холке (по умолчанию 1.17 м — параметр проекта; холка — верх корпуса у передних ног, здесь — верх силуэта над
передней четвертью длины; грива сбивает эту мерку — лучше подавать меш уже в метрах). По умолчанию НЕ масштабирует:
вход в метрах. Морда — в −Y Blender (= +Z Unity, как зверь в игре); если вход смотрит иначе — `--yaw`.

    blender -b --factory-startup -P Tools/Agent/blender_pyat_kamer.py -- ВЫХОД_ПРЕФИКС ФАЙЛ [ФАЙЛ …] [--holka 1.17 — только если вход не в метрах] [--yaw 0]
      → ПРЕФИКС_{front,34front,side,back,34back}.png, 700×700, ортокамера, плоское затенение, серый материал

Кадры одного масштаба: рамка 2.8 м у всех, так что «наш» и «пробный» кладутся рядом без подгонки.
"""
import math
import os
import sys

import bpy
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
holka, yaw, files = 0.0, 0.0, []
i = 0
out = argv[0]; i = 1
while i < len(argv):
    if argv[i] == "--holka": holka = float(argv[i + 1]); i += 2
    elif argv[i] == "--yaw": yaw = float(argv[i + 1]); i += 2
    else: files.append(argv[i]); i += 1

bpy.ops.wm.read_factory_settings(use_empty=True)
objs = []
for f in files:
    ext = os.path.splitext(f)[1].lower()
    before = set(bpy.data.objects)
    if ext == ".obj": bpy.ops.wm.obj_import(filepath=f, forward_axis='NEGATIVE_Z', up_axis='Y')
    elif ext in (".glb", ".gltf"): bpy.ops.import_scene.gltf(filepath=f)
    elif ext == ".fbx": bpy.ops.import_scene.fbx(filepath=f)
    objs += [o for o in bpy.data.objects if o not in before and o.type == 'MESH']

root = bpy.data.objects.new("корень", None); bpy.context.scene.collection.objects.link(root)
for o in objs:
    o.parent = root
    for p in o.data.polygons: p.use_smooth = False
root.rotation_euler = (0, 0, math.radians(yaw))
bpy.context.view_layer.update()

def bounds():
    pts = [o.matrix_world @ v.co for o in objs for v in o.data.vertices]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return pts, lo, hi

pts, lo, hi = bounds()
# холка — верх силуэта в передней четверти длины без головы: берём полосу 25–40 % длины от морды (морда в −Y)
L = hi.y - lo.y
band = [p.z for p in pts if lo.y + 0.25 * L <= p.y <= lo.y + 0.40 * L]
wither = (max(band) - lo.z) if band else (hi.z - lo.z)
k = holka / max(1e-6, wither) if holka > 0 else 1.0   # 0 — вход уже в метрах тела (тело из игры)
root.scale = (k, k, k)
bpy.context.view_layer.update()
pts, lo, hi = bounds()
root.location = Vector((-(lo.x + hi.x) / 2, -(lo.y + hi.y) / 2, -lo.z))
bpy.context.view_layer.update()

sc = bpy.context.scene
sc.render.engine = 'BLENDER_WORKBENCH'
sh = sc.display.shading
sh.light = 'STUDIO'; sh.color_type = 'SINGLE'; sh.single_color = (0.72, 0.72, 0.72); sh.show_cavity = False
sh.background_type = 'VIEWPORT'; sh.background_color = (0.15, 0.16, 0.18)
sc.render.resolution_x = sc.render.resolution_y = 700
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam
cam.data.type = 'ORTHO'; cam.data.ortho_scale = 2.8
c = Vector((0, 0, 0.8))
views = {
    "front": Vector((0, -1, 0)), "back": Vector((0, 1, 0)), "side": Vector((1, 0, 0)),
    "34front": Vector((math.sin(math.radians(35)), -math.cos(math.radians(35)), 0.15)),
    "34back": Vector((math.sin(math.radians(35)), math.cos(math.radians(35)), 0.2)),
}
for name, eye in views.items():
    eye = eye.normalized()
    cam.location = c + eye * 10
    cam.rotation_euler = (c - cam.location).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = f"{out}_{name}.png"
    bpy.ops.render.render(write_still=True)
print("ПЯТЬ КАМЕР:", out, "холка", round(wither * k, 3), "масштаб", round(k, 3), "мешей", len(objs))
