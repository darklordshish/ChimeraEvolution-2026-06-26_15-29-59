# ПОКОЙ РИГА К2 (22.10, ГеймБосс: поворот позы D = рамка позы · покой РИГА⁻¹ — рамки костей рига и графа в Unity повёрнуты по-разному,
# хребет на 35°). Кости арматуры шасси в покое: head/tail/z_axis — в осях и формате *-poza.json (z_axis — ось Z рамки кости).
#   blender -b --factory-startup -P pokoj_riga.py -- шасси.blend поза.json выход.json
import bpy, sys, json
from mathutils import Vector
a = sys.argv[sys.argv.index('--') + 1:]
bpy.ops.wm.open_mainfile(filepath=a[0])
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]; AW = arm.matrix_world
pz = json.load(open(a[1], encoding='utf-8'))
out = []
for b in pz['bones']:
    bb = arm.data.bones.get(b['name'])
    if bb is None: continue
    M = AW @ bb.matrix_local
    z = (M.to_3x3() @ Vector((0, 0, 1))).normalized()
    out.append(dict(name=b['name'], parent=b.get('parent', ''), head=[round(c, 5) for c in AW @ bb.head_local],
                    tail=[round(c, 5) for c in AW @ bb.tail_local], z_axis=[round(c, 5) for c in z]))
json.dump(dict(species=pz.get('species', 'Человек'), axes=pz.get('axes', ''), bones=out,
               note='кости рига К2 в ПОКОЕ (рамки как в *-poza.json): игра считает поворот позы D = поза · покой рига⁻¹; pokoj_riga.py'),
          open(a[2], 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
print('ПОКОЙ: %d костей → %s' % (len(out), a[2]))
