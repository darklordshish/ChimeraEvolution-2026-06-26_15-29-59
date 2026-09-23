# -*- coding: utf-8 -*-
"""ОБВОД ЛИСТА — контуры референс-листа геймдизайнера (23.09) по строкам, в метрах: анфас, профиль, спина.

Зачем. Лоу-поли по референсу строят так: ортопроекции анфас и профиль — подложкой, болванка из примитивов подгоняется под
ОБА контура сразу, форма проверяется со всех сторон (ресёрч 23.09 — `REFS.md`, раздел «Лист»). Наше поле — та же болванка
из примитивов с мягким объединением, поэтому граф человека строится подгонкой под эти контуры, а не сборкой из анатомии
по данным. Этот файл — первая половина метода: снимает контуры. Вторая — `ref_check.py`: сверяет с ними нашу оболочку.

Масштаб. Рост листа в пикселях — от макушки до подошвы в каждом ракурсе свой (рендер чуть разный, ±1 %). Метры задаёт
линия глаз: на ней игру ждёт первое лицо (1.70 м, калибр прежних поставок).

Оси — как у графа: Y от земли, X вбок (правая сторона — положительная), Z вперёд.
  • анфас: X = (столбец − середина) × масштаб; ширины усреднены по двум сторонам (лист сгенерирован и не строго симметричен);
  • профиль (SIDE LEFT, лицом влево): Z = (столбец начала − столбец) × масштаб, начало — середина голеностопа.

КОНТУРЫ ЕДУТ В РЕПОЗИТОРИЙ, ЛИСТ — НЕТ. Картинки референсов остаются на диске (`.gitignore`: `Anatomy/species/*/ref/` —
крупные пуши через прокси LFS падают), в репозиторий едет то, что их описывает. Граф строится по `ref_contours.json` рядом:
сборка воспроизводима без листа на диске, а этот файл пересоздаёт контуры, когда лист есть.

Запуск:  python ref_trace.py          → ref_contours.json и out/ref-trace.png (обвод поверх листа — проверить глазом)
"""
import json
import os

import numpy as np
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
SHEET = os.path.join(HERE, 'ref', 'sheet', 'atlet_list_2026-09-23_REFERENCE.png')

EYE_M = 1.70            # линия глаз, м
THR = 62                # порог яркости: фон листа 34–38, тень под стопами до ~50, шорты ~90
ROWS = (70, 578)        # ниже 578 — подписи ракурсов
# столбцы панелей (по пустым промежуткам между фигурами)
PANELS = {'front': (28, 212), 'side': (450, 538), 'back': (575, 770)}
EYE_ROW_FRONT = 116.75  # зрачки на анфасе (снято глазом с увеличенного кропа, ±0.5 px)
MIN_RUN = 2             # короче — шум края


def load():
    return np.asarray(Image.open(SHEET).convert('L')).astype(int)


def runs_of(row):
    """Непрерывные отрезки маски в строке: [(x0, x1)] включительно."""
    out, start = [], None
    for i, v in enumerate(row):
        if v and start is None:
            start = i
        if not v and start is not None:
            out.append((start, i - 1)); start = None
    if start is not None:
        out.append((start, len(row) - 1))
    return [r for r in out if r[1] - r[0] + 1 >= MIN_RUN]


def figure(L, view):
    x0, x1 = PANELS[view]
    m = L[ROWS[0]:ROWS[1], x0:x1] > THR
    rows = np.where(m.any(1))[0]
    top, sole = int(rows.min()) + ROWS[0], int(rows.max()) + ROWS[0]
    per = {}
    for y in range(top, sole + 1):
        per[y] = [(a + x0, b + x0) for a, b in runs_of(m[y - ROWS[0]])]
    return top, sole, per


def trace():
    L = load()
    ft, fs, fr = figure(L, 'front')
    H = EYE_M * (fs - ft) / (fs - EYE_ROW_FRONT)          # рост в метрах по линии глаз
    out = dict(sheet=os.path.relpath(SHEET, HERE).replace('\\', '/'), eye=EYE_M, height=round(H, 4), views={})

    # АНФАС: середина — по голове (строки черепа выше ушей) и по талии; обе сходятся до пикселя
    k = H / (fs - ft)
    skull = [fr[y][0] for y in range(ft + 3, ft + 12) if len(fr[y]) == 1]
    waist = [r for y in range(262, 272) for r in fr[y] if r[0] < 120 < r[1]]
    cx = float(np.mean([(a + b + 1) / 2 for a, b in skull + waist]))
    rows = []
    for y in range(ft, fs + 1):
        h = (fs - y) * k
        segs = [((a - cx) * k, (b + 1 - cx) * k) for a, b in fr[y]]
        rows.append(dict(y=round(h, 4), x=[[round(a, 4), round(b, 4)] for a, b in segs]))
    out['views']['front'] = dict(top=ft, sole=fs, center=round(cx, 2), m_per_px=round(k, 6), rows=rows)

    # ПРОФИЛЬ: свой рост в пикселях, тот же рост в метрах
    st, ss, sr = figure(L, 'side')
    k = H / (ss - st)
    ankle_rows = range(ss - int(0.10 / k), ss - int(0.07 / k))            # голеностоп 7–10 см от земли, над стопой
    oz = float(np.mean([(r[0][0] + r[-1][1] + 1) / 2 for y in ankle_rows for r in [sr[y]] if r]))
    rows = []
    for y in range(st, ss + 1):
        h = (ss - y) * k
        segs = [((oz - (b + 1)) * k, (oz - a) * k) for a, b in sr[y]]    # лицом влево: вперёд = влево
        rows.append(dict(y=round(h, 4), z=[[round(a, 4), round(b, 4)] for a, b in sorted(segs)]))
    out['views']['side'] = dict(top=st, sole=ss, origin=round(oz, 2), m_per_px=round(k, 6), rows=rows)

    # СПИНА: ширины для сверки анфаса (середина — по той же технике)
    bt, bs, br = figure(L, 'back')
    k = H / (bs - bt)
    skull = [br[y][0] for y in range(bt + 3, bt + 12) if len(br[y]) == 1]
    cxb = float(np.mean([(a + b + 1) / 2 for a, b in skull]))
    rows = []
    for y in range(bt, bs + 1):
        segs = [((a - cxb) * k, (b + 1 - cxb) * k) for a, b in br[y]]
        rows.append(dict(y=round((bs - y) * k, 4), x=[[round(a, 4), round(b, 4)] for a, b in segs]))
    out['views']['back'] = dict(top=bt, sole=bs, center=round(cxb, 2), m_per_px=round(k, 6), rows=rows)
    return L, out


def overlay(out, path):
    img = Image.open(SHEET).convert('RGB').crop((20, 80, 780, 570))
    d = ImageDraw.Draw(img)
    for view, col in (('front', (255, 80, 60)), ('side', (60, 200, 255)), ('back', (255, 200, 60))):
        v = out['views'][view]
        for i, r in enumerate(v['rows']):
            y = v['top'] + i - 80
            for seg in r.get('x', r.get('z', [])):
                if view == 'side':
                    xs = (v['origin'] - seg[1] / v['m_per_px'], v['origin'] - seg[0] / v['m_per_px'])
                else:
                    xs = (v['center'] + seg[0] / v['m_per_px'], v['center'] + seg[1] / v['m_per_px'])
                for x in xs:
                    d.point((x - 20, y), fill=col)
        c = v.get('center', v.get('origin'))
        d.line([(c - 20, 0), (c - 20, img.height)], fill=col)
    img = img.resize((img.width * 2, img.height * 2), Image.NEAREST)
    img.save(path)


CONTOURS = os.path.join(HERE, 'ref_contours.json')


def load_contours():
    """Контуры для графа: из `ref_contours.json`; нет файла — снять с листа."""
    if os.path.exists(CONTOURS):
        with open(CONTOURS, encoding='utf-8') as f:
            return json.load(f)
    return trace()[1]


def main():
    L, out = trace()
    os.makedirs(os.path.join(HERE, 'out'), exist_ok=True)
    p = CONTOURS
    with open(p, 'w', encoding='utf-8') as f:
        json.dump(out, f, ensure_ascii=False, separators=(',', ':'))
    overlay(out, os.path.join(HERE, 'out', 'ref-trace.png'))
    fv, sv = out['views']['front'], out['views']['side']
    print('рост %.3f м; анфас %d px (%.2f мм/px), профиль %d px; середина анфаса %.1f, начало профиля %.1f' %
          (out['height'], fv['sole'] - fv['top'], fv['m_per_px'] * 1000, sv['sole'] - sv['top'], fv['center'], sv['origin']))
    print('→', p)


if __name__ == '__main__':
    main()
