# #4b-1 Тест-химера-тестбед — План реализации

> **Воркфлоу проекта (не TDD):** Unity, автотестов нет. Верификация = пользователь запускает Play и репортит; **коммит только после плейтеста**. Claude пишет C#. `main`. Шаги — чекбоксы `- [ ]`.

**Спека:** `Docs/superpowers/specs/2026-07-30-4b1-test-himera-testbed.md`.

**Goal:** dev-спавнимая тест-химера — сфера-заглушка + случайный-спаннинг состав + тинт-по-составу + ридаут идентичности (`MostKin`). Психики НЕТ (тестбед).

**Architecture:** рантайм-спавнер собирает сферу + `CreatureBody`, зовёт новый `Configure(chassis, donors)` (сериализ. поля не выставить в рантайме) + случайные `Install` (публичный API #2A). Тинт-по-составу включается для тест-химеры (сейчас player-only). Дев-панель — кнопка спавн/реролл + ридаут.

**Стек:** Unity 6, C#, `CreatureBody` (партиал), `CompositionTint`/`MostKin` (готовы).

Каждая задача — коммит после плейтеста.

---

### Задача 1: Runtime-конфиг `CreatureBody` + тинт для тест-химеры

**Files:** Modify `Assets/_Chimera/Scripts/Player/CreatureBody.cs`

- [ ] **1.1 `Configure` runtime-API.** В `CreatureBody.cs` (ядро — `chassis`/`donors`/`BuildSlots`/`Recompute` partial-доступны):
```csharp
// РАНТАЙМ-СБОРКА (тест-химера / будущая стохастическая химеризация NPC): задать шасси+доноров и пересобрать.
// Обычные тела конфигурятся сериализацией (префаб/бутстрап); это — для рождённых на лету. Тинт-по-составу
// включаем тут же (заглушка-сфера без запечённого материала/Telegraph — красится составом, как тело игрока)
public void Configure(SpeciesSO newChassis, SpeciesSO[] newDonors, bool tintFromComposition = false)
{
    chassis = newChassis;
    donors = newDonors;
    tintComposition = tintFromComposition;
    BuildSlots();
    Recompute();
}
```
- [ ] **1.2 Флаг тинта + его чтение.** Добавить поле `bool tintComposition;` (рядом с рендер-полями). В `Recompute` найти `if (move != null) UpdateTint();` → `if (move != null || tintComposition) UpdateTint();`.
- [ ] **1.3 Компиляция** — консоль без ошибок. Коммит откладываем до Задачи 2.

---

### Задача 2: Спавнер тест-химеры (сфера + случайный-спаннинг + тинт)

**Files:** Create `Assets/_Chimera/Scripts/Debug/TestChimeraSpawner.cs`

- [ ] **2.1 Компонент-спавнер** (вешается на объект в сцене; `species` — назначить 5 видов в инспекторе):
```csharp
using System.Collections.Generic;
using UnityEngine;

/// <summary>ТЕСТБЕД: спавнит сферу-химеру со СЛУЧАЙНЫМ-спаннинг составом (от доминанты до истинной химеры),
/// красит по составу, показывает идентичность (MostKin). Психики нет — стоит, демонстрирует состав.
/// Дев-инструмент под #4b (диспатч/поведение проверяем против этих носителей). Положи в сцену, назначь species.</summary>
public class TestChimeraSpawner : MonoBehaviour
{
    [SerializeField] SpeciesSO[] species;        // пул: Человек/Волк/Змея/Лось/Ёж (назначить в инспекторе)
    [SerializeField] float spawnRadius = 8f;     // где вокруг спавнера появляется
    [SerializeField] int maxAugments = 8;        // потолок случайных аугументов (0..N → спаннинг спектра)

    public CreatureBody SpawnRandom()
    {
        if (species == null || species.Length == 0) { Debug.LogWarning("TestChimeraSpawner: пул species пуст"); return null; }

        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "TestChimera";
        Vector2 c = Random.insideUnitCircle * spawnRadius;
        go.transform.position = transform.position + new Vector3(c.x, 0.5f, c.y);
        if (go.TryGetComponent<Collider>(out var col)) Destroy(col); // без физ-коллайдера-заглушки

        var body = go.AddComponent<CreatureBody>();
        var chassis = species[Random.Range(0, species.Length)];
        body.Configure(chassis, species, tintFromComposition: true); // все виды — потенциальные доноры

        // СПАННИНГ: случайное число аугументов в случайные слоты (0..maxAugments) — от доминанты до каши
        int n = Random.Range(0, maxAugments + 1);
        for (int i = 0; i < n; i++)
        {
            int slot = Random.Range(0, body.SlotCount);
            var vars = body.GetVariants(slot);
            if (vars.Count > 0) body.Install(slot, Random.Range(0, vars.Count)); // случайный вариант (вкл. чужие виды)
        }
        return body;
    }
}
```
- [ ] **2.2 Временный триггер для проверки БЕЗ дев-панели** (уберём в Задаче 3): в `Update` спавнера — `if (Input.GetKeyDown(KeyCode.J)) SpawnRandom();` (быстрый плейтест до кнопки).

- [ ] **2.3 ПОЛЬЗОВАТЕЛЬ:** перекомпиляция. В сцене создай пустой объект **TestChimeraSpawner**, повесь скрипт, в поле **Species** назначь 5 видов (Человек/Волк/Змея/Лось/Ёж из `Assets/_Chimera/Data`). **Play** → жми **J** несколько раз.

- [ ] **2.4 ВЕРИФИКАЦИЯ (плейтест):**
  - Появляются сферы; **цвет РАЗНЫЙ** (состав виден: волчистая серее, змеистая зеленее, каша грязнее).
  - Реролл (J ещё) даёт разброс — от «почти чистых» до «мешанины».

- [ ] **2.5 КОММИТ (Задачи 1+2):**
```bash
git add Assets/_Chimera/Scripts/Player/CreatureBody.cs Assets/_Chimera/Scripts/Debug/TestChimeraSpawner.cs Assets/_Chimera/Scripts/Debug/TestChimeraSpawner.cs.meta
git commit -m "фича: тест-химера-тестбед — сфера, случайный-спаннинг состав, тинт (#4b-1)"
```

---

### Задача 3: Дев-панель — кнопка спавна + ридаут идентичности

**Files:** Modify `Assets/_Chimera/Scripts/Editor/ChimeraDevWindow.cs`; Modify `TestChimeraSpawner.cs` (убрать J)

- [ ] **3.1 Кнопка спавна.** В `ChimeraDevWindow` (где спавн видов): кнопка «спавн тест-химеры» → `Object.FindAnyObjectByType<TestChimeraSpawner>()?.SpawnRandom()` (гейт по null-спавнеру, как у других).

- [ ] **3.2 Ридаут идентичности.** В дев-панели показать по всем `TestChimera` в сцене (`FindObjectsByType<CreatureBody>` с фильтром по имени/маркеру, или добавить маркер-компонент): строку `MostKin(out tier)` → «доминанта: Волк (Medium)» или «истинная химера (MostKin null)». Переиспользовать паттерн показа родства.

- [ ] **3.3 Убрать временный J** из `TestChimeraSpawner.Update` (спавн теперь через кнопку).

- [ ] **3.4 ПОЛЬЗОВАТЕЛЬ:** Play → дев-кнопка «спавн тест-химеры» × N. Проверь ридаут: часть — доминантные (вид+тир), часть — истинные химеры.

- [ ] **3.5 ВЕРИФИКАЦИЯ:** кнопка спавнит; ридаут честно показывает идентичность каждой; спектр покрыт (доминантные + истинные химеры).

- [ ] **3.6 КОММИТ:**
```bash
git add Assets/_Chimera/Scripts/Editor/ChimeraDevWindow.cs Assets/_Chimera/Scripts/Debug/TestChimeraSpawner.cs
git commit -m "фича: дев-кнопка спавна тест-химеры + ридаут идентичности (#4b-1)"
```

---

## Self-review (покрытие спеки)

- §2.1 тело+Configure → Задача 1 ✅ · §2.2 спаннинг → Задача 2 (случайное число аугументов) ✅ · §2.4 сфера+тинт → Задача 1.2+2 ✅ · §2.3 ридаут → Задача 3 ✅ · §2.5 спавн → Задача 3 (кнопка), 2.2 (временный J) ✅.
- НЕ в #4b-1 (психика/диспатч/матчап) — не в плане ✅.
- Риск истинной химеры (снят — достижима) — спаннинг §2.2 даёт ✅.
- Гоча тинт-NPC vs Telegraph — сфера без Telegraph, флаг `tintComposition` гейтит (не задевает обычных NPC) ✅.
