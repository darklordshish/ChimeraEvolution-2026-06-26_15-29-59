"""МАССЫ ПО ЛИСТУ (04.11, ряд вариаций): плотная база деформируется по таблице `massy_karta.py` ДО раскладки вершин. Вершина идёт по своему классу — рука с плечевым поясом, нога, торс — долей весов рига: анфас сводит границы интервалов на высоте, глубина масштабируется отношением интервалов профиля. Трусы листа закрывают таз: там интерполяция. Кисть, стопа и голова едут жёстко, не масштабируются. Совпадающие вершины разных объектов получают один сдвиг: швы не расходятся. Кости не двигаются.
Режим Б — только рука и плечевой пояс; А — всё тело. В `--paneli` подставляется x точек панелей теми же сдвигами.

  blender -b база.blend --factory-startup --python-exit-code 1 -P massy_po_listu.py -- выход.blend карта.json A|B панели_вход.json панели_выход.json
"""
import sys, json
import bpy
import numpy as np

a = sys.argv[sys.argv.index('--') + 1:]; OUT, KF, REZH, Pin, Pout = a[:5]
K = json.load(open(KF, encoding='utf-8')); Z = np.array([r['z'] for r in K]); nz = len(Z); NAN = np.nan


def col(fn): return np.array([fn(r) for r in K], float)
def gap(v):
    i = np.nonzero(~np.isnan(v))[0]; return np.interp(np.arange(len(v)), i, v[i]) if len(i) else np.zeros_like(v)
def gl(v, s=3):
    k = np.exp(-0.5 * (np.arange(-3 * s, 3 * s + 1) / s) ** 2); k /= k.sum(); p = np.pad(v, 3 * s, mode='edge'); return np.convolve(p, k, 'valid')


ZTROU = (0.72, 1.09)
def dv(sel, part):
    def f(r):
        if ZTROU[0] < r['z'] < ZTROU[1] and sel != 'arm': return NAN
        A_, B_ = sel_int(r['list'], sel, r['z']), sel_int(r['telo'], sel, r['z'])
        return NAN if A_ is None or B_ is None else A_[part] - B_[part]
    return f
def sel_int(I, sel, z):
    if sel == 'leg': return I[0] if z < 0.87 and len(I) >= 1 else None
    if sel == 'tor': return I[0] if 0.87 <= z < 1.27 and len(I) >= 2 else None
    if sel == 'arm': return I[1] if 0.92 <= z < 1.27 and len(I) >= 2 else None
    if sel == 'plecho': return I[0] if 1.27 <= z < 1.56 and len(I) == 1 else None
    return None
def dlin(sel):
    def f(r):
        A_, B_ = sel_int(r['list'], sel, r['z']), sel_int(r['telo'], sel, r['z'])
        return NAN if A_ is None or B_ is None or B_[1] - B_[0] < 1e-3 else (A_[1] - A_[0]) / (B_[1] - B_[0])
    return f


D_tor = gl(gap(col(dv('tor', 1)))); D_ain = gl(gap(col(dv('arm', 0)))); D_aout = gl(gap(col(dv('arm', 1)))); D_s = gl(gap(col(dv('plecho', 1))), 1); L_in = gl(gap(col(dv('leg', 0)))); L_out = gl(gap(col(dv('leg', 1))))
AIN_m = gl(gap(col(lambda r: r['telo'][1][0] if 0.92 <= r['z'] < 1.27 and len(r['telo']) >= 2 else NAN))); AOUT_m = gl(gap(col(lambda r: r['telo'][1][1] if 0.92 <= r['z'] < 1.27 and len(r['telo']) >= 2 else NAN)))
OUTm = gl(gap(col(lambda r: r['telo'][0][1] if 1.27 <= r['z'] < 1.56 else NAN)), 2)
R_arm = np.clip(gl(gap(col(dlin('arm')))), 0.9, 1.3); R_leg = np.clip(gl(gap(col(dlin('leg')))), 0.9, 1.3)
def dep(r):
    if r['prof_telo'] is None or r['prof_list'] is None or ZTROU[0] < r['z'] < ZTROU[1]: return NAN
    return (r['prof_list'][1] - r['prof_list'][0]) / max(r['prof_telo'][1] - r['prof_telo'][0], 1e-3)
R_y = np.clip(gl(gap(col(dep))), 0.95, 1.2)
CY = gl(gap(col(lambda r: (r['prof_telo'][0] + r['prof_telo'][1]) / 2 if r['prof_telo'] else NAN)))
print('МАССЫ: выступ плеча по z: ' + ', '.join('%.2f:%+.1f см' % (z, d * 100) for z, d in zip(Z[::8], D_s[::8]) if 1.28 <= z <= 1.54))
ZI = lambda arr, z: np.interp(z, Z, arr)
ARM_B = {'плечо': 1, 'предплечье': 1, 'дельта': 1, 'дельта.перед': 1, 'дельта.зад': 1, 'бицепс': 1, 'трицепс': 1, 'разгибатели': 1, 'сгибатели': 1, 'лопатка': 0.6, 'трапеция': 0.3, 'опора_подмышки': 0.4}
LEG_B = {'бедро', 'голень', 'стопа', 'квадрицепс', 'задняя бедра', 'икра'}


def smesh(x, y, z, wa, wl):
    """сдвиг точки половины x ≥ 0"""
    zk = float(np.clip(z, 0.12, 1.60)); sx = 0.0; sy = 0.0
    # рука и плечевой пояс
    if wa > 0:
        za = max(zk, 0.92)
        if za >= 1.27:
            o_ = max(ZI(OUTm, za), 1e-3); ax = ZI(D_s, za) * min(x / o_, 1.0); ry = 1 + float(np.clip(ZI(D_s, za) / 0.08, -0.05, 0.25))
        else:
            a0, a1 = ZI(AIN_m, za), ZI(AOUT_m, za); t_ = float(np.clip((x - a0) / max(a1 - a0, 1e-3), 0, 1)) if z >= 0.92 else 0.5; ax = (1 - t_) * ZI(D_ain, za) + t_ * ZI(D_aout, za); ry = ZI(R_arm, za)   # интервал руки сводится целиком: ширина по листу
        if z < 0.92: ry = 1.0
        ay = (y - 0.03) * (ry - 1)
        sx += wa * ax; sy += wa * ay
    if REZH == 'A':
        if wl > 0:
            lx = 0.5 * (ZI(L_in, zk) + ZI(L_out, zk)) if z > 0.12 else 0.5 * (ZI(L_in, 0.12) + ZI(L_out, 0.12)); sx += wl * lx; sy += wl * (y - 0.03) * (ZI(R_leg, zk) - 1 if z > 0.12 else 0)
        wt = max(0.0, 1 - wa - wl)
        if wt > 0 and z < 1.60:
            tx = ZI(D_tor, zk) * min(x / max(0.12, x), 1.0) if zk < 1.27 else ZI(D_s, zk) * min(x / max(ZI(OUTm, zk), 1e-3), 1.0)
            sx += wt * tx; sy += wt * (y - ZI(CY, zk)) * (ZI(R_y, zk) - 1)
    return sx, sy


O = [o for o in bpy.data.objects if o.type == 'MESH' and '@' not in o.name]; S = {}; zap = []
for o in O:
    im = {g.index: g.name for g in o.vertex_groups}; M = o.matrix_world
    for v in o.data.vertices:
        p = M @ v.co; wa = wl = wv = 0.0
        for g in v.groups:
            n = im[g.group]; b = n[:-2] if n.endswith('.L') else n; wv += g.weight; wa += g.weight * ARM_B.get(b, 0); wl += g.weight * (b in LEG_B)
        wv = max(wv, 1e-6); wa = min(wa / wv, 1); wl = min(wl / wv, 1)
        if o.name in ('голова', 'Чутьё'): wa = wl = 0; h = True
        else: h = False
        sg = 1.0 if p.x >= 0 else -1.0; sx, sy = (0.0, 0.0) if h else smesh(abs(p.x), p.y, p.z, wa, wl)
        if not h and 1.48 < p.z < 1.57 and abs(p.x) > 0.11: wa_ = max(wa, float(np.clip((p.z - 1.48) / 0.03, 0, 1))); sx, sy = smesh(abs(p.x), p.y, p.z, wa_, wl)   # верхний угол дельты и трапеция идут по листу: на этих высотах вес руки мал
        if not h and 1.57 < p.z < 1.65 and abs(p.x) < 0.11:   # шея по вид.png (срез: диаметр шире на 2.8–3.8 см на z 1.60–1.62)
            q_ = np.interp(p.z, [1.57, 1.58, 1.60, 1.62, 1.65], [1.0, 0.93, 0.70, 0.81, 1.0]); sx += abs(p.x) * (q_ - 1); sy += (p.y - 0.01) * (0.5 * (q_ - 1))
        kl = (round(p.x, 4), round(p.y, 4), round(p.z, 4)); r_ = S.setdefault(kl, [0.0, 0.0, 0]); r_[0] += sg * sx; r_[1] += sy; r_[2] += 1; zap.append((o, v.index, kl))
# лицо (05.11): переносица вперёд рвёт полосу глазниц, плоскость рта выше; плотные вершины головы, кистью, до нарезки
for o in O:
    if o.name not in ('голова', 'Чутьё'): continue
    for i_, v in enumerate(o.data.vertices):
        p = o.matrix_world @ v.co
        if p.y > -0.07: continue
        w1 = np.exp(-((p.x / 0.018) ** 2 + ((p.z - 1.725) / 0.016) ** 2)); w2 = np.exp(-((p.x / 0.035) ** 2 + ((p.z - 1.64) / 0.016) ** 2))
        v.co.y -= 0.006 * w1; v.co.z += 0.005 * w2
        if o.data.shape_keys: o.data.shape_keys.key_blocks[0].data[i_].co = v.co
maks = 0
for o, i, kl in zap:
    r_ = S[kl]; dx, dy = r_[0] / r_[2], r_[1] / r_[2]; maks = max(maks, abs(dx), abs(dy)); v = o.data.vertices[i]; v.co.x += dx; v.co.y += dy
    if o.data.shape_keys: o.data.shape_keys.key_blocks[0].data[i].co = v.co
for o in O: o.data.update()
print('МАССЫ %s: вершин %d, наибольший сдвиг %.1f мм' % (REZH, len(zap), maks * 1000))
P = json.load(open(Pin, encoding='utf-8'))
for p in P:
    for t in p['tochki']:
        x, z = t[0], t[1]; wa = 1.0 if x > 0.17 and z > 1.27 else 0.0; wl = 1.0 if z < 0.72 else 0.0
        t[0] = round(x + smesh(x, 0.0, z, wa, wl if REZH == 'A' else 0)[0], 4) if x > 0 else 0
json.dump(P, open(Pout, 'w', encoding='utf-8'), ensure_ascii=False)
bpy.ops.wm.save_as_mainfile(filepath=OUT)
