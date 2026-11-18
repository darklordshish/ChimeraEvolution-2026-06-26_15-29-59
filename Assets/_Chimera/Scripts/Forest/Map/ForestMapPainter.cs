using UnityEngine;

/// <summary>
/// КАРТИНКА КАРТЫ (спека `2026-11-18-forest-karta-kak-istochnik` §7, шаг 6): из растра и слоёв карты, без единого объекта
/// сцены — клетка растра = пиксель. Отмывка рельефа по уклону, классы (вода, стена, лазание), поверхность (лес, поляна,
/// дорога, тропа), рёбра графа, логова значками по видам, вход, дверь лаборатории с маяком, ясень.
/// </summary>
public static class ForestMapPainter
{
    static readonly Color Clearing = new Color(0.62f, 0.72f, 0.42f);
    static readonly Color Forest = new Color(0.26f, 0.42f, 0.24f);
    static readonly Color Climb = new Color(0.66f, 0.56f, 0.40f);
    static readonly Color Wall = new Color(0.36f, 0.34f, 0.32f);
    static readonly Color Water = new Color(0.30f, 0.50f, 0.78f);
    static readonly Color Outside = new Color(0.16f, 0.16f, 0.18f);
    static readonly Color Road = new Color(0.80f, 0.66f, 0.46f);
    static readonly Color Trail = new Color(0.74f, 0.70f, 0.52f);
    static readonly Color Edge = new Color(1f, 1f, 1f, 0.55f);

    public static Color SpeciesColor(string species) => species switch
    {
        "Волк" => new Color(0.55f, 0.55f, 0.60f),
        "Змея" => new Color(0.85f, 0.75f, 0.20f),
        "Лось" => new Color(0.55f, 0.32f, 0.16f),
        "Ёж" => new Color(0.85f, 0.45f, 0.55f),
        _ => new Color(0.95f, 0.95f, 0.95f),
    };

    public static Texture2D Paint(ForestMap map, ForestMapRaster r, ForestMapGraph graph = null)
    {
        int n = r.n;
        var px = new Color[n * n];
        for (int iz = 0; iz < n; iz++)
            for (int ix = 0; ix < n; ix++)
            {
                int i = r.Index(ix, iz);
                Color c = r.cls[i] switch
                {
                    ForestMapRaster.Cls.Outside => Outside,
                    ForestMapRaster.Cls.Water => Water,
                    ForestMapRaster.Cls.Wall => Wall,
                    _ => r.surface[i] switch
                    {
                        ForestMapRaster.Surface.Road => Road,
                        ForestMapRaster.Surface.Trail => Trail,
                        ForestMapRaster.Surface.Climb => Climb,
                        ForestMapRaster.Surface.Forest => Forest,
                        _ => Clearing,
                    },
                };
                if (r.cls[i] != ForestMapRaster.Cls.Outside && r.cls[i] != ForestMapRaster.Cls.Water)
                    c *= Shade(r, ix, iz);
                c.a = 1f;
                px[i] = c;
            }

        if (graph != null)
        {
            var at = new System.Collections.Generic.Dictionary<string, Vector2>();
            foreach (var v in graph.vertices) at[v.id] = new Vector2(v.x, v.z);
            for (int k = 0; k < graph.edges.Count; k++)
            {
                var e = graph.edges[k];
                // Ломаной по фактическому ходу ребра; прямой — только если хода нет (граф из JSON).
                var path = graph.paths != null && k < graph.paths.Count ? graph.paths[k] : null;
                if (path != null && path.Count >= 2)
                    for (int i = 0; i + 1 < path.Count; i++) Line(px, r, path[i], path[i + 1], Edge);
                else if (at.TryGetValue(e.a, out var a) && at.TryGetValue(e.b, out var b)) Line(px, r, a, b, Edge);
            }
        }

        if (map.forage != null) foreach (var f in map.forage) Dot(px, r, f.pos, 1, new Color(0.55f, 0.95f, 0.35f));
        foreach (var l in map.lairs) Dot(px, r, new Vector2(l.pos.x, l.pos.z), 3, SpeciesColor(l.species));
        if (map.ash != null && map.ash.has) Dot(px, r, map.ash.pos, 5, new Color(0.12f, 0.30f, 0.10f));
        Dot(px, r, new Vector2(map.points.entry.x, map.points.entry.z), 4, Color.white);
        if (map.lab.has) Dot(px, r, new Vector2(map.lab.door.x, map.lab.door.z), 5, new Color(0.85f, 0.15f, 0.12f));
        if (map.beacon != null && map.beacon.has)
            Dot(px, r, new Vector2(map.beacon.basePos.x, map.beacon.basePos.z), 2, new Color(1f, 0.55f, 0.15f));

        var tex = new Texture2D(n, n, TextureFormat.RGB24, false);
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    /// <summary>Отмывка: свет с северо-запада, 0.7…1.15.</summary>
    static float Shade(ForestMapRaster r, int ix, int iz)
    {
        int n = r.n;
        int x0 = Mathf.Max(0, ix - 1), x1 = Mathf.Min(n - 1, ix + 1);
        int z0 = Mathf.Max(0, iz - 1), z1 = Mathf.Min(n - 1, iz + 1);
        float dx = r.height[r.Index(x1, iz)] - r.height[r.Index(x0, iz)];
        float dz = r.height[r.Index(ix, z1)] - r.height[r.Index(ix, z0)];
        var normal = new Vector3(-dx, 2f * r.step, -dz).normalized;
        float lit = Vector3.Dot(normal, new Vector3(-0.5f, 0.75f, 0.45f).normalized);
        return Mathf.Lerp(0.7f, 1.15f, Mathf.Clamp01(lit));
    }

    static void Dot(Color[] px, ForestMapRaster r, Vector2 p, int radius, Color c)
    {
        if (!r.Cell(p, out int cx, out int cz)) return;
        for (int dz = -radius; dz <= radius; dz++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (dx * dx + dz * dz > radius * radius) continue;
                int x = cx + dx, z = cz + dz;
                if (x < 0 || z < 0 || x >= r.n || z >= r.n) continue;
                px[r.Index(x, z)] = (dx * dx + dz * dz > (radius - 1) * (radius - 1)) ? Color.black : c;
            }
    }

    static void Line(Color[] px, ForestMapRaster r, Vector2 a, Vector2 b, Color c)
    {
        float len = Vector2.Distance(a, b);
        int steps = Mathf.Max(1, Mathf.CeilToInt(len / (r.step * 0.5f)));
        for (int k = 0; k <= steps; k++)
        {
            var p = Vector2.Lerp(a, b, k / (float)steps);
            if (!r.Cell(p, out int x, out int z)) continue;
            int i = r.Index(x, z);
            px[i] = Color.Lerp(px[i], c, c.a);
        }
    }
}
