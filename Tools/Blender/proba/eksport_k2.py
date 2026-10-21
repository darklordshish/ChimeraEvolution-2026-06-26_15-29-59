# ВЫГРУЗКА меша шасси с ключами (видов и областей) и кусками @ в FBX игры — оси и параметры как у kluch_vida.py:
#   blender -b --factory-startup -P eksport_k2.py -- сборка.blend Assets/_Chimera/Models/chelovek_mesh.fbx
import bpy, sys
a = sys.argv[sys.argv.index('--') + 1:]
bpy.ops.wm.open_mainfile(filepath=a[0])
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
for o in bpy.data.objects: o.select_set(o.type in ('ARMATURE', 'MESH'))
bpy.context.view_layer.objects.active = arm
bpy.ops.export_scene.fbx(filepath=a[1], use_selection=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                         object_types={'ARMATURE', 'MESH'}, add_leaf_bones=False, bake_anim=False, mesh_smooth_type='FACE', use_armature_deform_only=False)
print('ЭКСПОРТ готово')
