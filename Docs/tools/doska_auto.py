"""Автосводка для Доски CHIMERA: последние коммиты каждой линии и свежий снимок лимитов Codex — готовыми JSON для
ArtifactData (`auto/<линия>`, `limits/codex`). Запускает задача по расписанию «doska-refresh», когда на доске нажата
кнопка «Попросить линии обновить»; тексты строк линий («делаю / дальше») она не трогает — их пишут сами линии.

    python Docs/tools/doska_auto.py <папка вывода>
"""
import glob
import json
import os
import subprocess
import sys
from datetime import datetime, timezone

LINES = {'geimboss': 'main', 'modelnyj': 'line/modeli', 'lesnik': 'line/les', 'codex': 'line/codex'}


def commits(ref, n=4):
    out = subprocess.run(['git', 'log', ref, f'-{n}', '--format=%h%x1f%cI%x1f%s'], capture_output=True, text=True, encoding='utf-8')
    rows = []
    for line in out.stdout.splitlines():
        h, d, s = line.split('\x1f', 2)
        rows.append({'hash': h, 'date': d, 'subject': s[:200]})
    return rows


def codex_limits():
    for path in sorted(glob.glob(os.path.expanduser('~/.codex/sessions/*/*/*/rollout-*.jsonl')), key=os.path.getmtime, reverse=True)[:20]:
        last, at = None, None
        for line in open(path, encoding='utf-8'):
            ev = json.loads(line)
            p = ev.get('payload') or {}
            if p.get('type') == 'token_count' and (p.get('rate_limits') or {}).get('primary'):
                last, at = p['rate_limits'], ev.get('timestamp')
        if last:
            iso = lambda t: datetime.fromtimestamp(t, timezone.utc).isoformat() if t else None
            pri, sec = last.get('primary') or {}, last.get('secondary') or {}
            return {'agent': 'codex', 'primary_pct': pri.get('used_percent'), 'primary_resets': iso(pri.get('resets_at')),
                    'secondary_pct': sec.get('used_percent'), 'secondary_resets': iso(sec.get('resets_at')),
                    'plan': last.get('plan_type'), 'checked': at}
    return None


def main(out):
    os.makedirs(out, exist_ok=True)
    now = datetime.now(timezone.utc).isoformat()
    for line, ref in LINES.items():
        json.dump({'branch': ref, 'commits': commits(ref), 'at': now}, open(os.path.join(out, f'auto-{line}.json'), 'w', encoding='utf-8'), ensure_ascii=False)
    lim = codex_limits()
    if lim:
        json.dump(lim, open(os.path.join(out, 'limits-codex.json'), 'w', encoding='utf-8'), ensure_ascii=False)
    print('готово:', out)


if __name__ == '__main__':
    main(sys.argv[1])
