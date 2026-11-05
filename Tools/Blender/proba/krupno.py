"""КРУПНЫЙ ПЛАН ТЕЛА (05.11, Б7): орто-кадры заданных мест одним светом, чтобы правки лица, кисти и стопы сравнивать с листом. Workbench, плоские грани, только объекты базы.

  blender -b тело.blend --factory-startup -P krupno.py -- выход.png имя:x,y,z:r:az:el[:px] …   (az 0 — анфас (к −Y), 90 — с +X, 180 — сзади; el — вверх, r — половина кадра в м)
Кадры склеиваются в одну картинку рядом; PIL в Blender нет, поэтому клеит numpy через bpy.data.images.
"""
import sys, math, os
import bpy
import numpy as np
from mathutils import Vector
a = sys.argv[sys.argv.index('--') + 1:]; OUT = os.path.abspath(a[0]); sc = bpy.context.scene; sc.render.engine = 'BLENDER_WORKBENCH'; w = sc.display.shading
w.light = 'STUDIO'; w.color_type = 'OBJECT'; w.show_backface_culling = True; w.show_shadows = False; w.show_cavity = False; w.use_world_space_lighting = False
sc.world = sc.world or bpy.data.worlds.new('w'); sc.world.color = (0.82, 0.83, 0.84); sc.render.image_settings.file_format = 'PNG'
for o in bpy.data.objects:
    if o.type == 'MESH':
        o.hide_render = '@' in o.name or o.name == 'Cube'; o.color = (0.62, 0.61, 0.6, 1)
        for p in o.data.polygons: p.use_smooth = False
        o.data.update()
cam = bpy.data.objects.new('к', bpy.data.cameras.new('к')); sc.collection.objects.link(cam); cam.data.type = 'ORTHO'; sc.camera = cam
K = []
for i, s in enumerate(a[1:]):
    q = s.split(':'); c = Vector([float(x) for x in q[1].split(',')]); r = float(q[2]); az = math.radians(float(q[3])); el = math.radians(float(q[4])); px = int(q[5]) if len(q) > 5 else 420
    d = Vector((math.sin(az) * math.cos(el), -math.cos(az) * math.cos(el), math.sin(el)))
    cam.location = c + d * 3; cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler(); cam.data.ortho_scale = 2 * r; cam.data.clip_end = 10
    sc.render.resolution_x = sc.render.resolution_y = px; f = OUT + '.%d.png' % i; sc.render.filepath = f; bpy.ops.render.render(write_still=True); K.append(f)
Im = [bpy.data.images.load(f) for f in K]; W = sum(m.size[0] for m in Im); H = max(m.size[1] for m in Im); A = np.ones((H, W, 4)); x = 0
for m in Im:
    w_, h_ = m.size; b = np.array(m.pixels[:]).reshape(h_, w_, 4); A[H - h_:H, x:x + w_] = b[:] if False else b; x += w_
A[:, :, 3] = 1; R = bpy.data.images.new('р', W, H); R.pixels = A.ravel().tolist(); R.filepath_raw = OUT; R.file_format = 'PNG'; R.save()
for f in K: os.remove(f)
