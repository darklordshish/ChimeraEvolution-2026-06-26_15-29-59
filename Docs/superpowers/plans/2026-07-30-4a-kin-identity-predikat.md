# #4a Кин-идентичность предикатом — План реализации

> **Воркфлоу проекта (не TDD):** Unity, автотестов нет. Верификация = пользователь запускает Play и репортит; **коммит только после плейтеста**. Claude пишет C#. Работаем на `main`. Шаги — чекбоксы `- [ ]`.

**Спека:** `Docs/superpowers/specs/2026-07-30-4a-kin-identity-predikat.md`.

**Goal:** заменить кин-тип-чеки (`GetComponentInParent<XxxPsyche>`) на тир-осознанный предикат идентичности `IsKin` — поведение-сохраняюще, как фундамент под #4b.

**Architecture:** новый инстанс-хелпер `CreatureBody.IsKin(other, minTier=Weak)` = `Regard(this, other) >= minTier`. Психики (Moose/Wolf/Werewolf) в кин-местах берут `CreatureBody` соседа и зовут `ownBody.IsKin(...)` вместо тип-чека. У чистых видов `Regard` = None/Strong → любой порог даёт прежнее поведение.

**Стек:** Unity 6, C#, партиал `CreatureBody`, `enum KinTier { None, Weak, Medium, Strong }` (порядковый).

Каждая задача — отдельный тематический коммит после плейтеста.

---

### Задача 1: Хелпер `IsKin` (без смены поведения)

**Files:** Modify `Assets/_Chimera/Scripts/Player/CreatureBody.Identity.cs`

- [ ] **1.1 Добавить хелпер** рядом с `Regard`/`Tier`:
```csharp
// «признаёт ли ЭТО тело во мне своего» — тир-осознанно. Порог Weak по умолчанию (внутривидовое узнавание
// лениво). Химере даёт градиент: чем больше моего вида в теле, тем выше тир. У чистых видов — None/Strong
public bool IsKin(CreatureBody other, KinTier minTier = KinTier.Weak) => other != null && Regard(this, other) >= minTier;
```
- [ ] **1.2 Компиляция** — консоль без ошибок (хелпер пока никто не зовёт, поведение то же). Коммит откладываем до Задачи 2 (тематически одно).

---

### Задача 2: Кин-тип-чеки → `IsKin` (поведение-сохраняюще)

Заменить в психиках. Паттерн: `col.GetComponentInParent<XxxPsyche>()` → `col.GetComponentInParent<CreatureBody>()` + `ownBody.IsKin(thatBody)`, сохраняя ЛОГИКУ места. У каждой психики уже есть своё тело — если не закэшировано как `ownBody`, взять `GetComponent<CreatureBody>()` в Awake/лениво (как в HedgehogPsyche, где `ownBody` уже есть для `Regard`).

**Files:** Modify `Assets/_Chimera/Scripts/Enemies/MoosePsyche.cs`, `WolfPsyche.cs`, `WerewolfPsyche.cs`

- [ ] **2.1 Проверить/добавить `ownBody`** в Moose/Wolf/Werewolf (у Hedgehog/Snake уже есть). Ленивый геттер или кэш в Awake: `CreatureBody ownBody; ... TryGetComponent(out ownBody);`.

- [ ] **2.2 MoosePsyche** — 3 места:
  - «своих (лосей) не пугаем» (×2, `GetComponentInParent<MoosePsyche>() == null` → пугать): заменить на `!ownBody.IsKin(moraleBody)` где `moraleBody = morale.GetComponentInParent<CreatureBody>()`.
  - «найти собрата» (`GetComponentInParent<MoosePsyche>()`): найти кина — `var mb = col.GetComponentInParent<CreatureBody>(); if (mb != null && mb != ownBody && ownBody.IsKin(mb)) ...`.

- [ ] **2.3 WolfPsyche** — 3 кин-места (НЕ строку странного шума — она в Задаче 3):
  - «найти волка-собрата» (спасение), «счёт стаи», «разлёт стаи» — все `GetComponentInParent<WolfPsyche>()` → `CreatureBody` + `ownBody.IsKin(mb)` (исключая себя: `mb != ownBody`).

- [ ] **2.4 WerewolfPsyche** — 1 место:
  - «своих не глушим» (`GetComponent<WolfPsyche>() == null` → глушить): `!ownBody.IsKin(hpBody)` где `hpBody = hp.GetComponent<CreatureBody>()`.

- [ ] **2.5 ПОЛЬЗОВАТЕЛЬ:** перекомпиляция, консоль чистая. **Play** + проверь — **чистые виды НЕ изменились:**
  - Волчья стая собирается / спасает собрата / разлетается как раньше.
  - Лось рёвом НЕ пугает лосей (кин), пугает чужих.
  - Вервольф войом НЕ глушит волков (кин).

- [ ] **2.6 КОММИТ** (Задачи 1+2):
```bash
git add Assets/_Chimera/Scripts/Player/CreatureBody.Identity.cs Assets/_Chimera/Scripts/Enemies/MoosePsyche.cs Assets/_Chimera/Scripts/Enemies/WolfPsyche.cs Assets/_Chimera/Scripts/Enemies/WerewolfPsyche.cs
git commit -m "рефактор: кин-идентичность предикатом IsKin (тип-B #4a) — поведение то же"
```

---

### Задача 3: Странный шум волка (гоча с игроком)

**Files:** Modify `Assets/_Chimera/Scripts/Enemies/WolfPsyche.cs` (строка ~142)

- [ ] **3.1 Заменить** `bool strange = src.GetComponentInParent<SnakePsyche>() != null || src.GetComponentInParent<MoosePsyche>() != null;` на предикат «не-кин источник»:
```csharp
var srcBody = src.GetComponentInParent<CreatureBody>();
bool strange = srcBody != null && !ownBody.IsKin(srcBody); // не-свой звук странен (змея/лось — да, волк — нет)
```

- [ ] **3.2 ПОЛЬЗОВАТЕЛЬ:** Play + проверь:
  - Волк реагирует на шум змеи/лося как раньше (любопытство/настороженность).
  - **Проверь шум ИГРОКА:** не-волчистый игрок теперь может стать «странным» для волка (по спеке §3). Если поведение неприятное — вернёмся, добавим явное «src не игрок» (`src.GetComponent<PlayerController>() == null`).

- [ ] **3.3 КОММИТ:**
```bash
git add Assets/_Chimera/Scripts/Enemies/WolfPsyche.cs
git commit -m "рефактор: странный шум волка через IsKin (тип-B #4a) — не-кин источник"
```

---

## Self-review (покрытие спеки)

- Спека §2 (IsKin) → Задача 1 ✅ · §3 кин-кластер (Moose/Wolf/Werewolf) → Задача 2 ✅ · странный шум → Задача 3 ✅.
- НЕ в #4a (добыча/угроза, голос, дев-тулзы) — намеренно не в плане ✅.
- #4b-задел (вневидовая классификация) — только в спеке, не кодим ✅.
- Гоча странного шума (игрок) — вынесена отдельной задачей с явной проверкой ✅.
- Поведение-сохраняюще — контрольный плейтест 2.5 (чистые виды не изменились) ✅.
