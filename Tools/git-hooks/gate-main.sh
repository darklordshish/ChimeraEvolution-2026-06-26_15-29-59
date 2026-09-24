#!/usr/bin/env bash
# ГЕЙТ ПЕРЕД main (решение геймдизайнера 24.09; правило — Docs/ЗОНЫ.md, «Рабочие папки и ветки»). В main уходит только
# проверенное: детектор спек чист, а если задет Unity — EditMode зелёный. Линии публикуют сами, и одна сломанная
# публикация сломала бы main всем остальным. Обходить (`git push --no-verify`) нельзя.
#   CHIMERA_GATE_TESTS=always — гонять тесты при любой публикации (проверка самого гейта).
#   CHIMERA_PYTHON — путь к Python, если его нет среди обычных мест.
set -u
remote="${1:-origin}"
zero=0000000000000000000000000000000000000000
sha=""
while read -r lref lsha rref rsha; do
  [ -z "${lref:-}" ] && continue
  if [ "$rref" = "refs/heads/main" ] && [ "$lsha" != "$zero" ]; then sha="$lsha"; fi
done
[ -n "$sha" ] || exit 0

root=$(git rev-parse --show-toplevel)
cd "$root" || exit 1
say() { printf '[гейт main] %s\n' "$*" >&2; }
fail() { say "СТОП: $*"; exit 1; }

# проверяется ровно то, что уходит: публикуется HEAD, а рабочее дерево чистое
[ "$(git rev-parse HEAD)" = "$sha" ] || fail "публикуется не HEAD ($sha) — переключись на коммит, который отправляешь"
dirty=$(git status --porcelain -- Assets Packages ProjectSettings Docs Tools/Agent)
[ -z "$dirty" ] || fail "есть незакоммиченное — тесты проверили бы не то, что уходит:
$dirty"

# 1. детектор спек
py=""
for c in "${CHIMERA_PYTHON:-}" /c/ProgramData/anaconda3/python.exe "$HOME/anaconda3/python.exe" python3 python; do
  [ -n "$c" ] && "$c" -c "import sys" >/dev/null 2>&1 && { py="$c"; break; }
done
[ -n "$py" ] || fail "не нашёл Python для spec_status.py (задай CHIMERA_PYTHON)"
out=$(PYTHONIOENCODING=utf-8 "$py" Docs/tools/spec_status.py 2>&1) || fail "детектор спек красный:
$out"
say "детектор спек чист"

# 2. EditMode — если публикация задевает Unity
base=$(git merge-base "$sha" "refs/remotes/$remote/main" 2>/dev/null || true)
if [ "${CHIMERA_GATE_TESTS:-}" != "always" ] && [ -n "$base" ] &&
   ! git diff --name-only "$base" "$sha" | grep -qE '^(Assets|Packages|ProjectSettings|Tools/Agent)/'; then
  say "Unity не задет — тесты не нужны"
  exit 0
fi

P="--project-path $root"
if unity command set_autotick --enable true $P 2>&1 | grep -q "No Pipeline"; then
  # редактор этой папки закрыт — прогон в batchmode, он сам выходит по окончании
  ver=$(sed -n 's/^m_EditorVersion: //p' ProjectSettings/ProjectVersion.txt | tr -d '\r')
  exe="/c/Program Files/Unity/Hub/Editor/$ver/Editor/Unity.exe"
  res="$root/Temp/gate-editmode.xml"
  rm -f "$res"
  say "редактор закрыт — EditMode в batchmode (несколько минут)…"
  "$exe" -batchmode -projectPath "$root" -runTests -testPlatform EditMode -testResults "$res" -logFile "$root/Temp/gate-editmode.log" >/dev/null 2>&1
  [ -f "$res" ] || fail "тесты не запустились — смотри Temp/gate-editmode.log (открыт ли проект в другом Unity?)"
  head=$(grep -o '<test-run[^>]*>' "$res" | head -1)
  total=$(printf '%s' "$head" | grep -o ' total="[0-9]*"' | grep -o '[0-9]*')
  failed=$(printf '%s' "$head" | grep -o ' failed="[0-9]*"' | grep -o '[0-9]*')
else
  # редактор открыт — через него: сначала пересобрать скрипты и дождаться конца сборки, иначе прогон пойдёт на старой
  # сборке (поймано 23.09: recompile_status отдал прошлое «completed», и прошло 130 тестов вместо 238)
  say "EditMode через открытый редактор…"
  unity command eval --code 'UnityEditor.AssetDatabase.Refresh(); UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation(); return "ok";' $P >/dev/null 2>&1
  sleep 5
  idle=0
  for _ in $(seq 1 200); do
    r=$(unity command eval --code 'return (UnityEditor.EditorApplication.isCompiling || UnityEditor.EditorApplication.isUpdating) ? "busy" : "idle";' $P 2>&1 | tail -1)
    case "$r" in *'"result":"idle"'*) idle=$((idle + 1)); [ $idle -ge 2 ] && break ;; *) idle=0 ;; esac
    sleep 3
  done
  [ $idle -ge 2 ] || fail "редактор не закончил сборку скриптов"
  rm -f "$root/Temp/pipeline_test_status.json"
  unity command run_tests --mode editor --async_tests true $P >/dev/null 2>&1
  sleep 5
  s=""
  for _ in $(seq 1 300); do
    s=$(unity command test_status $P 2>&1 | tail -1)
    case "$s" in *'"status": "completed"'*) break ;; esac
    sleep 3
  done
  case "$s" in *'"status": "completed"'*) ;; *) fail "тесты не закончились" ;; esac
  total=$(printf '%s' "$s" | grep -o '"total": [0-9]*' | head -1 | grep -o '[0-9]*')
  failed=$(printf '%s' "$s" | grep -o '"failed": [0-9]*' | head -1 | grep -o '[0-9]*')
fi
[ -n "${total:-}" ] && [ "$total" -gt 0 ] || fail "тесты не прогнались (0 тестов)"
[ "${failed:-1}" = "0" ] || fail "EditMode: упало $failed из $total"
say "EditMode зелёный: $total тестов, упавших нет"
exit 0
