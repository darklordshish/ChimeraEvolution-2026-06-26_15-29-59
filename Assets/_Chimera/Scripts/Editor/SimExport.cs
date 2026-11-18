using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Chimera.Laws;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// ВЫГРУЗКА ВХОДОВ ЗАКОНОВ (план Ф0, С9): `Docs/Диаграммы/Эволюция/данные.json` — всё, что симулятор вне Unity берёт у игры:
/// виды (шасси и база), органы и вклады, записи приёмов, `LawsConfig`, `EvolutionConfig` павильона, экспрессия и разброс
/// префабов, логова сцены `ForestPavilion` (вид, ёмкость, темп в игровых часах) и площадь купола.
///
/// Формат — `JsonUtility` над `Chimera.Laws.SimData`; в шапке версия схемы и отпечаток FNV по содержимому, БЕЗ даты: тот же
/// состав — тот же файл. Цель выгрузки — исчезнуть: когда данные станут файлами, которые читают и игра, и симулятор, команда
/// не нужна (решение геймдизайнера 18.11, спека симулятора §0 п.2).
///
/// ДВЕ ЧАСТИ. Ядро (виды, органы, конфиги, префабы) собирается из ассетов без сцены — его сторожит EditMode-тест
/// `SimExportFreshTests`. Логова существуют только после сборки мира `ForestPavilion` (`ForestBuilder.Build`) — это минуты и
/// смена открытой сцены, поэтому их сверяет явный тест, а команда собирает их сама.
/// </summary>
public static class SimExport
{
    public const string OutPath = "Docs/Диаграммы/Эволюция/данные.json";
    const string ScenePath = "Assets/Scenes/ForestPavilion.unity";

    // вид → префаб NPC: числа экспрессии и разброса лежат на нём (у человека префаба нет — он игрок)
    static readonly (string species, string prefab)[] Prefabs =
    {
        ("Волк", "Wolf"), ("Лось", "Moose"), ("Ёж", "Hedgehog"), ("Змея", "Snake"),
    };

    [MenuItem("Chimera/Выгрузить данные симулятора")]
    public static void Generate()
    {
        var data = BuildCore();
        data.lairs = BuildLairs(out var scene, out var diameter);
        data.world.scene = scene;
        data.world.mapDiameter = diameter;
        data.world.domeArea = Mathf.PI * (diameter * 0.5f) * (diameter * 0.5f);
        File.WriteAllText(OutPath, Serialize(data), new UTF8Encoding(false));
        AssetDatabase.Refresh();
        Debug.Log($"[SimExport] {OutPath}: видов {data.species.Length}, логов {data.lairs.Length}, отпечаток {data.fingerprint}");
    }

    /// <summary>Текст файла: `JsonUtility` с отступами, пустой отпечаток → хеш → отпечаток на место. `\n`, без BOM.</summary>
    public static string Serialize(SimData data)
    {
        data.fingerprint = "";
        string blank = JsonUtility.ToJson(data, true).Replace("\r\n", "\n");
        data.fingerprint = SimData.Fnv64(blank);
        return JsonUtility.ToJson(data, true).Replace("\r\n", "\n");
    }

    /// <summary>Ядро выгрузки: всё, что берётся из ассетов и префабов без сцены. Мир и логова — `world` без сцены и пустые `lairs`
    /// (их дописывает `Generate` либо явный тест).</summary>
    public static SimData BuildCore()
    {
        var data = new SimData();
        float dayMinutes = WorldClock.DefaultDayMinutes;
        data.world = new SimWorld
        {
            scene = "", mapDiameter = 0f, domeArea = 0f,
            dayMinutes = dayMinutes, hoursPerSecond = WorldClock.HoursPerRealSecond(dayMinutes),
        };

        var cfg = LawsConfig.Current;
        data.laws = new SimLaws { venom = cfg.venom, bleed = cfg.bleed, rage = cfg.rage, slow = cfg.slow, thorns = cfg.thorns, hunger = cfg.hunger };

        var all = new List<SpeciesSO>();
        foreach (var guid in AssetDatabase.FindAssets("t:SpeciesSO"))
        {
            var so = AssetDatabase.LoadAssetAtPath<SpeciesSO>(AssetDatabase.GUIDToAssetPath(guid));
            if (so != null && AssetDatabase.GUIDToAssetPath(guid).StartsWith("Assets/_Chimera/Data/") ) all.Add(so);
        }
        all.Sort((a, b) => string.CompareOrdinal(a.speciesName, b.speciesName));   // порядок не от файловой системы
        data.species = new SimSpecies[all.Count];
        for (int i = 0; i < all.Count; i++) data.species[i] = Species(all[i]);

        data.evolution = Evolution(all);
        return data;
    }

    static SimSpecies Species(SpeciesSO so)
    {
        var s = new SimSpecies
        {
            name = so.speciesName, mutagenPool = so.mutagenPool, eatsMeat = so.eatsMeat, massive = so.massive,
            formScale = so.FormScale, mass = so.mass,
            // как их читает тело (`CreatureBody.BaseHp` и родня): ноль в ассете — «бутстрап не прогнан», подставляется норма
            baseHp = so.baseHp > 0 ? so.baseHp : 75f,
            baseStamina = so.baseStamina > 0 ? so.baseStamina : 100f,
            baseStaminaRegen = so.baseStaminaRegen > 0f ? so.baseStaminaRegen : 12f,
            diet = so.diet != null ? (DietEntry[])so.diet.Clone() : new DietEntry[0],
            fleesFrom = so.fleesFrom != null ? (string[])so.fleesFrom.Clone() : new string[0],
        };
        var organs = new List<SimOrgan>();
        if (so.organs != null) foreach (var o in so.organs) organs.Add(OrganOf(o));
        s.organs = organs.ToArray();
        s.prefab = PrefabOf(so.speciesName);
        return s;
    }

    static SimOrgan OrganOf(Organ o)
    {
        var abilities = new List<SimAbility>();
        if (o.abilities != null) foreach (var a in o.abilities) if (a != null) abilities.Add(AbilityOf(a));
        return new SimOrgan
        {
            name = o.organName, slot = o.slot, cost = o.cost, chassisOnly = o.chassisOnly, nativeOnly = o.nativeOnly,
            nativeChassis = o.nativeChassis ?? "",
            values = new OrganValues
            {
                hpBonus = o.hpBonus, staminaBonus = o.staminaBonus, staminaRegenBonus = o.staminaRegenBonus,
                atkCooldown = o.atkCooldown, moveSpeed = o.moveSpeed, dashSpeed = o.dashSpeed, dashCooldown = o.dashCooldown,
                damageReduction = o.damageReduction, regen = o.regen, regenOOC = o.regenOOC,
                dashDuration = o.dashDuration, thermalRange = o.thermalRange, hearingMult = o.hearingMult,
                enablesScent = o.enablesScent, coldBlooded = o.coldBlooded, camo = o.camo, enablesThermal = o.enablesThermal,
                thorns = o.thorns, venomResist = o.venomResist, bleedResist = o.bleedResist,
                keenHearing = o.keenHearing, insight = o.insight, digestion = o.digestion,
            },
            abilities = abilities.ToArray(),
        };
    }

    // все числа записи (float/int) в порядке объявления полей — как их видит `AbilityData.Resolve`
    static SimAbility AbilityOf(AbilityData a)
    {
        var list = new List<SimNumber>();
        var type = a.GetType();
        foreach (var f in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (f.FieldType != typeof(float) && f.FieldType != typeof(int)) continue;
            if (f.IsDefined(typeof(NonSerializedAttribute))) continue;   // `power` — модификатор индивида, в ассет не пишется
            bool isInt = f.FieldType == typeof(int);
            list.Add(new SimNumber
            {
                name = f.Name, isInt = isInt, value = isInt ? (int)f.GetValue(a) : (float)f.GetValue(a),
                expressed = f.IsDefined(typeof(ExpressedAttribute)), lowerIsBetter = f.IsDefined(typeof(LowerIsBetterAttribute)),
            });
        }
        return new SimAbility { type = type.Name, nativeOnly = a.NativeOnly, numbers = list.ToArray() };
    }

    static SimPrefab PrefabOf(string species)
    {
        var p = new SimPrefab();
        foreach (var (sp, prefab) in Prefabs)
        {
            if (sp != species) continue;
            var go = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Chimera/Prefabs/" + prefab + ".prefab");
            if (go == null) throw new InvalidOperationException("нет префаба " + prefab);
            p.hasPrefab = true; p.prefab = prefab;
            var body = go.GetComponent<CreatureBody>();
            if (body != null)
            {
                var so = new SerializedObject(body);
                p.expression = F(so, "expression", p.expression);
                p.applyVitals = B(so, "applyVitals", p.applyVitals);
                p.maxDamageReduction = F(so, "maxDamageReduction", p.maxDamageReduction);
                p.minAtkCooldown = F(so, "minAtkCooldown", p.minAtkCooldown);
                p.discountPerAffinity = F(so, "discountPerAffinity", p.discountPerAffinity);
                p.maxDiscount = F(so, "maxDiscount", p.maxDiscount);
                p.bonusStartAffinity = F(so, "bonusStartAffinity", p.bonusStartAffinity);
                p.bonusFullAffinity = F(so, "bonusFullAffinity", p.bonusFullAffinity);
                p.maxBonusMult = F(so, "maxBonusMult", p.maxBonusMult);
                float perOrgan = F(so, "affinityPerOrgan", p.affinityPerOrgan);
                p.affinityPerOrgan = perOrgan > 0f ? perOrgan : p.affinityPerOrgan;   // ноль — «не настроено» (гоча нового поля)
            }
            var variance = go.GetComponent<SpawnVariance>();
            if (variance != null)
            {
                var so = new SerializedObject(variance);
                p.hpSpread = F(so, "hpSpread", p.hpSpread);
                p.damageSpread = F(so, "damageSpread", p.damageSpread);
                p.speedSpread = F(so, "speedSpread", p.speedSpread);
            }
            foreach (var c in go.GetComponents<MonoBehaviour>())
            {
                if (c == null) continue;
                var so = new SerializedObject(c);
                var rhythm = so.FindProperty("attackCooldown");
                if (rhythm == null || rhythm.propertyType != SerializedPropertyType.Float) continue;
                p.psyche = c.GetType().Name; p.psycheRhythm = rhythm.floatValue;
                break;
            }
            return p;
        }
        return p;   // человек: префаба нет
    }

    static float F(SerializedObject so, string name, float fallback)
    {
        var pr = so.FindProperty(name);
        return pr != null && pr.propertyType == SerializedPropertyType.Float ? pr.floatValue : fallback;
    }

    static bool B(SerializedObject so, string name, bool fallback)
    {
        var pr = so.FindProperty(name);
        return pr != null && pr.propertyType == SerializedPropertyType.Boolean ? pr.boolValue : fallback;
    }

    // EvolutionConfig — объект сцены ForestPavilion, ассета у него нет. Читаем ВСЕГДА из этой сцены (а не из той, что открыта у
    // человека): выгрузка не должна зависеть от того, что у него на экране. Сцена открывается ДОБАВЛЕНИЕМ и закрывается тут же;
    // уже открытую `ForestPavilion` не трогаем.
    static SimEvolution Evolution(List<SpeciesSO> all)
    {
        var ev = new SimEvolution();
        var names = new List<string>();
        foreach (var s in all) names.Add(s.speciesName);
        ev.allSpecies = names.ToArray();

        var open = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(ScenePath);
        bool wasOpen = open.IsValid() && open.isLoaded;
        var scene = wasOpen ? open : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            EvolutionConfig cfg = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                cfg = root.GetComponentInChildren<EvolutionConfig>(true);
                if (cfg != null) break;
            }
            if (cfg == null) throw new InvalidOperationException("EvolutionConfig не найден в сцене ForestPavilion");
            ev.evolveNpc = cfg.EvolveNpc; ev.startAffinity = cfg.StartAffinity; ev.chimerizeMultiplier = cfg.ChimerizeMultiplier;
            if (cfg.AllSpecies != null && cfg.AllSpecies.Length > 0)
            {
                var list = new List<string>();
                foreach (var s in cfg.AllSpecies) if (s != null) list.Add(s.speciesName);
                ev.allSpecies = list.ToArray();
            }
        }
        finally { if (!wasOpen) EditorSceneManager.CloseScene(scene, true); }
        return ev;
    }

    /// <summary>Логова: открыть `ForestPavilion`, собрать мир, снять `LairSite`, вернуть прежнюю сцену. Темп — через `LairLaw`
    /// (`RespawnGameHours`). Правка сцены не сохраняется: после съёма открытая сцена перезагружается с диска.</summary>
    public static SimLair[] BuildLairs(out string sceneName, out float mapDiameter)
    {
        string prev = EditorSceneManager.GetActiveScene().path;
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        sceneName = scene.name;
        mapDiameter = 0f;
        var result = new List<SimLair>();
        ForestBuilder builder = null;
        try
        {
            builder = UnityEngine.Object.FindAnyObjectByType<ForestBuilder>();
            if (builder == null) throw new InvalidOperationException("в ForestPavilion нет ForestBuilder");
            mapDiameter = builder.mapDiameter;
            builder.Build();
            foreach (var l in builder.Lairs)
            {
                if (l == null) continue;
                var p = l.transform.position;
                result.Add(new SimLair
                {
                    name = l.name, species = l.speciesName, areaType = l.areaType.ToString(),
                    capacity = l.capacity, tier = l.tier,
                    homeRadius = l.homeRadius, spawnRadius = l.spawnRadius, fearRadius = l.fearRadius,
                    respawnSeconds = l.respawnSeconds, respawnGameHours = l.RespawnGameHours,
                    fearDecayPerSecond = l.fearDecayPerSecond, fearDecayPerGameHour = l.FearDecayPerGameHour,
                    fearPerKill = l.fearPerKill, exhaustFear = l.exhaustFear, spawnEnabled = l.spawnEnabled,
                    x = Mathf.Round(p.x * 100f) / 100f, z = Mathf.Round(p.z * 100f) / 100f,
                });
            }
        }
        finally
        {
            if (builder != null) builder.Clear();
            // вернуть прежнюю сцену; несохранённая правка ForestPavilion (мир, собранный командой) не нужна — перезагрузка с диска её отбросит
            if (!string.IsNullOrEmpty(prev) && prev != ScenePath) EditorSceneManager.OpenScene(prev, OpenSceneMode.Single);
            else if (string.IsNullOrEmpty(prev)) EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            else EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
        result.Sort((a, b) => { int c = string.CompareOrdinal(a.name, b.name); return c != 0 ? c : a.x.CompareTo(b.x); });
        return result.ToArray();
    }
}
