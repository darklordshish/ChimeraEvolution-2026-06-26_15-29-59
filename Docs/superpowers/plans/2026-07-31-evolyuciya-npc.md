# Эволюция NPC (стохастическая химеризация) — План реализации

> **Воркфлоу проекта (не TDD):** Unity, автотестов нет. Верификация = пользователь запускает Play и репортит; **коммит только после плейтеста**. Claude пишет C#. `main`. Шаги — чекбоксы `- [ ]`.

**Спека:** `Docs/superpowers/specs/2026-07-31-evolyuciya-npc-himerizaciya.md`.

**Goal:** NPC убивают друг друга → стохастически надевают органы жертвы (шанс=родство) → доминанта едет → метаморфоза психики. Живой эволюционирующий лес.

**Architecture:** родство — единая ось. `EvolutionConfig`-объект в сцене (список видов + параметры). `CreatureBody.Awake` для NPC: `donors=все` + `startAffinity` + tint. Грант органа — модуль `CreatureBody.Evolution.cs`, зовётся из `CreditKiller`. Ре-диспатч — тело трекает доминанту, эмитит событие, `Metamorph` перевешивает психику.

**Tech Stack:** Unity 6, C#, `CreatureBody` (партиал), `SetAffinity`/`GetAffinity`, `Install`/`GetVariants`, `MostKin`, `PsycheDispatch`.

Три коммита: (1) платформа-NPC; (2) грант-химеризации; (3) ре-диспатч-метаморфоза.

---

## Задача 1: Платформа NPC (`EvolutionConfig` + `donors=все` + стартовое родство + tint)

**Files:** Create `Enemies/EvolutionConfig.cs`; Modify `Player/CreatureBody.cs`

- [ ] **1.1 `EvolutionConfig` — объект в сцену** (тюнинг в инспекторе):
```csharp
using UnityEngine;

/// <summary>ЭВОЛЮЦИЯ NPC — тюнинг-объект в сцене (правило проекта: настраиваемое — объектом, не бутстрап).
/// Рантайм-реестр всех видов (его в проекте не было) + параметры химеризации. NPC-тела находят его в Awake.</summary>
public class EvolutionConfig : MonoBehaviour
{
    [SerializeField] SpeciesSO[] allSpecies;          // ВСЕ виды (Человек/Волк/Змея/Лось/Ёж) — назначить в инспекторе
    [SerializeField] bool evolveNpc = true;           // рубильник всей фичи
    [SerializeField, Range(0, 100)] int startAffinity = 1; // стартовое родство ко всем НЕ-своим видам (1=отладка «всё приоткрыто», 0=демка)
    [SerializeField] float chimerizeChancePerAffinity = 0.01f; // шанс гранта = родство × это (высокая динамика; тюним)

    public SpeciesSO[] AllSpecies => allSpecies;
    public bool EvolveNpc => evolveNpc;
    public int StartAffinity => startAffinity;
    public float ChimerizeChancePerAffinity => chimerizeChancePerAffinity > 0f ? chimerizeChancePerAffinity : 0.01f;

    static EvolutionConfig instance;
    public static EvolutionConfig Instance => instance != null ? instance : (instance = FindAnyObjectByType<EvolutionConfig>());
}
```

- [ ] **1.2 `CreatureBody` — платформа для NPC.** В `CreatureBody.Awake` (ПОСЛЕ определения chassis/donors, ДО `BuildSlots`; проверить порядок — если `BuildSlots` в `Start`, встроить туда/перед). Гейт «это NPC» = `move == null` (у игрока `move` — драйвер локомоции; у NPC его нет):
```csharp
        // ЭВОЛЮЦИЯ: NPC встаёт на платформу «тело=данные» как игрок — все ветки-доноры + стартовое родство +
        // цвет по составу. Родство — единая ось: >0 → ветка доступна (грант/скидка). Игрока не трогаем (у него donors уже все)
        if (move == null && chassis != null)
        {
            var cfg = EvolutionConfig.Instance;
            if (cfg != null && cfg.EvolveNpc && cfg.AllSpecies != null && cfg.AllSpecies.Length > 0)
            {
                donors = cfg.AllSpecies;                 // все виды — потенциальные доноры (родные надеты по умолчанию → чистый вид)
                tintComposition = true;                  // цвет по составу (развязка Telegraph.Rebase готова)
                foreach (var sp in cfg.AllSpecies)
                    if (sp != null && sp != chassis) SetAffinity(sp.speciesName, cfg.StartAffinity); // стартовое родство ко всем чужим
            }
        }
```
*(Если `donors` присваивается до `BuildSlots`, слоты соберутся со всеми вариантами. Проверить: где в жизненном цикле `BuildSlots` для обычного тела — Awake или Start; вставить блок ПЕРЕД ним.)*

- [ ] **1.3 ПОЛЬЗОВАТЕЛЬ:** перекомпиляция. В сцене создай пустой объект **EvolutionConfig**, повесь скрипт, в **All Species** назначь 5 видов из `Assets/_Chimera/Data` (Человек/Волк/Змея/Лось/Ёж). `Start Affinity` оставь 1.

- [ ] **1.4 ВЕРИФИКАЦИЯ (РЕГРЕСС — критично):** свежий **Play**. Обычный лес (волки/змеи/лоси/ежи) должен вести себя как раньше:
  - чистые виды выглядят собой (родные органы надеты), но теперь **тинтятся по составу** (чистый волк — волчий цвет; проверить, что телеграфы не сломались — развязка Rebase);
  - стая/охота/узнавание работают; `MostKin` чистого волка = Волк (родство ≠ идентичность-состава, доминанта не должна поехать);
  - в дев-ридауте у NPC появилось родство 1 к чужим видам.

- [ ] **1.5 КОММИТ (Задача 1):**
```bash
git add Assets/_Chimera/Scripts/Enemies/EvolutionConfig.cs Assets/_Chimera/Scripts/Enemies/EvolutionConfig.cs.meta Assets/_Chimera/Scripts/Player/CreatureBody.cs
git commit -m "фича: платформа эволюции NPC — donors=все + стартовое родство + tint (EvolutionConfig)"
```

---

## Задача 2: Грант органа на убийство (`CreatureBody.Evolution.cs`)

**Files:** Create `Player/CreatureBody.Evolution.cs`; Modify `Player/CreatureBody.cs` (`CreditKiller` зовёт грант)

- [ ] **2.1 Модуль гранта** — новый partial:
```csharp
using System.Collections.Generic;
using UnityEngine;

// ЭВОЛЮЦИЯ (стохастическая химеризация): убийца с шансом = родство надевает орган жертвы. Родство — единая
// ось (donors=все уже стоят из Awake). Зовётся из CreditKiller (this=жертва, killer=убийца). Partial-модуль.
public partial class CreatureBody
{
    /// <summary>Стохастически надеть убийце орган ИЗ СОСТАВА жертвы (this). Шанс = родство убийцы к виду
    /// органа × конфиг. Влезет по пулу (Install гейтит) — надел. Grant по СЛУЧАЙНОМУ надетому органу жертвы.</summary>
    public void TryChimerize(CreatureBody killer)
    {
        var cfg = EvolutionConfig.Instance;
        if (killer == null || slots == null || cfg == null || !cfg.EvolveNpc) return;

        // собираем звериные органы жертвы (вид+орган), по которым можно химеризоваться
        var loot = new List<(string species, string organ)>();
        foreach (var sl in slots)
            if (sl.Installed && sl.Pick != null && sl.Pick.species != null)
                loot.Add((sl.Pick.species, sl.Worn.organName));
        if (loot.Count == 0) return;

        var pick = loot[Random.Range(0, loot.Count)];
        float chance = killer.GetAffinity(pick.species) * cfg.ChimerizeChancePerAffinity;
        if (Random.value > chance) return; // не выпало

        // найти у убийцы слот+вариант этого органа (donors=все → вариант присутствует) и надеть
        for (int s = 0; s < killer.SlotCount; s++)
        {
            var vars = killer.GetVariants(s);
            for (int v = 0; v < vars.Count; v++)
                if (vars[v].species == pick.species && vars[v].organName == pick.organ)
                {
                    if (killer.Install(s, v)) Debug.Log($"химеризация: {killer.name} надел {pick.organ} ({pick.species})");
                    return; // один орган за убийство
                }
        }
    }
}
```

- [ ] **2.2 Вызов из `CreditKiller`.** В `CreatureBody.cs`, в конце `CreditKiller` (после начисления родства убийце), добавить:
```csharp
        TryChimerize(killer); // ЭВОЛЮЦИЯ: шанс надеть убийце орган из нашего состава (родство = шанс)
```

- [ ] **2.3 ПОЛЬЗОВАТЕЛЬ:** перекомпиляция → свежий **Play**. Наблюдай лес 1-2 минуты (при `StartAffinity=1` шанс ненулевой сразу; можно поднять `ChimerizeChancePerAffinity` в `EvolutionConfig` для быстрого эффекта).

- [ ] **2.4 ВЕРИФИКАЦИЯ:**
  - в консоли логи «химеризация: … надел …»;
  - у NPC меняется цвет (состав поехал); в дев-ридауте состав химеризуется;
  - лось, затоптавший волка, может нарасти волчьим органом.

- [ ] **2.5 КОММИТ (Задача 2):**
```bash
git add Assets/_Chimera/Scripts/Player/CreatureBody.Evolution.cs Assets/_Chimera/Scripts/Player/CreatureBody.Evolution.cs.meta Assets/_Chimera/Scripts/Player/CreatureBody.cs
git commit -m "фича: грант органа на убийство — стохастическая химеризация NPC (шанс=родство)"
```

---

## Задача 3: Ре-диспатч психики (метаморфоза по доминанте)

**Files:** Modify `Player/CreatureBody.cs` (трекинг доминанты + событие); Create `Enemies/Metamorph.cs`; maybe Modify `Enemies/PsycheDispatch.cs`

- [ ] **3.1 Трекинг доминанты + событие.** В `CreatureBody.cs`: поле + событие + проверка в конце `Recompute`:
```csharp
    SpeciesSO lastDominant;                      // для метаморфозы: сменилась доминанта → перевесить психику
    public System.Action<SpeciesSO> onDominantChanged; // слушатель — Metamorph (тело про психики не знает)
```
В конце `Recompute()` (после раздачи статов):
```csharp
        // МЕТАМОРФОЗА: доминанта переползла И уверенно (Medium+, гистерезис — не дёргаться на границе 50/50) → сигнал
        var dom = MostKin(out var domTier);
        if (dom != lastDominant && domTier >= KinTier.Medium)
        {
            lastDominant = dom;
            onDominantChanged?.Invoke(dom);
        }
```

- [ ] **3.2 `Metamorph` — слушатель ре-диспатча:**
```csharp
using UnityEngine;

/// <summary>МЕТАМОРФОЗА (эволюция NPC): доминанта тела переползла (Medium+) → снять старую психику и повесить
/// новую по составу (PsycheDispatch). Шасси-якорь: физически тело не меняется, меняется поведение/узнавание.
/// Тонкий слушатель — тело про психики не знает, только эмитит onDominantChanged.</summary>
public class Metamorph : MonoBehaviour
{
    CreatureBody body;

    void Awake()
    {
        body = GetComponent<CreatureBody>();
        if (body != null) body.onDominantChanged += Remorph;
    }
    void OnDestroy() { if (body != null) body.onDominantChanged -= Remorph; }

    void Remorph(SpeciesSO dom)
    {
        // снять ВСЕ текущие психики-модули (видовые + альфа), затем повесить новую по доминанте
        foreach (var p in GetComponents<MonoBehaviour>())
            if (p is WolfPsyche or SnakePsyche or MoosePsyche or HedgehogPsyche or ChimeraAlphaPsyche) Destroy(p);
        PsycheDispatch.Attach(body); // повесит по актуальной доминанте (+ Refeed)
    }
}
```

- [ ] **3.3 `Metamorph` на NPC.** В `CreatureBody.Awake` (в том же NPC-блоке Задачи 1.2), до-навесить слушателя:
```csharp
                if (GetComponent<Metamorph>() == null) gameObject.AddComponent<Metamorph>();
```
*(Ре-диспатч `PsycheDispatch.Attach` уже вешает психику; при метаморфозе `Metamorph` сначала снимает старые — чтобы не копились. Проверить, что `Attach` не дублирует при первом спавне: обычные NPC психику несут с префаба — Attach добавит вторую? Гоча §5 плана.)*

- [ ] **3.4 ГОЧА первого спавна.** Обычные NPC несут психику С ПРЕФАБА (`WolfPrefab` вешает `WolfPsyche`). `PsycheDispatch.Attach` их не касается (его зовёт только тест-химера). Метаморфоза же зовёт `Attach` → добавит психику поверх префабной. Решение: `Metamorph.Remorph` СНАЧАЛА снимает все психики (уже так) → префабная тоже снимется → `Attach` повесит одну по доминанте. Первый спавн `Attach` не трогает (метаморфоза только при СМЕНЕ доминанты). ОК — но проверить плейтестом, что при метаморфозе ровно одна психика.

- [ ] **3.5 ПОЛЬЗОВАТЕЛЬ:** перекомпиляция → свежий **Play**. Подними `ChimerizeChancePerAffinity` (напр. 0.05) для быстрой эволюции. Наблюдай: NPC, набравший чужих органов до доминанты-Medium, **меняет поведение**.

- [ ] **3.6 ВЕРИФИКАЦИЯ:**
  - волк, обожравшийся ежей (доминанта → Ёж, Medium+), начинает вести себя как ёж (клубок/залп) — лог смены;
  - шасси-якорь: габариты/база остались, психика/узнавание/цвет — по доминанте;
  - метаморфоза не дёргается на границе (гистерезис Medium); ровно одна психика после смены.

- [ ] **3.7 КОММИТ (Задача 3):**
```bash
git add Assets/_Chimera/Scripts/Player/CreatureBody.cs Assets/_Chimera/Scripts/Enemies/Metamorph.cs Assets/_Chimera/Scripts/Enemies/Metamorph.cs.meta
git commit -m "фича: метаморфоза психики по доминанте (ре-диспатч, гистерезис Medium) — эволюция NPC"
```

---

## Риски / гочи (из спеки §5 + плана)
- **Порядок `BuildSlots` в жизненном цикле** — блок Задачи 1.2 должен встать ДО сборки слотов, чтобы `donors=все` учлись. Проверить Awake/Start.
- **Гейт «NPC» = `move == null`** — свериться, что у игрока `move` заведомо не null к моменту Awake-проверки (иначе игрок попадёт под NPC-ветку). Альтернатива: `GetComponent<PlayerController>() == null`.
- **`MostKin` чистых видов** не должен поехать от `startAffinity` (родство ≠ идентичность-состав) — регресс-гейт 1.4.
- **Дубль психики при метаморфозе** (§3.4) — Remorph снимает все психики перед Attach.
- **`donors=все` включает Человека** — у NPC появится человеческая ветка (может нарасти «человеческим» органом). Норм (химера), но учесть в наблюдении.
- **Высокая динамика → каша** — ожидаемо; `ChimerizeChancePerAffinity` в `EvolutionConfig` крутится в инспекторе для наблюдения/осаждения.

## Self-review (покрытие спеки)
- §2.1 грант → Задача 2 ✅ · §2.2 donors=все+startAffinity → Задача 1 ✅ · §2.3 ре-диспатч Medium → Задача 3 ✅ · §2.4 tint NPC → Задача 1 ✅ · §2.5 модуль Evolution.cs → Задача 2 ✅.
- Регресс леса (§5) → гейт 1.4 ПЕРЕД грантом ✅. Реестр видов (§5) → `EvolutionConfig` ✅.
- Деферы (морфология/босс/баланс/градуированное открытие) — не в задачах ✅.
- Типы: `SetAffinity`/`GetAffinity`/`GetVariants`/`Install`/`SlotCount`/`MostKin`/`PsycheDispatch.Attach` — публичны/существуют ✅; психики-типы для снятия — существуют ✅.
