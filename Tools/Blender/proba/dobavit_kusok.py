# ДОБАВИТЬ КУСОК @ из другой сборки под новым именем (тот же скелет К2): база.blend откуда.blend имя_там имя_тут выход.blend
# добавить кусок из другой сборки под новым именем: база.blend откуда.blend имя_там имя_тут выход.blend
import bpy, sys
a = sys.argv[sys.argv.index('--') + 1:]
bpy.ops.wm.open_mainfile(filepath=a[0])
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
old = bpy.data.objects.get(a[3])
if old: bpy.data.objects.remove(old, do_unlink=True)
with bpy.data.libraries.load(a[1]) as (src, dst): dst.objects = [a[2]]
o = dst.objects[0]; bpy.context.scene.collection.objects.link(o); o.name = a[3]; o.data.name = a[3]; o.parent = arm
for m in o.modifiers:
    if m.type == 'ARMATURE': m.object = arm
print('ДОБАВЛЕНО: %s %d тр' % (o.name, len(o.data.polygons)))
bpy.ops.wm.save_as_mainfile(filepath=a[4])
