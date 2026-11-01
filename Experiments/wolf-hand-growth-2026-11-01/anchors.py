import bpy,numpy as np,json
from pathlib import Path
r=Path.cwd()/'Experiments/wolf-hand-growth-2026-11-01';bpy.ops.wm.open_mainfile(filepath=str(r/'original.blend'))
o=bpy.data.objects['Руки'];a=next(o for o in bpy.data.objects if o.type=='ARMATURE');gn={g.index:g.name for g in o.vertex_groups};p=np.array([list(o.matrix_world@v.co) for v in o.data.vertices]);wr=np.array(a.matrix_world@a.data.bones['предплечье'].tail_local)
ids=[v.index for v in o.data.vertices if p[v.index,0]<0 and p[v.index,2]<.92 and max(((g.weight,gn[g.group]) for g in v.groups),default=(0,''))[1]=='предплечье'];P=p[ids];d=np.linalg.norm(P-wr,axis=1);sel=np.nonzero(d>np.percentile(d,70))[0];Q=P[sel]
cent=[Q[np.argmax(np.linalg.norm(Q-Q.mean(0),axis=1))]]
for _ in range(3):cent.append(Q[np.argmax(np.min(np.linalg.norm(Q[:,None]-np.array(cent)[None],axis=2),axis=1))])
C=np.array(cent)
for _ in range(30):
 lab=np.argmin(np.linalg.norm(Q[:,None]-C[None],axis=2),axis=1);C=np.array([Q[lab==k].mean(0) for k in range(4)])
tips=[]
for k in range(4):
 grp=sel[lab==k];tips.append(ids[grp[np.argmax(d[grp])]])
print('WRIST',wr,'TIPS',[(i,p[i].tolist()) for i in tips]);print('HAND',len(ids),P.min(0),P.max(0))
(r/'anchor-candidates.json').write_text(json.dumps(dict(wrist=wr.tolist(),tips=[dict(vertex=i,p=p[i].tolist()) for i in tips],points=[dict(vertex=i,p=p[i].tolist()) for i in ids]),indent=2),encoding='utf-8')
