import bpy
from pathlib import Path
r=Path.cwd();bpy.ops.wm.open_mainfile(filepath=str(r/'Experiments/abdomen-refine-2026-10-31/candidate.blend'))
for o in bpy.data.objects:
 if o.type=='MESH':print('MESH',o.name,'tris',sum(len(f.vertices)-2 for f in o.data.polygons),'keys',list(o.data.shape_keys.key_blocks.keys()) if o.data.shape_keys else [])
