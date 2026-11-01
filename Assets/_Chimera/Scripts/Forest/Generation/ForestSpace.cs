using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Разметка пространства: поляны-арены под бой и выходы зверей.
/// Деревья в этих кругах не ставятся; это не второй пол и не NavMesh.
/// </summary>
public static class ForestSpace
{
    /// <summary>
    /// Версия разметки B-lite (поляны, коридоры, поды, дорожки). Входит в отпечаток мира:
    /// правила разметки меняются — меняется и версия, и «тот же сид» не выдаёт другой B-lite.
    /// </summary>
    public const int LayoutVersion = 1;

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

    public struct CoverPod
    {
        public Vector2 center;
        public float radius;
        /// <summary>Высота земли под подом: нужна детектору видимости, чтобы не сравнивать с нулём.</summary>
        public float groundY;
    }

    /// <summary>
    /// Поды-укрытия: по одному от каждой группы (арена / коридор) за проход, пока не
    /// кончится бюджет. Раньше арены выбирали все 24 пода (6 на каждую), и коридоры
    /// оставались пустыми, а пятая арена могла не получить ни одного. Кандидаты
    /// проверяются на воду, пятно станции/вход (exclude) и минимальный зазор между подами.
    /// </summary>
    public static List<CoverPod> BuildCoverPods(IReadOnlyList<Clearing> clearings,
        IReadOnlyList<Corridor> corridors, long seed,
        Func<float, float, float> ground = null, float waterLevel = float.NegativeInfinity,
        int want = 24, Func<float, float, bool> exclude = null)
    {
        var list = new List<CoverPod>();
        int nc = clearings != null ? clearings.Count : 0;
        int nr = corridors != null ? corridors.Count : 0;
        if (nc + nr == 0 || want <= 0) return list;

        // Кандидаты по группам: арена и коридор — равноправные группы раздачи.
        var groups = new List<List<Vector2>>(nc + nr);
        for (int i = 0; i < nc; i++)
        {
            var g = new List<Vector2>(8);
            for (int k = 0; k < 8; k++)
            {
                float a = (k / 8f + SeededHash.ToFloat01(
                    SeededHash.Hash(seed, 9601, i * 8 + k)) * 0.08f) * Mathf.PI * 2f;
                float r = clearings[i].radius + 6f;
                Vector2 p = clearings[i].center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                if (!PodSpotOk(p, ground, waterLevel, exclude, clearings)) continue;
                g.Add(p);
            }
            groups.Add(g);
        }
        for (int i = 0; i < nr; i++)
        {
            var g = new List<Vector2>(8);
            var c = corridors[i];
            Vector2 dir = (c.b - c.a).normalized;
            Vector2 side = new Vector2(-dir.y, dir.x);
            for (int k = 0; k < 4; k++)
            {
                float t = 0.2f + k * 0.2f;
                Vector2 mid = Vector2.Lerp(c.a, c.b, t);
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector2 p = mid + side * (c.width * 0.5f + 5f) * s;
                    if (!PodSpotOk(p, ground, waterLevel, exclude, clearings)) continue;
                    g.Add(p);
                }
            }
            groups.Add(g);
        }

        // Круговая раздача: арена без пода и коридор без пода — не «порядок обхода», а ошибка.
        var cursor = new int[groups.Count];
        bool progress = true;
        while (list.Count < want && progress)
        {
            progress = false;
            for (int g = 0; g < groups.Count && list.Count < want; g++)
            {
                var cand = groups[g];
                while (cursor[g] < cand.Count)
                {
                    Vector2 p = cand[cursor[g]++];
                    if (TooClose(list, p, 8f)) continue;
                    list.Add(new CoverPod { center = p, radius = 2.5f, groundY = ground != null ? ground(p.x, p.y) : 0f });
                    progress = true;
                    break;
                }
            }
        }
        // Хвост: круговая раздача могла упереться в минимальный зазор — добираем
        // из тех же кандидатов с меньшим зазором, чтобы бюджет не просел.
        if (list.Count < want)
        {
            for (int g = 0; g < groups.Count && list.Count < want; g++)
                foreach (var p in groups[g])
                {
                    if (list.Count >= want) break;
                    if (TooClose(list, p, 4f)) continue;
                    list.Add(new CoverPod { center = p, radius = 2.5f, groundY = ground != null ? ground(p.x, p.y) : 0f });
                }
        }
        return list;
    }

    static bool PodSpotOk(Vector2 p, Func<float, float, float> ground, float waterLevel,
        Func<float, float, bool> exclude, IReadOnlyList<Clearing> clearings)
    {
        if (exclude != null && exclude(p.x, p.y)) return false;
        // Поды не ставятся внутрь арены: там должно быть пусто под бой.
        if (Contains(clearings, p.x, p.y, 0f)) return false;
        if (ground != null && ground(p.x, p.y) < waterLevel + 0.5f) return false;
        return true;
    }

    static bool TooClose(IReadOnlyList<CoverPod> pods, Vector2 p, float minDist)
    {
        float r2 = minDist * minDist;
        for (int i = 0; i < pods.Count; i++)
        {
            float dx = pods[i].center.x - p.x;
            float dz = pods[i].center.y - p.y;
            if (dx * dx + dz * dz < r2) return true;
        }
        return false;
    }

    /// <summary>Точка рядом с подом-укрытием: туда не ставится дерево (иначе ствол в укрытии).</summary>
    public static bool NearPods(IReadOnlyList<CoverPod> pods, float x, float z, float margin = 2f)
    {
        if (pods == null) return false;
        for (int i = 0; i < pods.Count; i++)
        {
            float dx = pods[i].center.x - x;
            float dz = pods[i].center.y - z;
            float r = pods[i].radius + margin;
            if (dx * dx + dz * dz < r * r) return true;
        }
        return false;
    }
    public static List<Corridor> BuildCorridors(IReadOnlyList<Clearing> clearings,
        float width = 16f)
    {
        var list = new List<Corridor>();
        if (clearings == null || clearings.Count < 2) return list;

        // Сначала цепь по ближайшему соседу — связность.
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

        // Каждой арене — минимум два выхода: листья цепи замыкаются на ближайшую арену.
        for (int i = 0; i < clearings.Count; i++)
        {
            while (Exits(clearings, list, i) < 2)
            {
                int best = -1; float bestD = float.MaxValue;
                for (int j = 0; j < clearings.Count; j++)
                {
                    if (i == j || Connected(list, clearings[i].center, clearings[j].center)) continue;
                    float d = Vector2.Distance(clearings[i].center, clearings[j].center);
                    if (d < bestD) { bestD = d; best = j; }
                }
                if (best < 0) break;
                list.Add(new Corridor { a = clearings[i].center, b = clearings[best].center, width = width });
            }
        }
        return list;
    }

    public static int Exits(IReadOnlyList<Clearing> clearings,
        IReadOnlyList<Corridor> corridors, int index)
    {
        if (clearings == null || corridors == null || index < 0 || index >= clearings.Count)
            return 0;
        Vector2 c = clearings[index].center;
        int n = 0;
        foreach (var cor in corridors)
            if ((cor.a - c).sqrMagnitude < 0.25f || (cor.b - c).sqrMagnitude < 0.25f) n++;
        return n;
    }

    static bool Connected(IReadOnlyList<Corridor> corridors, Vector2 a, Vector2 b)
    {
        foreach (var c in corridors)
        {
            bool ab = (c.a - a).sqrMagnitude < 0.25f && (c.b - b).sqrMagnitude < 0.25f;
            bool ba = (c.a - b).sqrMagnitude < 0.25f && (c.b - a).sqrMagnitude < 0.25f;
            if (ab || ba) return true;
        }
        return false;
    }
    public static bool Near(IReadOnlyList<Clearing> clearings,
        IReadOnlyList<Corridor> corridors, float x, float z, float margin = 12f)
    {
        return Contains(clearings, x, z, margin)
            || ContainsCorridor(corridors, x, z, margin);
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
