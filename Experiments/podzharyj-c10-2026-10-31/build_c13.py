"""Separate pose correction from the approved neutral silhouette.

Default shape = c10b. Optional correction = c12-c10b as a relative shape key.
The correction is NOT automatically enabled and has no game integration.
"""
import bpy,sys,json
from mathutils import Vector
a=sys.argv[sys.argv.index('--')+1:]
KEY='Поджарый'; PSD='Поджарый·ПодъёмПлеч'
bpy.ops.wm.open_mainfile(filepath=a[1])
posed={o.name:[v.co.copy() for v in o.data.shape_keys.key_blocks[KEY].data]
       for o in bpy.data.objects if o.type=='MESH' and '@' not in o.name and o.data.shape_keys and KEY in o.data.shape_keys.key_blocks}
bpy.ops.wm.open_mainfile(filepath=a[0])
report=[]
for o in bpy.data.objects:
    if o.name not in posed:continue
    sk=o.data.shape_keys; base=sk.key_blocks[0]; neutral=sk.key_blocks[KEY]
    deltas=[p-v.co for p,v in zip(posed[o.name],neutral.data)]
    if max((v.length for v in deltas),default=0)<1e-7:continue
    correction=o.shape_key_add(name=PSD,from_mix=False); correction.relative_key=base
    error=0
    for i,d in enumerate(deltas):
        correction.data[i].co=base.data[i].co+d
        combined=neutral.data[i].co+(correction.data[i].co-base.data[i].co)
        error=max(error,(combined-posed[o.name][i]).length)
    correction.value=0
    report.append(dict(object=o.name,vertices=sum(d.length>1e-7 for d in deltas),max_delta_m=max(d.length for d in deltas),composition_error_m=error))
for o in bpy.data.objects:
    if o.type=='MESH' and o.data.shape_keys:
        for kb in o.data.shape_keys.key_blocks:kb.value=0
bpy.ops.wm.save_as_mainfile(filepath=a[2])
with open(a[3],'w',encoding='utf-8') as f:
    json.dump(dict(neutral=a[0],correction_target=a[1],correction_key=PSD,default_weight=0,objects=report),f,ensure_ascii=False,indent=2)
print('C13',report)
