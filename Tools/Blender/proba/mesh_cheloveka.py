"""ПРОБА 2, 10.10: цельный меш человека из образца Hunyuan3D-2mv (вход — четыре ракурса листа `витрина/человек/вид.png`).
Тот же тракт, что у волка (`mesh_volka.py` → `dovodka_volka.py`), но ДО рига — только кандидаты на выбор геймдизайнеру:
его глаз стоит в начале (урок волка 10.10). В игру не идёт, граф и данные человека не трогает.

Шаги: импорт .glb → один объект → лицо в −Y Blender (+Z Unity), стопы на земле, середина по X → масштаб по росту
`--rost` (1.85 м) → по желанию симметрия (`--sym 1`: половина +X зеркалится) → прореживание:
`--mid` равномерно, затем только корпус, плечи, бёдра и голени до `--budget` (голова, кисти и стопы держат плотность
`--mid` — там читается лицо и пальцы) → плоское затенение → OBJ (оси как у `blender_pyat_kamer.py`: вперёд −Z, вверх Y).

  blender -b --factory-startup -P mesh_cheloveka.py -- вход.glb выход.obj [--rost 1.85] [--sym 1] [--mid 9000] [--budget 4500]
      [--sx 0.97 --sy 0.93 --hy 0.85 — габарит по маскам] [--shorty 1 — снять кромки шорт] [--nos 1 — спинка и кончик носа по листу] [--chist 1 — чистка]
      [--golova бюст.glb — голова отдельным образцом, --gw 0.177 ширина головы с ушами, --gy/--gz сдвиг и высота реза]
"""
import sys, math
import bpy, bmesh
from mathutils import Matrix

a = sys.argv[sys.argv.index('--') + 1:]
src, out = a[0], a[1]
opt = dict(rost=1.85, sym=0.0, mid=9000.0, budget=4500.0, sx=1.0, sy=1.0, hy=0.0, shorty=0.0, chist=0.0)
sopt = {}
i = 2
while i < len(a):
    try: opt[a[i].lstrip('-')] = float(a[i + 1])
    except ValueError: sopt[a[i].lstrip('-')] = a[i + 1]
    i += 2

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
ms = [o for o in bpy.data.objects if o.type == 'MESH']
for o in bpy.data.objects:
    o.select_set(o in ms)
bpy.context.view_layer.objects.active = ms[0]
if len(ms) > 1:
    bpy.ops.object.join()
ob = bpy.context.view_layer.objects.active
bpy.ops.object.parent_clear(type='CLEAR_KEEP_TRANSFORM')
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
me = ob.data


def V():
    return [v.co for v in me.vertices]


# ширина — длинная горизонталь (плечи и руки), лицо — туда, куда смотрят стопы
xs = [v.x for v in V()]; ys = [v.y for v in V()]
if (max(ys) - min(ys)) > (max(xs) - min(xs)):
    me.transform(Matrix.Rotation(math.radians(90), 4, 'Z'))
z0 = min(v.z for v in V()); z1 = max(v.z for v in V()); H = z1 - z0
feet = [v for v in V() if v.z < z0 + 0.04 * H]
torso = [v for v in V() if z0 + 0.5 * H < v.z < z0 + 0.7 * H]
cy = sum(v.y for v in torso) / len(torso)
if max(v.y for v in feet) - cy > cy - min(v.y for v in feet):      # носки в +Y — развернуть лицом в −Y
    me.transform(Matrix.Rotation(math.radians(180), 4, 'Z'))
k = opt['rost'] / H
torso = [v for v in V() if z0 + 0.5 * H < v.z < z0 + 0.7 * H]
cx = (min(v.x for v in torso) + max(v.x for v in torso)) / 2
cy = sum(v.y for v in torso) / len(torso)
me.transform(Matrix.Translation((-cx, -cy, -z0)))
me.transform(Matrix.Scale(k, 4))

# ГОЛОВА ОТДЕЛЬНЫМ ОБРАЗЦОМ (`--golova бюст.glb`, 13.10): в полноростовых ракурсах голова — 55 px, лицо генератору лепить
# не из чего (вышло узким, с тяжёлым надбровьем). Панель HEAD CLOSEUPS листа даёт голову втрое крупнее — второй образец
# (бюст) ставится на тело: масштаб по ширине головы листа с ушами (`--gw`, 0.177 м — маска анфас на 1.72 м), макушка на
# рост, шея к шее; тело режется по шее, бюст — чуть ниже, булево объединение. Делается ДО зеркала: лицо станет симметричным
def widths(obj_me, z_hi, z_lo, n=60):
    """Ширина силуэта анфас и середина по Y по слоям сверху вниз."""
    rows = []
    step = (z_hi - z_lo) / n
    for k_ in range(n):
        zz = z_hi - (k_ + 0.5) * step
        q = [v.co for v in obj_me.vertices if abs(v.co.z - zz) < step]
        rows.append((zz, (max(c.x for c in q) - min(c.x for c in q)) if q else 0.0,
                     ((max(c.y for c in q) + min(c.y for c in q)) / 2) if q else 0.0,
                     ((max(c.x for c in q) + min(c.x for c in q)) / 2) if q else 0.0))
    return rows


def neck(rows):
    """Шея — самый узкий слой ниже самого широкого места головы (уши)."""
    k_ear = max(range(len(rows) // 2), key=lambda k_: rows[k_][1])
    k_n = min(range(k_ear, len(rows)), key=lambda k_: rows[k_][1] if rows[k_][1] > 0 else 9)
    return rows[k_ear], rows[k_n]


if 'golova' in sopt:
    body_rows = widths(me, opt['rost'], opt['rost'] - 0.40)
    be, bn = neck(body_rows)
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=sopt['golova'])
    hs = [o for o in bpy.data.objects if o not in before and o.type == 'MESH']
    for o in bpy.data.objects:
        o.select_set(o in hs)
    bpy.context.view_layer.objects.active = hs[0]
    if len(hs) > 1:
        bpy.ops.object.join()
    hb = bpy.context.view_layer.objects.active
    bpy.ops.object.parent_clear(type='CLEAR_KEEP_TRANSFORM')
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    # бюст — полмиллиона граней на одну голову: проредить до плотности тела, иначе общее прореживание съест корпус
    dm = hb.modifiers.new('d', 'DECIMATE'); dm.ratio = 40000 / len(hb.data.polygons)
    bpy.ops.object.modifier_apply(modifier=dm.name)
    hm = hb.data
    zt = max(v.co.z for v in hm.vertices); zb = min(v.co.z for v in hm.vertices)
    rows = widths(hm, zt, zt - 0.75 * (zt - zb))
    he, hn = neck(rows)
    kh = opt.get('gw', 0.177) / he[1]
    hm.transform(Matrix.Translation((-he[3], -he[2], -zt)))        # по Y — череп к черепу (слой ушей), не шея к шее
    hm.transform(Matrix.Scale(kh, 4))
    hm.transform(Matrix.Translation((0, be[2] + opt.get('gy', 0.0), opt['rost'])))
    zc = bn[0] + opt.get('gz', -0.03)                     # рез по шее тела, чуть ниже самого узкого места
    print('голова: масштаб ×%.4f; шея тела на %.3f (ширина %.3f), шея бюста на %.3f (ширина %.3f)'
          % (kh, bn[0], bn[1], opt['rost'] + (hn[0] - zt) * kh, hn[1] * kh))

    def keep(obj, z0_, z1_):
        bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, 0, (z0_ + z1_) / 2))
        cb = bpy.context.active_object; cb.scale = (3, 3, z1_ - z0_)
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        bpy.ops.object.select_all(action='DESELECT'); obj.select_set(True); bpy.context.view_layer.objects.active = obj
        md = obj.modifiers.new('b', 'BOOLEAN'); md.operation = 'INTERSECT'; md.object = cb; md.solver = 'EXACT'
        bpy.ops.object.modifier_apply(modifier=md.name)
        bpy.data.objects.remove(cb, do_unlink=True)

    keep(ob, -1.0, zc)
    keep(hb, zc - 0.04, 3.0)
    bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True); bpy.context.view_layer.objects.active = ob
    md = ob.modifiers.new('u', 'BOOLEAN'); md.operation = 'UNION'; md.object = hb; md.solver = 'EXACT'
    bpy.ops.object.modifier_apply(modifier=md.name)
    bpy.data.objects.remove(hb, do_unlink=True)
    me = ob.data
    # ступенька на стыке шей — сгладить пояс ±4 см вокруг реза
    bm = bmesh.new(); bm.from_mesh(me)
    zone = [v for v in bm.verts if abs(v.co.z - (zc - 0.025)) < 0.06]
    for _ in range(25):
        new = {}
        for v in zone:
            nb = [e.other_vert(v).co for e in v.link_edges]
            w_ = 1 - abs(v.co.z - (zc - 0.025)) / 0.06
            new[v] = v.co.lerp(sum(nb, v.co * 0) / len(nb), 0.5 * w_)
        for v, c in new.items():
            v.co = c
    bm.to_mesh(me); bm.free()

if opt['sym'] > 0:
    bm = bmesh.new(); bm.from_mesh(me)
    bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], dist=1e-5, plane_co=(0, 0, 0),
                           plane_no=(1, 0, 0), clear_inner=True)
    for v in bm.verts:
        if abs(v.co.x) < 1e-4:
            v.co.x = 0.0
    bm.to_mesh(me); bm.free()
    mm = ob.modifiers.new('mir', 'MIRROR'); mm.use_axis[0] = True; mm.use_clip = True; mm.merge_threshold = 0.001
    bpy.ops.object.modifier_apply(modifier=mm.name)

R = opt['rost']


def sm(t):
    t = min(1.0, max(0.0, t)); return t * t * (3 - 2 * t)


# ДОВОДКА 1 (13.10, выбран К2). ГАБАРИТ ПО МАСКАМ ЛИСТА: образец полнее листа — анфас шире на 2–3 см, в профиль глубже
# на 1.5–4 см (голова 0.21–0.23 против 0.18–0.19). `--sx` — ширина, `--sy` — глубина тела, `--hy` — глубина головы
# (выше 1.56–1.62 м); подобраны по IoU с `витрина/человек/masks`: анфас 0.859 → 0.871, профиль 0.889 → 0.927
if opt['sx'] != 1.0 or opt['sy'] != 1.0:
    hy = opt['hy'] or opt['sy']
    ys = sorted(v.co.y for v in me.vertices if 1.0 < v.co.z < 1.3); cy = ys[len(ys) // 2]
    for v in me.vertices:
        gf = (1 - min(1.0, max(0.0, (v.co.z - 1.52) / 0.08))) if 'golova' in sopt else 1.0     # своя голова уже в размере листа
        v.co.x *= 1 + (opt['sx'] - 1) * gf
        fy = opt['sy'] + (hy - opt['sy']) * min(1.0, max(0.0, (v.co.z - 1.56) / 0.06))
        v.co.y = cy + (v.co.y - cy) * (1 + (fy - 1) * gf)

# НОС (`--nos 1`, замечание геймдизайнера 13.10: «где впадина на переносице, обычно наоборот выпуклость»; замер по
# средней линии против панели головы листа: спинка носа на 1.71–1.73 м вдавлена на 0.5–0.8 см, кончик на 1.68 м короче
# на ~1 см — «седло»). Спинка выдвигается вперёд до 0.8 см (пик на 1.715 м), кончик — до 1.0 см (пик на 1.685 м);
# по ширине правка гаснет к 2.2 см от оси, глазницы и губы не трогаются
if opt.get('nos', 0) > 0:
    for v in me.vertices:
        if abs(v.co.x) < 0.022 and 1.655 < v.co.z < 1.75 and v.co.y < -0.06:
            wx = sm((0.022 - abs(v.co.x)) / 0.014)
            d = 0.008 * sm(1 - abs(v.co.z - 1.715) / 0.03) + 0.010 * sm(1 - abs(v.co.z - 1.685) / 0.025)
            v.co.y -= opt['nos'] * d * wx

# ШОРТЫ ЛИСТА генератор вылепил рельефом: пояс (1.04–1.12 м) и низ штанин (0.71–0.79 м) — кромки на теле. `--shorty 1`:
# сглаживание только в этих двух поясах корпуса и бёдер (руки не трогаются), до прореживания — кромка растворяется,
# объём под шортами остаётся телом
if opt['shorty'] > 0:
    bm = bmesh.new(); bm.from_mesh(me)
    zone = [v for v in bm.verts if abs(v.co.x) < 0.235 * opt['sx'] and (1.01 < v.co.z < 1.15 or 0.68 < v.co.z < 0.82)]
    w = {v: (sm((v.co.z - 1.01) / 0.03) * sm((1.15 - v.co.z) / 0.03) if v.co.z > 0.9 else sm((v.co.z - 0.68) / 0.03) * sm((0.82 - v.co.z) / 0.03))
         for v in zone}
    for _ in range(int(40 * opt['shorty'])):
        new = {}
        for v in zone:
            nb = [e.other_vert(v).co for e in v.link_edges]
            new[v] = v.co.lerp(sum(nb, v.co * 0) / len(nb), 0.5 * w[v])
        for v, c in new.items():
            v.co = c
    for v in bm.verts:                                   # средняя линия после сглаживания — обратно на плоскость зеркала
        if abs(v.co.x) < 5e-4:
            v.co.x = 0.0
    bm.to_mesh(me); bm.free()
    print('шорты: сглажено вершин %d' % len(zone))

n0 = len(me.polygons)
m = ob.modifiers.new('dec', 'DECIMATE'); m.ratio = opt['mid'] / max(1, n0)
if opt['sym'] > 0:
    m.use_symmetry = True; m.symmetry_axis = 'X'
bpy.ops.object.modifier_apply(modifier=m.name)
n1 = len(me.polygons)
if opt['budget'] < n1 or opt.get('telo', 0) > 0:
    # плотность держат: голова с шеей (выше 0.80 роста), кисти (ниже 0.50 роста и дальше 0.14 роста от оси), стопы
    body = [v.index for v in me.vertices
            if v.co.z < 0.80 * R and v.co.z > 0.06 * R and not (v.co.z < 0.50 * R and abs(v.co.x) > 0.14 * R)]
    vg = ob.vertex_groups.new(name='тело'); vg.add(body, 1.0, 'REPLACE')
    # бюджет считается от ТЕЛА (`--telo`): плотные голова, кисти и стопы свою долю держат, и при общем бюджете весь
    # недобор падал на корпус — он складывался в «плащ» (13.10, после посадки отдельной головы)
    bs = set(body); nb = sum(1 for p_ in me.polygons if all(vi in bs for vi in p_.vertices))
    if opt.get('telo', 0) > 0:
        opt['budget'] = (n1 - nb) + opt['telo']
    print('  плотные части %d тр, тело %d → %d тр' % (n1 - nb, nb, opt['budget'] - (n1 - nb)))
    m = ob.modifiers.new('dec2', 'DECIMATE'); m.ratio = opt['budget'] / n1; m.vertex_group = 'тело'; m.vertex_group_factor = 1.0
    if opt['sym'] > 0:
        m.use_symmetry = True; m.symmetry_axis = 'X'
    bpy.ops.object.modifier_apply(modifier=m.name)
# ЧИСТКА (`--chist 1`, та же, что у волка): зеркало после прореживания заново, слить совпавшие вершины, убрать вырожденные
# грани, закрыть дыры, узелки на средней линии — в цикле с триангуляцией
if opt['chist'] > 0:
    bm = bmesh.new(); bm.from_mesh(me)
    bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], dist=1e-5, plane_co=(0, 0, 0), plane_no=(1, 0, 0), clear_inner=True)
    for v in bm.verts:
        if abs(v.co.x) < 2e-3:
            v.co.x = 0.0
    bm.to_mesh(me); bm.free()
    mm = ob.modifiers.new('mir2', 'MIRROR'); mm.use_axis[0] = True; mm.use_clip = True; mm.merge_threshold = 1e-4
    bpy.ops.object.modifier_apply(modifier=mm.name)
    bm = bmesh.new(); bm.from_mesh(me)
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-4)
    bmesh.ops.dissolve_degenerate(bm, edges=bm.edges[:], dist=1e-5)
    # осколки булевой операции (второй остров внутри шеи после посадки головы): остаётся самый большой остров
    seen, comps = set(), []
    for v in bm.verts:
        if v in seen:
            continue
        comp, st = [], [v]
        while st:
            u = st.pop()
            if u in seen:
                continue
            seen.add(u); comp.append(u); st.extend(e.other_vert(u) for e in u.link_edges)
        comps.append(comp)
    comps.sort(key=len)
    for comp in comps[:-1]:
        bmesh.ops.delete(bm, geom=comp, context='VERTS')
    bmesh.ops.delete(bm, geom=[e for e in bm.edges if not e.link_faces], context='EDGES')
    bad = [e for e in bm.edges if len(e.link_faces) > 2]
    if bad:
        bmesh.ops.delete(bm, geom=list({f for e in bad for f in e.link_faces}), context='FACES')
    bmesh.ops.holes_fill(bm, edges=[e for e in bm.edges if e.is_boundary], sides=0)
    edge_v = [v for v in bm.verts if v.is_boundary and abs(v.co.x) < 0.005]
    for v in edge_v:
        v.co.x = 0.0
    bmesh.ops.remove_doubles(bm, verts=edge_v, dist=0.002)
    for k_ in range(6):
        if k_ == 1:      # узелок на средней линии (три вершины в 3 мм после заплатки — на спине читался шипом): слить
            # ...и ромбик мелких граней в борозде позвоночника (пары в 1–2 см от оси на пояснице): на ось и слить
            for v in bm.verts:
                if 0 < abs(v.co.x) < 0.018 and 0.9 < v.co.z < 1.5 and v.co.y > 0:
                    v.co.x = 0.0
            bmesh.ops.remove_doubles(bm, verts=[v for v in bm.verts if abs(v.co.x) < 1e-6 and 0.9 < v.co.z < 1.5 and v.co.y > 0], dist=0.025)
            bmesh.ops.remove_doubles(bm, verts=[v for v in bm.verts if abs(v.co.x) < 1e-6], dist=0.004)
            bmesh.ops.dissolve_degenerate(bm, edges=bm.edges[:], dist=1e-5)
        bmesh.ops.triangulate(bm, faces=bm.faces[:])
        bad = [e for e in bm.edges if not e.is_manifold]
        if not bad:
            if k_ >= 1:
                break
            continue
        vs = {v for e in bad for v in e.verts}
        bmesh.ops.delete(bm, geom=list({f for v in vs for f in v.link_faces}), context='FACES')
        bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
        bmesh.ops.holes_fill(bm, edges=[e for e in bm.edges if e.is_boundary], sides=0)
    bmesh.ops.dissolve_degenerate(bm, edges=bm.edges[:], dist=1e-5)
    assert not [e for e in bm.edges if not e.is_manifold], 'после чистки остались неманифолдные рёбра'
    bm.to_mesh(me); bm.free()
bm = bmesh.new(); bm.from_mesh(me); bmesh.ops.triangulate(bm, faces=bm.faces[:]); bm.to_mesh(me); bm.free()
for p in me.polygons:
    p.use_smooth = False
vs = V()
print('ЧЕЛОВЕК: %d → %d тр; рост %.3f, размах X %.3f, глубина Y %.3f; масштаб ×%.4f; симметрия %d' % (
    n0, len(me.polygons), max(v.z for v in vs), max(v.x for v in vs) - min(v.x for v in vs),
    max(v.y for v in vs) - min(v.y for v in vs), k, opt['sym']))
bpy.ops.wm.obj_export(filepath=out, export_selected_objects=False, forward_axis='NEGATIVE_Z', up_axis='Y',
                      export_materials=False, export_normals=False, export_uv=False)
