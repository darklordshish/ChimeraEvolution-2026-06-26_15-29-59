"""Audit the approved relief; weld only equal sampled trajectories."""
from pathlib import Path
import json
P=Path(__file__).parent
source=(P/'probe.py').read_text(encoding='utf8')
exec(compile(source.split("move([0]*5)\nrest_error=",1)[0],str(P/'probe.py'),'exec'),globals())
rows=json.loads((P/'frames.json').read_text(encoding='utf8'))
if (P/'audit-samples.npy').exists():
    samples=np.load(P/'audit-samples.npy');contacts=json.loads((P/'audit-contacts.json').read_text(encoding='utf8'))
else:
    samples=[];contacts=[]
    for row in rows:
        move(row['angles'])
        xyz=np.array([tuple(v.co) for v in me.vertices],dtype=np.float64);samples.append(xyz)
        dg=bpy.context.evaluated_depsgraph_get();hv=[]
        for obj in skin:
            ev=obj.evaluated_get(dg);mesh=ev.to_mesh();hv += [ev.matrix_world@v.co for v in mesh.vertices];ev.to_mesh_clear()
        tree=BVHTree.FromPolygons(hv,F,all_triangles=True)
        deepest=0.;count=0
        for k,p in enumerate(xyz):
            if anchors[k]['height']<.0005:continue
            hit,n,fi,dist=tree.find_nearest(Vector(p))
            signed=(Vector(p)-hit).dot(n)
            if signed<-.0005:count+=1;deepest=max(deepest,-signed)
        contacts.append(dict(frame=row['frame'],below_local_surface_vertices=count,max_depth_mm=deepest*1000))
        print('AUDIT',row['frame'],contacts[-1],flush=True)
    samples=np.stack(samples)
    np.save(P/'audit-samples.npy',samples)
    (P/'audit-contacts.json').write_text(json.dumps(contacts),encoding='utf8')
    
# Spatial candidates at rest, then compare EVERY sampled position.
kd=KDTree(len(points))
for k,p in enumerate(samples[0]):kd.insert(Vector(p),k)
kd.balance();mapping=[];representatives=[];buckets={};seam_max=0.;candidates=0
neighbors=[set() for _ in points]
for f in faces:
    for k in f:neighbors[k].update(j for j in f if j!=k)
for k,p in enumerate(samples[0]):
    nearby=kd.find_range(Vector(p),2e-6)
    chosen=None
    for _,j,_ in nearby:
        if j>=k:continue
        delta=float(np.linalg.norm(samples[:,k]-samples[:,representatives[mapping[j]]],axis=1).max())
        seam_max=max(seam_max,delta);candidates+=1
        if delta<=1e-6 and not any(n<k and mapping[n]==mapping[j] for n in neighbors[k]):
            chosen=mapping[j];break
    if chosen is None:chosen=len(representatives);representatives.append(k)
    mapping.append(chosen)
mapped=[tuple(mapping[k] for k in f) for f in faces]
kept=[i for i,f in enumerate(mapped) if len(set(f))==3]
removed=[i for i,f in enumerate(mapped) if len(set(f))<3]
removed_area=sum((points[faces[i][1]]-points[faces[i][0]]).cross(points[faces[i][2]]-points[faces[i][0]]).length*.5 for i in removed)
assert removed_area<1e-7,removed_area
newfaces=[mapped[i] for i in kept]
error=float(np.linalg.norm(samples-samples[:,np.asarray(representatives)[mapping]],axis=2).max())
assert error<=1e-6
move([0]*5)
opt=bpy.data.meshes.new('рельеф-без-дублей');opt.from_pydata([tuple(me.vertices[k].co) for k in representatives],[],newfaces);opt.update()
for mat in me.materials:opt.materials.append(mat)
for k,poly in enumerate(opt.polygons):poly.material_index=mats[kept[k]];poly.use_smooth=False
ob.data=opt;me=opt
visible('pattern');camera(145)
sc.render.filepath=str(R/'optimized-rear34.png');bpy.ops.render.render(write_still=True)
camera(145,True);sc.render.filepath=str(R/'optimized-detail.png');bpy.ops.render.render(write_still=True)
report=dict(frames=len(rows),vertices_before=len(points),vertices_after=len(representatives),triangles=len(newfaces),removed_degenerate_faces=len(removed),removed_rest_area_m2=removed_area,max_sampled_position_error_mm=error*1000,nearby_candidate_checks=candidates,max_nearby_trajectory_separation_mm=seam_max*1000,contacts=contacts,limitations=['Nearest oriented surface is a local penetration diagnostic, not a full self-intersection test.','Weld equivalence is checked at 49 sampled poses only.','Only collapsed microscopic triangles removed; no runtime or FBX integration.'])
(P/'audit.json').write_text(json.dumps(report,indent=2),encoding='utf8')
(P/'optimized-anchors.json').write_text(json.dumps(dict(objects=objects,triangles=F,anchors=[anchors[k] for k in representatives],raw_objects=[o.name for o in raw],raw_triangles=RF,normal_groups=groups),ensure_ascii=False),encoding='utf8')
bpy.ops.wm.save_as_mainfile(filepath=str(P/'optimized-preview.blend'))
print('SUMMARY',json.dumps({k:v for k,v in report.items() if k!='contacts'}),flush=True)


