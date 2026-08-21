# -*- coding: utf-8 -*-
"""РЕНДЕР ЗВЕРЯ В КАРТИНКУ — чтобы видеть результат ДО плейтеста.

Зачем. Численные проверки меряют лишь то, что догадался померить: «20 из 20 ok» спокойно уживается
с демоном на экране. Глаз, утопленный на 74 мм внутри головы, не ловила ни одна из двадцати — пока
не померил именно его. Картинка ловит целое.

Как. Поле считается ОДИН раз на сетке (как в BoneMesher: заполняем по костям, каждая трогает лишь
свою окрестность), дальше проекция вдоль оси взгляда. Марш лучами по всему объёму был бы в тысячу
раз дороже — на нём первая версия и не уложилась в таймаут.
"""
import sys, os, math
import numpy as np
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt

import argparse
AP = argparse.ArgumentParser(description='ДЕТЕКТОР морфологии: то же поле, что BoneMesher, силуэт и контур.')
AP.add_argument('--species', default='wolf', help='вид (папка в species/)')
AP.add_argument('--part', default='', help='слот: мерить и рисовать ЧАСТЬ в её собственном масштабе')
AP.add_argument('--layer', type=int, default=4, help='как buildLayers: 1 кости, 2 +мышцы, 3 +признаки, 4 всё')
AP.add_argument('--cell', type=float, default=0.009, help='шаг сетки, м')
ARGS = AP.parse_args()

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))       # Anatomy/
SPECIES_DIR = os.path.join(ROOT, 'species', ARGS.species)
OUT_DIR = os.path.join(SPECIES_DIR, 'out')
os.makedirs(OUT_DIR, exist_ok=True)
_src = os.path.join(SPECIES_DIR, ARGS.species + '.py')
_txt = open(_src, encoding='utf-8').read()
_txt = _txt.split('# ── ПРОВЕРКИ')[0]
# ОТЧЁТ ГЕНЕРАТОРА ГЛУШИТСЯ: нам нужны только кости, а печать проверок здесь лишний шум
exec(_txt.replace(chr(10) + 'report', chr(10) + '#report'))
by, pos, rot = build(bones)

WELD, FUR, BLEND = 0.3, 0.0, 0.09   # Weld · SkinFur · SkinBlend волка — как в данных вида

def from_to_up(d):
    """Поворот Unity `Quaternion.FromToRotation(Vector3.up, d)` матрицей — так игра ставит МЫШЦУ."""
    d = d / np.linalg.norm(d)
    up = np.array([0.0, 1.0, 0.0])
    c = float(np.dot(up, d))
    if c > 1 - 1e-9: return np.eye(3)
    if c < -1 + 1e-9: return np.diag([1.0, -1.0, -1.0])
    n = np.cross(up, d); sn = np.linalg.norm(n); n = n / sn
    K = np.array([[0, -n[2], n[1]], [n[2], 0, -n[0]], [-n[1], n[0], 0]])
    return np.eye(3) + sn * K + (1 - c) * (K @ K)

# ОСИ СЕЧЕНИЯ БЕРУТСЯ У ИГРЫ, А НЕ ВЫВОДЯТСЯ ЗАНОВО. Здесь стояла своя конструкция через cross, и она
# расходилась с `BoneMesher` для ВСЕХ костей: игра сплющивает сечение в осях, накопленных по euler-цепочке
# (`Quaternion.Euler`), а мышцу разворачивает `FromToRotation(up, dir)`. Расхождение не видно на круглых
# деталях и вылезает ровно там, где часть ПЛОСКАЯ — ухо (depth 0.40), спинка носа (section 1.40), рез рта:
# на картинке они были бы повёрнуты не так, как у игрока. Рендер, считающий не игровое поле, вреднее,
# чем его отсутствие: по нему принимаются решения
segs = []
for b in bones:
    P0, R = pos[b.name], rot[b.name]
    if b.endBone:
        ib = [x.name for x in bones].index(b.endBone)
        b1 = pos[b.endBone] + rot[b.endBone] @ (UP * (bones[ib].length * b.endAttach))
    else:
        b1 = P0 + R @ (UP * b.length)
    for side in ((1, -1) if b.mirror else (1,)):
        a, c = P0.copy(), b1.copy()
        Rw = R.copy()
        if side < 0:
            # ЗЕРКАЛО — ОТРАЖЕНИЕМ ПОЗЫ (как в билдере: pos.x = -pos.x, euler.y/.z меняют знак)
            a[0], c[0] = -a[0], -c[0]
            S = np.diag([-1.0, 1.0, 1.0])
            Rw = S @ Rw @ S
        if b.endBone:
            d = c - a
            if np.linalg.norm(d) < 1e-6: continue
            Rw = from_to_up(d)
        L = float(np.linalg.norm(c - a))
        if L < 1e-6: continue
        segs.append(dict(a=a, b1=c, L=L, R=Rw, r0=b.r0, r1=b.r1, sec=max(0.05, b.section),
                         dep=max(0.05, b.depth), slot=b.socket or b.name, layer=b.layer,
                         blend=b.blend if b.blend > 0 else BLEND))

def part_bounds(slot):
    # РАМКА ЧАСТИ — по костям её слота. Поле при этом считается по ВСЕМУ телу: соседи влияют через
    # слабое объединение между слотами, и часть, померенная в одиночку, — уже другая часть
    pts = []
    for s in segs:
        if s['slot'] != slot: continue
        r = max(s['r0'], s['r1']) * max(1.0, s['sec'], s['dep']) + s['blend']
        pts += [s['a'] - r, s['a'] + r, s['b1'] - r, s['b1'] + r]
    if not pts: return None
    return np.min(pts, axis=0) - 0.03, np.max(pts, axis=0) + 0.03

def grid_field(cell=0.009, part='', layer=4):
    lo = np.array([-0.34, -0.03, -1.02]); hi = np.array([0.34, 1.34, 1.30])
    if part:
        b = part_bounds(part)
        if b is None: raise SystemExit('нет костей со слотом «%s»' % part)
        lo, hi = np.maximum(lo, b[0]), np.minimum(hi, b[1])
        w = max(abs(lo[0]), abs(hi[0])); lo[0], hi[0] = -w, w        # держим симметрию по X
    n = np.maximum(((hi - lo) / cell).astype(int) + 1, 2)
    xs = lo[0] + np.arange(n[0]) * cell
    ys = lo[1] + np.arange(n[1]) * cell
    zs = lo[2] + np.arange(n[2]) * cell
    F = np.full(tuple(n), 9.0)
    OWN = np.full(tuple(n), -1, dtype=np.int16)
    slots = sorted({s['slot'] for s in segs})

    for s in sorted(segs, key=lambda x: x['layer']):
        # СРЕЗ СЛОЁВ — как buildLayers в игре: приёмка по слоям невозможна, если смотришь только итог.
        # Рез (слой 3) не срезается никогда: он не добавляет объём, а вычитает уже собранный
        if s['layer'] < 3 and s['layer'] >= layer: continue
        reach = max(s['r0'], s['r1']) * max(1.0, s['sec'], s['dep']) + s['blend'] + FUR + cell
        mn = np.minimum(s['a'], s['b1']) - reach
        mx = np.maximum(s['a'], s['b1']) + reach
        i0 = np.maximum(((mn - lo) / cell).astype(int), 0)
        i1 = np.minimum(((mx - lo) / cell).astype(int) + 2, n)
        if np.any(i1 <= i0): continue
        gx, gy, gz = np.meshgrid(xs[i0[0]:i1[0]], ys[i0[1]:i1[1]], zs[i0[2]:i1[2]], indexing='ij')
        P = np.stack([gx.ravel(), gy.ravel(), gz.ravel()], axis=1)
        q = (P - s['a']) @ s['R']              # в оси кости: она растёт по локальному +Y
        rad = np.hypot(q[:, 0] / s['sec'], q[:, 2] / s['dep'])
        along = q[:, 1]
        bb = (s['r0'] - s['r1']) / max(1e-4, s['L'])
        aa = math.sqrt(max(0.0, 1 - bb * bb))
        k = -bb * rad + aa * along
        d = np.where(k < 0, np.hypot(rad, along) - s['r0'],
            np.where(k > aa * s['L'], np.hypot(rad, along - s['L']) - s['r1'],
                     aa * rad + bb * along - s['r0'])).reshape(gx.shape)

        sub = (slice(i0[0], i1[0]), slice(i0[1], i1[1]), slice(i0[2], i1[2]))
        f = F[sub]
        if s['layer'] == 3:                       # ВЫЧИТАНИЕ — рот, ноздри
            # РОВНО ТОТ ЖЕ k, ЧТО В `BoneMesher`: там рез считается как -Smin(-field, d, s.blend).
            # Здесь стоял коэффициент 0.35, и это делало инструмент бесполезным ровно там, где он
            # нужнее всего: рот на картинке резался резче, чем в игре, — я смотрел бы на щель,
            # которой у игрока нет. Смысл собственного рендера в том, что он считает ИГРОВОЕ поле
            kk = s['blend']
            h = np.maximum(kk - np.abs(-f - d), 0) / kk
            F[sub] = -(np.minimum(-f, d) - h * h * kk * 0.25)
            continue
        sid = slots.index(s['slot'])
        kk = np.where(OWN[sub] == sid, s['blend'], s['blend'] * WELD)
        h = np.maximum(kk - np.abs(f - d), 0) / kk
        OWN[sub] = np.where(d < f, sid, OWN[sub])
        F[sub] = np.minimum(f, d) - h * h * kk * 0.25
    return F - FUR, lo, cell, n

def shot(F, lo, cell, path, view='side', title=''):
    """Проекция: вдоль оси взгляда ищем первую занятую ячейку, тень — из градиента поля."""
    axis = {'side': 0, 'front': 2, 'top': 1}[view]
    inside = F < 0
    hit = inside.any(axis=axis)
    # СМОТРИМ С ПРАВИЛЬНОЙ СТОРОНЫ. Морда глядит в +Z, поэтому анфас — это взгляд НАВСТРЕЧУ ей, от
    # больших Z к малым: ищем первую занятую ячейку с конца оси. Иначе «анфас» показывает зад зверя
    if view == 'front':
        idx = np.where(hit, inside.shape[axis] - 1 - np.flip(inside, axis=axis).argmax(axis=axis), 0)
    else:
        idx = np.where(hit, inside.argmax(axis=axis), 0)

    gx, gy, gz = np.gradient(F, cell)
    if view == 'side':      # смотрим вдоль X: экран Z×Y
        take = lambda A: np.take_along_axis(A, idx[None, :, :], 0)[0]
        img_n = np.stack([take(gx), take(gy), take(gz)], -1); img = img_n.transpose(1, 0, 2)
        M = hit.T
        ext = [lo[2], lo[2] + cell * F.shape[2], lo[1], lo[1] + cell * F.shape[1]]
        img = img[:, :, :]; M = hit  # (y,z)
        img = np.stack([take(gx), take(gy), take(gz)], -1)   # (y,z,3)
    elif view == 'front':   # вдоль Z: экран X×Y
        take = lambda A: np.take_along_axis(A, idx[:, :, None], 2)[:, :, 0]
        img = np.stack([take(gx), take(gy), take(gz)], -1).transpose(1, 0, 2)
        M = hit.T
        ext = [lo[0], lo[0] + cell * F.shape[0], lo[1], lo[1] + cell * F.shape[1]]
    if view == 'side':
        M = hit
        ext = [lo[2], lo[2] + cell * F.shape[2], lo[1], lo[1] + cell * F.shape[1]]

    n = img / (np.linalg.norm(img, axis=-1, keepdims=True) + 1e-9)
    light = np.array([0.4, 0.7, 0.6]); light /= np.linalg.norm(light)
    lam = np.clip(n @ light, 0, 1) * 0.72 + 0.28
    out = np.where(M, lam, 0.0)
    out = np.flipud(out)

    plt.figure(figsize=(out.shape[1] / 42, out.shape[0] / 42), dpi=100)
    plt.imshow(out, cmap='bone', vmin=0, vmax=1, extent=ext, aspect='equal')
    if title: plt.title(title, color='#c8d0dc', fontsize=9)
    plt.axis('off'); plt.tight_layout(pad=0.1)
    plt.savefig(path, dpi=100, facecolor='#20242c')
    plt.close()
    print('снято:', path, ' пикселей тела:', int(M.sum()))

# ── ЭТАЛОННЫЙ КОНТУР ВОЛКА В ПРОФИЛЬ (доли холки W = 1.16, переведены в наши метры).
# Собран из промеров и правил для художников: холка — высшая точка, голова несётся вровень со спиной
# или чуть ниже, грудь опускается до локтя, просвет под грудью не меньше её глубины, пах поднят.
# Смысл не в том, чтобы «подогнать по картинке»: одна интегральная мера ловит целое — длинную шею,
# горб за холкой, трубу-морду, — тогда как двадцать частных метрик дружно молчат
ETALON_TOP = [(-0.58, 1.10), (-0.45, 1.12), (-0.20, 1.13), (0.00, 1.15), (0.28, 1.16),
              (0.45, 1.14), (0.62, 1.10), (0.80, 1.07), (0.95, 1.06), (1.05, 1.03), (1.16, 0.96)]
# НИЖНЯЯ ЛИНИЯ СОБРАНА ПО ОПОРАМ, А НЕ НА ГЛАЗ. Первая версия была прикинута «примерно под верхней»,
# и в передней половине оказалась вздором: высота морды у носа выходила 3 см. Опоры теперь такие —
# глубина груди ≈ половина высоты в холке (низ груди 0.56 W), просвет под грудью не меньше её
# глубины, живот подтянут на 5-7 см выше низа груди, преднагрудье чуть ниже плечевого сустава,
# горло идёт прямой от преднагрудья к углу челюсти, угол челюсти на 0.20 W ниже темени.
#     ХВОСТ В СРАВНЕНИЕ НЕ ВХОДИТ: он свисает через всю зону крупа и давал «низ ниже эталона на
# 18 см» там, где мерился вовсе не корпус. Длину хвоста проверяет своя метрика (0.50-0.60 холки)
ETALON_BOT = [(-0.42, 0.800), (-0.20, 0.720), (0.05, 0.700), (0.20, 0.655), (0.42, 0.735),
              (0.60, 0.800), (0.80, 0.855), (0.90, 0.875), (1.05, 0.888), (1.16, 0.905)]

def compare(F, lo, cell, path):
    """Наш контур против эталона: рисуем поверх рендера и считаем расхождение в сантиметрах."""
    inside = F < 0
    hit = inside.any(axis=0)                      # силуэт-проекция: для картинки
    # МЕРИМ ТО ЖЕ, ЧТО СРАВНИВАЕМ. Эталон описывает контур КОРПУСА, а проекция сбоку накрывает ещё и
    # ноги — низ «проваливался» на 41 см там, где метрика ловила переднюю лапу вместо груди. Ноги
    # отстоят от плоскости симметрии, корпус и голова лежат на ней: медианный срез оставляет корпус
    ix = int(round((0.0 - lo[0]) / cell))
    med = inside[max(0, min(F.shape[0] - 1, ix))]  # (y, z) на X = 0
    zs = lo[2] + np.arange(F.shape[2]) * cell
    ys = lo[1] + np.arange(F.shape[1]) * cell
    top, bot = [], []
    for j in range(F.shape[2]):
        col = np.where(med[:, j])[0]
        if len(col) == 0: top.append(np.nan); bot.append(np.nan); continue
        top.append(ys[col.max()]); bot.append(ys[col.min()])
    top, bot = np.array(top), np.array(bot)

    et_t = np.interp(zs, [p[0] for p in ETALON_TOP], [p[1] for p in ETALON_TOP],
                     left=np.nan, right=np.nan)
    et_b = np.interp(zs, [p[0] for p in ETALON_BOT], [p[1] for p in ETALON_BOT],
                     left=np.nan, right=np.nan)
    # МАСКИ РАЗДЕЛЬНЫЕ: линии эталона кончаются в разных местах (низ не доходит до затылка), и общая
    # маска гасила весь низ в nan — метрика молчала ровно там, где силуэт хуже всего
    mt = ~np.isnan(top) & ~np.isnan(et_t)
    mb = ~np.isnan(bot) & ~np.isnan(et_b)
    dt = np.abs(top[mt] - et_t[mt]); db = np.abs(bot[mb] - et_b[mb])
    print('РАСХОЖДЕНИЕ С ЭТАЛОНОМ: верх %.3f ср / %.3f макс   низ %.3f ср / %.3f макс'
          % (dt.mean(), dt.max(), db.mean(), db.max()))
    # РАЗБОР ПО ОБЛАСТЯМ: одно число говорит «плохо», карта по областям — где именно
    zones = [('пах/круп', -0.42, -0.20), ('поясница', -0.20, 0.00), ('грудь/холка', 0.00, 0.35),
             ('стык шея-грудь', 0.35, 0.55), ('шея', 0.55, 0.80), ('голова', 0.80, 1.20)]
    for nm, z0, z1 in zones:
        zt = mt & (zs >= z0) & (zs < z1); zb = mb & (zs >= z0) & (zs < z1)
        vt = np.abs(top[zt] - et_t[zt]); vb = np.abs(bot[zb] - et_b[zb])
        st = '%+.3f' % np.mean(top[zt] - et_t[zt]) if zt.any() else '   —  '
        sb = '%+.3f' % np.mean(bot[zb] - et_b[zb]) if zb.any() else '   —  '
        print('   %-14s верх %s (|%.3f|)   низ %s (|%.3f|)'
              % (nm, st, vt.mean() if zt.any() else 0, sb, vb.mean() if zb.any() else 0))

    out = np.flipud(np.where(hit, 0.55, 0.0))
    ext = [lo[2], lo[2] + cell * F.shape[2], lo[1], lo[1] + cell * F.shape[1]]
    plt.figure(figsize=(11, 6), dpi=100)
    plt.imshow(out, cmap='bone', vmin=0, vmax=1, extent=ext, aspect='equal')
    plt.plot([p[0] for p in ETALON_TOP], [p[1] for p in ETALON_TOP], '-', color='#ff9f43', lw=2, label='эталон')
    plt.plot([p[0] for p in ETALON_BOT], [p[1] for p in ETALON_BOT], '-', color='#ff9f43', lw=2)
    plt.plot(zs[mt], top[mt], '-', color='#54d1ff', lw=1.4, label='наш силуэт')
    plt.plot(zs[mb], bot[mb], '-', color='#54d1ff', lw=1.4)
    plt.legend(loc='lower left', fontsize=9, facecolor='#20242c', labelcolor='#c8d0dc')
    plt.axis('off'); plt.tight_layout(pad=0.2)
    plt.savefig(path, dpi=100, facecolor='#20242c'); plt.close()
    print('снято:', path)
    return dt.mean() + db.mean()

if __name__ == '__main__':
    tag = (ARGS.part or 'body').replace('/', '_')
    lay = '' if ARGS.layer >= 4 else '_L%d' % ARGS.layer
    cell = ARGS.cell if not ARGS.part else min(ARGS.cell, 0.005)   # часть меряется мельче — свой масштаб
    F, lo, cell, n = grid_field(cell, ARGS.part, ARGS.layer)
    print('вид %s · часть %s · слой %d · сетка %s = %d ячеек'
          % (ARGS.species, ARGS.part or 'всё тело', ARGS.layer, n, int(np.prod(n))))
    p = lambda name: os.path.join(OUT_DIR, '%s_%s%s_%s.png' % (ARGS.species, tag, lay, name))
    shot(F, lo, cell, p('side'), 'side', 'профиль · %s%s' % (ARGS.part or 'тело', lay))
    shot(F, lo, cell, p('front'), 'front', 'анфас · %s%s' % (ARGS.part or 'тело', lay))
    if not ARGS.part:
        compare(F, lo, cell, p('compare'))
    else:
        # ЭТАЛОН ЧАСТИ — отдельный файл, снятый с референса. Пока его нет, детектор ЧЕСТНО молчит про
        # расхождение, а не подставляет контур целого тела: тот про часть ничего не знает
        et = os.path.join(SPECIES_DIR, 'etalon', '%s.py' % tag)
        print('эталон части: %s' % (et if os.path.exists(et)
              else 'НЕТ — сравнивать не с чем, снять с референса до правок'))
