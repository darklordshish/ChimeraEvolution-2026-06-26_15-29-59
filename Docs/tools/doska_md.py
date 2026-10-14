"""Выгрузка Доски CHIMERA в репозиторий — `Docs/ДОСКА.md`, для тех, кто не видит артефакт (Codex и любой читатель репо).

Источник правды — доска (https://claude.ai/artifact/7pSC1QEqeNcZaB1pJP2Au3), этот файл — её снимок. Порядок:
  1) ArtifactData list по коллекциям `lines` и `questions` с out_dir=<папка> — получаются <папка>/lines/*.json,
     <папка>/questions/*.json;
  2) python Docs/tools/doska_md.py <папка>            → пишет Docs/ДОСКА.md.
Строка дизайн-линии (Codex) приходит обратно через `Docs/доска/codex.md` в его папке — см. ЗОНЫ, «Доска CHIMERA».
"""
import json
import os
import sys
from datetime import datetime

LINES = [('geimboss', 'ГеймБосс — координатор и механики'), ('modelnyj', 'Модельный — модельная линия'),
         ('lesnik', 'Лесник — лесная линия'), ('codex', 'Codex — дизайн-линия')]
STATUS = {'open': 'ждёт ответа', 'answered': 'отвечен', 'dropped': 'снят'}


def load(folder):
    out = {}
    if os.path.isdir(folder):
        for name in sorted(os.listdir(folder)):
            if name.endswith('.json'):
                out[name[:-5]] = json.load(open(os.path.join(folder, name), encoding='utf-8'))
    return out


def when(iso):
    if not iso:
        return '—'
    try:
        t = datetime.fromisoformat(iso.replace('Z', '+00:00')).astimezone()
        return t.strftime('%d.%m %H:%M')
    except ValueError:
        return iso


def main(db, out):
    lines, questions = load(os.path.join(db, 'lines')), load(os.path.join(db, 'questions'))
    md = ['# ДОСКА CHIMERA — снимок', '',
          '> **Файл генерируется** `Docs/tools/doska_md.py` из доски https://claude.ai/artifact/7pSC1QEqeNcZaB1pJP2Au3 —',
          '> руками не править. Дизайн-линия (Codex) пишет свою строку и вопросы в `Docs/доска/codex.md` своей папки,',
          '> координатор переносит их на доску. Правило — `Docs/ЗОНЫ.md`, «Доска CHIMERA».', '',
          f'Снято: {datetime.now().strftime("%d.%m.%Y %H:%M")}', '', '## Линии', '']
    for key, title in LINES:
        l = lines.get(key, {})
        md += [f'### {title}', f'*обновлено {when(l.get("updated"))}*', '',
               f'- **Делаю:** {l.get("doing") or "—"}', f'- **Дальше:** {l.get("next") or "—"}',
               f'- **Жду:** {l.get("waiting") or "—"}', '']
    md += ['## Вопросы геймдизайнеру', '']
    order = sorted(questions.items(), key=lambda kv: (kv[1].get('status') != 'open', kv[1].get('asked', '')))
    if not order:
        md.append('Вопросов нет.')
    for qid, q in order:
        md += [f'### {qid} — {STATUS.get(q.get("status"), q.get("status"))} (от {q.get("from", "?")}, {when(q.get("asked"))})',
               '', q.get('text', ''), '']
        if q.get('answer'):
            md += [f'**Ответ геймдизайнера ({when(q.get("answered"))}):** «{q["answer"]}»', '']
    with open(out, 'w', encoding='utf-8', newline='\n') as f:
        f.write('\n'.join(md).rstrip() + '\n')
    print(f'{out}: линий {len(lines)}, вопросов {len(questions)}')


if __name__ == '__main__':
    root = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    main(sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else os.path.join(root, 'Docs', 'ДОСКА.md'))
