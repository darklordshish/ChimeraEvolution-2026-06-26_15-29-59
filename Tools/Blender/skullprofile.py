# -*- coding: utf-8 -*-
"""СИЛУЭТ ЧЕРЕПА С ПЛАСТИНЫ — попиксельно, из двух ортографий.

ЗАЧЕМ. Череп, собранный из труб «ось плюс радиус», остаётся трубой с дырками: у него нет ни свода,
ни ямы, ни дуги, стоящей над провалом. Оболочку нельзя приблизить набором капсул — её надо СНЯТЬ.
Профиль даёт верх и низ, вид сверху — ширину; вместе это сечения, и по ним лофтится настоящая форма.
Это ровно правило методики «эталон снимается с натуры попиксельно», применённое к части.

ЧТО ОТДАЁТ. `species/wolf_skull_data.py`: массив станций вдоль оси черепа, каждая —
(доля длины, верх, низ, полуширина) в ДОЛЯХ ДЛИНЫ ЧЕРЕПА. Доли, а не метры: калибр вида задаётся
одним числом, и в данных донора метров быть не должно.

Запуск:  python skullprofile.py
"""
import os, io, sys
import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
PLATE = os.path.join(ROOT, 'Anatomy', 'species', 'wolf', 'ref', 'skeleton', 'wolf_skull_plate.jpg')
OUT_PY = os.path.join(HERE, 'species', 'wolf_skull_data.py')
OUT_PNG = os.path.join(HERE, 'out', 'wolf_skull_extract.png')

# ── ПАНЕЛИ ПЛАСТИНЫ (пиксели оригинала 440×1357) ─────────────────────────────────────────────────
PROFILE = (18, 55, 438, 246)     # профиль: нос ВПРАВО (низ подрезан — ниже кости тень стола)
DORSAL = (95, 528, 350, 912)     # вид сверху: нос ВВЕРХ
# Кость на пластине светлая, фон — тёмный шершавый камень. Порога по яркости достаточно
THR = 132


def mask(box, thr=THR):
    im = Image.open(PLATE).convert('RGB').crop(box)
    a = np.asarray(im).astype(float)
    v = a.mean(2)
    m = v > thr
    m = ndimage.binary_opening(m, np.ones((3, 3)))
    m = ndimage.binary_closing(m, np.ones((7, 7)))
    lab, n = ndimage.label(m)
    if n:
        sizes = ndimage.sum(m, lab, range(1, n + 1))
        m = lab == (int(np.argmax(sizes)) + 1)
    return ndimage.binary_fill_holes(m), im


def main():
    # ── ПРОФИЛЬ: верх и низ по столбцам ──────────────────────────────────────────────────────────
    pm, pim = mask(PROFILE)
    H, W = pm.shape
    top, bot = {}, {}
    for x in range(W):
        col = np.flatnonzero(pm[:, x])
        if col.size >= 4:
            top[x], bot[x] = int(col.min()), int(col.max())
    xs = sorted(top)
    x0, x1 = xs[0], xs[-1]
    print('ПРОФИЛЬ: череп по X %d..%d (%d px), панель %dx%d' % (x0, x1, x1 - x0, W, H))

    # НА ПЛАСТИНЕ ЧЕРЕП БЕЗ НИЖНЕЙ ЧЕЛЮСТИ, и это пришлось увидеть, а не предположить. Сперва я
    # резал силуэт линией «граница зубных рядов», считая, что пасть закрыта; наложение показало, что
    # снизу идут скуловая дуга и нёбо ВЕРХНЕЙ челюсти, а нижней в кадре нет вовсе. Оттуда и брались
    # «угол челюсти» с «венечным отростком» — точки, снятые с чужих костей, и челюсть выходила плитой.
    #     Поэтому здесь снимается ТОЛЬКО МОЗГОВОЙ ЧЕРЕП. Нижняя челюсть — отдельный источник
    # (`ref/skeleton/wolf_skeleton_side.jpg`, где она на месте) и отдельная таблица

    # ── ВИД СВЕРХУ: полуширина по строкам (ось черепа вертикальна, нос вверху) ───────────────────
    dm, dim = mask(DORSAL)
    DH, DW = dm.shape
    wid = {}
    for y in range(DH):
        row = np.flatnonzero(dm[y])
        if row.size >= 4:
            wid[y] = (int(row.min()), int(row.max()))
    ys = sorted(wid)
    y0, y1 = ys[0], ys[-1]
    print('ВИД СВЕРХУ: череп по Y %d..%d (%d px), панель %dx%d' % (y0, y1, y1 - y0, DW, DH))

    # ── СТАНЦИИ: t от 0 (затылок) до 1 (резцы) ───────────────────────────────────────────────────
    N = 48
    rows = []
    L = float(x1 - x0)
    for i in range(N + 1):
        t = i / N
        x = int(round(x0 + t * L))
        if x not in top:
            continue
        cr_top, cr_bot = top[x], bot[x]
        # ширина: вид сверху идёт носом ВВЕРХ, значит t=0 (затылок) это y1
        dy = int(round(y1 - t * (y1 - y0)))
        if dy in wid:
            a, b = wid[dy]
            half = (b - a) / 2.0
        else:
            half = 0.0
        rows.append([t, (H - cr_top) / L, (H - cr_bot) / L, half / L])

    # ── СКУЛОВАЯ ДУГА ВЫЧИТАЕТСЯ ИЗ ПРОФИЛЯ ШИРИНЫ ──────────────────────────────────────────────
    # Обтяжка по двум видам не умеет вогнутостей: вид сверху меряет ширину ПО ДУГАМ, и оболочка
    # засасывает их в череп — височная яма исчезает, голова читается батоном. Три захода я пытался
    # вернуть её вычитанием, и каждый раз рез либо не открывался наружу, либо прорывал бок дырой.
    #     Здесь дуга ИСКЛЮЧЕНА из ширины и строится отдельной костью. Тогда яма между сводом и дугой
    # получается ПО ПОСТРОЕНИЮ: её не надо вырезать, потому что её нечем заполнить
    ARCH_T0, ARCH_T1 = 0.13, 0.63
    w0 = [r[3] for r in rows if abs(r[0] - ARCH_T0) < 0.011][0]
    w1 = [r[3] for r in rows if abs(r[0] - ARCH_T1) < 0.011][0]
    arch_peak, arch_at = 0.0, ARCH_T0
    for r in rows:
        if ARCH_T0 < r[0] < ARCH_T1:
            k = (r[0] - ARCH_T0) / (ARCH_T1 - ARCH_T0)
            plain = w0 + (w1 - w0) * k
            if r[3] - plain > arch_peak:
                arch_peak, arch_at = r[3] - plain, r[0]
            r[3] = min(r[3], plain)
    print('ДУГА: вынос %.3f длины черепа на t=%.2f (свод там %.3f)' % (arch_peak, arch_at, w0))

    # СГЛАЖИВАНИЕ. Контур снят попиксельно, и вместе с формой в него попал шум порога: соседние
    # станции скачут на пиксель-два, а на модели это волны поперёк черепа. Окно в пять станций
    # снимает дрожь, не трогая форму — характерные длины у черепа много больше пяти станций
    for col in (1, 2, 3):
        raw = [r[col] for r in rows]
        for i, r in enumerate(rows):
            lo, hi = max(0, i - 2), min(len(raw), i + 3)
            r[col] = sum(raw[lo:hi]) / (hi - lo)

    # ── ОСЬ ОБОЛОЧКИ: прямая через середины крайних сечений ──────────────────────────────────────
    # Станции обязаны быть ОТНОСИТЕЛЬНО оси, а не относительно низа кадра: иначе форма привязана к
    # рамке снимка, и первый же перекадрированный референс её сломает. Ось же — свойство черепа
    mid0 = (rows[0][1] + rows[0][2]) / 2.0
    mid1 = (rows[-1][1] + rows[-1][2]) / 2.0
    for r in rows:
        ax = mid0 + (mid1 - mid0) * r[0]
        r[1] -= ax
        r[2] -= ax
    AXIS = ((x0 + PROFILE[0], int(round(H - mid0 * L)) + PROFILE[1]),
            (x1 + PROFILE[0], int(round(H - mid1 * L)) + PROFILE[1]))
    print('ОСЬ ОБОЛОЧКИ в пикселях пластины: %s → %s' % AXIS)

    # ── ОТЧЁТ КАРТИНКОЙ: что именно снято ────────────────────────────────────────────────────────
    ov = pim.convert('RGB'); d = ImageDraw.Draw(ov)
    for x in xs:
        d.point((x, top[x]), fill=(255, 0, 255))
        d.point((x, bot[x]), fill=(0, 210, 255))
    ov2 = dim.convert('RGB'); d2 = ImageDraw.Draw(ov2)
    for y in ys:
        a, b = wid[y]
        d2.point((a, y), fill=(255, 0, 255)); d2.point((b, y), fill=(0, 210, 255))
    canvas = Image.new('RGB', (max(ov.width, ov2.width), ov.height + ov2.height + 8), (20, 20, 20))
    canvas.paste(ov, (0, 0)); canvas.paste(ov2, (0, ov.height + 8))
    k = min(3.0, 1500.0 / canvas.height)
    canvas.resize((int(canvas.width * k), int(canvas.height * k)), Image.LANCZOS).save(OUT_PNG)

    with io.open(OUT_PY, 'w', encoding='utf-8') as f:
        f.write('# -*- coding: utf-8 -*-\n')
        f.write('"""СЕЧЕНИЯ ЧЕРЕПА ВОЛКА — СНЯТЫ С ОРТОГРАФИЙ, не выведены из общих соображений.\n\n'
                'Источник: `Anatomy/species/wolf/ref/skeleton/wolf_skull_plate.jpg`, панели «профиль»\n'
                'и «вид сверху». Сгенерировано `Tools/Blender/skullprofile.py` — РУКАМИ НЕ ПРАВИТЬ,\n'
                'правка потеряется при следующем прогоне; менять надо панели и порог в самом скрипте.\n\n'
                'Формат станции: (t, верх, низ, полуширина) — ТОЛЬКО МОЗГОВОЙ ЧЕРЕП: на пластине\n'
                'нижней челюсти нет вовсе, она снимается с другого референса.\n'
                'Все величины — В ДОЛЯХ ДЛИНЫ ЧЕРЕПА, отсчёт от затылка (t=0) к резцам (t=1);\n'
                'вертикаль отсчитывается от низа панели, поэтому в данных нет ни одного метра."""\n\n')
        f.write('# Концы оси в пикселях ПЛАСТИНЫ: по ним вид ставит оболочку через `SK`\n')
        f.write('AXIS_PX = ((%d, %d), (%d, %d))\n\n'
                % (AXIS[0][0], AXIS[0][1], AXIS[1][0], AXIS[1][1]))
        f.write('# Скуловая дуга ИСКЛЮЧЕНА из ширины и строится отдельной костью:\n')
        f.write('# (вынос дуги за свод, где он максимален, начало и конец области дуги)\n')
        f.write('ARCH = (%.4f, %.4f, %.4f, %.4f)\n\n' % (arch_peak, arch_at, ARCH_T0, ARCH_T1))
        f.write('STATIONS = [\n')
        for r in rows:
            f.write('    (%.4f, %.4f, %.4f, %.4f),\n' % tuple(r))
        f.write(']\n')
    print('станций: %d → %s' % (len(rows), OUT_PY))
    print('проверка глазом: %s' % OUT_PNG)


if __name__ == '__main__':
    main()
