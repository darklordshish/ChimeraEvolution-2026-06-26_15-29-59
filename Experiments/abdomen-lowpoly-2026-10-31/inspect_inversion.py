import bpy,numpy as np
from pathlib import Path
r=Path.cwd();bpy.ops.wm.open_mainfile(filepath=str(r/'Experiments/abdomen-lowpoly-2026-10-31/candidate.blend'))
for o in bpy.data.objects:
 if o.type!='MESH' or '@' in o.name or not o.data.shape_keys or 'Поджарый' not in o.data.shape_keys.key_blocks:continue
 a=np.array([p.co[:] for p in o.data.shape_keys.key_blocks[0].data]);b=np.array([p.co[:] for p in o.data.shape_keys.key_blocks['Поджарый'].data])
 for f in o.data.polygons:
  ids=list(f.vertices)
  for j in range(1,len(ids)-1):
   i=[ids[0],ids[j],ids[j+1]];n0=np.cross(a[i[1]]-a[i[0]],a[i[2]]-a[i[0]]);n1=np.cross(b[i[1]]-b[i[0]],b[i[2]]-b[i[0]])
   if n0@n1<0:print('BAD',o.name,f.index,i,'basis',a[i].tolist(),'lean',b[i].tolist(),'normdot',n0@n1)
