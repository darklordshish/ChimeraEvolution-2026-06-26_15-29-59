using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Разметка пространства: поляны-арены под бой и выходы зверей.
/// Деревья в этих кругах не ставятся; это не второй пол и не NavMesh.
/// </summary>
public static class ForestSpace
{
    public struct Clearing
    {
        public Vector2 center;
        public float radius;
    }

    public struct Corridor
    {
        public Vector2 a;
        public Vector2 b;
        public float width;
    }
    public static List<Clearing> Build(DomeGenConfigSO cfg, long seed,
        Func<float, float, float> ground, Vector3 entry, Vector2 labCenter,
        DomeHydro.State hydro, int want = 5, float radius = 30f)
    {
        var list = new List<Clearing>();
        if (cfg == null || ground == null) return list;
        float rin = Mathf.Max(1f, DomeRockRing.InnerRadius(cfg));
        for (int i = 0; i < want * 60 && list.Count < want; i++)
        {
            float a = SeededHash.ToFloat01(SeededHash.Hash(seed, 9501, i)) * Mathf.PI * 2f;
            float r = Mathf.Lerp(rin * 0.25f, rin * 0.78f,
                SeededHash.ToFloat01(SeededHash.Hash(seed, 9502, i)));
            Vector2 p = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            if (Vector2.Distance(p, new Vector2(entry.x, entry.z)) < 45f) continue;
            if (Vector2.Distance(p, labCenter) < 55f) continue;
            if (hydro != null)
            {
                if (Vector2.Distance(p, hydro.lakeCenter) < hydro.lakeRadius + radius + 8f)
                    continue;
                if (ground(p.x, p.y) < hydro.lakeLevel + 0.5f) continue;
            }
            if (DomeSlope.SlopeAtS(cfg, p.x, p.y, ground) > 22f) continue;

            bool overlap = false;
            foreach (var c in list)
                if (Vector2.Distance(p, c.center) < radius * 2f + 8f) { overlap = true; break; }
            if (overlap) continue;
            list.Add(new Clearing { center = p, radius = radius });
        }
        return list;
    }

    public static List<Corridor> BuildCorridors(IReadOnlyList<Clearing> clearings,
        float width = 16f)
    {
        var list = new List<Corridor>();
        if (clearings == null || clearings.Count < 2) return list;
        for (int i = 1; i < clearings.Count; i++)
        {
            int best = -1; float bestD = float.MaxValue;
            for (int j = 0; j < i; j++)
            {
                float d = Vector2.Distance(clearings[i].center, clearings[j].center);
                if (d < bestD) { bestD = d; best = j; }
            }
            if (best >= 0)
                list.Add(new Corridor { a = clearings[best].center, b = clearings[i].center, width = width });
        }
        return list;
    }

    public static bool ContainsCorridor(IReadOnlyList<Corridor> corridors,
        float x, float z, float margin = 1f)
    {
        if (corridors == null) return false;
        Vector2 p = new Vector2(x, z);
        for (int i = 0; i < corridors.Count; i++)
        {
            var c = corridors[i];
            Vector2 ab = c.b - c.a;
            float len2 = ab.sqrMagnitude;
            float t = len2 < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p - c.a, ab) / len2);
            Vector2 closest = c.a + ab * t;
            float r = c.width * 0.5f + margin;
            if ((p - closest).sqrMagnitude < r * r) return true;
        }
        return false;
    }

    public static bool Contains(IReadOnlyList<Clearing> clearings,
        float x, float z, float margin = 1f)
    {
        if (clearings == null) return false;
        for (int i = 0; i < clearings.Count; i++)
        {
            float dx = x - clearings[i].center.x;
            float dz = z - clearings[i].center.y;
            float r = clearings[i].radius + margin;
            if (dx * dx + dz * dz < r * r) return true;
        }
        return false;
    }
}
