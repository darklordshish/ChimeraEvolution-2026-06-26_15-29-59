using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Физическая кромка арены B-lite: крупные камни по кругу с разрывами на выходах.
/// Нужна, когда редколесье не даёт читаемой границы: 1500 деревьев на Ø2000 — это поле.
/// </summary>
public static class DomeArenaEdge
{
    static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();

    public static int Build(GameObject root, IReadOnlyList<ForestSpace.Zone> clearings,
        IReadOnlyList<ForestSpace.Link> corridors, System.Func<float, float, float> ground,
        Material rock, Material bark, long seed)
    {
        if (root == null || clearings == null || ground == null) return 0;
        int count = 0;
        for (int ci = 0; ci < clearings.Count; ci++)
        {
            var c = clearings[ci];
            var local = new List<ForestSpace.Link>();
            if (corridors != null)
                foreach (var cor in corridors)
                    if ((cor.a - c.center).sqrMagnitude < 1f || (cor.b - c.center).sqrMagnitude < 1f)
                        local.Add(cor);
            int stones = 24;
            for (int i = 0; i < stones; i++)
            {
                float a = (i / (float)stones) * Mathf.PI * 2f
                    + SeededHash.ToFloat01(SeededHash.Hash(seed, 9901, ci * 20 + i)) * 0.10f;
                Vector2 p = c.center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (c.radius + 2f);
                if (NearCorridor(local, p, 5f)) continue;
                float h = ground(p.x, p.y);
                var go = new GameObject("ArenaEdge");
                go.transform.SetParent(root.transform, false);
                go.transform.position = new Vector3(p.x, h + 1.1f, p.y);
                go.transform.rotation = Quaternion.Euler(0f,
                    SeededHash.ToFloat01(SeededHash.Hash(seed, 9902, ci * 20 + i)) * 360f, 0f);
                var mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = Stone();
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = rock;
                var box = go.AddComponent<BoxCollider>();
                box.size = new Vector3(3.6f, 2.2f, 3.0f);
                count++;
            }
        }
        return count;
    }

    static bool NearCorridor(IReadOnlyList<ForestSpace.Link> corridors, Vector2 p, float gap)
    {
        if (corridors == null) return false;
        foreach (var c in corridors)
        {
            Vector2 ab = c.b - c.a;
            float len2 = ab.sqrMagnitude;
            float t = len2 < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p - c.a, ab) / len2);
            Vector2 q = c.a + ab * t;
            if ((p - q).sqrMagnitude < gap * gap) return true;
        }
        return false;
    }

    static Mesh Stone()
    {
        if (!meshes.TryGetValue("arena-stone", out var mesh))
            meshes["arena-stone"] = mesh = FloraMeshKit.Slab(3.6f, 2.2f, 3.0f);
        return mesh;
    }
}