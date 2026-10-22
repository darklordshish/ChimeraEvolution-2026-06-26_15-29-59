"""ОБЛАСТИ ШАБЛОНА К2 (22.10, П1 плана «человек → волк»; спека `2026-10-21-organ-prorastaet-klyuch-oblasti.md`): группы вершин,
по которым код при импорте обрезает ключ вида до ключа ОБЛАСТИ (орган меняет форму своей части тела по доле донора).

Область — по главной кости вершины и её БАЗОВОМУ положению (до ключей), детерминированно: те же вершины при каждом прогоне
и под любым ключом. Вес группы 1 внутри, на границе — плавный переход полосой `--polosa` м (ключ области не рвёт кожу на шве):

  «Руки.область»   кисть + предплечье (кость предплечье);
  «Сердце.область» грудная клетка спереди и с боков (кости грудная клетка, живот выше пупка) — клин груди;
  «Ноги.область»   бедро, голень, стопа;
  «Шкура.область»  покров: загривок, холка, спина и лопатки сзади (под гривой);
  «Пасть.область»  голова (челюсть, скулы — масса под мордой);
  «Чутьё.область»  голова (основания ушей и череп).
Имена — `<орган>.область`: по слоту органа, как куски (`Organ.slot`).

  blender -b --factory-startup -P oblasti_k2.py -- вход.blend выход.blend [--polosa 0.04] [--kadr префикс] [--klyuchi Волк,Лось [--klyuchi_obl Руки,Сердце,Шкура]]
"""
import sys
import bpy
import numpy as np

a = sys.argv[sys.argv.index('--') + 1:]
src, out = a[0], a[1]
opt = dict(polosa='0.04')
i = 2
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
POL = float(opt['polosa'])

bpy.ops.wm.open_mainfile(filepath=src)
body = [o for o in bpy.data.objects if o.type == 'MESH' and not o.name.startswith('@')]


def oblast(bn, p):
    """область вершины по главной кости и базовому положению (x, y — вперёд «−», z)"""
    b = bn.split('.')[0]; x, y, z = p
    out_ = set()
    if b == 'предплечье': out_.add('Руки')
    if b in ('бедро', 'голень', 'стопа'): out_.add('Ноги')
    if b in ('голова',): out_ |= {'Пасть', 'Чутьё'}
    if b in ('грудная клетка',) or (b == 'живот' and z > 1.12):
        if y < 0.06: out_.add('Сердце')
    if b in ('шея', 'лопатка', 'хребет', 'грудная клетка', 'живот') and y > 0.0 and z > 1.05:
        out_.add('Шкура')
    return out_


itog = {}
for o in body:
    Mw = o.matrix_world; gn = {g.index: g.name for g in o.vertex_groups}
    P = np.array([list(Mw @ v.co) for v in o.data.vertices])
    lab = []
    for v, p in zip(o.data.vertices, P):
        bn = gn[max(v.groups, key=lambda g: g.weight).group] if v.groups else ''
        lab.append(oblast(bn, p))
    nb = [set() for _ in P]
    for e in o.data.edges:
        nb[e.vertices[0]].add(e.vertices[1]); nb[e.vertices[1]].add(e.vertices[0])
    for org in ('Руки', 'Сердце', 'Ноги', 'Шкура', 'Пасть', 'Чутьё'):
        inside = np.array([org in l for l in lab])
        if not inside.any(): continue
        # вес: 1 внутри; снаружи — по расстоянию до ближайшей вершины области, спад на полосе POL
        w = inside.astype(float)
        if POL > 0:
            Pi = P[inside]
            for k in np.nonzero(~inside)[0]:
                d = np.min(np.linalg.norm(Pi - P[k], axis=1))
                if d < POL: w[k] = 1.0 - d / POL
        nm = org + '.область'
        g = o.vertex_groups.get(nm) or o.vertex_groups.new(name=nm)
        g.remove(list(range(len(P))))
        for k in np.nonzero(w > 1e-3)[0]: g.add([int(k)], float(w[k]), 'REPLACE')
        itog[nm] = itog.get(nm, 0) + int(inside.sum())
print('ОБЛАСТИ: ' + ', '.join('%s %d' % kv for kv in sorted(itog.items())))

# КЛЮЧИ ОБЛАСТЕЙ (`--klyuchi Волк,Лось`, ГеймБосс 22.10: Unity не импортирует группы вершин, поэтому ключ области запекается
# формой-ключом меша): «<ключ>·<слот>» = база + маска области × (ключ вида − база). Вес в игре — доля вида-донора органа
# слота, с глобальным ключом — супремум (код ГеймБосса)
# какие области запекать ключом (`--klyuchi_obl`, 22.10, ряд в игре): голова К2 под ключом вида НЕ подогнана (голову даёт кусок),
# и ключи «·Пасть»/«·Чутьё» раздували человеческую голову уже на доле 15 %; «·Ноги» без позы ноги давал ласту вместо лапы
# (пальцеходящая стопа — это поза костей, а не форма) — до слоя позы по области их нет
KL_OBL = opt.get('klyuchi_obl', 'Руки,Сердце,Шкура').split(',')
if opt.get('klyuchi'):
    n_k = 0
    for o in body:
        ks = o.data.shape_keys
        if ks is None: continue
        base = ks.key_blocks[0]
        for kl in opt['klyuchi'].split(','):
            if kl not in ks.key_blocks: continue
            src_ = ks.key_blocks[kl]
            for org in KL_OBL:
                g = o.vertex_groups.get(org + '.область')
                if g is None: continue
                w = {}
                for v in o.data.vertices:
                    for ge in v.groups:
                        if ge.group == g.index: w[v.index] = ge.weight
                if not w: continue
                nm = kl + '·' + org
                if nm in ks.key_blocks: o.shape_key_remove(ks.key_blocks[nm])
                kb = o.shape_key_add(name=nm, from_mix=False)
                for vi in range(len(o.data.vertices)):
                    b0 = base.data[vi].co; d = src_.data[vi].co - b0
                    kb.data[vi].co = b0 + d * w.get(vi, 0.0)
                n_k += 1
    print('КЛЮЧИ ОБЛАСТЕЙ: %d форм-ключей (%s)' % (n_k, opt['klyuchi']))
    bpy.ops.wm.save_as_mainfile(filepath=out)
bpy.ops.wm.save_as_mainfile(filepath=out)

if opt.get('kadr'):
    # кадр: тело в покое, раскраска по областям (Пасть и Чутьё — одна голова)
    C = {'Руки': (0.9, 0.5, 0.1, 1), 'Сердце': (0.9, 0.15, 0.15, 1), 'Ноги': (0.2, 0.45, 0.9, 1), 'Шкура': (0.2, 0.75, 0.3, 1),
         'Пасть': (0.75, 0.3, 0.85, 1)}
    for o in body:
        ca = o.data.color_attributes.new('обл', 'FLOAT_COLOR', 'POINT')
        gi = {g.name: g.index for g in o.vertex_groups}
        for v in o.data.vertices:
            col = (0.6, 0.6, 0.6, 1)
            ws = {g.group: g.weight for g in v.groups}
            for org, c in C.items():
                j = gi.get(org + '.область')
                if j is not None and ws.get(j, 0) > 0.5: col = c
            ca.data[v.index].color = col
    from mathutils import Vector
    sc = bpy.context.scene; sc.render.engine = 'BLENDER_WORKBENCH'; sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'VERTEX'
    sc.render.resolution_x = 700; sc.render.resolution_y = 1000
    cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); sc.collection.objects.link(cam); sc.camera = cam
    cam.data.type = 'ORTHO'; cam.data.ortho_scale = 2.2
    for nm, d in (('front', Vector((0, -1, 0))), ('back', Vector((0, 1, 0))), ('side', Vector((-1, 0, 0)))):
        cam.location = Vector((0, 0, 0.95)) + d * 10; cam.rotation_euler = d.to_track_quat('Z', 'Y').to_euler()
        sc.render.filepath = opt['kadr'] + '_' + nm + '.png'; bpy.ops.render.render(write_still=True)
