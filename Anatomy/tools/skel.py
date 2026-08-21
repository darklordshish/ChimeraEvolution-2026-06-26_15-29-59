# -*- coding: utf-8 -*-
"""РАСЧЁТ СКЕЛЕТА ДО СБОРКИ. Повторяет Unity: Quaternion.Euler(x,y,z) = Ry*Rx*Rz, кость растёт по +Y,
ребёнок стартует в точке attach вдоль родителя и наследует его поворот."""
import math
import numpy as np

def Rx(a):
    c, s = math.cos(a), math.sin(a)
    return np.array([[1, 0, 0], [0, c, -s], [0, s, c]])

def Ry(a):
    c, s = math.cos(a), math.sin(a)
    return np.array([[c, 0, s], [0, 1, 0], [-s, 0, c]])

def Rz(a):
    c, s = math.cos(a), math.sin(a)
    return np.array([[c, -s, 0], [s, c, 0], [0, 0, 1]])

def euler(d):
    x, y, z = [math.radians(v) for v in d]
    return Ry(y) @ Rx(x) @ Rz(z)

UP = np.array([0.0, 1.0, 0.0])

class B:
    def __init__(self, name, parent='', origin=(0, 0, 0), attach=1.0, length=0.1,
                 dir=(0, 0, 0), r0=0.05, r1=0.05, section=1.0, mirror=False, socket='', depth=1.0, blend=0.0, endBone='', endAttach=1.0, layer=0):
        self.name, self.parent, self.attach, self.length = name, parent, attach, length
        self.origin = np.array(origin, dtype=float)
        self.dir, self.r0, self.r1, self.section, self.depth = dir, r0, r1, section, depth
        self.mirror, self.socket, self.blend = mirror, socket, blend
        self.endBone, self.endAttach = endBone, endAttach
        self.layer = layer

def build(bones):
    # ИМЯ — АДРЕС, И ОН ОБЯЗАН БЫТЬ УНИКАЛЕН. При дубле словарь молча оставляет последнюю кость, а
    # первая теряет и позицию, и поворот: ссылавшиеся на неё дети уезжают в чужое место. Ошибки при
    # этом нет нигде — ищется по перекошенной морде. Дешевле упасть здесь
    seen = {}
    for b in bones:
        if b.name in seen:
            raise SystemExit('ДУБЛЬ ИМЕНИ КОСТИ «%s» (слои %d и %d) — имя это адрес, вторая займёт '
                             'место первой' % (b.name, seen[b.name], b.layer))
        seen[b.name] = b.layer
    by = {b.name: b for b in bones}
    pos, rot = {}, {}
    for b in bones:                              # порядок данных = порядок родителей (проверяется ниже)
        R = euler(b.dir)
        p = b.origin.copy()
        if b.parent:
            assert b.parent in pos, 'кость %s ссылается на %s, которой ещё нет' % (b.name, b.parent)
            par = by[b.parent]
            p = pos[b.parent] + rot[b.parent] @ (UP * (par.length * b.attach))
            R = rot[b.parent] @ R
        pos[b.name], rot[b.name] = p, R
    return by, pos, rot

def tip(b, pos, rot):
    return pos[b.name] + rot[b.name] @ (UP * b.length)

def spheres(b, pos, rot):
    """Как Grow: цепочка сфер от r0 к r1. Возвращает (центр, радиус, section)."""
    avg = max(0.001, (b.r0 + b.r1) * 0.5)
    segs = max(2, min(12, int(round(b.length / avg))))
    out = []
    for i in range(segs + 1):
        t = i / float(segs)
        c = pos[b.name] + rot[b.name] @ (UP * (b.length * t))
        out.append((c, b.r0 + (b.r1 - b.r0) * t, b.section))
    return out, segs

def report(bones, checks=()):
    by, pos, rot = build(bones)
    print('%-11s %-11s  начало x,y,z          конец x,y,z           сегм  шаг/диам' % ('кость', 'родитель'))
    lo = np.array([9.0, 9.0, 9.0]); hi = np.array([-9.0, -9.0, -9.0])
    for b in bones:
        t = tip(b, pos, rot)
        ss, segs = spheres(b, pos, rot)
        step = b.length / segs
        mark = '' if step < min(b.r0, b.r1) * 2 else '  ЩЕЛЬ!'
        print('%-11s %-11s (%6.3f,%6.3f,%6.3f) (%6.3f,%6.3f,%6.3f)  %2d  %.2f%s'
              % (b.name, b.parent or '(корень)', pos[b.name][0], pos[b.name][1], pos[b.name][2],
                 t[0], t[1], t[2], segs, step / max(0.001, min(b.r0, b.r1) * 2), mark))
        for c, r, sec in ss:
            for sgn in ((1, -1) if b.mirror else (1,)):
                cc = np.array([c[0] * sgn, c[1], c[2]])
                rr = np.array([r * sec, r, r])
                lo = np.minimum(lo, cc - rr); hi = np.maximum(hi, cc + rr)
    print()
    print('ГАБАРИТ НАРИСОВАННОГО: X %6.3f..%6.3f  Y %6.3f..%6.3f  Z %6.3f..%6.3f'
          % (lo[0], hi[0], lo[1], hi[1], lo[2], hi[2]))
    print('   ширина %.3f   высота %.3f   длина %.3f' % (hi[0] - lo[0], hi[1] - lo[1], hi[2] - lo[2]))
    print()
    bad = 0
    for label, got, want, tol in checks:
        d = got(by, pos, rot)
        ok = abs(d - want) <= tol
        bad += 0 if ok else 1
        print('%-34s %7.3f  цель %6.3f  %s%s' % (label, d, want, 'ok' if ok else 'МИМО ', '' if ok else '(%+.3f)' % (d - want)))
    print()
    print('ПРОВЕРОК МИМО: %d' % bad)
    return by, pos, rot
