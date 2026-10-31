import bpy,sys,json
import numpy as np
from mathutils import Vector
from pathlib import Path
a=sys.argv[sys.argv.index('--')+1:]
bpy.ops.wm.open_mainfile(filepath=a[0])
bundle=np.load(a[1]);weights=bundle['weights'];names=[str(n) for n in bundle['bone_names']]
config=json.loads(Path(a[2]).read_text(encoding='utf-8'))['selected']
arm=next(o for o in bpy.data.objects if o.type=='ARMATURE');aw=arm.matrix_world
old_bones={b.name for b in arm.data.bones}
objects=[o for o in bpy.data.objects if o.type=='MESH' and '@' not in o.name and o.data.shape_keys and 'Поджарый' in o.data.shape_keys.key_blocks]
assert sum(len(o.data.vertices) for o in objects)==len(weights)
bpy.context.view_layer.objects.active=arm
bpy.ops.object.mode_set(mode='EDIT')
for suffix in ('','.L'):
    source=arm.data.edit_bones['плечо'+suffix]
    h=aw@source.head;h.z-=config['offset']
    b=arm.data.edit_bones.new('опора_подмышки'+suffix)
    b.head=aw.inverted()@h;b.tail=aw.inverted()@(h+Vector((0,.12,0)))
    b.parent=arm.data.edit_bones['хребет'];b.use_connect=False;b.use_deform=True
bpy.ops.object.mode_set(mode='OBJECT')
start=0;changed=0
for o in objects:
    old=[{o.vertex_groups[g.group].name:g.weight for g in v.groups if o.vertex_groups[g.group].name in old_bones} for v in o.data.vertices]
    for group in list(o.vertex_groups):
        if group.name in old_bones:o.vertex_groups.remove(group)
    groups={n:o.vertex_groups.get(n) or o.vertex_groups.new(name=n) for n in names}
    for i,v in enumerate(o.data.vertices):
        row=weights[start+i];ws={names[j]:float(w) for j,w in enumerate(row) if w>1e-8}
        assert len(ws)<=4 and abs(sum(ws.values())-1)<1e-6
        for n,w in ws.items():groups[n].add([i],w,'REPLACE')
        if any(abs(w-old[i].get(n,0))>1e-6 for n,w in ws.items()) or any(n not in ws for n in old[i]):changed+=1
    start+=len(o.data.vertices)
    for key in o.data.shape_keys.key_blocks:key.value=0
bpy.ops.wm.save_as_mainfile(filepath=a[3])
Path(a[4]).write_text(json.dumps(dict(source=a[0],config=config,new_bones=names[-2:],changed_vertex_records=changed,
    neutral_geometry_delta_m=0,max_influences=4,
    warning='Other species keys have unchanged coordinates but changed skinning: must be revalidated/rebound before any production use.'),ensure_ascii=False,indent=2),encoding='utf-8')
print('SUPPORT_RIG',config,'changed weights',changed)
