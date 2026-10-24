using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Устья и гнёзда склона — геометрия, общая для превью и рантайма.
/// Порталы из плит (косяки + перемычка + тёмная карта ниши), скалы вокруг,
/// каирны-маркеры гнёзд. Числа — из s10e-2; посадка по переданному полу (кольцо).
/// Лес, срез A.
/// </summary>
public static class DomeMouthBuilder
{
    /// <summary>
    /// Создаёт геометрию устьев/гнёзд под parent. track (необязателен) получает
    /// уникальные меши, созданные здесь: рантайм-мир уничтожает их вместе с корнем.
    /// </summary>
    public static void Build(Transform parent, DomeGenConfigSO cfg,
        List<DomeSlope.MouthSpot> mouths, List<DomeSlope.NestSpot> nests,
        Func<float, float, float> ground, Material rock, Material dark, Material nest,
        ICollection<Mesh> track = null)
    {
        if (parent == null) throw new ArgumentNullException(nameof(parent));
        if (cfg == null) throw new ArgumentNullException(nameof(cfg));
        if (ground == null) throw new ArgumentNullException(nameof(ground));
        if (mouths == null) throw new ArgumentNullException(nameof(mouths));
        if (nests == null) throw new ArgumentNullException(nameof(nests));

        var cache = new Dictionary<string, Mesh>();
        var tracked = track != null ? new HashSet<Mesh>() : null;

        Mesh Cached(string key, Func<Mesh> build)
        {
            if (!cache.TryGetValue(key, out var mesh)) cache[key] = mesh = build();
            if (track != null && tracked.Add(mesh)) track.Add(mesh);
            return mesh;
        }

        Mesh Slab(float sx, float sy, float sz) =>
            Cached($"slab|{sx:F3}|{sy:F3}|{sz:F3}", () => FloraMeshKit.Slab(sx, sy, sz));
        Mesh Spike(float r, float h) =>
            Cached($"spike|{r:F3}|{h:F3}", () => FloraMeshKit.Spike(r, h));

        foreach (var m in mouths)
        {
            float gy = ground(m.pos.x, m.pos.y);
            float yaw = Mathf.Atan2(Mathf.Cos(m.facing), Mathf.Sin(m.facing)) * Mathf.Rad2Deg;
            var root = new GameObject("Mouth");
            root.transform.SetParent(parent, false);
            // Локальные координаты: поле привязано к корню мира, а не к мировому нулю.
            root.transform.localPosition = new Vector3(m.pos.x, gy - 0.3f, m.pos.y);
            root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            Box(root.transform, Slab(0.8f, 3f, 1.2f), rock, new Vector3(-1.6f, 0f, 0f));
            Box(root.transform, Slab(0.8f, 3f, 1.2f), rock, new Vector3(1.6f, 0f, 0f));
            Box(root.transform, Slab(4.2f, 0.8f, 1.4f), rock, new Vector3(0f, 3.2f, 0f));
            Box(root.transform, Slab(3.2f, 3f, 0.1f), dark, new Vector3(0f, 1.5f, -0.8f));
        }

        int ri = 0;
        foreach (var m in mouths)
        {
            for (int j = 0; j < 14; j++, ri++)
            {
                float a = SeededHash.ToFloat01(SeededHash.Hash(cfg.seed, 800, ri)) * Mathf.PI * 2f;
                float dist = 5f + SeededHash.ToFloat01(SeededHash.Hash(cfg.seed, 801, ri)) * 13f;
                float x = m.pos.x + Mathf.Cos(a) * dist;
                float z = m.pos.y + Mathf.Sin(a) * dist;
                var mesh = (ri % 2 == 0) ? Slab(0.9f, 0.5f, 0.7f) : Spike(0.5f, 2.4f);
                var go = Part(parent, "MouthRock", mesh, rock);
                go.transform.localPosition = new Vector3(x, ground(x, z), z);
                float s = 1.2f + SeededHash.ToFloat01(SeededHash.Hash(cfg.seed, 802, ri)) * 1.6f;
                go.transform.localScale = Vector3.one * s;
                go.transform.rotation = Quaternion.Euler(0f, SeededHash.ToFloat01(SeededHash.Hash(cfg.seed, 803, ri)) * 360f, 0f);
            }
        }

        foreach (var n in nests)
        {
            float gy = ground(n.pos.x, n.pos.y);
            Cairn(parent, Slab(0.9f, 0.54f, 0.9f), nest, n.pos.x, gy, n.pos.y);
            Cairn(parent, Slab(0.6f, 0.36f, 0.6f), nest, n.pos.x, gy + 0.55f, n.pos.y);
            Cairn(parent, Slab(0.35f, 0.21f, 0.35f), nest, n.pos.x, gy + 0.95f, n.pos.y);
        }
    }

    static void Box(Transform parent, Mesh mesh, Material mat, Vector3 localPos)
    {
        var go = new GameObject("MouthPart");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
    }

    static GameObject Part(Transform parent, string name, Mesh mesh, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    static void Cairn(Transform parent, Mesh mesh, Material mat, float x, float y, float z)
    {
        var go = Part(parent, "NestCairn", mesh, mat);
        go.transform.localPosition = new Vector3(x, y, z);
    }
}
