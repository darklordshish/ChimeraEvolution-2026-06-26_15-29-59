import bpy,json
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from mathutils.geometry import barycentric_transform
P=Path(__file__).parent
bpy.ops.wm.open_mainfile(filepath=str(P/'candidate.blend'))
V=[]; F=[]; K={}
for nm in ['хребет','шея','Сердце','Руки']:
    o=bpy.data.objects[nm]; me=o.data; me.calc_loop_triangles(); offset=len(V)
    V += [o.matrix_world@v.co for v in me.vertices]
    F += [tuple(offset+i for i in f.vertices) for f in me.loop_triangles]
    for k in me.shape_keys.key_blocks:
        K.setdefault(k.name,{})
        for i,v in enumerate(k.data): K[k.name][offset+i]=o.matrix_world@v.co
tree=BVHTree.FromPolygons(V,F,all_triangles=True)
report={'max_distance_to_base_m':0.,'max_interpolated_key_error_m':0.,'keys':{},'vertices':0}
for o in bpy.data.objects:
    if o.type!='MESH' or not o.name.startswith('Шкура.тон') or not o.name.endswith('@Змея'): continue
    for i,v in enumerate(o.data.vertices):
        p=o.matrix_world@v.co; h,n,fi,d=tree.find_nearest(p); ids=F[fi]; a,b,c=[V[j] for j in ids]
        report['vertices']+=1; report['max_distance_to_base_m']=max(d,report['max_distance_to_base_m'])
        for k in o.data.shape_keys.key_blocks:
            errs=[]
            # На шве совпадают несколько исходных треугольников разных слотов.
            for h2,n2,fi2,d2 in tree.find_nearest_range(p,1e-5):
                ids2=F[fi2]; abc=[V[j] for j in ids2]
                target=barycentric_transform(h2,*abc,*[K.get(k.name,{}).get(j,V[j]) for j in ids2])
                errs.append((o.matrix_world@k.data[i].co-target).length)
            err=min(errs)
            report['keys'][k.name]=max(report['keys'].get(k.name,0.),err)
            report['max_interpolated_key_error_m']=max(err,report['max_interpolated_key_error_m'])
(P/'interpolation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8');print(report)
assert report['max_distance_to_base_m']<1e-5
assert report['max_interpolated_key_error_m']<.001
