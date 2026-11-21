import bpy,json
from pathlib import Path
r=Path.cwd();bpy.ops.wm.open_mainfile(filepath=str(r/'Experiments/shoulder-local-2026-10-31/candidate.blend'))
rows=[]
for o in bpy.data.objects:
 if o.type!='MESH' or '@' in o.name or not o.data.shape_keys:continue
 k=o.data.shape_keys.key_blocks.get('Поджарый')
 if not k:continue
 pts=[]
 for v in o.data.vertices:
  p=o.matrix_world@k.data[v.index].co
  if abs(p.x)<.13 and p.y<-.015 and 1.03<p.z<1.35:pts.append([v.index,*[round(t,5) for t in p]])
 if pts:rows.append(dict(object=o.name,points=pts))
(r/'Experiments/shoulder-local-2026-10-31/abdomen-points.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(rows,ensure_ascii=False))
