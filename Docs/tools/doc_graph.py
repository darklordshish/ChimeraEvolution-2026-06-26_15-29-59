# -*- coding: utf-8 -*-
"""ГРАФ ДОКУМЕНТОВ — детектор ссылок между документами, памятью и скиллами (ресёрч «Многослойная память агентов», 10.11).

ЗАЧЕМ. Точность того, что агент достаёт из документов и памяти, держится не хранилищем, а свежестью и
непротиворечивостью: ссылка на переименованный файл молчит, запись памяти пересказывает отменённое правило, документ
без единой входящей ссылки забыт. Всё это видно только как граф. Отдельная база не нужна: граф строится из самих
markdown-ссылок за доли секунды, источник правды остаётся в git.

Узлы — .md файлы репозитория (кроме служебных папок) и файлы памяти Claude Code, если папка памяти на этой машине есть.
Рёбра — markdown-ссылки `[текст](путь)`, пути в обратных кавычках, оканчивающиеся на `.md`, и `[[имя]]` в памяти.

Проверяет:
  1. битые ссылки — путь не существует (КРАСНОЕ: гейт останавливается);
  2. память ссылается на документ, целиком отменённый в шапке (предупреждение: запись, вероятно, протухла);
  3. сироты — документы в Docs/ без единой входящей ссылки (предупреждение, список).
Ключ `--write` пишет обратные ссылки в `Docs/Диаграммы/ССЫЛКИ.md` (генерируемый файл, руками не правится).

Запуск:  python Docs/tools/doc_graph.py [--write] [--all]
Выход:   0 — битых ссылок нет, 1 — есть.
"""
import io
import os
import re
import sys
import urllib.parse
from collections import defaultdict

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')

HERE = os.path.dirname(os.path.abspath(__file__))
DOCS = os.path.dirname(HERE)
ROOT = os.path.dirname(DOCS)
SKIP_DIRS = {'.git', 'Library', 'Temp', 'Logs', 'obj', 'Build', 'Builds', 'UserSettings', 'node_modules', 'Experiments',
             '.vs', '.idea', 'Packages', 'Архив~', 'Archive~'}
MEMORY = os.path.join(os.path.expanduser('~'), '.claude', 'projects', 'C--Users-semion-Documents-Chimera-game-ChimeraEvolution', 'memory')

MD_LINK = re.compile(r'\[[^\]\n]*\]\(([^)\s]+)\)')
TICK = re.compile(r'`([^`\n]+?\.md)`')
INLINE_CODE = re.compile(r'`[^`\n]*`')
WIKI = re.compile(r'\[\[([a-z0-9-]+)\]\]')
CANCELLED = re.compile(r'⛔\s*ОТМЕН|ОТМЕНЕН[АО]?\s+ЦЕЛИКОМ|\*\*Статус:\*\*[^\n]*решения ОТМЕНЕНЫ', re.I)


def walk(base):
    for d, dirs, files in os.walk(base):
        dirs[:] = [x for x in dirs if x not in SKIP_DIRS and not x.startswith('.') or x in ('.claude',)]
        for f in files:
            if f.endswith('.md'):
                yield os.path.join(d, f)


def norm(p):
    return os.path.normcase(os.path.normpath(p))


def read(p):
    try:
        with open(p, encoding='utf-8') as fh:
            return fh.read()
    except (UnicodeDecodeError, OSError):
        return ''


def strip_code(text):
    return re.sub(r'```.*?```', '', text, flags=re.S)


def main():
    write = '--write' in sys.argv
    show_all = '--all' in sys.argv
    # узлы репозитория — только то, что в git: неотслеживаемые черновики на одной машине не должны менять граф
    import subprocess
    try:
        out = subprocess.run(['git', '-c', 'core.quotePath=false', 'ls-files', '-z', '--', '*.md'], cwd=ROOT,
                             capture_output=True, check=True).stdout.decode('utf-8')
        files = [os.path.join(ROOT, f) for f in out.split('\0') if f and not any(part in SKIP_DIRS for part in f.split('/'))]
    except (OSError, subprocess.CalledProcessError):
        files = list(walk(ROOT))
    mem_files = [os.path.join(MEMORY, f) for f in os.listdir(MEMORY)] if os.path.isdir(MEMORY) else []
    mem_files = [f for f in mem_files if f.endswith('.md')]
    nodes = {norm(f): f for f in files + mem_files}
    by_name = {os.path.splitext(os.path.basename(f))[0]: f for f in mem_files}

    edges = defaultdict(set)      # откуда → куда
    dead = []                     # (откуда, ссылка)
    for src in files + mem_files:
        if os.path.basename(src) == 'ССЫЛКИ.md':
            continue   # сам файл обратных ссылок — отчёт о графе, а не его ребро: иначе сирот не было бы вовсе
        text = strip_code(read(src))
        base = os.path.dirname(src)
        targets = []
        for m in MD_LINK.finditer(INLINE_CODE.sub('', text)):   # ссылка внутри `кода` — пример, а не ссылка
            targets.append(('link', m.group(1)))
        for m in TICK.finditer(text):
            targets.append(('tick', m.group(1)))
        for kind, raw in targets:
            ref = raw.split('#', 1)[0].strip()
            if not ref or re.match(r'^[a-z]+:', ref, re.I) or ref.startswith('<'):
                continue
            ref = re.sub(r':\d+$', '', urllib.parse.unquote(ref))   # `Файл.cs:193` — ссылка на строку
            if 'Experiments/' in ref.replace(os.sep, '/'):
                continue   # эксперименты дизайн-линии намеренно не вливаются в main (ЗОНЫ.md)
            if any(ch in ref for ch in '*?{}<>|') or ref.endswith('/'):
                continue
            cands = [os.path.join(base, ref), os.path.join(ROOT, ref), os.path.join(DOCS, ref)]
            hit = next((c for c in cands if os.path.exists(c)), None)
            if hit:
                if norm(hit) in nodes:
                    edges[norm(src)].add(norm(hit))
            elif kind == 'link':
                dead.append((src, raw))
        if src in mem_files:
            for m in WIKI.finditer(text):
                t = by_name.get(m.group(1))
                if t:
                    edges[norm(src)].add(norm(t))

    inbound = defaultdict(set)
    for s, ts in edges.items():
        for t in ts:
            if t != s:
                inbound[t].add(s)

    rel = lambda p: os.path.relpath(p, ROOT).replace(os.sep, '/') if norm(p).startswith(norm(ROOT)) else 'память/' + os.path.basename(p)

    cancelled = {n for n, f in nodes.items() if CANCELLED.search(read(f)[:1500])}
    stale = sorted({(rel(nodes[s]), rel(nodes[t])) for s, ts in edges.items() if s in map(norm, mem_files) for t in ts if t in cancelled})
    entry = {norm(os.path.join(DOCS, x)) for x in ('СТАТУС.md', 'УКАЗАТЕЛЬ.md', 'README.md')} | {norm(os.path.join(ROOT, 'CLAUDE.md'))}
    orphans = sorted(rel(f) for n, f in nodes.items()
                     if norm(f).startswith(norm(DOCS)) and n not in entry and not inbound.get(n)
                     and os.sep + 'research_notes' + os.sep not in f and os.sep + 'archive' + os.sep not in f)

    print(f'ГРАФ ДОКУМЕНТОВ: узлов {len(nodes)} (память {len(mem_files)}), рёбер {sum(len(v) for v in edges.values())}')
    if stale:
        print(f'\nПРЕДУПРЕЖДЕНИЕ: память ссылается на отменённое ({len(stale)}):')
        for s, t in stale:
            print(f'  {s} → {t}')
    print(f'\nСиротами в Docs/ (нет входящих ссылок): {len(orphans)}' + ('' if show_all else ' — список: --all'))
    if show_all:
        for o in orphans:
            print('  ' + o)

    if write:
        out = os.path.join(DOCS, 'Диаграммы', 'ССЫЛКИ.md')
        lines = ['# Обратные ссылки документов', '',
                 'Генерируется `python Docs/tools/doc_graph.py --write`; руками не правится. Для каждого документа Docs/ — кто на него ссылается (документы, скиллы, память).', '']
        for n, f in sorted(nodes.items(), key=lambda kv: rel(kv[1])):
            if not norm(f).startswith(norm(DOCS)) or os.sep + 'research_notes' + os.sep in f or norm(f) == norm(out):
                continue
            srcs = sorted(rel(nodes[s]) for s in inbound.get(n, ()))
            lines.append(f'- `{rel(f)}` ← ' + (', '.join(f'`{s}`' for s in srcs) if srcs else '**никто**'))
        with open(out, 'w', encoding='utf-8', newline='\n') as fh:
            fh.write('\n'.join(lines) + '\n')
        print(f'\nобратные ссылки записаны: {rel(out)}')

    if dead:
        print(f'\nБИТЫЕ ССЫЛКИ ({len(dead)}):')
        for s, r in dead:
            print(f'  {rel(s)}: {r}')
        return 1
    print('\nЧИСТО: битых ссылок нет')
    return 0


if __name__ == '__main__':
    sys.exit(main())
