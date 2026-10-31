import bpy,sys,json
from pathlib import Path
from mathutils import Matrix
a=sys.argv[sys.argv.index('--')+1:]
def capture(path):
    bpy.ops.wm.open_mainfile(filepath=path)
    arm=next(o for o in bpy.data.objects if o.type=='ARMATURE')
    for pb in arm.pose.bones:pb.matrix_basis=Matrix.Identity(4)
    meshes={};bad_weights=0;maximum=0
    for o in bpy.data.objects:
        if o.type!='MESH' or '@' in o.name or not o.data.shape_keys:continue
        if 'Поджарый' not in o.data.shape_keys.key_blocks:continue
        for k in o.data.shape_keys.key_blocks:k.value=1 if k.name=='Поджарый' else 0
        for v in o.data.vertices:
            ws=[g.weight for g in v.groups if o.vertex_groups[g.group].name in arm.data.bones and g.weight>1e-8]
            maximum=max(maximum,len(ws));bad_weights+=len(ws)>4 or abs(sum(ws)-1)>1e-5
    bpy.context.view_layer.update();dg=bpy.context.evaluated_depsgraph_get()
    for o in bpy.data.objects:
        if o.type!='MESH' or '@' in o.name or not o.data.shape_keys or 'Поджарый' not in o.data.shape_keys.key_blocks:continue
        e=o.evaluated_get(dg);me=e.to_mesh()
        meshes[o.name]=dict(base=[tuple(v.co) for v in o.data.vertices],
            keys={k.name:[tuple(v.co) for v in k.data] for k in o.data.shape_keys.key_blocks},
            evaluated=[tuple(o.matrix_world@v.co) for v in me.vertices],triangles=sum(len(p.vertices)-2 for p in o.data.polygons))
        e.to_mesh_clear()
    return meshes,{b.name:[list(r) for r in b.matrix_local] for b in arm.data.bones},bad_weights,maximum
s,srig,_,_=capture(a[0]);d,drig,bad,maximum=capture(a[1]);errors=[];delta=0
if s.keys()!=d.keys():errors.append('Object inventory changed')
for n,b in srig.items():
    if b!=drig.get(n):errors.append('Existing bone changed: '+n)
if set(drig)-set(srig)!={'опора_подмышки','опора_подмышки.L'}:errors.append('Unexpected extra bones')
for n,v in s.items():
    for field in ('base','keys','triangles'):
        if v[field]!=d[n][field]:errors.append(n+': geometry changed: '+field)
    for p,q in zip(v['evaluated'],d[n]['evaluated']):delta=max(delta,sum((x-y)**2 for x,y in zip(p,q))**.5)
if delta>1e-5:errors.append('Neutral deformation drift')
if bad:errors.append('Invalid skin weights')
report=dict(errors=errors,neutral_max_delta_m=delta,invalid_weights=bad,max_influences=maximum,
            original_bones=len(srig),candidate_bones=len(drig),triangles=sum(v['triangles'] for v in d.values()))
Path(a[2]).write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print('VERIFY_RIG',report)
assert not errors,errors
