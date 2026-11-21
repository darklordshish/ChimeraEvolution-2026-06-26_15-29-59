"""Щитки как вырезанные участки поверхности кожи с приподнятым краем.
Каждая исходная грань клиппируется контуром щитка в проекции сзади;
новые точки и ключи интерполируются внутри этой же исходной грани.
"""
import bpy,json,math
from pathlib import Path
from mathutils import Vector,Matrix
from mathutils.bvhtree import BVHTree
from mathutils.geometry import barycentric_transform
P=Path(__file__).parent
bpy.ops.wm.open_mainfile(filepath=str(P.parent/'shkura-ton-2026-11-06/candidate.blend'))
arm=next(o for o in bpy.data.objects if o.type=='ARMATURE');bones={b.name for b in arm.data.bones}
srcs=[o for o in bpy.data.objects if o.type=='MESH' and ((('@' not in o.name) and o.name not in ['Cube','хребет','шея','Сердце','Руки']) or (o.name.startswith('Шкура.тон') and o.name.endswith('@Змея')))]
names=list(dict.fromkeys(k.name for o in srcs if o.data.shape_keys for k in o.data.shape_keys.key_blocks))
V=[];F=[];W=[];K={n:[] for n in names}
for o in srcs:
    off=len(V);me=o.data;me.calc_loop_triangles();gn={g.index:g.name for g in o.vertex_groups}
    kb=me.shape_keys.key_blocks if me.shape_keys else None
    for i,v in enumerate(me.vertices):
        V.append(o.matrix_world@(kb[0].data[i].co if kb else v.co));W.append({gn[g.group]:g.weight for g in v.groups})
        for n in names:
            key=kb.get(n) if kb else None
            K[n].append(o.matrix_world@(key.data[i].co if key else (kb[0].data[i].co if kb else v.co)))
    for t in me.loop_triangles:F.append(tuple(off+i for i in t.vertices))
tree=BVHTree.FromPolygons(V,F,all_triangles=True)
def side(a,b,p):return (b.x-a.x)*(p.z-a.z)-(b.z-a.z)*(p.x-a.x)
def clip(poly,ring):
    for i,a in enumerate(ring):
        b=ring[(i+1)%len(ring)];out=[]
        if not poly:break
        prev=poly[-1];sp=side(a,b,prev)
        for cur in poly:
            ss=side(a,b,cur)
            if (sp<=0)!=(ss<=0):out.append(prev+(cur-prev)*(sp/(sp-ss)))
            if ss<=0:out.append(cur)
            prev=cur;sp=ss
        poly=out
    return poly

verts=[];faces=[];weights=[];keys={n:[] for n in names};materials=[];records=[];lifts=[];anchors=[]
def add(p,fi,lift):
    ids=F[fi];abc=[V[i] for i in ids]
    bary=barycentric_transform(p,*abc,Vector((1,0,0)),Vector((0,1,0)),Vector((0,0,1)))
    w={}
    for i,s in zip(ids,bary):
        for bn,value in W[i].items():w[bn]=w.get(bn,0)+s*value
    w=dict(sorted(((n,max(0,v)) for n,v in w.items() if n in bones and v>1e-7),key=lambda q:-q[1])[:4]);tot=sum(w.values());w={n:v/tot for n,v in w.items()}
    j=len(verts);verts.append(p+Vector((0,lift,0)));weights.append(w);lifts.append(lift)
    tangent=(abc[1]-abc[0]).normalized();normal=(abc[1]-abc[0]).cross(abc[2]-abc[0]).normalized();bitangent=normal.cross(tangent)
    offset=Vector((0,lift,0))
    anchors.append(dict(triangle=fi,bary=list(bary),offset=[offset.dot(tangent),offset.dot(bitangent),offset.dot(normal)]))
    for kn in names:keys[kn].append(sum((K[kn][i]*s for i,s in zip(ids,bary)),Vector())+Vector((0,lift,0)))
    return j
outline=[(-.35,.47),(.35,.47),(.50,.18),(.44,-.28),(0,-.49),(-.44,-.28),(-.50,.18)]
def lift_at(p,x,z,width,height):
    u=abs((p.x-x)/width);v=(p.z-z)/height
    across=max(0.,1-u/.5)
    # Небольшой киль поверх прилегающей поверхности, с затуханием у концов.
    length=min(1.,max(0.,(.47-v)/.20),max(0.,(v+.49)/.15))
    return .0012+.0038*max(0.,min(1.,(.47-v)/1.02))+.006*across*length
for row in range(9):
    for col in ([-2,-1,0,1,2] if row<3 else [-1,0,1]):
        x=col*.062;z=1.57-row*.057-abs(col)*.034
        width=(.080 if row<6 else [.073,.065,.057][row-6]) if abs(col)<2 else [.062,.056,.045][row]
        width*=.88
        height=.084 if abs(col)<2 else .075
        ring=[Vector((x+u*width,0,z+v*height)) for u,v in outline]
        inner=[Vector((x+(p.x-x)*.90,0,z+(p.z-z)*.90)) for p in ring]
        regions=[(inner,False)]+[([ring[i],ring[(i+1)%7],inner[(i+1)%7],inner[i]],True) for i in range(7)]
        pieces=[];edgeuses={};edgeinfo={};first=len(faces)
        for fi,ids in enumerate(F):
            abc=[V[i] for i in ids];normal=(abc[1]-abc[0]).cross(abc[2]-abc[0]).normalized()
            if normal.y<.12:continue
            if max(a.x for a in abc)<x-width*.5 or min(a.x for a in abc)>x+width*.5 or max(a.z for a in abc)<z-height*.6 or min(a.z for a in abc)>z+height*.5:continue
            footprint=clip(abc,ring)
            for region,border in regions:
                part=clip(footprint,region)
                for left,right in [(x-width,x),(x,x+width)]:
                    half=[Vector((left,0,z+height)),Vector((right,0,z+height)),Vector((right,0,z-height)),Vector((left,0,z-height))]
                    poly=clip(part,half)
                    if len(poly)<3:continue
                    center=sum(poly,Vector())/len(poly);area=sum((poly[j]-poly[0]).cross(poly[j+1]-poly[0]).length*.5 for j in range(1,len(poly)-1))
                    if area<1e-10:continue
                    hit=tree.ray_cast(Vector((center.x,.9,center.z)),Vector((0,-1,0)))
                    if hit[0] is None or abs(hit[0].y-center.y)>.0001:continue
                    idx=[add(p,fi,lift_at(p,x,z,width,height)) for p in poly]
                    for j in range(1,len(idx)-1):
                        faces.append((idx[0],idx[j],idx[j+1]));materials.append(3 if border else (0 if center.x<x else 1))
                    for j,p in enumerate(poly):
                        q=poly[(j+1)%len(poly)];key=tuple(sorted([tuple(round(c,6) for c in p),tuple(round(c,6) for c in q)]))
                        edgeuses[key]=edgeuses.get(key,0)+1;edgeinfo[key]=(p,q,fi,idx[j],idx[(j+1)%len(idx)])
        for key,count in edgeuses.items():
            if count!=1:continue
            # Короткая кромка толщиной 0.4 мм вместо стены до кожи:
            # не пересекает нижний ряд в нахлёсте.
            p,q,fi,a,b=edgeinfo[key];aa=add(p,fi,max(.0005,lifts[a]-.0004));bb=add(q,fi,max(.0005,lifts[b]-.0004))
            faces.extend([(a,aa,bb),(a,bb,b)]);materials.extend([2,2])
        # Разрез по килю иногда проходит точно через старую вершину:
        # удаляем нулевые треугольники, сохраняя счётчик каждой чешуйки.
        kept=[(f,m) for f,m in zip(faces[first:],materials[first:]) if (verts[f[1]]-verts[f[0]]).cross(verts[f[2]]-verts[f[0]]).length>2e-12]
        faces[first:]=[f for f,m in kept];materials[first:]=[m for f,m in kept]
        if len(faces)>first:records.append(dict(row=row,column=col,x=x,z=z,triangles=len(faces)-first))
me=bpy.data.meshes.new('щитки-проба');me.from_pydata(verts,[],faces);me.update()
ob=bpy.data.objects.new('Шкура.щитки-проба@Змея',me);bpy.context.scene.collection.objects.link(ob);ob.parent=arm;ob.matrix_world=Matrix.Identity(4)
mod=ob.modifiers.new('Armature','ARMATURE');mod.object=arm
for i,w in enumerate(weights):
    for n,value in w.items():(ob.vertex_groups.get(n) or ob.vertex_groups.new(name=n)).add([i],value,'REPLACE')
for kn in names:
    key=ob.shape_key_add(name=kn)
    for i,co in enumerate(keys[kn]):key.data[i].co=co
for i,t in enumerate([.82,.92,.58,.58]):
    mat=bpy.data.materials.new('Щитки-тон-%d'%i);mat.diffuse_color=(t,t,t,1);me.materials.append(mat)
for i,f in enumerate(me.polygons):f.material_index=int(materials[i]);f.use_smooth=False
me.calc_loop_triangles()
report=dict(plates=len(records),vertices=len(verts),triangles=len(me.loop_triangles),max_lift_y_m=max(lifts),min_lift_y_m=min(lifts),max_bone_influences=max(map(len,weights)),zero_area=sum(p.area<1e-12 for p in me.polygons),keys=names,records=records)
assert report['zero_area']==0 and report['max_bone_influences']<=4
(P/'geometry.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
(P/'anchors.json').write_text(json.dumps(dict(objects=[dict(name=o.name,vertices=len(o.data.vertices)) for o in srcs],triangles=F,anchors=anchors),ensure_ascii=False),encoding='utf8')
for o in bpy.data.objects:
    if o.type=='MESH':o.hide_render=('@' in o.name and o!=ob)
bpy.ops.wm.save_as_mainfile(filepath=str(P/'plates.blend'));print({k:v for k,v in report.items() if k!='records'})
