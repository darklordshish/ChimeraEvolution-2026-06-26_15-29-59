"""ЧЕРТА КЛЮЧА ИЗ САМОЙ ЦЕЛИ (19.10, брак ст.2 оборотня: «ничто не читается, мохра вместо шеи с дырками»; формат —
ГеймБосс 19.10). Волчьи куски гривы и головы на теле с ключом не дают формы листа: грива волка — тонкая оболочка лезвий,
голова на человеческой шее — шов со щелями. У цели (Hunyuan по колонке листа, та же поверхность, к которой ключ тянул
кожу) голова, воротник-грива, загривок и хвост — одна масса, как на листе. Отсюда черта ступени 2 — кусок самой цели:

  1. тело ключа на весе 1 (объекты шасси, форма-ключ `--imya`) — дерево для замера расстояния;
  2. от цели остаются грани, отстоящие от тела ключа дальше `--zazor` (мех), и голова выше `--golova_z` у средней линии;
     области: грива — выше `--griva_z` и не дальше `--griva_x` от средней линии; хвост — ниже `--hvost_z` и позади
     `--hvost_y` (Blender: −Y вперёд); всё остальное (руки, ноги) отбрасывается;
  3. островки меньше `--ostrov` граней выкидываются (обрезки меха на руках, «пояс» у корня хвоста);
  4. прорежение до `--dolya` (Decimate), грани плоские — стиль проекта;
  5. веса — переносом с тела ключа на весе 1 (ближайшая грань), не больше четырёх костей; объект `@<ключ>` встаёт ровно
     поверх тела с ключом 1 и скиннится к тем же костям — масштаба и шва нет (код — ГеймБосс: `BodyMesh` ставит его только
     на ступени 2 вида-ключа, снимает `skryvaet`, черты `zamenyaet_cherty` не ставит).

  blender -b --factory-startup -P kusok_celi.py -- ключ.blend цель.obj выход.fbx паспорт.json
      [--imya Волк] [--zazor 0.012] [--zazor_hvost 0.05] [--golova_z 1.50] [--griva_z 1.15] [--griva_x 0.42] [--hvost_z 1.0] [--hvost_y 0.12]
      [--ostrov 400] [--dolya 0.08] [--shots префикс]
Вход `ключ.blend` — сохранённый `kluch_vida.py --blend` (меш шасси с ключом и арматурой). Печатает «КУСОК: …».
"""
import sys, json, os
import bpy, bmesh
from mathutils import Vector
from mathutils.bvhtree import BVHTree

a = sys.argv[sys.argv.index('--') + 1:]
kblend, cel, out_fbx, pas_out = a[:4]
opt = dict(imya='Волк', zazor='0.012', zazor_hvost='0.05', golova_z='1.50', griva_z='1.15', griva_x='0.42', hvost_z='1.0', hvost_y='0.12',
           ostrov='400', dolya='0.08')
i = 4
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
NAME = opt['imya']; OBJ_NAME = '@' + NAME
HIDE = ['голова', 'Чутьё']                                  # объекты шасси, которые кусок снимает (голова и уши человека)

bpy.ops.wm.open_mainfile(filepath=kblend)
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
body = [o for o in bpy.data.objects if o.type == 'MESH']
for o in body:
    if o.data.shape_keys and NAME in o.data.shape_keys.key_blocks:
        o.data.shape_keys.key_blocks[NAME].value = 1.0
dg = bpy.context.evaluated_depsgraph_get()

# 1. тело ключа на весе 1 — дерево замера (без головы и ушей: их кусок заменяет) и источник весов (со всеми объектами)
V, F = [], []
for o in body:
    e = o.evaluated_get(dg); m = e.to_mesh()
    tmp = bmesh.new(); tmp.from_mesh(m); tmp.transform(o.matrix_world)
    if o.name not in HIDE:
        n0 = len(V)
        V += [v.co.copy() for v in tmp.verts]; F += [[n0 + v.index for v in f.verts] for f in tmp.faces]
    tmp.free()
bvh = BVHTree.FromPolygons(V, F)

# источник весов: копии объектов шасси с применённым ключом, группы вершин — те же
srcs = []
for o in body:
    c = o.copy(); c.data = o.data.copy(); bpy.context.scene.collection.objects.link(c)
    if c.data.shape_keys:
        for kb in c.data.shape_keys.key_blocks:
            kb.value = 1.0 if kb.name == NAME else 0.0
        c.shape_key_add(name='mix', from_mix=True)
        mix = [v.co.copy() for v in c.data.shape_keys.key_blocks['mix'].data]
        c.shape_key_clear()
        for v, p in zip(c.data.vertices, mix): v.co = p
    c.modifiers.clear(); c.parent = None; c.matrix_world = o.matrix_world.copy()
    srcs.append(c)

# 2. цель и отбор граней
bpy.ops.wm.obj_import(filepath=cel, forward_axis='NEGATIVE_Z', up_axis='Y')
t = [o for o in bpy.context.selected_objects if o.type == 'MESH'][0]
bpy.context.view_layer.objects.active = t
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
gap, gz, mz, mx, hz, hy, gt = (float(opt[k]) for k in ('zazor', 'golova_z', 'griva_z', 'griva_x', 'hvost_z', 'hvost_y', 'zazor_hvost'))
bm = bmesh.new(); bm.from_mesh(t.data); bm.faces.ensure_lookup_table()
n_all = len(bm.faces)
kill = []
for f in bm.faces:
    c = f.calc_center_median()
    d = bvh.find_nearest(c)[3]
    head = c.z > gz and abs(c.x) < 0.25
    mane = c.z > mz and abs(c.x) < mx and d > gap
    tail = c.z < hz and c.y > hy and d > (gap if c.z > 0.75 else gt)     # ниже ягодиц — дальше от тела: мех икр цели в хвост не идёт
    if not (head or mane or tail):
        kill.append(f)
bmesh.ops.delete(bm, geom=kill, context='FACES')

# 3. островки
bm.faces.ensure_lookup_table()
seen, drop, isl = set(), [], []
for f in bm.faces:
    if f.index in seen: continue
    comp, stack = [], [f]; seen.add(f.index)
    while stack:
        g = stack.pop(); comp.append(g)
        for e in g.edges:
            for h in e.link_faces:
                if h.index not in seen:
                    seen.add(h.index); stack.append(h)
    isl.append(len(comp))
    if len(comp) < int(opt['ostrov']):
        drop += comp
bmesh.ops.delete(bm, geom=drop, context='FACES')
loose = [v for v in bm.verts if not v.link_faces]
bmesh.ops.delete(bm, geom=loose, context='VERTS')
bm.to_mesh(t.data); bm.free()
print('КУСОК: граней цели %d → отобрано %d; островков %d, выброшено мельче %s: %d граней' % (
    n_all, len(t.data.polygons), len(isl), opt['ostrov'], len(drop)))

# 4. прорежение и плоские грани
md = t.modifiers.new('прореж', 'DECIMATE'); md.ratio = float(opt['dolya'])
bpy.ops.object.modifier_apply(modifier='прореж')
for p in t.data.polygons:
    p.use_smooth = False
t.name = OBJ_NAME; t.data.name = OBJ_NAME

# 5. веса — с тела ключа на весе 1
# ближайший объект-источник на вершину: веса этого объекта, интерполяция по ближайшей грани
src_bvh = []
for s in srcs:
    me = s.data
    src_bvh.append((s, BVHTree.FromPolygons([s.matrix_world @ v.co for v in me.vertices], [list(p.vertices) for p in me.polygons])))
W = [dict() for _ in t.data.vertices]
for v in t.data.vertices:
    best = None
    for s, b in src_bvh:
        hit = b.find_nearest(v.co)
        if hit[0] is not None and (best is None or hit[3] < best[2]):
            best = (s, hit, hit[3])
    s, (loc, nrm, fi, d), _ = best
    poly = s.data.polygons[fi]
    ws = {}
    pts = [s.matrix_world @ s.data.vertices[k].co for k in poly.vertices]
    inv = [1.0 / max(1e-6, (loc - p).length) for p in pts]; tot = sum(inv)
    for k, wk in zip(poly.vertices, inv):
        for ge in s.data.vertices[k].groups:
            nm = s.vertex_groups[ge.group].name
            ws[nm] = ws.get(nm, 0.0) + ge.weight * wk / tot
    top = sorted(ws.items(), key=lambda kv: -kv[1])[:4]; sm = sum(x for _, x in top) or 1.0
    W[v.index] = {nm: x / sm for nm, x in top if x > 1e-4}
for g in list(t.vertex_groups):
    t.vertex_groups.remove(g)
groups = {}
for vi, ws in enumerate(W):
    for nm, x in ws.items():
        if nm not in groups:
            groups[nm] = t.vertex_groups.new(name=nm)
        groups[nm].add([vi], x, 'REPLACE')
for s in srcs:
    bpy.data.objects.remove(s, do_unlink=True)
t.parent = arm
mdl = t.modifiers.new('Скелет', 'ARMATURE'); mdl.object = arm
for o in body:
    if o.data.shape_keys and NAME in o.data.shape_keys.key_blocks:
        o.data.shape_keys.key_blocks[NAME].value = 0.0
print('КУСОК: «%s» %d тр, %d вершин, костей %d: %s' % (OBJ_NAME, len(t.data.polygons), len(t.data.vertices), len(groups),
                                                     ', '.join(sorted(groups))))

# экспорт — как у kluch_vida
bpy.ops.object.select_all(action='SELECT')
bpy.context.view_layer.objects.active = arm
bpy.ops.export_scene.fbx(filepath=os.path.abspath(out_fbx), use_selection=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z',
                         axis_up='Y', object_types={'ARMATURE', 'MESH'}, add_leaf_bones=False, bake_anim=False,
                         mesh_smooth_type='FACE', use_armature_deform_only=False)
pas = {'kluch': NAME, 'object': OBJ_NAME, 'step': 2, 'skryvaet': HIDE, 'zamenyaet_cherty': ['голова', 'Пасть', 'Пасть.полость', 'Пасть.зубы', 'Шкура', 'Хвост'],
       'tris': len(t.data.polygons),
       'note': 'черта ключа «%s» на ступени 2 — кусок самой цели (голова, воротник-грива, загривок, хвост) в метрах шасси на весе 1, '
               'скиннится к костям шасси; skryvaet — объекты меша шасси, снимаемые при куске; zamenyaet_cherty — объекты черт '
               'вида-ключа, которые на ступени 2 не ставятся' % NAME,
       'generator': 'Tools/Blender/proba/kusok_celi.py ' + ' '.join('--%s %s' % kv for kv in sorted(opt.items()))}
json.dump(pas, open(pas_out, 'w', encoding='utf-8'), ensure_ascii=False, indent=2)
if 'blend' in opt:
    bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(opt['blend']))
print('КУСОК: готово → %s, паспорт → %s' % (out_fbx, pas_out))
