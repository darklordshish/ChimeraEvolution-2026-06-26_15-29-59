"""ШАГ ПО ГЛУБИНЕ (29.10, критик новой ст.2 п.3: «в профиль обе ноги и обе руки совпадают — читается одна нога и одна рука; разнос
только по глубине, без разведения вбок»). Поза ступени из файла: скакательный сустав каждой ноги сдвигается по оси вперёд-назад на
`dy_прав` / `dy_лев` м на той же высоте — двухзвенная обратная кинематика в плоскости y–z (колено по ту же сторону, что было), бедро
и голень поворачиваются вокруг оси X (`poza_po_listu.py`), стопа держит ориентацию — обе стопы остаются на земле (поворот одного
бедра поднимал стопу на 13 см, и анфас терял ногу: 0.817 → 0.76). `доп` — добавочные повороты в формате poza_po_listu (руки).

  python poza_shag.py поза.json суставы.json выход.json dy_прав dy_лев ["плечо.L:-12,плечо:8"]
"""
import json, subprocess, sys, math
import numpy as np
PZ, SJ, out, dyR, dyL = sys.argv[1], sys.argv[2], sys.argv[3], float(sys.argv[4]), float(sys.argv[5])
extra = sys.argv[6] if len(sys.argv) > 6 else ''
SK = 'Docs/models/rig/chelovek-skelet.json'; PY = sys.executable
B = {b['name']: b for b in json.load(open(PZ, encoding='utf-8'))['bones']}
def yz(p): return np.array([p[1], p[2]])
def ang(v): return math.degrees(math.atan2(v[0], -v[1]))   # угол от «вниз», + к +y (назад)
rot = []
for s, dy in (('', dyR), ('.L', dyL)):
    H, K, A = yz(B['бедро' + s]['head']), yz(B['голень' + s]['head']), yz(B['стопа' + s]['head'])
    L1, L2 = np.linalg.norm(K - H), np.linalg.norm(A - K)
    A2 = A + np.array([dy, 0.0]); d = np.linalg.norm(A2 - H)
    # колено — по ту же сторону от линии бедро–скакательный, что и было
    def koleno(Ap):
        dd = np.linalg.norm(Ap - H); a_ = (L1 ** 2 - L2 ** 2 + dd ** 2) / (2 * dd); h_ = math.sqrt(max(L1 ** 2 - a_ ** 2, 0))
        u = (Ap - H) / dd; n = np.array([-u[1], u[0]]); return H + u * a_ + n * h_, H + u * a_ - n * h_
    k1, k2 = koleno(A); sgn = 1 if np.linalg.norm(k1 - K) < np.linalg.norm(k2 - K) else -1
    K2 = koleno(A2)[0 if sgn == 1 else 1]
    d1 = ang(K2 - H) - ang(K - H); d2 = ang(A2 - K2) - ang(A - K)
    rot.append((s, d1, d2))
def run(sign):
    r = ','.join('бедро%s:%.2f,голень%s:%.2f' % (s, sign * d1, s, sign * (d2 - d1)) for s, d1, d2 in rot)
    if extra: r += ',' + extra
    subprocess.run([PY, 'Tools/Blender/proba/poza_po_listu.py', SK, PZ, SJ, out, '--povorot', r, '--derzhat', 'таз,стопа,стопа.L'], capture_output=True)
    O = {b['name']: b for b in json.load(open(out, encoding='utf-8'))['bones']}
    err = sum(abs(O['стопа' + s]['head'][1] - (B['стопа' + s]['head'][1] + dy)) + abs(O['стопа' + s]['head'][2] - B['стопа' + s]['head'][2]) for s, dy in (('', dyR), ('.L', dyL)))
    return err, r, O
e1 = run(1); e2 = run(-1); err, r, O = min(e1, e2, key=lambda t: t[0])
if err == e1[0]: run(1)
print('ШАГ: %s; ошибка %.3f м; стопы z %.3f / %.3f' % (r, err, min(O['стопа']['tail'][2], O['стопа']['head'][2]), min(O['стопа.L']['tail'][2], O['стопа.L']['head'][2])))
