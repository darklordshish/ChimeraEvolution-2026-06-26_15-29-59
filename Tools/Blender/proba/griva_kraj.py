"""КРАЙ ГРИВЫ НА ГРУДИ (27.10, дизайн-линия по ряду B/D/E′, FEEDBACK-2026-10-27-codex-ogranka-BDE §2: «под грудью широкая
выступающая полоса — общий горизонтальный подворот и бортик от руки до руки; край вести двумя наклонными участками с разной
высотой окончаний, прядями, прилегающими к объёму, без выноса вперёд в профиль; открыть нижнюю клетку между В2 и В3»).

Кусок гривы `@Волк` (после kusok_celi / slit_kuski, в покое ключа) правится спереди:
  1. СРЕЗ: грани передней части (центр грани впереди `--pered_y` и не дальше `--x_maks` от средней линии), чей центр ниже линии
     z(x) из двух наклонных участков: середина `--z_seredina`, концы на |x| = `--x_konec` — справа `--z_pravyj`, слева `--z_levyj`
     (разная высота концов — не зеркальный бортик). Островки мельче `--ostrov` граней после среза удаляются;
  2. ПРИЖИМ: вершины передней части в полосе `--polosa` над срезом тянутся к телу ключа (ближайшая точка формы-ключа шасси +
     `--zazor` по нормали) — сила от 1 у среза до 0 у верха полосы: край ложится на грудь, без выноса вперёд в профиль.
Веса куска не меняются (вершины не добавляются; удалённые грани уносят свои вершины).

  blender -b --factory-startup -P griva_kraj.py -- вход.blend выход.blend [--imya Волк] [--kusok @Волк] [--z_seredina 1.33]
      [--z_pravyj 1.44] [--z_levyj 1.40] [--x_konec 0.2] [--x_maks 0.26] [--pered_y -0.05] [--z_niz 1.05] [--polosa 0.07] [--zazor 0.006] [--ostrov 30]
"""
import sys
import bpy, bmesh
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

a = sys.argv[sys.argv.index('--') + 1:]
src, out = a[0], a[1]
opt = dict(imya='Волк', kusok='@Волк', z_seredina='1.33', z_pravyj='1.44', z_levyj='1.40', x_konec='0.2', x_maks='0.26',
           pered_y='-0.05', polosa='0.07', zazor='0.006', ostrov='30')
i = 2
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
zc, zr, zl, xk = (float(opt[k]) for k in ('z_seredina', 'z_pravyj', 'z_levyj', 'x_konec'))
XM, PY, POL, ZAZ = float(opt['x_maks']), float(opt['pered_y']), float(opt['polosa']), float(opt['zazor'])

bpy.ops.wm.open_mainfile(filepath=src)


def z_sreza(x):
    """два наклонных участка: от середины к концам, правый (−x у персонажа) и левый (+x) — своя высота"""
    t = min(abs(x) / xk, 1.0)
    return zc + t * ((zr if x < 0 else zl) - zc)


# тело ключа (форма-ключ шасси) — куда прижимать
T, TF = [], []
for o in bpy.data.objects:
    if o.type != 'MESH' or '@' in o.name or not o.data.shape_keys or opt['imya'] not in o.data.shape_keys.key_blocks: continue
    kb = o.data.shape_keys.key_blocks[opt['imya']]; b0 = len(T)
    T += [o.matrix_world @ d.co for d in kb.data]
    TF += [[b0 + v for v in p.vertices] for p in o.data.polygons]
BVH = BVHTree.FromPolygons(T, TF)

o = bpy.data.objects[opt['kusok']]; Mw = o.matrix_world; inv = Mw.inverted()
bm = bmesh.new(); bm.from_mesh(o.data); bm.verts.ensure_lookup_table()


ZN = float(opt.get('z_niz', '1.05'))


def pered(p):
    """передняя часть груди: впереди, у средней линии, выше `--z_niz` — в `@Волк` слиты и кисти с лапами, их срез не касается"""
    return p.y < PY and abs(p.x) < XM and p.z > ZN


# 1. срез
ub = [f for f in bm.faces if pered(Mw @ f.calc_center_median()) and (Mw @ f.calc_center_median()).z < z_sreza((Mw @ f.calc_center_median()).x)]
n_srez = len(ub)
bmesh.ops.delete(bm, geom=ub, context='FACES')
# островки
bm.faces.ensure_lookup_table(); seen = set(); mel = []
for f in bm.faces:
    if f.index in seen: continue
    st, isl = [f], []
    seen.add(f.index)
    while st:
        g = st.pop(); isl.append(g)
        for e in g.edges:
            for h in e.link_faces:
                if h.index not in seen: seen.add(h.index); st.append(h)
    if len(isl) < int(opt['ostrov']): mel += isl
bmesh.ops.delete(bm, geom=mel, context='FACES')
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
# 2. прижим
n_pr = 0; sd = []
for v in bm.verts:
    p = Mw @ v.co
    if not pered(p): continue
    h = p.z - z_sreza(p.x)
    if h < 0 or h > POL: continue
    q, nrm, _, _ = BVH.find_nearest(p)
    if q is None: continue
    w = 1.0 - h / POL; tgt = q + nrm.normalized() * ZAZ
    if (p - q).dot(nrm) < ZAZ: continue           # уже прилегает — не трогать
    p2 = p.lerp(tgt, w); sd.append((p2 - p).length); v.co = inv @ p2; n_pr += 1
bm.to_mesh(o.data); bm.free(); o.data.update()
print('ГРИВА: срезано граней %d, островков %d граней; прижато вершин %d (сдвиг средн. %.1f, макс %.1f мм); осталось граней %d' % (
    n_srez, len(mel), n_pr, 1000 * float(np.mean(sd)) if sd else 0, 1000 * max(sd, default=0), len(o.data.polygons)))
bpy.ops.wm.save_as_mainfile(filepath=out)
