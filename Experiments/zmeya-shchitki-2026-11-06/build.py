"""Художественная проба прилегающих спинных щитков, не игровой орган."""
import bpy,json,math
from pathlib import Path
from mathutils import Vector,Matrix
from mathutils.bvhtree import BVHTree
from mathutils.geometry import barycentric_transform
P=Path(__file__).parent; SRC=P.parent/'shkura-ton-2026-11-06'/'source.blend'
bpy.ops.wm.open_mainfile(filepath=str(SRC))
arm=next(o for o in bpy.data.objects if o.type=='ARMATURE')
srcs=[o for o in bpy.data.objects if o.type=='MESH' and '@' not in o.name and o.name!='Cube']
V=[];F=[];W=[];K={};own=[]
names=list(dict.fromkeys(k.name for o in srcs if o.data.shape_keys for k in o.data.shape_keys.key_blocks))
for o in srcs:
    off=len(V);me=o.data;me.calc_loop_triangles();gn={g.index:g.name for g in o.vertex_groups}
    kb=me.shape_keys.key_blocks if me.shape_keys else None
    for i,v in enumerate(me.vertices):
        V.append(o.matrix_world@(kb[0].data[i].co if kb else v.co));W.append({gn[g.group]:g.weight for g in v.groups})
        for n in names:
            key=kb.get(n) if kb else None
            K.setdefault(n,[]).append(o.matrix_world@(key.data[i].co if key else (kb[0].data[i].co if kb else v.co)))
    for t in me.loop_triangles: F.append(tuple(off+i for i in t.vertices));own.append(o.name)
tree=BVHTree.FromPolygons(V,F,all_triangles=True)
verts=[];faces=[];weights=[];keys={n:[] for n in names};records=[];anchors=[]
bones={b.name for b in arm.data.bones}
ring=[(-.40,.48),(.40,.48),(.50,.02),(.25,-.40),(0,-.60),(-.25,-.40),(-.50,.02)]

def surface(x,z):
    hit,n,fi,d=tree.ray_cast(Vector((x,.9,z)),Vector((0,-1,0)))
    if hit is None or n.y<.12:return None
    ids=F[fi];a,b,c=[V[i] for i in ids]
    bary=barycentric_transform(hit,a,b,c,Vector((1,0,0)),Vector((0,1,0)),Vector((0,0,1)))
    w={}
    for i,s in zip(ids,bary):
        for bn,value in W[i].items():w[bn]=w.get(bn,0)+s*value
    w=dict(sorted(((n,max(0,v)) for n,v in w.items() if n in bones and v>1e-7),key=lambda q:-q[1])[:4]);tot=sum(w.values());w={n:v/tot for n,v in w.items()}
    key={n:sum((K[n][i]*s for i,s in zip(ids,bary)),Vector()) for n in names}
    return hit,n,w,key,own[fi]

for row in range(7):
    z=1.565-row*.075
    columns=[-2,-1,0,1,2] if row<3 else [-1,0,1]
    for col in columns:
        x=col*.061
        width=.070 if abs(col)<2 else .059
        height=.105 if row<4 else .092
        zz=z-(.026 if col%2 else 0)
        local=[(x+u*width,zz+v*height,u,v) for u,v in ring]+[(x,zz+.03*height,0,.03)]
        surf=[surface(xx,z0) for xx,z0,_,_ in local]
        if any(s is None for s in surf):continue
        off=len(verts); maxlift=0.
        for j,(s,(_,_,u,v)) in enumerate(zip(surf,local)):
            hit,n,w,key,owner=s
            # Край у головы почти лежит на коже; свободный край приподнят.
            lift=.0012+.0040*(.48-v)/1.08 if j<7 else .008
            pos=hit+n*lift;verts.append(pos);weights.append(w);anchors.append(dict(plate=len(records),vertex=len(verts)-1,lift_m=lift,owner=owner))
            for kn in names:keys[kn].append(key[kn]+n*lift)
            maxlift=max(maxlift,lift)
        for j in range(7):faces.append((off+j,off+(j+1)%7,off+7))
        # Закрытая кромка, нижняя грань не нужна: под щитком живая кожа.
        for j,s in enumerate(surf[:7]):
            hit,n,w,key,owner=s;verts.append(hit+n*.0006);weights.append(w)
            for kn in names:keys[kn].append(key[kn]+n*.0006)
        for j in range(7):
            j2=(j+1)%7;faces += [(off+j,off+8+j,off+8+j2),(off+j,off+8+j2,off+j2)]
        records.append(dict(row=row,column=col,x=x,z=zz,width=width,height=height,max_lift_m=maxlift,vertices=list(range(off,len(verts)))))
me=bpy.data.meshes.new('щитки-проба');me.from_pydata(verts,[],faces);me.update()
ob=bpy.data.objects.new('Шкура.щитки-проба@Змея',me);bpy.context.scene.collection.objects.link(ob);ob.parent=arm;ob.matrix_world=Matrix.Identity(4)
mod=ob.modifiers.new('Armature','ARMATURE');mod.object=arm
for i,w in enumerate(weights):
    for n,value in w.items():(ob.vertex_groups.get(n) or ob.vertex_groups.new(name=n)).add([i],value,'REPLACE')
for kn in names:
    key=ob.shape_key_add(name=kn)
    for i,co in enumerate(keys[kn]):key.data[i].co=co
for i,t in enumerate([.86,.79,.91]):
    mat=bpy.data.materials.new('Щитки-тон-%d'%i);mat.diffuse_color=(t,t,t,1);me.materials.append(mat)
for i,f in enumerate(me.polygons):
    plate=i//21;local=i%21;f.material_index=0 if local<7 else 1;f.use_smooth=False
    if local<7 and records[plate]['row']%3==1:f.material_index=2
me.calc_loop_triangles()
report=dict(plates=len(records),vertices=len(verts),triangles=len(me.loop_triangles),max_lift_m=max(a['lift_m'] for a in anchors),max_bone_influences=max(map(len,weights)),zero_area=sum(p.area<1e-12 for p in me.polygons),keys=names,records=records)
assert report['zero_area']==0 and report['max_bone_influences']<=4
(P/'geometry.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
for o in bpy.data.objects:
    if o.type=='MESH':o.hide_render=('@' in o.name and o!=ob)
bpy.ops.wm.save_as_mainfile(filepath=str(P/'candidate.blend'))
print({k:v for k,v in report.items() if k!='records'})
