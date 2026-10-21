# СЛИТЬ КУСКИ (21.10): голова из голой цели и грива с хвостом из колонки — в один объект @<ключ> (группы вершин сливаются по именам)
# слить куски @ из нескольких сборок (тот же скелет К2) в один объект на арматуре base: base.blend имя выход.blend куски1.blend куски2.blend …
import bpy, sys
a = sys.argv[sys.argv.index('--') + 1:]
base, nm, out, srcs = a[0], a[1], a[2], a[3:]
bpy.ops.wm.open_mainfile(filepath=base)
for o in [o for o in bpy.data.objects if o.name.startswith('@')]: bpy.data.objects.remove(o, do_unlink=True)
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
parts = []
for s in srcs:
    with bpy.data.libraries.load(s) as (src, dst): dst.objects = [nm]
    o = dst.objects[0]; bpy.context.scene.collection.objects.link(o); parts.append(o)
bpy.ops.object.select_all(action='DESELECT')
for o in parts: o.select_set(True)
bpy.context.view_layer.objects.active = parts[0]
bpy.ops.object.join()
j = bpy.context.view_layer.objects.active; j.name = nm; j.data.name = nm; j.parent = arm
for m in j.modifiers:
    if m.type == 'ARMATURE': m.object = arm
print('СЛИТО: %s %d тр' % (nm, len(j.data.polygons)))
bpy.ops.wm.save_as_mainfile(filepath=out)
