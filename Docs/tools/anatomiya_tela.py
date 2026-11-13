# -*- coding: utf-8 -*-
"""АНАТОМИЯ ТЕЛА — схемы устройства тела игрока и NPC по коду.

Пишет `Docs/АНАТОМИЯ_ТЕЛА.md` (описание) и `Docs/АНАТОМИЯ_ТЕЛА/рис-*.svg` (схемы), а с ключом `--html путь` —
ещё и страницу со всеми схемами для просмотра.

ЭТО СНИМОК, А НЕ ДЕТЕКТОР: подписи — факты кода на дату, записанные здесь руками после сверки. Карта тел меряет
настоящий билдер, а этот скрипт только рисует. Поменялось устройство тела — сверить подписи с кодом и перегенерировать.
Правила конструктора живут в `CONSTRUCTOR_GUIDE.md`; здесь — картинка того, как они сейчас собраны.

Запуск: python Docs/tools/anatomiya_tela.py [--html путь.html]
"""
import html, os, sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
DOCS = os.path.join(ROOT, 'Docs')
FIG_DIR = os.path.join(DOCS, 'АНАТОМИЯ_ТЕЛА')
MD_PATH = os.path.join(DOCS, 'АНАТОМИЯ_ТЕЛА.md')
DATE = '16.09.2026'
E = html.escape


# ── примитивы SVG ─────────────────────────────────────────────────────────────────────
def markers(p):
    out = ['<defs>']
    for k, cls in (('m', 'ah'), ('pl', 'ah-pl'), ('npc', 'ah-npc'), ('ink', 'ah-ink')):
        out.append(f'<marker id="{p}-{k}" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" '
                   f'orient="auto-start-reverse"><path d="M0,0 L10,5 L0,10 z" class="{cls}"/></marker>')
    out.append('</defs>')
    return ''.join(out)


def tx(x, y, s, cls='', anchor='start'):
    return f'<text x="{x:.1f}" y="{y:.1f}" text-anchor="{anchor}" class="{cls}">{E(s)}</text>'


def box(x, y, w, h, cls, lines, tcls=None, pad=12, anchor='start', lh=18, r=4):
    s = f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="{r}" class="{cls}"/>'
    n = len(lines)
    y0 = y + h / 2 - (n - 1) * lh / 2 + 4.5
    x0 = x + pad if anchor == 'start' else x + w / 2
    if tcls is None:
        tcls = ['t-h'] + [''] * (n - 1)
    for i, line in enumerate(lines):
        s += tx(x0, y0 + i * lh, line, tcls[i] if i < len(tcls) else '', anchor)
    return s


def line(pts, kind, p, cls=None, dash=False):
    d = ' '.join(f'{a:.1f},{b:.1f}' for a, b in pts)
    c = cls or {'m': 'e', 'pl': 'e e-pl', 'npc': 'e e-npc', 'ink': 'e e-ink'}[kind]
    extra = ' stroke-dasharray="5 4"' if dash else ''
    return f'<polyline points="{d}" class="{c}"{extra} marker-end="url(#{p}-{kind})"/>'


FIGS = {}   # номер → (заголовок, ширина, высота, тело)


def svg(p, w, h, label, body):
    FIGS[p] = (label, w, h, body)
    return (f'<svg class="d" viewBox="0 0 {w} {h}" role="img" aria-label="{E(label)}" '
            f'xmlns="http://www.w3.org/2000/svg">{markers(p)}{body}</svg>')


# ── Рис. 1: одно тело, два водителя ───────────────────────────────────────────────────
def fig1():
    p = 'f1'
    LX, LW, CX, CW, RX, RW = 100, 280, 470, 290, 850, 320
    top0, step, bh = 58, 84, 70
    rows = [
        ('РОЖДЕНИЕ',
         ['Сцена SampleScene · объект Player', 'компоненты лежат в сцене'],
         ['Awake: слоты · Start: Recompute', 'Configure(шасси, доноры) — то же'],
         ['Спавнер: Instantiate(префаб вида)', 'босс: ChimeraFactory.Spawn → Configure'],
         ('toC', 'Awake'), ('toC', 'Awake')),
        ('СОСТАВ',
         ['шасси Человек · пул 16', 'доноры: Волк, Змея, Лось, Ёж'],
         ['слот = орган шасси', '+ органы доноров с тем же слотом'],
         ['шасси = вид префаба', 'доноры = 4 зверя из EvolutionConfig'],
         ('toC', 'chassis'), ('toC', 'chassis')),
        ('МОЩЬ',
         ['родство к виду органа: ×1 → ×2', 'в сцене растёт с 80 до 100'],
         ['Express: родной v × m,', 'донорский база + (v − база) × m'],
         ['фиксированная expression', 'волк 0.45 · лось, змея, ёж 0.5'],
         ('toC', 'm'), ('toC', 'm')),
        ('КОМПОНЕНТЫ',
         ['+ Senses — профиль чувств', '+ Betrayal — эрозия признания'],
         ['+ Noise, Satiety, ScentTrail,', 'EmotionTint, TintMixer, Stamina'],
         ['+ Personality, SpawnVariance, Morale', '+ Metamorph (эволюция включена)'],
         ('toL', 'AddComponent'), ('toR', 'AddComponent')),
        ('ПРИЁМЫ',
         ['грани Player*: PlayerBite,', 'PlayerAttack, PlayerConstrict…'],
         ['запись органа → носитель;', 'носитель платит цену и ждёт откат'],
         ['доставки: BiteAbility, ChargeAbility…', 'машины Constrict, CurlDefense'],
         ('toL', 'Configure'), ('toR', 'Configure')),
        ('СТАТЫ',
         ['PlayerController: ход, рывок', 'PlayerAttack: темп от Сердца'],
         ['сумма вкладов → Health, Stamina,', 'маркеры Thorns, Massive, Camouflage…'],
         ['психика получает скорость хода', 'числа приёмов — только в записях'],
         ('toL', 'SetLegs'), ('toR', 'OnBodyStats')),
        ('КОМАНДЫ',
         ['PlayerInputDriver: кнопка → TryUse()', '1–6 — смена органа в слоте'],
         ['тело не читает ввод', 'и не запускает приёмы'],
         ['психика решает: CanUse → TryUse()', 'альфа: арсенал и простой захват'],
         ('toC', 'ToggleSlot'), ('toC', 'Ability<T>')),
        ('ЧУВСТВА',
         ['Perception: нюх, термо, Чутьё, слух', 'Senses.Set — дальности от сборки'],
         ['флаги органов слота Чутьё', 'раздаются только игроку'],
         ['Senses и AlertState вешает психика', 'сборка тела их не меняет'],
         ('toL', 'Senses.Set'), None),
        ('ПСИХИКА',
         ['психики нет', 'смена доминанты ни на что не влияет'],
         ['доминанта ≥ Medium сменилась', '→ событие onDominantChanged'],
         ['Metamorph → PsycheDispatch.Attach', 'Wolf, Snake, Moose, Hedgehog или альфа'],
         None, ('toR', 'событие')),
    ]
    b = []
    b.append(tx(LX + LW / 2, 26, 'ИГРОК', 't-lab t-pl t-strong', 'middle'))
    b.append(tx(CX + CW / 2, 26, 'ТЕЛО · CreatureBody', 't-lab t-strong', 'middle'))
    b.append(tx(RX + RW / 2, 26, 'NPC', 't-lab t-npc t-strong', 'middle'))
    b.append(f'<line x1="{LX}" y1="38" x2="{LX + LW}" y2="38" class="rule-pl"/>')
    b.append(f'<line x1="{CX}" y1="38" x2="{CX + CW}" y2="38" class="rule-ink"/>')
    b.append(f'<line x1="{RX}" y1="38" x2="{RX + RW}" y2="38" class="rule-npc"/>')
    for i, (lab, L, C, R, la, ra) in enumerate(rows):
        top = top0 + i * step
        mid = top + bh / 2
        b.append(tx(88, mid + 4, lab, 't-lab', 'end'))
        b.append(box(LX, top, LW, bh, 'b-pl', L))
        b.append(box(CX, top, CW, bh, 'b-body', C))
        b.append(box(RX, top, RW, bh, 'b-npc', R))
        gl = (LX + LW + CX) / 2
        gr = (CX + CW + RX) / 2
        if la:
            d, lbl = la
            pts = [(LX + LW + 4, mid), (CX - 5, mid)] if d == 'toC' else [(CX - 4, mid), (LX + LW + 5, mid)]
            b.append(line(pts, 'pl', p))
            b.append(tx(gl, mid - 8, lbl, 't-edge', 'middle'))
        if ra:
            d, lbl = ra
            pts = [(RX - 4, mid), (CX + CW + 5, mid)] if d == 'toC' else [(CX + CW + 4, mid), (RX - 5, mid)]
            b.append(line(pts, 'npc', p))
            b.append(tx(gr, mid - 8, lbl, 't-edge', 'middle'))
    h = top0 + len(rows) * step - 4
    return svg(p, 1180, h, 'Рис. 1. Одно тело, два водителя', ''.join(b))


# ── Рис. 2: пересчёт тела ─────────────────────────────────────────────────────────────
def fig2():
    p = 'f2'
    b = []
    trig = ['Start', 'Configure', 'Install · Remove', 'ToggleSlot (1–6)', 'Grant/RemoveChimeraSlot',
            'ExpandPool', 'SetExpression', 'Refeed — после диспатча', 'Update — родство к донорам', 'изменилось']
    b.append('<rect x="20" y="40" width="230" height="364" rx="4" class="b-plain"/>')
    b.append(tx(34, 66, 'ЗАПУСКАЮТ ПЕРЕСЧЁТ', 't-lab'))
    for i, t in enumerate(trig):
        indent = 14 if t == 'изменилось' else 0
        b.append(tx(34 + indent, 98 + i * 29, t, 't-mono'))
    b.append(line([(250, 222), (272, 222), (272, 72), (295, 72)], 'ink', p))
    b.append(tx(262, 214, 'Recompute', 't-edge', 'end'))
    SX, SW = 300, 250
    stages = [
        (40, ['слоты тела', 'каждый непустой слот']),
        (140, ['Express(слот)', 'вклад органа с мощью m']),
        (240, ['группы по типу слота', 'дубль → супремум: max, время — min']),
        (340, ['сумма групп', 'hp, stam, ход, рывок, броня, реген, флаги']),
    ]
    for y, lines in stages:
        b.append(box(SX, y, SW, 64, 'b-body', lines))
    for (y, _), lbl in zip(stages[:-1], ('вклад', 'группа', 'итог')):
        b.append(line([(SX + SW / 2, y + 64 + 3), (SX + SW / 2, y + 100 - 4)], 'ink', p))
        b.append(tx(SX + SW / 2 + 10, y + 86, lbl, 't-edge'))
    b.append('<rect x="20" y="440" width="530" height="118" rx="4" class="b-note"/>')
    b.append(tx(34, 466, 'ПЕРЕСЧЁТ НЕ ДЕЛАЕТ', 't-lab'))
    for i, t in enumerate(('не читает ввод и не запускает приёмы',
                           'не вешает психику — это PsycheDispatch',
                           'не трогает Senses у NPC')):
        b.append(tx(34, 494 + i * 22, t, 't-sub'))
    OX, OW, oy0, ostep, oh = 640, 520, 36, 54, 46
    outs = [
        (['ProvisionAbilities — приёмы из записей органов'], 'both'),
        (['Satiety.SetMetabolism — голод по однородности'], 'both'),
        (['маркеры: ColdBlooded, Camouflage, Thorns,', 'VenomResist, BleedResist, Massive (флаг шасси)'], 'both'),
        (['Perception и Senses.Set — чувства от сборки'], 'pl'),
        (['PlayerAttack.SetTempo · PlayerController.SetLegs'], 'pl'),
        (['Health: HP = база шасси × (1 + бонус) × разброс × боссовость'], 'both'),
        (['Stamina: база × (1 + бонус), реген так же'], 'both'),
        (['OnBodyStats(скорость хода) — психикам'], 'npc'),
        (['MorphBuilder.Build — модель из надетых органов,', 'телеграф, камуфляж, вспышка, тепло — заново'], 'both'),
        (['UpdateTint · цвет запахового следа по составу'], 'both'),
        (['MostKin → onDominantChanged (слушает Metamorph)'], 'both'),
    ]
    cys = [oy0 + i * ostep + oh / 2 for i in range(len(outs))]
    b.append(f'<line x1="600" y1="{cys[0]:.1f}" x2="600" y2="{cys[-1]:.1f}" class="e e-ink"/>')
    b.append(f'<line x1="{SX + SW}" y1="372" x2="600" y2="372" class="e e-ink"/>')
    tags = {'both': ('b-plain', 'оба', 't-lab', 'ink'), 'pl': ('b-pl', 'игрок', 't-lab t-pl', 'pl'),
            'npc': ('b-npc', 'NPC', 't-lab t-npc', 'npc')}
    for (lines, who), cy in zip(outs, cys):
        cls, tag, tcls, kind = tags[who]
        y = cy - oh / 2
        b.append(line([(600, cy), (OX - 5, cy)], kind, p))
        b.append(box(OX, y, OW, oh, cls, lines, tcls=[''] * len(lines), lh=17))
        b.append(tx(OX + OW - 12, cy + 4, tag, tcls, 'end'))
    h = oy0 + len(outs) * ostep + 4
    return svg(p, 1180, h, 'Рис. 2. Пересчёт тела', ''.join(b))


# ── Рис. 3: мощь органа (по масштабу) ────────────────────────────────────────────────
def fig3():
    p = 'f3'
    X0, X1, Y0, Y1 = 64, 564, 300, 40
    fx = lambda a: X0 + (X1 - X0) * a / 100
    fy = lambda m: Y0 - (Y0 - Y1) * m / 2
    b = []
    for a in (0, 20, 40, 60, 80, 100):
        b.append(f'<line x1="{fx(a):.1f}" y1="{Y1}" x2="{fx(a):.1f}" y2="{Y0}" class="grid-l"/>')
        b.append(tx(fx(a), Y0 + 20, str(a), 't-tick', 'middle'))
    for m in (0, 0.5, 1, 1.5, 2):
        b.append(f'<line x1="{X0}" y1="{fy(m):.1f}" x2="{X1}" y2="{fy(m):.1f}" class="grid-l"/>')
        b.append(tx(X0 - 10, fy(m) + 4, ('%g' % m).replace('.', ','), 't-tick', 'end'))
    b.append(f'<line x1="{X0}" y1="{Y0}" x2="{X1}" y2="{Y0}" class="axis"/>')
    b.append(tx(X0, 22, 'мощь органа m', 't-lab'))
    b.append(tx((X0 + X1) / 2, Y0 + 44, 'родство к виду органа', 't-lab', 'middle'))
    b.append(f'<polyline points="{fx(0):.1f},{fy(1):.1f} {fx(100):.1f},{fy(2):.1f}" class="series-code"/>')
    b.append(tx(fx(46), fy(1.62), 'дефолт кода: с 0', 't-sub', 'end'))
    b.append(f'<polyline points="{fx(0):.1f},{fy(1):.1f} {fx(80):.1f},{fy(1):.1f} {fx(100):.1f},{fy(2):.1f}" class="series-pl"/>')
    b.append(f'<circle cx="{fx(80):.1f}" cy="{fy(1):.1f}" r="4" class="dot-pl"/>')
    b.append(f'<circle cx="{fx(100):.1f}" cy="{fy(2):.1f}" r="4" class="dot-pl"/>')
    b.append(tx(fx(2), fy(1) + 20, 'игрок, сцена: ×1 до 80, затем до ×2', 't-pl t-strong'))
    b.append(f'<line x1="{X0}" y1="{fy(0.5):.1f}" x2="{X1}" y2="{fy(0.5):.1f}" class="series-npc"/>')
    b.append(f'<line x1="{X0}" y1="{fy(0.45):.1f}" x2="{X1}" y2="{fy(0.45):.1f}" class="series-npc2"/>')
    b.append(tx(X1 - 4, fy(0.5) - 8, 'лось, змея, ёж 0,5', 't-npc', 'end'))
    b.append(tx(X1 - 4, fy(0.45) + 18, 'волк 0,45', 't-npc', 'end'))
    return svg(p, 600, 352, 'Рис. 3. Мощь органа', ''.join(b))


# ── Рис. 4: приём из записи органа ────────────────────────────────────────────────────
def fig4():
    p = 'f4'
    b = []
    b.append(box(20, 50, 150, 60, 'b-body', ['запись приёма', 'Organ.abilities']))
    b.append(line([(170, 80), (201, 80)], 'ink', p))
    b.append('<polygon points="205,80 265,40 325,80 265,120" class="b-body"/>')
    b.append(tx(265, 76, 'NativeOnly', 't-mono', 'middle'))
    b.append(tx(265, 93, 'и не дома?', 't-sub', 'middle'))
    b.append(line([(265, 120), (265, 166)], 'm', p))
    b.append(tx(275, 146, 'да', 't-edge'))
    b.append(box(165, 170, 200, 60, 'b-note', ['пропуск', 'клубок ежа на чужом шасси'], tcls=['t-h', 't-sub']))
    b.append(line([(325, 80), (366, 80)], 'ink', p))
    b.append(tx(345, 72, 'нет', 't-edge', 'middle'))
    b.append(box(370, 50, 180, 60, 'b-body', ['Resolve', '[Expressed] с мощью m']))
    b.append(line([(550, 80), (586, 80)], 'ink', p))
    b.append(box(590, 50, 200, 60, 'b-body', ['не дома: OnForeignChassis', 'захват: стадия ≤ 2']))
    b.append(line([(790, 80), (826, 80)], 'ink', p))
    b.append(box(830, 50, 150, 60, 'b-body', ['дубли типа: Sup', 'max, LowerIsBetter — min']))
    b.append(line([(980, 80), (1006, 80)], 'ink', p))
    b.append('<polygon points="1010,80 1070,44 1130,80 1070,116" class="b-body"/>')
    b.append(tx(1070, 85, 'игрок?', 't-h', 'middle'))
    b.append(line([(1070, 116), (1070, 145), (875, 145), (875, 166)], 'pl', p))
    b.append(tx(965, 138, 'да', 't-edge t-pl', 'middle'))
    b.append(box(780, 170, 190, 60, 'b-pl', ['PlayerCarrier', 'грань игрока']))
    b.append(line([(1070, 116), (1070, 166)], 'npc', p))
    b.append(tx(1080, 146, 'нет', 't-edge t-npc'))
    b.append(box(985, 170, 185, 60, 'b-npc', ['NpcCarrier', 'доставка NPC']))
    b.append(tx(975, 262, 'get-or-add компонента → Configure(запись)', 't-mono', 'middle'))
    b.append(tx(20, 262, 'запуск приёма: носитель платит цену из бака и держит перезарядку записи', 't-sub'))
    b.append(box(400, 170, 340, 60, 'b-note', ['у тела нет записи этого типа', 'Configure(null) → Available = false'], tcls=['t-h', 't-mono']))
    return svg(p, 1180, 280, 'Рис. 4. Приём из записи органа', ''.join(b))


# ── Рис. 5: смерть, родство, метаморфоза ─────────────────────────────────────────────
def fig5():
    p = 'f5'
    b = []
    y, h = 40, 76
    b.append(box(20, y, 190, h, 'b-body', ['смерть жертвы', 'Health.onDeath', '→ CreditKiller'], tcls=['t-h', 't-mono', 't-mono']))
    b.append(line([(210, 78), (246, 78)], 'ink', p))
    b.append(tx(228, 70, 'убийца', 't-edge', 'middle'))
    b.append(box(250, y, 240, h, 'b-body', ['убийце — любому телу', 'родство: шасси и органы × 0,55', 'сытость — если ест мясо']))
    b.append(line([(490, 78), (526, 78)], 'ink', p))
    b.append(box(530, y, 210, h, 'b-body', ['TryChimerize', 'случайный орган жертвы', 'шанс = родство / 100'], tcls=['t-mono t-h', '', '']))
    b.append(line([(740, 78), (776, 78)], 'ink', p))
    b.append(tx(758, 70, 'Install', 't-edge', 'middle'))
    b.append(box(780, y, 180, h, 'b-body', ['Recompute', 'слот, пул, вклады,', 'идентичность заново'], tcls=['t-mono t-h', '', '']))
    b.append(line([(960, 78), (986, 78)], 'ink', p))
    b.append('<polygon points="990,78 1070,34 1150,78 1070,122" class="b-body"/>')
    b.append(tx(1070, 74, 'доминанта', 't-h', 'middle'))
    b.append(tx(1070, 91, 'сменилась?', 't-sub', 'middle'))
    b.append(line([(1070, 122), (1070, 176)], 'npc', p))
    b.append(tx(1080, 152, 'да', 't-edge t-npc'))
    b.append(tx(1070, 20, 'нет — психика прежняя', 't-sub', 'middle'))
    y2 = 180
    b.append(box(950, y2, 220, h, 'b-npc', ['Metamorph — только NPC', 'Constrict.End()', 'снять психику'], tcls=['t-h', 't-mono', '']))
    b.append(line([(950, 218), (864, 218)], 'npc', p))
    b.append(box(600, y2, 260, h, 'b-npc', ['PsycheDispatch.Attach', 'психика по MostKin', 'и Refeed'], tcls=['t-mono t-h', '', 't-mono']))
    b.append(line([(600, 218), (514, 218)], 'npc', p))
    b.append(box(250, y2, 260, h, 'b-npc', ['новая психика', 'Wolf · Snake · Moose · Hedgehog', 'или ChimeraAlpha'], tcls=['t-h', 't-mono', 't-mono']))
    b.append(line([(250, 218), (115, 218), (115, 120)], 'm', p, dash=True))
    b.append(tx(125, 244, 'следующее убийство', 't-edge'))
    sy = 320
    BX, BW = 250, 920
    fxx = lambda k: BX + BW * k
    b.append(tx(20, sy + 4, 'ИДЕНТИЧНОСТЬ К ВИДУ', 't-lab'))
    b.append(tx(20, sy + 24, 'шасси весит 0,1,', 't-sub'))
    b.append(tx(20, sy + 42, 'органы вида — 0,9 / N', 't-sub'))
    segs = [(0, 0.65, 'seg0', 'None — чужой'), (0.65, 0.85, 'seg1', 'Weak'), (0.85, 0.999, 'seg2', 'Medium'), (0.999, 1.0, 'seg3', '')]
    for a, c, cls, name in segs:
        b.append(f'<rect x="{fxx(a):.1f}" y="{sy - 12}" width="{max(3, fxx(c) - fxx(a)):.1f}" height="22" class="{cls}"/>')
        if name:
            b.append(tx((fxx(a) + fxx(c)) / 2, sy + 4, name, 't-seg', 'middle'))
    for k, lbl in ((0, '0'), (0.65, '0,65'), (0.85, '0,85'), (1.0, '1 — Strong')):
        b.append(f'<line x1="{fxx(k):.1f}" y1="{sy + 10}" x2="{fxx(k):.1f}" y2="{sy + 18}" class="axis"/>')
        b.append(tx(fxx(k), sy + 34, lbl, 't-tick', 'end' if k == 1.0 else 'middle' if k else 'start'))
    b.append(tx(fxx(0.85), sy + 54, 'метаморфоза — от этого порога', 't-sub', 'middle'))
    return svg(p, 1180, 390, 'Рис. 5. Смерть, родство, метаморфоза', ''.join(b))


# ── данные таблиц (сверено с SpeciesBootstrap и носителями на 16.09) ─────────────────
SW = {'Bite': '#FF4733', 'Leap': '#FF991A', 'Antler': '#33D999', 'Charge': '#E61A80', 'Volley': '#F2CC4D',
      'Kick': '#CCD940', 'Sword': '#8CBFF2', 'Roll': '#999EA8', 'Curl': '#73808F', 'Howl': '#9980FF',
      'Grab': '#B333E6', 'RageTint': '#B81A1A'}

# приём, запись, органы, NPC (носитель, цвет) | 'psyche:Имя' | None, игрок (носитель, цвет) | None, цена и откат
RECORDS = [
    ('Укус', 'BiteData', 'Пасть волка, Ядовитые клыки, Цепкая пасть', ('BiteAbility', 'Bite'), ('PlayerBite', 'Bite'), 'откат 0,7'),
    ('Наскок', 'LeapData', 'Волчьи ноги, Тело-хвост', ('LeapAbility', 'Leap'), None, 'цена 30 и 20'),
    ('Удар конечностью', 'LimbStrikeData', 'Кисть, Коготь, Копыто', ('LimbStrikeAbility', 'Kick'), ('PlayerAttack', 'Sword'), 'NPC: откат 1,1; игрок: темп Сердца'),
    ('Пинок', 'KickData', 'Ноги человека', None, ('PlayerKick', 'Kick'), 'откат 1'),
    ('Рога', 'AntlerData', 'Рога', ('AntlerAbility', 'Antler'), ('PlayerAntler', 'Antler'), 'откат 2,5'),
    ('Таран', 'ChargeData', 'Лосиные ноги', ('ChargeAbility', 'Charge'), ('PlayerCharge', 'Charge'), 'разбег NPC: цена 70'),
    ('Залп', 'VolleyData', 'Игломёт', ('QuillVolley', 'Volley'), ('PlayerQuillVolley', None), 'откат 2,5'),
    ('Перекат', 'RollData', 'Ежиные ноги', None, ('PlayerRoll', 'Roll'), 'едет на рывке'),
    ('Клубок', 'CurlData', 'Ежиные ноги — только дома', ('CurlDefense', 'Curl'), ('CurlDefense', 'Curl'), 'расход бака в клубке'),
    ('Захват', 'ConstrictData', 'Пасть волка, Цепкая пасть, Хвост змеи', ('Constrict', 'Grab'), ('PlayerConstrict', 'Grab'), 'откат после отпускания 2,5 (ёж 3), замах 0,35 (ёж 0)'),
    ('Вой', 'HowlData', 'Пасть волка', 'psyche:WolfPsyche', ('PlayerHowl', 'Howl'), 'откат 10'),
    ('Рёв', 'BellowData', 'Глотка', 'psyche:MoosePsyche', ('PlayerBellow', 'Howl'), 'откат 10'),
    ('Клич', 'ScreamData', 'Рот человека', None, ('PlayerScream', 'RageTint'), 'откат 12'),
]

CHIP = {'удар': 'Kick', 'пинок': 'Kick', 'клич': 'RageTint', 'наскок': 'Leap', 'укус': 'Bite', 'вой': 'Howl',
        'захват': 'Grab', 'таран': 'Charge', 'рёв': 'Howl', 'рога': 'Antler', 'залп': 'Volley', 'перекат': 'Roll', 'клубок': 'Curl'}
SLOTS = ['хребет', 'Руки', 'Ноги', 'Сердце', 'Чутьё', 'Пасть', 'Шкура', 'Рога', 'Игломёт', 'Хвост', 'Наконечник']
SPECIES = [
    ('Человек', 'шасси игрока · 75 HP', {'хребет': ('Хребет', [], 'ch'), 'Руки': ('Кисть', ['удар'], ''), 'Ноги': ('Ноги', ['пинок'], ''),
        'Сердце': ('Сердце', [], ''), 'Чутьё': ('Чутьё', [], ''), 'Пасть': ('Рот', ['клич'], ''), 'Шкура': ('Кожа', [], '')}),
    ('Волк', 'экспрессия 0,45 · 38 HP', {'хребет': ('Хребет', [], 'ch'), 'Руки': ('Коготь', ['удар'], ''), 'Ноги': ('Волчьи ноги', ['наскок'], ''),
        'Сердце': ('Волчье сердце', [], ''), 'Чутьё': ('Нюх', [], ''), 'Пасть': ('Пасть', ['укус', 'вой', 'захват'], 'дом'), 'Шкура': ('Шкура', [], '')}),
    ('Змея', 'экспрессия 0,5 · 60 HP', {'Ноги': ('Тело-хвост', ['наскок'], 'ch'), 'Сердце': ('Хладнокровное сердце', [], ''), 'Чутьё': ('Пит-орган', [], ''),
        'Пасть': ('Ядовитые клыки', ['укус'], ''), 'Шкура': ('Чешуя', [], ''), 'Хвост': ('Хвост', ['захват'], 'дом'), 'Наконечник': ('Погремушка', [], 'ch')}),
    ('Лось', 'экспрессия 0,5 · 70 HP · массивный', {'хребет': ('Хребет', [], 'ch'), 'Руки': ('Копыто', ['удар'], ''), 'Ноги': ('Лосиные ноги', ['таран'], ''),
        'Сердце': ('Лосиное сердце', [], ''), 'Чутьё': ('Слух', [], ''), 'Пасть': ('Глотка', ['рёв'], ''), 'Шкура': ('Толстая шкура', [], ''), 'Рога': ('Рога', ['рога'], '')}),
    ('Ёж', 'экспрессия 0,5 · 52 HP', {'хребет': ('Хребет', [], 'ch'), 'Ноги': ('Ежиные ноги', ['перекат', 'клубок'], 'дом'), 'Сердце': ('Ядоупорное сердце', [], ''),
        'Чутьё': ('Пятак', [], ''), 'Пасть': ('Цепкая пасть', ['укус', 'захват'], 'дом'), 'Шкура': ('Иглы', [], ''), 'Игломёт': ('Игломёт', ['залп'], '')}),
]

NOTES = [
    ('Мощь игрока растёт только с 80 родства',
     'В сцене у тела игрока `bonusStartAffinity` = 80 и скидка 0,01 за единицу. В коде дефолт 0 и 0,008, а комментарий называет 80 «мёртвой зоной силы». Сцена перебивает код.'),
    ('Игрок тоже химеризуется от убийств',
     '`TryChimerize` не проверяет, кто убийца. Пока в сцене `evolveNpc` = 1, игрок с шансом «родство / 100» надевает орган жертвы, если он влезает в пул.'),
    ('Рога, Игломёт и Хвост игроку — только химерным слотом',
     'У человека нет органов этих мест, значит нет и слотов. В сцене `chimeraSlots` = 0.'),
    ('Один удар — два цвета замаха',
     'Запись `LimbStrikeData` общая, но доставка NPC красит замах цветом пинка (`Kick`), грань игрока — цветом меча (`Sword`).'),
    ('Порог диспатча и порог метаморфозы разные',
     '`PsycheDispatch` берёт доминанту уже при признании Weak (0,65). Метаморфозу запускает смена доминанты с порогом Medium (0,85).'),
    ('У змеи нет Хребта и Рук',
     'В бутстрапе у змеи 7 органов: Ядовитые клыки, Хладнокровное сердце, Тело-хвост, Чешуя, Пит-орган, Погремушка, Хвост. Слотов «хребет» и «Руки» у неё нет.'),
    ('Клич есть только у игрока',
     'У `ScreamData` нет NPC-носителя, и ни одна психика её не читает. Вой и рёв NPC читают психики волка и лося.'),
    ('Затраты хода ещё не в органах',
     'Цена рывка 25, расход спринта 30 и урон срыва рывком 6 и 5 — в `PlayerController`; расход погони волка 10 и лося 5 — в психиках (долг детектора `PsycheDataTests`). Чьи это числа — ног или поведения, — ждёт решения.'),
    ('Ритм атак NPC — число психики, у игрока — темп Сердца',
     'Перезарядку каждого приёма держит его носитель по записи. Как часто NPC вообще выбирает атаку, решает психика (`attackCooldown`); у игрока ритм удара конечностью задаёт Сердце.'),
]


# ── HTML ─────────────────────────────────────────────────────────────────────────────
def sw_html(name):
    return f'<span class="sw" style="--c:{SW[name]}" title="TelegraphColors.{name}"></span>'


def carrier_html(c):
    if c is None:
        return '<span class="none">нет</span>'
    if isinstance(c, str) and c.startswith('psyche:'):
        return f'<span class="none">носителя нет</span> · читает <code>{c[7:]}</code>'
    name, color = c
    dot = sw_html(color) if color else '<span class="sw sw-empty" title="свой цвет не задан"></span>'
    return f'{dot}<code>{E(name)}</code>'


def records_table_html():
    rows = ''.join(
        f'<tr><th scope="row">{E(n)}<code class="dt">{d}</code></th><td>{E(o)}</td><td class="c-npc">{carrier_html(npc)}</td>'
        f'<td class="c-pl">{carrier_html(pl)}</td><td>{E(cost)}</td></tr>'
        for n, d, o, npc, pl, cost in RECORDS)
    return ('<div class="scroll"><table class="rec"><thead><tr><th scope="col">приём · запись</th><th scope="col">органы с записью</th>'
            '<th scope="col" class="c-npc">NPC</th><th scope="col" class="c-pl">игрок</th><th scope="col">цена и откат у носителя</th>'
            '</tr></thead><tbody>' + rows + '</tbody></table></div>')


def slot_matrix_html():
    head = ''.join(f'<th scope="col">{E(s)}</th>' for s in SLOTS)
    rows = []
    for sp, meta, organs in SPECIES:
        cells = []
        for s in SLOTS:
            if s not in organs:
                cells.append('<td class="empty"><span aria-label="слота нет">·</span></td>')
                continue
            name, recs, flag = organs[s]
            chips = ''.join(f'<span class="chip">{sw_html(CHIP[r])}{E(r)}</span>' for r in recs)
            badge = '<span class="badge">только шасси</span>' if flag == 'ch' else ('<span class="badge badge-home">дом</span>' if flag == 'дом' else '')
            cells.append(f'<td><span class="organ">{E(name)}</span>{badge}{chips}</td>')
        rows.append(f'<tr><th scope="row"><span class="sp">{E(sp)}</span><span class="meta">{E(meta)}</span></th>{"".join(cells)}</tr>')
    return f'<div class="scroll"><table class="mx"><thead><tr><th scope="col">вид</th>{head}</tr></thead><tbody>{"".join(rows)}</tbody></table></div>'


def md_code(s):
    """`code` в тексте заметок → <code> для HTML."""
    out, parts = [], s.split('`')
    for i, part in enumerate(parts):
        out.append(f'<code>{E(part)}</code>' if i % 2 else E(part))
    return ''.join(out)


PALETTE_LIGHT = {'ground': '#EAEFEA', 'surface': '#F7F9F6', 'ink': '#16201C', 'muted': '#55635D', 'rule': '#C4CEC7',
                 'body-soft': '#E0E7E1', 'pl': '#255F92', 'pl-soft': '#DCE7F1', 'npc': '#91540F', 'npc-soft': '#F2E5D3',
                 'seg0': '#D5DDD7', 'seg1': '#B9C7BE', 'seg2': '#8FA497', 'seg3': '#16201C'}
PALETTE_DARK = {'ground': '#0F1412', 'surface': '#161D1A', 'ink': '#DBE3DE', 'muted': '#93A19A', 'rule': '#2D3833',
                'body-soft': '#1D2521', 'pl': '#86B4DF', 'pl-soft': '#172839', 'npc': '#DEA45C', 'npc-soft': '#2D2114',
                'seg0': '#232C28', 'seg1': '#34423B', 'seg2': '#556A5E', 'seg3': '#DBE3DE'}

SVG_CSS = '''
text{fill:var(--ink);font-family:"Golos Text","Segoe UI",system-ui,Arial,sans-serif;font-size:13px}
.t-h,.t-strong{font-weight:600}
.t-sub{fill:var(--muted);font-size:12px}
.t-lab{fill:var(--muted);font-family:"JetBrains Mono",Consolas,"Courier New",monospace;font-size:10.5px;letter-spacing:.07em}
.t-mono{font-family:"JetBrains Mono",Consolas,"Courier New",monospace;font-size:11.5px}
.t-edge{fill:var(--muted);font-family:"JetBrains Mono",Consolas,"Courier New",monospace;font-size:10.5px}
.t-tick{fill:var(--muted);font-family:"JetBrains Mono",Consolas,"Courier New",monospace;font-size:11px}
.t-seg{font-family:"JetBrains Mono",Consolas,"Courier New",monospace;font-size:11px}
.t-pl{fill:var(--pl)} .t-npc{fill:var(--npc)}
.b-body{fill:var(--body-soft);stroke:var(--ink);stroke-width:1.1}
.b-plain{fill:var(--surface);stroke:var(--rule);stroke-width:1}
.b-pl{fill:var(--pl-soft);stroke:var(--pl);stroke-width:1.2}
.b-npc{fill:var(--npc-soft);stroke:var(--npc);stroke-width:1.2}
.b-note{fill:none;stroke:var(--muted);stroke-width:1;stroke-dasharray:4 3}
.e{fill:none;stroke:var(--muted);stroke-width:1.4}
.e-pl{stroke:var(--pl)} .e-npc{stroke:var(--npc)} .e-ink{stroke:var(--ink)}
.ah{fill:var(--muted)} .ah-pl{fill:var(--pl)} .ah-npc{fill:var(--npc)} .ah-ink{fill:var(--ink)}
.rule-pl{stroke:var(--pl);stroke-width:2} .rule-npc{stroke:var(--npc);stroke-width:2} .rule-ink{stroke:var(--ink);stroke-width:2}
.grid-l{stroke:var(--rule);stroke-width:1;fill:none}
.axis{stroke:var(--muted);stroke-width:1.2;fill:none}
.series-pl{fill:none;stroke:var(--pl);stroke-width:3;stroke-linejoin:round}
.series-code{fill:none;stroke:var(--muted);stroke-width:1.6;stroke-dasharray:6 5}
.series-npc{stroke:var(--npc);stroke-width:3}
.series-npc2{stroke:var(--npc);stroke-width:1.6;stroke-dasharray:3 3}
.dot-pl{fill:var(--pl);stroke:var(--surface);stroke-width:2}
.seg0{fill:var(--seg0)} .seg1{fill:var(--seg1)} .seg2{fill:var(--seg2)} .seg3{fill:var(--seg3)}
'''


def tokens(pal):
    return ';'.join(f'--{k}:{v}' for k, v in pal.items())


def standalone_svg(key):
    """Отдельный файл схемы для доков: светлая палитра зашита, шрифты с системными запасными."""
    label, w, h, body = FIGS[key]
    css = SVG_CSS.replace('\n', '')
    for k, v in PALETTE_LIGHT.items():
        css = css.replace(f'var(--{k})', v)
    return (f'<svg xmlns="http://www.w3.org/2000/svg" width="{w}" height="{h}" viewBox="0 0 {w} {h}" role="img" aria-label="{E(label)}">'
            f'<title>{E(label)}</title><style>{css}</style>'
            f'<rect width="{w}" height="{h}" fill="{PALETTE_LIGHT["surface"]}"/>{markers(key)}{body}</svg>\n')


PAGE_CSS = r'''
:root{%LIGHT%}
@media (prefers-color-scheme: dark){:root:not([data-theme="light"]){%DARK%}}
:root[data-theme="dark"]{%DARK%}
*{box-sizing:border-box}
body{margin:0;background:var(--ground);color:var(--ink);font:16px/1.55 "Golos Text",system-ui,-apple-system,"Segoe UI",sans-serif}
.wrap{max-width:1240px;margin:0 auto;padding-inline:clamp(16px,4vw,40px);padding-block:44px 80px}
header{display:grid;gap:14px;padding-bottom:36px}
.eyebrow{font:500 .74rem/1.3 "JetBrains Mono",ui-monospace,Consolas,monospace;letter-spacing:.08em;text-transform:uppercase;color:var(--muted)}
h1{font-family:"Unbounded","Golos Text",system-ui,sans-serif;font-weight:600;font-size:clamp(2rem,4.6vw,3.3rem);line-height:1.08;letter-spacing:-.015em;margin:0;text-wrap:balance}
h2{font-family:"Unbounded","Golos Text",system-ui,sans-serif;font-weight:500;font-size:clamp(1.15rem,2.2vw,1.45rem);line-height:1.25;margin:0;text-wrap:balance}
p{margin:0;max-width:68ch}
.lede{font-size:1.1rem;max-width:62ch}
code{font-family:"JetBrains Mono",ui-monospace,Consolas,monospace;font-size:.86em;background:var(--body-soft);padding:.05em .32em;border-radius:3px;white-space:nowrap}
.legend{display:flex;flex-wrap:wrap;gap:8px 18px;font-size:.9rem;color:var(--muted)}
.legend span{display:inline-flex;align-items:center;gap:8px}
.legend i{width:14px;height:14px;border-radius:3px;border:1.5px solid;display:inline-block}
.lg-pl{background:var(--pl-soft);border-color:var(--pl)!important}
.lg-npc{background:var(--npc-soft);border-color:var(--npc)!important}
.lg-body{background:var(--body-soft);border-color:var(--ink)!important}
section{display:grid;gap:18px;padding-block:44px;border-top:1px solid var(--rule)}
.sec-head{display:grid;gap:8px}
figure{margin:0;background:var(--surface);border:1px solid var(--rule);border-radius:6px;overflow:hidden}
figcaption{padding:12px 18px 16px;border-top:1px solid var(--rule);font-size:.9rem;color:var(--muted)}
.scroll{overflow-x:auto}
svg.d{display:block;width:100%;min-width:940px;height:auto;padding:10px 6px}
.f3 svg.d{min-width:0}
svg.d text{fill:var(--ink);font-family:"Golos Text",system-ui,sans-serif;font-size:13px}
%SVGCSS%
.power{display:grid;grid-template-columns:minmax(0,1.15fr) minmax(0,1fr);gap:18px;align-items:start}
.rules{display:grid;gap:10px}
.rule{background:var(--surface);border:1px solid var(--rule);border-radius:6px;padding:14px 16px;display:grid;gap:6px}
.rule h3{margin:0;font-size:.95rem;font-weight:600}
.rule .f{font-family:"JetBrains Mono",ui-monospace,monospace;font-size:.95rem}
.rule p{font-size:.9rem;color:var(--muted)}
table{border-collapse:collapse;width:100%;font-size:.9rem}
th,td{text-align:left;vertical-align:top;padding:9px 12px;border-bottom:1px solid var(--rule)}
thead th{font:500 .72rem/1.3 "JetBrains Mono",ui-monospace,monospace;letter-spacing:.07em;text-transform:uppercase;color:var(--muted);background:var(--surface);white-space:nowrap}
.rec{min-width:980px;background:var(--surface)}
.rec tbody th{font-weight:600;white-space:nowrap}
.rec .dt{display:block;margin-top:2px;background:none;padding:0;color:var(--muted);font-weight:400}
.rec td code{background:none;padding:0}
th.c-pl,td.c-pl{box-shadow:inset 3px 0 0 var(--pl-soft)}
th.c-npc,td.c-npc{box-shadow:inset 3px 0 0 var(--npc-soft)}
thead th.c-pl{color:var(--pl)} thead th.c-npc{color:var(--npc)}
.sw{display:inline-block;width:10px;height:10px;border-radius:50%;background:var(--c);margin-right:7px;vertical-align:.02em;box-shadow:0 0 0 1px rgba(0,0,0,.25)}
.sw-empty{background:none;box-shadow:inset 0 0 0 1.5px var(--muted)}
.none{color:var(--muted)}
.mx{min-width:1080px;background:var(--surface);table-layout:fixed}
.mx thead th:first-child{width:150px}
.mx tbody th{white-space:normal}
.mx .sp{display:block;font-family:"Unbounded","Golos Text",sans-serif;font-weight:500;font-size:.95rem}
.mx .meta{display:block;color:var(--muted);font-size:.76rem;margin-top:3px}
.mx td{font-size:.84rem;border-left:1px solid var(--rule)}
.mx td.empty{color:var(--rule);text-align:center;font-size:1.2rem;background:repeating-linear-gradient(135deg,transparent 0 6px,var(--body-soft) 6px 7px)}
.organ{display:block;font-weight:500;line-height:1.3}
.chip{display:flex;align-items:center;margin-top:4px;font-size:.78rem;color:var(--muted)}
.badge{display:inline-block;margin-top:4px;font:500 .64rem/1.2 "JetBrains Mono",ui-monospace,monospace;letter-spacing:.04em;text-transform:uppercase;color:var(--muted);border:1px dashed var(--muted);border-radius:3px;padding:1px 4px}
.badge-home{border-style:solid}
.notes{display:grid;grid-template-columns:repeat(auto-fit,minmax(300px,1fr));gap:12px 28px;margin:0}
.notes div{display:grid;gap:4px;padding-top:12px;border-top:1px solid var(--rule)}
.notes dt{font-weight:600}
.notes dd{margin:0;color:var(--muted);font-size:.92rem}
.chips{display:flex;flex-wrap:wrap;gap:8px}
.chips span{font-size:.86rem;background:var(--surface);border:1px solid var(--rule);border-radius:4px;padding:4px 9px}
footer{border-top:1px solid var(--rule);padding-top:22px;color:var(--muted);font-size:.85rem}
@media (max-width:820px){.power{grid-template-columns:1fr}}
'''


def svg_css_scoped():
    return '\n'.join(('svg.d ' + rule) if rule and not rule.startswith('text') else rule
                     for rule in SVG_CSS.strip().split('\n')).replace('svg.d .t-pl{fill:var(--pl)} .t-npc', 'svg.d .t-pl{fill:var(--pl)} svg.d .t-npc')


def page(f1, f2, f3, f4, f5):
    css = (PAGE_CSS.replace('%LIGHT%', tokens(PALETTE_LIGHT)).replace('%DARK%', tokens(PALETTE_DARK))
           .replace('%SVGCSS%', svg_css_scoped()))
    notes = ''.join(f'<div><dt>{E(t)}</dt><dd>{md_code(d)}</dd></div>' for t, d in NOTES)
    return f'''<title>Анатомия тела CHIMERA</title>
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Golos+Text:wght@400;500;600&family=JetBrains+Mono:wght@400;500&family=Unbounded:wght@500;600&display=swap">
<style>{css}</style>
<div class="wrap">
<header>
  <div class="eyebrow">CHIMERA · по коду на {DATE} · после спеки 16.09</div>
  <h1>Анатомия тела</h1>
  <p class="lede">Игрок и NPC — один и тот же компонент <code>CreatureBody</code>: шасси, слоты, органы, записи приёмов, один пересчёт. Различаются четыре вещи: как тело рождается, откуда берётся мощь органа, кто исполняет приём и кто отдаёт команды.</p>
  <div class="legend" aria-label="Обозначения"><span><i class="lg-pl"></i>только у игрока</span><span><i class="lg-npc"></i>только у NPC</span><span><i class="lg-body"></i>общее тело</span></div>
</header>
<section><div class="sec-head"><h2>Одно тело, два водителя</h2><p>{E(DESC["f1"])}</p></div>
<figure><div class="scroll">{f1}</div><figcaption>{md_code(CAP["f1"])}</figcaption></figure></section>
<section><div class="sec-head"><h2>Пересчёт тела</h2><p>{E(DESC["f2"])}</p></div>
<figure><div class="scroll">{f2}</div><figcaption>{md_code(CAP["f2"])}</figcaption></figure></section>
<section><div class="sec-head"><h2>Мощь органа</h2><p>{E(DESC["f3"])}</p></div>
<div class="power"><figure class="f3">{f3}<figcaption>{md_code(CAP["f3"])}</figcaption></figure>
<div class="rules">
<div class="rule"><h3>Родной орган шасси</h3><div class="f">v × m</div><p>Раскрывается от себя. Времена (перезарядки) — как записаны.</p></div>
<div class="rule"><h3>Донорский орган в родном слоте</h3><div class="f">база + (v − база) × m</div><p>База — вытесненный родной орган этого слота.</p></div>
<div class="rule"><h3>Донорский в химерном слоте</h3><div class="f">0 + v × m</div><p>Вытеснять нечего, бленд идёт от нуля.</p></div>
<div class="rule"><h3>Записи приёмов</h3><div class="f">[Expressed] — тот же закон</div><p>Урон всех приёмов раскрывается так (спека 16.09); остальные поля записи идут как записаны.</p></div>
</div></div></section>
<section><div class="sec-head"><h2>Приём из записи органа</h2><p>{E(DESC["f4"])}</p></div>
<figure><div class="scroll">{f4}</div><figcaption>{md_code(CAP["f4"])}</figcaption></figure>
{records_table_html()}
<p class="none" style="font-size:.88rem">Точка — цвет замаха из <code>TelegraphColors</code>, который зовёт сам носитель. Пустой кружок — свой цвет не задан.</p></section>
<section><div class="sec-head"><h2>Слоты по видам</h2><p>{E(DESC["slots"])}</p></div>
{slot_matrix_html()}
<p class="none" style="font-size:.88rem">Штриховка — у вида нет органа, значит нет и слота. «Дом» — у органа задано родное шасси.</p></section>
<section><div class="sec-head"><h2>Смерть, родство, метаморфоза</h2><p>{E(DESC["f5"])}</p></div>
<figure><div class="scroll">{f5}</div><figcaption>{md_code(CAP["f5"])}</figcaption></figure>
<div class="chips" aria-label="Карта диспатча психики"><span>Волк → <code>WolfPsyche</code></span><span>Змея → <code>SnakePsyche</code></span><span>Лось → <code>MoosePsyche</code></span><span>Ёж → <code>HedgehogPsyche</code></span><span>Человек или никто → <code>ChimeraAlphaPsyche</code></span></div></section>
<section><div class="sec-head"><h2>Неочевидное по факту</h2><p>Места, где код, сцена и привычное описание расходятся.</p></div>
<dl class="notes">{notes}</dl></section>
<footer>Собрано из кода и сцены репозитория <code>ChimeraEvolution</code>. Схемы и описание в репозитории — <code>Docs/АНАТОМИЯ_ТЕЛА.md</code>, генератор — <code>Docs/tools/anatomiya_tela.py</code>.</footer>
</div>
'''


# ── тексты: описание и подписи (одни для страницы и для доков) ───────────────────────
DESC = {
    'f1': 'По строкам — что одинаково и что расходится. Стрелки — настоящие вызовы и поля между телом и сторонами.',
    'f2': '`Recompute` — единственное место, где состав превращается в поведение. Он идёт одинаково для игрока и NPC; разница только в том, какие получатели лежат на объекте.'.replace('`', ''),
    'f3': 'Мощь m — единственная ручка, через которую орган раскрывается в числа. Её источник — главное различие игрока и NPC.',
    'f4': 'Записи лежат в органе. Тело раскрывает их при каждом пересчёте и отдаёт носителю своей стороны; носитель сам платит цену приёма и держит его перезарядку.',
    'slots': 'Слоты тела — это органы его шасси. Донор даёт варианты только в слот с тем же именем; органы «только шасси» не крадутся никогда. Любой орган любого вида принимает лишь химерный слот.',
    'f5': 'Петля эволюции замыкается через смерть: убийца получает родство и шанс надеть орган жертвы, пересчёт меняет идентичность, а смена доминанты у NPC меняет психику.',
}
CAP = {
    'f1': 'Источники: `CreatureBody.cs` (Awake, Recompute), `CreatureBody.Abilities.cs`, `PsycheDispatch.cs`, `Metamorph.cs`, `ChimeraAlphaPsyche.cs`, сцена `SampleScene`, генераторы `*Prefab.cs`. Разброс особи и личность в сцене выключены (`IndividualityConfig` = 0).',
    'f2': 'Порядок раздачи справа — порядок строк в `CreatureBody.Recompute`. Модель строится у любого вида, у шасси которого есть места (`sockets`), игрок — так же.',
    'f3': '`BonusMultiplier`: если `expression` > 0, возвращает её; иначе линейно ×1…×2 по родству от `bonusStartAffinity` до `bonusFullAffinity`. В сцене у игрока старт 80, в коде дефолт 0.',
    'f4': '`CreatureBody.ProvisionAbilities`, база доставок `WindupAbility` (цена и перезарядка), машина `Constrict` (перезарядка захвата). «Дома» — родное шасси органа не задано или совпало с шасси тела. Носитель не сносится, а гаснет.',
    'f5': '`CreditKiller`, `CreatureBody.Evolution.cs`, `CreatureBody.Identity.cs`, `Metamorph.cs`. Идентичность — доля вида в массе тела; признание — по эффективной идентичности (минус эрозия предательства). Эволюция идёт, пока в сцене `evolveNpc` = 1.',
}


def markdown():
    rec_rows = '\n'.join(
        f'| {n} · `{d}` | {o} | {md_carrier(npc)} | {md_carrier(pl)} | {cost} |' for n, d, o, npc, pl, cost in RECORDS)
    slot_head = '| вид | ' + ' | '.join(SLOTS) + ' |\n|' + '---|' * (len(SLOTS) + 1)
    slot_rows = []
    for sp, meta, organs in SPECIES:
        cells = []
        for s in SLOTS:
            if s not in organs:
                cells.append('·')
                continue
            name, recs, flag = organs[s]
            extra = ' *(только шасси)*' if flag == 'ch' else (' *(дом)*' if flag == 'дом' else '')
            cells.append(name + extra + (': ' + ', '.join(recs) if recs else ''))
        slot_rows.append(f'| **{sp}** — {meta} | ' + ' | '.join(cells) + ' |')
    notes = '\n'.join(f'- **{t}.** {d}' for t, d in NOTES)
    return f'''# Анатомия тела: игрок и NPC

- **Что это:** схемы устройства тела игрока и NPC — снимок по коду и сцене на {DATE}, после спеки
  `2026-09-16-dovodka-dannyh-v-organah.md`. Не детектор: подписи сверены руками и записаны в генераторе
  `Docs/tools/anatomiya_tela.py`. Поменялось устройство тела — сверить и перегенерировать.
- **Правила** конструктора живут в `CONSTRUCTOR_GUIDE.md`; как тело собирается в метрах и стыках — `УСТРОЙСТВО_ТЕЛА.md`
  и генерируемые `Диаграммы/`. Здесь — как части тела связаны между собой.
- **Одной фразой:** игрок и NPC — один и тот же `CreatureBody`. Различаются четыре вещи: как тело рождается, откуда
  берётся мощь органа, кто исполняет приём и кто отдаёт команды.

Цвета на схемах: синее — только у игрока, охра — только у NPC, серо-зелёное с чёрной рамкой — общее тело.

---

## Рис. 1. Одно тело, два водителя

![Рис. 1. Одно тело, два водителя](АНАТОМИЯ_ТЕЛА/рис-1.svg)

{DESC["f1"]}

- **Рождение.** Игрок лежит в сцене готовым объектом; NPC-вид рождается спавнером из префаба, босс и химеры —
  `ChimeraFactory.Spawn` через тот же конструктор, что у игрока.
- **Мощь органа.** У игрока — родство к виду органа (×1…×2), у NPC — фиксированная экспрессия вида.
- **Приёмы.** Одна запись органа кормит грань игрока или доставку NPC. Носитель сам платит цену из бака и держит
  перезарядку записи; психика только спрашивает «можно ли сейчас» (`CanUse`) и решает, чем бить.
- **Команды.** Тело не читает ввод и не запускает приёмы: у игрока это делает `PlayerInputDriver`, у NPC — психика.
  Химера-альфа бьёт всем арсеналом тела и хватает простейшим образом — одну цель, когда рядом нет других.

{CAP["f1"]}

## Рис. 2. Пересчёт тела

![Рис. 2. Пересчёт тела](АНАТОМИЯ_ТЕЛА/рис-2.svg)

`Recompute` — единственное место, где состав превращается в поведение. Слоты раскрываются в вклады органов, дубли
одного типа слота сводятся супремумом, группы суммируются — и итог расходится по получателям, какие лежат на объекте:
общим (здоровье, выносливость, маркеры, модель, цвет), игроку (чувства, ход, темп удара) или психике NPC (скорость хода).

{CAP["f2"]}

## Рис. 3. Мощь органа

![Рис. 3. Мощь органа](АНАТОМИЯ_ТЕЛА/рис-3.svg)

{DESC["f3"]}

| орган | закон |
|---|---|
| родной орган шасси | `v × m`; времена — как записаны |
| донорский в родном слоте | `база + (v − база) × m`, база — вытесненный родной орган |
| донорский в химерном слоте | `0 + v × m` |
| записи приёмов | поля `[Expressed]` — тем же законом; урон всех приёмов раскрывается так |

{CAP["f3"]}

## Рис. 4. Приём из записи органа

![Рис. 4. Приём из записи органа](АНАТОМИЯ_ТЕЛА/рис-4.svg)

{DESC["f4"]}

| приём · запись | органы с записью | NPC | игрок | цена и откат у носителя |
|---|---|---|---|---|
{rec_rows}

{CAP["f4"]}

## Слоты по видам

{DESC["slots"]}

{slot_head}
{chr(10).join(slot_rows)}

«·» — у вида нет органа, значит нет и слота. «Дом» — у органа задано родное шасси: там приём сильнее или вовсе доступен.

## Рис. 5. Смерть, родство, метаморфоза

![Рис. 5. Смерть, родство, метаморфоза](АНАТОМИЯ_ТЕЛА/рис-5.svg)

{DESC["f5"]}

Карта диспатча психики: Волк → `WolfPsyche`, Змея → `SnakePsyche`, Лось → `MoosePsyche`, Ёж → `HedgehogPsyche`,
Человек или истинная химера → `ChimeraAlphaPsyche`.

{CAP["f5"]}

## Неочевидное по факту

{notes}
'''


def md_carrier(c):
    if c is None:
        return 'нет'
    if isinstance(c, str) and c.startswith('psyche:'):
        return f'носителя нет, читает `{c[7:]}`'
    return f'`{c[0]}`'


def main():
    figs = (fig1(), fig2(), fig3(), fig4(), fig5())
    os.makedirs(FIG_DIR, exist_ok=True)
    for i, key in enumerate(('f1', 'f2', 'f3', 'f4', 'f5'), 1):
        with open(os.path.join(FIG_DIR, f'рис-{i}.svg'), 'w', encoding='utf-8', newline='\n') as f:
            f.write(standalone_svg(key))
    with open(MD_PATH, 'w', encoding='utf-8', newline='\n') as f:
        f.write(markdown())
    print('доки:', MD_PATH, '+ 5 схем в', FIG_DIR)
    if '--html' in sys.argv:
        out = sys.argv[sys.argv.index('--html') + 1]
        with open(out, 'w', encoding='utf-8', newline='\n') as f:
            f.write(page(*figs))
        print('страница:', out)


if __name__ == '__main__':
    main()
