using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Адреса логов волка, лося и ежа (спека `2026-11-13-forest-logovo-po-zakonam` §4.4). Чистая функция мира:
/// на входе — то, что генератор уже посчитал (земля, вода, деревья, прогалины, лаборатория и вход), на выходе —
/// точки в координатах поля. Детерминирована сидом: кандидаты — узлы сетки, порядок обхода — хэш сида, первый
/// годный с выдержанным разносом берётся. Из s11 «места и связи» взяты только адреса.
/// <para>
/// Где кто живёт: волк — в лесу, вдали от воды и людей; лось — у воды или на прогалине у воды; ёж — на кромке
/// леса, где деревья рядом с открытым местом. Числа — рабочая цель первой зоны, тюнинг по плейтесту.
/// </para>
/// </summary>
public static class DomeAnimalLairs
{
    public enum Kind { Wolf, Moose, Hedgehog }

    public struct Spot
    {
        public Kind kind;
        public Vector2 pos;
    }

    /// <summary>Мир для поиска. Всё — в координатах поля.</summary>
    public struct World
    {
        public float diameter;
        public long seed;
        public Func<float, float, float> ground;      // высота пола
        public DomeHydro.State hydro;                  // озеро и река; null — воды нет
        public IReadOnlyList<Vector3> trees;           // стволы с коллайдером
        public IReadOnlyList<ForestSpace.Zone> zones;  // прогалины
        public Vector2 lab;
        public Vector2 entry;
        public IReadOnlyList<Vector2> avoid;           // уже занятые адреса (змеиные логова, гнездо совы)
    }

    // ---- правила видов (рабочая цель первой зоны) ----
    public const int WolfCount = 3, MooseCount = 2, HedgehogCount = 3;
    public const float WolfSpacing = 400f, MooseSpacing = 300f, HedgehogSpacing = 200f;
    public const float OtherSpacing = 30f;          // логова разных видов и чужие адреса — не ближе
    public const float WolfTreeRadius = 25f;
    public const int WolfMinTrees = 6;
    public const float WolfMaxSlope = 20f, WolfMinFromPeople = 150f, WolfMinFromWater = 60f;
    public const float MooseMaxSlope = 10f, MooseShoreMin = 15f, MooseShoreMax = 60f, MooseZoneToWater = 100f;
    public const float HedgehogMaxSlope = 20f, HedgehogOpenRadius = 8f, HedgehogEdgeRadius = 20f;
    public const int HedgehogEdgeTrees = 3;
    public const float HedgehogMinFromWater = 20f;
    /// <summary>Логова держатся внутри чаши: до скального кольца не ближе этого (кольцо начинается на R−80).</summary>
    public const float RingMargin = 110f;
    public const float LabClear = 60f;               // двор лаборатории и проходы к ней — не логово

    const int GridRes = 120;
    const float CellBucket = 25f;

    public static List<Spot> Find(World w)
    {
        if (w.ground == null) throw new ArgumentNullException(nameof(w.ground));
        var trees = new TreeIndex(w.trees, CellBucket);
        var taken = new List<Spot>();
        var avoid = w.avoid ?? Array.Empty<Vector2>();

        Pick(w, Kind.Wolf, WolfCount, WolfSpacing, taken, avoid, 1100,
            p => Vector2.Distance(p, w.lab) >= WolfMinFromPeople
                 && Vector2.Distance(p, w.entry) >= WolfMinFromPeople
                 && trees.Count(p, WolfTreeRadius) >= WolfMinTrees
                 && WaterDistance(w.hydro, p) >= WolfMinFromWater
                 && Slope(w, p) < WolfMaxSlope);

        Pick(w, Kind.Moose, MooseCount, MooseSpacing, taken, avoid, 1200,
            p =>
            {
                float d = WaterDistance(w.hydro, p);
                bool shore = d >= MooseShoreMin && (d <= MooseShoreMax || InZoneNearWater(w, p));
                return shore && Slope(w, p) < MooseMaxSlope;
            });

        Pick(w, Kind.Hedgehog, HedgehogCount, HedgehogSpacing, taken, avoid, 1300,
            p => trees.Count(p, HedgehogOpenRadius) == 0
                 && trees.Count(p, HedgehogEdgeRadius) >= HedgehogEdgeTrees
                 && WaterDistance(w.hydro, p) >= HedgehogMinFromWater
                 && Slope(w, p) < HedgehogMaxSlope);

        return taken;
    }

    static void Pick(World w, Kind kind, int want, float spacing, List<Spot> taken,
        IReadOnlyList<Vector2> avoid, int salt, Func<Vector2, bool> fits)
    {
        float r = w.diameter * 0.5f;
        float cell = w.diameter / GridRes;
        int n = GridRes * GridRes;
        // Порядок обхода — перестановка узлов по хэшу сида: детерминированно и без перекоса к углу сетки.
        var order = new List<(ulong key, int idx)>(n);
        for (int i = 0; i < n; i++) order.Add((SeededHash.Hash(w.seed, salt, i), i));
        order.Sort((a, b) => a.key.CompareTo(b.key));

        int placed = 0;
        foreach (var (_, idx) in order)
        {
            if (placed >= want) break;
            int ix = idx % GridRes, iz = idx / GridRes;
            var p = new Vector2(-r + (ix + 0.5f) * cell, -r + (iz + 0.5f) * cell);
            if (p.magnitude > r - RingMargin) continue;
            if (Vector2.Distance(p, w.lab) < LabClear) continue;
            if (!FarFrom(p, taken, kind, spacing) || !FarFrom(p, avoid)) continue;
            if (!fits(p)) continue;
            taken.Add(new Spot { kind = kind, pos = p });
            placed++;
        }
    }

    static bool FarFrom(Vector2 p, List<Spot> taken, Kind kind, float spacing)
    {
        foreach (var s in taken)
        {
            float need = s.kind == kind ? spacing : OtherSpacing;
            if (Vector2.Distance(p, s.pos) < need) return false;
        }
        return true;
    }

    static bool FarFrom(Vector2 p, IReadOnlyList<Vector2> avoid)
    {
        foreach (var a in avoid) if (Vector2.Distance(p, a) < OtherSpacing) return false;
        return true;
    }

    /// <summary>Уклон в градусах центральными разностями, шаг 1 м — как у склона змей (`DomeSlope`).</summary>
    public static float Slope(World w, Vector2 p)
    {
        const float e = 1f;
        float gx = (w.ground(p.x + e, p.y) - w.ground(p.x - e, p.y)) / (2f * e);
        float gz = (w.ground(p.x, p.y + e) - w.ground(p.x, p.y - e)) / (2f * e);
        return Mathf.Atan(Mathf.Sqrt(gx * gx + gz * gz)) * Mathf.Rad2Deg;
    }

    /// <summary>Расстояние до кромки воды: озеро — кругом, река — лентой по точкам. Воды нет — бесконечность.</summary>
    public static float WaterDistance(DomeHydro.State h, Vector2 p)
    {
        if (h == null) return float.PositiveInfinity;
        float best = float.PositiveInfinity;
        if (h.lakeRadius > 0f) best = Vector2.Distance(p, h.lakeCenter) - h.lakeRadius;
        if (h.riverPts != null)
            foreach (var rp in h.riverPts)
            {
                float d = Vector2.Distance(p, rp) - h.riverHalfWidth;
                if (d < best) best = d;
            }
        return best;
    }

    static bool InZoneNearWater(World w, Vector2 p)
    {
        if (w.zones == null) return false;
        foreach (var z in w.zones)
            if (Vector2.Distance(p, z.center) <= z.radius && WaterDistance(w.hydro, z.center) <= MooseZoneToWater)
                return true;
        return false;
    }

    /// <summary>Стволы в корзинах сетки: подсчёт в радиусе без перебора всего леса.</summary>
    sealed class TreeIndex
    {
        readonly Dictionary<(int, int), List<Vector2>> buckets = new Dictionary<(int, int), List<Vector2>>();
        readonly float size;

        public TreeIndex(IReadOnlyList<Vector3> trees, float size)
        {
            this.size = size;
            if (trees == null) return;
            foreach (var t in trees)
            {
                var key = ((int)Mathf.Floor(t.x / size), (int)Mathf.Floor(t.z / size));
                if (!buckets.TryGetValue(key, out var list)) buckets[key] = list = new List<Vector2>();
                list.Add(new Vector2(t.x, t.z));
            }
        }

        public int Count(Vector2 p, float radius)
        {
            int n = 0, span = Mathf.CeilToInt(radius / size);
            int cx = (int)Mathf.Floor(p.x / size), cz = (int)Mathf.Floor(p.y / size);
            float r2 = radius * radius;
            for (int x = cx - span; x <= cx + span; x++)
                for (int z = cz - span; z <= cz + span; z++)
                    if (buckets.TryGetValue((x, z), out var list))
                        foreach (var t in list)
                            if ((t - p).sqrMagnitude <= r2) n++;
            return n;
        }
    }
}
