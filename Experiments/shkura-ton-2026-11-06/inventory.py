import bpy, json, sys
from pathlib import Path
p=Path(__file__).parent
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(p/'source.fbx'))
out=[]
for o in bpy.data.objects:
    if o.type!='MESH': continue
    o.data.calc_loop_triangles()
    out.append(dict(name=o.name,verts=len(o.data.vertices),tris=len(o.data.loop_triangles),keys=[k.name for k in o.data.shape_keys.key_blocks] if o.data.shape_keys else [],bounds=[[round(min((o.matrix_world@v.co)[i] for v in o.data.vertices),3),round(max((o.matrix_world@v.co)[i] for v in o.data.vertices),3)] for i in range(3)]))
(p/'inventory.json').write_text(json.dumps(out,ensure_ascii=False,indent=2),encoding='utf8')
bpy.ops.wm.save_as_mainfile(filepath=str(p/'source.blend'))
print(json.dumps(out,ensure_ascii=False))
