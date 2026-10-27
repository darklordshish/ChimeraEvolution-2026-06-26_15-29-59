"""ТОРС ЦЕЛИ — ПО СИЛУЭТАМ ВИТРИНЫ (27.10, решение геймдизайнера по животу: путь (1) — V живота оставить и не гасить, полку под
грудью — убрать, ширину торса — к анфасу витрины; дизайн-линия: «полки на витрине нет — ошибка генератора»). Цель правится
мерой против картинки, а не по глазу:

  1. маски анфаса и профиля — с листа витрины (`--list`, панели по ширине `--paneli front,left,34`; фигура темнее `--porog`);
  2. силуэт цели (свои треугольники в орто-проекции) совмещается с маской: масштаб — рост, низ — земля, сдвиг по горизонтали и
     зеркало — по максимуму наложения;
  3. по каждой высоте торса (от `таз:head` до `шея:head` ориентиров) — край силуэта: в профиль ПЕРЕДНИЙ контур (полка под грудью —
     выступ контура), в анфас левый и правый край центрального куска (торс; строка, где к торсу прилипла рука и кусок шире
     `--maks_shir`, пропускается). Строка, где край цели даёт не торс (|x| > `--x_tors`), пропускается;
  4. разница (мм) сглаживается по высоте (`--glad` м) и прикладывается к вершинам торса: в профиль — сдвиг по y передних вершин
     (вес — «передность» и близость к средней линии), в анфас — сдвиг по x от середины (вес — близость к краю);
  5. печатаются отклонения контура до и после (мм) — мерило.
Внутренний рисунок (V живота) силуэт не содержит и не трогается.

  blender -b --factory-startup -P cel_po_vitrine.py -- цель.obj orientiry-celi.json выход.obj --list витрина/оборотень/тело.png
      [--paneli front,left,34] [--porog 225] [--x_tors 0.2] [--glad 0.03] [--profil 1] [--anfas 1] [--sila 1] [--vybros 0.06] [--maks 0.03] [--skladki 1.15:1.35:0.03 [--sigma_x 0.006]]
"""
import sys, json, os
import bpy, bmesh
import numpy as np
from mathutils import Vector

a = sys.argv[sys.argv.index('--') + 1:]
src, lm_p, out = a[0], a[1], a[2]
opt = dict(paneli='front,left,34', porog='225', x_tors='0.2', glad='0.03', profil='1', anfas='1', sila='1', maks_shir='0.75')
i = 3
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
LM = {k: np.array(v) for k, v in json.load(open(lm_p, encoding='utf-8')).items()}
Z0, Z1 = LM['таз:head'][2], LM['шея:head'][2]
XT = float(opt['x_tors'])

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.wm.obj_import(filepath=src, forward_axis='NEGATIVE_Z', up_axis='Y')
ob = bpy.context.selected_objects[0]
bm = bmesh.new(); bm.from_mesh(ob.data); bm.transform(ob.matrix_world); bpy.data.objects.remove(ob)
bm.verts.ensure_lookup_table()
P = np.array([list(v.co) for v in bm.verts]); TRI = np.array([[l.vert.index for l in f.loops][:3] for f in bm.faces if len(f.loops) == 3])
H = P[:, 2].max() - P[:, 2].min(); zlo = P[:, 2].min()


def maska_lista(ip):
    im = bpy.data.images.load(os.path.abspath(opt['list'])); w, h = im.size
    g = np.array(im.pixels[:]).reshape(h, w, im.channels)[::-1, :, :3].mean(2) * 255
    n = len(opt['paneli'].split(',')); x0, x1 = ip * w // n + 5, (ip + 1) * w // n - 5
    m = g[:, x0:x1] < float(opt['porog'])
    ys, xs = np.nonzero(m); return m[ys.min():ys.max() + 1, xs.min():xs.max() + 1]


def rastr(u, v, S, Wd, Hd):
    """силуэт треугольников цели: u (м, по горизонтали), v (м, высота от низа) → маска Hd×Wd при S px/м, низ — нижняя строка"""
    im = np.zeros((Hd, Wd), bool)
    U = u[TRI] * S + Wd / 2; V = (Hd - 1) - v[TRI] * S
    for t_u, t_v in zip(U, V):
        x0, x1 = int(max(t_u.min(), 0)), int(min(t_u.max() + 1, Wd - 1)); y0, y1 = int(max(t_v.min(), 0)), int(min(t_v.max() + 1, Hd - 1))
        if x1 < x0 or y1 < y0: continue
        X_, Y_ = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
        (ax, bx, cx), (ay, by, cy) = t_u, t_v
        d = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
        if abs(d) < 1e-12: continue
        l1 = ((by - cy) * (X_ - cx) + (cx - bx) * (Y_ - cy)) / d; l2 = ((cy - ay) * (X_ - cx) + (ax - cx) * (Y_ - cy)) / d
        im[y0:y1 + 1, x0:x1 + 1] |= (l1 >= 0) & (l2 >= 0) & (1 - l1 - l2 >= 0)
    return im


def sovmestit(m, u):
    """масштаб по росту; сдвиг по горизонтали и зеркало — по максимуму наложения силуэта цели на маску"""
    Hd, Wd = m.shape; S = (Hd - 1) / H
    t = rastr(u, P[:, 2] - zlo, S, Wd, Hd)
    best = None
    for zerk in (False, True):
        mm = m[:, ::-1] if zerk else m
        for dx in range(-Wd // 3, Wd // 3):
            sc = (np.roll(t, dx, axis=1) & mm).sum()
            if best is None or sc > best[0]: best = (sc, dx, zerk)
    _, dx, zerk = best
    return S, dx, zerk


def kraj_stroki(row, pered_vlevo):
    xs = np.nonzero(row)[0]
    if not len(xs): return None
    return xs.min() if pered_vlevo else xs.max()


def otkl(dd):
    v = np.array([d for d in dd if d is not None]); return (np.abs(v).mean() * 1000, np.abs(v).max() * 1000) if len(v) else (0, 0)


def glad(zs, ds):
    zs = np.array(zs); ds = np.array(ds, float); out_ = np.zeros_like(ds); s = float(opt['glad'])
    for k_, z in enumerate(zs):
        w = np.exp(-((zs - z) / s) ** 2); out_[k_] = (w * ds).sum() / w.sum()
    return zs, out_


SILA = float(opt['sila'])
P_ISH = P.copy()
NB = [[e.other_vert(v).index for e in v.link_edges] for v in bm.verts]


def gladit_sdvig(n_=10):
    """сдвиг по вершинам сглаживается по сетке — иначе соседи с разной «передностью» получают разный сдвиг (рябь у таза, проба 27.10)"""
    global P
    D = P - P_ISH
    for _ in range(n_):
        D = np.array([0.5 * D[k_] + 0.5 * D[nb_].mean(0) if nb_ else D[k_] for k_, nb_ in enumerate(NB)])
    P = P_ISH + D
# ВЫБРОСЫ (27.10, первая проба: максимум анфаса 181 → 246 мм, мятый поясок у таза): строка с разницей больше `--vybros` — не торс
# (в центральный кусок попала рука или нога), пропускается; сдвиг ограничен `--maks`
VYBROS, MAKS = float(opt.get('vybros', '0.06')), float(opt.get('maks', '0.03'))
tors = (P[:, 2] > Z0) & (P[:, 2] < Z1) & (np.abs(P[:, 0]) < XT + 0.06)
# ---------- 1. профиль: передний контур ----------
if opt['profil'] == '1':
    m = maska_lista(opt['paneli'].split(',').index('left'))
    S, dx, zerk = sovmestit(m, P[:, 1])
    mm = m[:, ::-1] if zerk else m; Hd, Wd = mm.shape
    t = np.roll(rastr(P[:, 1], P[:, 2] - zlo, S, Wd, Hd), dx, axis=1)
    # перед персонажа (−y) на картинке: после совмещения u = y·S + Wd/2 + dx — перед левее
    zs, dd = [], []
    for z in np.arange(Z0, Z1, 1.0 / S):
        r = int(round((Hd - 1) - (z - zlo) * S))
        if not (0 <= r < Hd): continue
        km, kt = kraj_stroki(mm[r], True), kraj_stroki(t[r], True)
        if km is None or kt is None: continue
        y_t = (kt - Wd / 2 - dx) / S
        band = (np.abs(P[:, 2] - z) < 1.0 / S) & (np.abs(P[:, 1] - y_t) < 0.01)
        if band.any() and np.abs(P[band, 0]).min() > XT: continue          # край цели здесь — рука
        if abs(km - kt) / S > VYBROS: continue                            # выброс: край даёт рука или нога, а не торс
        zs.append(z); dd.append(float(np.clip((km - kt) / S, -MAKS, MAKS)))
    d0 = otkl(dd)
    zs, ddg = glad(zs, dd)
    yc = np.median(P[tors, 1])
    for k_ in np.nonzero(tors)[0]:
        z = P[k_, 2]
        if P[k_, 1] > yc: continue
        dy = float(np.interp(z, zs, ddg)) if len(zs) else 0.0
        wf = min(1.0, (yc - P[k_, 1]) / 0.08) * max(0.0, 1 - abs(P[k_, 0]) / (XT + 0.06))
        wz = min(1.0, (z - Z0) / 0.05, (Z1 - z) / 0.05)
        P[k_, 1] += SILA * dy * wf * max(wz, 0)
    gladit_sdvig()
    t2 = np.roll(rastr(P[:, 1], P[:, 2] - zlo, S, Wd, Hd), dx, axis=1)
    dd2 = []
    for z in zs:
        r = int(round((Hd - 1) - (z - zlo) * S)); km, kt = kraj_stroki(mm[r], True), kraj_stroki(t2[r], True)
        dd2.append(None if km is None or kt is None else (km - kt) / S)
    d1 = otkl(dd2)
    print('ВИТРИНА: профиль, передний контур торса — строк %d, отклонение средн./макс %.1f/%.1f мм → %.1f/%.1f мм (зеркало %s)' % (len(zs), *d0, *d1, zerk))
# ---------- 2. анфас: ширина торса ----------
if opt['anfas'] == '1':
    m = maska_lista(opt['paneli'].split(',').index('front'))
    S, dx, zerk = sovmestit(m, P[:, 0])
    mm = m[:, ::-1] if zerk else m; Hd, Wd = mm.shape; c0 = Wd / 2 + dx

    def kusok(row):
        xs = np.nonzero(row)[0]
        if not len(xs): return None
        c = int(round(c0));
        if not (0 <= c < len(row)) or not row[c]: return None
        l = c
        while l > 0 and row[l - 1]: l -= 1
        r_ = c
        while r_ < len(row) - 1 and row[r_ + 1]: r_ += 1
        return l, r_
    t = np.roll(rastr(P[:, 0], P[:, 2] - zlo, S, Wd, Hd), dx, axis=1)
    zs, dl, dr = [], [], []
    for z in np.arange(Z0, Z1, 1.0 / S):
        r = int(round((Hd - 1) - (z - zlo) * S))
        if not (0 <= r < Hd): continue
        km, kt = kusok(mm[r]), kusok(t[r])
        if km is None or kt is None: continue
        if (km[1] - km[0]) / S > float(opt['maks_shir']) or (kt[1] - kt[0]) / S > float(opt['maks_shir']): continue
        if max(abs(km[0] - kt[0]), abs(km[1] - kt[1])) / S > VYBROS: continue
        zs.append(z); dl.append(float(np.clip((km[0] - kt[0]) / S, -MAKS, MAKS))); dr.append(float(np.clip((km[1] - kt[1]) / S, -MAKS, MAKS)))
    d0 = otkl(dl + dr)
    _, dlg = glad(zs, dl); zs_, drg = glad(zs, dr)
    for k_ in np.nonzero(tors)[0]:
        x, z = P[k_, 0], P[k_, 2]
        if abs(x) < 0.02 or not len(zs): continue
        # картинка: u = x·S + c0 (без зеркала); левый край картинки — −x
        d = float(np.interp(z, zs_, dlg if x < 0 else drg))
        wz = min(1.0, (z - Z0) / 0.05, (Z1 - z) / 0.05)
        P[k_, 0] += SILA * d * min(1.0, abs(x) / XT) * max(wz, 0)   # отражается маска, не цель: знак сдвига тот же
    gladit_sdvig()
    t2 = np.roll(rastr(P[:, 0], P[:, 2] - zlo, S, Wd, Hd), dx, axis=1); d2 = []
    for z in zs:
        r = int(round((Hd - 1) - (z - zlo) * S)); km, kt = kusok(mm[r]), kusok(t2[r])
        if km and kt: d2 += [(km[0] - kt[0]) / S, (km[1] - kt[1]) / S]
    d1 = otkl(d2)
    print('ВИТРИНА: анфас, ширина торса — строк %d, отклонение краёв средн./макс %.1f/%.1f мм → %.1f/%.1f мм (зеркало %s)' % (len(zs), *d0, *d1, zerk))
# ---------- 3. ПОПЕРЕЧНЫЕ СКЛАДКИ (`--skladki z0:z1:σz`): полка под грудью — вогнутая линия поперёк торса, силуэт её не выправляет.
# Передний торс — поле высот y(x, z); вершина встаёт на среднее по ВЕРТИКАЛИ в своей узкой полосе x (гаусс σz по высоте, `--sigma_x`
# 6 мм поперёк), только по y: поперечная складка уходит, продольные рёбра (V живота — решение геймдизайнера оставить) остаются.
# (Сглаживание по соседям-«вертикалям» сетки генератора мяло складку в крошку — проба 27.10)
if opt.get('skladki'):
    a0, a1, sz = (float(x) for x in opt['skladki'].split(':')); sx = float(opt.get('sigma_x', '0.006'))
    yc = np.median(P[tors, 1])
    ob_ = np.nonzero((P[:, 2] > a0 - 2 * sz) & (P[:, 2] < a1 + 2 * sz) & (P[:, 1] < yc) & (np.abs(P[:, 0]) < XT))[0]
    sel = [k_ for k_ in ob_ if a0 < P[k_, 2] < a1]
    Q = P[ob_]; Pn = P.copy(); mx = 0.0
    for k_ in sel:
        w = np.exp(-((Q[:, 2] - P[k_, 2]) / sz) ** 2 - ((Q[:, 0] - P[k_, 0]) / sx) ** 2)
        # только передняя поверхность: точки дальше 3 см по y — другая стенка (подмышка, бок)
        w *= np.abs(Q[:, 1] - P[k_, 1]) < 0.03
        if w.sum() < 1e-6: continue
        ys = (w * Q[:, 1]).sum() / w.sum(); wz = min(1.0, (P[k_, 2] - a0) / 0.03, (a1 - P[k_, 2]) / 0.03)
        Pn[k_, 1] = P[k_, 1] + wz * (ys - P[k_, 1]); mx = max(mx, abs(Pn[k_, 1] - P[k_, 1]))
    P = Pn
    print('ВИТРИНА: складки — вершин %d в полосе z %.2f–%.2f (σz %.0f мм, σx %.0f мм), сдвиг по y макс %.1f мм' % (len(sel), a0, a1, sz * 1000, sx * 1000, mx * 1000))
for v, p in zip(bm.verts, P): v.co = Vector(p)
me = bpy.data.meshes.new('cel'); bm.to_mesh(me); o = bpy.data.objects.new('cel', me); bpy.context.scene.collection.objects.link(o)
bpy.ops.wm.obj_export(filepath=out, export_selected_objects=False, forward_axis='NEGATIVE_Z', up_axis='Y', export_materials=False,
                      export_normals=False, export_uv=False)
