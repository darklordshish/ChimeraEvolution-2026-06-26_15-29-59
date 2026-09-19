# Лес s3d: настоящий второй канал — emission телеграфа

- Статус: ВЫПОЛНЕНА · решения ДЕЙСТВУЮТ (PlayMode 105/105 18.09; спред вместо двух точек)
- Отменяет: —
- Отменена: —
- Продолжает: `2026-09-18-forest-s3c-telegraph-pulse.md`
- Ревью связки: `designer-artdirector` (принять с условиями) + `tester` (план вернуть → учтено)

## Проблема

Пульс s3c модулирует тот же `_BaseColor`, который съедает туман. Нужен канал,
независимый от тумана. Аудит: `_EmissionColor` в шейдере URP/Lit есть, но keyword
`_EMISSION` выключен во всех 12 материалах существ — MPB в пустоту.

## Решение (s3d: keyword + emission в пульс-блоке)

- `EmissionSetup` (`Scripts/Forest/Climate/`, entry для `run_script`): включает `_EMISSION`
  на всех материалах `Assets/_Chimera/Materials` (shared, emission чёрный — вид 1:1,
  только кодом). Проверка — тестом, не grep.
- `Telegraph.Apply`: в пульс-блоке пишет `_EmissionColor = pulseColor × (amp×wave)` через MPB;
  вне пульса — явно чёрный (залипший glow = брак). Без keyword материалы игнорят —
  вид не меняется. Палитра только из `Telegraph`, яркость ×1 (без кадра bloom не трогаем).
- Тесты: EditMode `EmissionKeywordTests` (все `.mat` папки с keyword, ≥12 штук) +
  PlayMode `TelegraphEmissionTests` (туман — осцилляция非黑, ясно — чёрный, статусы — чёрный;
  `Insight` пиним true + сброс, чтения разнесены на полупериод 0.25с при 2Гц).

Принято от связки: emission вместо hull + keyword кодом + сначала код, потом тест +
ассеты тестом + фаза полупериодом + veil-цель как в s3c.

## Инварианты

EditMode 1 тест (keyword на всех материалах) + PlayMode 3 теста (осцилляция/чёрный/чёрный).

## Что НЕ делаем

- Unlit-hull (ломает силуэт), bloom-буст ×2 (без кадра — кислота), свет/VOLUME (s7),
  правки статусов.

## Приёмка

`run_tests --mode playmode` зелёный (3 новых) + `run_tests --mode editor` без регрессий.
Плейтест не нужен. `Docs/СТАТУС.md` — за `docs-keeper` при мердже.
