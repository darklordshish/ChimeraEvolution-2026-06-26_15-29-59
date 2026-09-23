# ПРОТОТИП «мельче клетка + прореживание» (модельная линия, 23.09): склеить оболочку, прорядить (QEM, симметрично по X),
# выгрузить только её.  blender -b -P decimate.py -- in.obj out.obj бюджет_треугольников
import sys
import bpy
a = sys.argv[sys.argv.index('--') + 1:]
src, dst, budget = a[0], a[1], int(a[2])
for o in list(bpy.data.objects):
    bpy.data.objects.remove(o, do_unlink=True)
bpy.ops.wm.obj_import(filepath=src, forward_axis='NEGATIVE_Z', up_axis='Y')
fields = [o for o in bpy.context.scene.objects if o.name.startswith('field')]
others = [o for o in bpy.context.scene.objects if not o.name.startswith('field')]
for o in others:
    bpy.data.objects.remove(o, do_unlink=True)
bpy.ops.object.select_all(action='DESELECT')
for o in fields:
    o.select_set(True)
bpy.context.view_layer.objects.active = fields[0]
bpy.ops.object.join()
f = bpy.context.view_layer.objects.active
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.remove_doubles(threshold=0.0005); bpy.ops.object.mode_set(mode='OBJECT')
n = sum(len(p.vertices) - 2 for p in f.data.polygons)
m = f.modifiers.new('dec', 'DECIMATE'); m.decimate_type = 'COLLAPSE'; m.ratio = budget / n
m.use_symmetry = True; m.symmetry_axis = 'X'
bpy.ops.object.modifier_apply(modifier=m.name)
m = f.modifiers.new('tri', 'TRIANGULATE'); bpy.ops.object.modifier_apply(modifier=m.name)
print('QEM %d -> %d' % (n, len(f.data.polygons)))
bpy.ops.wm.obj_export(filepath=dst, export_selected_objects=False, forward_axis='NEGATIVE_Z', up_axis='Y',
                      export_materials=False, export_normals=False, export_uv=False)
