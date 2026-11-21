import bpy,json
from pathlib import Path
r=Path.cwd();files=[r/'Experiments/abdomen-refine-2026-10-31/candidate.blend',r/'Experiments/wolf-hand-growth-2026-11-01/original.blend']
for p in files:
 bpy.ops.wm.open_mainfile(filepath=str(p));a=next(o for o in bpy.data.objects if o.type=='ARMATURE');print('FILE',p.name,str(p.parent.name),'BONES',len(a.data.bones))
 for b in a.data.bones:
  if any(s in b.name.lower() for s in ('пал','фаланг','кист','пяст','предплеч','сгиб','разгиб')):
   print('BONE',b.name,'PARENT',b.parent.name if b.parent else None,'CHILDREN',[x.name for x in b.children])
 for o in bpy.data.objects:
  if o.type=='MESH' and o.name=='Руки':
   names={g.index:g.name for g in o.vertex_groups};counts={}
   k=o.data.shape_keys.key_blocks.get('Поджарый') or o.data.shape_keys.key_blocks[0]
   for v in o.data.vertices:
    q=o.matrix_world@k.data[v.index].co
    if q.z<.91:
     for g in v.groups:
      if g.weight>.01:counts[names[g.group]]=counts.get(names[g.group],0)+1
   print('HAND_GROUPS',json.dumps(counts,ensure_ascii=False))
