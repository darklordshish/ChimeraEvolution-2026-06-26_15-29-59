"""Диагностическое движение реального рига: щитки + тоновый рисунок.
Не игровые анимации. Никаких исправлений исходного тела/рига/щитков.
"""
import bpy,math,json,sys
from pathlib import Path
from mathutils import Vector,Matrix
from mathutils.bvhtree import BVHTree
from mathutils.geometry import intersect_ray_tri,barycentric_transform
P=Path(__file__).parent;R=P/'renders';R.mkdir(exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(P.parent/'shkura-ton-2026-11-06/candidate.blend'))
arm=next(o for o in bpy.data.objects if o.type=='ARMATURE')
with bpy.data.libraries.load(str(P.parent/'zmeya-shchitki-2026-11-06/candidate.blend'),link=False) as (a,b):b.objects=['Шкура.щитки-проба@Змея']
plate=b.objects[0];bpy.context.scene.collection.objects.link(plate)
mw=plate.matrix_world.copy();plate.parent=arm;plate.matrix_world=mw
for mod in plate.modifiers:
    if mod.type=='ARMATURE':mod.object=arm
for o in list(bpy.data.objects):
    if o.type=='ARMATURE' and o!=arm:bpy.data.objects.remove(o,do_unlink=True)
aw=arm.matrix_world.copy();ai=aw.inverted();rest={b.name:(aw@b.matrix_local).copy() for b in arm.data.bones}
def subtree(root):
    return {root}|{b.name for b in arm.data.bones[root].children_recursive} if root in arm.data.bones else set()
torso=subtree('живот');upper=set().union(*(subtree(n) for n in ['грудная клетка','шея','лопатка','лопатка.L','плечо','плечо.L','опора_подмышки','опора_подмышки.L']))
arms={s:subtree('плечо'+s) for s in ['','.L']}
order=sorted(arm.data.bones,key=lambda b:len(b.parent_recursive))
def rot(p,angle,axis):return Matrix.Translation(p)@Matrix.Rotation(math.radians(angle),4,axis)@Matrix.Translation(-p)
def pose(q):
    flex,twist,side,lift,reach=q
    for pb in arm.pose.bones:pb.matrix_basis=Matrix.Identity(4)
    bpy.context.view_layer.update()
    p1=aw@arm.data.bones['живот'].head_local;p2=aw@arm.data.bones['грудная клетка'].head_local
    d1=rot(p1,flex*.5,'X')@rot(p1,twist*.5,'Z')@rot(p1,side*.5,'Y')
    d2=d1@rot(p2,flex*.5,'X')@rot(p2,twist*.5,'Z')@rot(p2,side*.5,'Y')
    ad={}
    for s in arms:
        p=aw@arm.data.bones['плечо'+s].head_local;sign=1 if p.x>0 else -1
        ad[s]=d2@rot(p,-sign*lift,'Y')@rot(p,-reach,'X')
    for b in order:
        d=d2 if b.name in upper else d1 if b.name in torso else Matrix.Identity(4)
        for s,names in arms.items():
            if b.name in names:d=ad[s]
        arm.pose.bones[b.name].matrix=ai@d@rest[b.name]
        bpy.context.view_layer.update()

body=[o for o in bpy.data.objects if o.type=='MESH' and '@' not in o.name and o.name!='Cube']
tones=[o for o in bpy.data.objects if o.type=='MESH' and o.name.startswith('Шкура.тон') and o.name.endswith('@Змея')]
base=(.58,.55,.50)
for o in bpy.data.objects:
    if o.type!='MESH':continue
    visible=(o in body and o.name not in ['хребет','шея','Сердце','Руки']) or o in tones or o==plate
    o.hide_render=not visible
    for i,mat in enumerate(o.data.materials):
        if mat:
            factor=mat.diffuse_color[0] if o in tones or o==plate else 1.
            m=mat.copy();m.diffuse_color=(*(c*factor for c in base),1);o.data.materials[i]=m
    for f in o.data.polygons:f.use_smooth=False
    if o.data.shape_keys:
        for k in o.data.shape_keys.key_blocks:k.value=0
sc=bpy.context.scene;sc.render.engine='BLENDER_WORKBENCH';w=sc.display.shading
w.light='STUDIO';w.studio_light='paint.sl';w.color_type='MATERIAL';w.show_shadows=False;w.show_cavity=False;w.show_specular_highlight=False;w.show_backface_culling=True;w.background_type='WORLD'
sc.world=sc.world or bpy.data.worlds.new('World');sc.world.color=(.82,.83,.84);sc.view_settings.view_transform='Standard'
sc.render.image_settings.file_format='PNG';sc.render.resolution_x=800;sc.render.resolution_y=720;sc.render.resolution_percentage=100
cam=bpy.data.objects.new('Motion_camera',bpy.data.cameras.new('Motion_camera'));sc.collection.objects.link(cam);sc.camera=cam;cam.data.type='ORTHO';cam.data.ortho_scale=1.58;cam.data.clip_end=20
def camera(az):
    a=math.radians(az);d=Vector((math.sin(a),-math.cos(a),.08)).normalized();target=Vector((0,0,1.26))
    cam.location=target+d*4;cam.rotation_euler=(-d).to_track_quat('-Z','Y').to_euler()

def evaluated(o,dg):
    ev=o.evaluated_get(dg);me=ev.to_mesh();me.calc_loop_triangles()
    v=[ev.matrix_world@p.co for p in me.vertices];f=[tuple(t.vertices) for t in me.loop_triangles];mi=[t.material_index for t in me.loop_triangles]
    ev.to_mesh_clear();return v,f,mi
geometry=json.loads((P.parent/'zmeya-shchitki-2026-11-06/geometry.json').read_text(encoding='utf8'))
plateids=[i for i,r in enumerate(geometry['records']) for _ in range(r['triangles'])]
plate.data.calc_loop_triangles();assert len(plateids)==len(plate.data.loop_triangles)
pose([0]*5);dg=bpy.context.evaluated_depsgraph_get();pv0,pf0,pm0=evaluated(plate,dg)
areas0=[(pv0[f[1]]-pv0[f[0]]).cross(pv0[f[2]]-pv0[f[0]]).length for f in pf0]

def cross_tri(a,b):
    for tri,other in [(a,b),(b,a)]:
        for i,p in enumerate(tri):
            direction=tri[(i+1)%3]-p;length=direction.length
            if length<1e-8:continue
            h=intersect_ray_tri(*other,direction/length,p,True)
            if h is None:continue
            t=(h-p).dot(direction/length)
            if not(1e-6<t<length-1e-6):continue
            bary=barycentric_transform(h,*other,Vector((1,0,0)),Vector((0,1,0)),Vector((0,0,1)))
            if min(bary)>1e-5:return True
    return False

def measure(label,q,contacts=False):
    pose(q);dg=bpy.context.evaluated_depsgraph_get();V=[];F=[]
    for o in body:
        v,f,mi=evaluated(o,dg);off=len(V);V+=v;F += [tuple(off+i for i in t) for t in f]
    tree_raw=BVHTree.FromPolygons(V,F,all_triangles=True)
    VV=[];FF=[]
    for o in body+tones:
        if o.hide_render:continue
        v,f,mi=evaluated(o,dg);off=len(VV);VV+=v;FF += [tuple(off+i for i in t) for t in f]
    tree=BVHTree.FromPolygons(VV,FF,all_triangles=True);pv,pf,pm=evaluated(plate,dg)
    below=[];gaps=[];rawgaps=[];compressed=[];samples=0
    for i,f in enumerate(pf):
        if pm[i]==2:continue # кромки не сравниваются с верхними поверхностями
        c=sum((pv[j] for j in f),Vector())/3;point,n,fi,d=tree.find_nearest(c);signed=(c-point).dot(n)
        samples+=1;gaps.append(d);rawgaps.append(tree_raw.find_nearest(c)[3])
        if signed<-.0005:below.append((i,signed))
        area=(pv[f[1]]-pv[f[0]]).cross(pv[f[2]]-pv[f[0]]).length
        if area/max(areas0[i],1e-12)<.25:compressed.append(i)
    row=dict(label=label,angles=q,top_triangles=samples,below_skin_over_0_5mm=len(below),max_below_skin_mm=max([-d*1000 for i,d in below],default=0),max_nearest_skin_mm=max(gaps)*1000,max_nearest_raw_hidden_body_mm=max(rawgaps)*1000,top_faces_area_below_25pct=len(compressed),below_plate_ids=sorted({plateids[i] for i,d in below}))
    if contacts:
        bvh=BVHTree.FromPolygons(pv,pf,all_triangles=True);pairs=set();top_pairs=set();rim_pairs=set()
        for i,j in bvh.overlap(bvh):
            if i>=j or plateids[i]==plateids[j]:continue
            pair=tuple(sorted((plateids[i],plateids[j])))
            both_top=pm[i]!=2 and pm[j]!=2
            target=top_pairs if both_top else rim_pairs
            if pair not in target and cross_tri([pv[k] for k in pf[i]],[pv[k] for k in pf[j]]):
                pairs.add(pair);target.add(pair)
        row['noncoplanar_crossing_plate_pairs']=[list(p) for p in sorted(pairs)]
        row['top_top_crossing_plate_pairs']=[list(p) for p in sorted(top_pairs)]
        row['crossing_pairs_with_rim']=[list(p) for p in sorted(rim_pairs)]
    return row

poses=[('rest',[0,0,0,0,0]),('flex30',[30,0,0,0,0]),('extend20',[-20,0,0,0,0]),('twist40',[0,40,0,0,0]),('side20',[0,0,20,0,0]),('arms90',[0,0,0,90,0]),('reach70',[0,0,0,0,70]),('combined',[25,25,0,0,60])]
reports=[]
for label,q in poses:
    row=measure(label,q,True);reports.append(row);print('POSE',json.dumps(row,ensure_ascii=False),flush=True)
    for az,view in [(145,'rear34'),(90,'side')]:
        camera(az);sc.render.filepath=str(R/f'{label}-{view}.png');bpy.ops.render.render(write_still=True)
(P/'poses.json').write_text(json.dumps(dict(note='Synthetic FK stress poses, keys 0; noncoplanar crossings exclude coplanar and boundary-only contacts; signed nearest-surface samples are not a solid-volume proof.',poses=reports),ensure_ascii=False,indent=2),encoding='utf8')
pose([0]*5);bpy.ops.wm.save_as_mainfile(filepath=str(P/'combined.blend'))
if '--stills-only' not in sys.argv:
    sc.render.resolution_x=sc.render.resolution_y=600;camera(145)
    timeline=[poses[0],poses[1],poses[2],poses[3],('twist-minus40',[0,-40,0,0,0]),poses[5],poses[6],poses[7],poses[0]]
    frames=[];counter=0
    for (l0,q0),(l1,q1) in zip(timeline,timeline[1:]):
        for j in range(6):
            t=j/6;s=t*t*(3-2*t);q=[a+(b-a)*s for a,b in zip(q0,q1)];row=measure(l1,q)
            row['frame']=counter;frames.append(row);sc.render.filepath=str(R/f'frame-{counter:03d}.png');bpy.ops.render.render(write_still=True);counter+=1
    row=measure('rest',[0]*5);row['frame']=counter;frames.append(row);sc.render.filepath=str(R/f'frame-{counter:03d}.png');bpy.ops.render.render(write_still=True)
    (P/'frames.json').write_text(json.dumps(frames,ensure_ascii=False,indent=2),encoding='utf8')
print('MOTION_DONE',flush=True)
