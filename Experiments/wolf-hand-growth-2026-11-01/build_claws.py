"""Separate keratin hooks, anchored to vertices of the original regional hand.

Prototype on the unchanged original template. Does not claim to repair the
rejected regional-key transfer to the lean, decimated template.
"""
import bpy,bmesh,numpy as np,math,json
from pathlib import Path
from mathutils import Vector
root=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(root/'original.blend'))
body=bpy.data.objects['Руки'];arm=next(o for o in bpy.data.objects if o.type=='ARMATURE')
gn={g.index:g.name for g in body.vertex_groups}
base=body.data.shape_keys.key_blocks['Basis'];region=body.data.shape_keys.key_blocks['Волк·Руки']
P=np.array([list(body.matrix_world@p.co) for p in base.data]);D=np.array([list(body.matrix_world@p.co) for p in region.data])-P
dominant=[max(((g.weight,gn[g.group]) for g in v.groups),default=(0,''))[1] for v in body.data.vertices]
left=[i for i,p in enumerate(P) if p[0]<0 and p[2]<.92 and dominant[i]=='предплечье']
# A geometric wrist section, not the FBX bone tail (FBX does not retain bone lengths).
wrist=P[[i for i in left if .89<P[i,2]<.915]].mean(axis=0)
points=P[left];distance=np.linalg.norm(points-wrist,axis=1)
sel=np.nonzero(distance>np.percentile(distance,65))[0];Q=points[sel]
centers=[Q[np.argmax(np.linalg.norm(Q-Q.mean(0),axis=1))]]
for _ in range(3):centers.append(Q[np.argmax(np.min(np.linalg.norm(Q[:,None]-np.array(centers)[None],axis=2),axis=1))])
C=np.array(centers)
for _ in range(30):
    labels=np.argmin(np.linalg.norm(Q[:,None]-C[None],axis=2),axis=1)
    C=np.array([Q[labels==k].mean(0) for k in range(4)])
anchors=[]
for k in range(4):
    group=sel[labels==k];anchors.append(left[group[np.argmax(distance[group])]])
# Fifth, radial digit: innermost fingertip in this hand's actual mesh.
thumb=max((i for i in left if .83<P[i,2]<.90),key=lambda i:P[i,0]);anchors.append(thumb)
assert len(set(anchors))==5
root_distance=min(np.linalg.norm(P[a]-P[b]) for j,a in enumerate(anchors) for b in anchors[j+1:]);assert root_distance>.010,root_distance
old=bpy.data.objects.get('Руки@Волк');bpy.data.objects.remove(old,do_unlink=True)
V0=[];V1=[];F=[];W=[];log=[]
for side in (-1,1):
    wr=wrist.copy();wr[0]*=(-side)
    ids=anchors if side<0 else [int(np.argmin(np.linalg.norm(P-np.array([-P[i,0],P[i,1],P[i,2]]),axis=1))) for i in anchors]
    for digit,index in enumerate(ids):
        root0=P[index];root1=root0+(.65/.85)*D[index]
        axis=root1-wr;axis/=np.linalg.norm(axis)
        palm=P[[i for i in left if .83<P[i,2]<.89]].mean(axis=0);palm[0]*=(-side)
        bend=palm-root1;bend-=axis*(bend@axis)
        if np.linalg.norm(bend)<1e-6:bend=np.array([0,1,0])-axis*axis[1]
        bend/=np.linalg.norm(bend);cross=np.cross(axis,bend);cross/=np.linalg.norm(cross)
        L=.029 if digit==4 else .035;R=.0038 if digit==4 else .0045
        pts=[];direction=axis.copy();point=root1-axis*.0015
        rings=[];weights={gn[g.group]:g.weight for g in body.data.vertices[index].groups if gn[g.group] in arm.data.bones}
        for j in range(5):
            t=j/4;rad=R*(1-t)**1.2
            ring=[]
            for q in range(4 if j<4 else 1):
                offset=(math.cos(q*math.pi/2)*bend+math.sin(q*math.pi/2)*cross)*rad
                ring.append(len(V1));V1.append((point+offset).tolist())
                # Collapsed nail starts at the same source finger vertex.
                V0.append((root0+offset*.08+axis*(t*.001-.0015)).tolist());W.append(weights)
            rings.append(ring)
            theta=.85/4;direction=direction*math.cos(theta)+bend*math.sin(theta);direction/=np.linalg.norm(direction);point=point+direction*(L/4)
        F.append(list(reversed(rings[0])))
        for j in range(4):
            a,b=rings[j:j+2]
            for q in range(4):F.append([a[q],a[(q+1)%4],b[0]] if len(b)==1 else [a[q],a[(q+1)%4],b[(q+1)%4],b[q]])
        log.append(dict(side=side,digit=digit,source_vertex=index,root0=root0.tolist(),regional_delta=D[index].tolist(),root1=root1.tolist(),first_ring=rings[0],length_m=L))
mesh=bpy.data.meshes.new('Волчьи когти · раздельные');mesh.from_pydata(V0,[],F);mesh.update()
o=bpy.data.objects.new('Руки@Волк',mesh);bpy.context.scene.collection.objects.link(o);o.parent=arm
for name,points in [('Basis',V0),('рост',V1)]:
    k=o.shape_key_add(name=name,from_mix=False)
    for p,co in zip(k.data,points):p.co=co
for i,ws in enumerate(W):
    for name,w in ws.items():
        g=o.vertex_groups.get(name) or o.vertex_groups.new(name=name);g.add([i],w,'REPLACE')
mod=o.modifiers.new('Armature','ARMATURE');mod.object=arm
bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
for obj in bpy.data.objects:
    if obj.type=='MESH' and obj.data.shape_keys:
        for k in obj.data.shape_keys.key_blocks:k.value=0
bpy.ops.wm.save_as_mainfile(filepath=str(root/'hooks.blend'))
report=dict(anchors=log,min_anchor_separation_m=float(root_distance),claws_per_hand=5,triangles=sum(len(f.vertices)-2 for f in mesh.polygons),growth_endpoint_share=.65,region_at_endpoint=.65/.85,note='Prototype on original template only. Root follows original regional key exactly below first global stage. Finger anchors are geometric hypotheses, require visual approval; no finger rig or active fist animation is claimed.')
(root/'hooks.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8');print('HOOKS',report)
