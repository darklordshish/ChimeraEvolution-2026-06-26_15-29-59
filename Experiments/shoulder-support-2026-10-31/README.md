# Проба опорных костей подмышки

31.10.2026 · Codex · по запросу геймдизайнера «под крыльями скелет поставить» / «продолжишь».

**Результат отрицательный для приёмки:** простая дополнительная опора даёт ограниченное улучшение, но не закрывает деформацию. Прототип сохранён для изучения, не для замены игрового рига.

Пакет и таблицы: [сравнение](../../Референсы/работа/дизайн/shoulder-support-2026-10-31/README.md).

Вход — `../podzharyj-c10-2026-10-31/c13.blend`, `Поджарый=1`, корректор `Поджарый·ПодъёмПлеч=0`. Геометрия и существующие формы-ключи не правятся. Новые веса меняют деформацию других ключей: до любого использования в игре необходима отдельная перевязка/проверка, как требует прежний опыт с К2.

## Воспроизведение

Blender 5.2, PowerShell 7, из корня рабочей папки. Пути в командах — обычные аргументы.

```powershell
$b = 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe'
$t = 'Experiments/shoulder-support-2026-10-31'
$src = 'Experiments/podzharyj-c10-2026-10-31/c13.blend'
& $b -b --factory-startup --python-exit-code 1 -P "$t/benchmark.py" -- $src "$t/scapula-benchmark.json" --helpers
& $b -b --factory-startup --python-exit-code 1 -P "$t/build_support_rig.py" -- $src "$t/helper-weights.npz" "$t/helper-benchmark.json" "$t/support.blend" "$t/support-build.json"
& $b -b --factory-startup --python-exit-code 1 -P "$t/verify_rig.py" -- $src "$t/support.blend" "$t/rig-verification.json"
& $b -b --factory-startup --python-exit-code 1 -P Experiments/podzharyj-c10-2026-10-31/eksport_k2.py -- "$t/support.blend" "$t/support.fbx"
```

Для кадра после экспорта (префиксы выходов должны быть абсолютными, как требует стенд):

```powershell
$root = (Get-Location).Path
& $b -b --factory-startup --python-exit-code 1 -P "$t/render_probe.py" -- "$root/$t/support.fbx" --kluch Поджарый --ves 1 --test_pose raised --raised_angle 100 --support "$root/$t/helper-benchmark.json" --kadr "$root/$t/support100" --otchet "$root/$t/support100.json"
```

Baseline — тот же вызов на `c13.fbx`, без `--support`. Для движения вперёд: `--pose_axis X --raised_angle -60`; назад — `--pose_axis X --raised_angle 35`. В текущем адаптере `--test_pose raised` включает механизм диагностики, а ось задаётся явно. Это не клип Animator и не игровая анимация.

`render_probe.py` проверяемо адаптирует замороженный стенд c13 в памяти, не меняя его исходник: добавляет ось X и движение helper-костей. `support_motion.py` нужен для движения опор: сами по себе новые кости в FBX не знают, как следовать за рукой. В прототипе нет автоматического драйвера Unity.

## Файлы

- `benchmark.py` — исходный риг с разными движениями лопатки; затем 32 конфигурации двух промежуточных опор, с ограничением до четырёх влияний **до** измерения.
- `helper-benchmark.json`, `scapula-benchmark.json` — все результаты и настройки, а не только выбранные числа.
- `helper-weights.npz` — веса выбранного осторожного варианта с явным порядком имён костей.
- `support.blend`, `support.fbx` — настоящий риг 53 кости вместо 51. В сохранении ключи нулевые; для просмотра включить `Поджарый=1`. Корректор плеча выключен.
- `verify_rig.py`, `rig-verification.json` — независимое сравнение геометрии, ключей, старых костей, нейтрального результата и ограничения весов.
- `check_result.py` — заранее записанный критерий полезности: ≥25% снижения складок, ни одна проверенная поза не хуже, швы закрыты. Он **падает** на текущем результате; снижать порог ради зелёного теста не стали.

Отдельные команды выполнены; полностью новый автоматический конвейер игры не создавался. Unity и игровые тесты не запускались.
