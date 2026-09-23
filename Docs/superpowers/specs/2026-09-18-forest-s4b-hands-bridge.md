# Лес s4b: хук рук в PlayerAttack (предмет бьёт вместо записи)

- **Статус:** ВЫПОЛНЕНА · решения ДЕЙСТВУЮТ (PlayMode 91/91 18.09; болванке нужен коллайдер)
- **Отменяет:** —

- Продолжает: `2026-09-18-forest-s4-hands-gear.md`
- Ревью связки: `combat-senses` (брать) + `tester` (принять)

## Проблема

Предмет в руках не бьёт: `PlayerAttack` всегда читает запись органа. Нужен мост
«предмет → удар» минимальной правкой — иначе s4 остаётся словарём.

## Решение (s4b: мост + 1 ветка в грани + SO-обёртка)

Новый `HandsBridge` (`Scripts/Forest/Gear/`, `MonoBehaviour` на теле игрока):
держит `HandsLoadout` (EquipMain/Off по дефам), `IsBackstab` — выставляемый флаг
(`TODO(чувства)`: истинный источник — стелс/агро; дефолт false = безопасный «в лоб»),
`power` (модификатор индивида, тест ставит явно), `Tick` жжёт топливо/порчу,
`HasHands` (есть main-деф), `ResolveStrike()` (сломан → голые), `SpendSwing()`
(прочность за замах, не за попадание — предсказуемо).

Правка `PlayerAttack.DoAttack` (одна ветка, темп не трогаем): если на теле есть
`HandsBridge` с руками — паёк и конус из `HandStrike` (`damage/range/halfAngle/knock/
bleed` + `damageMult` в `Deliver`, мощь не печём), иначе старый путь записи.
Сломан → голые (урон 10). Пустой мост → путь записи (ноль изменений поведения).

`HandItemDefSO` (`CreateAssetMenu`): SO-обёртка plain-дефа для инспектора
(ассеты — позже, `.gitkeep` остаётся с пометкой).

Принято от связки: ветка вместо наследника-доставки (не дублируем скан/конус, не рвём
`PlayerCarrier`-контракт) + флаг снаружи + пороги ×10-теста 60/120 при HP 30.

## Инварианты (PlayMode `Tests/PlayMode/Forest/Gear/HandsBridgeTests.cs`, 4 теста)

Изолированные тело+болванка на кейс (прецедент `Player_Roll`), `yield null +
SyncTransforms`, хитстоп отпускаем в `TearDown` (`WaitForSecondsRealtime(0.15)` +
`timeScale=1`), кулдаун ждём `tempo+0.05`, стамина не тратится гранью (как сейчас):
`Spear_Front` (8 → HP 22), `Spear_BackstabX3` (флаг → 8×3=24 → HP 6),
`Broken_FallsBackToBare10` (два замаха 8+10 → HP 12), `TwoHanded_MutesOffhand`
(одноручное+факел ок, двуручное глушит off). Меряем полный удар по Health
(before/after Current), `TakeDamage` напрямую не дёргаем.

## Что НЕ делаем

- Силки-Constrict (нужна машина) → s4c. Темп/кулдауны/стамина — не трогаем.
- Истинный IsBackstab из чувств, баланс ×10 (риск записан, число 60/120 — факт).

## Приёмка

`run_tests --mode playmode` зелёный (4 новых) + `run_tests --mode editor` без регрессий.
Плейтест не нужен. `Docs/СТАТУС.md` — за `docs-keeper` при мердже.
