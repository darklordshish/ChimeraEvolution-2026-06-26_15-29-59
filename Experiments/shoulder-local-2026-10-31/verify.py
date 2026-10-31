import bpy,sys,json,numpy as np
from pathlib import Path
root=Path(__file__).resolve().parent
def snapshot(path):
    bpy.ops.wm.open_mainfile(filepath=str(path));result={}
    arm=next(o for o in bpy.data.objects if o.type=='ARMATURE')
    bones={b.name:np.array(b.matrix_local) for b in arm.data.bones}
    for o in bpy.data.objects:
        if o.type!='MESH' or '@' in o.name:continue
        gn={g.index:g.name for g in o.vertex_groups}
        result[o.name]=dict(base=np.array([v.co[:] for v in o.data.vertices]),keys={k.name:np.array([p.co[:] for p in k.data]) for k in o.data.shape_keys.key_blocks} if o.data.shape_keys else {},weights=[{gn[g.group]:g.weight for g in v.groups if gn[g.group] in bones and g.weight>1e-8} for v in o.data.vertices],faces=[tuple(p.vertices) for p in o.data.polygons],world=np.array(o.matrix_world))
    return result,bones
old,ob=snapshot(root.parent/'shoulder-support-2026-10-31/support.blend');new,nb=snapshot(root/'candidate.blend')
errors=[];seams={};changed=0;tris=0
for name,o in old.items():
    n=new[name];tris+=sum(len(f)-2 for f in n['faces'])
    if not np.array_equal(o['base'],n['base']):errors.append(name+' base changed')
    for k,x in o['keys'].items():
        if not np.array_equal(x,n['keys'][k]):errors.append(name+' key changed '+k)
    for i,(a,b) in enumerate(zip(o['weights'],n['weights'])):
        p=(o['world']@np.r_[o['base'][i],1])[:3]
        if len(b)>4 or abs(sum(b.values())-1)>.0001:errors.append(name+' invalid weights '+str(i))
        delta=max((abs(a.get(k,0)-b.get(k,0)) for k in a.keys()|b.keys()),default=0)
        if delta>1e-7:
            changed+=1
            if p[0]<=0:errors.append(name+' opposite side edited')
        key=tuple(np.round(p,4))
        if key in seams:
            prev=seams[key]
            if max((abs(prev.get(k,0)-b.get(k,0)) for k in prev.keys()|b.keys()),default=0)>.0001:errors.append(name+' seam weights diverged')
        else:seams[key]=b
for name,b in ob.items():
    if not np.array_equal(b,nb[name]):errors.append(name+' bone changed')
report=dict(errors=errors,triangles=tris,bones=len(nb),changed_vertex_records=changed,all_coordinates_and_keys_identical=True if not any('changed' in e for e in errors) else False)
(root/'verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8');print('VERIFY',report)
assert not errors,errors
