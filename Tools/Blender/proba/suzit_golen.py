"""ГОЛЕНЬ БЕЗ ГОЛЕНИЩА (28.10, критик r2 п.1: «манжета по кругу на высоте колена, верх шире бедра ~10 %, ноги заправлены в
голенища»). Сглаживание зоны колена (glad_koleno.py) манжету не сняло: это не шов, а объём — ключ подгонки снял с цели голень,
у колена широкую, как голенище. Шаг перед огранкой, в ключе `--imya`: вершины с главной костью голени получают параметр t вдоль
кости (0 — колено, 1 — пятка) и расстояние до её оси в покое скелета; средний радиус голени по полосам t сравнивается с радиусом
конца бедра (вершины бедра с t > 0.85). Где голень шире, на участке t < `--do` она радиально сводится к радиусу бедра с плавным
переходом (smoothstep) к своему радиусу на t = `--do`.

  blender -b --factory-startup -P suzit_golen.py -- вход.blend выход.blend [--imya Волк] [--obekt Ноги] [--do 0.4] [--zapas 1.0] [--kusok @Волк] [--kap 1 | --osi 1 [--bok vnutr]] [--otpusk 0.2]
"""
import sys
import bpy
import numpy as np

a = sys.argv[sys.argv.index('--') + 1:]
src, out = a[0], a[1]
opt = dict(imya='Волк', obekt='Ноги', do='0.4', zapas='1.0')
i = 2
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
DO, ZAP = float(opt['do']), float(opt['zapas'])
bpy.ops.wm.open_mainfile(filepath=src)
o = bpy.data.objects[opt['obekt']]; kb = o.data.shape_keys.key_blocks[opt['imya']]
arm = [x for x in bpy.data.objects if x.type == 'ARMATURE'][0]
Mo, Mi = o.matrix_world, o.matrix_world.inverted()
gn = {g.index: g.name for g in o.vertex_groups}
glav = [gn[max(v.groups, key=lambda g: g.weight).group] if v.groups else '' for v in o.data.vertices]
X = np.array([list(Mo @ d.co) for d in kb.data])


def os_(kost):
    b = arm.data.bones[kost]; h = np.array(arm.matrix_world @ b.head_local); t = np.array(arm.matrix_world @ b.tail_local)
    return h, t


def tr(P, h, t):
    d = t - h; L = np.linalg.norm(d); u = d / L
    s = (P - h) @ u; R = P - h - np.outer(s, u)
    return s / L, R, np.linalg.norm(R, axis=1)


itog = []
for st in ('', '.L'):
    gol, bed = 'голень' + st, 'бедро' + st
    if gol not in arm.data.bones: continue
    mg = np.array([g == gol for g in glav]); mb = np.array([g == bed for g in glav])
    tg, Rg, rg = tr(X[mg], *os_(gol)); tb, _, rb = tr(X[mb], *os_(bed))
    r_bed = float(np.median(rb[tb > 0.85])) * ZAP
    # средний радиус голени по полосам t
    bins = np.linspace(0, 1, 21); rt = np.array([np.median(rg[(tg >= bins[j]) & (tg < bins[j + 1])]) if ((tg >= bins[j]) & (tg < bins[j + 1])).any() else np.nan for j in range(20)])
    bc = (bins[:-1] + bins[1:]) / 2; ok = ~np.isnan(rt); r_of = lambda t: np.interp(t, bc[ok], rt[ok])
    r_do = float(r_of(DO))
    idx = np.nonzero(mg)[0]; n_s = 0; sd = []
    # `--osi 1` (28.10, дизайн-линия FEEDBACK-2026-10-28d п.1, вариант В: «передняя и боковые плоскости через колено непрерывно,
    # без полки голенища; полнота икры — сзади; ширину анфаса держать, а не круговым пределом радиуса»): отклонение от оси голени
    # раскладывается на БОК (мировая X, перпендикулярно кости) и ПЕРЁД/ЗАД; бок ограничен боком конца бедра (p90 |бок|, t > 0.75),
    # перёд — передом конца бедра; зад (икра) не трогается. До t = do полностью, дальше предел отпускается за `--otpusk`
    if opt.get('osi') == '1':
        hg, tg_ = os_(gol); ug = (tg_ - hg) / np.linalg.norm(tg_ - hg)
        sb = np.array([1.0, 0, 0]) - ug[0] * ug; sb /= np.linalg.norm(sb); fr = np.cross(ug, sb)
        if fr[1] > 0: fr = -fr                                   # перёд — к −y (лицо смотрит в −y)
        _, Rb, _ = tr(X[mb], *os_(bed)); mb_t = tb > 0.75
        # бедро — в своей системе, но бок и перёд берём теми же мировыми направлениями
        bok_b = float(np.percentile(np.abs(Rb[mb_t] @ sb), 90)) * ZAP; per_b = float(np.percentile(Rb[mb_t] @ fr, 90)) * ZAP
        OT_ = float(opt.get('otpusk', '0.2')); n_s = 0; sd = []
        for j, k in enumerate(idx):
            t = tg[j]
            if t < -0.1 or t > DO + OT_: continue
            s_ = 0.0 if t <= DO else (t - DO) / OT_; s_ = s_ * s_ * (3 - 2 * s_)
            b_ = float(Rg[j] @ sb); f_ = float(Rg[j] @ fr); nb_, nf_ = b_, f_
            lim_b = bok_b + s_ * 1.0; lim_f = per_b + s_ * 1.0
            # `--bok vnutr` — бок сводится только к средней линии: у витрины ноги стоят шире (карта IoU анфаса: снаружи не хватает,
            # изнутри лишнее), сужение снаружи теряло совпадение с силуэтом
            vnutr = np.sign(b_) != np.sign(float((X[k] - Rg[j])[0]))      # бок смотрит к x = 0
            if abs(b_) > lim_b and (opt.get('bok') != 'vnutr' or vnutr): nb_ = np.sign(b_) * lim_b
            if f_ > lim_f: nf_ = lim_f
            if nb_ == b_ and nf_ == f_: continue
            nov = X[k] + sb * (nb_ - b_) + fr * (nf_ - f_); sd.append(float(np.linalg.norm(nov - X[k]))); X[k] = nov; n_s += 1
        itog.append('%s: бок ≤ %.1f см, перёд ≤ %.1f см (конец бедра) до t %.2f, отпуск %.2f; сведено %d вершин, сдвиг средн. %.1f, макс %.1f мм' % (
            gol, 100 * bok_b, 100 * per_b, DO, OT_, n_s, 1000 * np.mean(sd) if sd else 0, 1000 * max(sd, default=0)))
        continue
    # `--kap 1` (28.10, критик r3: «порог ТОЛЩИНЫ бедро → лапа 8–10 %», замер стенда --nogi в позе: бедро у колена r90 13.0 см,
    # голень на t 0.35 — 14.2–15.2): радиус КАЖДОЙ вершины голени ограничен k·r90 конца бедра до t = do, дальше предел плавно
    # отпускается за `--otpusk` по t; средний радиус полосы (без --kap) раздутие икры не ловил — сведено было 31 вершина
    if opt.get('kap') == '1':
        r_b90 = float(np.percentile(rb[tb > 0.75], 90)) * ZAP; OT_ = float(opt.get('otpusk', '0.2'))
        for j, k in enumerate(idx):
            t = tg[j]
            if t < -0.1 or t > DO + OT_: continue
            s_ = 0.0 if t <= DO else (t - DO) / OT_; s_ = s_ * s_ * (3 - 2 * s_)
            r_max = r_b90 + s_ * 1.0                      # за отпуском предел уходит в бесконечность (1 м)
            if rg[j] <= r_max: continue
            nov = X[k] - Rg[j] * (1 - r_max / rg[j]); sd.append(float(np.linalg.norm(nov - X[k]))); X[k] = nov; n_s += 1
        itog.append('%s: предел k·r90 бедра %.1f см до t %.2f (отпуск %.2f); сведено %d вершин, сдвиг средн. %.1f, макс %.1f мм' % (
            gol, 100 * r_b90, DO, OT_, n_s, 1000 * np.mean(sd) if sd else 0, 1000 * max(sd, default=0)))
        continue
    for j, k in enumerate(idx):
        t = tg[j]
        if t >= DO or t < -0.1: continue
        s = np.clip(t / DO, 0, 1); s = s * s * (3 - 2 * s)
        r_cel = r_bed + (r_do - r_bed) * s            # целевой средний радиус на t
        r_sr = float(r_of(max(t, 0)))
        if r_sr <= r_cel: continue
        f = r_cel / r_sr; nov = X[k] - Rg[j] * (1 - f)
        sd.append(float(np.linalg.norm(nov - X[k]))); X[k] = nov; n_s += 1
    # кусок лапы (`--kusok @Волк`) сидит на теле с прошлого шага (шов) — сводится тем же множителем по t, иначе голенище в куске
    for kn in [x for x in opt.get('kusok', '').split(',') if x]:
        ko = bpy.data.objects[kn]; gk = {g.index: g.name for g in ko.vertex_groups}; Mk = ko.matrix_world; n_k = 0
        for v in ko.data.vertices:
            if not v.groups or gk[max(v.groups, key=lambda g: g.weight).group] != gol: continue
            P = np.array(Mk @ v.co); t_, R_, _ = tr(P[None], *os_(gol)); t = float(t_[0])
            if t >= DO or t < -0.1: continue
            s_ = np.clip(t / DO, 0, 1); s_ = s_ * s_ * (3 - 2 * s_); r_cel = r_bed + (r_do - r_bed) * s_; r_sr = float(r_of(max(t, 0)))
            if r_sr <= r_cel: continue
            v.co = Mk.inverted() @ __import__('mathutils').Vector(P - R_[0] * (1 - r_cel / r_sr)); n_k += 1
        ko.data.update(); itog.append('%s: кусок %s — сведено %d вершин' % (gol, kn, n_k))
    itog.append('%s: бедро у колена r %.1f см, голень r(0.05) %.1f → r(%.2f) %.1f см; сведено %d вершин, сдвиг средн. %.1f, макс %.1f мм' % (
        gol, 100 * r_bed / ZAP, 100 * float(r_of(0.05)), DO, 100 * r_do, n_s, 1000 * np.mean(sd) if sd else 0, 1000 * max(sd, default=0)))
for k, d in enumerate(kb.data): d.co = Mi @ __import__('mathutils').Vector(X[k])
o.data.update()
for s in itog: print('ГОЛЕНЬ: ' + s)
bpy.ops.wm.save_as_mainfile(filepath=out)
