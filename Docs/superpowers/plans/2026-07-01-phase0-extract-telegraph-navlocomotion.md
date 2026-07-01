# Фаза 0 — Извлечение `Telegraph` + `NavLocomotion` — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Вынести две продублированные подсистемы (`SetTelegraph`+`mpb` и NavMesh-навигацию) из монолитов `WolfAI`/`WerewolfBoss` в два общих компонента, не меняя наблюдаемого поведения.

**Architecture:** Два новых `MonoBehaviour` — `Telegraph` (подкраска материалов через `MaterialPropertyBlock`) и `NavLocomotion` (направление к цели по NavMesh + точка блуждания). `WolfAI` и `WerewolfBoss` получают ссылки на них и заменяют собственные методы вызовами. Поведение идентично; это фундамент под `CreatureBody`/`Psyche` в следующих фазах.

**Tech Stack:** Unity 6, C#, `UnityEngine.AI` (NavMesh), `MaterialPropertyBlock`.

**Верификация:** автотестов нет — каждая задача проверяется **игрой** (останов Play → перекомпиляция → запуск сцены → сверка поведения через `DebugHud`). Правило: неожиданно поменялось наблюдаемое — стоп и чинить до следующей задачи.

**Scope:** только Фаза 0 из спеки `Docs/superpowers/specs/2026-07-01-creature-abstraction-design.md`. Фазы 1–6 получат свои планы по мере готовности.

---

## File Structure

- **Create** `Assets/_Chimera/Scripts/Combat/Telegraph.cs` — подкраска тела в цвет замаха и возврат исходного (per-renderer `_BaseColor` через MPB). Красит только меши тела, не трогает `TrailRenderer` (запаховый след).
- **Create** `Assets/_Chimera/Scripts/Enemies/NavLocomotion.cs` — направление к точке с учётом стен (троттлинг `NavMesh.CalculatePath`) + случайная точка блуждания.
- **Modify** `Assets/_Chimera/Scripts/Enemies/WolfAI.cs` — удалить дублированные телеграф/навигацию, делегировать компонентам.
- **Modify** `Assets/_Chimera/Scripts/Enemies/WerewolfBoss.cs` — то же.

---

### Task 1: Компонент `Telegraph`

**Files:**
- Create: `Assets/_Chimera/Scripts/Combat/Telegraph.cs`

- [ ] **Step 1: Написать компонент**

```csharp
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Телеграф приёма: красит материалы ТЕЛА в цвет замаха и возвращает исходные.
/// Per-renderer _BaseColor через MaterialPropertyBlock (без инстансинга материалов).
/// Красит только Mesh/SkinnedMesh — TrailRenderer (запаховый след) не трогает.
/// Извлечено из дублей WolfAI/WerewolfBoss (Фаза 0 рефактора существ).
/// </summary>
public class Telegraph : MonoBehaviour
{
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    Renderer[] renderers;
    Color[] baseColors;
    MaterialPropertyBlock mpb;
    bool active;
    Color activeColor;

    void Awake()
    {
        var list = new List<Renderer>();
        foreach (var r in GetComponentsInChildren<Renderer>())
            if (r is MeshRenderer || r is SkinnedMeshRenderer) list.Add(r); // не красим след/линии
        renderers = list.ToArray();

        baseColors = new Color[renderers.Length];
        mpb = new MaterialPropertyBlock();
        for (int i = 0; i < renderers.Length; i++)
        {
            var m = renderers[i].sharedMaterial;
            baseColors[i] = (m != null && m.HasProperty(BaseColor)) ? m.GetColor(BaseColor) : Color.gray;
        }
    }

    /// <summary>Включить/выключить телеграф заданного цвета. Идемпотентно — лишней работы нет.</summary>
    public void Set(bool on, Color color)
    {
        if (on == active && (!on || color == activeColor)) return;
        active = on;
        activeColor = color;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            renderers[i].GetPropertyBlock(mpb);
            mpb.SetColor(BaseColor, on ? color : baseColors[i]);
            renderers[i].SetPropertyBlock(mpb);
        }
    }

    public void Clear() => Set(false, activeColor);
}
```

- [ ] **Step 2: Проверить компиляцию**

В Unity (при фокусе окна) — консоль без красных ошибок. Файл создаёт `.meta` автоматически.

---

### Task 2: Компонент `NavLocomotion`

**Files:**
- Create: `Assets/_Chimera/Scripts/Enemies/NavLocomotion.cs`

- [ ] **Step 1: Написать компонент**

```csharp
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Локомоция по NavMesh для ИИ: направление к точке с учётом стен (троттлинг пересчёта пути)
/// и случайная точка блуждания. Само перемещение (CharacterController) остаётся у существа —
/// здесь только «куда идти». Извлечено из дублей WolfAI/WerewolfBoss (Фаза 0).
/// </summary>
public class NavLocomotion : MonoBehaviour
{
    [SerializeField] float pathInterval = 0.2f;   // как часто пересчитывать путь
    [SerializeField] float pathDirectRange = 5f;  // ближе — идём напрямую, без пафайндинга

    NavMeshPath navPath;
    float nextPathTime;
    Vector3 cachedNavDir;
    Vector3 wanderTarget;
    float nextWanderTime;

    void Awake() => navPath = new NavMeshPath();

    /// <summary>Направление к точке с учётом стен (к следующему углу пути NavMesh).</summary>
    public Vector3 DirTo(Vector3 dest)
    {
        if (navPath == null) navPath = new NavMeshPath(); // страховка от domain-reload в Play
        Vector3 flat = dest - transform.position; flat.y = 0f;
        if (flat.sqrMagnitude < pathDirectRange * pathDirectRange)
            return flat.sqrMagnitude > 0.01f ? flat.normalized : transform.forward;

        if (Time.time >= nextPathTime)
        {
            nextPathTime = Time.time + pathInterval;
            cachedNavDir = Compute(dest);
        }
        return cachedNavDir;
    }

    Vector3 Compute(Vector3 dest)
    {
        if (NavMesh.CalculatePath(transform.position, dest, NavMesh.AllAreas, navPath) && navPath.corners.Length > 1)
        {
            Vector3 d = navPath.corners[1] - transform.position; d.y = 0f;
            if (d.sqrMagnitude > 0.01f) return d.normalized;
        }
        Vector3 fb = dest - transform.position; fb.y = 0f;
        return fb.sqrMagnitude > 0.01f ? fb.normalized : transform.forward;
    }

    /// <summary>Случайная точка на навмеше для блуждания (меняется по таймеру/при достижении).</summary>
    public Vector3 Wander(float radius)
    {
        if (Time.time >= nextWanderTime || (wanderTarget - transform.position).sqrMagnitude < 4f)
        {
            nextWanderTime = Time.time + Random.Range(2f, 5f);
            Vector2 c = Random.insideUnitCircle * radius;
            Vector3 cand = transform.position + new Vector3(c.x, 0f, c.y);
            wanderTarget = NavMesh.SamplePosition(cand, out var hit, 4f, NavMesh.AllAreas) ? hit.position : transform.position;
        }
        return wanderTarget;
    }
}
```

- [ ] **Step 2: Проверить компиляцию** — консоль без ошибок.

---

### Task 3: Перевести `WolfAI` на компоненты

**Files:**
- Modify: `Assets/_Chimera/Scripts/Enemies/WolfAI.cs`

- [ ] **Step 1: Добавить требования компонентов** — к атрибутам класса добавить:

```csharp
[RequireComponent(typeof(Telegraph))]
[RequireComponent(typeof(NavLocomotion))]
```

- [ ] **Step 2: Удалить продублированные поля**

Удалить: `static readonly int BaseColor = ...;`, `Renderer[] renderers;`, `Color[] baseColors;`, `MaterialPropertyBlock mpb;`, `NavMeshPath navPath;`, `float nextPathTime;`, `Vector3 cachedNavDir;`, `Vector3 wanderTarget;`, `float nextWanderTime;`, а также сериализованные `pathInterval` и `pathDirectRange` (переехали в `NavLocomotion`).

Добавить ссылки: `Telegraph telegraph;`, `NavLocomotion nav;`.

- [ ] **Step 3: Обновить `Awake`**

Удалить блок сбора `renderers`/`baseColors`/`mpb` и создание `navPath`. Вместо этого получить компоненты (с защитой):

```csharp
if (!TryGetComponent(out telegraph)) telegraph = gameObject.AddComponent<Telegraph>();
if (!TryGetComponent(out nav)) nav = gameObject.AddComponent<NavLocomotion>();
```

Оставить получение `controller/stagger/knockback/ownHealth` и добавление `ScentTrail` как было.

- [ ] **Step 4: Заменить вызовы телеграфа и навигации**

- Удалить метод `void SetTelegraph(bool on) {...}` целиком.
- Все `SetTelegraph(true)` → `telegraph.Set(true, activeTelegraph)`.
- Все `SetTelegraph(false)` → `telegraph.Clear()`.
- Удалить методы `Vector3 NavDir(Vector3 dest) {...}`, `Vector3 ComputeNavDir(Vector3 dest) {...}`, `Vector3 WanderTarget() {...}`.
- Все `NavDir(x)` → `nav.DirTo(x)`.
- Все `WanderTarget()` → `nav.Wander(wanderRadius)`.
- Убрать `using UnityEngine.AI;`, если после правок он больше не используется (иначе оставить).

- [ ] **Step 5: Проверка игрой**

Останови Play → дай перекомпилиться → запусти сцену. Проверь: волки замахиваются с цветным телеграфом (укус красный / прыжок оранжевый / захват фиолетовый), обходят стены и блуждают в покое **как раньше**. Запаховый след **не** окрашивается в цвет телеграфа. `DebugHud` — без ошибок.

- [ ] **Step 6: Коммит**

```bash
git add Assets/_Chimera/Scripts/Combat/Telegraph.cs Assets/_Chimera/Scripts/Enemies/NavLocomotion.cs Assets/_Chimera/Scripts/Enemies/WolfAI.cs
git commit -m "Фаза 0: WolfAI на общие Telegraph + NavLocomotion"
```

---

### Task 4: Перевести `WerewolfBoss` на компоненты

**Files:**
- Modify: `Assets/_Chimera/Scripts/Enemies/WerewolfBoss.cs`

- [ ] **Step 1: Добавить требования компонентов** — к атрибутам класса добавить `[RequireComponent(typeof(Telegraph))]` и `[RequireComponent(typeof(NavLocomotion))]`.

- [ ] **Step 2: Удалить продублированные поля** — те же, что в Task 3 Step 2: `BaseColor`, `renderers`, `baseColors`, `mpb`, `navPath`, `nextPathTime`, `cachedNavDir`, `wanderTarget`, `nextWanderTime`, сериализованные `pathInterval`, `pathDirectRange`. Добавить `Telegraph telegraph;`, `NavLocomotion nav;`.

- [ ] **Step 3: Обновить `Awake`** — удалить сбор рендереров и `navPath = new(...)`; добавить:

```csharp
if (!TryGetComponent(out telegraph)) telegraph = gameObject.AddComponent<Telegraph>();
if (!TryGetComponent(out nav)) nav = gameObject.AddComponent<NavLocomotion>();
```

- [ ] **Step 4: Заменить вызовы** — удалить `SetTelegraph`, `NavDir`, `ComputeNavDir`, `WanderTarget`; заменить `SetTelegraph(true)` → `telegraph.Set(true, activeTelegraph)`, `SetTelegraph(false)` → `telegraph.Clear()`, `NavDir(x)` → `nav.DirTo(x)`, `WanderTarget()` → `nav.Wander(wanderRadius)`. Убрать неиспользуемый `using UnityEngine.AI;`, если остался только ради удалённого.

- [ ] **Step 5: Проверка игрой**

Останови Play → перекомпиляция → спавни босса (dev-панель / `WerewolfSpawner`). Проверь: телеграф укуса/прыжка/чарджа/воя цветной, чардж на четвереньках догоняет, погоня/блуждание по NavMesh **как раньше**. След не красится.

- [ ] **Step 6: Коммит**

```bash
git add Assets/_Chimera/Scripts/Enemies/WerewolfBoss.cs
git commit -m "Фаза 0: WerewolfBoss на общие Telegraph + NavLocomotion"
```

---

### Task 5: Проверка префабов (ручной шаг в редакторе)

`[RequireComponent]` добавит `Telegraph` и `NavLocomotion` на объекты автоматически, но стоит убедиться, что они появились и на **префабах** `Wolf.prefab` и `Werewolf.prefab` (иначе значения `NavLocomotion` возьмутся дефолтные — что для Фазы 0 совпадает с прежними 0.2/5, так что критично только для будущего тюнинга).

- [ ] Открой `Assets/_Chimera/Prefabs/Wolf.prefab` и `Werewolf.prefab` → убедись, что компоненты `Telegraph` и `NavLocomotion` присутствуют. Если нет — Add Component вручную, сохрани префаб.

---

## Self-Review

- **Покрытие спеки (Фаза 0):** «извлечь `Telegraph` и `NavLocomotion` из дублей; `WolfAI`/`WerewolfBoss` зовут их; поведение то же» — покрыто Task 1–4. ✅
- **Плейсхолдеры:** нет — оба новых файла даны целиком, правки перечислены поимённо. ✅
- **Согласованность имён:** `Telegraph.Set(bool,Color)`/`Clear()`, `NavLocomotion.DirTo(Vector3)`/`Wander(float)` — используются одинаково в Task 3 и 4. ✅
- **Поведение-сохранность:** телеграф теперь фильтрует не-меши (было неявно через порядок захвата рендереров до добавления `ScentTrail`) — наблюдаемо идентично. `NavLocomotion.DirTo` содержит защиту `navPath==null` (лечит NRE после перекомпиляции в Play) — поведение в билде не меняется. ✅
