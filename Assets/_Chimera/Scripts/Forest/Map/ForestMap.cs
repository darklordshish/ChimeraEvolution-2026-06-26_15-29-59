using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// КАРТА ЛЕСА — факты мира в метрах на плоскости купола (спека `2026-11-18-forest-karta-kak-istochnik` §4, §9).
/// Три уровня: решения (`MapPlan`) → КАРТА → 3D. Карта — не намерения, а то, что вышло: где вода, где дверь
/// лаборатории, сколько суши. Плоскость: центр (0, 0), x — восток, z — север, метры; это локальные координаты корня мира.
///
/// ДО ШАГА 5Б КАРТА — СНИМОК СОБРАННОГО МИРА, ИСТОЧНИКОМ ОНА СТАНЕТ ПОСЛЕ 5Б (ГеймБосс 18.11). `FromWorld` снимает слои
/// с мира v2: рамка, вода, кольцо, вход и появление, лаборатория, площади (шаг 1), рельеф (2), деревья (3), логова (4),
/// разметка, пути, ясень, маяк (5а). По карте уже строятся чанки (рельеф) и деревья (план леса); остальное 3D читает поля
/// мира. Критерий закрытия 5б — мир, собранный из карты после JSON, даёт тот же отпечаток (спека §5 п. 2).
/// Сериализация — `JsonUtility`; `hash` — отпечаток содержания (`ForestMapHash`), без времени и без самого поля `hash`.
/// </summary>
[Serializable]
public sealed class ForestMap
{
    public const int Schema = 1;

    public int schema = Schema;
    public long seed;
    public int worldVersion;
    public float diameter;
    public string hash = "";

    public Water water = new Water();
    public Ring ring = new Ring();
    public Points points = new Points();
    public Lab lab = new Lab();
    public Areas areas = new Areas();
    /// <summary>Рельеф v2: рецепт поля и высота по нему (шаг 2). После `FromJson` — <see cref="Restore"/>.</summary>
    public ForestMapRelief relief;
    /// <summary>Деревья (шаг 3): точка, порода, масштаб, лежит/стоит, твёрдый ствол. Подлесок — вне карты.</summary>
    public List<DomeFlora.TreeRec> trees = new List<DomeFlora.TreeRec>();
    // --- шаг 5а: разметка пространства, пути, ориентиры ---
    public List<ForestSpace.Zone> zones = new List<ForestSpace.Zone>();
    public List<ForestSpace.Link> links = new List<ForestSpace.Link>();
    public List<ForestSpace.Zone> commons = new List<ForestSpace.Zone>();
    public List<ForestSpace.Zone> waterholes = new List<ForestSpace.Zone>();
    public List<ForestSpace.CoverPod> coverPods = new List<ForestSpace.CoverPod>();
    public List<ForestPoi> pois = new List<ForestPoi>();
    public List<ForestRoad> roads = new List<ForestRoad>();
    public Ash ash = new Ash();
    /// <summary>Корм (шаг 7): кусты по сиду, на проходимой суше; отрастание и съеденность — рантайм.</summary>
    public List<ForestForage.Item> forage = new List<ForestForage.Item>();
    public Beacon beacon = new Beacon();

    /// <summary>Ясень — главный ориентир: место и земля под ним.</summary>
    [Serializable]
    public sealed class Ash
    {
        public bool has;
        public Vector2 pos;
        public float groundY;
    }

    /// <summary>Маяк лаборатории (17.11): основание на плоскости, высота фонаря, галка.</summary>
    [Serializable]
    public sealed class Beacon
    {
        public bool has;
        public bool on;
        public Vector3 basePos;
        public float height;
    }

    /// <summary>Логова (шаг 4): адрес, вид, тип, ёмкость, радиусы; темп и спад страха — в игровых часах.</summary>
    public List<Lair> lairs = new List<Lair>();

    /// <summary>
    /// Логово карты. Темп и спад страха — в ИГРОВЫХ ЧАСАХ (так читает симулятор); перевод из секунд рантайма — закон
    /// `LairLaw`, темп часов — `LairSite.GameHoursPerSecond` (одно место того и другого).
    /// </summary>
    [Serializable]
    public sealed class Lair
    {
        public string name;
        public string species;
        public LairSite.LairAreaType type;
        public Vector3 pos;
        public List<Vector3> entrances = new List<Vector3>();
        public int capacity;
        public int tier;
        public bool spawnEnabled;
        public float respawnHours;
        public float homeRadius;
        public float spawnRadius;
        public float fearRadius;
        public float fearPerKill;
        public float fearDecayPerHour;
        public float exhaustFear;
    }

    /// <summary>Запись логова карты из чисел рантайма (секунды → игровые часы законом `LairLaw`).</summary>
    public static Lair LairOf(string name, string species, LairSite.LairAreaType type, Vector3 pos, List<Vector3> entrances,
        int capacity, int tier, bool spawnEnabled, float respawnSeconds, float homeRadius, float spawnRadius,
        float fearRadius, float fearPerKill, float fearDecayPerSecond, float exhaustFear)
    {
        float hps = LairSite.GameHoursPerSecond;
        return new Lair
        {
            name = name, species = species, type = type, pos = pos, entrances = entrances ?? new List<Vector3>(),
            capacity = capacity, tier = tier, spawnEnabled = spawnEnabled,
            respawnHours = Chimera.Laws.LairLaw.RespawnHours(respawnSeconds, hps),
            homeRadius = homeRadius, spawnRadius = spawnRadius, fearRadius = fearRadius, fearPerKill = fearPerKill,
            fearDecayPerHour = Chimera.Laws.LairLaw.FearDecayPerHour(fearDecayPerSecond, hps),
            exhaustFear = exhaustFear,
        };
    }

    [Serializable]
    public sealed class Water
    {
        public bool hasLake;
        public Vector2 lakeCenter;
        public float lakeRadius;
        public float lakeLevel;
        public float lakeDepth;
        public List<Vector2> river = new List<Vector2>();
        public float riverHalfWidth;
        public float riverDepth;
    }

    /// <summary>Кольцо скал: внутренний и внешний радиус полосы, м.</summary>
    [Serializable]
    public sealed class Ring
    {
        public float inner;
        public float outer;
    }

    /// <summary>Вход в павильон и точка появления игрока (она же старт игрока для симулятора).</summary>
    [Serializable]
    public sealed class Points
    {
        public Vector3 entry;
        public Vector3 spawn;
    }

    /// <summary>Лаборатория: центр, поворот, дверь (выход босса, основание маяка) и направление наружу.</summary>
    [Serializable]
    public sealed class Lab
    {
        public bool has;
        public Vector2 center;
        public float yawDeg;
        public Vector3 door;
        public Vector2 outward;
    }

    /// <summary>Площади, км²: всё внутри кольца и суша (без озера и реки).</summary>
    [Serializable]
    public sealed class Areas
    {
        public float totalKm2;
        public float landKm2;
    }

    /// <summary>Шаг сетки подсчёта площади суши, м (шаг растров карты, решение 18.11).</summary>
    public const float AreaStep = 4f;

    /// <summary>Снимок фактов собранного мира. Ни одного нового вычисления, кроме площади суши.</summary>
    /// <param name="spawn">точка появления игрока — её решает `ForestBuilder` (6 м от входа в чашу), а не `DomeWorld.SpawnPoint` (зал лаборатории)</param>
    /// <param name="lairs">слой логов — план логов `ForestBuilder` (шаг 4); null — слой пуст</param>
    public static ForestMap FromWorld(DomeWorld world, long seed, int worldVersion, Vector3 spawn,
        List<Lair> lairs = null, Beacon beacon = null)
    {
        if (world == null) throw new ArgumentNullException(nameof(world));
        var m = new ForestMap { seed = seed, worldVersion = worldVersion, diameter = world.MapDiameter };

        var h = world.Hydro;
        if (h != null)
        {
            m.water.hasLake = h.lakeRadius > 0f;
            m.water.lakeCenter = h.lakeCenter;
            m.water.lakeRadius = h.lakeRadius;
            m.water.lakeLevel = h.lakeLevel;
            m.water.lakeDepth = h.lakeDepth;
            if (h.riverPts != null) m.water.river.AddRange(h.riverPts);
            m.water.riverHalfWidth = h.riverHalfWidth;
            m.water.riverDepth = h.riverDepth;
        }

        // Кольцо скал — по его собственной формуле, а не по маске: маска кольца — полоса [rin−30, rout], запас для форм.
        if (world.Config != null)
        {
            m.ring.inner = DomeRockRing.InnerRadius(world.Config);
            m.ring.outer = DomeRockRing.OuterRadius(world.Config);
        }

        m.points.entry = world.Entry;
        m.points.spawn = spawn;

        if (world.LabComplex != null)
        {
            m.lab.has = true;
            m.lab.center = world.LabCenter;
            m.lab.yawDeg = world.LabComplex.yawDeg;
            m.lab.door = world.LabDoor;
            float yaw = m.lab.yawDeg * Mathf.Deg2Rad;
            m.lab.outward = new Vector2(-Mathf.Sin(yaw), -Mathf.Cos(yaw)); // фасад — к чаше, как ForestBuilder.TryGetExit
        }

        m.areas = AreasOf(m.ring.inner, h);
        if (world.MapTrees != null) m.trees = world.MapTrees; // план леса (шаг 3)
        if (lairs != null) m.lairs = lairs; // план логов (шаг 4)
        // Шаг 5а: разметка, пути, ориентиры — копиями списков мира.
        m.zones = new List<ForestSpace.Zone>(world.Zones);
        m.links = new List<ForestSpace.Link>(world.Links);
        m.commons = new List<ForestSpace.Zone>(world.Commons);
        m.waterholes = new List<ForestSpace.Zone>(world.Waterholes);
        m.coverPods = new List<ForestSpace.CoverPod>(world.CoverPods);
        m.pois = new List<ForestPoi>(world.Pois.All);
        if (world.Roads != null) m.roads = new List<ForestRoad>(world.Roads.roads);
        m.ash = new Ash { has = world.HasAsh, pos = world.Ash.pos, groundY = world.Ash.groundY };
        if (beacon != null) m.beacon = beacon;
        m.relief = world.MapRelief; // снят перед чанками; у мира с неподдержанным слоем карты нет вовсе (ForestBuilder)
        m.hash = ComputeHash(m);
        return m;
    }

    static Areas AreasOf(float inner, DomeHydro.State hydro)
    {
        var a = new Areas { totalKm2 = Mathf.PI * inner * inner / 1e6f };
        int n = Mathf.CeilToInt(inner * 2f / AreaStep);
        float start = -inner + AreaStep * 0.5f;
        long land = 0;
        for (int iz = 0; iz < n; iz++)
            for (int ix = 0; ix < n; ix++)
            {
                var p = new Vector2(start + ix * AreaStep, start + iz * AreaStep);
                if (p.sqrMagnitude > inner * inner) continue;
                if (DomeAnimalLairs.WaterDistance(hydro, p) >= 0f) land++;
            }
        a.landKm2 = land * AreaStep * AreaStep / 1e6f;
        return a;
    }

    public string ToJson() => JsonUtility.ToJson(this, true);

    /// <summary>Восстановить несериализуемое после `FromJson`: копию террейна рельефа.</summary>
    public void Restore() => relief?.Restore();

    public static ForestMap FromJson(string json) =>
        string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<ForestMap>(json);

    public static string ComputeHash(ForestMap m) => ForestMapHash.Of(m).ToString("x16");
}
