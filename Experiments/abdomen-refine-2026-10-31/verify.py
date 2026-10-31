import bpy,numpy as np,json
from pathlib import Path
root=Path(__file__).resolve().parent
def snap(path):
    bpy.ops.wm.open_mainfile(filepath=str(path));meshes={}
    arm=next(o for o in bpy.data.objects if o.type=='ARMATURE')
    for o in bpy.data.objects:
        if o.type!='MESH':continue
        meshes[o.name]=dict(base=np.array([v.co[:] for v in o.data.vertices]),faces=[tuple(f.vertices) for f in o.data.polygons],keys={k.name:np.array([v.co[:] for v in k.data]) for k in o.data.shape_keys.key_blocks} if o.data.shape_keys else {},weights=[[(g.group,g.weight) for g in v.groups] for v in o.data.vertices])
    return meshes,{b.name:np.array(b.matrix_local) for b in arm.data.bones}
a,ba=snap(root.parent/'abdomen-lowpoly-2026-10-31/candidate.blend');b,bb=snap(root/'candidate.blend');errors=[];changed=[];maxd=0
for n,x in a.items():
    y=b[n]
    if x['faces']!=y['faces'] or x['weights']!=y['weights'] or not np.array_equal(x['base'],y['base']):errors.append(n+' topology/base/weights changed')
    for k,p in x['keys'].items():
        delta=np.linalg.norm(y['keys'][k]-p,axis=1)
        if delta.max(initial=0)>1e-8:
            if n!='Сердце' or k!='Поджарый':errors.append(n+' unexpected key edit '+k)
            else:
                changed=np.nonzero(delta>1e-8)[0].tolist();maxd=float(delta.max())
                if any(i<110 for i in changed):errors.append('original outside/boundary vertex changed')
                coords=p[changed]
                if not np.all((abs(coords[:,0])<.09)&(coords[:,1]<-.08)&(coords[:,2]>1.12)&(coords[:,2]<1.315)):errors.append('edits outside abdomen')
for n,v in ba.items():
    if not np.array_equal(v,bb[n]):errors.append(n+' bone changed')
if maxd>.0121:errors.append('displacement exceeds cap')
report=dict(errors=errors,changed_vertices=len(changed),max_displacement_m=maxd,triangles=sum(len(f)-2 for n,m in b.items() if '@' not in n for f in m['faces']),all_other_keys_base_topology_weights_and_bones_identical=not errors)
(root/'verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8');print('VERIFY',report);assert not errors,errors
