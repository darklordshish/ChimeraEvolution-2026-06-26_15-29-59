"""Local, bounded repair of c9. No remesh, no weight changes, no new vertices.

Repairs are evaluated in the target shape, not the undeformed basis. Edge flips
must improve triangle quality in target and basis; smoothing is confined to
the folded axillary patch, with shared object seams pinned.
"""
import bpy, bmesh, sys, json, math
from mathutils import Vector
from collections import defaultdict

a = sys.argv[sys.argv.index('--') + 1:]
bpy.ops.wm.open_mainfile(filepath=a[0])
log = {'source': a[0], 'objects': []}
objects = [o for o in bpy.data.objects if o.type == 'MESH' and '@' not in o.name
           and o.data.shape_keys and 'Поджарый' in o.data.shape_keys.key_blocks]
seams = defaultdict(list)
for o in objects:
    for v in o.data.vertices:
        p = o.matrix_world @ v.co
        seams[tuple(round(t,4) for t in p)].append((o.name,v.index))
pinned = {(name,vi) for ids in seams.values() if len({n for n,_ in ids}) > 1 for name,vi in ids}

def triangle_score(vs):
    n = (vs[1]-vs[0]).cross(vs[2]-vs[0])
    perimeter2 = sum((vs[(i+1)%3]-vs[i]).length_squared for i in range(3))
    return 2*math.sqrt(3)*n.length/max(perimeter2,1e-12), n

for o in objects:
    if o.name not in ('Сердце','Руки','хребет'):
        continue
    bm = bmesh.new(); bm.from_mesh(o.data)
    bm.verts.ensure_lookup_table(); bm.edges.ensure_lookup_table(); bm.faces.ensure_lookup_table()
    sk = bm.verts.layers.shape.get('Поджарый')
    edge_line = bm.edges.layers.int.get('перелом_линия')
    P = lambda v: o.matrix_world @ v[sk]
    original = {v.index:v[sk].copy() for v in bm.verts}
    changed = 0
    for sweep in range(2):
        for e in list(bm.edges):
            if not e.is_valid or not e.is_manifold or len(e.link_faces)!=2:
                continue
            if edge_line is not None and e[edge_line]:
                continue
            f,g = e.link_faces
            if len(f.verts)!=3 or len(g.verts)!=3:
                continue
            x,y = e.verts
            u = next(v for v in f.verts if v not in e.verts)
            v = next(v for v in g.verts if v not in e.verts)
            c = sum((P(q) for q in (x,y,u,v)),Vector())/4
            if not (1.20 < c.z < 1.46 and abs(c.x)<.23):
                continue
            if any(q in u.link_edges for q in v.link_edges):
                continue
            old = [triangle_score([P(q) for q in face.verts]) for face in (f,g)]
            ref = old[0][1]+old[1][1]
            if ref.length < 1e-10:
                continue
            ref.normalize()
            old_min = min(t[0] for t in old)
            base_old = [triangle_score([q.co for q in face.verts]) for face in (f,g)]
            base_ref = base_old[0][1]+base_old[1][1]
            if old_min>.35: continue
            # Evaluate Blender's actual winding on a disposable copy. Independently
            # orienting hypothetical triangles can conceal a folded quadrilateral.
            bm.edges.index_update(); bm.edges.ensure_lookup_table()
            trial=bm.copy(); trial.edges.ensure_lookup_table()
            te=trial.edges[e.index]
            res=bmesh.ops.rotate_edges(trial,edges=[te],use_ccw=False)
            skt=trial.verts.layers.shape.get('Поджарый')
            accepted=False
            if res['edges']:
                nf=list(res['edges'][0].link_faces)
                new=[triangle_score([o.matrix_world @ q[skt] for q in face.verts]) for face in nf]
                base_new=[triangle_score([q.co for q in face.verts]) for face in nf]
                accepted=(len(nf)==2 and min(t[0] for t in new)>old_min+.12
                          and min(t[1].normalized().dot(ref) for t in new)>.5
                          and base_ref.length>1e-10
                          and all(t[1].dot(base_ref)>0 for t in base_new)
                          and min(t[0] for t in base_new)>=min(t[0] for t in base_old)-.02)
            trial.free()
            if not accepted: continue
            result = bmesh.ops.rotate_edges(bm,edges=[e],use_ccw=False)
            if result['edges']:
                changed += 1
    # Geodesic one-ring smoothing, confined to axillary fold. Object seams stay fixed.
    smooth_ids=[]
    if o.name=='Руки':
        for vert in bm.verts:
            p=P(vert)
            if .14<abs(p.x)<.185 and .015<p.y<.115 and 1.275<p.z<1.38:
                if not vert.is_boundary and (o.name,vert.index) not in pinned:
                    smooth_ids.append(vert)
        for _ in range(4):
            updates=[]
            for vert in smooth_ids:
                nb=[ed.other_vert(vert) for ed in vert.link_edges]
                mean=sum((q[sk] for q in nb),Vector())/len(nb)
                delta=(mean-vert[sk])*.18
                candidate=vert[sk]+delta
                offset=candidate-original[vert.index]
                if offset.length>.006:
                    candidate=original[vert.index]+offset.normalized()*.006
                updates.append((vert,candidate))
            for vert,candidate in updates:
                vert[sk]=candidate
    # Chest: limit high-frequency depth changes below the pectoral plane while
    # retaining x/z contours, seam vertices and the authored crease vertices.
    chest_ids=[]
    if o.name=='Сердце' and '--chest' in a:
        protected=set()
        if edge_line is not None:
            protected={v for edge in bm.edges if edge[edge_line] for v in edge.verts}
        for vert in bm.verts:
            p=P(vert)
            if p.y<-.03 and 1.24<p.z<1.43 and abs(p.x)<.17:
                if not vert.is_boundary and vert not in protected and (o.name,vert.index) not in pinned:
                    chest_ids.append(vert)
        for _ in range(3):
            updates=[]
            for vert in chest_ids:
                neighbors=[ed.other_vert(vert) for ed in vert.link_edges]
                # Fit local depth against x,z instead of averaging across a sloped mass.
                import numpy as np
                ps=np.array([list(P(q)) for q in neighbors])
                A=np.column_stack((ps[:,0],ps[:,2],np.ones(len(ps))))
                if len(ps)<3 or np.linalg.matrix_rank(A)<3: continue
                coef=np.linalg.lstsq(A,ps[:,1],rcond=None)[0]
                p=P(vert); target=float(coef @ np.array([p.x,p.z,1]))
                q=vert[sk].copy()
                q.y += max(-.0035,min(.0035,(target-p.y)*.22))
                q.y=max(original[vert.index].y-.004,min(original[vert.index].y+.004,q.y))
                updates.append((vert,q))
            for vert,q in updates: vert[sk]=q
    bm.to_mesh(o.data); bm.free(); o.data.update()
    moved=[(o.data.shape_keys.key_blocks['Поджарый'].data[i].co-p).length for i,p in original.items()]
    entry=dict(object=o.name,edge_flips=changed,smoothed_vertices=len(smooth_ids),chest_vertices=len(chest_ids),
               max_displacement_m=max(moved,default=0),vertices=len(o.data.vertices),
               triangles=sum(len(p.vertices)-2 for p in o.data.polygons))
    log['objects'].append(entry); print('C10',entry)
bpy.ops.wm.save_as_mainfile(filepath=a[1])
with open(a[2],'w',encoding='utf-8') as f:
    json.dump(log,f,ensure_ascii=False,indent=2)
