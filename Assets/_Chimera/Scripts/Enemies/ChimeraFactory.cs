using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>СБОРКА ХИМЕРЫ — один путь для всех, кто рождается СОСТАВОМ, а не префабом: тест-химера, босс,
/// дальше — химеры леса. Носитель оживляется так же, как оживлялась тест-химера (#4b-2), а состав набирается
/// ТЕМ ЖЕ конструктором, что у игрока: `Configure` → `Install` → `GrantChimeraSlot`.
///     Своего пути сборки нет ни у кого. Вид, босс и игрок различаются не кодом, а тем, ЧТО легло в слоты —
/// иначе каждый новый тип существа приносил бы свой сборщик, и они расходились бы молча (так уже было с
/// вервольфом: флаг «всё надето с рождения» жил мимо конструктора и отсекал его от морфа).</summary>
public static class ChimeraFactory
{
    /// <summary>ПРИРОДНАЯ ОСОБЬ — экспрессия NPC, рождённого составом. У префабов это сериализованное поле
    /// (волк 0.45, змея/лось/ёж 0.5); у фабричного тела его не было вовсе — 0 означает «все звериные бонусы
    /// выключены», и `Power` был нулём. Тюнинг вида можно передать явно параметром `expression`.</summary>
    public const float NaturalExpression = 0.5f;

    /// <summary>Родить химеру. Порядок значим и выстрадан тестбедом:
    /// CC и Health — ДО тела (его Awake их ищет), `beforeBody` — ДО доставок (укус кэширует ярость в Awake),
    /// состав — после `Configure` (слоты строятся из шасси и доноров), психика — после состава (MostKin валиден).
    /// `Configure` ставит и платформу эволюции (доноры/родство/Metamorph) — её Awake пропускает: шасси в этот
    /// момент ещё null.</summary>
    /// <param name="beforeBody">то, что должно существовать раньше доставок и тела (модуль боссовости, ярость)</param>
    /// <param name="compose">состав — ТОЛЬКО через публичный API конструктора</param>
    /// <param name="expression">экспрессия особи (0 — природная, <see cref="NaturalExpression"/>)</param>
    public static CreatureBody Spawn(SpeciesSO chassis, SpeciesSO[] donors, Vector3 position, string name,
                                     Action<GameObject> beforeBody = null, Action<CreatureBody> compose = null,
                                     float expression = NaturalExpression)
    {
        if (chassis == null) { Debug.LogWarning($"ChimeraFactory: у «{name}» нет шасси — рожать нечего"); return null; }

        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name;
        go.transform.position = position;   // ДО CharacterController: заданное после он перебивает (гоча проекта)
        if (go.TryGetComponent<Collider>(out var col)) UnityEngine.Object.Destroy(col);
        var cc = go.AddComponent<CharacterController>();
        // КОРЕНЬ — НА ЗЕМЛЕ: низ капсулы = низ корня (как у префабов: центр = полвысоты). Было center = 0,
        // то есть половина капсулы под землёй — спавнер ставил корень на землю, и тело рождалось полузаглублённым.
        cc.height = 1f; cc.radius = 0.5f; cc.center = new Vector3(0f, 0.5f, 0f);
        go.AddComponent<Health>();          // Max задаст тело в Recompute

        beforeBody?.Invoke(go);

        // ДОСТАВКИ заводит тело по записям органов (провизия в Recompute) — уже после beforeBody, ярость им видна.
        // Гарантийного укуса нет: альфа бьёт тем, что дало тело (Arsenal), а без записей удара — только сближается
        var body = go.AddComponent<CreatureBody>();
        // ЭКСПРЕССИЯ — ДО Configure: у префаба это сериализованное поле, у фабричного тела его не было,
        // и `Power` (мощь носителя для порогов психик) приходил нулём. 0 — «не задано»: подставляем природную.
        body.Configure(chassis, donors, tintFromComposition: true,
                       newExpression: expression > 0f ? expression : NaturalExpression);
        // ЭКОНОМИКА ПУЛА — ИГРОКУ, А НЕ РОЖДЁННОМУ СОСТАВОМ. Холодная химера родится с родством 0 → без скидки
        // звериные органы не влезают в пул, и все Install молча отваливались: тело вышло бы чистым
        body.ExpandPool(9999);

        compose?.Invoke(body);

        // МЕТАМОРФОЗА МОГЛА УЖЕ ПОВЕСИТЬ ПСИХИКУ: с платформой эволюции тело ре-диспатчит на смену доминанты,
        // а компоновка её двигает. Второй Attach дал бы ДВА модуля (Destroy отложен до конца кадра) —
        // двойные атаки и двойной расход. Поэтому добираем только пустое.
        if (!PsycheDispatch.HasPsyche(body)) PsycheDispatch.Attach(body);

        // ЗАГЛУШКУ ДОЛОЙ, ЕСЛИ МОРФ ЧТО-ТО ПОСТРОИЛ. Шар остаётся честным признаком «у этого шасси нет плана
        // мест» (синтетика в тестах), а не украшением поверх настоящего тела
        if (go.GetComponentsInChildren<Renderer>().Length > 1)
        {
            if (go.TryGetComponent<MeshRenderer>(out var mr)) UnityEngine.Object.Destroy(mr);
            if (go.TryGetComponent<MeshFilter>(out var mf)) UnityEngine.Object.Destroy(mf);
        }
        return body;
    }

    /// <summary>РЕЦЕПТ ОДНОГО ДОНОРА: в каждый родной слот, где у вида `species` есть орган, ставится его орган.
    /// Так честно, через конструктор, собирается оборотень — человек плюс весь набор одного вида (вервольф — если волк).</summary>
    public static int InstallAllFrom(CreatureBody body, string species)
    {
        int ok = 0;
        for (int s = 0; s < body.SlotCount; s++)
        {
            if (body.GetSlot(s).chimera) continue;
            if (TryInstall(body, s, v => !v.native && v.species == species)) ok++;
        }
        return ok;
    }

    /// <summary>СЛУЧАЙНЫЙ ЗВЕРИНЫЙ СОСТАВ: `attempts` попыток поставить чужой орган в случайный родной слот.
    /// Попытка может не лечь (слот уже занят этим органом, орган носится в другом месте) — это норма.</summary>
    public static int InstallRandomBeasts(CreatureBody body, int attempts)
    {
        int ok = 0;
        for (int i = 0; i < attempts; i++)
        {
            int s = UnityEngine.Random.Range(0, body.SlotCount);
            if (body.GetSlot(s).chimera) continue;
            if (TryInstall(body, s, v => !v.native)) ok++;
        }
        return ok;
    }

    /// <summary>РАСШИРЕНИЕ ХИМЕРНЫМИ СЛОТАМИ: слот выдаётся и в него ставится случайный чужой орган, какой ещё не
    /// носится. Запрет «один орган в двух местах» держит сам конструктор — здесь его не повторяем.</summary>
    public static int GrantAndFillChimeraSlots(CreatureBody body, int count)
    {
        int ok = 0;
        for (int i = 0; i < count; i++)
        {
            body.GrantChimeraSlot();
            if (TryInstall(body, body.SlotCount - 1, v => !v.native)) ok++;
        }
        return ok;
    }

    /// <summary>Состав одной строкой — для лога и дев-панели: «Пасть: Пасть (Волк) · Руки: Коготь (Волк) · …».</summary>
    public static string Describe(CreatureBody body)
    {
        var parts = new List<string>();
        for (int s = 0; s < body.SlotCount; s++)
        {
            var v = body.GetSlot(s);
            if (!v.installed) continue;
            parts.Add($"{(v.chimera ? "химерный" : v.slot)}: {v.organName} ({v.species})");
        }
        return parts.Count > 0 ? string.Join(" · ", parts) : "без чужих органов";
    }

    static bool TryInstall(CreatureBody body, int slot, Predicate<CreatureBody.VariantView> filter)
    {
        var vars = body.GetVariants(slot);
        var candidates = new List<int>();
        for (int v = 0; v < vars.Count; v++)
            if (filter(vars[v]) && vars[v].affordable && !vars[v].worn) candidates.Add(v);
        while (candidates.Count > 0) // в случайном порядке — первый, который конструктор принял
        {
            int k = UnityEngine.Random.Range(0, candidates.Count);
            if (body.Install(slot, candidates[k])) return true;
            candidates.RemoveAt(k);
        }
        return false;
    }
}
