"""3/4 СЗАДИ ПРИ ДВУХ НАПРАВЛЕНИЯХ СВЕТА (05.11): тёмно при обоих — форма, при одном — свет. Workbench, поворот студийного света 0° и 150°, крупно и с дистанции (фигура ~200 px).

  blender -b тело.blend --factory-startup -P dva_sveta.py -- папка префикс
"""
import sys, math, os
import bpy
from mathutils import Vector
a = sys.argv[sys.argv.index('--') + 1:]; OUT, PR = os.path.abspath(a[0]), a[1]; sc = bpy.context.scene; sc.render.engine = 'BLENDER_WORKBENCH'; w = sc.display.shading
w.light = 'STUDIO'; w.color_type = 'OBJECT'; w.show_backface_culling = True; w.show_shadows = False; w.show_cavity = False; w.use_world_space_lighting = True
sc.world = sc.world or bpy.data.worlds.new('w'); sc.world.color = (0.82, 0.83, 0.84); sc.render.image_settings.file_format = 'PNG'
for o in bpy.data.objects:
    if o.type == 'MESH':
        o.hide_render = '@' in o.name; o.color = (0.72, 0.66, 0.60, 1)
        for p in o.data.polygons: p.use_smooth = False
d = bpy.data.cameras.new('c'); d.type = 'ORTHO'; d.ortho_scale = 2.05; c = bpy.data.objects.new('c', d); sc.collection.objects.link(c); y, n = math.radians(218), math.radians(8); nap = Vector((math.sin(y) * math.cos(n), -math.cos(y) * math.cos(n), math.sin(n))); c.location = Vector((0, 0, 0.93)) + nap * 6; c.rotation_euler = (-nap).to_track_quat('-Z', 'Y').to_euler(); sc.camera = c
for s_, ug in (('a', 0), ('b', 150)):
    w.studiolight_rotate_z = math.radians(ug)
    for naz, rx, ry in (('', 420, 620), ('_dal', 150, 222)):
        sc.render.resolution_x, sc.render.resolution_y = rx, ry; sc.render.filepath = os.path.join(OUT, '%s_svet%s%s.png' % (PR, s_, naz)); bpy.ops.render.render(write_still=True)
