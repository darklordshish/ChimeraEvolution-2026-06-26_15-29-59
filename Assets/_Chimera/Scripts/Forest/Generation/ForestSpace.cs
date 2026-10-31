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
