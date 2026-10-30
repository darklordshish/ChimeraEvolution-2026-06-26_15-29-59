"""МЕСТО РОГОВ НА ГОЛОВЕ ЧЕЛОВЕКА (30.10, опись органов: рога лося на человеке не встают — кусок `parts/los-roga.json` есть, места нет).
Посадочное место на сплошной коже, как `chelovek-osnovanie-mordy.json` и `chelovek-koren-hvosta.json`: кольца в меше нет, `ring` —
валентность (8, как у шва куска лося «основание рога»), `ring_m` — эллипс места на коже. Центр — точка черепа, куда попадает луч из
середины черепа в направлении `--napr` (вбок к +X Blender = −X Unity, чуть вперёд, вверх: над виском, на границе лба и темени);
`axis_out` — нормаль кожи там, `up` — проекция «вверх-назад» на касательную, `side` = up × axis_out. Сторона −X Unity, вторая —
зеркалом по X (`mirror` у куска лося). Голова человека ключом «Поджарый» не меняется — место от формы базы не зависит.

  blender -b --factory-startup -P mesto_rogov.py -- chelovek_mesh.fbx skelet.json выход.json [--napr 0.55,-0.30,0.78] [--ellips 0.030,0.026]
"""
import sys, json, math
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

a = sys.argv[sys.argv.index('--') + 1:]
fbx, sk_p, out = a[:3]
opt = dict(napr='0.55,-0.30,0.78', ellips='0.030,0.026')
i = 3
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=fbx)
SK = {b['name']: b for b in json.load(open(sk_p, encoding='utf-8'))['bones']}
hb, ht = Vector(SK['голова']['head']), Vector(SK['голова']['tail'])

# кожа головы: объект «голова» меша (кости формы у человека нет — по имени объекта)
dg = bpy.context.evaluated_depsgraph_get()
obj = [o for o in bpy.data.objects if o.type == 'MESH' and o.name.split('.')[0] == 'голова']
if not obj: sys.exit('нет объекта «голова» в ' + fbx)
o = obj[0].evaluated_get(dg); me = o.to_mesh()
V = [o.matrix_world @ v.co for v in me.vertices]
bvh = BVHTree.FromPolygons(V, [list(p.vertices) for p in me.polygons])
zs = [v.z for v in V]
C = Vector((0.0, sum(v.y for v in V) / len(V), (max(zs) + hb.z) / 2))        # середина черепа: от основания головы до макушки
D = Vector([float(x) for x in opt['napr'].split(',')]).normalized()
hit = bvh.ray_cast(C + D * 0.5, -D)                                             # снаружи внутрь — ближайшая к лучу кожа снаружи
if hit[0] is None: sys.exit('луч не попал в кожу головы')
c, n = hit[0], hit[1].normalized()
if n.dot(D) < 0: n = -n
up = (Vector((0, 0.25, 1)) - n * n.dot(Vector((0, 0.25, 1)))).normalized()     # «верх» места — вверх и чуть назад по касательной
side = up.cross(n).normalized()
rx, ry = (float(x) for x in opt['ellips'].split(','))
pts = []
for k in range(8):                                                              # вершина 0 — на «верху», дальше в сторону side
    p = c + side * (rx * math.sin(math.pi / 4 * k)) + up * (ry * math.cos(math.pi / 4 * k))
    loc = bvh.find_nearest(p)[0]
    pts.append(loc if loc is not None else p)
bd = (ht - hb).normalized()
U = lambda q: [round(-q.x, 5), round(q.z, 5), round(-q.y, 5)]
pas = dict(species='Человек', plan='двуногий', fbx='Assets/_Chimera/Models/chelovek_mesh.fbx', mirror=True,
           armature='все узлы графа человека', keys=[], generator='Tools/Blender/proba/mesto_rogov.py --napr %s --ellips %s' % (opt['napr'], opt['ellips']),
           source='Docs/models/FEEDBACK-2026-10-30-modeli-opis-organov.md', kind='посадочное место на сплошной коже', bones=['голова'],
           seam={'type': 'основание рога', 'ring': list(range(8)), 'ring_m': [U(q) for q in pts], 'center_m': U(c),
                 'ellipse_m': [rx, ry], 'axis_out': U(n), 'up': U(up), 'side': U(side), 'bone': 'голова',
                 'on_bone_m': round((c - hb).dot(bd), 4),
                 'note': 'метры тела человека, оси Unity; ПОСАДКА РОГА НА СПЛОШНУЮ КОЖУ ЧЕРЕПА (30.10): кольца в меше нет — `ring` только '
                         'валентность (8, как шов куска лося «основание рога»), ring_m — эллипс места, спроецированный на кожу. Место над виском, '
                         'на границе лба и темени; axis_out — нормаль кожи; вершина 0 — на «верху», дальше в сторону `side`; ellipse_m — вбок × по '
                         '«верху» (основание рога лося в 6 раз шире — масштаб куска решает посадка); кольцо дано для стороны −X Unity (+X Blender), '
                         'вторая — зеркалом по X'})
json.dump(pas, open(out, 'w', encoding='utf-8'), ensure_ascii=False, indent=2)
print('МЕСТО РОГОВ: центр (Unity) %s, ось %s, на кости %.3f м' % (U(c), U(n), pas['seam']['on_bone_m']))
