import bpy
from pathlib import Path
r=Path.cwd();bpy.ops.wm.open_mainfile(filepath=str(r/'Experiments/shoulder-local-2026-10-31/candidate.blend'))
o=bpy.data.objects['Сердце'];k=o.data.shape_keys.key_blocks['Поджарый']
sel=[]
for f in o.data.polygons:
 p=[o.matrix_world@k.data[i].co for i in f.vertices]
 if all(abs(q.x)<.105 and q.y<-.08 and 1.115<q.z<1.32 for q in p):sel.append(f)
from collections import Counter
ed=Counter(tuple(sorted((f.vertices[j],f.vertices[(j+1)%len(f.vertices)]))) for f in sel for j in range(len(f.vertices)))
b=[e for e,n in ed.items() if n==1];print('PATCH',len(sel),'EDGES',len(b),'VERTS',len(set(i for f in sel for i in f.vertices)))
print('BOUNDARY',b)
