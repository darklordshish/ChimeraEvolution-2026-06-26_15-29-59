"""Independent check: Blender's evaluated Armature modifier against saved fit."""
import bpy, sys, json
import numpy as np
from pathlib import Path
from mathutils import Matrix, Vector
a=sys.argv[sys.argv.index('--')+1:]; root=Path(a[0]).resolve()
bpy.ops.wm.open_mainfile(filepath=str(root/'corrected.blend'))
pose={b['name']:b for b in json.loads((root/'pose.json').read_text(encoding='utf-8'))['bones']}
arm=next(o for o in bpy.data.objects if o.type=='ARMATURE'); aw=arm.matrix_world
# The game's morphed joint offsets are independent of the rest-pose parent's tail.
# FBX import marked shoulders as connected; Blender then ignores their new translation.
# Detach the preview connection flag only; retain parent hierarchy and bind matrices.
bpy.context.view_layer.objects.active=arm
bpy.ops.object.mode_set(mode='EDIT')
connected=[b.name for b in arm.data.edit_bones if b.use_connect]
for b in arm.data.edit_bones: b.use_connect=False
bpy.ops.object.mode_set(mode='OBJECT')
print('PREVIEW_DISCONNECTED',connected)
for o in bpy.data.objects:
    if o.type=='MESH' and o.data.shape_keys:
        for k in o.data.shape_keys.key_blocks: k.value=float(k.name=='Волк')
order=[]
def walk(b):
    order.append(b.name)
    for child in b.children: walk(child)
for b in arm.data.bones:
    if b.parent is None: walk(b)
for bn in order:
    pb=arm.pose.bones[bn]
    if pb.name not in pose: continue
    p=pose[pb.name]; h,t,z=(Vector(p[k]) for k in ('head','tail','z_axis'))
    y=(t-h).normalized(); x=y.cross(z).normalized(); z=x.cross(y)
    pb.matrix=aw.inverted() @ Matrix.Translation(h) @ Matrix((x,y,z)).transposed().to_4x4()
    bpy.context.view_layer.update()
target=[]
for l in (root/'posed.obj').read_text().splitlines():
    if l.startswith('v '):
        x,y,z=map(float,l.split()[1:4]); target.append((x,-z,y))
target=np.array(target); ids={}; errors=[]; rows=[]
dg=bpy.context.evaluated_depsgraph_get()
print('BONES',[(b.name,b.use_deform) for b in arm.data.bones if not b.use_deform])
print('MODIFIERS',[(o.name,[(m.type,getattr(m,'use_deform_preserve_volume',None)) for m in o.modifiers]) for o in bpy.data.objects if o.type=='MESH'])
sys.path.insert(0,str(root))
from check_roundtrip import skin_matrices,vertex_matrix
pred=skin_matrices(arm,pose)
for pb in arm.pose.bones:
    if pb.name in pred:
        actual=np.array(aw @ pb.matrix @ pb.bone.matrix_local.inverted() @ aw.inverted())
        delta=float(np.max(np.abs(actual-pred[pb.name])))
        if delta>0.001: print('MATRIX_MISMATCH',pb.name,delta)
for o in bpy.data.objects:
    if o.type!='MESH' or '@' in o.name: continue
    ev=o.evaluated_get(dg); me=ev.to_mesh(); errs=[]
    print('KEYS',o.name,[(k.name,k.value,k.relative_key.name,k.mute) for k in o.data.shape_keys.key_blocks if k.value])
    assert len(me.vertices)==len(o.data.vertices)
    for v in o.data.vertices:
        b=o.matrix_world @ v.co; key=tuple(round(float(x),5) for x in b)
        idx=ids.setdefault(key,len(ids))
        p=np.array(ev.matrix_world @ me.vertices[v.index].co)
        errs.append(float(np.linalg.norm(p-target[idx])))
    errors.extend(errs); rows.append(dict(object=o.name,max_m=max(errs)))
    ev.to_mesh_clear()
res=dict(max_m=max(errors),mean_m=float(np.mean(errors)),objects=rows)
(root/'blender-check.json').write_text(json.dumps(res,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(res,ensure_ascii=False))
assert res['max_m']<0.0001,res
