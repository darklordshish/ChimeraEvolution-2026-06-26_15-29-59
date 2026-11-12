# ВЫГРУЗКА меша шасси с ключами (видов и областей) и кусками @ в FBX игры — оси и параметры как у kluch_vida.py:
#   blender -b --factory-startup -P eksport_k2.py -- сборка.blend Assets/_Chimera/Models/chelovek_mesh.fbx
import bpy, sys
a = sys.argv[sys.argv.index('--') + 1:]
bpy.ops.wm.open_mainfile(filepath=a[0])
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
# все формы-ключи — на 0 (svesti_klyuchi оставлял «Лось» включённым на 1: в Blender-кадрах тело раздувалось «капюшоном»;
# вес ставит игра, в FBX веса по умолчанию — нули). Группы областей «*.область» — служебные (ключи областей уже запечены)
for o in [x for x in bpy.data.objects if x.type == 'MESH']:
    if o.data.shape_keys:
        for kb in o.data.shape_keys.key_blocks: kb.value = 0.0
    for g in [g for g in o.vertex_groups if g.name.endswith('.область')]: o.vertex_groups.remove(g)
# ВЕСА ЗДЕСЬ НЕ МЕНЯЮТСЯ, ТОЛЬКО ПРОВЕРЯЮТСЯ (27.10, пункт А разбора тракта): смена весов после снятия ключа уводит ключ в позе
# (опыт дизайн-линии: до 87 мм; ограничение 4 костями здесь же сдвигало ключ «Лось» на руках до 31 мм). Веса доводит
# vesa_final.py — с перевязкой ключей; тут — что игра получит то же, что Blender: не больше 4 костей (импорт Unity режет до 4),
# сумма 1, швы шасси (вершины разных объектов в одной точке) — с одинаковыми весами
bones_ = {b.name for b in arm.data.bones}; bad = []; seen = {}
for o in [x for x in bpy.data.objects if x.type == 'MESH']:
    gn = {g.index: g.name for g in o.vertex_groups}
    for v in o.data.vertices:
        ws = {gn[g.group]: g.weight for g in v.groups if gn[g.group] in bones_ and g.weight > 0}
        if len(ws) > 4: bad.append('%s:%d — костей %d' % (o.name, v.index, len(ws)))
        elif ws and abs(sum(ws.values()) - 1) > 1e-3: bad.append('%s:%d — сумма %.4f' % (o.name, v.index, sum(ws.values())))
        if '@' in o.name: continue
        w_ = o.matrix_world @ v.co; k = (round(w_.x, 4), round(w_.y, 4), round(w_.z, 4))
        if k not in seen: seen[k] = (o.name, ws)
        elif any(abs(seen[k][1].get(n, 0) - ws.get(n, 0)) > 1e-4 for n in set(ws) | set(seen[k][1])): bad.append('%s:%d — шов с %s' % (o.name, v.index, seen[k][0]))
print('ВЕСА: нарушений %d%s' % (len(bad), (' — ' + '; '.join(bad[:5])) if bad else ''))
if bad: raise SystemExit('ВЕСА не финальные — сначала vesa_final.py (с перевязкой ключей), потом oblasti_k2.py')
for o in bpy.data.objects: o.select_set(o.type in ('ARMATURE', 'MESH'))
bpy.context.view_layer.objects.active = arm
bpy.ops.export_scene.fbx(filepath=a[1], use_selection=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                         object_types={'ARMATURE', 'MESH'}, add_leaf_bones=False, bake_anim=False, mesh_smooth_type='FACE', use_armature_deform_only=False)
print('ЭКСПОРТ готово')
