import bpy,json
from pathlib import Path
from mathutils.kdtree import KDTree
P=Path(__file__).parent
def snap():
    arm=next(o for o in bpy.data.objects if o.type=='ARMATURE')
    data={}
    for o in bpy.data.objects:
        if o.type!='MESH':continue
        o.data.calc_loop_triangles(); gn={g.index:g.name for g in o.vertex_groups}
        ks=o.data.shape_keys.key_blocks if o.data.shape_keys else []
        data[o.name]=dict(V=[o.matrix_world@v.co for v in o.data.vertices],
                         K={k.name:[o.matrix_world@v.co for v in k.data] for k in ks},
                         W=[{gn[g.group]:g.weight for g in v.groups} for v in o.data.vertices],
                         tris=len(o.data.loop_triangles),tone=o.data.materials[0].diffuse_color[0] if o.data.materials else None)
    return data,{b.name:arm.matrix_world@b.matrix_local for b in arm.data.bones}
bpy.ops.wm.open_mainfile(filepath=str(P/'export.blend')); before,bones=snap()
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(P/'candidate.fbx')); after,bones2=snap()
report={'bone_count':len(bones2),'same_objects':set(before)==set(after),'same_bones':set(bones)==set(bones2),'max_bone_matrix_error':0.,'max_position_error_m':0.,'max_key_error_m':0.,'max_weight_error':0.,'max_tone_error':0.,'triangles_unchanged':True,'keys_unchanged':True}
assert report['same_objects'] and report['same_bones']
for n,m in bones.items(): report['max_bone_matrix_error']=max(report['max_bone_matrix_error'],max(abs(m[i][j]-bones2[n][i][j]) for i in range(4) for j in range(4)))
for n,a in before.items():
    b=after[n];tree=KDTree(len(a['V']))
    for i,v in enumerate(a['V']):tree.insert(v,i)
    tree.balance()
    report['triangles_unchanged'] &= a['tris']==b['tris']; report['keys_unchanged'] &= set(a['K'])==set(b['K'])
    if '.тон' in n:report['max_tone_error']=max(report['max_tone_error'],abs(a['tone']-b['tone']))
    for i,p in enumerate(b['V']):
        _,j,d=tree.find(p);report['max_position_error_m']=max(report['max_position_error_m'],d)
        near=tree.find_range(p,1e-5)
        for k in b['K']:
            err=min((b['K'][k][i]-a['K'][k][q]).length for _,q,_ in near)
            report['max_key_error_m']=max(report['max_key_error_m'],err)
        ww=b['W'][i]
        err=min(max((abs(ww.get(k,0)-a['W'][q].get(k,0)) for k in set(ww)|set(a['W'][q])),default=0.) for _,q,_ in near)
        report['max_weight_error']=max(report['max_weight_error'],err)
(P/'fbx-verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8');print(report)
assert report['triangles_unchanged'] and report['keys_unchanged']
assert report['max_position_error_m']<1e-5 and report['max_key_error_m']<1e-5
assert report['max_weight_error']<1e-5 and report['max_tone_error']<1e-5
