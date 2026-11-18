using System;
using UnityEngine;

/// <summary>
/// РАСТР КАРТЫ (спека `2026-11-18-forest-karta-kak-istochnik` §9, шаг 6): производный слой, не источник. Строится ТОЛЬКО
/// из слоёв карты — рельефа (`ForestMapRelief.Height`), воды, кольца, дорог, связей, деревьев (ГеймБосс 18.11: тогда при 5б
/// он не меняется). Ключ — «хеш карты + шаг». Читают его граф путей и картинка; 3D — функцию высоты.
///
/// Клетка — квадрат `step` м с центром в (origin + (i + ½)·step). Классы: ходьба (уклон ≤ walkMax), лазание (≤ wallMin),
/// стена, вода, вне кольца. Поверхность — для фактов рёбер графа: брод, дорога, тропа, склон, лес, поляна.
/// </summary>
public sealed class ForestMapRaster
{
    public enum Cls : byte { Walk, Climb, Wall, Water, Outside }
    public enum Surface : byte { Clearing, Forest, Trail, Road, Climb, Ford }

    public const float DefaultStep = 4f;
    /// <summary>Радиус «под деревом» для поверхности «лес», м (крона дерева павильона — 2–4 м).</summary>
    public const float ForestRadius = 6f;

    public float step;
    public float origin;
    public int n;
    public float[] height;
    public float[] slopeDeg;
    public Cls[] cls;
    public Surface[] surface;
    public string key;
    public float walkMaxDegrees;
    public float wallMinDegrees;

    public int Index(int ix, int iz) => iz * n + ix;
    public Vector2 Center(int ix, int iz) => new Vector2(origin + (ix + 0.5f) * step, origin + (iz + 0.5f) * step);

    public bool Cell(Vector2 p, out int ix, out int iz)
    {
        ix = Mathf.FloorToInt((p.x - origin) / step);
        iz = Mathf.FloorToInt((p.y - origin) / step);
        return ix >= 0 && iz >= 0 && ix < n && iz < n;
    }

    public bool Passable(int i) => cls[i] != Cls.Wall && cls[i] != Cls.Outside;

    public static ForestMapRaster Build(ForestMap map, float step = DefaultStep)
    {
        if (map == null) throw new ArgumentNullException(nameof(map));
        if (map.relief == null) throw new ArgumentException("у карты нет рельефа — растр строить не из чего");
        var r = new ForestMapRaster { step = step };
        float half = map.diameter * 0.5f;
        r.n = Mathf.CeilToInt(map.diameter / step);
        r.origin = -half;
        r.key = map.hash + "_" + step.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        var cfg = map.relief.Config;
        r.walkMaxDegrees = cfg.WalkMaxDegrees;
        r.wallMinDegrees = cfg.WallMinDegrees;
        int n = r.n, count = n * n;
        r.height = new float[count];
        r.slopeDeg = new float[count];
        r.cls = new Cls[count];
        r.surface = new Surface[count];

        for (int iz = 0; iz < n; iz++)
            for (int ix = 0; ix < n; ix++)
            {
                var c = r.Center(ix, iz);
                r.height[r.Index(ix, iz)] = map.relief.Height(c.x, c.y);
            }

        var road = new bool[count];
        var trail = new bool[count];
        var forest = new bool[count];
        if (map.roads != null)
            foreach (var rd in map.roads)
                if (rd.points != null)
                    for (int k = 0; k + 1 < rd.points.Count; k++) Stamp(r, road, rd.points[k], rd.points[k + 1], rd.width * 0.5f);
        if (map.links != null)
            foreach (var l in map.links) Stamp(r, trail, l.a, l.b, l.width * 0.5f);
        if (map.trees != null)
            foreach (var t in map.trees)
            {
                var p = new Vector2(t.pos.x, t.pos.z);
                Stamp(r, forest, p, p, ForestRadius);
            }

        float inner2 = map.ring.inner * map.ring.inner;
        for (int iz = 0; iz < n; iz++)
            for (int ix = 0; ix < n; ix++)
            {
                int i = r.Index(ix, iz);
                var c = r.Center(ix, iz);
                float s = Slope(r, ix, iz);
                r.slopeDeg[i] = s;
                if (c.sqrMagnitude > inner2) r.cls[i] = Cls.Outside;
                else if (WaterDistance(map.water, c) < 0f) r.cls[i] = Cls.Water;
                else if (s > r.wallMinDegrees) r.cls[i] = Cls.Wall;
                else if (s > r.walkMaxDegrees) r.cls[i] = Cls.Climb;
                else r.cls[i] = Cls.Walk;

                r.surface[i] = r.cls[i] == Cls.Water ? Surface.Ford
                    : road[i] ? Surface.Road
                    : trail[i] ? Surface.Trail
                    : r.cls[i] == Cls.Climb ? Surface.Climb
                    : forest[i] ? Surface.Forest
                    : Surface.Clearing;
            }
        return r;
    }

    /// <summary>Уклон клетки, градусы: центральная разность по соседям (на краю — односторонняя).</summary>
    static float Slope(ForestMapRaster r, int ix, int iz)
    {
        int n = r.n;
        int x0 = Mathf.Max(0, ix - 1), x1 = Mathf.Min(n - 1, ix + 1);
        int z0 = Mathf.Max(0, iz - 1), z1 = Mathf.Min(n - 1, iz + 1);
        float dx = (r.height[r.Index(x1, iz)] - r.height[r.Index(x0, iz)]) / ((x1 - x0) * r.step);
        float dz = (r.height[r.Index(ix, z1)] - r.height[r.Index(ix, z0)]) / ((z1 - z0) * r.step);
        return Mathf.Atan(Mathf.Sqrt(dx * dx + dz * dz)) * Mathf.Rad2Deg;
    }

    /// <summary>Отметить клетки ближе <paramref name="radius"/> к отрезку a–b.</summary>
    static void Stamp(ForestMapRaster r, bool[] mask, Vector2 a, Vector2 b, float radius)
    {
        float rr = Mathf.Max(radius, r.step * 0.5f);
        Vector2 lo = Vector2.Min(a, b) - Vector2.one * rr, hi = Vector2.Max(a, b) + Vector2.one * rr;
        int ix0 = Mathf.Max(0, Mathf.FloorToInt((lo.x - r.origin) / r.step)), ix1 = Mathf.Min(r.n - 1, Mathf.FloorToInt((hi.x - r.origin) / r.step));
        int iz0 = Mathf.Max(0, Mathf.FloorToInt((lo.y - r.origin) / r.step)), iz1 = Mathf.Min(r.n - 1, Mathf.FloorToInt((hi.y - r.origin) / r.step));
        for (int iz = iz0; iz <= iz1; iz++)
            for (int ix = ix0; ix <= ix1; ix++)
                if (DomeFootprint.DistanceToSegment(r.Center(ix, iz), a, b) <= rr) mask[r.Index(ix, iz)] = true;
    }

    /// <summary>Расстояние до воды по слою воды карты (как `DomeAnimalLairs.WaterDistance` и `ForestBuilder.IsWater`).</summary>
    public static float WaterDistance(ForestMap.Water w, Vector2 p)
    {
        if (w == null) return float.PositiveInfinity;
        float best = float.PositiveInfinity;
        if (w.hasLake && w.lakeRadius > 0f) best = Vector2.Distance(p, w.lakeCenter) - w.lakeRadius;
        if (w.river != null)
            foreach (var rp in w.river)
            {
                float d = Vector2.Distance(p, rp) - w.riverHalfWidth;
                if (d < best) best = d;
            }
        return best;
    }
}
