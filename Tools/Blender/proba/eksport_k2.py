# ВЫГРУЗКА меша шасси с ключами (видов и областей) и кусками @ в FBX игры — оси и параметры как у kluch_vida.py:
#   blender -b --factory-startup -P eksport_k2.py -- сборка.blend Assets/_Chimera/Models/chelovek_mesh.fbx
import bpy, sys
a = sys.argv[sys.argv.index('--') + 1:]
bpy.ops.wm.open_mainfile(filepath=a[0])
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
# ВЕСА — НЕ БОЛЬШЕ 4 КОСТЕЙ с нормализацией (ГеймБосс 22.10: импорт Unity режет до 4, у «Сердце» урезалось 54 % вершин, у @Волк 934 —
# игра гнула их не как Blender: «нагрудник», дыра в гриве, тёмный пояс). Группы областей «*.область» — служебные (ключи областей
# уже запечены), в выгрузку не идут
# все формы-ключи — на 0 (svesti_klyuchi оставлял «Лось» включённым на 1: в Blender-кадрах тело раздувалось «капюшоном»;
# вес ставит игра, в FBX веса по умолчанию — нули)
for o in [x for x in bpy.data.objects if x.type == 'MESH' and x.data.shape_keys]:
    for kb in o.data.shape_keys.key_blocks: kb.value = 0.0
bones_ = {b.name for b in arm.data.bones}
for o in [x for x in bpy.data.objects if x.type == 'MESH']:
    for g in [g for g in o.vertex_groups if g.name.endswith('.область')]: o.vertex_groups.remove(g)
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.object.vertex_group_limit_total(group_select_mode='BONE_DEFORM', limit=4)
    bpy.ops.object.vertex_group_normalize_all(group_select_mode='BONE_DEFORM', lock_active=False)
    over = sum(1 for v in o.data.vertices if sum(1 for g in v.groups if o.vertex_groups[g.group].name in bones_ and g.weight > 0) > 4)
    print('ВЕСА: %s — вершин с >4 костями после ограничения: %d' % (o.name, over))
for o in bpy.data.objects: o.select_set(o.type in ('ARMATURE', 'MESH'))
bpy.context.view_layer.objects.active = arm
bpy.ops.export_scene.fbx(filepath=a[1], use_selection=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                         object_types={'ARMATURE', 'MESH'}, add_leaf_bones=False, bake_anim=False, mesh_smooth_type='FACE', use_armature_deform_only=False)
print('ЭКСПОРТ готово')
