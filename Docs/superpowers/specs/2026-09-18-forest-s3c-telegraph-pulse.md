# Лес s3c: пульс-акцент телеграфа (честное имя вместо «обводки»)

- **Статус:** ВЫПОЛНЕНА · решения ДЕЙСТВУЮТ (PlayMode 98/98 18.09: pulse, не outline)
- **Отменяет:** —

- Продолжает: `2026-09-18-forest-s3-night-weather.md`
- Ревью связки: `designer-artdirector` (не принять как второй канал) + `combat-senses` (условно ЗА)

## Проблема

В тумане телеграф гаснет вместе с hue. Нужен акцент, выживающий без смены шейдера
(настоящий второй канал — emission/unlit — требует проверки материалов, отдельно).

## Решение (s3c: пульс в проверенном канале + честный нейминг)

- `TelegraphChannels`: `outlineColor/outlineWidth` → `pulseColor/pulseAmp (0,1]` + `pulseFreq`
  (мёртвое поле убрано; правка s3-файла и его теста — тот же фиче-ряд).
- `Telegraph`: `SetPulse/ClearPulse` + `Update` с early-out (не armed/не active/не intent —
  return; дефолт — поведение 1:1). Гейт — видимость <0.8, отпуск >0.9 (гистерезис),
  амплитуда рампой 0.85→0.4. Осцилляция итога (рест→градиент→veil) к `pulseColor` —
  градиент стадийных не ломается, veil не сливает hue, статусы (`intent=false`) молчат.
- События (`Set/Clear/Rebase/HitFlash`) пишут статикой — last-writer за событием,
  следующий кадр Update продолжает пульс.

Принято от связки: не «обводка», а пульс-акцент + рампа/гистерезис + осцилляция поверх
veil-цели + MPB read-back в тестах (прецедент `Rebase`).

## Инварианты (PlayMode `Tests/PlayMode/Forest/Climate/TelegraphPulseTests.cs`, 3 теста)

Куб + URP/Lit + `RebuildRenderers`: `Pulse_OscillatesInFog` (два кадра различаются),
`NoPulse_InClear` (статика), `Statuses_DontPulse` (intent=false молчит).

## Что НЕ делаем

- Emission/unlit-обводку (нужна ревизия материалов), видео-приёмку (кадры парой + пульс
  яркостный — читается и при дальтонизме), правки статусов/tense.

## Приёмка

`run_tests --mode playmode` зелёный (3 новых) + `run_tests --mode editor` без регрессий
(правка s3-теста каналов учтена). Плейтест не нужен.
`Docs/СТАТУС.md` — за `docs-keeper` при мердже.
