# СВЕРКА ФАЙЛОВ ПОЗЫ (21.10): прямая кинематика игры только из <вид>-poza.json и <вид>-sustavy.json против ориентиров цели.
#   python fk_proverka.py chelovek-skelet.json <...>-poza.json <...>-sustavy.json orientiry.json
# прямая кинематика игры ТОЛЬКО из файлов: D = рамка позы · рамка покоя К2⁻¹ (рамка: y — вдоль кости, z — z_axis);
# голова ребёнка = голова родителя + D_родителя·(покой ребёнка − покой родителя); корень — head из позы
import sys, json, numpy as np
sk, pz, sj, lm = [json.load(open(p, encoding='utf-8')) for p in sys.argv[1:5]]
K = {b['name']: b for b in sk['bones']}; P = {b['name']: b for b in pz['bones']}; S = {b['name']: b for b in sj['bones']}
def frame(b):
    h, t, z = (np.array(b[k], float) for k in ('head', 'tail', 'z_axis'))
    y = (t - h) / np.linalg.norm(t - h); x = np.cross(y, z); x /= np.linalg.norm(x); z2 = np.cross(x, y)
    return np.stack([x, y, z2], 1)
D = {n: frame(P[n]) @ frame(K[n]).T for n in P}
PH, PT = {}, {}
def fk(n):
    if n in PH: return
    par = S[n].get('parent') or ''
    if par: fk(par); PH[n] = PH[par] + D[par] @ (np.array(S[n]['head']) - np.array(S[par]['head']))
    else: PH[n] = np.array(P[n]['head'], float)
    PT[n] = PH[n] + D[n] @ (np.array(S[n]['tail']) - np.array(S[n]['head']))
for n in S: fk(n)
chk = [('бедро:head', 'бедро', 'h'), ('голень:head', 'голень', 'h'), ('голень:tail', 'голень', 't'), ('предплечье:head', 'предплечье', 'h'),
       ('предплечье:tail', 'предплечье', 't'), ('шея:head', 'шея', 'h'), ('голова:head', 'голова', 'h'), ('плечо:head', 'плечо', 'h')]
for k, n, w in chk:
    for s in ('', '.L'):
        kk = k.replace(':', s + ':') if s and k.split(':')[0] in ('бедро', 'голень', 'предплечье', 'плечо') else k
        nn = n + s if s and n in ('бедро', 'голень', 'предплечье', 'плечо') else n
        if kk not in lm or nn not in PH: continue
        v = PH[nn] if w == 'h' else PT[nn]
        print('%-18s игра %s  цель %s  Δ %.3f м' % (kk, np.round(v, 3), np.round(lm[kk], 3), np.linalg.norm(v - np.array(lm[kk]))))
print('низ стоп (хвост кости «стопа»): %.3f / %.3f м' % (PT['стопа'][2], PT['стопа.L'][2]))
