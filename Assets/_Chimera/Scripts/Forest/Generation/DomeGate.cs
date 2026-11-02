using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// S3 §4: ворота уровня — 2 штуки, 35–60 м от `rin` внутри чаши. Проём 6–8 м,
/// два столба 0.4×0.4×2.6–3.2 м, перекладина 4–6 м на 2.8–3.2 м. Без створок,
/// забора и коллайдеров: это адрес и ориентир, а не стена (NavMesh не задевает).
/// </summary>
public static class DomeGate
{
    public struct Spot
    {
        public Vector2 center;
        public float yawDeg;
        public float groundY;
        public int index;
    }

    /// <summary>Якоря ворот ДО формы (S3 §3.2): из них строится маска `gate`.</summary>
    public static List<Spot> Anchors(DomeGenConfigSO cfg, long seed, int want = 2)
    {
        var list = new List<Spot>();
        if (cfg == null) return list;
        float rin = Mathf.Max(1f, DomeRockRing.InnerRadius(cfg));
        float entryAngle = Mathf.Atan2(-1f, 0f); // угол входа не важен: важно развести ворота
        for (int i = 0; i < want; i++)
        {
            float a = SeededHash.ToFloat01(SeededHash.Hash(seed, 9851, i)) * Mathf.PI * 2f;
            float inward = Mathf.Lerp(35f, 60f,
                SeededHash.ToFloat01(SeededHash.Hash(seed, 9852, i)));
            float r = Mathf.Max(1f, rin - inward);
            // Разводим ворота от входа по углу: они не должны стоять в одной точке.
            a = a + entryAngle + (i == 0 ? 0.8f : -0.8f);
            list.Add(new Spot
            {
                center = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r,
                // Проём ориентирован по касательной к кольцу: проход «вдоль чаши».
                yawDeg = Mathf.Atan2(Mathf.Cos(a), -Mathf.Sin(a)) * Mathf.Rad2Deg,
                index = i,
            });
        }
        return list;
    }

    /// <summary>
    /// Достроить ворота на финальном полу: XZ остаётся якорем, высота — `ShapeGround`.
    /// </summary>
    public static void Build(GameObject root, IReadOnlyList<Spot> anchors,
        System.Func<float, float, float> ground, Material postMat, Material lintelMat,
        ICollection<Mesh> track = null)
    {
        if (root == null || anchors == null || ground == null) return;
        foreach (var spot in anchors)
        {
            float gy = ground(spot.center.x, spot.center.y);
            var gate = new GameObject("LevelGate_" + spot.index);
            gate.transform.SetParent(root.transform, false);
            gate.transform.position = new Vector3(spot.center.x, gy, spot.center.y);
            gate.transform.rotation = Quaternion.Euler(0f, spot.yawDeg, 0f);

            const float openingHalf = 3.4f; // проём 6.8 м
            var post = FloraMeshKit.Slab(0.2f, 1.5f, 0.2f); // 0.4 × 3.0 × 0.4
            track?.Add(post);
            Part(gate.transform, "GatePostL", post, postMat,
                new Vector3(-(openingHalf + 0.2f), 1.5f, 0f));
            Part(gate.transform, "GatePostR", post, postMat,
                new Vector3(openingHalf + 0.2f, 1.5f, 0f));

            var lintel = FloraMeshKit.Slab(3.2f, 0.15f, 0.2f); // 6.4 × 0.3 × 0.4
            track?.Add(lintel);
            Part(gate.transform, "GateLintel", lintel, lintelMat,
                new Vector3(0f, 3.0f, 0f));
        }
    }

    static void Part(Transform parent, string name, Mesh mesh, Material mat, Vector3 pos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
    }
}
