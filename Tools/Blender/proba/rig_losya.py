"""РИГ ЛОСЯ-МЕША (14.10): меш по листу (`mesh_losya.py`, кандидат C) на скелет, переставленный внутрь меша
(`skelet_po_meshu.py`; решение геймдизайнера 14.10 «подогнать скелет под меш»). Тот же тракт, что у волка и человека:
имена костей — контракт, объект меша = место (слот), игра ставит меш по именам костей (`BodyMesh`).

  1. РОГА — кусок органа «Рога» (слот `Рога`, `mirrorX`): у каждого рога шов «основание рога» — плоское кольцо 8 поперёк
     ствола на `--xc` м от средней линии; череп под рогом закрыт крышкой (место без органа — сплошная кожа). Вершина 0
     кольца — на «верху», дальше к +X Blender (−X Unity), как у ушей;
  2. ВЕСА — автовеса (тепло) по деформирующим костям, правила: рога жёстко на `голова`; у конечностей вес только своей
     стороны; ниже запястья и скакательного (костей там нет) — жёстко на последней кости ноги;
  3. НАРЕЗКА — грань к месту с наибольшим весом (место кости — из графа `los-graph.json`, `socket`), три прохода сглаживания;
  4. ПАСПОРТ — `parts/los-roga.json` (шов, кольцо, объекты, кости), формат как у ушей волка.
`--masshtab` — общий масштаб меша от земли (размер вида в игре); скелет подаётся уже в том же масштабе.

  blender -b --factory-startup -P rig_losya.py -- меш.obj скелет.json граф.json выход.fbx [--masshtab 1] [--xc 0.40]
      [--pasporta папка] [--blend файл] [--shots префикс]
"""
import sys, os, json, math
import bpy, bmesh
from mathutils import Vector, Matrix, kdtree

a = sys.argv[sys.argv.index('--') + 1:]
src, skp, grp, out = a[:4]
opt = dict(masshtab='1', xc='0.40')
i = 4
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
S, XC = float(opt['masshtab']), float(opt['xc']) * float(opt['masshtab'])
REP = []

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.wm.obj_import(filepath=src, forward_axis='NEGATIVE_Z', up_axis='Y')
ob = [o for o in bpy.data.objects if o.type == 'MESH'][0]; ob.name = 'лось'
bpy.context.view_layer.objects.active = ob; ob.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)   # импорт OBJ кладёт смену осей в поворот объекта
me = ob.data
me.transform(Matrix.Scale(S, 4))
sk = json.load(open(skp, encoding='utf-8'))
BN = {b['name']: b for b in sk['bones']}
SOCKET = {n['name']: n.get('socket', '') for n in json.load(open(grp, encoding='utf-8'))['nodes']}

# ---------- 1. РОГА ----------
bm = bmesh.new(); bm.from_mesh(me)
VP = bm.verts.layers.int.new('piece'); RG = bm.verts.layers.int.new('ring')


def region(seed, ok):
    reg, st = set(), [seed]
    while st:
        f = st.pop()
        if f in reg or not ok(f):
            continue
        reg.add(f); st.extend(g for e in f.edges for g in e.link_faces if g is not f)
    return reg


def cut_loop(reg, what):
    E = [e for e in bm.edges if len(e.link_faces) == 2 and (e.link_faces[0] in reg) != (e.link_faces[1] in reg)]
    deg = {}
    for e in E:
        for v in e.verts:
            deg[v] = deg.get(v, 0) + 1
    assert deg and all(d == 2 for d in deg.values()), '%s: граница не простая петля' % what
    where = [v.co.copy() for v in deg]
    bmesh.ops.split_edges(bm, edges=E)
    res = []
    for inside in (True, False):
        es = [e for e in bm.edges if e.is_boundary and (e.link_faces[0] in reg) == inside
              and all(any((v.co - q).length < 1e-7 for q in where) for v in e.verts)]
        adj = {}
        for e in es:
            for v in e.verts:
                adj.setdefault(v, []).append(e)
        v0 = next(iter(adj)); loop, prev = [v0], None
        while True:
            e = [e for e in adj[loop[-1]] if e is not prev][0]
            nx = e.other_vert(loop[-1]); prev = e
            if nx is v0:
                break
            loop.append(nx)
        assert len(loop) == len(adj), '%s: петель больше одной' % what
        res.append(loop)
    return res


def ring_seam(Lp, Lb, c, u, w, n, code, piece, k=0.92):
    """Новое кольцо из n точек эллипса чуть уже кромки и полоса «кромка → кольцо» у куска и у тела; тело — с крышкой."""
    a2 = lambda co: math.atan2((co - c).dot(u), (co - c).dot(w)) % (2 * math.pi)
    rx = k * max(abs((v.co - c).dot(u)) for v in Lp); rw = k * max(abs((v.co - c).dot(w)) for v in Lp)
    for pc, L in ((piece, Lp), (0, Lb)):
        an_ = [a2(v.co) for v in L]
        k0 = min(range(len(L)), key=lambda k_: min(an_[k_], 2 * math.pi - an_[k_]))
        L = L[k0:] + L[:k0]; an_ = an_[k0:] + an_[:k0]
        if an_[0] > math.pi: an_[0] -= 2 * math.pi
        q = max(2, len(an_) // 4)
        if sum(1 for k_ in range(1, q) if an_[k_] > math.pi) > q // 2:
            L = [L[0]] + L[:0:-1]; an_ = [an_[0]] + an_[:0:-1]
        aL = [an_[0]]
        for x in an_[1:]:
            aL.append(max(aL[-1], x))
        r_ = []
        for k_ in range(n):
            th = 2 * math.pi * k_ / n
            v = bm.verts.new(c + u * (rx * math.sin(th)) + w * (rw * math.cos(th))); v[RG] = code + k_; v[VP] = pc; r_.append(v)
        an = [2 * math.pi * k_ / n for k_ in range(n)]
        m, i_, j_ = len(L), 0, 0
        aA = aL + [aL[0] + 2 * math.pi]; aB = an + [an[0] + 2 * math.pi]
        while i_ < m or j_ < n:
            if j_ >= n or (i_ < m and aA[i_ + 1] <= aB[j_ + 1]):
                tri = (L[i_], L[(i_ + 1) % m], r_[j_ % n]); i_ += 1
            else:
                tri = (L[i_ % m], r_[(j_ + 1) % n], r_[j_ % n]); j_ += 1
            if len(set(tri)) == 3:
                bm.faces.new(tri)
        if pc == 0:                                       # череп под рогом закрыт
            cv = bm.verts.new(c); cv[VP] = 0; cv[RG] = code + 50
            for k_ in range(n):
                bm.faces.new((cv, r_[k_], r_[(k_ + 1) % n]))
    return rx, rw


SEAM = {}
bm.faces.ensure_lookup_table()
for sg, code in ((1, 101), (-1, 201)):
    tip = max(bm.faces, key=lambda f: f.calc_center_median().x * sg)
    horn = region(tip, lambda f: f.calc_center_median().x * sg > XC)
    Lp, Lb = cut_loop(horn, 'шов рога')
    c = sum((v.co for v in Lp), Vector()) / len(Lp)
    ax = Vector((sg, 0, 0)); w = Vector((0, 0, 1)); u = ax.cross(w) if sg > 0 else w.cross(ax)
    # u — вбок кольца: к +X Blender у стороны +X (как у ушей волка: вершины дальше к +X Blender)
    u = Vector((0, -1, 0)) if sg > 0 else Vector((0, 1, 0))
    rr = ring_seam(Lp, Lb, c, u, w, 8, code, 2)
    if sg > 0:
        SEAM['основание рога'] = dict(c=c, ax=ax, up=w, u=u, r=rr)
    seen, st = set(), [tip.verts[0]]
    while st:
        x_ = st.pop()
        if x_ in seen:
            continue
        seen.add(x_); x_[VP] = 2; st.extend(e.other_vert(x_) for e in x_.link_edges)
    REP.append('рог стороны %+d: граней %d, кромка %d вершин, кольцо 8 полуоси %.3f × %.3f м, центр (%.3f %.3f %.3f)' % (
        sg, len(horn), len(Lp), rr[0], rr[1], *c))
bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
# ориентация кусков — по знаку объёма от центра куска
seenf = set()
for f0 in bm.faces:
    if f0 in seenf:
        continue
    comp, st = [], [f0]
    while st:
        f = st.pop()
        if f in seenf:
            continue
        seenf.add(f); comp.append(f); st.extend(g for e in f.edges for g in e.link_faces if g is not f)
    cen = sum((f.calc_center_median() for f in comp), Vector()) / len(comp)
    vol = sum((l_[0] - cen).dot((l_[k_] - cen).cross(l_[k_ + 1] - cen)) for f in comp
              for l_ in [[lp.vert.co for lp in f.loops]] for k_ in range(1, len(l_) - 1))
    if vol < 0:
        bmesh.ops.reverse_faces(bm, faces=comp)
bnd = sum(1 for e in bm.edges if e.is_boundary); nonman = sum(1 for e in bm.edges if not e.is_manifold and not e.is_boundary)
REP.append('открытых рёбер %d (ждём 16: кольца рогов у куска), неманифолд %d' % (bnd, nonman))
bm.to_mesh(me); bm.free()
vpv = [d.value for d in me.attributes['piece'].data]; rgv = [d.value for d in me.attributes['ring'].data]

# ---------- арматура ----------
arm = bpy.data.armatures.new('Скелет'); ao = bpy.data.objects.new('Скелет', arm)
bpy.context.scene.collection.objects.link(ao)
bpy.ops.object.select_all(action='DESELECT')
bpy.context.view_layer.objects.active = ao; ao.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
for b in sk['bones']:
    eb = arm.edit_bones.new(b['name'])
    eb.head, eb.tail = Vector(b['head']), Vector(b['tail'])
    if 'z_axis' in b:
        eb.align_roll(Vector(b['z_axis']))
    assert eb.name == b['name'], 'имя кости изменено: %s → %s' % (b['name'], eb.name)
for b in sk['bones']:
    if b['parent']:
        arm.edit_bones[b['name']].parent = arm.edit_bones[b['parent']]
bpy.ops.object.mode_set(mode='OBJECT')
LIMB = ['лопатка', 'плечо', 'предплечье', 'бедро', 'голень']
# ВСЕ КОСТИ ГРАФА ДЕФОРМИРУЮТ: у лося горб, грива, грудь, серьга, ляжка — узлы графа со своими местами (`горб`, `Сердце`,
# `шея`, `Ноги`); без весов этих костей горб и грудь уходили месту `Руки` через лопатку
DEF = [b['name'] for b in sk['bones']]
for b in arm.bones:
    b.use_deform = b.name in DEF

# ---------- 2. автовеса ----------
skin = ob.copy(); skin.data = me.copy(); bpy.context.scene.collection.objects.link(skin)
bpy.ops.object.select_all(action='DESELECT')
skin.select_set(True); ao.select_set(True); bpy.context.view_layer.objects.active = ao
bpy.ops.object.parent_set(type='ARMATURE_AUTO')
gname = {g.index: g.name for g in skin.vertex_groups}
heat = [{gname[g.group]: g.weight for g in v.groups if g.weight > 1e-4} for v in skin.data.vertices]
empty = sum(1 for h in heat if not h)
assert empty < 0.2 * len(heat), 'автовеса не сошлись: без весов %d из %d вершин' % (empty, len(heat))
kd = kdtree.KDTree(len(me.vertices))
for v in me.vertices:
    kd.insert(v.co, v.index)
kd.balance()
sm_ = lambda x: 0.0 if x <= 0 else (1.0 if x >= 1 else x * x * (3 - 2 * x))
YH = Vector(BN['голова']['head']).y; ZD = Vector(BN['серьга']['head']).z + 0.05 * S   # затылочный сустав; верх серьги
WRIST = Vector(BN['предплечье']['tail']).z; HOCK = Vector(BN['голень']['tail']).z
W = []
for v in me.vertices:
    co = v.co
    w = dict(heat[v.index])
    if not w:
        for _, j, _ in kd.find_n(co, 16):
            if heat[j]:
                w = dict(heat[j]); break
    if vpv[v.index] == 2 or rgv[v.index] > 100:
        w = {'голова': 1.0}                               # рога и их кольца — жёстко на черепе
    else:
        # ГОЛОВА ЖЁСТКАЯ впереди затылочного сустава (уши и лоб уходили гриве и горбу по теплу): вес `голова` нарастает на
        # 0.12 м до сустава; серьга (ниже подбородка) остаётся за шеей
        hw = sm_((YH + 0.06 * S - co.y) / (0.12 * S)) * sm_((co.z - ZD) / (0.10 * S))
        if hw > 0:
            w = {k_: x * (1 - hw) for k_, x in w.items()}; w['голова'] = w.get('голова', 0) + hw
        sfx = '.L' if co.x > 0 else ''
        other = '' if sfx else '.L'
        if abs(co.x) > 0.08 * S:                          # конечность — только своей стороны
            for nm in list(w):
                if nm.split('.')[0] in LIMB and nm.endswith('.L') != (sfx == '.L'):
                    w.pop(nm)
        front = co.y < 0
        if co.z < (WRIST if front else HOCK) and abs(co.x) > 0.12 * S:   # ниже последнего сустава — жёстко на последней кости
            w = {('предплечье' if front else 'голень') + sfx: 1.0}
    w = {k_: x for k_, x in w.items() if x > 1e-6} or {'хребет': 1.0}
    top = sorted(w.items(), key=lambda kv: -kv[1])[:4]; s_ = sum(x for _, x in top)
    W.append({k_: x / s_ for k_, x in top})
bpy.data.objects.remove(skin, do_unlink=True)

# ---------- 3. нарезка ----------
slot_of = lambda nm: SOCKET.get(nm[:-2] if nm.endswith('.L') else nm) or 'хребет'
F = [tuple(p.vertices) for p in me.polygons]
fslot, fixed = [], []
for f in F:
    if vpv[f[0]] == 2 and all(vpv[x] == 2 for x in f):
        fslot.append('Рога'); fixed.append(True); continue
    sc = {}
    for vi in f:
        for nm, x in W[vi].items():
            sc[slot_of(nm)] = sc.get(slot_of(nm), 0) + x
    fslot.append(max(sc, key=sc.get)); fixed.append(False)
edge_f = {}
for k_, f in enumerate(F):
    for x, y in ((f[0], f[1]), (f[1], f[2]), (f[2], f[0])):
        edge_f.setdefault((min(x, y), max(x, y)), []).append(k_)
nb = [[] for _ in F]
for fs in edge_f.values():
    if len(fs) == 2:
        nb[fs[0]].append(fs[1]); nb[fs[1]].append(fs[0])
for _ in range(3):
    for k_ in range(len(F)):
        if fixed[k_]:
            continue
        o_ = [fslot[j] for j in nb[k_] if fslot[j] != fslot[k_] and not fixed[j]]
        if len(o_) >= 2 and o_.count(o_[0]) >= 2:
            fslot[k_] = o_[0]
ORDER = sorted(set(fslot), key=lambda s_: ['хребет', 'горб', 'Сердце', 'шея', 'голова', 'Руки', 'Ноги', 'Хвост', 'Рога'].index(s_)
               if s_ in ['хребет', 'горб', 'Сердце', 'шея', 'голова', 'Руки', 'Ноги', 'Хвост', 'Рога'] else 99)
V = [v.co.copy() for v in me.vertices]
bpy.data.objects.remove(ob, do_unlink=True)
parts = {}
for sl in ORDER:
    fs = [F[k_] for k_ in range(len(F)) if fslot[k_] == sl]
    ids = sorted({vi for f in fs for vi in f}); re_ = {vi: k_ for k_, vi in enumerate(ids)}
    m = bpy.data.meshes.new(sl)
    m.from_pydata([V[vi] for vi in ids], [], [tuple(re_[vi] for vi in f) for f in fs]); m.update()
    for p in m.polygons:
        p.use_smooth = False
    o = bpy.data.objects.new(sl, m); bpy.context.scene.collection.objects.link(o)
    assert o.name == sl, 'имя объекта изменено: ' + o.name
    groups = {}
    for vi in ids:
        for nm, x in W[vi].items():
            if nm not in groups:
                groups[nm] = o.vertex_groups.new(name=nm)
            groups[nm].add([re_[vi]], x, 'REPLACE')
    o.parent = ao
    md = o.modifiers.new('Скелет', 'ARMATURE'); md.object = ao
    parts[sl] = (o, ids)
for b in arm.bones:
    b.use_deform = True
REP.append('объекты: ' + ', '.join('%s %d тр' % (sl, len(parts[sl][0].data.polygons)) for sl in ORDER) +
           '; всего %d тр' % sum(len(parts[sl][0].data.polygons) for sl in ORDER))

# ---------- 4. паспорт рогов ----------
U = lambda p: [round(-p[0], 5), round(p[2], 5), round(-p[1], 5)]
if 'pasporta' in opt and 'основание рога' in SEAM:
    d = SEAM['основание рога']
    pts = [d['c'] + d['u'] * (d['r'][0] * math.sin(2 * math.pi * k_ / 8)) + d['up'] * (d['r'][1] * math.cos(2 * math.pi * k_ / 8)) for k_ in range(8)]
    hb = Vector(BN['голова']['head'])
    pas = {'species': 'Лось', 'plan': 'четвероногий', 'slot': 'Рога', 'objects': {'main': 'Рога'}, 'mirror': True, 'fit': 'ring',
           'fbx': 'Assets/_Chimera/Models/los_mesh.fbx', 'bones': ['голова'], 'armature': 'все узлы графа лося', 'keys': [],
           'tris': len(parts['Рога'][0].data.polygons), 'tris_main': len(parts['Рога'][0].data.polygons),
           'generator': 'Tools/Blender/proba/rig_losya.py --masshtab %g --xc %s --pasporta …' % (S, opt['xc']),
           'source': 'Референсы/работа/модельная/2026-10-13-proba-los/',
           'seam': {'type': 'основание рога', 'ring': list(range(8)), 'ring_m': [U(q) for q in pts], 'center_m': U(d['c']),
                    'ellipse_m': [round(d['r'][0], 4), round(d['r'][1], 4)], 'axis_out': U(d['ax']), 'up': U(d['up']), 'side': U(d['u']),
                    'bone': 'голова', 'on_bone_m': round((d['c'] - hb).length, 4), 'overhang_m': 0.0,
                    'note': 'метры тела лося, оси Unity; кольцо плоское, поперёк ствола рога; вершина 0 — на «верху», дальше в сторону `side`; '
                            'ellipse_m — вбок × по «верху»; кольцо дано для стороны −X Unity (+X Blender), вторая — зеркалом по X; '
                            'череп под рогом закрыт крышкой'}}
    os.makedirs(opt['pasporta'], exist_ok=True)
    json.dump(pas, open(os.path.join(opt['pasporta'], 'los-roga.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=2)
    REP.append('паспорт: parts/los-roga.json')

bpy.ops.object.select_all(action='SELECT')
bpy.context.view_layer.objects.active = ao
bpy.ops.export_scene.fbx(filepath=out, use_selection=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z',
                         axis_up='Y', object_types={'ARMATURE', 'MESH'}, add_leaf_bones=False, bake_anim=False,
                         mesh_smooth_type='FACE', use_armature_deform_only=False)
if 'blend' in opt:
    bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(opt['blend']))
print('РИГ: ' + '\nРИГ: '.join(REP))

# ---------- кадры: нарезка цветом и поза ----------
if 'shots' in opt:
    sc = bpy.context.scene; sc.render.engine = 'BLENDER_WORKBENCH'
    sh = sc.display.shading; sh.light = 'STUDIO'; sh.color_type = 'OBJECT'; sh.show_cavity = True
    sh.background_type = 'VIEWPORT'; sh.background_color = (0.15, 0.16, 0.18)
    sc.render.resolution_x = sc.render.resolution_y = 800
    COL = {'хребет': (0.4, 0.7, 0.4, 1), 'горб': (0.2, 0.75, 0.75, 1), 'Сердце': (0.9, 0.5, 0.2, 1), 'шея': (0.9, 0.75, 0.3, 1),
           'голова': (0.85, 0.3, 0.3, 1), 'Руки': (0.45, 0.65, 0.95, 1), 'Ноги': (0.7, 0.5, 0.85, 1), 'Хвост': (0.6, 0.6, 0.6, 1), 'Рога': (0.95, 0.9, 0.6, 1)}
    for sl, (o, _) in parts.items():
        o.color = COL.get(sl, (0.7, 0.7, 0.7, 1))
    cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); sc.collection.objects.link(cam); sc.camera = cam
    cam.data.type = 'ORTHO'; cam.data.ortho_scale = 5.0 * S; cam.data.clip_end = 100
    c0 = Vector((0, -0.45, 2.0)) * S
    pre = os.path.abspath(opt['shots'])

    def shoot(tag):
        for nm, d in (('side', (1, 0, 0)), ('front', (0, -1, 0)), ('34', (0.7, -0.7, 0.3))):
            e = Vector(d).normalized(); cam.location = c0 + e * 20; cam.rotation_euler = (c0 - cam.location).to_track_quat('-Z', 'Y').to_euler()
            sc.render.filepath = '%s_%s_%s.png' % (pre, tag, nm); bpy.ops.render.render(write_still=True)
    shoot('narezka')
    # поза: шаг передней правой, задней левой, голова вниз, хвост вверх — проверка весов
    bpy.context.view_layer.objects.active = ao; bpy.ops.object.mode_set(mode='POSE')
    for bn, ang, axis in (('плечо', 25, 'X'), ('предплечье', -30, 'X'), ('бедро.L', -20, 'X'), ('голень.L', 30, 'X'), ('шея', 20, 'X'), ('хвост', -30, 'X')):
        pb = ao.pose.bones.get(bn)
        if pb:
            pb.rotation_mode = 'XYZ'; setattr(pb.rotation_euler, axis.lower(), math.radians(ang))
    bpy.ops.object.mode_set(mode='OBJECT')
    shoot('poza')
