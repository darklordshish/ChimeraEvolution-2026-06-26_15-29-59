# Карта тел и валидация — план реализации

> **Для исполнителя:** задачи идут по порядку, шаги помечены чекбоксами. Автотестов в проекте нет и Unity из терминала не запускается — верификацией служит прогон генератора из меню и осмотр отчёта. Каждая задача заканчивается коммитом.

**Цель:** получить карту тел, которая строит существо настоящим билдером и обмеряет результат, и валидатор, ругающийся на объективные поломки при бутстрапе.

**Архитектура:** генератор — редакторский C# в `Assets/_Chimera/Scripts/Editor/`. Он собирает тело на временном `GameObject`, снимает границы через `Renderer.bounds`, пишет markdown в `Docs/`, сносит объект. Валидатор переиспользует те же измерения. Никакой арифметики билдера не дублируется — иначе появится третий источник правды.

**Стек:** Unity 6 (6000.4.7f1), C#, `UnityEditor`, существующие `MorphBuilder` / `SpeciesSO` / `CreatureBody`.

**Спека:** `Docs/superpowers/specs/2026-08-10-karta-tel-i-validaciya.md`

---

## Структура файлов

| Файл | Ответственность |
|---|---|
| `Assets/_Chimera/Scripts/Editor/BodyProbe.cs` | СБОРКА И ОБМЕР: строит тело на временном объекте, отдаёт список замеренных деталей, сносит. Ничего не форматирует. |
| `Assets/_Chimera/Scripts/Editor/BodyMap.cs` | ОТЧЁТ: берёт замеры у `BodyProbe`, пишет `Docs/Диаграммы/КАРТА_ТЕЛ.md`. Пункт меню. |
| `Assets/_Chimera/Scripts/Editor/BodyRules.cs` | ПРАВИЛА: пороги и проверки над замерами. Используется и картой, и бутстрапом. |
| `Assets/_Chimera/Scripts/Editor/SpeciesBootstrap.cs` | правка: вызвать `BodyRules` после генерации видов |
| `Docs/Диаграммы/КАРТА_ТЕЛ.md` | выход генератора (в git) |
| `Docs/УСТРОЙСТВО_ТЕЛА.md` | слой 1: правила системы |
| `Docs/Паспорта/<Вид>.md` | слой 2: пять паспортов |

Разделение `Probe` / `Map` / `Rules` намеренное: измерение, форматирование и суждение — три разные ответственности, и правила понадобятся бутстрапу без отчёта.

---

## Task 1: Каркас пробы — построить и снести

**Файлы:** Create `Assets/_Chimera/Scripts/Editor/BodyProbe.cs`

- [ ] **Шаг 1: создать файл с каркасом**

```csharp
using System.Collections.Generic;
using UnityEngine;

/// <summary>ИЗМЕРИТЕЛЬ ТЕЛА: собирает существо НАСТОЯЩИМ `MorphBuilder` на временном объекте и снимает
/// фактические границы деталей. Ничего не пересчитывает: повтори мы арифметику билдера — появился бы
/// третий источник правды рядом с данными и кодом, и карта начала бы врать (спека 2026-08-10).</summary>
public static class BodyProbe
{
    /// <summary>Замер одной детали: что это, чей ребёнок, где и какого размера НА САМОМ ДЕЛЕ.</summary>
    public struct Part
    {
        public string name;      // имя сокета (контракт имён)
        public string parent;    // имя родительского объекта в иерархии
        public Vector3 center;   // мировой центр (объект строится в нуле, так что это локальные координаты)
        public Vector3 size;     // габарит по границам рендерера
        public bool hasRenderer;
    }

    /// <summary>Построить тело вида и вернуть замеры. Объект сносится до выхода.</summary>
    public static List<Part> Measure(SpeciesSO species)
    {
        var parts = new List<Part>();
        if (species == null) return parts;

        var go = new GameObject("~BodyProbe");
        try
        {
            // CharacterController нужен билдеру: высоты в данных заданы ОТ ЗЕМЛИ, и без него сборка
            // уедет по вертикали (MorphBuilder считает footY по низу контроллера)
            var cc = go.AddComponent<CharacterController>();
            cc.height = 2f; cc.center = new Vector3(0f, 1f, 0f);

            var worn = new List<Organ>();
            if (species.organs != null)
                foreach (var o in species.organs) if (o != null) worn.Add(o);

            MorphBuilder.Build(go.transform, species, worn);
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var t = r.transform;
                parts.Add(new Part {
                    name = t.name,
                    parent = t.parent != null ? t.parent.name : "(корень)",
                    center = r.bounds.center,
                    size = r.bounds.size,
                    hasRenderer = true,
                });
            }
        }
        finally
        {
            // ВНЕ PLAY `Object.Destroy` ОТЛОЖЕН и объект пережил бы прогон — нужен немедленный снос
            Object.DestroyImmediate(go);
        }
        return parts;
    }
}
```

- [ ] **Шаг 2: проверить компиляцию скобок**

Run:
```bash
awk '{o+=gsub(/\{/,"{"); c+=gsub(/\}/,"}")} END{print o"/"c}' "Assets/_Chimera/Scripts/Editor/BodyProbe.cs"
```
Ожидается: одинаковые числа.

- [ ] **Шаг 3: коммит**

```bash
git add Assets/_Chimera/Scripts/Editor/BodyProbe.cs
git commit -m "Карта тел: измеритель строит тело настоящим билдером"
```

---

## Task 2: Отчёт — дерево частей и размах форм

**Файлы:** Create `Assets/_Chimera/Scripts/Editor/BodyMap.cs`

- [ ] **Шаг 1: создать генератор отчёта**

```csharp
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>КАРТА ТЕЛ: отчёт по фактически построенным существам. Пишет в `Docs/`, читается человеком и
/// агентами — они НЕ пересчитывают факты, а берут их отсюда (спека 2026-08-10).</summary>
public static class BodyMap
{
    const string OutPath = "Docs/Диаграммы/КАРТА_ТЕЛ.md";

    [MenuItem("Chimera/Выгрузить карту тел")]
    public static void Generate()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# КАРТА ТЕЛ");
        sb.AppendLine();
        sb.AppendLine("Отчёт СГЕНЕРИРОВАН: `Chimera → Выгрузить карту тел`. Руками не править — перезапишется.");
        sb.AppendLine("Числа сняты с ФАКТИЧЕСКИ построенного тела (границы рендереров), а не пересчитаны.");
        sb.AppendLine();

        foreach (var guid in AssetDatabase.FindAssets("t:SpeciesSO"))
        {
            var species = AssetDatabase.LoadAssetAtPath<SpeciesSO>(AssetDatabase.GUIDToAssetPath(guid));
            if (species == null) continue;
            var parts = BodyProbe.Measure(species);

            sb.AppendLine($"## {species.speciesName}");
            sb.AppendLine();
            sb.AppendLine($"Деталей построено: **{parts.Count}**");
            sb.AppendLine();
            sb.AppendLine("| деталь | родитель | центр (x,y,z) | габарит (ш×в×д) |");
            sb.AppendLine("|---|---|---|---|");
            foreach (var p in parts)
                sb.AppendLine($"| {p.name} | {p.parent} | {p.center.x:F3}, {p.center.y:F3}, {p.center.z:F3} " +
                              $"| {p.size.x:F3} × {p.size.y:F3} × {p.size.z:F3} |");
            sb.AppendLine();
        }

        System.IO.File.WriteAllText(OutPath, sb.ToString(), System.Text.Encoding.UTF8);
        Debug.Log($"Карта тел записана: {OutPath}");
    }
}
```

- [ ] **Шаг 2: проверить скобки**

Run:
```bash
awk '{o+=gsub(/\{/,"{"); c+=gsub(/\}/,"}")} END{print o"/"c}' "Assets/_Chimera/Scripts/Editor/BodyMap.cs"
```
Ожидается: одинаковые числа.

- [ ] **Шаг 3: попросить прогон**

Геймдизайнер жмёт `Chimera → Выгрузить карту тел`, затем сообщает: появился ли `Docs/Диаграммы/КАРТА_ТЕЛ.md`, есть ли в консоли ошибки, сколько деталей у каждого вида.

- [ ] **Шаг 4: коммит**

```bash
git add Assets/_Chimera/Scripts/Editor/BodyMap.cs "Docs/Диаграммы/КАРТА_ТЕЛ.md"
git commit -m "Карта тел: отчёт по фактическим деталям"
```

---

## Task 3: СВЕРКА КАРТЫ С ИГРОЙ (критическая)

Смысл задачи: убедиться, что сборка **в редакторе** совпадает со сборкой **в Play**. Если нет — все дальнейшие числа врут, а мы им поверим.

**Файлы:** нет правок кода; при расхождении — правки в `BodyProbe.cs`

- [ ] **Шаг 1: снять эталон из игры**

Геймдизайнер запускает Play, выбирает в Hierarchy живого волка, разворачивает `Morph` и записывает: сколько всего детей, как называется первая пятёрка.

- [ ] **Шаг 2: сравнить с картой**

В `КАРТА_ТЕЛ.md` в разделе «Волк» посчитать те же величины.

- [ ] **Шаг 3: свести расхождения**

Совпало — задача закрыта. Не совпало — записать разницу и чинить `BodyProbe`: типичные причины — не отработал `Awake` компонентов, не тот набор органов (карта берёт все родные, игра могла надеть иное), отсутствует `CharacterController`.

- [ ] **Шаг 4: коммит (если правил пробу)**

```bash
git add Assets/_Chimera/Scripts/Editor/BodyProbe.cs
git commit -m "Карта тел: сборка в редакторе сведена с игрой"
```

---

## Task 4: Стыки — зазоры и нахлёсты числом

**Файлы:** Modify `Assets/_Chimera/Scripts/Editor/BodyMap.cs`

- [ ] **Шаг 1: добавить расчёт стыка в отчёт**

Вставить после таблицы деталей, внутри цикла по видам:

```csharp
            sb.AppendLine("### Стыки (по длинной оси родителя)");
            sb.AppendLine();
            sb.AppendLine("| ребёнок | родитель | конец родителя | начало ребёнка | зазор (+) / нахлёст (−) |");
            sb.AppendLine("|---|---|---|---|---|");
            var byName = new Dictionary<string, BodyProbe.Part>();
            foreach (var p in parts) byName[p.name] = p;   // одноимённые: берём последнюю, детали цепи равнозначны
            foreach (var p in parts)
            {
                if (!byName.TryGetValue(p.parent, out var par)) continue;
                float childFront = p.center.z - p.size.z * 0.5f;
                float parentEnd  = par.center.z - par.size.z * 0.5f;
                float gap = childFront - parentEnd;
                sb.AppendLine($"| {p.name} | {p.parent} | {parentEnd:F3} | {childFront:F3} | {gap:F3} |");
            }
            sb.AppendLine();
```

- [ ] **Шаг 2: проверить скобки**

Run:
```bash
awk '{o+=gsub(/\{/,"{"); c+=gsub(/\}/,"}")} END{print o"/"c}' "Assets/_Chimera/Scripts/Editor/BodyMap.cs"
```
Ожидается: одинаковые числа.

- [ ] **Шаг 3: прогон и осмотр**

Геймдизайнер жмёт пункт меню. Проверить: у змеи между звеньями цепи зазор около нуля; там, где на скриншотах были видимые щели, число заметно больше нуля.

- [ ] **Шаг 4: коммит**

```bash
git add Assets/_Chimera/Scripts/Editor/BodyMap.cs "Docs/Диаграммы/КАРТА_ТЕЛ.md"
git commit -m "Карта тел: стыки зазорами и нахлёстами"
```

---

## Task 5: Перекрытия — кто лезет в чужой объём

**Файлы:** Modify `Assets/_Chimera/Scripts/Editor/BodyMap.cs`

- [ ] **Шаг 1: добавить раздел перекрытий**

Вставить после раздела стыков, внутри цикла по видам:

```csharp
            sb.AppendLine("### Перекрытия объёмов (кандидаты в дубли)");
            sb.AppendLine();
            sb.AppendLine("| A | B | доля пересечения от меньшего |");
            sb.AppendLine("|---|---|---|");
            for (int i = 0; i < parts.Count; i++)
                for (int j = i + 1; j < parts.Count; j++)
                {
                    if (parts[i].name == parts[j].name) continue;          // одноимённые звенья цепи — не дубль
                    if (parts[i].name == parts[j].parent || parts[j].name == parts[i].parent) continue; // родство
                    var a = new Bounds(parts[i].center, parts[i].size);
                    var b = new Bounds(parts[j].center, parts[j].size);
                    if (!a.Intersects(b)) continue;
                    var min = Vector3.Max(a.min, b.min);
                    var max = Vector3.Min(a.max, b.max);
                    var d = max - min;
                    float inter = Mathf.Max(0f, d.x) * Mathf.Max(0f, d.y) * Mathf.Max(0f, d.z);
                    float va = a.size.x * a.size.y * a.size.z, vb = b.size.x * b.size.y * b.size.z;
                    float share = inter / Mathf.Max(0.000001f, Mathf.Min(va, vb));
                    if (share < 0.35f) continue;                            // мелкие касания не шумим
                    sb.AppendLine($"| {parts[i].name} | {parts[j].name} | {share:P0} |");
                }
            sb.AppendLine();
```

- [ ] **Шаг 2: проверить скобки**

Run:
```bash
awk '{o+=gsub(/\{/,"{"); c+=gsub(/\}/,"}")} END{print o"/"c}' "Assets/_Chimera/Scripts/Editor/BodyMap.cs"
```
Ожидается: одинаковые числа.

- [ ] **Шаг 3: прогон и осмотр**

Ожидается, что раздел поймает известные дефекты: «Шкура» против «хребет» (полный дубль корпуса), «Сердце» против корпуса.

- [ ] **Шаг 4: коммит**

```bash
git add Assets/_Chimera/Scripts/Editor/BodyMap.cs "Docs/Диаграммы/КАРТА_ТЕЛ.md"
git commit -m "Карта тел: перекрытия объёмов"
```

---

## Task 6: Правила — пороги и проверки

**Файлы:** Create `Assets/_Chimera/Scripts/Editor/BodyRules.cs`

- [ ] **Шаг 1: создать файл правил**

```csharp
using System.Collections.Generic;
using UnityEngine;

/// <summary>ПРАВИЛА ТЕЛА: суждения над замерами. Отдельно от карты, потому что нужны бутстрапу без отчёта.
/// Пороги — доли КАЛИБРА, не метры: иначе мелкие виды всегда в допуске, а крупные всегда виноваты.</summary>
public static class BodyRules
{
    public const float GapWarn = 0.08f;     // щель больше 8% калибра — предупреждение
    public const float OverlapWarn = 0.40f; // нахлёст больше 40% калибра
    public const float DupError = 0.60f;    // пересечение объёмов больше 60% — дубль области
    public const float AxisMargin = 0.05f;  // запас длинной оси меньше 5% — ось вот-вот переключится

    public struct Issue { public string species, where, text; public bool error; }

    /// <summary>Проверки по ДАННЫМ вида: то, что видно без сборки.</summary>
    public static List<Issue> CheckData(SpeciesSO s)
    {
        var list = new List<Issue>();
        if (s == null || s.sockets == null) return list;
        var byName = new Dictionary<string, BodySocket>();
        foreach (var k in s.sockets) if (k != null && !string.IsNullOrEmpty(k.name)) byName[k.name] = k;

        foreach (var k in s.sockets)
        {
            if (k == null || string.IsNullOrEmpty(k.name)) continue;

            // ЗАПАС ДЛИННОЙ ОСИ: она выбирается по максимальной стороне, и при близких числах молча
            // переключается, разворачивая ВСЮ ветку детей (гоча из CLAUDE.md)
            var b = k.baseSize;
            float max = Mathf.Max(b.x, Mathf.Max(b.y, b.z));
            float second = 0f;
            foreach (var v in new[] { b.x, b.y, b.z }) if (v < max && v > second) second = v;
            if (max > 0f && second > 0f && (max / second - 1f) < AxisMargin)
                list.Add(new Issue { species = s.speciesName, where = k.name, error = true,
                                     text = $"запас длинной оси {(max / second - 1f):P0} — ось может переключиться" });

            // ЦЕПЬ БЕЗ ДИАМЕТРА: ни своего, ни наследуемого — деталь схлопнется в плоскость молча
            if (k.linkLength > 0f && k.linkDiameter <= 0f)
            {
                bool inherits = !string.IsNullOrEmpty(k.parent) && byName.TryGetValue(k.parent, out var par)
                                && par.linkLength > 0f;
                if (!inherits)
                    list.Add(new Issue { species = s.speciesName, where = k.name, error = true,
                                         text = "цепь без диаметра: свой не задан и родитель не цепь" });
            }

            // ЦИКЛ В ГРАФЕ: билдер страхуется глубиной, но данные всё равно неверны
            var seen = new HashSet<string> { k.name };
            var cur = k;
            while (!string.IsNullOrEmpty(cur.parent) && byName.TryGetValue(cur.parent, out var up))
            {
                if (!seen.Add(up.name))
                {
                    list.Add(new Issue { species = s.speciesName, where = k.name, error = true,
                                         text = $"цикл в графе: {up.name}" });
                    break;
                }
                cur = up;
            }
        }
        return list;
    }

    /// <summary>Проверки по ЗАМЕРАМ: то, что видно только на построенном теле.</summary>
    public static List<Issue> CheckParts(string speciesName, List<BodyProbe.Part> parts)
    {
        var list = new List<Issue>();
        var byName = new Dictionary<string, BodyProbe.Part>();
        foreach (var p in parts) byName[p.name] = p;

        foreach (var p in parts)
        {
            if (!byName.TryGetValue(p.parent, out var par)) continue;
            if (p.size.x > par.size.x && p.size.y > par.size.y)
                list.Add(new Issue { species = speciesName, where = p.name, error = true,
                                     text = $"деталь больше родителя: {p.size.x:F3}×{p.size.y:F3} против {par.size.x:F3}×{par.size.y:F3}" });
        }
        return list;
    }
}
```

- [ ] **Шаг 2: проверить скобки**

Run:
```bash
awk '{o+=gsub(/\{/,"{"); c+=gsub(/\}/,"}")} END{print o"/"c}' "Assets/_Chimera/Scripts/Editor/BodyRules.cs"
```
Ожидается: одинаковые числа.

- [ ] **Шаг 3: коммит**

```bash
git add Assets/_Chimera/Scripts/Editor/BodyRules.cs
git commit -m "Карта тел: правила и пороги отдельным файлом"
```

---

## Task 7: Валидатор в бутстрапе

**Файлы:** Modify `Assets/_Chimera/Scripts/Editor/SpeciesBootstrap.cs`

- [ ] **Шаг 1: найти конец генерации видов**

Run:
```bash
grep -n "ValidateSockets" "Assets/_Chimera/Scripts/Editor/SpeciesBootstrap.cs"
```

- [ ] **Шаг 2: добавить вызов правил рядом с существующей проверкой**

В том же методе, где зовётся `ValidateSockets`, после неё:

```csharp
        // ПРАВИЛА ТЕЛА: объективные поломки — ошибкой в консоль. Анатомию НЕ проверяем: стилизация это
        // решение геймдизайнера, её место в карте таблицей (спека 2026-08-10)
        foreach (var guid in AssetDatabase.FindAssets("t:SpeciesSO"))
        {
            var sp = AssetDatabase.LoadAssetAtPath<SpeciesSO>(AssetDatabase.GUIDToAssetPath(guid));
            foreach (var issue in BodyRules.CheckData(sp))
                if (issue.error) Debug.LogError($"[тело] {issue.species} · {issue.where}: {issue.text}");
                else Debug.LogWarning($"[тело] {issue.species} · {issue.where}: {issue.text}");
        }
```

- [ ] **Шаг 3: проверить скобки**

Run:
```bash
awk '{o+=gsub(/\{/,"{"); c+=gsub(/\}/,"}")} END{print o"/"c}' "Assets/_Chimera/Scripts/Editor/SpeciesBootstrap.cs"
```
Ожидается: одинаковые числа.

- [ ] **Шаг 4: прогон**

Геймдизайнер жмёт `Chimera → Создать дефолтные виды` и присылает список сообщений `[тело]` из консоли.

- [ ] **Шаг 5: коммит**

```bash
git add Assets/_Chimera/Scripts/Editor/SpeciesBootstrap.cs
git commit -m "Валидатор тела: объективные поломки ловятся при бутстрапе"
```

---

## Task 8: Слой 1 — документ «Устройство тела»

**Файлы:** Create `Docs/УСТРОЙСТВО_ТЕЛА.md`

- [ ] **Шаг 1: написать документ по карте и отчётам ревизии**

Разделы (каждый заполняется фактами из `КАРТА_ТЕЛ.md` и двух отчётов ревизии от 2026-08-09):

1. Что живёт в данных, что в коде — таблица «сущность → где задана → кто читает».
2. Как аугумент подключается к шасси: слот, место, форма, приоритет источников.
3. Порядок сборки: от чего считается позиция, от чего размер, что наследуется.
4. Кто пересобирается после морфа и кто протухает.
5. Правила, противоречащие друг другу — список с указанием, какое считается верным.

- [ ] **Шаг 2: коммит**

```bash
git add "Docs/УСТРОЙСТВО_ТЕЛА.md"
git commit -m "Устройство тела: правила системы одним документом"
```

---

## Task 9: Слой 2 — паспорта пяти видов агентами

**Файлы:** Create `Docs/Паспорта/Человек.md`, `Волк.md`, `Змея.md`, `Лось.md`, `Ёж.md`

- [ ] **Шаг 1: запустить пять агентов параллельно**

Каждому — один вид. В задании: читать `Docs/Диаграммы/КАРТА_ТЕЛ.md` и `Docs/УСТРОЙСТВО_ТЕЛА.md`, **не пересчитывать факты**, писать восемь секций из спеки строго по порядку, дописывая файл посекционно. Правки кода и данных запрещены.

- [ ] **Шаг 2: подхват после обрывов**

Лимит сессии рвал агентов дважды. По каждому оборванному паспорту — новый агент с указанием: прочитать готовый файл и продолжить с первой незаполненной секции.

- [ ] **Шаг 3: свести расхождения**

Где паспорт расходится с картой — прав генератор. Внести поправку в паспорт.

- [ ] **Шаг 4: коммит**

```bash
git add "Docs/Паспорта/"
git commit -m "Паспорта видов: пять разборов по единому шаблону"
```

---

## Task 10: Обновление документации

**Файлы:** Modify `Docs/CONSTRUCTOR_GUIDE.md`, `CLAUDE.md`

- [ ] **Шаг 1: привести гайд в соответствие с кодом**

Правила, которые уже неверны: калибр наследуется долей (`sizeRel`), форма может приходить от чужого органа (`formFrom`/`formRole`), роли частей заменили списки имён, цвет по составу теперь безусловный.

- [ ] **Шаг 2: дописать гочи недели в CLAUDE.md**

- место, заданное в метрах, не поспевает за соседями при правке родителя;
- правка родителя тянет всю ветку (шея на горбе ужала морду втрое);
- перевод в доли консервирует кривизну, если считать от испорченных чисел;
- ссылка, снятая в `Awake`, мертва у морф-существ — тело пересобирается на каждое убийство.

- [ ] **Шаг 3: коммит**

```bash
git add "Docs/CONSTRUCTOR_GUIDE.md" CLAUDE.md
git commit -m "Доки: правила приведены к коду, гочи недели записаны"
```

---

## Самопроверка плана

**Покрытие спеки.** Карта — задачи 1–5; сверка с игрой — 3; валидатор — 6–7; слой 1 — 8; слой 2 и протокол агентов — 9; обновление доков — 10. Раздел карты «Органы» и «Живучесть ссылок» вошли в документ слоя 1 (задача 8), поскольку это статические факты о коде, а не замеры. Сводная таблица пропорций — в паспортах (секция 1 шаблона), потому что эталоны видовые.

**Заглушек нет:** весь код приведён целиком, команды с ожидаемым выводом.

**Согласованность имён:** `BodyProbe.Part` / `BodyProbe.Measure` / `BodyRules.Issue` / `BodyRules.CheckData` / `BodyRules.CheckParts` используются одинаково во всех задачах.
