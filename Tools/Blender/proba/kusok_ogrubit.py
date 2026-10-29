"""КУСОК ГРУБЕЕ НА ЧАСТИ ТЕЛА (29.10, критик новой ст.2 п.1 «ботфорт»: мера `shov_mera.py` даёт ступеньку радиуса −1…+2 % и в покое, и
в позе — толщины порога нет; на кадре шов читается сменой фактуры: ниже шва кусок цели густой и гладкий, выше — крупные грани
огранки тела). Кусок прореживается схлопыванием рёбер (Decimate по группе вершин) только на вершинах с главной костью из
`--kosti` до доли `--dolya` — грани лапы той же крупности, что у тела; голова, гребень и кисти не трогаются. Веса костей Blender
переносит сам. Шаг — до шва куска (`shov_kuska.py` садит новый край на тело).

  blender -b --factory-startup -P kusok_ogrubit.py -- вход.blend выход.blend [--kusok @Волк] [--kosti бедро,голень,стопа] [--dolya 0.35]
"""
import sys
import bpy

a = sys.argv[sys.argv.index('--') + 1:]
src, out = a[0], a[1]
opt = dict(kusok='@Волк', kosti='бедро,голень,стопа', dolya='0.35')
i = 2
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
KO = set(opt['kosti'].split(','))
bpy.ops.wm.open_mainfile(filepath=src)
o = bpy.data.objects[opt['kusok']]; gn = {g.index: g.name for g in o.vertex_groups}
vg = o.vertex_groups.new(name='~ogrubit')
ids = [v.index for v in o.data.vertices if v.groups and gn[max(v.groups, key=lambda g: g.weight).group].split('.')[0] in KO]
vg.add(ids, 1.0, 'REPLACE')
n0 = len(o.data.polygons)
for m in list(o.modifiers):
    if m.type != 'ARMATURE': o.modifiers.remove(m)
md = o.modifiers.new('ogrubit', 'DECIMATE'); md.decimate_type = 'COLLAPSE'; md.ratio = float(opt['dolya']); md.vertex_group = vg.name
md.use_collapse_triangulate = True
bpy.context.view_layer.objects.active = o
for m in list(o.modifiers):            # Decimate — первым, до арматуры (применяется к покою)
    if m.type == 'ARMATURE': o.modifiers.move(o.modifiers.find(m.name), len(o.modifiers) - 1)
bpy.ops.object.modifier_apply(modifier=md.name)
o.vertex_groups.remove(o.vertex_groups['~ogrubit'])
print('КУСОК-ГРУБЕЕ: %s — на костях %s вершин %d, граней всего %d → %d (доля %.2f)' % (o.name, ','.join(sorted(KO)), len(ids), n0, len(o.data.polygons), float(opt['dolya'])))
bpy.ops.wm.save_as_mainfile(filepath=out)
