import bpy,json
from pathlib import Path
P=Path(__file__).parent
out={}
for fn in ['source.blend','snake-expanded.blend']:
    bpy.ops.wm.open_mainfile(filepath=str(P/fn)); rows=[]
    for o in bpy.data.objects:
        if o.type!='MESH' or not o.name.startswith('Шкура.тон') or not o.name.endswith('@Змея'): continue
        o.data.calc_loop_triangles(); area=[]; repeated=0
        for f in o.data.loop_triangles:
            pts=[o.matrix_world@o.data.vertices[i].co for i in f.vertices]
            area.append((pts[1]-pts[0]).cross(pts[2]-pts[0]).length*.5)
            repeated+=len(set(f.vertices))!=3
        rows.append(dict(name=o.name,tris=len(area),zero_area=sum(a<1e-12 for a in area),repeated_indices=repeated))
    out[fn]=rows
(P/'degenerate-source.json').write_text(json.dumps(out,ensure_ascii=False,indent=2),encoding='utf8'); print(out)
