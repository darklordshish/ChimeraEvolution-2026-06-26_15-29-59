# -*- coding: utf-8 -*-
"""СОГЛАСИЕ ЛИСТА С САМИМ СОБОЙ — можно ли верить референс-листу как ортопроекциям одного тела.

Лист сгенерирован картинкой, а не отрендерен с модели: каждый ракурс нейросеть выводит заново, и ракурсы могут описывать
разные тела. Прежде чем восстанавливать по листу 3D (руками по подложкам или multiview-генератором), лист проверяется
тем, что у ортопроекций обязано совпадать:
  • АНФАС ↔ СПИНА — один и тот же силуэт зеркально: крайние точки слева и справа на каждой высоте;
  • ПРОФИЛЬ ЛЕВЫЙ ↔ ПРАВЫЙ — один силуэт зеркально: передний и задний край на каждой высоте;
  • ОПОРНЫЕ ВЫСОТЫ — сужение шеи, подмышка, промежность, кончики пальцев: в ортопроекциях высота точки одна во всех видах
    (для ортокамеры это и есть эпиполярное условие: точка обязана лежать на той же строке).
Каждый вид нормирован СВОИМ ростом (макушка — подошва) и переведён в метры ростом листа из `ref_contours.json`, так что
разница масштаба рендера (±1 %) в расхождение не идёт. Разрешение листа ~3.9 мм на пиксель: расхождение в 1 px — шум.

Запуск:  python list_soglasie.py          → таблица расхождений и out/list-soglasie.png (контуры видов попарно)
"""
import json
import os

import numpy as np
from PIL import Image, ImageDraw

import ref_trace as R

HERE = os.path.dirname(os.path.abspath(__file__))
PANELS = dict(R.PANELS, side_r=(995, 1100))          # правый профиль (SIDE RIGHT, лицом вправо)
H = json.load(open(os.path.join(HERE, 'ref_contours.json'), encoding='utf-8'))['height']
BANDS = [(0.92, 1.00, 'голова'), (0.80, 0.92, 'шея и плечи'), (0.52, 0.80, 'торс и руки'), (0.00, 0.52, 'ноги')]


def view(L, name):
    """Силуэт вида: для каждой высоты (м от земли, по своему росту) — отрезки маски в пикселях."""
    top, sole, per = R.figure(L, name) if name in R.PANELS else figure_r(L, name)
    k = H / (sole - top)
    return dict(top=top, sole=sole, k=k, rows={round((sole - y) * k, 4): per[y] for y in range(top, sole + 1)})


def figure_r(L, name):
    x0, x1 = PANELS[name]
    m = L[R.ROWS[0]:R.ROWS[1], x0:x1] > R.THR
    rows = np.where(m.any(1))[0]
    top, sole = int(rows.min()) + R.ROWS[0], int(rows.max()) + R.ROWS[0]
    return top, sole, {y: [(a + x0, b + x0) for a, b in R.runs_of(m[y - R.ROWS[0]])] for y in range(top, sole + 1)}


def at(v, h):
    """Отрезки на высоте h (ближайшая строка)."""
    keys = np.array(list(v['rows']))
    return v['rows'][float(keys[np.abs(keys - h).argmin()])]


def center(v, lo=0.93, hi=0.97):
    """Ось фигуры по черепу (одна дуга в строке)."""
    c = [(r[0][0] + r[-1][1] + 1) / 2 for h, r in v['rows'].items() if lo * H <= h <= hi * H and len(r) == 1]
    return float(np.mean(c))


def extents(v, c, h, sign):
    """Крайние точки силуэта в метрах от оси: (ближний к −, ближний к +); sign=−1 зеркалит вид."""
    segs = at(v, h)
    if not segs:
        return None
    a, b = (segs[0][0] - c) * v['k'], (segs[-1][1] + 1 - c) * v['k']
    return (a, b) if sign > 0 else (-b, -a)


def compare(v1, c1, v2, c2, sign2):
    """Расхождение крайних точек двух видов по высотам: список (h, Δ левого края, Δ правого края) в метрах."""
    out = []
    for h in np.arange(0.02, H - 0.01, 0.01):
        e1, e2 = extents(v1, c1, h, 1), extents(v2, c2, h, sign2)
        if e1 and e2:
            out.append((h, e1[0] - e2[0], e1[1] - e2[1]))
    return out


def neck(v):
    """Сужение шеи: самая узкая строка между 0.80 и 0.90 роста (одна дуга)."""
    best = None
    for h, r in v['rows'].items():
        if 0.80 * H <= h <= 0.90 * H and len(r) == 1:
            w = r[0][1] - r[0][0]
            if best is None or w < best[1]:
                best = (h, w)
    return best[0] if best else None


def armpit(v):
    """Подмышка: самая высокая строка торса, где рука отделилась от тела (три дуги и больше)."""
    hs = [h for h, r in v['rows'].items() if 0.55 * H <= h <= 0.80 * H and len(r) >= 3]
    return max(hs) if hs else None


def crotch(v, c):
    """Промежность: самая высокая строка, где по оси фигуры фон (ноги разошлись)."""
    hs = [h for h, r in v['rows'].items() if h <= 0.60 * H and not any(a <= c <= b for a, b in r)]
    return max(hs) if hs else None


def fingertips(v):
    """Кончики пальцев: самая низкая строка выше колена, где снаружи ног ещё есть дуги рук (4 дуги)."""
    hs = [h for h, r in v['rows'].items() if 0.30 * H <= h <= 0.60 * H and len(r) >= 4]
    return min(hs) if hs else None


def stats(rows, lo, hi):
    d = [abs(x) for h, a, b in rows if lo * H <= h < hi * H for x in (a, b)]
    return (np.mean(d) * 100, np.max(d) * 100) if d else (0, 0)


def main():
    L = R.load()
    V = {n: view(L, n) for n in PANELS}
    C = {n: center(V[n]) for n in V}
    print('рост в пикселях: ' + ', '.join('%s %d' % (n, V[n]['sole'] - V[n]['top']) for n in V))

    fb = compare(V['front'], C['front'], V['back'], C['back'], -1)
    # профили: ось — середина голеностопа, как в ref_trace; левый лицом влево, правый лицом вправо
    def ankle(v):
        rows = [r for h, r in v['rows'].items() if 0.07 <= h <= 0.10 and r]
        return float(np.mean([(r[0][0] + r[-1][1] + 1) / 2 for r in rows]))
    lr = compare(V['side'], ankle(V['side']), V['side_r'], ankle(V['side_r']), -1)

    print('\nСИЛУЭТ, расхождение крайних точек, см (среднее / макс):')
    print('%-14s %-18s %-18s' % ('пояс', 'анфас ↔ спина', 'профиль Л ↔ П'))
    for lo, hi, name in BANDS:
        a, b = stats(fb, lo, hi), stats(lr, lo, hi)
        print('%-14s %5.1f / %-10.1f %5.1f / %-10.1f' % (name, a[0], a[1], b[0], b[1]))

    print('\nОПОРНЫЕ ВЫСОТЫ, м (одна точка — одна высота во всех видах):')
    marks = [('сужение шеи', {n: neck(V[n]) for n in V}),
             ('подмышка', {n: armpit(V[n]) for n in ('front', 'back')}),
             ('промежность', {n: crotch(V[n], C[n]) for n in ('front', 'back')}),
             ('кончики пальцев', {n: fingertips(V[n]) for n in ('front', 'back')})]
    for name, d in marks:
        vals = [x for x in d.values() if x is not None]
        spread = (max(vals) - min(vals)) * 100 if vals else 0
        print('%-16s %s   разброс %.1f см' % (name, '  '.join('%s %.3f' % (n, x) for n, x in d.items() if x is not None), spread))

    draw(L, V, C, fb, lr)


def draw(L, V, C, fb, lr):
    """Пары видов наложены: анфас красным, спина (зеркально) жёлтым; профиль Л голубым, П (зеркально) зелёным."""
    S = 2
    img = Image.new('RGB', (2 * 260 * S, 520 * S), (38, 40, 46))
    d = ImageDraw.Draw(img)

    def put(v, c, sign, ox, col):
        for h, segs in v['rows'].items():
            y = (H - h) / H * 480 * S + 20 * S
            for seg in segs:
                for x in (seg[0], seg[1] + 1):
                    u = (x - c) * v['k'] * sign
                    d.point((ox + u / H * 480 * S, y), fill=col)

    put(V['front'], C['front'], 1, 130 * S, (255, 90, 70))
    put(V['back'], C['back'], -1, 130 * S, (255, 210, 60))
    ank = lambda v: float(np.mean([(r[0][0] + r[-1][1] + 1) / 2 for h, r in v['rows'].items() if 0.07 <= h <= 0.10 and r]))
    put(V['side'], ank(V['side']), 1, 390 * S, (80, 200, 255))
    put(V['side_r'], ank(V['side_r']), -1, 390 * S, (90, 230, 120))
    os.makedirs(os.path.join(HERE, 'out'), exist_ok=True)
    img.save(os.path.join(HERE, 'out', 'list-soglasie.png'))


if __name__ == '__main__':
    main()
