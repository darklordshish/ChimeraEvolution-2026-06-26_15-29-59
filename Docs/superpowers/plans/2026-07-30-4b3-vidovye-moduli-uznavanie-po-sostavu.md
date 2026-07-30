# #4b-3 Видовые модули + узнавание по составу — План реализации

> **Воркфлоу проекта (не TDD):** Unity, автотестов нет. Верификация = пользователь запускает Play и репортит; **коммит только после плейтеста**. Claude пишет C#. `main`. Шаги — чекбоксы `- [ ]`.

**Спека:** `Docs/superpowers/specs/2026-07-30-4b3-vidovye-moduli-uznavanie-po-sostavu.md`.

**Goal:** диспатч по доминанте вешает видовую психику (Волк/Змея/Лось/Ёж), альфа — истинным химерам и доминанте-Человек; узнавание (`Regard`) переезжает с шасси на доминанту.

**Architecture:** `Regard` берёт `target.MostKin` вместо `target.Chassis` (чистые виды — no-op, химеры узнаются по составу). `PsycheDispatch` — `switch` по `MostKin.speciesName` → тип психики → `AddComponent`. Видовые психики само-достраивают зависимости, префаб вида не нужен.

**Tech Stack:** Unity 6, C#, `CreatureBody` (партиал), `MostKin`/`Tier`, `AddComponent(System.Type)`.

Два коммита: (1) узнавание-по-составу; (2) диспатч видовых модулей.

---

## File Structure

- **Modify** `Assets/_Chimera/Scripts/Player/CreatureBody.Identity.cs` — `Regard`: `Chassis` → `MostKin`.
- **Modify** `Assets/_Chimera/Scripts/Enemies/PsycheDispatch.cs` — карта доминанта→психика (switch), альфа-фолбэк.

---

## Задача 1: Узнавание по составу (`Regard` → доминанта)

**Files:** Modify `Player/CreatureBody.Identity.cs`

- [ ] **1.1 Поправка `Regard`.** Найти (стр. ~90):
```csharp
    public static KinTier Regard(CreatureBody observer, CreatureBody target) =>
        observer != null && target != null && target.Chassis != null ? observer.Tier(target.Chassis) : KinTier.None;
```
Заменить на:
```csharp
    /// <summary>ЕДИНЫЙ ГЛАГОЛ РОДСТВА: как `observer` признаёт существо `target` — по ДОМИНАНТЕ его состава
    /// (`MostKin`), а не по шасси. «Кто ты» решает идентичность-по-составу: волк-по-идентичности (доминанта
    /// Волк на любом шасси) для стаи волк. Чистые виды: `MostKin == Chassis` → поведение цело (no-op). Истинная
    /// химера (`MostKin`=null) — никому не своя (согласуется с химерой-альфой «монстр для всех»).</summary>
    public static KinTier Regard(CreatureBody observer, CreatureBody target)
    {
        if (observer == null || target == null) return KinTier.None;
        var dom = target.MostKin(out _);                                   // идентичность цели = доминанта состава
        return dom != null ? observer.Tier(dom) : KinTier.None;
    }
```
*(`IsKin` зовёт `Regard` → тоже переезжает на состав автоматически. 3 градации `KinTier` на выходе нетронуты.)*

- [ ] **1.2 Компиляция** — консоль без ошибок.

- [ ] **1.3 ПОЛЬЗОВАТЕЛЬ (РЕГРЕСС обычного леса — критично):** перекомпиляция → свежий **Play** БЕЗ тест-химер. Проверь, что чистые виды ведут себя как раньше:
  - волки собираются в стаю, охотятся на игрока/змей/ежей, не дерутся между собой;
  - лось не пугается сородичей; ёж не сворачивается от своих; змея охотится на тёплых;
  - кин-игрок (нахимеренный под вид) по-прежнему узнаётся стаей (если проверял раньше).
  Узнавание НЕ должно сломаться — для чистых видов правка no-op.

- [ ] **1.4 КОММИТ (Задача 1):**
```bash
git add Assets/_Chimera/Scripts/Player/CreatureBody.Identity.cs
git commit -m "фича: узнавание по составу — Regard смотрит доминанту (MostKin), не шасси (#4b-3)"
```

---

## Задача 2: Диспатч видовых модулей (`PsycheDispatch` карта)

**Files:** Modify `Enemies/PsycheDispatch.cs`

- [ ] **2.1 Карта доминанта→психика.** Заменить тело `Attach`:
```csharp
    public static void Attach(CreatureBody body)
    {
        if (body == null) return;
        var dom = body.MostKin(out var tier);

        // ДОМИНАНТА РЕШАЕТ ВИД: психика-компонент = маркер идентичности (тип-B чеки находят его по нему).
        // Человек (NPC-психики нет) и истинная химера (dom==null) → ХИМЕРА-АЛЬФА («затычка пробела в круге»)
        System.Type psyche = dom?.speciesName switch
        {
            "Волк" => typeof(WolfPsyche),
            "Змея" => typeof(SnakePsyche),
            "Лось" => typeof(MoosePsyche),
            "Ёж"   => typeof(HedgehogPsyche),
            _      => typeof(ChimeraAlphaPsyche),
        };

        Debug.Log(dom != null
            ? $"диспатч: доминанта {dom.speciesName} ({tier}) → {psyche.Name}"
            : "диспатч: истинная химера (кин ни к кому) → химера-альфа");

        body.gameObject.AddComponent(psyche);
        body.Refeed(); // психика навешена ПОСЛЕ Recompute — пере-раздать урон/скорость (иначе OnBodyStats её не застал)
    }
```

- [ ] **2.2 Сверить точные `speciesName` (перед компиляцией):** имена видов в switch должны ТОЧНО совпадать с `SpeciesSO.speciesName` в ассетах (`Assets/_Chimera/Data`) — особенно **«Ёж»** (буква ё, не «Еж»). Ридаут дев-панели показывает их как «Волк/Змея/Лось/Ёж/Человек» — сверить с ассетами при сомнении.

- [ ] **2.3 Компиляция** — консоль без ошибок.

- [ ] **2.4 ПОЛЬЗОВАТЕЛЬ:** перекомпиляция → свежий **Play** → дев-панель **«спавн (случайный состав)»** × несколько.

- [ ] **2.5 ВЕРИФИКАЦИЯ (плейтест):**
  - **Диспатч:** в консоли доминанта → её психика (`WolfPsyche`/`SnakePsyche`/…), не «пока альфа». Ролл даёт видовых + истинных химер (альфа).
  - **Видовое поведение:** доминанта-Волк ведёт себя как волк (стая/тень/прыжок); Змея — засада/яд; Ёж — клубок/залп; Лось — берсерк/таран. (Если психика падает NRE на химере — сказать, добавлю `AddComponent`-guard.)
  - **Узнавание по составу:** волк-по-идентичности признан обычной стаей (не дерётся с ней); истинная химера — чужая всем.
  - **Тип-B маркер:** волк-по-идентичности охотится на змею-по-идентичности (RPS-круг среди химер держится).

- [ ] **2.6 КОММИТ (Задача 2):**
```bash
git add Assets/_Chimera/Scripts/Enemies/PsycheDispatch.cs
git commit -m "фича: диспатч видовых модулей по доминанте (#4b-3)"
```

---

## Self-review (покрытие спеки)
- §2.1 диспатч-карта → Задача 2 ✅ · §2.2 Regard-поправка → Задача 1 ✅ · §2.3 тип-B маркер → верификация 2.5 ✅ · §2.4 носитель — тест-химера #4b-2 (без правок) ✅.
- Риск регресса леса (§5) → отдельный плейтест-гейт 1.3 ПЕРЕД диспатчем ✅.
- Хрупкость имён-строк (§5) → сверка 2.2 ✅.
- Деферы (база психики / соц-градации / сенсорика альфы / боссовость) — не в задачах ✅.
- Типы: `WolfPsyche`/`SnakePsyche`/`MoosePsyche`/`HedgehogPsyche`/`ChimeraAlphaPsyche` — существуют; `AddComponent(System.Type)` — Unity API; `MostKin(out)`/`Refeed()`/`Tier(SpeciesSO)` — публичны ✅.
