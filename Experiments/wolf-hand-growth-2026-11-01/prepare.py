import bpy,numpy as np,json
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
root=Path(__file__).resolve().parent
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(root.parent/'augment-combinations-2026-10-31/donor-human.fbx'),axis_forward='-Z',axis_up='Y',automatic_bone_orientation=False)
o=bpy.data.objects['Руки'];keys=o.data.shape_keys.key_blocks
P=[o.matrix_world@p.co for p in keys['Basis'].data]
D=np.array([list(o.matrix_world@q.co-p) for p,q in zip(P,keys['Волк·Руки'].data)])
F=[[f.vertices[0],f.vertices[i],f.vertices[i+1]] for f in o.data.polygons for i in range(1,len(f.vertices)-1)]
bvh=BVHTree.FromPolygons(P,F)
bpy.ops.wm.save_as_mainfile(filepath=str(root/'original.blend'))
bpy.ops.wm.open_mainfile(filepath=str(root.parent/'abdomen-refine-2026-10-31/candidate.blend'))
o=bpy.data.objects['Руки'];base=o.data.shape_keys.key_blocks['Basis'];key=o.shape_key_add(name='Волк·Руки',from_mix=False)
distances=[];deltas=[]
for i,p in enumerate(base.data):
    wp=o.matrix_world@p.co;point,_,face,d=bvh.find_nearest(wp);assert point is not None
    ids=F[face];a,b,c=[np.array(P[j]) for j in ids]
    uv=np.linalg.lstsq(np.column_stack((b-a,c-a)),np.array(point)-a,rcond=None)[0];w=np.array([1-uv.sum(),uv[0],uv[1]])
    delta=np.einsum('i,ij->j',w,D[ids]);key.data[i].co=o.matrix_world.inverted()@(wp+Vector(delta))
    distances.append(d);deltas.append(float(np.linalg.norm(delta)))
# Append just the unmodified existing claw organ to this isolated candidate.
arm=next(o for o in bpy.data.objects if o.type=='ARMATURE')
with bpy.data.libraries.load(str(root/'original.blend')) as (src,dst):dst.objects=['Руки@Волк']
organ=dst.objects[0];bpy.context.scene.collection.objects.link(organ);mw=organ.matrix_world.copy();organ.parent=arm;organ.matrix_world=mw
for mod in organ.modifiers:
    if mod.type=='ARMATURE':mod.object=arm
for o in bpy.data.objects:
    if o.type=='MESH' and o.data.shape_keys:
        for k in o.data.shape_keys.key_blocks:k.value=0
bpy.ops.wm.save_as_mainfile(filepath=str(root/'regional.blend'))
report=dict(source_fbx='augment-combinations-2026-10-31/donor-human.fbx',regional_key='Волк·Руки',projection_max_m=max(distances),projection_p95_m=float(np.quantile(distances,.95)),projection_mean_m=float(np.mean(distances)),nonzero_delta_vertices=sum(x>1e-7 for x in deltas),max_regional_delta_m=max(deltas),warning='Transferred regional key is an experimental approximation, not original game data. Original FBX row is the unmodified control. No global stage or growth scaling applied to geometry here.')
(root/'prepare.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8');print('PREPARE',report)
