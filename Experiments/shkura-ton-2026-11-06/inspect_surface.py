import bpy,json
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
P=Path(__file__).parent
bpy.ops.wm.open_mainfile(filepath=str(P/'source.blend'))
trees={}
for o in bpy.data.objects:
    if o.type=='MESH' and '@' not in o.name:
        trees[o.name]=BVHTree.FromPolygons([o.matrix_world@v.co for v in o.data.shape_keys.key_blocks[0].data] if o.data.shape_keys else [o.matrix_world@v.co for v in o.data.vertices],[tuple(f.vertices) for f in o.data.polygons])
rows=[]
for z in [1.15,1.25,1.35,1.45,1.55]:
    for x in [-.10,0,.10]:
        hits=[]
        for nm,tree in trees.items():
            hit=tree.ray_cast(Vector((x,1,z)),Vector((0,-1,0)))
            if hit[0] is not None: hits.append((hit[3],nm))
        rows.append(dict(x=x,z=z,visible=min(hits)[1] if hits else None))
(P/'surface-owners.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf8')
print(rows)
