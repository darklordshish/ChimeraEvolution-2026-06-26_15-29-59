# -*- coding: utf-8 -*-
"""КОНТАКТНЫЙ ЛИСТ: пачка картинок одной сеткой с подписями.

Зачем инструмент. Отбор референсов идёт просмотром десятка кандидатов, и открывать их по одному —
это десять действий вместо одного. На маленькой картинке силуэт читается даже лучше: видно позу и
ракурс, не отвлекая деталями. Главное же — лист заставляет СМОТРЕТЬ: за один заход имя файла
разошлось с содержимым семь раз (череп лося оказался парой челюстей, "скелет" Албинуса — мышцами).
Подпись под клеткой — это то, что потом попадёт в паспорт, и сверить её можно только глазом.

Запуск:
    python tools/sheet.py species/hedgehog/ref/_cand
    python tools/sheet.py species/snake/ref/photo --out ../out/snake.jpg --cols 4
"""
import io, os, sys, argparse
from PIL import Image, ImageDraw

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
HERE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))   # Anatomy/
EXT = ('.jpg', '.jpeg', '.png', '.webp', '.bmp')
LIMIT = 2000        # потолок читаемости изображения


def build(src, out, cols, cell):
    files = sorted(f for f in os.listdir(src) if f.lower().endswith(EXT))
    if not files:
        print('пусто:', src); return None
    # Порядок номерной, а не лексикографический: h10 не должен вставать между h1 и h2
    def key(f):
        stem = os.path.splitext(f)[0]
        digits = ''.join(c for c in stem if c.isdigit())
        return (len(digits) == 0, int(digits) if digits else 0, stem)
    files.sort(key=key)

    rows = (len(files) + cols - 1) // cols
    pad, cap = 6, 16
    w, h = cols * (cell + pad) + pad, rows * (cell + pad + cap) + pad
    if max(w, h) > LIMIT:                      # ужимаем клетку, чтобы лист остался читаемым целиком
        k = LIMIT / max(w, h)
        cell = int(cell * k); pad = max(3, int(pad * k)); cap = max(10, int(cap * k))
        w, h = cols * (cell + pad) + pad, rows * (cell + pad + cap) + pad

    sheet = Image.new('RGB', (w, h), (24, 24, 28))
    d = ImageDraw.Draw(sheet)
    for i, f in enumerate(files):
        try:
            im = Image.open(os.path.join(src, f)).convert('RGB')
        except Exception as e:
            print('НЕ ОТКРЫЛСЯ', f, e); continue
        im.thumbnail((cell, cell), Image.LANCZOS)
        cx = pad + (i % cols) * (cell + pad)
        cy = pad + (i // cols) * (cell + pad + cap)
        sheet.paste(im, (cx + (cell - im.width) // 2, cy + (cell - im.height) // 2))
        d.text((cx + 2, cy + cell + 2), os.path.splitext(f)[0][:28], fill=(210, 210, 200))

    sheet.save(out, quality=90)
    print('ЛИСТ', out, '%dx%d' % sheet.size, '·', len(files), 'шт', '· клетка', cell)
    return out


if __name__ == '__main__':
    ap = argparse.ArgumentParser()
    ap.add_argument('src')
    ap.add_argument('--out', default=None)
    ap.add_argument('--cols', type=int, default=4)
    ap.add_argument('--cell', type=int, default=460)
    a = ap.parse_args()
    src = a.src if os.path.isabs(a.src) else os.path.join(HERE, a.src)
    out = a.out or os.path.join(src, '_sheet.jpg')
    build(src, out, a.cols, a.cell)
