import bpy,json,numpy as np
from pathlib import Path
from collections import Counter
root=Path(__file__).resolve().parent
def snap(path):
    bpy.ops.wm.open_mainfile(filepath=str(path));out={};arm=next(o for o in bpy.data.objects if o.type=='ARMATURE')
    for o in bpy.data.objects:
        if o.type!='MESH' or '@' in o.name:continue
        gn={g.index:g.name for g in o.vertex_groups}
        out[o.name]=dict(coords=np.array([v.co[:] for v in o.data.vertices]),keys={k.name:np.array([p.co[:] for p in k.data]) for k in o.data.shape_keys.key_blocks} if o.data.shape_keys else {},weights=[{gn[g.group]:g.weight for g in v.groups if gn[g.group] in arm.data.bones and g.weight>0} for v in o.data.vertices],faces=[tuple(p.vertices) for p in o.data.polygons])
    return out,{b.name:np.array(b.matrix_local) for b in arm.data.bones}
old,b0=snap(root.parent/'shoulder-local-2026-10-31/candidate.blend');new,b1=snap(root/'candidate.blend');errors=[]
def edges(faces):return Counter(tuple(sorted((f[i],f[(i+1)%len(f)]))) for f in faces for i in range(len(f)))
for n,o in old.items():
    c=new[n]
    if n!='Сердце':
        if o['faces']!=c['faces'] or o['weights']!=c['weights'] or not np.array_equal(o['coords'],c['coords']):errors.append(n+' changed')
        for k in o['keys']:
            if not np.array_equal(o['keys'][k],c['keys'][k]):errors.append(n+' key changed '+k)
    else:
        # Exact outside vertex/key/weight preservation, matched by base coordinates.
        lookup={tuple(p):i for i,p in enumerate(c['coords'])};outside=0
        x=o['keys']['Поджарый']
        patch=[i for i,f in enumerate(o['faces']) if all(abs(x[j,0])<.105 and x[j,1]<-.08 and 1.115<x[j,2]<1.32 for j in f)]
        ids={j for i,f in enumerate(o['faces']) if i not in patch for j in f}
        for j in ids:
            v=lookup.get(tuple(o['coords'][j]))
            if v is None:errors.append('outside vertex missing');continue
            if o['weights'][j]!=c['weights'][v]:errors.append('outside weight changed')
            for k in o['keys']:
                if not np.array_equal(o['keys'][k][j],c['keys'][k][v]):errors.append('outside key changed')
            outside+=1
        e0=edges(o['faces']);e1=edges(c['faces'])
        if sum(v==1 for v in e0.values())!=sum(v==1 for v in e1.values()):errors.append('new boundary edges')
        if any(v>2 for v in e1.values()):errors.append('nonmanifold edges')
        if len(set(j for f in c['faces'] for j in f))!=len(c['coords']):errors.append('loose vertices')
    for w in c['weights']:
        if len(w)>4 or abs(sum(w.values())-1)>1e-4:errors.append(n+' weights invalid')
for n,b in b0.items():
    if not np.array_equal(b,b1[n]):errors.append(n+' bone changed')
result=dict(errors=errors,source_triangles=sum(len(f)-2 for o in old.values() for f in o['faces']),candidate_triangles=sum(len(f)-2 for o in new.values() for f in o['faces']),outside_heart_vertices_verified=outside,bones=len(b1),note='Only neutral lean-body candidate verified. Resampled PSD and new topology require motion review.')
(root/'verification.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8');print('VERIFY',result);assert not errors,errors
