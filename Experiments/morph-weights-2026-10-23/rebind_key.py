"""Recompute ONE experimental key from the retained posed fit and FINAL weights.

Not a game export: other keys and partial-key recipes must be rebuilt separately.
Reject ill-conditioned inverse transforms instead of silently amplifying errors.
"""
import sys, json
from pathlib import Path
import bpy
import numpy as np
from mathutils import Vector
sys.path.insert(0,str(Path(__file__).resolve().parent))
from check_roundtrip import read_pose, skin_matrices, vertex_matrix, objects, worldpoint, reference

a=sys.argv[sys.argv.index('--')+1:]; root=Path(a[0]).resolve()
bpy.ops.wm.open_mainfile(filepath=str(root/'weights.blend'))
arm=next(o for o in bpy.data.objects if o.type=='ARMATURE')
skin=skin_matrices(arm,read_pose(root/'pose.json')); target=reference(root)
ids={}; maxcond=0; shifts=[]
for o in objects():
    key=o.data.shape_keys.key_blocks['Волк']; inv=o.matrix_world.inverted()
    for v in o.data.vertices:
        idx=ids.setdefault(tuple(round(float(x),5) for x in worldpoint(o,v.co)),len(ids))
        m=vertex_matrix(o,v,skin)
        cond=float(np.linalg.cond(m[:3,:3])); maxcond=max(cond,maxcond)
        if not np.isfinite(cond) or cond>100:
            raise RuntimeError(f'Unstable inverse: {o.name}:{v.index}, condition={cond}')
        p=np.linalg.solve(m,np.r_[target[idx],1])[:3]
        shifts.append(float(np.linalg.norm(p-worldpoint(o,key.data[v.index].co))))
        key.data[v.index].co=inv @ Vector(p)
assert len(ids)==len(target)
bpy.ops.wm.save_as_mainfile(filepath=str(root/'corrected.blend'))
print(json.dumps(dict(max_condition=maxcond,max_rest_shift_m=max(shifts)),ensure_ascii=False))
