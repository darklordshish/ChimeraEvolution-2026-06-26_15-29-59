import bpy,numpy as np,json
from pathlib import Path
root=Path(__file__).resolve().parent
def snapshot(path):
    bpy.ops.wm.open_mainfile(filepath=str(path));result={}
    arm=next(o for o in bpy.data.objects if o.type=='ARMATURE')
    for o in bpy.data.objects:
        if o.type!='MESH' or '@' in o.name:continue
        result[o.name]=dict(base=np.array([v.co[:] for v in o.data.vertices]),keys={k.name:np.array([p.co[:] for p in k.data]) for k in o.data.shape_keys.key_blocks} if o.data.shape_keys else {},faces=[tuple(f.vertices) for f in o.data.polygons],weights=[[(g.group,g.weight) for g in v.groups] for v in o.data.vertices])
    return result,{b.name:np.array(arm.matrix_world@b.matrix_local) for b in arm.data.bones}
old,b0=snapshot(root.parent/'abdomen-refine-2026-10-31/candidate.blend');new,b1=snapshot(root/'combinations.blend');errors=[]
for n,a in old.items():
    b=new[n]
    if a['faces']!=b['faces'] or a['weights']!=b['weights'] or not np.array_equal(a['base'],b['base']):errors.append(n+' base/topology/weights changed')
    for k,values in a['keys'].items():
        if not np.array_equal(values,b['keys'][k]):errors.append(n+' key changed '+k)
for n,m in b0.items():
    if not np.array_equal(m,b1[n]):errors.append(n+' rest bone changed')
for o in bpy.data.objects:
    if o.type!='MESH' or '@' not in o.name:continue
    for v in o.data.vertices:
        ws=[g.weight for g in v.groups if o.vertex_groups[g.group].name in b1 and g.weight>0]
        if len(ws)>4 or abs(sum(ws)-1)>1e-4:errors.append(o.name+' invalid weights')
cases=json.loads((root/'cases.json').read_text(encoding='utf-8'))['cases']
for c in cases:
    slots=[n.split('@')[0] for n in c['organs']]
    if len(slots)!=len(set(slots)):errors.append(c['id']+' duplicate organ slots')
report=dict(errors=errors,body_meshes_unchanged=list(old),bones=len(b1),visual_cases=len(cases),note='Checks asset preservation, weights and duplicate visual slots only. Not gameplay legality, growth-envelope or collision validation.')
(root/'verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8');print('VERIFY',report);assert not errors,errors
