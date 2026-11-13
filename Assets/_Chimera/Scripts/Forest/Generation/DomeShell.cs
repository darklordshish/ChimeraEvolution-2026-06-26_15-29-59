using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// S3 §4.2: остов — след лаборатории у кромки открытой зоны, а НЕ кромка-арена.
/// 2 штуки на павильон, ≤10×8 м, высота ≤0.8 м, плита ≤0.4 м, камни 0.6–1.2 м,
/// коллайдер ≤0.5 м, ≤30 камней на павильон, 0 замкнутых колец. Лута и триггеров нет.
/// <para>
/// На v1 остаётся исторический <see cref="DomeArenaEdge"/>; сюда попадает только v2.
/// </para>
/// </summary>
public static class DomeShell
{
    public struct Spot
    {
        public Vector2 center;
        public float yawDeg;
        public float groundY;
        public int zone;
        public float halfWidth;
        public float halfDepth;
    }

    /// <summary>
    /// Поставить остовы у кромки первых зон, у которых есть связь: остов без зоны —
    /// просто куча бетона, а остов у кромки объясняет, почему здесь нет леса.
    /// </summary>
    public static List<Spot> Build(GameObject root, IReadOnlyList<ForestSpace.Zone> zones,
        IReadOnlyList<ForestSpace.Link> links, System.Func<float, float, float> ground,
        Material slabMat, Material stoneMat, long seed, ICollection<Mesh> track = null,
        int want = 2)
    {
        var spots = new List<Spot>();
        if (root == null || zones == null || ground == null || zones.Count == 0) return spots;

        int stones = 0;
        for (int zi = 0; zi < zones.Count && spots.Count < want; zi++)
        {
            var zone = zones[zi];
            // Остов — причина прогалины, а не награда за связь: он ставится у кромки
            // зоны и без валидной связи (S3 §4.2). Связь лишь задаёт направление.
            Vector2 dir = EdgeDirection(zone, zones, links, zi, seed);
            float dist = zone.radius * 0.62f;
            Vector2 center = zone.center + dir * dist;
            float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
            float gy = ground(center.x, center.y);
            var spot = new Spot
            {
                center = center,
                yawDeg = yaw,
                groundY = gy,
                zone = zi,
                halfWidth = 5f,
                halfDepth = 4f,
            };
            spots.Add(spot);

            var shell = new GameObject("LabShell_" + zi);
            shell.transform.SetParent(root.transform, false);
            shell.transform.position = new Vector3(center.x, gy, center.y);
            shell.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            var slab = FloraMeshKit.Slab(5f, 0.35f, 4f);
            track?.Add(slab);
            Part(shell.transform, "ShellSlab", slab, slabMat,
                new Vector3(0f, 0.175f, 0f));

            // Камни — дуга вдоль кромки, не кольцо. ≤3 на остов, ≤30 на павильон.
            for (int k = 0; k < 3 && stones < 30; k++)
            {
                float a = Mathf.Lerp(-0.9f, 0.9f, k / 2f);
                float r = 1.1f + 0.3f * k;
                var mesh = FloraMeshKit.Icosahedron(r);
                track?.Add(mesh);
                var go = Part(shell.transform, "ShellStone", mesh, stoneMat,
                    new Vector3(Mathf.Sin(a) * 5.2f, 0.35f + r * 0.25f, Mathf.Cos(a) * 4.2f));
                go.transform.localScale = new Vector3(1.15f, 0.55f, 1.05f);
                var box = go.AddComponent<BoxCollider>();
                box.size = new Vector3(r * 2f, 0.5f, r * 2f);
                stones++;
            }
        }
        return spots;
    }

    static Vector2 EdgeDirection(ForestSpace.Zone zone, IReadOnlyList<ForestSpace.Zone> zones,
        IReadOnlyList<ForestSpace.Link> links, int index, long seed)
    {
        Vector2 c = zone.center;
        if (links != null)
            foreach (var l in links)
            {
                if ((l.a - c).sqrMagnitude < 0.25f)
                {
                    Vector2 d = l.b - l.a;
                    if (d.sqrMagnitude > 1e-4f)
                        return new Vector2(-d.y, d.x).normalized;
                }
                if ((l.b - c).sqrMagnitude < 0.25f)
                {
                    Vector2 d = l.a - l.b;
                    if (d.sqrMagnitude > 1e-4f)
                        return new Vector2(-d.y, d.x).normalized;
                }
            }
        float a = SeededHash.ToFloat01(SeededHash.Hash(seed, 9801, index)) * Mathf.PI * 2f;
        return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
    }

    static GameObject Part(Transform parent, string name, Mesh mesh, Material mat, Vector3 pos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }
}
