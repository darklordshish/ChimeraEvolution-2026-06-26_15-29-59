import bpy,sys,json
a=sys.argv[sys.argv.index('--')+1:]
bpy.ops.wm.open_mainfile(filepath=a[0])
arm=next(o for o in bpy.data.objects if o.type=='ARMATURE')
for b in arm.data.bones:
    print('BONE',b.name,'parent',b.parent.name if b.parent else '',
          'head',list(arm.matrix_world@b.head_local),'tail',list(arm.matrix_world@b.tail_local))
for o in bpy.data.objects:
    if o.type!='MESH' or '@' in o.name or not o.data.shape_keys:continue
    if 'Поджарый' not in o.data.shape_keys.key_blocks:continue
    kb=o.data.shape_keys.key_blocks['Поджарый'];gn={g.index:g.name for g in o.vertex_groups}
    points=[]
    for v in o.data.vertices:
        p=o.matrix_world@kb.data[v.index].co
        if .10<abs(p.x)<.22 and 1.24<p.z<1.43:
            points.append(dict(index=v.index,p=list(p),weights={gn[g.group]:g.weight for g in v.groups if g.weight>.01}))
    if points:print('PATCH',o.name,json.dumps(points[:8],ensure_ascii=False))
