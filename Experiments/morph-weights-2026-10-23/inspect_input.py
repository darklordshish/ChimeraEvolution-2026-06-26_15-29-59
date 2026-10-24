import bpy, sys, json
from pathlib import Path
root = Path(sys.argv[sys.argv.index('--') + 1]).resolve()
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(root / 'inputs/chelovek_mesh.fbx'))
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
rows = []
for o in bpy.data.objects:
    if o.type != 'MESH': continue
    rows.append(dict(name=o.name, vertices=len(o.data.vertices), matrix=[list(r) for r in o.matrix_world],
                     keys=[k.name for k in o.data.shape_keys.key_blocks] if o.data.shape_keys else [],
                     over4=sum(len(v.groups)>4 for v in o.data.vertices)))
    if o.data.shape_keys:
        for k in o.data.shape_keys.key_blocks: k.value=0
print(json.dumps(rows, ensure_ascii=False))
print('ARM', arm.name, list(arm.matrix_world), 'bones', len(arm.data.bones))
bpy.ops.wm.save_as_mainfile(filepath=str(root / 'inputs/source.blend'))
for o in list(bpy.data.objects):
    if o.type == 'MESH' and '@' in o.name:
        bpy.data.objects.remove(o, do_unlink=True)
bpy.ops.wm.save_as_mainfile(filepath=str(root / 'inputs/template.blend'))
(root / 'input-inspection.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf-8')
