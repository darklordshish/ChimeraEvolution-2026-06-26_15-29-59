import bpy,json
from pathlib import Path
from mathutils.bvhtree import BVHTree
from mathutils import Vector
P=Path(__file__).parent
rows={}
for fn in ['source.blend','snake-coarse.blend','candidate.blend']:
    bpy.ops.wm.open_mainfile(filepath=str(P/fn))
    V=[]; F=[]
    for nm in ['хребет','шея','Сердце','Руки']:
        o=bpy.data.objects[nm]; offset=len(V); V += [o.matrix_world@v.co for v in o.data.vertices]
        F += [tuple(offset+i for i in f.vertices) for f in o.data.polygons]
    tree=BVHTree.FromPolygons(V,F); base_area=sum((V[f[1]]-V[f[0]]).cross(V[f[2]]-V[f[0]]).length*.5 for f in F)
    report={'base_area':base_area,'skin_area':0.,'max_centroid_distance':0.,'max_vertex_distance':0.,'zero_area':0}
    for o in bpy.data.objects:
        if o.type!='MESH' or not o.name.startswith('Шкура.тон') or not o.name.endswith('@Змея'):continue
        o.data.calc_loop_triangles()
        for v in o.data.vertices:
            d=tree.find_nearest(o.matrix_world@v.co)[3];report['max_vertex_distance']=max(report['max_vertex_distance'],d)
        for t in o.data.loop_triangles:
            pts=[o.matrix_world@o.data.vertices[i].co for i in t.vertices]
            a=(pts[1]-pts[0]).cross(pts[2]-pts[0]).length*.5; report['skin_area']+=a; report['zero_area']+=a<1e-12
            d=tree.find_nearest(sum(pts,Vector())/3)[3];report['max_centroid_distance']=max(report['max_centroid_distance'],d)
    rows[fn]=report
(P/'coverage.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf8');print(rows)
