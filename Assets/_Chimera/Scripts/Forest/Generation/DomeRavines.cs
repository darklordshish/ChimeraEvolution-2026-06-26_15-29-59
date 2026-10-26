using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// B1.1: локальные овраги на стенде B0. Детерминированная выемка в поле высот,
/// упавшие стволы по бортам и визуальные гнёзда змей без LairSite/спавнера.
/// </summary>
public static class DomeRavines
{
    public struct Ravine
    {
        public Vector2 center;
        public float angleDeg;
        public float length;
        public float width;
        public float depth;
    }

    public static List<Ravine> Build(DomeGenConfigSO cfg, long seed, Vector2 labCenter,
        Vector2 entry, DomeHydro.State hydro, float rin, int count = 3)
    {
        var list = new List<Ravine>();
        if (cfg == null || hydro == null) return list;
        for (int i = 0; i < count; i++)
        {
            Ravine best = default;
            float bestGround = float.NegativeInfinity;
            bool has = false;
            for (int attempt = 0; attempt < 96; attempt++)
            {
                int salt = i * 131 + attempt;
                float u = SeededHash.ToFloat01(SeededHash.Hash(seed, 7301, salt));
                float v = SeededHash.ToFloat01(SeededHash.Hash(seed, 7302, salt));
                float a = u * Mathf.PI * 2f;
                float r = Mathf.Lerp(rin * 0.35f, rin * 0.78f, v);
                Vector2 p = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                if (p.magnitude > rin - 28f) continue;
                if (Vector2.Distance(p, labCenter) < 55f) continue;
                if (Vector2.Distance(p, entry) < 35f) continue;
                float ground = DomeHydro.SampleHydro(cfg, hydro, p.x, p.y);
                if (ground < hydro.lakeLevel + 2.5f) continue;

                float length = Mathf.Lerp(34f, 58f,
                    SeededHash.ToFloat01(SeededHash.Hash(seed, 7303, salt)));
                float width = Mathf.Lerp(8f, 12f,
                    SeededHash.ToFloat01(SeededHash.Hash(seed, 7304, salt)));
                float depth = Mathf.Lerp(4f, 6f,
                    SeededHash.ToFloat01(SeededHash.Hash(seed, 7305, salt)));
                depth = Mathf.Min(depth, ground - hydro.lakeLevel - 0.15f);
                if (depth < 1.2f) continue;

                bool overlap = false;
                for (int k = 0; k < list.Count; k++)
                {
                    float min = (list[k].length + length) * 0.35f;
                    if (Vector2.Distance(p, list[k].center) < min) { overlap = true; break; }
                }
                if (overlap) continue;

                if (ground > bestGround)
                {
                    bestGround = ground;
                    best = new Ravine
                    {
                        center = p,
                        angleDeg = SeededHash.ToFloat01(SeededHash.Hash(seed, 7306, salt)) * 180f,
                        length = length,
                        width = width,
                        depth = depth,
                    };
                    has = true;
                }
            }
            if (has) list.Add(best);
        }
        return list;
    }

    /// <summary>Выемка оврага в сыром поле высот; дно не опускается ниже воды.</summary>
    public static float Apply(float raw, IReadOnlyList<Ravine> ravines, float x, float z, float lakeLevel)
    {
        if (ravines == null) return raw;
        float y = raw;
        for (int i = 0; i < ravines.Count; i++)
        {
            var r = ravines[i];
            float dx = x - r.center.x, dz = z - r.center.y;
            float c = Mathf.Cos(r.angleDeg * Mathf.Deg2Rad);
            float s = Mathf.Sin(r.angleDeg * Mathf.Deg2Rad);
            float along = dx * c + dz * s;
            float across = -dx * s + dz * c;
            float u = (along / r.length) * (along / r.length) +
                      (across / r.width) * (across / r.width);
            if (u >= 1f) continue;
            float k = 1f - u;
            k = k * k * (3f - 2f * k);
            y -= r.depth * k;
        }
        return Mathf.Max(y, lakeLevel + 0.45f);
    }

    public static int BuildFallenTrees(GameObject root, IReadOnlyList<Ravine> ravines, long seed,
        System.Func<float, float, float> ground, Material bark, ICollection<Mesh> track)
    {
        if (root == null || ravines == null) return 0;
        int n = 0;
        for (int i = 0; i < ravines.Count; i++)
        {
            var r = ravines[i];
            for (int k = 0; k < 2; k++)
            {
                float along = (k == 0 ? -0.2f : 0.25f) * r.length;
                float across = (k == 0 ? -1f : 1f) * r.width * 0.45f;
                float c = Mathf.Cos(r.angleDeg * Mathf.Deg2Rad);
                float s = Mathf.Sin(r.angleDeg * Mathf.Deg2Rad);
                float x = r.center.x + along * c - across * s;
                float z = r.center.y + along * s + across * c;
                float y = ground(x, z);
                var mesh = FloraMeshKit.Trunk(0.28f, 0.16f, 6.5f);
                track?.Add(mesh);
                var go = new GameObject("FallenTrunk");
                go.transform.SetParent(root.transform, false);
                go.transform.position = new Vector3(x, y + 0.28f, z);
                go.transform.rotation = Quaternion.Euler(90f, r.angleDeg + (k == 0 ? 20f : -25f), 0f);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = bark;
                n++;
            }
        }
        return n;
    }

    /// <summary>Земляные борта: приподнятые валы по краям, чтобы ложбина читалась.</summary>
    public static int BuildWalls(GameObject root, IReadOnlyList<Ravine> ravines,
        System.Func<float, float, float> ground, Material rock, ICollection<Mesh> track)
    {
        if (root == null || ravines == null) return 0;
        int n = 0;
        for (int i = 0; i < ravines.Count; i++)
        {
            var r = ravines[i];
            float c = Mathf.Cos(r.angleDeg * Mathf.Deg2Rad);
            float s = Mathf.Sin(r.angleDeg * Mathf.Deg2Rad);
            for (int side = -1; side <= 1; side += 2)
            {
                float across = side * r.width * 0.58f;
                float x = r.center.x - across * s;
                float z = r.center.y + across * c;
                float y = ground(x, z);
                var mesh = FloraMeshKit.Slab(0.9f, 2.4f, r.length * 0.48f);
                track?.Add(mesh);
                var go = new GameObject("RavineWall");
                go.transform.SetParent(root.transform, false);
                go.transform.position = new Vector3(x, y - 0.25f, z);
                go.transform.rotation = Quaternion.Euler(0f, r.angleDeg, 0f);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = rock;
                n++;
            }
        }
        return n;
    }

    /// <summary>Тёмное дно: читаемая полоса земли между бортами.</summary>
    public static int BuildFloor(GameObject root, IReadOnlyList<Ravine> ravines,
        System.Func<float, float, float> ground, Material dark, ICollection<Mesh> track)
    {
        if (root == null || ravines == null) return 0;
        int n = 0;
        for (int i = 0; i < ravines.Count; i++)
        {
            var r = ravines[i];
            float x = r.center.x, z = r.center.y;
            float y = ground(x, z);
            var mesh = FloraMeshKit.Slab(r.width * 0.42f, 0.08f, r.length * 0.42f);
            track?.Add(mesh);
            var go = new GameObject("RavineFloor");
            go.transform.SetParent(root.transform, false);
            go.transform.position = new Vector3(x, y + 0.04f, z);
            go.transform.rotation = Quaternion.Euler(0f, r.angleDeg, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = dark;
            n++;
        }
        return n;
    }

    /// <summary>Каменные борта: делают выемку читаемой, даже если поле вокруг низкое.</summary>
    public static int BuildRim(GameObject root, IReadOnlyList<Ravine> ravines, long seed,
        System.Func<float, float, float> ground, Material rock, ICollection<Mesh> track)
    {
        if (root == null || ravines == null) return 0;
        int n = 0;
        for (int i = 0; i < ravines.Count; i++)
        {
            var r = ravines[i];
            float c = Mathf.Cos(r.angleDeg * Mathf.Deg2Rad);
            float s = Mathf.Sin(r.angleDeg * Mathf.Deg2Rad);
            for (int side = -1; side <= 1; side += 2)
            {
                for (int k = -2; k <= 2; k++)
                {
                    float along = k * r.length * 0.18f;
                    float across = side * r.width * 0.58f;
                    float x = r.center.x + along * c - across * s;
                    float z = r.center.y + along * s + across * c;
                    float y = ground(x, z);
                    float radius = 1.8f + Mathf.Abs(k) * 0.25f;
                    var mesh = FloraMeshKit.Icosahedron(radius);
                    track?.Add(mesh);
                    var go = new GameObject("RavineRim");
                    go.transform.SetParent(root.transform, false);
                    go.transform.position = new Vector3(x, y + radius * 0.55f, z);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    go.AddComponent<MeshRenderer>().sharedMaterial = rock;
                    n++;
                }
            }
        }
        return n;
    }

    public static int BuildNests(GameObject root, IReadOnlyList<Ravine> ravines, long seed,
        System.Func<float, float, float> ground, Material dark, Material olive,
        ICollection<Mesh> track)
    {
        if (root == null || ravines == null) return 0;
        int n = 0;
        for (int i = 0; i < ravines.Count; i++)
        {
            var r = ravines[i];
            float along = (i % 2 == 0 ? -0.1f : 0.15f) * r.length;
            float c = Mathf.Cos(r.angleDeg * Mathf.Deg2Rad);
            float s = Mathf.Sin(r.angleDeg * Mathf.Deg2Rad);
            float x = r.center.x + along * c;
            float z = r.center.y + along * s;
            float y = ground(x, z);
            var baseMesh = FloraMeshKit.Slab(2.8f, 0.18f, 2.2f);
            var moundMesh = FloraMeshKit.Icosahedron(0.9f);
            track?.Add(baseMesh);
            track?.Add(moundMesh);
            var nest = new GameObject("VisualSnakeNest");
            nest.transform.SetParent(root.transform, false);
            nest.transform.position = new Vector3(x, y + 0.15f, z);
            nest.transform.rotation = Quaternion.Euler(0f, r.angleDeg, 0f);
            AddPart(nest.transform, "NestBase", baseMesh, dark, Vector3.zero);
            AddPart(nest.transform, "NestMound", moundMesh, olive,
                new Vector3(0.6f, 0.15f, 0.2f));
            n++;
        }
        return n;
    }

    static void AddPart(Transform parent, string name, Mesh mesh, Material mat, Vector3 pos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
    }
}
