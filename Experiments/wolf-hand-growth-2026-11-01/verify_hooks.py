import bpy,numpy as np,json,math
from pathlib import Path
from mathutils import Matrix,Vector
root=Path(__file__).resolve().parent
def snapshot():
    result={}
    for o in bpy.data.objects:
        if o.type!='MESH' or '@' in o.name:continue
        result[o.name]=dict(base=np.array([v.co[:] for v in o.data.vertices]),faces=[tuple(f.vertices) for f in o.data.polygons],keys={k.name:np.array([p.co[:] for p in k.data]) for k in o.data.shape_keys.key_blocks} if o.data.shape_keys else {},weights=[[(g.group,g.weight) for g in v.groups] for v in o.data.vertices])
    return result
bpy.ops.wm.open_mainfile(filepath=str(root/'original.blend'));before=snapshot()
bpy.ops.wm.open_mainfile(filepath=str(root/'hooks.blend'));after=snapshot();errors=[]
for n,a in before.items():
    b=after[n]
    if a['faces']!=b['faces'] or a['weights']!=b['weights'] or not np.array_equal(a['base'],b['base']):errors.append(n+' altered')
    for k,p in a['keys'].items():
        if not np.array_equal(p,b['keys'][k]):errors.append(n+' key altered '+k)
body=bpy.data.objects['Руки'];claw=bpy.data.objects['Руки@Волк'];arm=next(o for o in bpy.data.objects if o.type=='ARMATURE')
meta=json.loads((root/'hooks.json').read_text(encoding='utf-8'));rows=[]
for pose in ('rest','forearm35'):
    for b in arm.pose.bones:b.matrix_basis=Matrix.Identity(4)
    bpy.context.view_layer.update()
    if pose=='forearm35':
        b=arm.pose.bones['предплечье'];aw=arm.matrix_world;h=aw@b.head
        b.matrix=aw.inverted()@Matrix.Translation(h)@Matrix.Rotation(math.radians(35),4,'X')@Matrix.Translation(-h)@aw@b.matrix;bpy.context.view_layer.update()
    for share in (0,.18,.35,.60):
        for o in bpy.data.objects:
            if o.type=='MESH' and o.data.shape_keys:
                for k in o.data.shape_keys.key_blocks:k.value=share/.85 if k.name=='Волк·Руки' else share/.65 if k.name=='рост' and o==claw else 0
        bpy.context.view_layer.update();dg=bpy.context.evaluated_depsgraph_get();eb=body.evaluated_get(dg);ec=claw.evaluated_get(dg);mb=eb.to_mesh();mc=ec.to_mesh()
        distances=[];lengths=[]
        for a in meta['anchors']:
            ring_center=sum((ec.matrix_world@mc.vertices[i].co for i in a['first_ring']),Vector())/4
            anchor=eb.matrix_world@mb.vertices[a['source_vertex']].co;distances.append((ring_center-anchor).length)
            tip=ec.matrix_world@mc.vertices[a['first_ring'][0]+16].co;lengths.append((tip-ring_center).length)
        maxgap=max(distances);rows.append(dict(pose=pose,share=share,root_anchor_max_mm=maxgap*1000,tip_length_min_m=min(lengths),tip_length_max_m=max(lengths)))
        if maxgap>.002:errors.append('root does not follow source finger')
        eb.to_mesh_clear();ec.to_mesh_clear()
for v in claw.data.vertices:
    if len([g for g in v.groups if g.weight>0])>4 or abs(sum(g.weight for g in v.groups)-1)>1e-4:errors.append('invalid weights')
report=dict(errors=errors,body_unchanged=True if not any('altered' in e for e in errors) else False,rows=rows,claws=10,triangles=sum(len(f.vertices)-2 for f in claw.data.polygons),note='Root-to-anchor check on original template, including one forearm rotation. Not finger articulation, self-intersection detection or acceptance of lean-template transfer.')
(root/'verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8');print('VERIFY',report);assert not errors,errors
