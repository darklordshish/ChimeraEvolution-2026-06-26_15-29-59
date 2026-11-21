"""A/B одного разбиения: обычные веса против фиксированной привязки к коже.
Привязка вычисляется после деформации кожи; Blender-проба, не FBX-функция.
"""
from pathlib import Path
source=(Path(__file__).parent/'probe-source.py').read_text(encoding='utf8')
prefix=source.split('\nreports=[]',1)[0]
assert prefix.count("P.parent/'zmeya-shchitki-2026-11-06/candidate.blend'")==1
prefix=prefix.replace("P.parent/'zmeya-shchitki-2026-11-06/candidate.blend'","P/'plates.blend'")
prefix=prefix.replace("P.parent/'zmeya-shchitki-2026-11-06/geometry.json'","P/'geometry.json'")
exec(compile(prefix,'frozen_probe_setup','exec'),globals())
base_pose=pose;control=plate
bound=control.copy();bound.data=control.data.copy();bound.name='щитки_поверхность@Змея';sc.collection.objects.link(bound)
bound.shape_key_clear()
for m in bound.modifiers:
    if m.type=='ARMATURE':m.show_render=False;m.show_viewport=False
anchors=json.loads((P/'anchors.json').read_text(encoding='utf8'))
host=[bpy.data.objects[r['name']] for r in anchors['objects']]
assert sum(len(o.data.vertices) for o in host)==max(max(t) for t in anchors['triangles'])+1
assert len(bound.data.vertices)==len(anchors['anchors'])

def bind_pose(q):
    base_pose(q);dg=bpy.context.evaluated_depsgraph_get();points=[]
    for o,row in zip(host,anchors['objects']):
        v,f,mi=evaluated(o,dg);assert len(v)==row['vertices'];points+=v
    inv=bound.matrix_world.inverted()
    for i,a in enumerate(anchors['anchors']):
        tri=anchors['triangles'][a['triangle']];aa,bb,cc=[points[j] for j in tri]
        tangent=(bb-aa).normalized();normal=(bb-aa).cross(cc-aa).normalized();bitangent=normal.cross(tangent)
        assert normal.length>.9
        p=aa*a['bary'][0]+bb*a['bary'][1]+cc*a['bary'][2]
        u,v,w=a['offset'];p+=tangent*u+bitangent*v+normal*w
        bound.data.vertices[i].co=inv@p
    bound.data.update();bpy.context.view_layer.update()

results=[]
for variant in ['lbs','surface']:
    plate=control if variant=='lbs' else bound
    control.hide_render=variant!='lbs';bound.hide_render=variant!='surface'
    pose=base_pose if variant=='lbs' else bind_pose
    folder=P/('renders-'+variant);folder.mkdir(exist_ok=True)
    for label,q in poses:
        row=measure(label,q,True);row['variant']=variant;results.append(row)
        print('POSE',variant,label,'below',row['below_skin_over_0_5mm'],'gap',row['max_nearest_skin_mm'],'pairs',len(row['top_top_crossing_plate_pairs']),flush=True)
        for az,view in [(145,'rear34'),(90,'side')]:
            camera(az);sc.render.filepath=str(folder/f'{label}-{view}.png');bpy.ops.render.render(write_still=True)
(P/'poses.json').write_text(json.dumps(results,ensure_ascii=False,indent=2),encoding='utf8')
plate=bound;pose=bind_pose;control.hide_render=True;bound.hide_render=False
sc.render.resolution_x=sc.render.resolution_y=600;camera(145)
timeline=[poses[0],poses[1],poses[2],poses[3],('twist-minus40',[0,-40,0,0,0]),poses[5],poses[6],poses[7],poses[0]]
folder=P/'renders-surface';frames=[];counter=0
for (l0,q0),(l1,q1) in zip(timeline,timeline[1:]):
    for j in range(6):
        t=j/6;s=t*t*(3-2*t);q=[a+(b-a)*s for a,b in zip(q0,q1)];row=measure(l1,q,True)
        row['frame']=counter;frames.append(row);sc.render.filepath=str(folder/f'frame-{counter:03d}.png');bpy.ops.render.render(write_still=True);counter+=1
row=measure('rest',[0]*5,True);row['frame']=counter;frames.append(row);sc.render.filepath=str(folder/f'frame-{counter:03d}.png');bpy.ops.render.render(write_still=True)
(P/'frames.json').write_text(json.dumps(frames,ensure_ascii=False,indent=2),encoding='utf8')
bind_pose([0]*5);bpy.ops.wm.save_as_mainfile(filepath=str(P/'preview.blend'))
summary=dict(frames=len(frames),max_under_mm=max(r['max_below_skin_mm'] for r in frames),max_gap_mm=max(r['max_nearest_skin_mm'] for r in frames),max_top_crossing_pairs=max(len(r['top_top_crossing_plate_pairs']) for r in frames),compressed_events=sum(r['top_faces_area_below_25pct'] for r in frames))
(P/'summary.json').write_text(json.dumps(summary,indent=2),encoding='utf8');print('DONE',summary,flush=True)
