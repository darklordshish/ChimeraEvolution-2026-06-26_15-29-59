# Морфология химер (из данных) — План реализации

> **Воркфлоу проекта (не TDD):** Unity, автотестов нет. Верификация = пользователь запускает Play и репортит; **коммит только после плейтеста**. `main`. Шаги — чекбоксы `- [ ]`.
> **Замечание по контексту:** план написан на исходе недельного лимита; КОД исполняется свежим заходом. Перед каждой задачей исполнитель СВЕРЯЕТ точную структуру по факту (`SpeciesBootstrap` — как задаются органы; `CreatureBody.Awake` — как собирается `renderers`, стр. ~158; `WolfPrefab.BuildBlocky`/`AttachWolfHead`).

**Спека:** `Docs/superpowers/specs/2026-07-31-morfologiya-himer.md`.

**Goal:** модель тела собирается из данных (`шасси-скелет + органы-части`) в рантайме и пересобирается при эволюции; проверка — вервольф из данных.

**Architecture:** `Organ` несёт куб-часть (`visualPart` + scale/offset/euler); `SpeciesSO.skeleton` — якоря шасси (`part`→localPos+size). `MorphBuilder` по надетым органам создаёт кубы-части на якорях шасси. `CreatureBody.Recompute` зовёт сборку; `renderers` пере-собираются для тинта.

**Tech Stack:** Unity 6, C#, `SpeciesSO`/`Organ` (Player/SpeciesSO.cs), `SpeciesBootstrap` (Editor), `CreatureBody`, `GameObject.CreatePrimitive`.

Три коммита: (1) данные-морфологии; (2) MorphBuilder; (3) интеграция+кубы.

---

## Задача 1: Данные морфологии (`Organ.visual*` + `SpeciesSO.skeleton`)

**Files:** Modify `Player/SpeciesSO.cs`; Modify `Editor/SpeciesBootstrap.cs`

- [ ] **1.1 Поля визуала в `Organ`** (в конец класса `Organ`, SpeciesSO.cs):
```csharp
    // МОРФОЛОГИЯ (data-driven модель): видимая КУБ-ЧАСТЬ органа. Пустой visualPart = НЕВИДИМЫЙ (Сердце/Чутьё)
    public string visualPart;                 // имя якоря на скелете шасси (по смыслу слота: "морда"/"передние"/"хвост"/"корпус"/"макушка"/"голова"...)
    public Vector3 visualScale = Vector3.one; // габариты куба-части
    public Vector3 visualOffset;              // локальное смещение от якоря
    public Vector3 visualEuler;               // локальный поворот
```

- [ ] **1.2 Скелет в `SpeciesSO`** (поле + класс `SkeletonAnchor`):
```csharp
    // СКЕЛЕТ (data-driven морфология): именованные ЯКОРЯ — куда MorphBuilder крепит куб-части органов.
    // Позиции под РОСТ/ПОЗУ вида: человек прямоходящий (голова вверху, руки по бокам), волк 4-ногий
    public SkeletonAnchor[] skeleton;
```
```csharp
[System.Serializable]
public class SkeletonAnchor
{
    public string part;                    // имя якоря (совпадает с Organ.visualPart)
    public Vector3 localPos;               // локальная позиция места на теле
    public Vector3 baseSize = Vector3.one; // базовый габарит места (часть масштабируется относительно него)
}
```

- [ ] **1.3 Заполнить Волка и Человека в `SpeciesBootstrap`.** СВЕРИТЬ, как там задаются органы, и по образцу дописать `visualPart`/`visualScale`/… каждому ВИДИМОМУ органу + массив `skeleton` шасси:
  - **Человек** (прямоходящий): якоря `голова` (верх), `корпус` (торс), `руки` (по бокам, ×2 позиции или одна), `ноги` (низ, ×2). Органы: Голова→`голова`, Руки(кисть/меч)→`руки`, Ноги→`ноги`, торс/Шкура→`корпус`.
  - **Волк** (4-ногий): якоря `морда` (впереди-низко), `корпус`, `передние`/`задние` (лапы), `хвост` (сзади). Органы: Пасть→`морда`, Коготь(Руки)→`передние`, Ноги→`задние`, Хвост→`хвост`, Шкура→`корпус`. Сердце/Нюх — `visualPart` пусто (невидимы).
  - Числа (позиции/размеры) — грубо, из `BuildBlocky` как ориентир; тюнинг плейтестом.

- [ ] **1.4 ⚠️ ПОЛЬЗОВАТЕЛЬ:** «Chimera → Создать дефолтные виды» + **Ctrl+S** (новые поля из бутстрапа; без прогона — пустые, [[chimera-bootstrap-gate]]). Проверить в ассете Волк/Человек, что `visualPart`/`skeleton` заполнены.

- [ ] **1.5 Компиляция ОК.** Коммит откладываем до Задачи 2 (данные без сборки не видны).

---

## Задача 2: `MorphBuilder` — сборка модели из состава

**Files:** Create `Player/MorphBuilder.cs` (или `Morph/MorphBuilder.cs`)

- [ ] **2.1 Сборщик.** По надетым органам + скелету шасси строит кубы-части под контейнером `"Morph"`:
```csharp
using System.Collections.Generic;
using UnityEngine;

/// <summary>МОРФОЛОГИЯ: собирает КУБ-МОДЕЛЬ тела из данных — часть каждого видимого органа на якоре ШАССИ.
/// Скелет от шасси (поза/рост), форма от органа. Пересобирается при смене состава (эволюция). Кубы грубо.</summary>
public static class MorphBuilder
{
    const string Container = "Morph";

    // wornParts: пары (Organ, вид-донор-для-цвета). Шасси-фёрст: родной орган на part перекрывает химерный дубль.
    public static void Build(Transform root, SpeciesSO chassis, IReadOnlyList<Organ> wornOrgans)
    {
        // снести прошлую сборку
        var old = root.Find(Container);
        if (old != null) Object.Destroy(old.gameObject);
        if (chassis == null || chassis.skeleton == null) return;

        var container = new GameObject(Container);
        container.transform.SetParent(root, false);

        var usedParts = new HashSet<string>(); // шасси-фёрст: один part — одна часть
        foreach (var organ in wornOrgans)
        {
            if (organ == null || string.IsNullOrEmpty(organ.visualPart)) continue; // невидимый орган
            if (!usedParts.Add(organ.visualPart)) continue;                         // part занят (родной раньше химерного — порядок гарантирует вызывающий)
            var anchor = FindAnchor(chassis, organ.visualPart);
            if (anchor == null) continue;                                            // нет якоря под part у шасси → часть не рисуется (форм-лимит)

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            if (cube.TryGetComponent<Collider>(out var col)) Object.Destroy(col);    // визуал без физики (коллайдер — у CharacterController)
            cube.name = organ.organName;
            var t = cube.transform;
            t.SetParent(container.transform, false);
            t.localPosition = anchor.localPos + organ.visualOffset;
            t.localRotation = Quaternion.Euler(organ.visualEuler);
            t.localScale = Vector3.Scale(anchor.baseSize, organ.visualScale);
        }
    }

    static SkeletonAnchor FindAnchor(SpeciesSO chassis, string part)
    {
        foreach (var a in chassis.skeleton) if (a != null && a.part == part) return a;
        return null;
    }
}
```
- [ ] **2.2 Порядок «шасси-фёрст».** Вызывающий (`CreatureBody`, Задача 3) передаёт органы так, чтобы РОДНЫЕ шли ПЕРЕД химерными (тогда `usedParts` займёт part родным). СВЕРИТЬ порядок обхода slots.
- [ ] **2.3 Компиляция ОК.**

---

## Задача 3: Интеграция в `CreatureBody` + кубы Волк/Человек

**Files:** Modify `Player/CreatureBody.cs` (или `.Tint.cs`); Modify `Editor/WolfPrefab.cs`

- [ ] **3.1 Вызов сборки из `Recompute`.** В `CreatureBody.Recompute` (рядом с `UpdateTint`, конец): собрать список надетых органов (родные первыми) и позвать `MorphBuilder.Build(transform, chassis, worn)`:
```csharp
        // МОРФОЛОГИЯ: пересобрать куб-модель из состава (родные органы ПЕРЕД химерными — шасси-фёрст)
        var worn = new List<Organ>();
        foreach (var sl in slots) if (!sl.Empty && sl.Worn != null && !sl.Pick.native) { } // ... сверить доступ к slot.Pick/Worn (в CreatureBody.Slots.cs — приватный Slot; возможно нужен публичный слепок)
        // ПОДХОД: собрать worn из GetSlot/GetVariants публичного API ИЛИ добавить internal-хелпер, отдающий надетые органы.
        MorphBuilder.Build(transform, chassis, worn);
```
*(СВЕРИТЬ: `Slot`/`Organ` доступ. `slots` приватны в `CreatureBody.Slots.cs` — но `Recompute` в том же классе (partial), доступ есть. Собрать `worn`: сначала все НЕ-химерные-слоты (родные+донорские в слотах шасси), потом химерные. `sl.Worn` = надетый `Organ`.)*

- [ ] **3.2 Пере-собрать `renderers` для тинта.** После `MorphBuilder.Build` части — новые рендереры; `renderers` (собран в Awake, стр. ~158) их не знает → тинт не покрасит. Пере-собрать `renderers` (та же `FindAll GetComponentsInChildren<Renderer>` с исключениями лица), затем `UpdateTint`. СВЕРИТЬ порядок: Build → пере-сбор renderers → UpdateTint (→ Telegraph.Rebase).

- [ ] **3.3 Вернуть кубы Волк/Человек.** В `WolfPrefab` — отключить FBX (`TryAttachModel` → всегда `BuildBlocky`, либо параметр). ИЛИ: убрать статичный `BuildBlocky` вовсе — модель теперь строит `MorphBuilder` из органов (но тогда шасси без органов пусто; безопаснее оставить BuildBlocky как «скелет-плейсхолдер», части-органы поверх). РЕШИТЬ при исполнении, глядя на результат. Координация с модельным чатом (не наступить на FBX).

- [ ] **3.4 ⚠️ ПОЛЬЗОВАТЕЛЬ:** переген видов (если 1.4 не сделан) + перекомпиляция → свежий **Play**.

- [ ] **3.5 ВЕРИФИКАЦИЯ (плейтест):**
  - **вервольф** (dev-спавн) собран из данных: человечий скелет + волчьи морда/лапы/хвост/шкура — узнаваемо, не месиво;
  - **чистый волк** — волчьи части на волчьем скелете;
  - **динамика:** химеризуй (эволюция/конструктор) — части появляются/меняются на лету;
  - **форм-лимит:** орган без якоря — часть не появляется, без ошибок;
  - **тинт:** части красятся по составу, телеграфы работают.

- [ ] **3.6 КОММИТЫ** (тематически): данные-морфологии (Задача 1) / MorphBuilder (Задача 2) / интеграция+кубы (Задача 3).

---

## Риски / гочи
- **Доступ к надетым органам из `Recompute`** (§3.1) — `slots`/`Slot` приватны, но `Recompute` в том же partial-классе → доступ есть; собрать `worn` аккуратно (родные первыми для шасси-фёрст).
- **`renderers` не знает морф-части** (§3.2) — пере-собрать после Build, иначе тинт не покрасит новые кубы. КЛЮЧЕВОЙ шаг.
- **Пересборка каждый `Recompute`** — снос/постройка кубов; при эволюции часто. Пока грубо (полная пересборка); диф/троттлинг — если тормозит.
- **Человек↔волк скелеты** (2-ногий/4-ногий) — part маппит по смыслу; вервольф должен выйти узнаваемым. Числа якорей — тюнинг плейтестом.
- **BuildBlocky vs MorphBuilder** — двойной визуал (старые кубы + новые части)? Решить: BuildBlocky как плейсхолдер-скелет ИЛИ полностью на органы-части. §3.3.
- **Бутстрап-гоча** — переген обязателен ([[chimera-bootstrap-gate]]).

## Self-review (покрытие спеки)
- §2.1 данные (Organ.visual + skeleton) → Задача 1 ✅ · §2.2 MorphBuilder-сборка → Задача 2 ✅ · §2.3 конфликт/шасси-фёрст → Задача 2 (usedParts) + 3.1 (порядок) ✅ · §2.4 тинт поверх → Задача 3.2 ✅ · §2.5 кубы Волк/Человек → Задача 3.3 ✅.
- Вервольф-проверка (§6) → 3.5 ✅. Деферы (виды/красота/анимация) — не в задачах ✅.
- **Помечены места сверки при исполнении** (SpeciesBootstrap, доступ к slots, renderers-пересбор, BuildBlocky-решение) — свежий заход уточняет по факту.
