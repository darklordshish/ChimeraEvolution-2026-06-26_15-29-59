# -*- coding: utf-8 -*-
"""КАДРЫ ДЛЯ РЕЦЕНЗИИ — один набор на вид, одна раскладка по ракурсам, чтобы проходы критика были сравнимы.

Конфиг вида — `Anatomy/species/<вид>/kritik.json`:
  species, graph, head, organs, cell — что и на какой клетке собирает стенд `Kadr.cs`;
  frames  {ракурс: [вид камеры, cx, cy, half]} — рамка в метрах; 3/4 снимать под углами образца (`yaw:<угол>:<наклон>`);
  mirror  — какие кадры отзеркалить, чтобы смотрели в ту же сторону, что главный профиль образца;
  sheet   — лист-образец (если он один); ref {имя: [x0, y0, x1, y1] — кроп листа | "путь" — отдельный файл образца};
  q       {ракурс: {view: "yaw:<угол>:<наклон>", panel: кроп листа | mask: "путь к ч/б маске", center, frame: [cx, cy, half]}}
          — сверка масок 3/4 с образцом. Маска-файл — для фото на живом фоне (делается `mask` из обвода по `setka`).
          center: "top" — по макушке (человек); "band:<от>:<до>" — по середине фигуры в поясе высот (зверь: по корпусу,
          уши, хвост и повёрнутая голова на снимке центровку не сбивают); "bbox" — по габариту;
  q_bands [[доля от, до, имя]] — пояса сверки масок по высоте (человек: голова/плечи/торс/кисть/ноги; зверь:
          голова/корпус/ноги); fit_bands — какие пояса решают подбор угла (`ugol`), по умолчанию все;
  роль    — чьё тело и откуда его видят, ракурсы по убыванию приоритета: идёт в бриф и решает спорные правки;
  решения — сознательные решения вида и заглушки, со ссылкой на источник: идут в бриф критика дословно.
Снимает редактор ЭТОЙ рабочей папки (`--project-path` — корень репо, где лежит скрипт). Строка стенда проверяется на КАЖДОМ
кадре: примитивы или «НЕТ БЛОКОВ» — съёмка останавливается, такой набор критику не отдают.

  python kadry.py конфиг.json ref   ПАПКА                  образец → ПАПКА/ref_*.png (нарезка листа ×2, файлы как есть)
  python kadry.py конфиг.json shoot ПРЕФИКС ПАПКА          кадры → ПАПКА/ПРЕФИКС_<ракурс>.png (обрезаны, зеркало по mirror)
  python kadry.py конфиг.json q     ПРЕФИКС ПАПКА [n]      3/4 на n фазах сетки (`faza.py`) → совпадение масок по поясам
                                                            (среднее, разброс) и наложение ПАПКА/ov-ПРЕФИКС-<ракурс>.png
  python kadry.py конфиг.json ugol  РАКУРС ПАПКА ОТ ДО ШАГ НАКЛОНЫ   подбор угла 3/4: перебор yaw от..до с шагом и наклонов
                                                            (через запятую) → лучшие пять по fit_bands; записать в q и frames
  python kadry.py - setka ФОТО ВЫХОД.png [ШАГ]              фото с пиксельной сеткой — снять по ней обвод силуэта
  python kadry.py - mask  ФОТО КОНТУР.json ВЫХОД.png        обвод [[x, y], …] в пикселях фото → ч/б маска (белое — фигура)
"""
import json
import os
import re
import shutil
import subprocess
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageOps

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '..', '..', '..'))
UNITY = shutil.which('unity') or 'unity'
H = 400                                          # высота маски 3/4 в пикселях


def unity(*args):
    r = subprocess.run([UNITY, 'command', '--project-path', ROOT] + list(args), capture_output=True, text=True,
                       encoding='utf-8', errors='replace')
    if '\ttrue\t' not in r.stdout:
        raise SystemExit('unity %s: %s %s' % (args[0], r.stdout[-400:], r.stderr[-400:]))
    return r.stdout


def guard(stats, where):
    """Стенд говорит, чем собрано тело. Примитивы или «НЕТ БЛОКОВ» — кадр врёт (каталог форм испорчен тестом или блока
    нет): съёмка останавливается."""
    if 'НЕТ БЛОКОВ' in stats or 'примитивов 0' not in stats:
        cleanup()
        raise SystemExit('%s: стенд собрал тело не так — %s. Почини сборку (скилл chimera-unity, MissingBlocks), потом снимай'
                         % (where, stats))


def shot(cfg, graph, view, cx, cy, half, size, out):
    r = unity('run_script', '--file', os.path.join(HERE, 'Kadr.cs'), '--entry', 'Kadr.Shot', '--args',
              json.dumps([cfg['species'], graph, cfg['head'], cfg['organs'], cfg['cell'], view, cx, cy, half], ensure_ascii=False))
    m = re.search(r'"result":"([^"]*)"', r)
    stats = m.group(1) if m else r
    guard(stats, os.path.basename(out))
    unity('capture_game_view', '--camera', 'СтендCam', '--width', str(size), '--height', str(size), '--save_path', 'Кадры/k.png')
    shutil.move(os.path.join(ROOT, 'Assets', 'Кадры', 'k.png'), out)
    return stats


def cleanup():
    shutil.rmtree(os.path.join(ROOT, 'Assets', 'Кадры'), ignore_errors=True)
    meta = os.path.join(ROOT, 'Assets', 'Кадры.meta')
    if os.path.exists(meta):
        os.remove(meta)


def figure(img, thr=55, pad=12):
    bb = img.convert('L').point(lambda v: 255 if v > thr else 0).getbbox()
    return img.crop((max(0, bb[0] - pad), max(0, bb[1] - pad), min(img.width, bb[2] + pad), min(img.height, bb[3] + pad)))


def cmd_ref(cfg, folder):
    os.makedirs(folder, exist_ok=True)
    sheet = Image.open(os.path.join(ROOT, cfg['sheet'])).convert('RGB') if cfg.get('sheet') else None
    if sheet is not None:
        sheet.save(os.path.join(folder, 'ref_00_sheet_full.png'))
    for name, src in cfg['ref'].items():
        if isinstance(src, str):                       # отдельный файл образца (фото, схема) — как есть, в PNG
            Image.open(os.path.join(ROOT, src)).convert('RGB').save(os.path.join(folder, name + '.png'))
            continue
        c = sheet.crop(src)
        c.resize((c.width * 2, c.height * 2), Image.LANCZOS).save(os.path.join(folder, name + '.png'))
    print('образец:', ', '.join(sorted(cfg['ref'])))


def cmd_shoot(cfg, prefix, folder):
    raw = os.path.join(folder, 'raw')
    os.makedirs(raw, exist_ok=True)
    for name, (view, cx, cy, half) in cfg['frames'].items():
        p = os.path.join(raw, '%s_%s.png' % (prefix, name))
        stats = shot(cfg, cfg['graph'], view, cx, cy, half, 1000, p)
        im = figure(Image.open(p).convert('RGB'))
        if name in cfg.get('mirror', []):
            im = ImageOps.mirror(im)
        im.save(os.path.join(folder, '%s_%s.png' % (prefix, name)))
    cleanup()
    print('стенд:', stats)
    print('кадры', prefix, '→', folder)


def mask_of(img, thr, center='top'):
    """Маска фигуры, приведённая к высоте H, и её середина по горизонтали (см. `center` в шапке)."""
    m = np.asarray(img.convert('L')) > thr
    ys, xs = np.where(m)
    m = m[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
    k = H / m.shape[0]
    m = np.asarray(Image.fromarray((m * 255).astype(np.uint8)).resize((max(1, int(m.shape[1] * k)), H), Image.NEAREST)) > 127
    if center == 'bbox':
        return m, m.shape[1] / 2
    if center.startswith('band:'):
        a, b = (float(v) for v in center.split(':')[1:3])
        cols = np.where(m[int(a * H):int(b * H)].any(0))[0]
        return m, (cols.min() + cols.max()) / 2
    top = np.where(m[:int(0.08 * H)].any(0))[0]
    return m, (top.min() + top.max()) / 2


def ref_mask(cfg, q):
    center = q.get('center', 'top')
    if 'mask' in q:                                    # обвод фигуры с фото: белое — фигура
        return mask_of(Image.open(os.path.join(ROOT, q['mask'])), 127, center)
    sheet = Image.open(os.path.join(ROOT, cfg['sheet'])).convert('RGB')
    return mask_of(sheet.crop(q['panel']), q.get('thr', 62), center)


def place(m, c, W=400):
    A = np.zeros((H, W), bool)
    o = int(W / 2 - c)
    src = m[:, max(0, -o):max(0, -o) + W - max(0, o)]
    A[:, max(0, o):max(0, o) + src.shape[1]] = src
    return A


def iou(a, b, rows):
    a, b = a[rows], b[rows]
    return (a & b).sum() / max(1, (a | b).sum())


def overlay(ours, rm, rc, out, center='top', W=400):
    img = ours.convert('RGB')
    m = np.asarray(img.convert('L')) > 55
    ys, xs = np.where(m)
    img = img.crop((xs.min(), ys.min(), xs.max() + 1, ys.max() + 1))
    img = img.resize((int(img.width * H / img.height), H), Image.LANCZOS)
    _, oc = mask_of(ours, 55, center)
    can = Image.new('RGB', (W, H), (38, 40, 46))
    can.paste(img, (int(W / 2 - oc), 0))
    d = ImageDraw.Draw(can)
    for y in range(H):
        row = rm[y]
        for x in np.where(row[1:] != row[:-1])[0]:
            d.point((x - rc + W / 2, y), fill=(255, 60, 40))
    can.resize((W * 2, H * 2), Image.NEAREST).save(out)


def frame_of(cfg, q):
    f = cfg['frames'].get('front') or next(iter(cfg['frames'].values()))
    return q.get('frame', [0, f[2], f[3]])


def cmd_q(cfg, prefix, folder, n):
    sys.path.insert(0, HERE)
    import faza
    raw = os.path.join(folder, 'raw')
    os.makedirs(raw, exist_ok=True)
    doc = json.load(open(os.path.join(ROOT, cfg['graph']), encoding='utf-8'))
    bands = cfg['q_bands']
    for view, q in cfg['q'].items():
        center = q.get('center', 'top')
        rm, rc = ref_mask(cfg, q)
        R = place(rm, rc)
        fx, fy, fh = frame_of(cfg, q)
        acc = [[] for _ in bands]
        for k in range(n):
            g = cfg['graph']
            if n > 1:
                g = os.path.join(raw, 'faza-%d.json' % k)
                json.dump(faza.anchored(doc, k, n, cfg['cell']), open(g, 'w', encoding='utf-8'), ensure_ascii=False)
            p = os.path.join(raw, '%s_%s_f%d.png' % (prefix, view, k))
            shot(cfg, g, q['view'], fx, fy, fh, 600, p)
            om, oc = mask_of(Image.open(p), 55, center)
            O = place(om, oc)
            for i, (a, b, _) in enumerate(bands):
                acc[i].append(iou(R, O, slice(int(a * H), int(b * H))))
            if k == 0:
                overlay(Image.open(p), rm, rc, os.path.join(folder, 'ov-%s-%s.png' % (prefix, view)), center)
        print('%s %-8s %s' % (prefix, view, '  '.join('%s %.3f (%.3f..%.3f)' % (bands[i][2], sum(v) / n, min(v), max(v))
                                                    for i, v in enumerate(acc))))
    cleanup()


def cmd_ugol(cfg, view, folder, y0, y1, dy, pitches):
    """Подбор угла 3/4: наш кадр под каждым (yaw, наклон) против маски образца; решают пояса `fit_bands`. Угол подбирается
    один раз на образец и записывается в конфиг — и в `q`, и в кадр критика в `frames`."""
    q = cfg['q'][view]
    center = q.get('center', 'top')
    rm, rc = ref_mask(cfg, q)
    R = place(rm, rc)
    fx, fy, fh = frame_of(cfg, q)
    bands = [b for b in cfg['q_bands'] if b[2] in q.get('fit_bands', cfg.get('fit_bands', [b[2] for b in cfg['q_bands']]))]
    raw = os.path.join(folder, 'raw')
    os.makedirs(raw, exist_ok=True)
    res = []
    yaw = y0
    while (yaw <= y1 + 1e-9) if dy > 0 else (yaw >= y1 - 1e-9):
        for pt in pitches:
            v = 'yaw:%g:%g' % (yaw, pt)
            p = os.path.join(raw, 'ugol_%s_%g_%g.png' % (view, yaw, pt))
            shot(cfg, cfg['graph'], v, fx, fy, fh, 600, p)
            om, oc = mask_of(Image.open(p), 55, center)
            O = place(om, oc)
            s = sum(iou(R, O, slice(int(a * H), int(b * H))) for a, b, _ in bands) / len(bands)
            res.append((s, v))
        yaw += dy
    cleanup()
    for s, v in sorted(res, reverse=True)[:5]:
        print('%-16s %.3f' % (v, s))


def cmd_setka(photo, out, step=50):
    """Фото с пиксельной сеткой: подписи каждые 2 шага — по ним снимается обвод силуэта в координатах самого фото."""
    im = Image.open(photo).convert('RGB')
    d = ImageDraw.Draw(im, 'RGBA')
    for x in range(0, im.width, step):
        d.line([(x, 0), (x, im.height)], fill=(255, 60, 40, 170 if x % (2 * step) == 0 else 70))
        if x % (2 * step) == 0:
            d.rectangle([x + 1, 1, x + 38, 14], fill=(0, 0, 0, 180)); d.text((x + 3, 2), str(x), fill=(255, 220, 120))
    for y in range(0, im.height, step):
        d.line([(0, y), (im.width, y)], fill=(40, 200, 255, 170 if y % (2 * step) == 0 else 70))
        if y % (2 * step) == 0:
            d.rectangle([1, y + 1, 40, y + 14], fill=(0, 0, 0, 180)); d.text((3, y + 2), str(y), fill=(120, 220, 255))
    im.save(out)
    print(out, im.size)


def cmd_mask(photo, contour, out):
    """Обвод силуэта (многоугольник в пикселях фото, 30–80 точек по часовой) → ч/б маска размера фото. Проверять
    наложением: маска поверх фото должна лечь по контуру зверя, а не по шерсти-ореолу и не по траве."""
    im = Image.open(photo)
    pts = [tuple(p) for p in json.load(open(contour, encoding='utf-8'))]
    m = Image.new('L', im.size, 0)
    ImageDraw.Draw(m).polygon(pts, fill=255)
    m.save(out)
    check = Image.blend(im.convert('RGB'), Image.merge('RGB', (m, Image.new('L', im.size, 0), Image.new('L', im.size, 0))), 0.35)
    check.save(os.path.splitext(out)[0] + '-проверка.png')
    print(out, 'и', os.path.splitext(out)[0] + '-проверка.png')


if __name__ == '__main__':
    if hasattr(sys.stdout, 'reconfigure'):
        sys.stdout.reconfigure(encoding='utf-8')
    what = sys.argv[2]
    if what == 'setka':
        cmd_setka(sys.argv[3], sys.argv[4], int(sys.argv[5]) if len(sys.argv) > 5 else 50)
        sys.exit()
    if what == 'mask':
        cmd_mask(sys.argv[3], sys.argv[4], sys.argv[5])
        sys.exit()
    cfg = json.load(open(sys.argv[1], encoding='utf-8'))
    if what == 'ref':
        cmd_ref(cfg, sys.argv[3])
    elif what == 'shoot':
        cmd_shoot(cfg, sys.argv[3], sys.argv[4])
    elif what == 'q':
        cmd_q(cfg, sys.argv[3], sys.argv[4], int(sys.argv[5]) if len(sys.argv) > 5 else 1)
    elif what == 'ugol':
        cmd_ugol(cfg, sys.argv[3], sys.argv[4], float(sys.argv[5]), float(sys.argv[6]), float(sys.argv[7]),
                 [float(v) for v in sys.argv[8].split(',')])
    else:
        raise SystemExit(__doc__)
