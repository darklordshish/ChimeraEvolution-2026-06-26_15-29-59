"""Волк · Кисть: отдельная деталь ниже настоящего запястья.

blender -b --factory-startup -P Tools/Blender/detali/kist_volka.py -- graph-world.json output.fbx passport.json
Граф мира — DumpGraph.Run. Калибр источника — метры тела волка; перенос — PartAssembly.
Крупные кольца подушки и четыре пальца вместо voxel-remesh прежнего Experiments.
Полуширина .088 и длина подушки .186 взяты из воспроизведённого noga_iz_obrazca,
который меряет volk-C-raw.obj; длины пальцев .070/.058 и когтей .022 — тот же пилот.
Новый шов — конец пясти из графа, а не прежние .13/.15 м.
"""
import json, math, sys
from pathlib import Path
import bpy, bmesh
from mathutils import Vector

graph_path, output, passport = sys.argv[sys.argv.index('--')+1:]
G={n['name']:n for n in json.loads(Path(graph_path).read_text(encoding='utf-8-sig'))}
def unity(p): return Vector((-p[0],p[1],p[2]))
def blender(p): return Vector((-p.x,-p.z,p.y))
W=unity(G['пясть']['b']); A=unity(G['пясть']['a'])
axis=(W-A).normalized(); side=Vector((1,0,0)); forward=side.cross(axis).normalized()
# forward ориентирован назад при оси вниз: выбираем к носу волка.
if forward.z<0: forward=-forward
r=G['пясть']['r1']
bpy.ops.wm.read_factory_settings(use_empty=True)
verts=[]; keys=[]; faces=[]
def vertex(p,k):
    verts.append(blender(p));keys.append(blender(k));return len(verts)-1
def ring(center,rx,rz, keycenter, keyrx,keyrz, hand=False):
    ids=[]
    for j in range(8):
        t=2*math.pi*j/8
        p=center+side*(rx*math.sin(t))+forward*(rz*math.cos(t))
        # Ладонь двуногого — к бедру: плоскость кисти вдоль YZ, толщина по X.
        k=keycenter+(side*(keyrz*math.sin(t))+forward*(keyrx*math.cos(t)) if hand else side*(keyrx*math.sin(t))+forward*(keyrz*math.cos(t)))
        ids.append(vertex(p,k))
    return ids
def bridge(a,b):
    for j in range(len(a)):faces.append((a[j],a[(j+1)%len(a)],b[(j+1)%len(b)],b[j]))
seam=ring(W,r,r,W,r,r)
# Переход от узкого запястья к подушке. В ключе — запястье, ладонь, костяшки.
rows=[seam]
for y,dx,dz,zoff,depth,halfwidth,thickness in [
    (.155,.030,.029,.002,.025,.028,.023),
    (.125,.037,.035,.004,.055,.035,.021),
    (.075,.075,.067,.018,.090,.048,.022),
    (.035,.078,.072,.031,.120,.047,.018),
    (.006,.072,.060,.025,.133,.043,.015)]:
    rows.append(ring(Vector((W.x,y,W.z+zoff)),dx,dz,W+axis*depth,halfwidth,thickness,True))
for a,b in zip(rows,rows[1:]):bridge(a,b)
bottom=vertex(Vector((W.x,0,W.z+.025)),W+axis*.140)
for j in range(8):faces.append((rows[-1][j],rows[-1][(j+1)%8],bottom))
# Четыре пальца с отдельными гранёными когтями. Основания скрыты внутри подушки.
for finger in range(4):
    offset=finger-1.5
    base=Vector((W.x+offset*.042,.032,W.z+.075))
    length=.070 if abs(offset)<1 else .058
    direction=Vector((offset*.12,-.20,1)).normalized()
    up=Vector((0,1,0));up=(up-direction*up.dot(direction)).normalized(); lateral=direction.cross(up)
    keybase=W+axis*.105+Vector((0,0,offset*.025))
    loops=[]
    for t,rad in [(0,.021),(.5,.019),(1,.014)]:
        ids=[]
        for j in range(6):
            q=2*math.pi*j/6
            p=base+direction*length*t+rad*(up*math.cos(q)+lateral*math.sin(q))
            k=keybase+axis*(length*t)+Vector((-.024*t*t,0,offset*.004*t))+rad*.70*Vector((math.cos(q),0,math.sin(q)))
            ids.append(vertex(p,k))
        loops.append(ids)
    for a,b in zip(loops,loops[1:]):bridge(a,b)
    faces.append(tuple(reversed(loops[0])));faces.append(tuple(loops[-1]))
    claw=[]
    for j in range(4):
        q=2*math.pi*j/4
        claw.append(vertex(base+direction*length+.010*(up*math.cos(q)+lateral*math.sin(q)),keybase+axis*length+Vector((-.024,0,offset*.004))+.007*Vector((math.cos(q),0,math.sin(q)))))
    tip=vertex(base+direction*(length+.022)-up*.008,keybase+axis*(length+.017)+Vector((-.044,0,offset*.004)))
    for j in range(4):faces.append((claw[j],claw[(j+1)%4],tip))
    faces.append(tuple(reversed(claw)))
mesh=bpy.data.meshes.new('Кисть');mesh.from_pydata(verts,[],faces);mesh.update()
ob=bpy.data.objects.new('Кисть',mesh);bpy.context.collection.objects.link(ob)
bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=bm.faces[:]);bm.to_mesh(mesh);bm.free()
ob.shape_key_add(name='Basis');key=ob.shape_key_add(name='двуногий')
for v,p in zip(key.data,keys):v.co=p
armdata=bpy.data.armatures.new('Волк');arm=bpy.data.objects.new('Волк',armdata);bpy.context.collection.objects.link(arm)
bpy.context.view_layer.objects.active=arm;arm.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
for n in G.values():
    bone=armdata.edit_bones.new(n['name']);bone.head=blender(unity(n['a']));bone.tail=blender(unity(n['b']))
for n in G.values():
    if n['parent']:armdata.edit_bones[n['name']].parent=armdata.edit_bones[n['parent']]
bpy.ops.object.mode_set(mode='OBJECT');ob.parent=arm
ob.modifiers.new('Armature','ARMATURE').object=arm
ob.vertex_groups.new(name='пясть').add(list(range(len(verts))),1,'REPLACE')
# Проверка контракта до экспорта: плоскость сустава, фиксированное кольцо, область за концом кости.
def from_b(p):return Vector((-p.x,p.z,-p.y))
for state in (verts,keys):
    assert min((from_b(p)-W).dot(axis) for p in state)>-1e-6
assert all((verts[i]-keys[i]).length<1e-8 for i in seam)
assert len(seam)==8
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=str(Path(output).resolve()),use_selection=True,apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=False,mesh_smooth_type='OFF',use_armature_deform_only=False)
tris=sum(len(p.vertices)-2 for p in mesh.polygons)
data=dict(species='Волк',slot='Руки',plan='четвероногий',seam=dict(type='запястье',ring=seam,ring_m=[list(from_b(verts[i])) for i in seam],center_m=list(W),ellipse_m=[r,r],note='метры тела волка, оси Unity; плоскость поперёк пясти; индексы Blender, в Unity сверять ring_m'),objects=dict(main='Кисть'),bones=['пясть'],armature='все узлы графа волка',keys=['двуногий'],tris=tris,tris_main=tris,generator='Tools/Blender/detali/kist_volka.py',source='volk-C-raw.obj: промеры подушки из воспроизведённого пилота; volk-prirodnyj_meshy_2026-09-24.png; oboroten-chelovek+volk_meshy_2026-09-24.png')
Path(passport).write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('Кисть:',len(verts),'вершин;',tris,'треугольников; шов',list(W))
