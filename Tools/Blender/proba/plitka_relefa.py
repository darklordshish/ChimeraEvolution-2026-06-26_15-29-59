"""ПЛИТКА РЕЛЬЕФА ЧЕШУИ ЗМЕИ (10.11, спека `2026-11-10-kozha-organa-proekciej.md` §3.5: плитка рельефа один раз на донора). Форма — та же, что в объёме Codex 06.11 (`ChimeraEvolution-codex/Experiments/zmeya-relief-grid-2026-11-06/probe.py`): ромбическая сетка шаг 0.065 м, угол 0.62 рад, в ячейке продольный киль (0.25,0.25)–(0.75,0.75) высотой 12.5 мм, шесть плоских скатов к углам. Считается аналитически, а не снимается с меша: бесшовность точная, без привязки к телу.

Плитка — один период сетки: по горизонтали (ось x тела) 0.065/sin 0.62 = 0.1118 м, по вертикали (ось z тела) 0.065/cos 0.62 = 0.0798 м, в периоде две ячейки. Картинка квадратная 1024×1024, растянута на этот прямоугольник: масштаб по осям в `plitka.json`.

  python plitka_relefa.py папка_выхода [--n 1024]
Выход: `relef-vysota.png` (16 бит, 0 — кожа, 65535 — 12.5 мм), `relef-normali.png` (касательные нормали, x вправо, y вверх, OpenGL), `plitka.json`.
"""
import sys, os, json, math
import numpy as np
from PIL import Image

a = sys.argv[1:]; OUT = a[0]; N = int(a[a.index('--n') + 1]) if '--n' in a else 1024
STEP, ANG, H = .065, .62, .0125; S, C = math.sin(ANG), math.cos(ANG); TX, TZ = STEP / S, STEP / C
os.makedirs(OUT, exist_ok=True)
x = (np.arange(N) + .5) / N * TX; z = (np.arange(N) + .5) / N * TZ; X, Z = np.meshgrid(x, z[::-1])   # строка 0 — верх плитки
U, V = (S * X + C * Z) / STEP, (-S * X + C * Z) / STEP; fu, fv = U - np.floor(U), V - np.floor(V)


def vysota(fu, fv):
    """киль в координатах ячейки: шесть плоских треугольников (a,b,r0),(b,r1,r0),(b,c,r1),(c,d,r1),(d,r0,r1),(d,a,r0) — как у Codex"""
    P = np.stack([fu, fv], -1); h = np.zeros(fu.shape)
    a_, b_, c_, d_, r0, r1 = (np.array(q) for q in ((0, 0), (1, 0), (1, 1), (0, 1), (.25, .25), (.75, .75)))
    tri = [((a_, b_, r0), (0, 0, 1)), ((b_, r1, r0), (0, 1, 1)), ((b_, c_, r1), (0, 0, 1)), ((c_, d_, r1), (0, 0, 1)), ((d_, r0, r1), (0, 1, 1)), ((d_, a_, r0), (0, 0, 1))]
    for (p0, p1, p2), (h0, h1, h2) in tri:
        e1, e2, q = p1 - p0, p2 - p0, P - p0; den = e1[0] * e2[1] - e1[1] * e2[0]
        s = (q[..., 0] * e2[1] - q[..., 1] * e2[0]) / den; t = (e1[0] * q[..., 1] - e1[1] * q[..., 0]) / den
        m = (s >= -1e-9) & (t >= -1e-9) & (s + t <= 1 + 1e-9); h = np.where(m, h0 * (1 - s - t) + h1 * s + h2 * t, h)
    return np.clip(h, 0, 1) * H


Hm = vysota(fu, fv)
# нормали — разностями в метрах, с переходом через край плитки (бесшовно)
dx, dz = TX / N, TZ / N
gx = (np.roll(Hm, -1, 1) - np.roll(Hm, 1, 1)) / (2 * dx); gz = (np.roll(Hm, 1, 0) - np.roll(Hm, -1, 0)) / (2 * dz)
Nr = np.stack([-gx, -gz, np.ones_like(Hm)], -1); Nr /= np.linalg.norm(Nr, axis=-1, keepdims=True)
Image.fromarray((Hm / H * 65535).round().astype(np.uint16)).save(os.path.join(OUT, 'relef-vysota.png'))
Image.fromarray(((Nr * .5 + .5) * 255).round().astype(np.uint8)).save(os.path.join(OUT, 'relef-normali.png'))
shov = max(np.abs(Hm[:, 0] - Hm[:, -1]).max(), np.abs(Hm[0] - Hm[-1]).max()) * 1000
json.dump(dict(vid='Змея', istochnik='ChimeraEvolution-codex/Experiments/zmeya-relief-grid-2026-11-06/probe.py', shag_m=STEP, ugol_rad=ANG, vysota_m=H,
               plitka_m=dict(x=round(TX, 5), z=round(TZ, 5)), pikseli=N, yacheek_v_plitke=2, normali='касательные, OpenGL (+y вверх), x — ось x тела, y — ось z тела',
               vysota='16 бит: 0 — кожа, 65535 — 12.5 мм'), open(os.path.join(OUT, 'plitka.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
print('ПЛИТКА: %d×%d, период %.4f × %.4f м, перепад на краю (бесшовность) %.3f мм' % (N, N, TX, TZ, shov))
