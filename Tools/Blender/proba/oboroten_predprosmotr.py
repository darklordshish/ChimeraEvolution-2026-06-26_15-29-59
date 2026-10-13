"""ПРЕДПРОСМОТР ОБОРОТНЯ ИЗ КУСКОВ — форма кусков на чужом теле без игры (13.10).

Собирает человека-меш и волчьи куски по ТЕМ ЖЕ правилам, что `PartPieces.cs` у механик (рецепт `CONSTRUCTOR_GUIDE.md`,
«Перевести вид на меш и нарезать на куски», п.6), по числам из паспортов:
  · шов донора → шов носителя: центр в центр, один масштаб по отношению эллипсов;
  · голова (череп, морда, полость, зубы, уши, мочка) держит мировой поворот донора; у двуногого носителя центр шва
    донора — `center_biped_m`;
  · воротник гривы и хвост встают кадром шва: ось к оси, «верх» к «верху».
Это НЕ приёмка (приёмка — кадр игры), а стенд для формы: увидеть воротник, посадку головы и хвост до письма механикам.
`--kluch 1` применяет к воротнику пробную форму-ключ «двуногий» (см. `kluch_dvunogij`).

  blender -b -P oboroten_predprosmotr.py -- человек.blend волк.blend папка_handoff префикс_кадров [--stupen 0|1|2] [--kluch 1] [--ushi 1]
      [--organy 1 — волчьи морда и уши графтом] [--kmax 1] [--sliyanie 1 — морда поверх лица] [--glub м] [--vverh м — вдоль «верха» шва] [--golova 1 — крупный план головы]
"""
import sys, os, json, math
import bpy
from mathutils import Vector, Matrix

a = sys.argv[sys.argv.index('--') + 1:]
chel, volk, hand, pre = a[0], a[1], a[2], os.path.abspath(a[3])
opt = dict(stupen=2.0, kluch=0.0, ushi=0.0, organy=0.0, kmax=0.0, golova=0.0, sliyanie=0.0, glub=0.0, vverh=0.0)
i = 4
while i < len(a):
    opt[a[i].lstrip('-')] = float(a[i + 1]); i += 2

bpy.ops.wm.open_mainfile(filepath=chel)
for o in list(bpy.data.objects):
    if o.type == 'ARMATURE':
        o.hide_render = True
with bpy.data.libraries.load(volk) as (src, dst):
    dst.meshes = [m for m in src.meshes]
WOLF = {m.name.split('.0')[0] if m.name[-4:-3] == '.' and m.name[-3:].isdigit() else m.name: m for m in dst.meshes}


def B(u):
    """Оси Unity паспорта → Blender."""
    return Vector((-u[0], -u[2], u[1]))


def pas(path):
    return json.load(open(os.path.join(hand, path), encoding='utf-8'))


def frame(seam, centre=None):
    c = B(centre or seam['center_m']); ax = B(seam['axis_out']).normalized(); up = B(seam['up']).normalized()
    side = up.cross(ax).normalized()
    return c, Matrix((side, up, ax)).transposed()          # столбцы: вбок, «верх», ось


def kluch_dvunogij(co, c, R):
    """Пробная форма-ключ воротника для двуногого. У волка масса гривы вентральная (манишка на 0.35 м ниже оси при
    0.20 над ней): на вертикальной шее это борода-нагрудник, а загривок голый. В кадре шва (x вбок, h «верх» —
    дорсальная сторона, t вдоль оси; t < 0 — к телу): вентральная сторона короче и ближе к оси, дорсальная длиннее —
    грива ложится на плечи и спину."""
    x, h, t = (co - c) @ R
    s = lambda v: max(0.0, min(1.0, v)) ** 2 * (3 - 2 * max(0.0, min(1.0, v)))
    dors, vent = s(h / 0.12), s(-h / 0.12)
    t2 = t * (1 + 0.9 * dors - 0.45 * vent)
    h2 = h * (1 - 0.40 * vent + 0.0 * dors)
    x2 = x * (1 + 0.20 * s(-t / 0.25))
    return c + R @ Vector((x2, h2, t2))


def put(names, c_d, R_d, c_n, R_n, k, world, key=False):
    for nm in names:
        if nm not in WOLF:
            continue
        me = WOLF[nm].copy()
        for v in me.vertices:
            p = kluch_dvunogij(v.co, c_d, R_d) if key else v.co
            d = p - c_d
            v.co = c_n + (d if world else R_n @ (R_d.transposed() @ d)) * k
        o = bpy.data.objects.new('волк:' + nm, me); bpy.context.scene.collection.objects.link(o)
        o.color = (0.55, 0.42, 0.30, 1) if nm == 'Шкура' else (0.80, 0.78, 0.74, 1)
        for p_ in me.polygons:
            p_.use_smooth = False


ch_neck = pas('cherty/chelovek-golova-kusok.json')['seam']; ch_tail = pas('chelovek-koren-hvosta.json')['seam']
v_head = pas('cherty/volk-golova-kusok.json'); v_mane = pas('cherty/volk-griva.json')['seam']; v_tail = pas('cherty/volk-hvost-kusok.json')['seam']
cn, Rn = frame(ch_neck)
k = (ch_neck['ellipse_m'][0] / v_mane['ellipse_m'][0] + ch_neck['ellipse_m'][1] / v_mane['ellipse_m'][1]) / 2
cm, Rm = frame(v_mane)
if opt['stupen'] >= 1:
    put(['Шкура'], cm, Rm, cn, Rn, k, False, key=opt['kluch'] > 0)
ct, Rt = frame(v_tail); cht, Rht = frame(ch_tail)
kt = (ch_tail['ellipse_m'][0] / v_tail['ellipse_m'][0] + ch_tail['ellipse_m'][1] / v_tail['ellipse_m'][1]) / 2
if opt['stupen'] >= 1:
    put(['Хвост'], ct, Rt, cht, Rht, kt, False)
hide = []
if opt['stupen'] >= 2:
    cb = B(v_head['center_biped_m'])
    put(['голова', 'Пасть', 'Пасть.полость', 'Пасть.зубы'] + (['Чутьё', 'Чутьё.мочка'] if opt['ushi'] > 0 else []), cb, None, cn, None, k, True)
    hide = ['голова', 'Пасть', 'Чутьё']
# ХИМЕРА ПО ОРГАНАМ (`--organy 1`): волчья морда (Пасть) на шов «основание морды» человека и волчьи уши (Чутьё) на его
# «основания ушей»; куски со швом на голове держат мировой поворот донора. `--kmax 1` — масштаб по БОЛЬШЕМУ из отношений
# полуосей (кусок закрывает проём целиком), иначе по среднему (как у шеи)
if opt['organy'] > 0:
    hide = [h for h in hide if h == 'голова'] if opt['stupen'] >= 2 else []
    def kk(a_, b_):
        r = (a_['ellipse_m'][0] / b_['ellipse_m'][0], a_['ellipse_m'][1] / b_['ellipse_m'][1])
        return max(r) if opt['kmax'] > 0 else sum(r) / 2
    # `--sliyanie 1` — СЛИЯНИЕ ПОВЕРХ ТЕЛА (решение 13.10, `PartAssembly.Union`): человеческое лицо не снимается, морда
    # садится на посадочное место `chelovek-osnovanie-mordy.json` (лицевая область); `--glub` — утопить шов в голову, м
    sl = opt['sliyanie'] > 0
    hm, vm = pas('chelovek-osnovanie-mordy.json' if sl else 'parts/chelovek-lico.json')['seam'], pas('parts/volk-morda.json')['seam']
    km = kk(hm, vm)
    put(['Пасть', 'Пасть.полость', 'Пасть.зубы', 'Чутьё.мочка'], B(vm['center_m']), None,
        B(hm['center_m']) - B(hm['axis_out']).normalized() * opt['glub'] + B(hm['up']).normalized() * opt['vverh'], None, km, True)
    he, ve = pas('parts/chelovek-ushi.json')['seam'], pas('parts/volk-ushi.json')['seam']
    ke = sum((he['ellipse_m'][0] / ve['ellipse_m'][0], he['ellipse_m'][1] / ve['ellipse_m'][1])) / 2
    for sg in (1, -1):
        cd_, cn_ = B(ve['center_m']), B(he['center_m']); cd_.x *= sg; cn_.x *= sg
        me_ = WOLF['Чутьё'].copy()
        keep = [v.index for v in me_.vertices if v.co.x * sg > 0]
        for v in me_.vertices:
            v.co = cn_ + (v.co - cd_) * ke if v.co.x * sg > 0 else Vector((0, 0, -5))
        o = bpy.data.objects.new('волк:ухо', me_); bpy.context.scene.collection.objects.link(o); o.color = (0.80, 0.78, 0.74, 1)
    hide += ['Чутьё'] if sl else ['Пасть', 'Чутьё']
    print('ОРГАНЫ: k морды %.2f, k ушей %.2f' % (km, ke))
if opt['golova'] > 0:                                    # крупный план головы вместо полного роста
    pass
for o in bpy.data.objects:
    if o.type == 'MESH' and not o.name.startswith('волк:'):
        o.color = (0.72, 0.72, 0.72, 1); o.hide_render = o.name in hide

sc = bpy.context.scene
sc.render.engine = 'BLENDER_WORKBENCH'
sh = sc.display.shading
sh.light = 'STUDIO'; sh.color_type = 'OBJECT'; sh.show_cavity = True
sh.background_type = 'VIEWPORT'; sh.background_color = (0.15, 0.16, 0.18)
sc.render.resolution_x, sc.render.resolution_y = 600, 900
cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); sc.collection.objects.link(cam); sc.camera = cam
cam.data.type = 'ORTHO'; cam.data.clip_end = 100; cam.data.ortho_scale = 2.3
s35, c35 = math.sin(math.radians(35)), math.cos(math.radians(35))
for nm, d in (('front', (0, -1, 0)), ('34f', (-s35, -c35, 0.1)), ('side', (-1, 0, 0)), ('34b', (-s35, c35, 0.15)), ('back', (0, 1, 0))):
    e = Vector(d).normalized(); c = Vector((0, -0.03, 1.70)) if opt['golova'] > 0 else Vector((0, 0, 0.98))
    cam.data.ortho_scale = 0.62 if opt['golova'] > 0 else 2.3
    cam.location = c + e * 10
    cam.rotation_euler = (c - cam.location).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = '%s_%s.png' % (pre, nm)
    bpy.ops.render.render(write_still=True)
print('ОБОРОТЕНЬ: ступень %d, k головы и гривы %.2f, k хвоста %.2f, ключ воротника %d' % (opt['stupen'], k, kt, opt['kluch']))
