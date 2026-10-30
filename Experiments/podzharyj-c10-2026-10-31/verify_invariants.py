"""Compare source/candidate independently of render and silhouette metrics."""
import bpy,sys,json
from pathlib import Path
a=sys.argv[sys.argv.index('--')+1:]
def capture(path):
    bpy.ops.wm.open_mainfile(filepath=path)
    result={}
    for o in bpy.data.objects:
        if o.type=='ARMATURE':
            result['rig']={b.name:[list(row) for row in b.matrix_local] for b in o.data.bones}
        if o.type!='MESH':continue
        result[o.name]={
            'vertices':[tuple(v.co) for v in o.data.vertices],
            'triangles':sum(len(p.vertices)-2 for p in o.data.polygons),
            'weights':[[[o.vertex_groups[g.group].name,round(g.weight,8)] for g in v.groups] for v in o.data.vertices],
            'keys':{k.name:[tuple(v.co) for v in k.data] for k in o.data.shape_keys.key_blocks} if o.data.shape_keys else {}}
    return result
src=capture(a[0]); dst=capture(a[1]); errors=[]; changed={}
if src.keys()!=dst.keys():errors.append('Object inventory changed')
if src.get('rig')!=dst.get('rig'):errors.append('Rig changed')
for name,s in src.items():
    if name=='rig' or name not in dst:continue
    d=dst[name]
    for key in ('vertices','weights','triangles'):
        if s[key]!=d[key]:errors.append(name+': '+key+' changed')
    added=set(d['keys'])-set(s['keys']); removed=set(s['keys'])-set(d['keys'])
    allowed={'Поджарый·ПодъёмПлеч'} if '--allow-psd' in a else set()
    if removed or added-allowed:errors.append(name+': unexpected shape key inventory change')
    for key,vs in s['keys'].items():
        if key not in d['keys']:continue
        diffs=[max(abs(x-y) for x,y in zip(v,w)) for v,w in zip(vs,d['keys'][key])]
        maximum=max(diffs,default=0)
        if maximum>1e-7:
            changed[name+':'+key]=dict(max_axis_delta_m=maximum,moved_vertices=sum(v>1e-7 for v in diffs))
            if key!='Поджарый':errors.append(name+': unexpected key change '+key)
report=dict(source=a[0],candidate=a[1],errors=errors,changed_keys=changed,
            total_triangles=sum(d['triangles'] for n,d in dst.items() if n!='rig' and '@' not in n))
Path(a[2]).write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(report,ensure_ascii=False))
assert not errors,errors
