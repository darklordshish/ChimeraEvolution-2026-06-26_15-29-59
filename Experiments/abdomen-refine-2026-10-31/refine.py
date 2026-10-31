"""Same six volumes, continuous shallow pillows rather than closed beveled plates."""
import bpy,json,math,numpy as np
from pathlib import Path
from mathutils import Vector
root=Path(__file__).resolve().parent
original=root.parent/'shoulder-local-2026-10-31/candidate.blend'
bpy.ops.wm.open_mainfile(filepath=str(original))
old=bpy.data.objects['Сердце'];sourcebase=[tuple(v.co[:]) for v in old.data.vertices]
sourcekeys=np.array([p.co[:] for p in old.data.shape_keys.key_blocks['Поджарый'].data])
selected=[i for i,f in enumerate(old.data.polygons) if all(abs(sourcekeys[v,0])<.105 and sourcekeys[v,1]<-.08 and 1.115<sourcekeys[v,2]<1.32 for v in f.vertices)]
keep={j for i,f in enumerate(old.data.polygons) if i not in selected for j in f.vertices}
pinnedbase={sourcebase[i] for i in keep}
bpy.ops.wm.open_mainfile(filepath=str(root.parent/'abdomen-lowpoly-2026-10-31/candidate.blend'))
o=bpy.data.objects['Сердце'];key=o.data.shape_keys.key_blocks['Поджарый']
originalpoints=np.array([p.co[:] for p in key.data]);inv=o.matrix_world.inverted();changes=[]
def smooth(v):v=max(0,min(1,v));return v*v*(3-2*v)
for v in o.data.vertices:
    if tuple(v.co[:]) in pinnedbase:continue
    p=o.matrix_world@key.data[v.index].co;x,y,z=p
    if not (abs(x)<.09 and y<-.08 and 1.12<z<1.315):continue
    baseline=-.128+2.0*x*x+.025*(z-1.22)
    bump=0
    for cz,width,height,amplitude in [(1.274,.035,.022,.0090),(1.217,.034,.022,.0085),(1.163,.033,.021,.0075)]:
        for side in (-1,1):
            bump+=amplitude*math.exp(-((x-side*.038)/width)**2-((z-cz)/height)**2)
    target=baseline-bump
    blend=smooth((z-1.125)/.018)*smooth((1.312-z)/.018)*smooth((.10-abs(x))/.025)
    delta=max(-.012,min(.012,(target-y)*blend))
    if abs(delta)<1e-8:continue
    p.y+=delta
    # Broader upper pair, narrower lower pair, gently arched intersections.
    # Same six approved locations; tiny shifts break the tiled-rectangle outline.
    p.x+=x*.12*math.tanh((z-1.215)/.045)*blend
    p.z+=.004*(abs(x)/.075)**2*math.sin((z-1.13)*math.pi/.18)*blend
    key.data[v.index].co=inv@p
    changes.append(dict(vertex=v.index,delta_y_m=delta,displacement_m=(p-Vector((x,y,z))).length))
for obj in bpy.data.objects:
    if obj.type=='MESH' and obj.data.shape_keys:
        for k in obj.data.shape_keys.key_blocks:k.value=0
bpy.ops.wm.save_as_mainfile(filepath=str(root/'candidate.blend'))
report=dict(source='abdomen-lowpoly-2026-10-31/candidate.blend',changed=changes,max_displacement_m=max((r['displacement_m'] for r in changes),default=0),note='Same topology and weights. Only lean shape key, interior abdominal vertices; six shallow masses integrated into curved torso baseline. No chest or latissimus edits.')
(root/'refine.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8');print('REFINE',len(changes),report['max_displacement_m'])
