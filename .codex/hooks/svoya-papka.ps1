# СВОЯ ПАПКА (решение геймдизайнера 24.09; правило — Docs/ЗОНЫ.md, «Рабочие папки и ветки»). Сессия линии пишет файлы
# только в своей папке: чужой линии — письмом, общее — через координатора. Разрешены ещё память и черновики Claude.
# Хук PreToolUse на Edit/Write/NotebookEdit: код выхода 2 — запись запрещена, текст ошибки видит Claude.
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$in = [Console]::In.ReadToEnd() | ConvertFrom-Json
$p = $in.tool_input.file_path
if (-not $p) { $p = $in.tool_input.notebook_path }
if (-not $p) { exit 0 }
$root = $env:CLAUDE_PROJECT_DIR
if (-not $root) { $root = $in.cwd }
function Norm([string]$x) { [IO.Path]::GetFullPath(($x -replace '/', '\')).TrimEnd('\').ToLowerInvariant() }
$full = Norm $p
$allowed = @((Norm $root), (Norm (Join-Path $env:USERPROFILE '.claude')), (Norm $env:TEMP), 'c:\temp\claude')
foreach ($a in $allowed) { if ($full -eq $a -or $full.StartsWith($a + '\')) { exit 0 } }
[Console]::Error.WriteLine("Запись в $p запрещена: это вне папки сессии ($root). Одна линия — одна папка (Docs/ЗОНЫ.md): чужой линии — письмом, общий файл — через координатора.")
exit 2
