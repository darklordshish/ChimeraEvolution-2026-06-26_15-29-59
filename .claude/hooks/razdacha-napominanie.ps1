# НАПОМИНАНИЕ О РАЗДАЧЕ МОДЕЛЕЙ (геймдизайнер 10.11: «вписать на видное место, чтобы не забывать»). Хук PreToolUse на
# Edit/Write: основная сессия впервые за сессию правит код (.cs, .shader, .py, .ps1, .sh) — в контекст приходит
# напоминание: код по готовому плану отдаётся Sonnet (скилл razdacha-modelej), себе — решения, план и проверка.
# Ничего не запрещает (выход 0); субагентам не мешает — у них в данных хука есть agent_id.
[Console]::OutputEncoding = [Text.Encoding]::UTF8
try { $in = [Console]::In.ReadToEnd() | ConvertFrom-Json } catch { exit 0 }
if ($in.agent_id) { exit 0 }
$p = $in.tool_input.file_path
if (-not $p -or $p -notmatch '\.(cs|shader|hlsl|py|ps1|sh)$') { exit 0 }
$mark = Join-Path $env:TEMP ("chimera-razdacha-" + $in.session_id)
if (Test-Path $mark) { exit 0 }
New-Item -ItemType File -Path $mark -Force | Out-Null
$msg = 'Раздача моделей (скилл razdacha-modelej): ты правишь код в основной сессии. Если это код по готовому плану — отдай его субагенту на Sonnet с точным планом, а себе оставь решения, план и проверку результата. Мелкая правка в пару строк или отладка по ходу разговора — можно самому.'
@{ hookSpecificOutput = @{ hookEventName = 'PreToolUse'; additionalContext = $msg } } | ConvertTo-Json -Compress
exit 0
