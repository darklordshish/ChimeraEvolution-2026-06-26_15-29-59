# -*- coding: utf-8 -*-
"""ПОИСК И ЗАГРУЗКА РЕФЕРЕНСОВ С ВИКИСКЛАДА.

Зачем инструмент, а не «нашёл ссылку руками». Референсов нужно по три-четыре ракурса на вид и по
пять видов — это два десятка файлов, каждый со своей лицензией и своим разрешением. Руками это
делается один раз и потом не повторяется; скриптом — воспроизводимо, и в отчёт попадает лицензия,
без которой картинку нельзя держать в репозитории.

ЧТО ВАЖНО ПРИ ОТБОРЕ (проверено на волке):
  • нужен ПРОФИЛЬ, АНФАС и СВЕРХУ — сектор сечения это направление, с одного профиля его не снять;
  • поза референса ≠ нейтральная стойка: с монтированного скелета берут устройство, углы — с фото;
  • подпись «Canis lupus» не значит, что это волк: на пластине черепа однажды оказался мелкий канид.
    Отвергнутое кладём в `_other/` с причиной, чтобы второй раз не скачивать то же самое.

Запуск:
    python commons.py search "Alces alces skeleton" --limit 12
    python commons.py cat "Side views of Alces alces"
    python commons.py get "File:Xxx.jpg" species/moose/ref/skeleton/moose_skeleton_side.jpg
"""
import io, os, sys, json, argparse, urllib.parse, urllib.request

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
HERE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))   # Anatomy/
API = 'https://commons.wikimedia.org/w/api.php'
UA = 'CHIMERA-refs/1.0 (gamedev asset research; contact via repo)'


def _api(params):
    params = dict(params, format='json', formatversion='2')
    url = API + '?' + urllib.parse.urlencode(params)
    req = urllib.request.Request(url, headers={'User-Agent': UA})
    with urllib.request.urlopen(req, timeout=40) as r:
        return json.loads(r.read().decode('utf-8'))


def search(query, limit=12):
    """Поиск файлов. Печатает имя, размер в пикселях и лицензию — по ним и отбираем."""
    d = _api({'action': 'query', 'generator': 'search', 'gsrnamespace': '6',
              'gsrsearch': query, 'gsrlimit': str(limit),
              'prop': 'imageinfo', 'iiprop': 'url|size|extmetadata'})
    pages = (d.get('query') or {}).get('pages') or []
    if not pages:
        print('ничего не найдено:', query)
        return []
    out = []
    for p in pages:
        ii = (p.get('imageinfo') or [{}])[0]
        meta = ii.get('extmetadata') or {}
        lic = (meta.get('LicenseShortName') or {}).get('value', '?')
        w, h = ii.get('width', 0), ii.get('height', 0)
        # МЕЛКОЕ НЕ ГОДИТСЯ: контур снимается попиксельно, на 600 px он превращается в кашу
        mark = 'ok ' if min(w, h) >= 700 else 'МЕЛКО'
        print('%s %5dx%-5d %-22s %s' % (mark, w, h, lic[:22], p['title']))
        out.append((p['title'], ii.get('url', ''), w, h, lic))
    return out


def cat(name, limit=60):
    """Файлы КАТЕГОРИИ. Надёжнее полнотекстового поиска: тот затягивает сканы книг, где нужное слово
    встретилось в тексте, а категория — это ручная классификация, и «вид сбоку» в ней означает
    именно вид сбоку."""
    if not name.lower().startswith('category:'):
        name = 'Category:' + name
    d = _api({'action': 'query', 'generator': 'categorymembers', 'gcmtitle': name,
              'gcmtype': 'file', 'gcmlimit': str(limit),
              'prop': 'imageinfo', 'iiprop': 'url|size|extmetadata'})
    pages = (d.get('query') or {}).get('pages') or []
    if not pages:
        print('пустая или несуществующая категория:', name)
        return []
    out = []
    for p in sorted(pages, key=lambda q: -((q.get('imageinfo') or [{}])[0].get('width', 0))):
        ii = (p.get('imageinfo') or [{}])[0]
        meta = ii.get('extmetadata') or {}
        lic = (meta.get('LicenseShortName') or {}).get('value', '?')
        w, h = ii.get('width', 0), ii.get('height', 0)
        mark = 'ok ' if min(w, h) >= 700 else 'МЕЛКО'
        print('%s %5dx%-5d %-22s %s' % (mark, w, h, lic[:22], p['title']))
        out.append((p['title'], ii.get('url', ''), w, h, lic))
    return out


def get(title, dest, maxpx=2400):
    """Скачать файл по имени `File:...` в путь относительно `Anatomy/`.

    УМЕНЬШАЕМ ПРИ ЗАГРУЗКЕ. Оригиналы на Викискладе бывают по 5000 px и 6 МБ; двадцать таких файлов
    — это сотня мегабайт в репозитории навсегда. Контур снимается попиксельно, но 2400 px для этого
    с запасом: у волка, на котором метод отлажен, исходники 2000 px."""
    d = _api({'action': 'query', 'titles': title, 'prop': 'imageinfo',
              'iiprop': 'url|size|extmetadata'})
    pages = (d.get('query') or {}).get('pages') or []
    if not pages or 'imageinfo' not in pages[0]:
        print('НЕ НАЙДЕН:', title)
        return False
    ii = pages[0]['imageinfo'][0]
    meta = ii.get('extmetadata') or {}
    path = dest if os.path.isabs(dest) else os.path.join(HERE, dest)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    # Викисклад сам отдаёт уменьшенную копию — качать оригинал ради ресайза незачем.
    # SVG качаем ТОЛЬКО через миниатюру: оригинал это разметка, а не картинка, и всё дальше по
    # цепочке (сегментация, промер) на ней падает — молча, потому что файл-то скачался
    url = ii['url']
    is_svg = title.lower().endswith('.svg')
    if is_svg or (maxpx and max(ii.get('width', 0), ii.get('height', 0)) > maxpx):
        url = ii.get('thumburl') or url
        d2 = _api({'action': 'query', 'titles': title, 'prop': 'imageinfo',
                   'iiprop': 'url', 'iiurlwidth': str(maxpx)})
        p2 = (d2.get('query') or {}).get('pages') or []
        if p2 and p2[0].get('imageinfo'):
            url = p2[0]['imageinfo'][0].get('thumburl') or url
    req = urllib.request.Request(url, headers={'User-Agent': UA})
    with urllib.request.urlopen(req, timeout=90) as r, open(path, 'wb') as f:
        f.write(r.read())
    from PIL import Image
    try:
        with Image.open(path) as im:
            gotw, goth = im.size
    except Exception as e:
        os.remove(path)
        print('НЕ КАРТИНКА, удалён: %s (%s)' % (title, e))
        return False
    print('СКАЧАН %s  %dx%d  %s  автор: %s' % (
        os.path.relpath(path, HERE), gotw, goth,
        (meta.get('LicenseShortName') or {}).get('value', '?'),
        (meta.get('Artist') or {}).get('value', '?')[:70].replace('\n', ' ')))
    return True


if __name__ == '__main__':
    AP = argparse.ArgumentParser()
    sub = AP.add_subparsers(dest='cmd', required=True)
    s = sub.add_parser('search'); s.add_argument('query'); s.add_argument('--limit', type=int, default=12)
    c = sub.add_parser('cat'); c.add_argument('name'); c.add_argument('--limit', type=int, default=60)
    g = sub.add_parser('get'); g.add_argument('title'); g.add_argument('dest')
    g.add_argument('--max', type=int, default=2400, help='ограничение по большей стороне, px')
    A = AP.parse_args()
    if A.cmd == 'search':
        search(A.query, A.limit)
    elif A.cmd == 'cat':
        cat(A.name, A.limit)
    else:
        get(A.title, A.dest, A.max)
