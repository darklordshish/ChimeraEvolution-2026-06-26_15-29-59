import bpy,json
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
P=Path(__file__).parent
def bodies():
    out={}
    for o in bpy.data.objects:
        if o.type!='MESH' or '@' in o.name:continue
        out[o.name]=dict(v=[tuple(v.co) for v in o.data.vertices],f=[tuple(f.vertices) for f in o.data.polygons],k={k.name:[tuple(v.co) for v in k.data] for k in o.data.shape_keys.key_blocks} if o.data.shape_keys else {},w=[[(o.vertex_groups[g.group].name,g.weight) for g in v.groups] for v in o.data.vertices])
    arm=next(o for o in bpy.data.objects if o.type=='ARMATURE')
    return out,{b.name:[tuple(row) for row in b.matrix_local] for b in arm.data.bones}
bpy.ops.wm.open_mainfile(filepath=str(P.parent/'shkura-ton-2026-11-06/source.blend'));body,bones=bodies()
bpy.ops.wm.open_mainfile(filepath=str(P/'candidate.blend'));body2,bones2=bodies()
V=[];F=[]
for o in bpy.data.objects:
    if o.type=='MESH' and '@' not in o.name:
        off=len(V);V += [o.matrix_world@v.co for v in o.data.vertices];F += [tuple(off+i for i in f.vertices) for f in o.data.polygons]
tree=BVHTree.FromPolygons(V,F)
plate=bpy.data.objects['Шкура.щитки-проба@Змея'];plate.data.calc_loop_triangles();penetrating=0;minheight=1.;maxheight=0.;miss=0;edge_probes=0
for t in plate.data.loop_triangles:
    c=sum((plate.matrix_world@plate.data.vertices[i].co for i in t.vertices),Vector())/3
    hit=tree.ray_cast(Vector((c.x,.9,c.z)),Vector((0,-1,0)))
    if hit[0] is None:
        # Луч по границе исходных граней может промахнуться из-за float.
        near=[tree.ray_cast(Vector((c.x+dx,.9,c.z+dz)),Vector((0,-1,0))) for dx,dz in [(2e-6,0),(-2e-6,0),(0,2e-6),(0,-2e-6)]]
        near=[h for h in near if h[0] is not None]
        if not near:miss+=1;continue
        hit=min(near,key=lambda h:abs(c.y-h[0].y));edge_probes+=1
    h=c.y-hit[0].y;minheight=min(h,minheight);maxheight=max(h,maxheight);penetrating+=h<-.00001
report=dict(body_geometry_keys_weights_unchanged=body==body2,bones_unchanged=bones==bones2,bone_count=len(bones2),triangle_centers_below_skin=penetrating,ray_misses=miss,boundary_rays_with_2um_offset=edge_probes,min_clearance_y_m=minheight,max_clearance_y_m=maxheight,scope='Покой, ключи 0; не проверка анимации и контактов щитков друг с другом')
(P/'verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8');print(report)
assert report['body_geometry_keys_weights_unchanged'] and report['bones_unchanged'] and penetrating==0 and miss==0
