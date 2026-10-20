"""СВЕСТИ КЛЮЧИ В ОДИН МЕШ ШАССИ (19.10, второй ключ — «Лось» на человеке К2). `kluch_vida.py` строит ключ одного вида на
чистом шасси, а `kusok_celi.py` добавляет объекты черт `@<вид>`. В игре у человека один меш, и в нём должны жить ключи
ВСЕХ видов и черты всех ступеней. Сводка: база — сборка с ключом (ключами) и чертами; из другой сборки переносятся её
форма-ключ (по объектам шасси с тем же числом вершин) и её объекты `@…` (с арматурой базы и теми же группами костей).

  blender -b --factory-startup -P svesti_klyuchi.py -- база.blend другая.blend ключ выход.fbx [--cherty @Лось,@Лось1] [--blend выход.blend]
"""
import sys, os
import bpy

a = sys.argv[sys.argv.index('--') + 1:]
base, other, key, out = a[:4]
opt = {}
i = 4
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
want = [n for n in opt.get('cherty', '').split(',') if n]

bpy.ops.wm.open_mainfile(filepath=base)
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
base_objs = set(bpy.data.objects)
# объекты другой сборки — по именам (одноимённые Blender при загрузке переименовывает в «имя.001» — суффикс снимаем)
import re
with bpy.data.libraries.load(other, link=False) as (src, dst):
    dst.objects = [n for n in src.objects]
others = {re.sub(r'\.\d{3}$', '', o.name): o for o in dst.objects if o is not None and o.type == 'MESH' and o not in base_objs}
moved = 0
for o in [o for o in base_objs if o.type == 'MESH' and not o.name.startswith('@')]:
    s = others.get(o.name)
    if s is None or s.data.shape_keys is None or key not in s.data.shape_keys.key_blocks:
        continue
    assert len(s.data.vertices) == len(o.data.vertices), 'объект %s: вершин %d против %d' % (o.name, len(s.data.vertices), len(o.data.vertices))
    if o.data.shape_keys is None:
        o.shape_key_add(name='Basis')
    if key in o.data.shape_keys.key_blocks:
        o.shape_key_remove(o.data.shape_keys.key_blocks[key])
    kb = o.shape_key_add(name=key, from_mix=False)
    sk = s.data.shape_keys.key_blocks[key]
    for vi in range(len(o.data.vertices)):
        kb.data[vi].co = sk.data[vi].co
    moved += 1
print('СВОДКА: ключ «%s» перенесён в %d объектов шасси' % (key, moved))
for n in want:
    s = others.get(n)
    assert s is not None, 'нет объекта черты %s в %s' % (n, other)
    old = next((o for o in base_objs if o.name == n), None)
    if old is not None:
        bpy.data.objects.remove(old, do_unlink=True)
    s.name = n
    bpy.context.scene.collection.objects.link(s)
    s.parent = arm
    for md in list(s.modifiers):
        s.modifiers.remove(md)
    md = s.modifiers.new('Скелет', 'ARMATURE'); md.object = arm
    print('СВОДКА: черта %s — %d тр' % (n, len(s.data.polygons)))
bpy.ops.object.select_all(action='DESELECT')
for o in bpy.context.scene.objects:
    if o.type in ('ARMATURE', 'MESH'):
        o.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.export_scene.fbx(filepath=os.path.abspath(out), use_selection=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z',
                         axis_up='Y', object_types={'ARMATURE', 'MESH'}, add_leaf_bones=False, bake_anim=False,
                         mesh_smooth_type='FACE', use_armature_deform_only=False)
if 'blend' in opt:
    bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(opt['blend']))
print('СВОДКА: готово → %s' % out)
