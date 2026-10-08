"""ДЕТЕКТОР КАТАЛОГА РЕФЕРЕНСОВ (спека `2026-10-08-hranilishche-referensov.md`, решение 8).

Каталог `Референсы/КАТАЛОГ.md` — единственный указатель эталонов. Детектор сверяет его с диском в обе стороны:
- файл витрины или архива, которого каталог не называет, — красное (маски покрываются строкой с их папкой `masks/`);
- путь витрины или архива в каталоге, которого нет на диске, — красное.
Паспорта (`*.md`) в витрине — часть объекта, отдельной строки не требуют. «Работа» не сверяется: это черновики линий.

    python Docs/tools/refs_catalog.py        # код выхода 1 — расхождение
"""
import os
import re
import sys

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Референсы")
ROOT = os.path.normpath(ROOT)


def main():
    cat_path = os.path.join(ROOT, "КАТАЛОГ.md")
    if not os.path.exists(cat_path):
        print("нет каталога: " + cat_path)
        return 1
    text = open(cat_path, encoding="utf-8").read()
    named = set(re.findall(r"`((?:витрина|архив)/[^`]+)`", text))
    dirs = {n for n in named if n.endswith("/")}
    files = named - dirs

    bad = []
    on_disk = []
    for tier in ("витрина", "архив"):
        for dp, _, fs in os.walk(os.path.join(ROOT, tier)):
            for f in fs:
                rel = os.path.relpath(os.path.join(dp, f), ROOT).replace(os.sep, "/")
                if rel.endswith(".md") or rel.endswith(".meta"):
                    continue
                on_disk.append(rel)
                if rel not in files and not any(rel.startswith(d) for d in dirs):
                    bad.append("не в каталоге: " + rel)
    for n in sorted(named):
        if not os.path.exists(os.path.join(ROOT, n)):
            bad.append("в каталоге, но нет на диске: " + n)

    # одна роль — один файл: в папке объекта витрины не больше одного `вид.*`, `мышцы.*`, `ступени.*`
    vit = os.path.join(ROOT, "витрина")
    if os.path.isdir(vit):
        for obj in os.listdir(vit):
            od = os.path.join(vit, obj)
            if not os.path.isdir(od):
                continue
            roles = {}
            for f in os.listdir(od):
                if os.path.isfile(os.path.join(od, f)) and not f.endswith((".md", ".meta")):
                    roles.setdefault(os.path.splitext(f)[0], []).append(f)
            for r, fs in roles.items():
                if len(fs) > 1:
                    bad.append(f"витрина/{obj}: роль «{r}» — {len(fs)} файла ({', '.join(fs)}), нужен один")

    print(f"РЕФЕРЕНСЫ: в витрине и архиве {len(on_disk)} файлов, в каталоге {len(named)} путей")
    if bad:
        print("\n".join(bad))
        return 1
    print("ЧИСТО: каталог и диск совпадают")
    return 0


if __name__ == "__main__":
    sys.exit(main())
