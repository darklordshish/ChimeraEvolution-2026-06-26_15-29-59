"""ВЕСА ФИНАЛЬНЫЕ — ДО КЛЮЧЕЙ ОБЛАСТЕЙ И ВЫГРУЗКИ, С ПЕРЕВЯЗКОЙ (27.10, пункт А разбора тракта, опыт дизайн-линии
`codex/morph-weights-experiment`: смена весов после снятия ключа уводила ключ в позе до 87 мм — ключ снят обратным скиннингом
ПРИ СТАРЫХ весах). Шаг делает всё, что меняет веса шасси, одним заходом и тут же перевязывает то, что показывается в позе:

  1. снимок: для каждой пары «координаты → поза» поверхность в позе P = M(старые веса, поза) · координаты;
  2. веса: `--gladit z0:z1:ymax:шагов` (сглаживание полосы торса, бывший vesa_gladit.py) → не больше 4 костей с нормализацией →
     совпадающим вершинам швов — веса первого объекта (бывшее в eksport_k2.py);
  3. перевязка: координаты = M(новые веса, поза)⁻¹ · P; обращение при числе обусловленности > 100 запрещено (ошибка, а не
     молчаливое усиление); контроль — та же поверхность в позе с новыми весами, порог 0.1 мм.

Что в какой позе показывается (`--klyuchi`, список «ключ:поза» через запятую):
  - форма-ключ вида на теле шасси (без `@` в имени) — в позе своей ступени;
  - кусок-черта `@<ключ>` — его БАЗОВЫЕ координаты в той же позе;
  - объект органа `<слот>@<вид>` — ключ «рост» в позе ступени 2 вида (`--rost вид:поза`); база органа — в покое, перевязка не нужна.
Покой (поза = покой рига) от весов не зависит: базовые координаты тела не трогаются.

После шага веса НЕ меняются: ключи областей (oblasti_k2.py) — после него, выгрузка (eksport_k2.py) веса только проверяет.

  blender -b --factory-startup -P vesa_final.py -- вход.blend выход.blend --klyuchi Волк:volk/poza.json,Лось:los/poza.json,Волк1:st1/poza.json
      [--rost Волк:volk/poza.json] [--gladit 1.04:1.40:0.02:4]
"""
import sys, json
import bpy
import numpy as np
from mathutils import Matrix, Vector

a = sys.argv[sys.argv.index('--') + 1:]
src, out = a[0], a[1]
opt = {}
i = 2
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
bpy.ops.wm.open_mainfile(filepath=src)
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
BONES = {b.name for b in arm.data.bones}
MESH = [o for o in bpy.data.objects if o.type == 'MESH']


def para(s):
    return [tuple(x.split(':', 1)) for x in s.split(',') if x] if s else []


def skin_matrices(pose_file):
    """матрицы скиннинга кости: покой рига → поза из файла (head/tail/z_axis, оси Blender), как в игре"""
    P = {b['name']: b for b in json.load(open(pose_file, encoding='utf-8'))['bones']}
    M = {}
    for bone in arm.data.bones:
        if bone.name not in P: continue
        p = P[bone.name]
        h, t, z = (Vector(p[k]) for k in ('head', 'tail', 'z_axis'))
        y = (t - h).normalized(); x = y.cross(z).normalized(); z = x.cross(y)
        rest = arm.matrix_world @ bone.matrix_local
        d = Matrix((x, y, z)).transposed() @ rest.to_3x3().inverted()
        M[bone.name] = np.array(Matrix.Translation(h) @ d.to_4x4() @ Matrix.Translation(-rest.translation))
    return M


def vesa(o):
    gn = {g.index: g.name for g in o.vertex_groups}
    return [{gn[g.group]: g.weight for g in v.groups if gn[g.group] in BONES and g.weight > 0} for v in o.data.vertices]


def v_pozu(o, W, co_list, S):
    """мировые точки в позе: смесь матриц скиннинга по весам (нормированным), координаты — локальные объекта"""
    Mw = np.array(o.matrix_world); out_ = np.zeros((len(co_list), 3)); mats = []
    for vi, co in enumerate(co_list):
        ws = {n: w for n, w in W[vi].items() if n in S}; s = sum(ws.values())
        m = sum((S[n] * (w / s) for n, w in ws.items()), np.zeros((4, 4))) if s else np.eye(4)
        mats.append(m); out_[vi] = (m @ Mw @ np.r_[co, 1])[:3]
    return out_, mats


# что перевязывать: (объект, ключ или None = база, файл позы)
KL = para(opt.get('klyuchi', '')); RO = para(opt.get('rost', ''))
cel = []
for kn, pf in KL:
    for o in MESH:
        if '@' not in o.name and o.data.shape_keys and kn in o.data.shape_keys.key_blocks: cel.append((o, kn, pf))
        if o.name == '@' + kn: cel.append((o, None, pf))
for vid, pf in RO:
    for o in MESH:
        if o.name.endswith('@' + vid) and not o.name.startswith('@') and o.data.shape_keys and 'рост' in o.data.shape_keys.key_blocks:
            cel.append((o, 'рост', pf))
SK = {pf: skin_matrices(pf) for _, _, pf in cel}


def koord(o, kn):
    src_ = o.data.shape_keys.key_blocks[kn].data if kn else o.data.vertices
    return [np.array(x.co) for x in src_]


# 1. снимок поверхности в позе при СТАРЫХ весах
W0 = {o.name: vesa(o) for o in MESH}
snap = [v_pozu(o, W0[o.name], koord(o, kn), SK[pf])[0] for o, kn, pf in cel]

# 2. веса
if opt.get('gladit'):
    z0, z1, ymax, n = opt['gladit'].split(':'); z0, z1, ymax, n = float(z0), float(z1), float(ymax), int(n)
    for o in [x for x in MESH if '@' not in x.name]:
        W = vesa(o); nb = [[] for _ in o.data.vertices]
        for e in o.data.edges: nb[e.vertices[0]].append(e.vertices[1]); nb[e.vertices[1]].append(e.vertices[0])
        band = [vi for vi, v in enumerate(o.data.vertices) if z0 < (o.matrix_world @ v.co).z < z1 and (o.matrix_world @ v.co).y < ymax]
        for _ in range(n):
            Wn = list(W)
            for vi in band:
                acc = dict(W[vi]); c = 1.0
                for j in nb[vi]:
                    for k, w in W[j].items(): acc[k] = acc.get(k, 0) + w
                    c += 1
                top = sorted(acc.items(), key=lambda kv: -kv[1])[:4]; s = sum(w for _, w in top)
                Wn[vi] = {k: w / s for k, w in top}
            W = Wn
        for vi in band:
            for g in list(o.data.vertices[vi].groups):
                if o.vertex_groups[g.group].name in BONES: o.vertex_groups[g.group].remove([vi])
            for k, w in W[vi].items():
                (o.vertex_groups.get(k) or o.vertex_groups.new(name=k)).add([vi], w, 'REPLACE')
        print('ВЕСА: %s — сглажено вершин полосы %d (%d шагов)' % (o.name, len(band), n))
for o in MESH:
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.object.vertex_group_limit_total(group_select_mode='BONE_DEFORM', limit=4)
    bpy.ops.object.vertex_group_normalize_all(group_select_mode='BONE_DEFORM', lock_active=False)
seen, n_sv = {}, 0
for o in [x for x in MESH if '@' not in x.name]:
    gn = {g.index: g.name for g in o.vertex_groups}
    for v in o.data.vertices:
        w_ = o.matrix_world @ v.co; k = (round(w_.x, 4), round(w_.y, 4), round(w_.z, 4))
        ws = {gn[g.group]: g.weight for g in v.groups if gn[g.group] in BONES}
        if k not in seen: seen[k] = ws; continue
        if seen[k] == ws: continue
        for g in list(v.groups):
            if gn[g.group] in BONES: o.vertex_groups[g.group].remove([v.index])
        for nm_, w__ in seen[k].items():
            (o.vertex_groups.get(nm_) or o.vertex_groups.new(name=nm_)).add([v.index], w__, 'REPLACE')
        n_sv += 1
print('ВЕСА: не больше 4 костей, нормализованы; швы — выровнено вершин %d' % n_sv)

# 3. перевязка: координаты = M(новые)⁻¹ · снимок
W1 = {o.name: vesa(o) for o in MESH}
cmax, errs = 0.0, []
for (o, kn, pf), P in zip(cel, snap):
    old = koord(o, kn); _, mats = v_pozu(o, W1[o.name], old, SK[pf])
    Mw = np.array(o.matrix_world); Mi = np.linalg.inv(Mw); new = []
    for vi, m in enumerate(mats):
        c = float(np.linalg.cond(m[:3, :3])); cmax = max(cmax, c)
        if not np.isfinite(c) or c > 100: raise RuntimeError('ПЕРЕВЯЗКА: обращение неустойчиво %s:%d, обусловленность %.1f' % (o.name, vi, c))
        new.append((Mi @ np.linalg.solve(m, np.r_[P[vi], 1]))[:3])
    dst = o.data.shape_keys.key_blocks[kn].data if kn else o.data.vertices
    for vi, p in enumerate(new): dst[vi].co = Vector(p)
    sdv = max((float(np.linalg.norm(Mw[:3, :3] @ (p - q))) for p, q in zip(new, old)), default=0.0)
    chk, _ = v_pozu(o, W1[o.name], new, SK[pf]); e = float(np.abs(chk - P).max()) if len(P) else 0.0; errs.append(e)
    print('ПЕРЕВЯЗКА: %s · %s (%s) — сдвиг в покое до %.1f мм, в позе после %.4f мм' % (o.name, kn or 'база', pf.split('/')[-1], sdv * 1000, e * 1000))
print('ПЕРЕВЯЗКА: обусловленность макс %.3f, ошибка в позе макс %.4f мм' % (cmax, max(errs, default=0) * 1000))
assert max(errs, default=0) < 1e-4, 'перевязка не воспроизводит поверхность в позе (порог 0.1 мм)'
bpy.ops.wm.save_as_mainfile(filepath=out)
