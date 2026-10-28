"""КЛЮЧ БЛИЖЕ К БАЗЕ ПО ОБЛАСТЯМ (28.10, тело ст.1 «Волк1»: дизайн-линия §5 п.4 — «удержать человеческую опору ног, глубокая звериная
стойка остаётся отличием ст.2»; раскладка разворотов ст.1: ноги дала подгонка — 93 из 171). Форма-ключ тянется к базе шаблона по
главной кости вершины: ключ = база + k·(ключ − база), `--kosti кость:k,…` (без суффикса .L — обе стороны). Совпадающие вершины швов
разных объектов получают одно k (по первому объекту), шов не расходится.

  blender -b --factory-startup -P kluch_blizhe.py -- вход.blend выход.blend --imya Волк1 --kosti голень:0.3,стопа:0.3,бедро:0.6
"""
import sys
import bpy

a = sys.argv[sys.argv.index('--') + 1:]
src, out = a[0], a[1]
opt = {}
i = 2
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
KK = {x.split(':')[0]: float(x.split(':')[1]) for x in opt['kosti'].split(',')}
bpy.ops.wm.open_mainfile(filepath=src)
pos_k, n = {}, 0
for o in [x for x in bpy.data.objects if x.type == 'MESH' and '@' not in x.name and x.data.shape_keys and opt['imya'] in x.data.shape_keys.key_blocks]:
    kb = o.data.shape_keys.key_blocks[opt['imya']]; gn = {g.index: g.name for g in o.vertex_groups}
    for vi, v in enumerate(o.data.vertices):
        b = o.matrix_world @ v.co; key = (round(b.x, 4), round(b.y, 4), round(b.z, 4))
        if key not in pos_k:
            gl = gn[max(v.groups, key=lambda g: g.weight).group].replace('.L', '') if v.groups else ''
            pos_k[key] = KK.get(gl)
        k = pos_k[key]
        if k is None: continue
        kb.data[vi].co = v.co + (kb.data[vi].co - v.co) * k; n += 1
print('БЛИЖЕ: ключ «%s» к базе — вершин %d (%s)' % (opt['imya'], n, opt['kosti']))
bpy.ops.wm.save_as_mainfile(filepath=out)
