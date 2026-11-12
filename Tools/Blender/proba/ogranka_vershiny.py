"""ВЕРШИНЫ И РЁБРА С ЛИСТА ОГРАНКИ (04.11, проба способа: геймдизайнер по r10 «не попал в районе груди и плеч»). С `огранка-рёбра.png` снимаются не области, а граф: тёмные линии → скелет → ломаные между развилками → упрощение Дугласа — Пекера; углы ломаных — кандидаты вершин, ломаные — рёбра. Короткие штрихи со свободным концом (соски, вставки) отсеиваются порогом длины. Ракурсы: анфас и сзади по половине листа, профиль. Выход — json в пикселях и кадр-наложение для проверки глазом.

  python ogranka_vershiny.py огранка-рёбра.png выход.json наложение.png [--rdp 2.5] [--min 14] [--porog 70]
"""
import sys, json
import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage as ndi
from skimage.morphology import skeletonize
from skimage.measure import approximate_polygon

a = sys.argv[1:]; SRC, OUT, KADR = a[:3]
def op(n, d): return type(d)(a[a.index(n) + 1]) if n in a else d
RDP, MINL, POR = op('--rdp', 2.5), op('--min', 14.0), op('--porog', 70)
g = np.asarray(Image.open(SRC).convert('L')).astype(int)
# коробки зоны груди, плеча и дельты: x0, x1, y0, y1 в пикселях листа
KOR = dict(front=(219, 372, 135, 295), back=(1095, 1233, 135, 295), profile=(488, 625, 150, 305), front_l=(66, 219, 135, 295), back_r=(1233, 1371, 135, 295))
SOS = [(i, j) for i in (-1, 0, 1) for j in (-1, 0, 1) if i or j]


def graf(k, x0, x1, y0, y1):
    s = g[y0:y1, x0:x1] < POR; sk = skeletonize(s); pts = {tuple(p) for p in np.argwhere(sk)}
    sos = {p: [(p[0] + i, p[1] + j) for i, j in SOS if (p[0] + i, p[1] + j) in pts] for p in pts}
    uz = {p for p in pts if len(sos[p]) != 2}
    # узлы-кляксы сливаются в один
    m = np.zeros(sk.shape, bool)
    for p in uz: m[p] = True
    L, n = ndi.label(m, structure=np.ones((3, 3))); cen = {}
    for i in range(1, n + 1):
        q = np.argwhere(L == i); c = q.mean(0); cen[i] = tuple(q[np.linalg.norm(q - c, axis=1).argmin()])
    uzel = {p: L[p] for p in uz}
    vis = set(); lom = []
    for p in uz:
        for q in sos[p]:
            if (p, q) in vis or q in uz and uzel[q] == uzel[p]: continue
            ch = [p, q]; vis.add((p, q)); pr, cu = p, q
            while cu not in uz:
                nx = [x for x in sos[cu] if x != pr]
                if not nx: break
                pr, cu = cu, nx[0]; ch.append(cu)
            vis.add((ch[-1], ch[-2]))
            if ch[-1] in uz: lom.append((uzel[p], uzel[ch[-1]], ch))
    # длины и отсев коротких висячих штрихов
    st = {}
    for a_, b_, ch in lom:
        st[a_] = st.get(a_, 0) + 1; st[b_] = st.get(b_, 0) + 1
    ost = []
    for a_, b_, ch in lom:
        A_ = np.array(ch, float); dl = np.linalg.norm(np.diff(A_, axis=0), axis=1).sum()
        if dl < MINL and (st[a_] == 1 or st[b_] == 1): continue
        if a_ == b_ and dl < MINL * 2: continue
        ost.append((a_, b_, ch))
    V = {}; E = []
    def uzv(i):
        p = cen[i]; V.setdefault(('u', i), (float(p[1] + x0), float(p[0] + y0))); return ('u', i)
    n_ = 0
    for a_, b_, ch in ost:
        ap = approximate_polygon(np.array([cen[a_]] + ch[1:-1] + [cen[b_]], float), RDP); pr = uzv(a_)
        for q in ap[1:-1]:
            n_ += 1; kl = ('l', n_); V[kl] = (float(q[1] + x0), float(q[0] + y0)); E.append((pr, kl)); pr = kl
        kn = uzv(b_)
        if pr != kn: E.append((pr, kn))
    nm = {k_: i for i, k_ in enumerate(V)}
    return dict(tochki=[list(V[k_]) for k_ in V], rebra=[[nm[x], nm[y]] for x, y in E], kor=[x0, x1, y0, y1], razvilok=sum(1 for k_ in V if k_[0] == 'u'), lomanyh=len(ost), otseyano=len(lom) - len(ost))


R = {k: graf(k, *v) for k, v in KOR.items()}
json.dump(R, open(OUT, 'w', encoding='utf-8'), ensure_ascii=False)
im = Image.open(SRC).convert('RGB'); d = ImageDraw.Draw(im)
for k, r in R.items():
    print('ВЕРШИНЫ %s: вершин %d (развилок %d), рёбер %d, ломаных %d, отсеяно %d' % (k, len(r['tochki']), r['razvilok'], len(r['rebra']), r['lomanyh'], r['otseyano']))
    for x, y in r['rebra']: d.line([tuple(r['tochki'][x]), tuple(r['tochki'][y])], fill=(230, 40, 40), width=1)
    for p in r['tochki']: d.ellipse([p[0] - 1.5, p[1] - 1.5, p[0] + 1.5, p[1] + 1.5], fill=(30, 90, 240))
kr = [im.crop((v[0] - 10, v[2] - 5, v[1] + 10, v[3] + 5)) for k_, v in KOR.items() if k_ in ('front', 'back', 'profile')]
W_ = sum(c.width for c in kr) * 2; o = Image.new('RGB', (W_, max(c.height for c in kr) * 2), 'white'); x = 0
for c in kr: o.paste(c.resize((c.width * 2, c.height * 2), Image.LANCZOS), (x, 0)); x += c.width * 2
o.save(KADR)
