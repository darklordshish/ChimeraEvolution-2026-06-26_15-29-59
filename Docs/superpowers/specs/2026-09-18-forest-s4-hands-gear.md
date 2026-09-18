# Лес s4: руки человека — изолированный модуль предметов (без хука в бой)

- Статус: ВЫПОЛНЕНА · решения ДЕЙСТВУЮТ (EditMode 138/138 + 1 старый inconclusive 18.09)
- Отменяет: —
- Отменена: —
- Ревью связки: `combat-senses` (форма принята) + `tester` (принять)

## Проблема

Голый человек слабее любого зверя, а предмету в руках не на что опереться: нет ни тиров,
ни прочности, ни формы удара, совместимой с `LimbStrikeData`. Хук в `PlayerAttack` трогать
нельзя (чужие файлы рид-онли) — сначала изолированный словарь.

## Решение (s4: только данные + математика, хук — s4b с разрешения)

`Scripts/Forest/Gear/` (всё чистое, деф — plain `[Serializable]`-класс, не SO):

- `HandItemKind`: Bare/Club/Spear/Torch/BoneKnife/BoneSpear/ChimeraTrophy.
- `HandItemDef`: tier T0..T3 + та же форма, что `LimbStrikeData`
  (damage/knockForce/bleedStacks/range/halfAngle/staminaCost/cooldown/windupTime —
  `windupTime` нужен NPC-доставке) + `backstabMult` (копьё ×3, нож ×10; что такое засада —
  флаг снаружи, из чувств/стелса) + `durabilityMax` (0 = не ломается) + `spoilSeconds`
  (0 = вечный; химерный трофей портится временем) + `fuelSeconds` (факел горит, не бьётся) +
  `twoHanded` + `lightRadius` (только факел).
- `HandItemInstance`: def + `durabilityLeft/spoilLeft/fuelLeft`; `Use()` (удар) и `Tick(dt)`
  (время); сломан (прочность/порча/топливо на нуле) → откат к голым. Голые не ломаются.
- `HandsLoadout`: main/off; двуручное занимает оба; факел только в off + одноручное.
- `HandStrikeResolver` (чистый): `Resolve(def, broken, isBackstab, power)` → `HandStrike`
  {damage, damageMult, knock, bleed, range, halfAngle, stamina, cooldown, windup}.
  `power` НЕ печётся в int — отдаётся в `damageMult` (иначе двойное округление у `Deliver`).
  Голые: damage 10 / range 1.6 / halfAngle 60 (форма как запись).
- `HandItemRules.Validate`: неотрицательности, range>0, halfAngle 0..180, backstab≥1,
  факел — свет и топливо, двуручный факел запрещён.

Принято от связки: windup в выходе + mult отдельно + порча/топливо в дефе + plain-деф.
Риск записан: ×10 нож — ваншот-риск по боссам, перед хуком прогнать числом (s4b).

## Инварианты (EditMode `Tests/EditMode/Forest/Gear/HandsTests.cs`, 13 тестов)

Голые по умолчанию; копьё ×3 только в спине; нож ×10 в спине и база без; сломанный → голые;
прочность 3→0; голые не ломаются; порча и топливо по времени; двуручное глушит off;
факел только off; негативы ловятся; чистота резолвера.

## Что НЕ делаем

- Хук в `PlayerAttack`/`Install` (s4b, матрица: спереди/сзади, сломанное → 10, двуручное глушит
  off; темп/кулдаун не трогать). Силки-ловушки (отложенный Constrict, s4b). Цвета/свет факела (арт).

## Приёмка

`run_tests --mode editor` зелёный (13 новых) + дифф без чужих файлов.
Плейтест не нужен. `Docs/СТАТУС.md` — за `docs-keeper` при мердже.
