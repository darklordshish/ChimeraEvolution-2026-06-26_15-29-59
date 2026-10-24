"""Measure actual shape-key skinning against the saved fitted posed surface.

This is the regression test, not the corrective algorithm. Exits nonzero above 0.1 mm.
"""
import bpy, sys, json
import numpy as np
from pathlib import Path
from mathutils import Matrix, Vector

def read_pose(path):
    return {b['name']: b for b in json.loads(Path(path).read_text(encoding='utf-8'))['bones']}

def skin_matrices(arm, pose):
    matrices = {}
    aw = arm.matrix_world
    for bone in arm.data.bones:
        if bone.name not in pose:
            continue
        p = pose[bone.name]
        h, t, z = (Vector(p[k]) for k in ('head','tail','z_axis'))
        y = (t-h).normalized(); x = y.cross(z).normalized(); z = x.cross(y)
        rot = Matrix((x,y,z)).transposed()
        rest = aw @ bone.matrix_local
        d = rot @ rest.to_3x3().inverted()
        matrices[bone.name] = np.array(Matrix.Translation(h) @ d.to_4x4() @ Matrix.Translation(-rest.translation), dtype=float)
    return matrices

def vertex_matrix(o, v, skin):
    pairs = [(o.vertex_groups[g.group].name,g.weight) for g in v.groups
             if o.vertex_groups[g.group].name in skin and g.weight>0]
    total = sum(w for _,w in pairs)
    return sum((skin[n]*(w/total) for n,w in pairs), np.zeros((4,4))) if total else np.eye(4)

def objects():
    return [o for o in bpy.data.objects if o.type=='MESH' and '@' not in o.name]

def worldpoint(o, co):
    return np.array(o.matrix_world @ co, dtype=float)

def reference(root):
    pts=[]
    for line in (root/'posed.obj').read_text().splitlines():
        if line.startswith('v '):
            x,y,z=map(float,line.split()[1:4]); pts.append((x,-z,y))
    return np.array(pts)

def evaluate(root):
    arm=next(o for o in bpy.data.objects if o.type=='ARMATURE')
    skin=skin_matrices(arm,read_pose(root/'pose.json'))
    expected=reference(root)
    ids={}; rows=[]; errors=[]; conditions=[]; over4=0
    for o in objects():
        kb=o.data.shape_keys.key_blocks['Волк']
        e=[]
        for v in o.data.vertices:
            key=tuple(round(float(x),5) for x in worldpoint(o,v.co))
            idx=ids.setdefault(key,len(ids))
            m=vertex_matrix(o,v,skin)
            p=(m @ np.r_[worldpoint(o,kb.data[v.index].co),1])[:3]
            d=float(np.linalg.norm(p-expected[idx])); e.append(d); errors.append(d)
            conditions.append(float(np.linalg.cond(m[:3,:3])))
            over4+=sum(o.vertex_groups[g.group].name in skin and g.weight>0 for g in v.groups)>4
        rows.append(dict(object=o.name,vertices=len(e),mean_m=float(np.mean(e)),p95_m=float(np.percentile(e,95)),max_m=max(e)))
    assert len(ids)==len(expected),(len(ids),len(expected))
    return dict(mean_m=float(np.mean(errors)),p95_m=float(np.percentile(errors,95)),max_m=max(errors),
                over4=int(over4),max_condition=max(conditions),objects=rows)

if __name__=='__main__':
    a=sys.argv[sys.argv.index('--')+1:]
    root=Path(a[0]).resolve(); bpy.ops.wm.open_mainfile(filepath=str(root/a[1]))
    result=evaluate(root)
    (root/a[2]).write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(result,ensure_ascii=False))
    assert result['max_m'] < 0.0001, 'Final skinning no longer reproduces the fitted surface (threshold 0.1 mm)'
    assert result['over4']==0
