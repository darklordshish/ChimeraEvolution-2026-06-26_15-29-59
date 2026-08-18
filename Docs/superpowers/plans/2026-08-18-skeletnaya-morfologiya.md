# Скелетная морфология: план прототипа

> **Для исполнителя:** задачи идут по порядку, шаги — чекбоксами. Спека: `Docs/superpowers/specs/2026-08-18-skeletnaya-morfologiya.md`.

**Цель:** проверить на передней ноге И на голове волка, что скелет из костей с мясом по правилам даёт силуэт лучше нынешнего — **одним методом для обеих частей**.

**Архитектура:** кость линейна (длина, направление, толщина у концов). Сустав общий: конец родителя = начало ребёнка, радиус там один. Мясо строится веретеном вдоль кости. Скелет живёт рядом со старым сокет-планом, не заменяя его: место может отдать свою форму костям, остальные работают как прежде.

**Стек:** Unity 6 (6000.4.7f1), C#, URP. Тестов нет — проверка численная (расчёт до сборки) плюс плейтест по правилу тандема.

**Верификация в этом проекте:** сначала считаем скриптом, что получится, потом просим Play. Не наоборот — за нарушение этого правила 15.08 потрачено три прогона впустую.

---

### Задача 1: Модель данных кости

**Файлы:**
- Создать: `Assets/_Chimera/Scripts/Player/Bone.cs`
- Изменить: `Assets/_Chimera/Scripts/Player/SpeciesSO.cs` (добавить поле `bones`)

- [ ] **Шаг 1: Создать `Bone.cs`**

```csharp
using UnityEngine;

/// <summary>КОСТЬ — линейный примитив скелета. В отличие от места (габаритная коробка в долях родителя,
/// девять неанатомичных чисел), кость описана ДЛИНОЙ И УГЛОМ — ровно тем, что снимается с референса.
///
/// ОСЬ КОСТИ — ЛОКАЛЬНАЯ +Y: кость растёт от своего начала вверх, а `dir` разворачивает её куда нужно.
/// Поворот НАСЛЕДУЕТСЯ от родителя, поэтому цепочка «плечо → предплечье → пясть» задаётся углами в
/// суставах, а не абсолютными позициями: согнул плечо — вся нога поехала за ним.</summary>
[System.Serializable]
public class Bone
{
    public string name;              // имя-адрес: по нему находят кость модули и правила
    public string parent;            // родительская кость («» у корня)
    public float attach = 1f;        // ГДЕ НА РОДИТЕЛЕ: доля вдоль его длины (1 = конец, 0.5 = середина)
    public float length = 0.1f;      // ДЛИНА В МЕТРАХ — главное число, снимается с референса
    public Vector3 dir;              // углы поворота относительно родителя (наследуются вниз по цепи)

    // ТОЛЩИНА У КОНЦОВ. Разные r0 и r1 дают веретено вместо капсулы — именно этим силуэт перестаёт
    // быть «шариками»: кость сужается к суставу, мясо идёт конусом
    public float r0 = 0.05f;
    public float r1 = 0.05f;

    // СЕЧЕНИЕ: ширина к глубине. 1 = круг, 0.5 = овал вдвое уже, чем глубже (грудь волка анфас плоская)
    public float section = 1f;

    public int chain;                // сегментов, если кость составная (позвоночник, рёбра, хвост)
    public bool mirrorX;             // пара зеркально по X (конечности, рёбра)
}
```

- [ ] **Шаг 2: Добавить поле в `SpeciesSO`**

В `SpeciesSO.cs` после строки `public BodySocket[] sockets;` добавить:

```csharp
    // СКЕЛЕТ (спека 2026-08-18): кости живут РЯДОМ с сокет-планом, не заменяя его. Место, чьё имя
    // совпадает с именем кости, отдаёт ей свою форму — остальные места строятся по-старому.
    // Так прототип проверяется на двух частях, не ломая три оставшихся вида
    public Bone[] bones;
```

- [ ] **Шаг 3: Проверить компиляцию**

Запустить: `awk '{o+=gsub(/\{/,"{"); c+=gsub(/\}/,"}")} END{print o"/"c}' Assets/_Chimera/Scripts/Player/Bone.cs`
Ожидается: одинаковые числа.

---

### Задача 2: Расчёт скелета и проверка непрерывности

**Файлы:**
- Создать: `Assets/_Chimera/Scripts/Player/SkeletonBuilder.cs`

- [ ] **Шаг 1: Написать расчёт позиций**

```csharp
using System.Collections.Generic;
using UnityEngine;

/// <summary>СБОРКА СКЕЛЕТА: считает мировые позиции костей и наращивает на них мясо.
///
/// ГЛАВНЫЙ ИНВАРИАНТ — СУСТАВ ОБЩИЙ: начало дочерней кости ЕСТЬ точка на родительской, а радиус там
/// один. Отсюда по построению невозможны дефекты, которые в парадигме мест ловились только глазом:
/// деталь не висит в воздухе (её начало — конец предыдущей), нет уступа (радиусы совпадают),
/// ничего не «отваливается» (сустав принадлежит обеим костям).</summary>
public static class SkeletonBuilder
{
    /// <summary>Где кость начинается и куда смотрит. `depth` страхует от цикла в данных.</summary>
    public static (Vector3 pos, Quaternion rot) Place(Bone b, Dictionary<string, Bone> byName,
                                                      Dictionary<string, (Vector3, Quaternion)> done, int depth = 0)
    {
        if (done.TryGetValue(b.name, out var cached)) return cached;

        var rot = Quaternion.Euler(b.dir);
        var pos = Vector3.zero;

        if (depth < 16 && !string.IsNullOrEmpty(b.parent) && byName.TryGetValue(b.parent, out var par) && par != b)
        {
            var (ppos, prot) = Place(par, byName, done, depth + 1);
            // ТОЧКА НА РОДИТЕЛЕ вдоль его оси (+Y), доля `attach`: 1 = конец кости, 0.5 = середина.
            // Именно здесь сустав становится общим — ребёнок стартует ровно там, где кончается родитель
            pos = ppos + prot * (Vector3.up * (par.length * b.attach));
            rot = prot * rot;   // поворот НАСЛЕДУЕТСЯ: согнул плечо — поехала вся нога
        }

        done[b.name] = (pos, rot);
        return (pos, rot);
    }

    /// <summary>Конец кости — начало её детей.</summary>
    public static Vector3 Tip(Bone b, Vector3 pos, Quaternion rot) => pos + rot * (Vector3.up * b.length);

    /// <summary>Радиус кости в точке `t` вдоль её длины (0 — начало, 1 — конец).</summary>
    public static float RadiusAt(Bone b, float t) => Mathf.Lerp(b.r0, b.r1, Mathf.Clamp01(t));
}
```

- [ ] **Шаг 2: Проверить непрерывность расчётом (роль «упавшего теста»)**

Создать временный скрипт проверки и выполнить:

```bash
cd /c/Users/semion/Documents/Chimera_game/ChimeraEvolution && /c/ProgramData/Anaconda3/python.exe - <<'PYEOF'
# -*- coding: utf-8 -*-
# ПРОВЕРКА ИНВАРИАНТА НА БУМАГЕ, до всякой сборки: конец родителя должен совпасть с началом ребёнка
import math
bones = [
    ('плечо',      '',           1.0, 0.170, (0,0,0),      0.055, 0.045),
    ('предплечье', 'плечо',      1.0, 0.190, (25,0,0),     0.045, 0.032),
    ('пясть',      'предплечье', 1.0, 0.120, (-20,0,0),    0.032, 0.026),
    ('лапа',       'пясть',      1.0, 0.060, (-5,0,0),     0.026, 0.030),
]
pos, rot = {}, {}
for nm, par, att, ln, d, r0, r1 in bones:
    a = math.radians(d[0])
    if par == '':
        pos[nm] = (0.0, 0.0); rot[nm] = a
    else:
        pl = dict((b[0], b[3]) for b in bones)[par]
        px, py = pos[par]; pr = rot[par]
        pos[nm] = (px + math.sin(pr)*pl*att, py + math.cos(pr)*pl*att)
        rot[nm] = pr + a
    print('%-11s начало (%.3f, %.3f)' % (nm, pos[nm][0], pos[nm][1]))
PYEOF
```

Ожидается: у каждой следующей кости начало совпадает с концом предыдущей — разрывов нет ни одного.

- [ ] **Шаг 3: Не коммитить.** Код без данных ничего не строит; коммит будет после задачи 4.

---

### Задача 3: Правило обрастания — веретено вдоль кости

**Файлы:**
- Изменить: `Assets/_Chimera/Scripts/Player/SkeletonBuilder.cs` (добавить `Grow`)

- [ ] **Шаг 1: Добавить наращивание мяса**

```csharp
    /// <summary>МЯСО НА КОСТИ — веретено: цепочка сфер от `r0` к `r1` плюс шар в суставе.
    ///
    /// ПЛОТНОСТЬ РЕШАЕТ ГЛАДКОСТЬ. Найдено 15.08 на ручной лепке: в кубическом языке силуэт сглаживает
    /// не форма отдельной детали, а ЧИСЛО промежуточных объёмов — два шара встык читаются уступом,
    /// пять перетекают. Шаг берём от толщины: чем тоньше кость, тем чаще сегменты.
    ///
    /// СЕЧЕНИЕ НЕРАВНОМЕРНОЕ. `section` сплющивает объём по X: грудь волка анфас вдвое уже, чем глубока,
    /// и без этого корпус читается бочкой, сколько ни правь длины.</summary>
    public static void Grow(Transform parent, Bone b, Vector3 pos, Quaternion rot, float side,
                            List<GameObject> made, Material mat)
    {
        float avg = Mathf.Max(0.001f, (b.r0 + b.r1) * 0.5f);
        int segs = Mathf.Clamp(Mathf.RoundToInt(b.length / avg), 2, 12);

        for (int i = 0; i <= segs; i++)
        {
            float t = i / (float)segs;
            float r = RadiusAt(b, t);
            var p = pos + rot * (Vector3.up * (b.length * t));
            if (side < 0f) p.x = -p.x;

            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = b.name;                       // ИМЯ ПО КОСТИ — контракт имён частей продолжает работать
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.position = p;
            go.transform.rotation = rot;
            go.transform.localScale = new Vector3(r * 2f * b.section, r * 2f, r * 2f);
            if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
            made.Add(go);
        }
    }
```

- [ ] **Шаг 2: Проверить, что веретено сужается**

```bash
cd /c/Users/semion/Documents/Chimera_game/ChimeraEvolution && /c/ProgramData/Anaconda3/python.exe - <<'PYEOF'
# -*- coding: utf-8 -*-
# СКОЛЬКО СЕГМЕНТОВ И КАК СУЖАЕТСЯ — до сборки
for nm, ln, r0, r1 in (('плечо',0.170,0.055,0.045), ('предплечье',0.190,0.045,0.032),
                       ('пясть',0.120,0.032,0.026), ('лапа',0.060,0.026,0.030)):
    avg = (r0+r1)/2; segs = max(2, min(12, round(ln/avg)))
    print('%-11s сегментов %2d   радиус %.3f -> %.3f' % (nm, segs, r0, r1))
PYEOF
```

Ожидается: у каждой кости 2–12 сегментов, радиус меняется от начала к концу — то есть строится веретено, а не капсула.

---

### Задача 4: Данные — передняя нога волка на костях

**Файлы:**
- Изменить: `Assets/_Chimera/Scripts/Editor/SpeciesBootstrap.cs` (блок `speciesName = "Волк"`, добавить `wolf.bones`)

- [ ] **Шаг 1: Снять замеры с нынешней ноги**

```bash
cd /c/Users/semion/Documents/Chimera_game/ChimeraEvolution && /c/ProgramData/Anaconda3/python.exe - <<'PYEOF'
# -*- coding: utf-8 -*-
# ОТКУДА БЕРЁМ ДЛИНЫ: из карты тел, раздел «Волк». Нога от земли (0.000) до плечевого сустава (0.725)
import io, os, re, sys
s = io.open(os.path.join('Docs','Диаграммы','КАРТА_ТЕЛ.md'), encoding='utf-8').read()
i = s.index('## Волк'); blk = s[i:s.index('\n## ', i+5)]
for r in blk.split('\n'):
    if r.startswith('| Руки'):
        sys.stdout.buffer.write((r[:110] + '\n').encode('utf-8'))
PYEOF
```

- [ ] **Шаг 2: Вписать кости ноги**

В `SpeciesBootstrap.cs`, сразу после `wolf.sockets = new[] { ... };`, добавить:

```csharp
        // СКЕЛЕТ — ПРОТОТИП (спека 2026-08-18). Пока только передняя нога и голова: проверяем ОДИН метод
        // на типичной цепочке и на сложном комплексе. Длины сняты с референса в долях холки 1.170:
        // локоть на 0.50 холки, низ груди ниже локтя, лапа ровно на земле
        wolf.bones = new[]
        {
            new Bone { name = "плечо",      parent = "",           length = 0.170f, dir = new Vector3(160f, 0f, 0f), r0 = 0.055f, r1 = 0.045f, section = 0.85f, mirrorX = true },
            new Bone { name = "предплечье", parent = "плечо",      length = 0.190f, dir = new Vector3(25f, 0f, 0f),  r0 = 0.045f, r1 = 0.032f, section = 0.85f, mirrorX = true },
            new Bone { name = "пясть",      parent = "предплечье", length = 0.120f, dir = new Vector3(-18f, 0f, 0f), r0 = 0.032f, r1 = 0.026f, section = 0.85f, mirrorX = true },
            new Bone { name = "лапа",       parent = "пясть",      length = 0.060f, dir = new Vector3(-7f, 0f, 0f),  r0 = 0.026f, r1 = 0.030f, section = 1.10f, mirrorX = true },
        };
```

- [ ] **Шаг 3: Посчитать, куда встанет нога, ДО сборки**

```bash
cd /c/Users/semion/Documents/Chimera_game/ChimeraEvolution && /c/ProgramData/Anaconda3/python.exe - <<'PYEOF'
# -*- coding: utf-8 -*-
import math
bones = [('плечо','',0.170,160.0),('предплечье','плечо',0.190,25.0),
         ('пясть','предплечье',0.120,-18.0),('лапа','пясть',0.060,-7.0)]
L = dict((b[0], b[2]) for b in bones)
pos, rot = {}, {}
start_y = 0.725   # верх ноги по карте
for nm, par, ln, d in bones:
    a = math.radians(d)
    if par == '': pos[nm] = (0.0, start_y); rot[nm] = a
    else:
        px, py = pos[par]; pr = rot[par]
        pos[nm] = (px + math.sin(pr)*L[par], py + math.cos(pr)*L[par]); rot[nm] = pr + a
    tipx = pos[nm][0] + math.sin(rot[nm])*ln
    tipy = pos[nm][1] + math.cos(rot[nm])*ln
    print('%-11s начало Y %.3f  конец Y %.3f  вынос Z %+.3f' % (nm, pos[nm][1], tipy, tipx))
PYEOF
```

Ожидается: конец лапы **на 0.000 ± 0.02** (стоит на земле), локоть (конец плеча) около **0.585** = половина холки, зигзаг по Z — локоть назад, пясть вперёд.

- [ ] **Шаг 4: Если конец лапы не на земле — править ДЛИНЫ, а не смещения.** Кость линейна: не сходится низ — значит сумма длин или углы, третьего нет.

---

### Задача 5: Сборка ноги в билдере и первый плейтест

**Файлы:**
- Изменить: `Assets/_Chimera/Scripts/Player/MorphBuilder.cs` (вызвать скелет для мест, покрытых костями)

- [ ] **Шаг 1: Подключить скелет к сборке**

В `MorphBuilder.Build`, сразу после создания `container`, добавить:

```csharp
        // СКЕЛЕТ ПЕРЕХВАТЫВАЕТ СВОИ МЕСТА. Кость с именем места означает: эту часть тела строит скелет,
        // а старый сокет-план её пропускает. Так прототип живёт рядом со старой системой, не ломая
        // остальные виды — у них `bones` просто пуст
        var boneNames = new HashSet<string>();
        if (chassis.bones != null)
        {
            var byBone = new Dictionary<string, Bone>();
            foreach (var bn in chassis.bones)
                if (bn != null && !string.IsNullOrEmpty(bn.name)) byBone[bn.name] = bn;
            var donePos = new Dictionary<string, (Vector3, Quaternion)>();
            foreach (var bn in chassis.bones)
            {
                if (bn == null || string.IsNullOrEmpty(bn.name)) continue;
                boneNames.Add(bn.name);
                var (bp, br) = SkeletonBuilder.Place(bn, byBone, donePos);
                bp += Vector3.up * footY;
                SkeletonBuilder.Grow(container.transform, bn, bp, br, +1f, made0, null);
                if (bn.mirrorX) SkeletonBuilder.Grow(container.transform, bn, bp, br, -1f, made0, null);
            }
        }
```

Перед циклом по сокетам объявить `var made0 = new List<GameObject>();`, а в самом цикле первой строкой добавить пропуск:

```csharp
            if (boneNames.Contains(socket.name)) continue;   // эту часть строит скелет
```

- [ ] **Шаг 2: Убрать старую переднюю ногу из сокет-плана волка**

В `SpeciesBootstrap.cs` у волка место `Руки` временно переименовать в `Руки-старое`, чтобы кости и места не рисовали одно и то же. Проверить, что имя не читается контрактом:

```bash
grep -rn '"Руки"' Assets/_Chimera/Scripts --include=*.cs | grep -v SpeciesBootstrap | head
```

Ожидается: пусто либо только слоты органов — тогда переименование безопасно.

- [ ] **Шаг 3: Прогон и плейтест**

Попросить: **Chimera → Создать дефолтные виды** + **Ctrl+S**, затем **Play** и скрин волка строго в профиль.

Смотреть: нога стоит на земле, локоть не размазан, сегменты перетекают друг в друга, зигзаг читается.

- [ ] **Шаг 4: Коммит после подтверждения плейтестом**

```bash
git add Assets/_Chimera/Scripts/Player/Bone.cs Assets/_Chimera/Scripts/Player/SkeletonBuilder.cs Assets/_Chimera/Scripts/Player/SpeciesSO.cs Assets/_Chimera/Scripts/Player/MorphBuilder.cs Assets/_Chimera/Scripts/Editor/SpeciesBootstrap.cs Assets/_Chimera/Data/*.asset
git commit -m "Скелет: кость как примитив, передняя нога волка на костях"
```

---

### Задача 6: Голова волка тем же методом

**Файлы:**
- Изменить: `Assets/_Chimera/Scripts/Editor/SpeciesBootstrap.cs` (добавить кости черепа)

- [ ] **Шаг 1: Вписать кости черепа**

Дописать в `wolf.bones` (длины из ортографий черепа: коробка 45% длины головы, ростр 55%, длина головы 0.47):

```csharp
            new Bone { name = "коробка",  parent = "",         length = 0.210f, dir = new Vector3(90f, 0f, 0f),  r0 = 0.090f, r1 = 0.075f, section = 0.85f },
            new Bone { name = "ростр",    parent = "коробка",  length = 0.260f, dir = new Vector3(-8f, 0f, 0f),  r0 = 0.065f, r1 = 0.047f, section = 0.72f },
            new Bone { name = "скула",    parent = "коробка",  attach = 0.45f, length = 0.130f, dir = new Vector3(-10f, 55f, 0f), r0 = 0.030f, r1 = 0.022f, section = 1f, mirrorX = true },
            new Bone { name = "челюсть",  parent = "коробка",  attach = 0.30f, length = 0.240f, dir = new Vector3(-14f, 0f, 0f), r0 = 0.045f, r1 = 0.028f, section = 0.80f },
```

- [ ] **Шаг 2: Посчитать габарит головы ДО сборки**

```bash
cd /c/Users/semion/Documents/Chimera_game/ChimeraEvolution && /c/ProgramData/Anaconda3/python.exe - <<'PYEOF'
# -*- coding: utf-8 -*-
import math
# коробка вдоль Z (dir 90 по X), ростр продолжает её с наклоном −8
box_len, rostr_len = 0.210, 0.260
total = box_len + rostr_len
print('длина головы %.3f  (референс 0.470, допуск 0.42..0.51)' % total)
print('коробка %.0f%% / ростр %.0f%%  (референс 45/55)' % (box_len/total*100, rostr_len/total*100))
print('ширина по скулам %.3f  (референс 0.263)' % (2*(0.030 + 0.130*math.sin(math.radians(55)))))
PYEOF
```

Ожидается: длина в допуске, пропорция коробки к ростру около 45/55, ширина по скулам близка к 0.263.

- [ ] **Шаг 3: Убрать старую голову из сокет-плана**

Переименовать место `голова` волка в `голова-старое`. **Внимание:** имя `голова` читается контрактом (`Telegraph.IsHeadName`, `PlayerController.IsOwnFace`), поэтому кость обязана называться так, чтобы эмоц-тинт продолжал работать — проверить:

```bash
grep -n "IsHeadName" -A 4 Assets/_Chimera/Scripts/Combat/Feedback/Telegraph.cs
```

Если в списке есть `"голова"`, добавить в него `"коробка"`, `"ростр"`, `"челюсть"` — иначе ярость перестанет краситься на морде.

- [ ] **Шаг 4: Прогон и плейтест**

Попросить прогон и **два** скрина: профиль и анфас. Смотреть: морда длиннее коробки, скулы дают ширину анфас, челюсть под ростром, переходы не рассыпаются.

- [ ] **Шаг 5: Коммит после подтверждения**

```bash
git add Assets/_Chimera/Scripts/Editor/SpeciesBootstrap.cs Assets/_Chimera/Scripts/Combat/Feedback/Telegraph.cs Assets/_Chimera/Data/*.asset
git commit -m "Скелет: голова волка на костях тем же методом"
```

---

### Задача 7: Приговор методу

- [ ] **Шаг 1: Сравнить с `362e307` по критериям спеки**

Взять скрины из старой парадигмы и новые, ответить письменно:

```
локоть не размазан            да / нет
грудь не отваливается          да / нет   (проверяется после переноса рёбер, но переходы видны уже)
морда не рассыпается           да / нет
ручных правок на силуэт        больше / меньше
голова потребовала ОТДЕЛЬНЫХ приёмов, которых не было у ноги?   да / нет
```

Последняя строка — главная. Если да, метод не обобщается и спеку надо править до переноса остальных видов.

- [ ] **Шаг 2: Записать вывод в память и спеку**

Дописать в `Docs/superpowers/specs/2026-08-18-skeletnaya-morfologiya.md` раздел «Результат прототипа» с числами и вердиктом.
