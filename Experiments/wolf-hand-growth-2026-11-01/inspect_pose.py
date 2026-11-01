import bpy,numpy as np
from pathlib import Path
r=Path.cwd()/'Experiments/wolf-hand-growth-2026-11-01';bpy.ops.wm.open_mainfile(filepath=str(r/'original.blend'))
a=next(o for o in bpy.data.objects if o.type=='ARMATURE')
for b in a.pose.bones:
 e=np.max(abs(np.array(b.matrix_basis)-np.eye(4)))
 if e>1e-5:print('POSE',b.name,e)
o=bpy.data.objects['Руки'];v=o.data.vertices[583];k=o.data.shape_keys.key_blocks['Basis'];ev=o.evaluated_get(bpy.context.evaluated_depsgraph_get());me=ev.to_mesh()
print('POINT',list(o.matrix_world@v.co),list(o.matrix_world@k.data[583].co),list(ev.matrix_world@me.vertices[583].co),'KEYVALUES',[(b.name,b.value) for b in o.data.shape_keys.key_blocks]);print('ARM-MW',a.matrix_world)
