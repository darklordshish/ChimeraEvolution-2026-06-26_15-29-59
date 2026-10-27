from pathlib import Path
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.patches import FancyBboxPatch, FancyArrowPatch

OUT = Path("Docs/les/графы")
OUT.mkdir(parents=True, exist_ok=True)
plt.rcParams["font.family"] = "DejaVu Sans"

BLUE = "#3b6ea5"
GREEN = "#2e7d32"
ORANGE = "#d97706"
RED = "#c62828"
GRAY = "#607d8b"
PURPLE = "#6a1b9a"
BROWN = "#795548"
GOLD = "#b8860b"
INK = "#263238"
PAPER = "#fbfaf6"


def canvas(title, sub=""):
    fig, ax = plt.subplots(figsize=(16, 10))
    fig.patch.set_facecolor(PAPER)
    ax.set_facecolor(PAPER)
    ax.set_xlim(-8, 8)
    ax.set_ylim(-6.8, 5.2)
    ax.axis("off")
    ax.set_title(title, fontsize=18, color=INK, fontweight="bold", pad=18)
    if sub:
        ax.text(0, 4.95, sub, ha="center", va="top", fontsize=10.5, color="#455a64")
    return fig, ax


def node(ax, x, y, label, color="#dce9f5", edge=INK, w=1.8, h=0.82, fs=10):
    box = FancyBboxPatch((x - w / 2, y - h / 2), w, h,
                         boxstyle="round,pad=0.07,rounding_size=0.12",
                         linewidth=1.4, edgecolor=edge, facecolor=color)
    ax.add_patch(box)
    ax.text(x, y, label, ha="center", va="center", fontsize=fs, color=INK, zorder=5)


def edge(ax, p1, p2, label="", color=GRAY, style="-", rad=0.0, lw=1.6, fs=8.2, lo=(0, 0.14)):
    a = FancyArrowPatch(p1, p2, arrowstyle="-|>", mutation_scale=13,
                        linewidth=lw, color=color, linestyle=style,
                        connectionstyle=f"arc3,rad={rad}", zorder=2)
    ax.add_patch(a)
    if label:
        mx, my = (p1[0] + p2[0]) / 2, (p1[1] + p2[1]) / 2
        ax.text(mx + lo[0], my + lo[1], label, ha="center", va="center",
                fontsize=fs, color=color, bbox=dict(facecolor=PAPER, edgecolor="none", pad=1.1), zorder=6)


def note(ax, text, fs=9.4):
    ax.text(-7.7, -4.75, text, ha="left", va="top", fontsize=fs, color=INK,
            bbox=dict(facecolor="#fff8e1", edgecolor="#e0c97f", boxstyle="round,pad=0.45"),
            zorder=9)


def save(fig, name):
    for ext in ("svg", "png"):
        fig.savefig(OUT / f"{name}.{ext}", dpi=170, bbox_inches="tight", facecolor=fig.get_facecolor())
    plt.close(fig)


# ---------------------------------------------------------------- 01 current
fig, ax = canvas("01. Текущий граф павильона: дерево, петель нет",
                 "Сид 1337. Длины — NavMesh; красный пунктир — PathPartial.")
p = {
    "Entry": (-6.9, -2.5), "Den2": (-5.9, -0.4), "Lake": (-1.9, -2.0),
    "LabDoor": (4.4, -1.4), "Atrium": (6.2, -0.2), "Terrace": (5.0, -3.6),
    "Mouth": (0.2, -3.9), "Lip": (2.4, -3.9), "Ash": (-2.4, 3.2),
    "Den0": (-6.3, 3.4), "Den1": (-4.5, 4.4),
}
node(ax, *p["Entry"], "Вход", "#d7e8f7")
node(ax, *p["Den2"], "Устье 2", "#f3e5d8")
node(ax, *p["Lake"], "Озеро", "#cfe8f3")
node(ax, *p["LabDoor"], "LabDoor", "#d7e8f7")
node(ax, *p["Atrium"], "LabAtrium", "#d7e8f7")
node(ax, *p["Terrace"], "Терраса", "#e8e0c9", w=1.7)
node(ax, *p["Mouth"], "Устье\nканьона", "#e8e0c9", w=1.6)
node(ax, *p["Lip"], "Лип", "#e8e0c9", w=1.4)
node(ax, *p["Ash"], "Ясень", "#d9ead3")
node(ax, *p["Den0"], "Устье 0", "#f3e5d8")
node(ax, *p["Den1"], "Устье 1", "#f3e5d8")

edge(ax, p["Entry"], p["Lake"], "806 м", BLUE, lo=(0, -0.2))
edge(ax, p["Lake"], p["LabDoor"], "614 м", BLUE, lo=(0, -0.2))
edge(ax, p["LabDoor"], p["Atrium"], "18 м", BLUE, lo=(0.25, 0.1))
edge(ax, p["LabDoor"], p["Terrace"], "634 м", BLUE, rad=-0.2, lo=(-0.2, 0.2))
edge(ax, p["Terrace"], p["Lip"], "73 м", BLUE, lo=(0, -0.2))
edge(ax, p["Lip"], p["Mouth"], "", RED, "--")
edge(ax, p["Mouth"], p["Lake"], "берег", BLUE, lo=(0, 0.15))
edge(ax, p["Entry"], p["Den2"], "43 м", GREEN, lo=(0.2, 0.1))
edge(ax, p["Den2"], p["Lake"], "", GREEN, rad=0.1)
edge(ax, p["Den0"], p["Den1"], "", GREEN)
edge(ax, p["Den0"], p["Lake"], "", GREEN, rad=0.15)
edge(ax, p["Den1"], p["Lake"], "", GREEN, rad=0.2)
edge(ax, p["Ash"], p["LabDoor"], "PathPartial", RED, "--", rad=-0.15, lo=(0, 0.2))
note(ax, "Нет ни одного цикла между районами: все связи — ветки дерева.\n"
         "Красные пунктиры — не доказательство разрыва, а длинный запрос\n"
         "или остров NavMesh (ясень, лип). Это не граф петель.")
save(fig, "01-tekushchij-graf")

# ---------------------------------------------------------------- 02 station loop
fig, ax = canvas("02. Петля A: сквозь станцию",
                 "Зелёный путь — 40–45 м через зал; серый — 82 м вокруг. Нужна северная дверь 4×4.")
p = {
    "Entry": (-6.4, -1.6), "Lake": (-4.0, -1.6), "Door": (-1.2, 0.0),
    "Atrium": (0.9, 0.9), "Arena": (2.9, 0.9), "North": (4.9, 0.0),
    "Terrace": (4.9, 2.8),
}
node(ax, *p["Entry"], "Вход", "#d7e8f7", w=1.7)
node(ax, *p["Lake"], "Озеро", "#cfe8f3", w=1.7)
node(ax, *p["Door"], "LabDoor", "#d7e8f7", w=1.7)
node(ax, *p["Atrium"], "LabAtrium", "#d7e8f7", w=1.7)
node(ax, *p["Arena"], "Арена", "#d7e8f7", w=1.5)
node(ax, *p["North"], "NorthDoor\n(новая)", "#d9ead3", w=1.9)
node(ax, *p["Terrace"], "Терраса", "#e8e0c9", w=1.8)
edge(ax, p["Entry"], p["Lake"], "806 м", BLUE, lo=(0, -0.2))
edge(ax, p["Lake"], p["Door"], "614 м", BLUE, lo=(0, -0.2))
edge(ax, p["Door"], p["Atrium"], "18 м", GREEN, lw=2.4, lo=(0.0, 0.1))
edge(ax, p["Atrium"], p["Arena"], "зал", GREEN, lw=2.4, lo=(0, 0.15))
edge(ax, p["Arena"], p["North"], "40–45 м", GREEN, lw=2.4, rad=0.05, lo=(0.2, 0.1))
edge(ax, p["North"], p["Door"], "82 м вокруг", GRAY, rad=-0.5, lw=2.0, lo=(0, -0.3))
edge(ax, p["North"], p["Terrace"], "к каньону", BLUE, lo=(0.15, 0.1))
note(ax, "Петля LabDoor → Atrium → Arena → NorthDoor → снаружи → LabDoor.\n"
         "Экономия 37–42 м. Главное — второй выход станции: юг к озеру, север к каньону.\n"
         "Цена: одна дверь, NavMesh, проверка NPC. Риск низкий.\n"
         "Меняет формулировку «станция закрыта» — это решение геймдизайнера.")
save(fig, "02-petlya-stanciya")

# ---------------------------------------------------------------- 03 canyon
fig, ax = canvas("03. Петля B/C: каньон — верх и низ",
                 "Сегодня каньон — тупик. Кромка 389 м короче каньона 420 м. После закрытия кромки — наоборот.")
p = {
    "Entry": (-6.5, -2.2), "Lake": (-4.2, -2.2), "Mouth": (-1.6, -2.2),
    "Floor": (0.4, 0.0), "Lip": (3.6, 0.0), "Terrace": (5.4, 1.4),
    "Station": (5.4, -1.0),
}
node(ax, *p["Entry"], "Вход", "#d7e8f7", w=1.7)
node(ax, *p["Lake"], "Озеро", "#cfe8f3", w=1.7)
node(ax, *p["Mouth"], "Устье\nканьона", "#e8e0c9", w=1.8)
node(ax, *p["Floor"], "Пол\n300 м", "#e8e0c9", w=1.5)
node(ax, *p["Lip"], "Лип", "#e8e0c9", w=1.4)
node(ax, *p["Terrace"], "Терраса", "#e8e0c9", w=1.8)
node(ax, *p["Station"], "Станция", "#d7e8f7", w=1.8)
edge(ax, p["Entry"], p["Lake"], "806 м", BLUE, lo=(0, -0.2))
edge(ax, p["Lake"], p["Mouth"], "берег", BLUE, lo=(0, -0.2))
edge(ax, p["Mouth"], p["Floor"], "300 м", BLUE, lo=(-0.2, 0.15))
edge(ax, p["Floor"], p["Lip"], "рампа 120 м\n(нет)", GREEN, "--", lw=2.2, lo=(0, 0.35))
edge(ax, p["Lip"], p["Terrace"], "73 м", BLUE, lo=(0, 0.15))
edge(ax, p["Terrace"], p["Mouth"], "кромка 389 м\n(обход)", ORANGE, lw=2.2, rad=0.15, lo=(0.4, -0.3))
edge(ax, p["Lip"], p["Floor"], "", RED, "--", lw=2.2, rad=0.4)
edge(ax, p["Terrace"], p["Station"], "634 м", BLUE, lo=(0.2, 0.0))
note(ax, "B: рампа на липе + закрыть кромку → каньон становится коротким путём (экономия ≈389 м).\n"
         "C: сброс с липа — только после анти-фолла по месту, one-way link, save и теста softlock.\n"
         "Сейчас лип — остров NavMesh, а сброс ниже спавна телепортирует игрока вверх.")
save(fig, "03-petlya-kanon")

# ---------------------------------------------------------------- 04 ecosystem
fig, ax = canvas("04. Живая экосистема: роли, ресурсы и петли",
                 "Сплошные стрелки — давление/питание; пунктир — разрыв или желаемая связь.")
node(ax, -5.0, 2.5, "Вода", "#cfe8f3", w=1.7)
node(ax, 0.0, 3.5, "Корм", "#d9ead3", w=1.7)
node(ax, 5.0, 2.5, "Логова", "#f3e5d8", w=1.9)
node(ax, 6.3, 4.3, "Сова\n(позже)", "#dbeafe", w=2.0)
node(ax, 3.0, 0.0, "Волк\nстая", "#f8d7da", w=1.9)
node(ax, -2.2, 0.0, "Змея\nзасада", "#e1d5e7", w=2.0)
node(ax, 0.0, -2.5, "Лось\nтанк", "#e6d3c4", w=1.9)
node(ax, -4.2, -2.5, "Ёж\nиглы", "#ffe0b2", w=1.9)
edge(ax, (2.8, -0.2), (0.3, -2.2), "стая 5", RED, lw=2.0, lo=(0.25, 0.1))
edge(ax, (2.5, -0.1), (-3.8, -2.2), "добыча", RED, lw=1.5, rad=0.1, lo=(0, 0.2))
edge(ax, (-3.9, -2.1), (-2.4, 0.2), "иглы", ORANGE, lw=2.0, lo=(-0.1, 0.15))
edge(ax, (0.0, -2.1), (0.0, 3.0), "корм", GREEN, lw=1.5, lo=(0.2, 0.0))
edge(ax, (-3.8, -2.1), (0.0, 3.0), "", GREEN, lw=1.2, rad=0.1)
edge(ax, (3.3, 0.4), (4.5, 2.1), "дом", BROWN, lw=1.8, lo=(0.2, 0.0))
edge(ax, (-1.8, 0.4), (4.5, 2.1), "укрытие", PURPLE, lw=1.5, rad=-0.15, lo=(0, 0.2))
edge(ax, (5.8, 4.1), (0.6, 3.3), "позже", BLUE, "--", lw=1.1, rad=0.1, lo=(0, 0.2))
edge(ax, (4.05, 2.5), (-4.15, 2.5), "нет связи 751–949 м", RED, "--", lw=1.5, lo=(0, 0.2))
edge(ax, (4.5, 2.2), (0.0, 3.0), "нет общих мест", ORANGE, "--", lw=1.3, rad=-0.2, lo=(0, 0.2))
ax.text(0, -4.9, "Желаемая петля: логово → вода → корм → логово. Сейчас её нет: логова далеко от воды,\n"
                 "а деревья и вода не являются укрытием/преградой.",
        ha="center", va="top", fontsize=9.6, color=GREEN)
save(fig, "04-ekosistema")

# ---------------------------------------------------------------- 05 role audit
fig, ax = plt.subplots(figsize=(16, 9))
fig.patch.set_facecolor(PAPER)
ax.set_facecolor(PAPER)
ax.axis("off")
ax.set_title("05. Аудит ролей: вид × бой × пространство × петля",
             fontsize=18, color=INK, fontweight="bold", pad=18)
cols = ["Вид", "Бой", "Пространство", "Петля / роль в экосистеме"]
rows = [
    ("Волк", "стая, 2/5, кольцо 5.5/9", "арена 18–30 м", "преследователь"),
    ("Змея", "засада, термо 14 м", "стена ≤12 м, насест 5 м", "засадник"),
    ("Лось", "масса, таран 25.5 м", "прямая ≥26 м", "танк/травоядный"),
    ("Ёж", "иглы, анти-змея", "узость или дуга", "колючая добыча"),
    ("Сова (позже)", "эхо, обзор", "высота, крона", "вертикальный охотник"),
    ("Игрок-химера", "адаптивный набор", "камера 6/7 м", "угроза/эволюция"),
]
table = ax.table(cellText=rows, colLabels=cols, cellLoc="left",
                 bbox=[0.03, 0.18, 0.94, 0.62])
table.auto_set_font_size(False)
table.set_fontsize(12.0)
for (r, c), cell in table.get_celld().items():
    cell.set_edgecolor("#cfd8dc")
    cell.set_facecolor("#ffffff" if r % 2 else "#f5f7f6")
    if r == 0:
        cell.set_facecolor("#dce9f5")
        cell.set_text_props(weight="bold", color=INK)
ax.text(0.5, 0.08,
        "Вывод: у каждого вида уже есть роль, но карта не даёт ей места. Петля должна быть не «дорогой», а сценой роли:\n"
        "волку — арена, змее — стена, лосю — прямая, ежу — узость, сове — высота.",
        transform=ax.transAxes, ha="center", va="center", fontsize=11.5, color=INK)
save(fig, "05-audit-rolej")

# ---------------------------------------------------------------- 06 combined
fig, ax = canvas("06. Сводное предложение: петли + экосистема",
                 "Зелёный — станция, синий — каньон, оранжевый — экология, красный — будущий one-way.")
node(ax, -6.6, -2.4, "Вход", "#d7e8f7", w=1.7)
node(ax, -4.2, -1.4, "Озеро", "#cfe8f3", w=1.7)
node(ax, -1.4, 0.2, "Станция", "#d7e8f7", w=1.8)
node(ax, 1.4, 1.8, "Терраса", "#e8e0c9", w=1.8)
node(ax, 3.0, -0.2, "Каньон", "#e8e0c9", w=1.8)
node(ax, 5.3, 0.4, "Ясень", "#d9ead3", w=1.7)
node(ax, -5.4, 2.6, "Логова", "#f3e5d8", w=1.8)
node(ax, 0.0, 3.4, "Вода/корм", "#d9ead3", w=2.0)
node(ax, 4.4, 3.1, "Сова", "#dbeafe", w=1.7)
edge(ax, (-6.6, -2.4), (-4.2, -1.4), "806", BLUE, lo=(0, -0.2))
edge(ax, (-4.2, -1.4), (-1.4, 0.2), "614", BLUE, lo=(0, -0.2))
edge(ax, (-1.4, 0.2), (1.4, 1.8), "станция", GREEN, lw=2.2, lo=(0.1, 0.1))
edge(ax, (1.4, 1.8), (3.0, -0.2), "каньон", BLUE, lw=2.0, lo=(0.2, 0.0))
edge(ax, (3.0, -0.2), (-4.2, -1.4), "устье", BLUE, lw=1.5, rad=0.1, lo=(0, -0.2))
edge(ax, (3.0, -0.2), (1.4, 1.8), "рампа", GREEN, "--", lw=1.7, rad=0.25, lo=(0, -0.3))
edge(ax, (5.3, 0.4), (1.4, 1.8), "ясень", GREEN, lw=1.4, lo=(0, 0.2))
edge(ax, (-5.4, 2.6), (0.0, 3.4), "вода/корм", ORANGE, lw=1.7, lo=(0, 0.2))
edge(ax, (0.0, 3.4), (5.3, 0.4), "корм", ORANGE, lw=1.3, rad=-0.15, lo=(0, 0.2))
edge(ax, (4.4, 3.1), (5.3, 0.4), "сова", BLUE, "--", lw=1.1, lo=(0.2, 0.0))
edge(ax, (3.0, -0.2), (1.4, 1.8), "one-way", RED, "--", lw=2.0, rad=-0.4, lo=(0, 0.3))
note(ax, "Порядок: 1) станция — дешёвая петля; 2) каньон — рампа + закрыть кромку;\n"
         "3) экология — вода/корм/логова; 4) one-way — только с save и возвратом.\n"
         "Это предложение, не решение.")
save(fig, "06-svodnoe-predlozhenie")
print("saved to", OUT)
