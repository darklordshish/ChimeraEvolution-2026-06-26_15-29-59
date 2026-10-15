"""Стоимость задачи и лимиты подписок — для доски («Расход: Claude и Codex»).

Два источника, одна запись `costs/<задача>` на доске (поле `agent`: claude | codex):

  codex  — сессия Codex ~/.codex/sessions/**/rollout-*-<id>.jsonl: последнее событие `token_count` даёт накопленный
           расход сессии и снимок лимитов подписки (окно 5 часов и неделя); картинки — ~/.codex/generated_images/<id>/.
           Пишет и `limits/codex`.
  claude — журнал сессии Claude Code ~/.claude/projects/<папка>/<id>.jsonl: сумма `usage` ответов модели за отрезок
           времени задачи. Лимиты подписки Claude в журнале не пишутся — их координатор берёт инструментом get_usage
           и передаёт числами (`--limit5h`, `--week`, `--resets5h`, `--resetsweek`, `--plan`).

«Токены» — то, что модель обработала заново: вход + запись в кэш + выход; чтение кэша — отдельно (`tokens_cached`):
оно дешевле, но у длинной сессии на него приходится почти всё.

    python Docs/tools/usage_cost.py codex  <id|путь> <задача> <итог> <линия> <папка вывода> [--before 5ч%]
    python Docs/tools/usage_cost.py claude <id|путь> <задача> <итог> <линия> <папка вывода> --from ISO --to ISO
                                           [--before 5ч%] [--limit5h % --week % --resets5h ISO --resetsweek ISO --plan P]
Пишет <папка>/cost.json (и limits.json), их координатор отправляет на доску ArtifactData с file_path.
"""
import argparse
import glob
import json
import os
from datetime import datetime, timezone

CODEX = os.path.expanduser('~/.codex')
CLAUDE = os.path.expanduser('~/.claude/projects')


def find(ref, pattern):
    if os.path.exists(ref):
        return ref
    hits = sorted(glob.glob(pattern.format(ref=ref), recursive=True))
    if not hits:
        raise SystemExit(f'сессии {ref} нет')
    return hits[-1]


def iso(ts):
    return datetime.fromtimestamp(ts, timezone.utc).isoformat() if ts else None


def codex(a):
    path = find(a.ref, os.path.join(CODEX, 'sessions', '**', 'rollout-*{ref}*.jsonl'))
    sid = os.path.basename(path)[-42:-6]
    last, started = None, None
    for line in open(path, encoding='utf-8'):
        ev = json.loads(line)
        started = started or ev.get('timestamp')
        p = ev.get('payload') or {}
        if p.get('type') == 'token_count':
            last = p
    if last is None:
        raise SystemExit('в сессии нет token_count')
    u = (last.get('info') or {}).get('total_token_usage') or {}
    rl = last.get('rate_limits') or {}
    pri, sec = rl.get('primary') or {}, rl.get('secondary') or {}
    cached = u.get('cached_input_tokens') or 0
    cost = {'tokens': (u.get('input_tokens') or 0) - cached + (u.get('output_tokens') or 0), 'tokens_cached': cached,
            'tokens_output': u.get('output_tokens'), 'session': sid, 'started': started,
            'images': len(glob.glob(os.path.join(CODEX, 'generated_images', sid, '*'))), 'limit_after': pri.get('used_percent')}
    limits = {'agent': 'codex', 'primary_pct': pri.get('used_percent'), 'primary_resets': iso(pri.get('resets_at')),
              'secondary_pct': sec.get('used_percent'), 'secondary_resets': iso(sec.get('resets_at')), 'plan': rl.get('plan_type')}
    return cost, limits


def claude(a):
    path = find(a.ref, os.path.join(CLAUDE, '*', '{ref}.jsonl'))
    new = cached = out = turns = 0
    for line in open(path, encoding='utf-8'):
        ev = json.loads(line)
        t = ev.get('timestamp') or ''
        if not (a.since <= t <= a.until):
            continue
        u = (ev.get('message') or {}).get('usage')
        if ev.get('type') != 'assistant' or not u:
            continue
        turns += 1
        new += (u.get('input_tokens') or 0) + (u.get('cache_creation_input_tokens') or 0) + (u.get('output_tokens') or 0)
        cached += u.get('cache_read_input_tokens') or 0
        out += u.get('output_tokens') or 0
    cost = {'tokens': new, 'tokens_cached': cached, 'tokens_output': out, 'turns': turns, 'session': os.path.basename(path)[:-6],
            'started': a.since, 'images': 0, 'limit_after': a.limit5h}
    limits = None
    if a.limit5h is not None:
        limits = {'agent': 'claude', 'primary_pct': a.limit5h, 'primary_resets': a.resets5h, 'secondary_pct': a.week,
                  'secondary_resets': a.resetsweek, 'plan': a.plan}
    return cost, limits


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('agent', choices=['codex', 'claude'])
    for k in ('ref', 'task', 'result', 'line', 'out'):
        ap.add_argument(k)
    ap.add_argument('--from', dest='since', default='')
    ap.add_argument('--to', dest='until', default='9999')
    ap.add_argument('--before', type=float)
    ap.add_argument('--limit5h', type=float)
    ap.add_argument('--week', type=float)
    ap.add_argument('--resets5h')
    ap.add_argument('--resetsweek')
    ap.add_argument('--plan')
    a = ap.parse_args()
    cost, limits = (codex if a.agent == 'codex' else claude)(a)
    now = datetime.now(timezone.utc).isoformat()
    cost.update({'agent': a.agent, 'line': a.line, 'task': a.task, 'result': a.result, 'finished': now,
                 'limit_before': a.before})
    os.makedirs(a.out, exist_ok=True)
    json.dump(cost, open(os.path.join(a.out, 'cost.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    if limits:
        limits['checked'] = now
        json.dump(limits, open(os.path.join(a.out, 'limits.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    print(json.dumps(cost, ensure_ascii=False))


if __name__ == '__main__':
    main()
