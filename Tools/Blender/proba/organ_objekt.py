"""ОРГАН НА ШАБЛОНЕ (22.10, CONSTRUCTOR_GUIDE «Орган на шаблоне», спека `2026-10-21-organ-prorastaet-klyuch-oblasti.md` §2.1): объект
`<слот>@<вид>` в меше шасси, привязан к его костям, виден только при привитом органе этого вида на этот слот. Растёт формой-ключом
`рост`: база — объект, прижатый к телу шасси (каждая вершина сдвинута к ближайшей точке тела в покое на `--sjatie`), `рост` на весе 1 —
полная форма. Код ставит вес `рост` = доля вида / 0.65.

  blender -b --factory-startup -P organ_objekt.py -- сборка.blend откуда.blend имя_там <слот>@<вид> выход.blend [--sjatie 0.85]
"""
import sys
import bpy
from mathutils.bvhtree import BVHTree

a = sys.argv[sys.argv.index('--') + 1:]
base, src, nm_src, nm, out = a[:5]
opt = {}
i = 5
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
SJ = float(opt.get('sjatie', '0.85'))

bpy.ops.wm.open_mainfile(filepath=base)
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
old = bpy.data.objects.get(nm)
if old: bpy.data.objects.remove(old, do_unlink=True)
body = [o for o in bpy.data.objects if o.type == 'MESH' and '@' not in o.name]
V, F = [], []
for o in body:
    n0 = len(V); V += [o.matrix_world @ v.co for v in o.data.vertices]; F += [[n0 + k for k in p.vertices] for p in o.data.polygons]
bvh = BVHTree.FromPolygons(V, F)
with bpy.data.libraries.load(src) as (s_, d_): d_.objects = [nm_src]
o = d_.objects[0]; bpy.context.scene.collection.objects.link(o)
o.name = nm; o.data.name = nm; o.parent = arm
for m in o.modifiers:
    if m.type == 'ARMATURE': m.object = arm
if o.data.shape_keys: o.shape_key_clear()
full = [v.co.copy() for v in o.data.vertices]
for v in o.data.vertices:
    w = o.matrix_world @ v.co; loc = bvh.find_nearest(w)[0]
    if loc is not None: v.co = o.matrix_world.inverted() @ (w + (loc - w) * SJ)
o.shape_key_add(name='Basis', from_mix=False)
kb = o.shape_key_add(name='рост', from_mix=False)
for k, p in enumerate(full): kb.data[k].co = p
print('ОРГАН: %s — %d тр, база прижата к телу на %.0f %%, «рост» — полная форма' % (nm, len(o.data.polygons), SJ * 100))
bpy.ops.wm.save_as_mainfile(filepath=out)
