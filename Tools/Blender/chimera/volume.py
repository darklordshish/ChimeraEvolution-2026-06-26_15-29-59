# -*- coding: utf-8 -*-
"""ОБЪЁМ ВИДА: болван из костей, превращённый в СПЛОШНОЕ ТЕЛО на воксельной сетке.

ЗАЧЕМ СЕТКА, А НЕ ЛУЧ ПО ПРУТЬЯМ. Кости и мышцы — решётка с пустотами: внутренностей у них нет,
грудная и брюшная полости пустые. Для луча «внутри» тогда означает «попал в прут», и обмер выходит
бессмысленным: диагональ вдоль ребра живёт долго, прямой бок выходит сразу, и сечение получается
ножом — при обмере волка вышла ширина 0.012 м при высоте 0.364. Замкнуть можно ОБЪЁМ, а не луч,
поэтому тело сперва отливается в сетку, а мерится уже отливка.

ДВЕ ОПЕРАЦИИ, И У НИХ РАЗНЫЕ РАБОТЫ — путать их нельзя:
  * `binary_closing` радиусом R — затягивает ЩЕЛИ шириной до R: промежутки между рёбрами, между
    пястными костями. Форму толще не делает, потому что после раздутия идёт сжатие;
  * `binary_fill_holes` — заливает ЗАМКНУТЫЕ ПОЛОСТИ: грудную клетку, брюшную, мозговую коробку.
    Полость в четверть метра никаким R не затянешь, а замкнута она честно — рёбра смыкаются с
    грудиной, брюшная стенка с тазом. Это и есть «внутренности», которых в скелете нет.

Порядок обязателен: сперва замыкание (иначе полость течёт через щель между рёбрами и не считается
замкнутой — заливка тогда не сработает), потом заливка.
"""
import math
import numpy as np
from scipy import ndimage as nd

CELL = 0.008          # сторона вокселя, м. Мельче самой тонкой детали (ухо 10 мм)


def _profile_np(kind, t):
    if kind == 'long':
        w = 0.17
        return 0.52 + 0.48 * np.maximum(np.exp(-(t / w) ** 2), np.exp(-((1.0 - t) / w) ** 2))
    if kind == 'blade':
        return 0.35 + 0.65 * (1.0 - t) ** 0.6
    if kind == 'spindle':
        return 0.30 + 0.70 * np.sin(np.pi * np.clip(t, 0, 1) ** 0.85) ** 0.8
    if kind == 'egg':
        return np.sqrt(np.maximum(0.05, 1.0 - (2.0 * t - 1.0) ** 2 * 0.55))
    if kind == 'dome':
        return np.sqrt(np.maximum(0.05, 1.0 - t ** 3 * 0.92))
    return np.ones_like(t)


class Volume:
    """Отливка тела: у каждой ячейки есть хозяин — слот, чья кость к ней ближе.

    Хозяин решается ТЕМ ЖЕ правилом, что в `BoneMesher` («модуль вершины по хозяину ячейки»), иначе
    детектор проверял бы себя, а не игру. Парная конечность держит левый и правый экземпляры
    РАЗНЫМИ хозяевами: мерится правая, а левая при этом честно её ограничивает — пока обе считались
    одним слотом, центр тяжести «Рук» садился на среднюю линию, между лап."""

    def __init__(self, bones, placed, surface_of, paired, layers=(0, 1, 2), surface_bone=None):
        items = [(b, placed[b.name][0], placed[b.name][1])
                 for b in bones if b.layer in layers]
        # список экземпляров: (имя-хозяина, кость, положение, поворот, зеркалить ли точку)
        inst = []
        surface_bone = surface_bone or {}
        for b, pos, rot in items:
            own = surface_bone.get(b.name, surface_of.get(b.socket, b.socket))
            inst.append((own, b, pos, rot, False))
            if b.mirrorX:
                inst.append((own + '←' if own in paired else own, b, pos, rot, True))
        self.names = sorted({i[0] for i in inst})
        ids = {n: k for k, n in enumerate(self.names)}

        lo = [1e9] * 3
        hi = [-1e9] * 3
        for own, b, pos, rot, mir in inst:
            R = max(b.r0, b.r1) * max(b.section, b.depth) + 0.02
            for s in (-0.05, 1.05):
                c = [pos[k] + rot[k][1] * b.length * s for k in range(3)]
                if mir:
                    c[0] = -c[0]      # ЗЕРКАЛЬНАЯ ПОЛОВИНА ТОЖЕ В КОРОБКЕ. Без этого сетка кроится
                for k in range(3):    # по авторским костям, левый бок обрезается, и грудная полость
                    lo[k] = min(lo[k], c[k] - R)   # перестаёт быть замкнутой — заливка не срабатывает
                    hi[k] = max(hi[k], c[k] + R)
        self.lo = tuple(lo)
        self.dim = tuple(int(math.ceil((hi[k] - lo[k]) / CELL)) + 2 for k in range(3))

        owner = np.full(self.dim, -1, np.int8)
        best = np.full(self.dim, 1e9, np.float32)
        for own, b, pos, rot, mir in inst:
            if getattr(b, 'shell', None):
                self._stamp_shell(owner, best, ids[own], b, pos, rot, mir)
            else:
                self._stamp(owner, best, ids[own], b, pos, rot, mir)
        self.owner = owner

    def _stamp(self, owner, best, oid, b, pos, rot, mir):
        """Впечатать кость в сетку. Тот же тапер-капсульный объём с эллиптическим сечением."""
        if b.length < 1e-9:
            return
        ax = np.array([rot[0][0], rot[1][0], rot[2][0]])
        ay = np.array([rot[0][1], rot[1][1], rot[2][1]])
        az = np.array([rot[0][2], rot[1][2], rot[2][2]])
        P = np.array(pos, float)
        R = max(b.r0, b.r1) * max(b.section, b.depth) + 0.01
        cs = [P + ay * b.length * s for s in (-0.05, 1.05)]
        lo = np.min(cs, 0) - R
        hi = np.max(cs, 0) + R
        if mir:                       # зеркальный экземпляр живёт в отражённой половине
            lo, hi = np.array([-hi[0], lo[1], lo[2]]), np.array([-lo[0], hi[1], hi[2]])
        i0 = np.maximum(((lo - self.lo) / CELL).astype(int), 0)
        i1 = np.minimum(((hi - self.lo) / CELL).astype(int) + 2, self.dim)
        if np.any(i1 <= i0):
            return
        g = np.meshgrid(*[np.arange(i0[k], i1[k]) for k in range(3)], indexing='ij')
        w = [self.lo[k] + g[k] * CELL for k in range(3)]
        if mir:
            w[0] = -w[0]
        v = [w[k] - P[k] for k in range(3)]
        t = v[0] * ay[0] + v[1] * ay[1] + v[2] * ay[2]
        u = t / b.length
        ok = (u >= -0.05) & (u <= 1.05)
        uc = np.clip(u, 0.0, 1.0)
        r = (b.r0 + (b.r1 - b.r0) * uc) * _profile_np(getattr(b, 'profile', 'long'), uc)
        x = (v[0] * ax[0] + v[1] * ax[1] + v[2] * ax[2]) / max(1e-9, b.section)
        z = (v[0] * az[0] + v[1] * az[1] + v[2] * az[2]) / max(1e-9, b.depth)
        ok &= (x * x + z * z) <= r * r
        if not ok.any():
            return
        # хозяин — у кого ось ближе; расстояние до отрезка оси
        tc = np.clip(t, 0.0, b.length)
        d = np.sqrt(sum((v[k] - tc * ay[k]) ** 2 for k in range(3)))
        sl = tuple(slice(i0[k], i1[k]) for k in range(3))
        win = ok & (d < best[sl])
        best[sl] = np.where(win, d, best[sl])
        owner[sl] = np.where(win, oid, owner[sl])

    def _stamp_shell(self, owner, best, oid, b, pos, rot, mir, power=2.05):
        """Впечатать ОБОЛОЧКУ ПО СТАНЦИЯМ — ту же, что рисует `mesh.shell_geo`.

        БЕЗ ЭТОГО ОТЛИВКА НЕ ВИДИТ ЧЕРЕПА. В таблице костей `череп` — стержень длиной 0.386 при
        радиусе 0.030, а настоящая форма (свод, заглазничный перехват, морда) живёт станциями,
        снятыми попиксельно с четырёх аспектов пластины. Обмерщик щупал стержень и возвращал голову
        коробкой: на рендере анфас она читалась плитой с плоскими пластинами ушей.

        Сечение — СУПЕРЭЛЛИПС степени 2.05, верх и низ от оси РАЗДЕЛЬНО: череп несимметричен по
        высоте, а бока у него плоские, и чистый эллипс делает из свода дирижабль. Формула повторена
        за `shell_geo` буква в букву — детектор обязан считать ту же форму, что рисуется."""
        st = b.shell
        L = b.length
        ax = np.array([rot[0][0], rot[1][0], rot[2][0]])
        ay = np.array([rot[0][1], rot[1][1], rot[2][1]])
        up = -np.array([rot[0][2], rot[1][2], rot[2][2]])   # у кости вперёд локальная Z смотрит ВНИЗ
        P = np.array(pos, float)
        T = np.array([s[0] for s in st])
        RT = np.array([s[1] for s in st]) * L
        RB = -np.array([s[2] for s in st]) * L
        HW = np.array([s[3] for s in st]) * L
        R = max(RT.max(), RB.max(), HW.max()) + 0.01
        cs = [P, P + ay * L]
        lo = np.min(cs, 0) - R
        hi = np.max(cs, 0) + R
        if mir:
            lo, hi = np.array([-hi[0], lo[1], lo[2]]), np.array([-lo[0], hi[1], hi[2]])
        i0 = np.maximum(((lo - self.lo) / CELL).astype(int), 0)
        i1 = np.minimum(((hi - self.lo) / CELL).astype(int) + 2, self.dim)
        if np.any(i1 <= i0):
            return
        g = np.meshgrid(*[np.arange(i0[k], i1[k]) for k in range(3)], indexing='ij')
        w = [self.lo[k] + g[k] * CELL for k in range(3)]
        if mir:
            w[0] = -w[0]
        v = [w[k] - P[k] for k in range(3)]
        t = (v[0] * ay[0] + v[1] * ay[1] + v[2] * ay[2]) / max(1e-9, L)
        ok = (t >= T[0]) & (t <= T[-1])
        tc = np.clip(t, T[0], T[-1])
        hw = np.interp(tc, T, HW)
        hu = np.interp(tc, T, RT)
        hd = np.interp(tc, T, RB)
        x = v[0] * ax[0] + v[1] * ax[1] + v[2] * ax[2]
        u = v[0] * up[0] + v[1] * up[1] + v[2] * up[2]
        h = np.where(u >= 0, hu, hd)
        e = 2.0 / power
        f = (np.abs(x) / np.maximum(hw, 1e-6)) ** power + (np.abs(u) / np.maximum(h, 1e-6)) ** power
        ok &= f <= 1.0
        if not ok.any():
            return
        d = np.sqrt(np.maximum(sum((v[k] - np.clip(t, 0, 1) * L * ay[k]) ** 2 for k in range(3)), 0))
        sl = tuple(slice(i0[k], i1[k]) for k in range(3))
        win = ok & (d < best[sl])
        best[sl] = np.where(win, d, best[sl])
        owner[sl] = np.where(win, oid, owner[sl])

    def solid(self, slot, pad):
        """Сплошное тело слота: щели затянуты, полости залиты — В ТОМ ЧИСЛЕ СКВОЗНЫЕ.

        ЗАЛИВКА В ТРЁХ ИЗМЕРЕНИЯХ ГРУДНУЮ КЛЕТКУ НЕ БЕРЁТ, и это не мелочь. `binary_fill_holes`
        заливает только то, что не связано с внешностью, а грудная полость открыта спереди в шею и
        сзади в брюхо: это ТРУБА, а не пузырь, и дырой она не считается. Пока обмер брал последнюю
        точку внутри, он проходил полость насквозь и подмены не было видно; стоило перейти на первый
        выход — торс схлопнулся с 0.372 до 0.122, зверь стал плоским.

        Труба, открытая вдоль оси, замкнута в КАЖДОМ ПОПЕРЕЧНОМ СРЕЗЕ. Поэтому к объёмной заливке
        добавляется послойная по всем трём осям, а результаты объединяются: что не закрылось как
        пузырь, закроется как сечение."""
        m = self.owner == self.names.index(slot)
        k = max(1, int(round(pad / CELL)))
        if k > 0:
            m = nd.binary_closing(m, nd.generate_binary_structure(3, 1), iterations=k)
        out = nd.binary_fill_holes(m)
        for axis in range(3):
            f = np.empty_like(m)
            for i in range(m.shape[axis]):
                sl = [slice(None)] * 3
                sl[axis] = i
                sl = tuple(sl)
                f[sl] = nd.binary_fill_holes(m[sl])
            out |= f
        return out

    def body(self, pad=0.02):
        """ВСЁ ТЕЛО ЦЕЛИКОМ, без деления на слоты: нужно, чтобы знать, где зверь кончается.

        По нему меряется, насколько глубоко корень слота можно утопить в соседа. Своего слота для
        этого мало: у челюсти «назад» это вверх-назад, в затылок, и без общего тела кольцо уезжало
        наружу — из головы торчал плоский лоскут."""
        m = self.owner >= 0
        k = max(1, int(round(pad / CELL)))
        m = nd.binary_closing(m, nd.generate_binary_structure(3, 1), iterations=k)
        return nd.binary_fill_holes(m)

    def idx(self, p):
        return tuple(int(round((p[k] - self.lo[k]) / CELL)) for k in range(3))

    def at(self, mask, p):
        i = self.idx(p)
        if any(i[k] < 0 or i[k] >= self.dim[k] for k in range(3)):
            return False
        return bool(mask[i])
