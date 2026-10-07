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
# r3 (Модельный, 07.10; критик r2: «подушка тяжела сзади — башмак»): у листа лапа — пясть, которая внизу расходится дугой
# из четырёх пальцев; сзади только малая пястная подушка. В r2 тело подушки было 15 см в ширину и уходило назад на 4–5 см
# за ось — масса стояла под пяткой. Теперь кольца — продолжение пясти: полуширина 2.7 → 3.4 см, зад не дальше 2.3 см
# от оси; массу низа и ширину лапы (≈10 см) дают пальцы.
# Ключ двуногого (последние три числа: вдоль кисти, полуширина, толщина) — r3b по критику: ладонь была вдвое длиннее
# пальцев и кончалась «полкой», из-под которой торчали пальцы. Теперь ладонь 7.6 см до костяшек (было 13.3), костяшки —
# самое широкое и толстое место, пальцы начинаются на нём
for y,dx,dz,zoff,depth,halfwidth,thickness in [
    (.155,.027,.026,.003,.020,.030,.024),
    (.120,.030,.028,.006,.038,.036,.024),
    (.085,.032,.030,.010,.055,.044,.025),
    (.055,.034,.030,.014,.068,.047,.025),
    (.030,.033,.026,.014,.076,.044,.021)]:
    rows.append(ring(Vector((W.x,y,W.z+zoff)),dx,dz,W+axis*depth,halfwidth,thickness,True))
for a,b in zip(rows,rows[1:]):bridge(a,b)
bottom=vertex(Vector((W.x,.026,W.z-.013)),W+axis*.080)        # пястная подушка — сзади и выше пальцев: в анфас её не видно
for j in range(8):faces.append((rows[-1][j],rows[-1][(j+1)%8],bottom))
# Четыре пальца с отдельными гранёными когтями. Основания скрыты внутри низа пясти.
# r3: пальцы — ДУГОЙ (средние впереди, крайние отстают на 1.2 см), короче (4.8/4.0 см против 7/5.8) и толще у основания —
# они и есть масса низа лапы (критик r2: «основания пальцев — отдельные бруски»); наклон вниз сильнее — палец лежит
# на земле подушечкой, а не торчит вперёд трубкой
for finger in range(4):
    offset=finger-1.5
    outer=abs(offset)>1
    # r3b: крайние прижаты к средним до касания у основания, без разлёта («лепестки»), и стоят выше — лапа компактный овал
    base=Vector((W.x+offset*.016,.036 if outer else .030,W.z+.022-(.012 if outer else 0)))
    length=.040 if outer else .048
    klen=.080 if outer else .095          # ключ: пальцы кисти — 1.25 ладони, с когтями ≈1.6 (у оборотня ≈1.3 с когтями)
    direction=Vector((offset*.04,-.40,1)).normalized()
    up=Vector((0,1,0));up=(up-direction*up.dot(direction)).normalized(); lateral=direction.cross(up)
    # r3c (критик C: «обод-манжета и пальцы-грабли»): основания пальцев заходят на кольцо костяшек и перекрывают его —
    # четыре бугра костяшек вместо обода; пальцы толще ×1.4, узел среднего сустава на 30 % толще основания
    keybase=W+axis*.068+Vector((0,0,offset*.023))
    loops=[]
    # палец «боб»: у основания кольцо выше, к кончику спад — коготь продолжает дугу (критик: «карандаш в бруске»);
    # в ключе — узел среднего сустава толще и излом к ладони ~25°
    for t,rad,krad,lift,bend in [(0,.018 if outer else .019,.029,.004,0.),(.5,.016 if outer else .017,.037,.001,-.010),(1,.011 if outer else .012,.016,-.005,-.032)]:
        ids=[]
        for j in range(6):
            q=2*math.pi*j/6
            p=base+direction*length*t+up*lift+rad*(up*math.cos(q)+lateral*math.sin(q))
            k=keybase+axis*(klen*t)+Vector((bend,0,offset*.018*t))+krad*.70*Vector((math.cos(q),0,math.sin(q)))   # веер расходится к кончикам
            ids.append(vertex(p,k))
        loops.append(ids)
    for a,b in zip(loops,loops[1:]):bridge(a,b)
    faces.append(tuple(reversed(loops[0])));faces.append(tuple(loops[-1]))
    claw=[]
    for j in range(4):
        q=2*math.pi*j/4
        claw.append(vertex(base+direction*length+up*-.005+.009*(up*math.cos(q)+lateral*math.sin(q)),keybase+axis*klen+Vector((-.032,0,offset*.018))+.009*Vector((math.cos(q),0,math.sin(q)))))   # основание когтя ≈0.8 кончика пальца
    # когти по плану разные (критик): у лапы короткий тупой вперёд-вниз, у кисти длинный загнутый к ладони
    tip=vertex(base+direction*(length+.011)-up*.008,keybase+axis*(klen+.026)+Vector((-.060,0,offset*.020)))
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
