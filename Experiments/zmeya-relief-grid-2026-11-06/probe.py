"""Реальный рельеф на ТОЙ ЖЕ сетке ромбов, без рисованного контура.
Поднимается сама кожа; края ячеек остаются на исходной поверхности.
"""
import bpy,math,json,numpy as np
from pathlib import Path
from mathutils import Vector,Matrix
from mathutils.geometry import barycentric_transform
from mathutils.bvhtree import BVHTree
from mathutils.kdtree import KDTree
P=Path(__file__).parent;R=P/'renders';R.mkdir(exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(P.parent/'shkura-ton-2026-11-06/candidate.blend'))
arm=next(o for o in bpy.data.objects if o.type=='ARMATURE')
source=(P/'pose-source.py').read_text(encoding='utf8')
helpers='aw=arm.matrix_world.copy();'+source.split('aw=arm.matrix_world.copy();',1)[1].split('\nbody=[',1)[0]
exec(compile(helpers,'pose_helpers','exec'),globals())
pose([0]*5)
skin=[o for o in bpy.data.objects if o.type=='MESH' and o.name.startswith('Шкура.тон') and o.name.endswith('@Змея')]
for o in bpy.data.objects:
    if o.type=='MESH' and o.data.shape_keys:
        for key in o.data.shape_keys.key_blocks:key.value=0.
bpy.context.view_layer.update()
V=[];F=[];TI=[];objects=[]
for o in skin:
    off=len(V);objects.append(dict(name=o.name,vertices=len(o.data.vertices)))
    V += [o.matrix_world@v.co for v in o.data.vertices];o.data.calc_loop_triangles()
    for t in o.data.loop_triangles:F.append(tuple(off+i for i in t.vertices));TI.append(0 if '.тон1@' in o.name else 1)
STEP=.065;ANGLE=.62;HEIGHT=.0125
S=math.sin(ANGLE);C=math.cos(ANGLE)
raw=[o for o in bpy.data.objects if o.type=='MESH' and '@' not in o.name and o.name!='Cube']
RV=[];RF=[]
for o in raw:
    off=len(RV);RV += [o.matrix_world@v.co for v in o.data.vertices];o.data.calc_loop_triangles()
    RF += [tuple(off+j for j in t.vertices) for t in o.data.loop_triangles]
rawtree=BVHTree.FromPolygons(RV,RF,all_triangles=True)
kd=KDTree(len(RV))
for i,p in enumerate(RV):kd.insert(p,i)
kd.balance();groups=[min(j for p,j,d in kd.find_range(v,1e-5)) for v in RV]
def bary64(p,abc):
    nearest=min(range(3),key=lambda j:(p-abc[j]).length)
    if (p-abc[nearest]).length<2e-7:return [float(j==nearest) for j in range(3)]
    a0,a1,a2=np.asarray([tuple(q) for q in abc],dtype=np.float64)
    st=np.linalg.lstsq(np.column_stack((a1-a0,a2-a0)),np.asarray(tuple(p))-a0,rcond=None)[0]
    b=np.clip([1-st.sum(),st[0],st[1]],0,1);return (b/b.sum()).tolist()
def uv(p):return Vector(((S*p.x+C*p.z)/STEP,(-S*p.x+C*p.z)/STEP))
def side(a,b,p):q=uv(p);return (b.x-a.x)*(q.y-a.y)-(b.y-a.y)*(q.x-a.x)
def clip(poly,region):
    for i,a in enumerate(region):
        b=region[(i+1)%len(region)];out=[]
        if not poly:break
        prev=poly[-1];sp=side(a,b,prev)
        for cur in poly:
            ss=side(a,b,cur)
            if (sp>=-1e-10)!=(ss>=-1e-10):
                if abs(sp-ss)>1e-16:out.append(prev+(cur-prev)*(sp/(sp-ss)))
            if ss>=-1e-10:out.append(cur)
            prev=cur;sp=ss
        poly=out
    return poly
points=[];faces=[];mats=[];anchors=[];cells=set();cell_ids=[];anchor_errors=[]
def add(p,fi,h):
    abc=[V[j] for j in F[fi]]
    # У тоновой кожи есть очень узкие треугольники после резки.
    # Float32 barycentric_transform на них теряет точность даже в покое.
    b=bary64(p,abc)
    reconstructed=sum((q*s for q,s in zip(abc,b)),Vector())
    anchor_errors.append((reconstructed-p).length)
    h0,n0,rfi,d0=rawtree.find_nearest(reconstructed)
    nb=bary64(h0,[RV[j] for j in RF[rfi]])
    anchors.append(dict(triangle=fi,bary=list(b),height=max(0.,h),normal_triangle=rfi,normal_bary=nb));points.append(reconstructed);return len(points)-1
for fi,ids in enumerate(F):
    abc=[V[j] for j in ids];center=sum(abc,Vector())/3;normal=(abc[1]-abc[0]).cross(abc[2]-abc[0]).normalized()
    # Маска зависит от положения, а не от нормали отдельной грани:
    # соседние грани должны получать одинаковую высоту на общем ребре.
    active=max(p.y for p in abc)>-.01 and max(p.z for p in abc)>1.06 and min(p.z for p in abc)<1.66
    if not active:
        face=[add(p,fi,0) for p in abc];faces.append(tuple(face));mats.append(TI[fi]);cell_ids.append(None);continue
    q=uv(center);ii,jj=math.floor(q.x),math.floor(q.y)
    # Входные грани уже разрезаны границами тех же ячеек. На погрешностях
    # границы проверяются соседние ячейки, геометрия вне них не теряется.
    coords=[uv(p) for p in abc]
    for i in range(math.floor(min(p.x for p in coords)+1e-7),math.floor(max(p.x for p in coords)-1e-7)+1):
        for j in range(math.floor(min(p.y for p in coords)+1e-7),math.floor(max(p.y for p in coords)-1e-7)+1):
            a,b,c,d=Vector((i,j)),Vector((i+1,j)),Vector((i+1,j+1)),Vector((i,j+1))
            r0,r1=Vector((i+.25,j+.25)),Vector((i+.75,j+.75))
            # Длинный киль вдоль тела (u=v), а не пирамидальная вершина.
            regions=[((a,b,r0),(0,0,1)),((b,r1,r0),(0,1,1)),((b,c,r1),(0,0,1)),((c,d,r1),(0,0,1)),((d,r0,r1),(0,1,1)),((d,a,r0),(0,0,1))]
            for region,heights in regions:
                poly=clip(abc,region)
                if len(poly)<3:continue
                index=[]
                for p in poly:
                    q=uv(p)-region[0];e1=region[1]-region[0];e2=region[2]-region[0];den=e1.x*e2.y-e1.y*e2.x
                    ss=(q.x*e2.y-q.y*e2.x)/den;tt=(e1.x*q.y-e1.y*q.x)/den
                    tent=max(0.,min(1.,heights[0]*(1-ss-tt)+heights[1]*ss+heights[2]*tt))
                    # В самой кайме области рельеф мягко сходит на нет.
                    fade=min(1.,max(0.,(p.z-1.06)/.04),max(0.,(1.66-p.z)/.04))*min(1.,max(0.,(p.y+.01)/.06))
                    index.append(add(p,fi,HEIGHT*tent*fade))
                for k in range(1,len(index)-1):
                    f=(index[0],index[k],index[k+1])
                    if (points[f[1]]-points[f[0]]).cross(points[f[2]]-points[f[0]]).length<2e-12:continue
                    faces.append(f);mats.append(TI[fi]);cell_ids.append((i,j));cells.add((i,j))

# Клиппирование float32 на почти параллельном проекции ребре даёт
# микроскопический выход из грани. Точка проецируется обратно;
# допустимое смещение проекции — 0.1 мм, далеко меньше рельефа/пикселя.
assert max(anchor_errors)<.0001, max(anchor_errors)
me=bpy.data.meshes.new('совпадающий-рельеф');me.from_pydata(points,[],faces);me.update()
ob=bpy.data.objects.new('Шкура.рельеф-проба@Змея',me);bpy.context.scene.collection.objects.link(ob)
base=(.58,.55,.50)
for i,t in enumerate([1.,.74]):
    mat=bpy.data.materials.new('ТотЖеТон%d'%i);mat.diffuse_color=(*(c*t for c in base),1);me.materials.append(mat)
for i,f in enumerate(me.polygons):f.material_index=mats[i];f.use_smooth=False
restkeys=[tuple(round(c,6) for c in p) for p in V]
def move(q):
    pose(q);dg=bpy.context.evaluated_depsgraph_get();vv=[]
    for o,row in zip(skin,objects):
        ev=o.evaluated_get(dg);m=ev.to_mesh();assert len(m.vertices)==row['vertices'];vv += [ev.matrix_world@v.co for v in m.vertices];ev.to_mesh_clear()
    if q==[0]*5:print('HOST_REST',max((p-v).length for p,v in zip(vv,V)),[(o.name,[(k.name,k.value) for k in o.data.shape_keys.key_blocks if k.value]) for o in skin],flush=True)
    # Нормальное поле берётся с цельной исходной кожи: разрезы тонов
    # содержат T-стыки и не должны создавать разрыв направления рельефа.
    rv=[]
    for o in raw:
        ev=o.evaluated_get(dg);m=ev.to_mesh();rv += [ev.matrix_world@v.co for v in m.vertices];ev.to_mesh_clear()
    acc={}
    for tri in RF:
        a,b,c=[rv[k] for k in tri];n=(b-a).cross(c-a)
        for k in tri:acc[groups[k]]=acc.get(groups[k],Vector())+n
    nn=[acc[k].normalized() for k in groups]
    for k,a in enumerate(anchors):
        tri=F[a['triangle']];p=sum((vv[j]*s for j,s in zip(tri,a['bary'])),Vector())
        n=sum((nn[j]*s for j,s in zip(RF[a['normal_triangle']],a['normal_bary'])),Vector()).normalized()
        me.vertices[k].co=p+n*a['height']
    me.update();bpy.context.view_layer.update()

sc=bpy.context.scene;sc.render.engine='BLENDER_WORKBENCH';w=sc.display.shading
w.light='STUDIO';w.studio_light='paint.sl';w.color_type='MATERIAL';w.show_shadows=False;w.show_cavity=False;w.show_specular_highlight=False;w.show_backface_culling=True;w.background_type='WORLD'
sc.world=sc.world or bpy.data.worlds.new('World');sc.world.color=(.82,.83,.84);sc.view_settings.view_transform='Standard';sc.render.image_settings.file_format='PNG'
sc.render.resolution_x=800;sc.render.resolution_y=720;sc.render.resolution_percentage=100
cam=bpy.data.objects.new('Camera',bpy.data.cameras.new('Camera'));sc.collection.objects.link(cam);sc.camera=cam;cam.data.type='ORTHO';cam.data.ortho_scale=1.58;cam.data.clip_end=20
def camera(az,detail=False):
    t=Vector((0,0,1.36 if detail else 1.26));a=math.radians(az);d=Vector((math.sin(a),-math.cos(a),.08)).normalized()
    cam.location=t+d*4;cam.rotation_euler=(-d).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=.72 if detail else 1.58
def visible(mode):
    for o in bpy.data.objects:
        if o.type!='MESH':continue
        isbase='@' not in o.name and o.name not in ['Cube','хребет','шея','Сердце','Руки']
        o.hide_render=not(isbase or (o in skin and mode=='flat') or (o==ob and mode!='flat'))
        if o!=ob:
            for j,mat in enumerate(o.data.materials):
                if not mat:continue
                factor=(1. if '.тон1@' in o.name else .74) if o in skin else 1.
                new=mat.copy();new.diffuse_color=(*(c*factor for c in base),1);o.data.materials[j]=new
    for i,mat in enumerate(me.materials):mat.diffuse_color=(*(c*(1 if mode=='mono' or i==0 else .74) for c in base),1)

move([0]*5)
rest_error=max((v.co-p).length for v,p in zip(me.vertices,points))
print('REST_CHECK',rest_error,flush=True)
assert rest_error<.0126,rest_error
for mode in ['flat','mono','pattern']:
    visible(mode)
    for view,az,detail in [('rear34',145,False),('side',90,False),('detail',145,True)]:
        camera(az,detail);sc.render.filepath=str(R/(mode+'-'+view+'.png'));bpy.ops.render.render(write_still=True)
visible('pattern');camera(145);sc.render.resolution_x=sc.render.resolution_y=600
poses=[('rest',[0]*5),('flex30',[30,0,0,0,0]),('extend20',[-20,0,0,0,0]),('twist40',[0,40,0,0,0]),('twist-minus40',[0,-40,0,0,0]),('arms90',[0,0,0,90,0]),('reach70',[0,0,0,0,70]),('combined',[25,25,0,0,60]),('rest',[0]*5)]
frames=[];counter=0
for (l0,q0),(l1,q1) in zip(poses,poses[1:]):
    for j in range(6):
        t=j/6;s=t*t*(3-2*t);q=[a+(b-a)*s for a,b in zip(q0,q1)];move(q)
        sc.render.filepath=str(R/f'frame-{counter:03d}.png');bpy.ops.render.render(write_still=True);frames.append(dict(frame=counter,angles=q));counter+=1
move([0]*5);sc.render.filepath=str(R/f'frame-{counter:03d}.png');bpy.ops.render.render(write_still=True);frames.append(dict(frame=counter,angles=[0]*5))
(P/'frames.json').write_text(json.dumps(frames),encoding='utf8')
report=dict(cells=len(cells),vertices=len(points),triangles=len(faces),max_height_mm=max(a['height'] for a in anchors)*1000,max_anchor_error_m=max(anchor_errors),step=STEP,angle=ANGLE,artificial_outlines=False,area_flat=sum((points[f[1]]-points[f[0]]).cross(points[f[2]]-points[f[0]]).length*.5 for f in faces),area_host=sum((V[f[1]]-V[f[0]]).cross(V[f[2]]-V[f[0]]).length*.5 for f in F))
(P/'geometry.json').write_text(json.dumps(report,indent=2),encoding='utf8')
(P/'anchors.json').write_text(json.dumps(dict(objects=objects,triangles=F,anchors=anchors),ensure_ascii=False),encoding='utf8')
assert abs(report['area_flat']-report['area_host'])<1e-5
bpy.ops.wm.save_as_mainfile(filepath=str(P/'preview.blend'));print('DONE',report)
