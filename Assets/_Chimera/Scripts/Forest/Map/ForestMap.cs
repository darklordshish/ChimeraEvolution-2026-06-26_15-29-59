using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// КАРТА ЛЕСА — факты мира в метрах на плоскости купола (спека `2026-11-18-forest-karta-kak-istochnik` §4, §9).
/// Три уровня: решения (`MapPlan`) → КАРТА → 3D. Карта — не намерения, а то, что вышло: где вода, где дверь
/// лаборатории, сколько суши. Плоскость: центр (0, 0), x — восток, z — север, метры; это локальные координаты корня мира.
///
/// Шаг 1 (18.11): карта — снимок собранного мира v2 (`FromWorld`), слои: рамка, вода, кольцо, вход и появление,
/// лаборатория, площади. Слои рельефа, леса, логов, путей и корма — шаги 2–7; поток «3D читает карту» — шаги 2–5.
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
    public static ForestMap FromWorld(DomeWorld world, long seed, int worldVersion, Vector3 spawn)
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
        // Рельеф: тот, по которому мир строил чанки (шаг 2б), иначе — снять с террейна.
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
