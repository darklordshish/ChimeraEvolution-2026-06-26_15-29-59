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
import io, os, sys, json, socket, hashlib, argparse, urllib.parse, urllib.request

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
HERE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))   # Anatomy/
API = 'https://commons.wikimedia.org/w/api.php'
UA = 'CHIMERA-refs/1.0 (gamedev asset research; contact via repo)'


# ── ОБХОД НЕДОСТУПНОГО ДАТА-ЦЕНТРА ────────────────────────────────────────────────────────────────
# DNS Викимедиа отдаёт ближайший анонс text-lb, и он может оказаться недоступен с этой машины: у нас
# 185.15.59.224 (esams) не отвечает на 443 вовсе, при живом файловом хосте 185.15.59.240. Адрес
# один на ВСЕ текстовые узлы — commons, api, wikipedia, — поэтому «попробовать другой домен» не
# помогает: он тот же самый IP.
#     Дата-центров у Викимедиа несколько, и остальные отвечают. Подменяем разрешение имени на живой
# анонс: SNI и проверка сертификата остаются настоящими, меняется только маршрут.
TEXT_LB = ['208.80.154.224', '185.15.58.224', '103.102.166.224', '198.35.26.96']
_pinned = {'ip': None}


def _reachable(ip, port=443, timeout=6):
    s = socket.socket(); s.settimeout(timeout)
    try:
        s.connect((ip, port)); return True
    except Exception:
        return False
    finally:
        s.close()


def _pin_datacenter():
    """Найти отвечающий анонс и прибить к нему разрешение имён текстовых узлов."""
    if _pinned['ip']:
        return _pinned['ip']
    for ip in TEXT_LB:
        if _reachable(ip):
            _pinned['ip'] = ip
            _orig = socket.getaddrinfo

            def _patched(host, port, *a, **kw):
                if isinstance(host, str) and host.endswith('wikimedia.org') and not host.startswith('upload.'):
                    return _orig(ip, port, *a, **kw)
                return _orig(host, port, *a, **kw)

            socket.getaddrinfo = _patched
            print('[сеть] ближайший узел Викимедиа недоступен, работаю через %s' % ip)
            return ip
    return None


def _open(url, timeout=45, tries=4):
    """Открыть с повтором. Связь до Викисклада рвётся через раз, и без повтора каждый запрос
    становится лотереей: половина поиска отваливается на середине, а причина выглядит как
    «ничего не найдено». Пауза растёт, чтобы не долбить недоступный узел."""
    import time
    last = None
    for i in range(tries):
        try:
            req = urllib.request.Request(url, headers={'User-Agent': UA})
            return urllib.request.urlopen(req, timeout=timeout).read()
        except Exception as e:
            last = e
            if i == 0 and 'upload.' not in url:
                _pin_datacenter()      # первая же осечка — пробуем другой дата-центр
            if i < tries - 1:
                time.sleep(1 + 2 * i)
    raise last


def _api(params):
    params = dict(params, format='json', formatversion='2')
    return json.loads(_open(API + '?' + urllib.parse.urlencode(params)).decode('utf-8'))


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


def upload_url(title):
    """Прямой путь к файлу на `upload.wikimedia.org`, вычисленный из имени.

    ЗАЧЕМ. API-хост `commons.wikimedia.org` бывает недоступен (у нас — заблокирован по TCP 443,
    при живом DNS и работающем файловом хосте: 0 из 4 против 4 из 4). Путь к самому файлу от API
    не зависит и считается арифметикой: первый и первые два символа MD5 имени с подчёркиваниями.
    Значит скачать по известному имени можно всегда, а недоступен только ПОИСК."""
    n = title.split(':', 1)[-1].replace(' ', '_')
    h = hashlib.md5(n.encode('utf-8')).hexdigest()
    return 'https://upload.wikimedia.org/wikipedia/commons/%s/%s/%s' % (h[0], h[:2], urllib.parse.quote(n))


def direct(title, dest, maxpx=2000):
    """Скачать в обход API и уменьшить локально. Лицензию при этом НЕ УЗНАТЬ — её надо
    вписать в паспорт вида руками, со страницы файла."""
    path = dest if os.path.isabs(dest) else os.path.join(HERE, dest)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, 'wb') as f:
        f.write(_open(upload_url(title), timeout=120))
    from PIL import Image
    try:
        im = Image.open(path)
    except Exception as e:
        os.remove(path)
        print('НЕ КАРТИНКА, удалён: %s (%s)' % (title, e))
        return False
    w, h = im.size
    if maxpx and max(w, h) > maxpx:
        k = maxpx / float(max(w, h))
        im = im.convert('RGBA' if im.mode == 'RGBA' else 'RGB')
        im = im.resize((int(w * k), int(h * k)), Image.LANCZOS)
        im.save(path, quality=90) if path.lower().endswith(('.jpg', '.jpeg')) else im.save(path)
        w, h = im.size
    print('СКАЧАН %s  %dx%d  (лицензию вписать руками)' % (os.path.relpath(path, HERE), w, h))
    return True


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
    with open(path, 'wb') as f:
        f.write(_open(url, timeout=120))
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
    dd = sub.add_parser('direct'); dd.add_argument('title'); dd.add_argument('dest')
    dd.add_argument('--max', type=int, default=2000)
    g.add_argument('--max', type=int, default=2400, help='ограничение по большей стороне, px')
    A = AP.parse_args()
    if A.cmd == 'search':
        search(A.query, A.limit)
    elif A.cmd == 'cat':
        cat(A.name, A.limit)
    elif A.cmd == 'direct':
        direct(A.title, A.dest, A.max)
    else:
        get(A.title, A.dest, A.max)
