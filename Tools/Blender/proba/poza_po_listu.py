"""ПОЗА СТУПЕНИ ПО ЛИСТУ (21.10). Файл позы `podgonka_v_poze.py --poza` снят с 3D-цели, а цель стоит не совсем как лист
(оборотень ст.2: корпус «таз → плечевой сустав» 33° от вертикали против ~27° у профиля листа, колено 77° против ~92°).
Ключ — форма в покое, поза — отдельным файлом (вариант Б), поэтому позу правят отдельно от формы.

Работает на цепи игры, без Blender: D кости = рамка позы · рамка покоя К2⁻¹; правка — поворот вокруг оси X мира через
сустав, добавленный к D кости, дети едут за ней (кроме `--derzhat`: их ориентация в мире остаётся); суставы покоя
(`sustavy`) НЕ меняются — длины цели те же; корень после правки сдвигается по высоте так, чтобы нижний хвост «стопы»
остался там, где был (на земле).

  python poza_po_listu.py скелет_К2.json поза.json суставы.json выход.json --povorot хребет:-6,голень:15,голень.L:15,стопа:-15,стопа.L:-15 [--derzhat таз]
Поворот — градусы: «+» кладёт кость вперёд (ось Z кости → −Y), «−» — назад. Печатает наклон корпуса и угол колена до и после.
"""
import sys, json, math
import numpy as np

a = sys.argv[1:]
sk_p, pz_p, sj_p, out_p = a[:4]
opt = {}
i = 4
while i < len(a):
    opt[a[i].lstrip('-')] = a[i + 1]; i += 2
POV = {kv.split(':')[0]: float(kv.split(':')[1]) for kv in opt.get('povorot', '').split(',') if kv}
DERZ = set(x for x in opt.get('derzhat', 'таз').split(',') if x)

sk, pz, sj = (json.load(open(p, encoding='utf-8')) for p in (sk_p, pz_p, sj_p))
K = {b['name']: b for b in sk['bones']}; P = {b['name']: b for b in pz['bones']}; S = {b['name']: b for b in sj['bones']}


def frame(b):
    h, t, z = (np.array(b[k], float) for k in ('head', 'tail', 'z_axis'))
    y = (t - h) / np.linalg.norm(t - h); x = np.cross(y, z); x /= np.linalg.norm(x); z2 = np.cross(x, y)
    return np.stack([x, y, z2], 1)


def rx(deg):
    c, s = math.cos(math.radians(deg)), math.sin(math.radians(deg))
    return np.array([[1, 0, 0], [0, c, -s], [0, s, c]])


D = {n: frame(P[n]) @ frame(K[n]).T for n in P}
par = {n: (S[n].get('parent') or '') for n in S}
order = []
def walk(n):
    order.append(n)
    for c in S:
        if par[c] == n: walk(c)
for n in S:
    if not par[n]: walk(n)


def fk(Dm, root_h):
    PH, PT = {}, {}
    for n in order:
        p = par[n]
        PH[n] = root_h.copy() if not p else PH[p] + Dm[p] @ (np.array(S[n]['head']) - np.array(S[p]['head']))
        PT[n] = PH[n] + Dm[n] @ (np.array(S[n]['tail']) - np.array(S[n]['head']))
    return PH, PT


def mery(PH, PT):
    v = PH['плечо'] - PH['бедро']; lean = math.degrees(math.atan2(-v[1], v[2]))
    a_ = PH['бедро'] - PH['голень']; b_ = PT['голень'] - PH['голень']
    knee = math.degrees(math.acos(np.dot(a_, b_) / np.linalg.norm(a_) / np.linalg.norm(b_)))
    return lean, knee, min(PT['стопа'][2], PT['стопа.L'][2])


root = [n for n in order if not par[n]][0]
R0 = np.array(P[root]['head'], float)
PH0, PT0 = fk(D, R0)
l0, k0, z0 = mery(PH0, PT0)
# правка: накопленный поворот A — у ребёнка от родителя, кроме удерживаемых
A, D2 = {}, {}
for n in order:
    p = par[n]
    base = np.eye(3) if (not p or n in DERZ) else A[p]
    A[n] = base @ rx(POV.get(n, 0.0)) if n in POV else base
    D2[n] = A[n] @ D[n]
PH, PT = fk(D2, R0)
dz = z0 - min(PT['стопа'][2], PT['стопа.L'][2])
PH, PT = fk(D2, R0 + np.array([0, 0, dz]))
l1, k1, z1 = mery(PH, PT)
print('ПОЗА: корпус таз→плечо %.0f° → %.0f°, колено %.0f° → %.0f°, низ стопы %.3f → %.3f м, корень по высоте %+.3f м' % (l0, l1, k0, k1, z0, z1, dz))
out = []
for b in pz['bones']:
    n = b['name']
    F = D2[n] @ frame(K[n])
    out.append(dict(b, head=[round(c, 5) for c in PH[n]], tail=[round(c, 5) for c in PT[n]], z_axis=[round(c, 5) for c in F[:, 2]]))
pz['bones'] = out
pz['note'] = pz.get('note', '') + '; правка по листу: ' + ', '.join('%s %+g°' % kv for kv in POV.items()) + ' (poza_po_listu.py)'
json.dump(pz, open(out_p, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
print('ПОЗА: готово →', out_p)
