using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Дождь павильона (s10h): flat-призмы (вердикт художника — не мягкие Shuriken-стрипы),
/// падают и заворачиваются сверху. 600 полос в боксе вокруг центра. В редакторе стоят
/// (кадр), в Play падают (тест).
/// </summary>
public class DomeRain : MonoBehaviour
{
    public static string CodeVersion() => "s10h-streak2";
    public int drops = 600;
    public float area = 160f;
    public float height = 30f;
    public float speed = 25f;

    readonly List<Transform> pool = new List<Transform>();
    static Mesh streakMesh;
    static Material streakMat;

    public static DomeRain BuildPreview(GameObject parent, float cx, float cz)
    {
        var go = new GameObject("~DomeRain");
        go.transform.SetParent(parent.transform, false);
        go.transform.position = new Vector3(cx, 0f, cz);
        var rain = go.AddComponent<DomeRain>();
        rain.Build();
        return rain;
    }

    void Build()
    {
        if (streakMesh == null)
        {
            // Тонкий бокс из кита (видно со всех сторон, без backface-дыр):
            // ручной quad-меш в этом пайплайне не рисуется (кадр s10h).
            streakMesh = FloraMeshKit.Slab(0.06f, 2.5f, 0.06f);
            streakMesh.name = "RainStreak";
        }
        if (streakMat == null)
        {
            streakMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            streakMat.color = new Color(0.8f, 0.87f, 0.95f);
        }
        for (int i = 0; i < drops; i++)
        {
            var go = new GameObject("Drop");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(
                (SeededHash.ToFloat01(SeededHash.Hash(4242, i, 1)) * 2f - 1f) * area / 2,
                SeededHash.ToFloat01(SeededHash.Hash(4242, i, 2)) * height,
                (SeededHash.ToFloat01(SeededHash.Hash(4242, i, 3)) * 2f - 1f) * area / 2);
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = streakMesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = streakMat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pool.Add(go.transform);
        }
    }

    void Update()
    {
        float dt = Time.deltaTime;
        foreach (var t in pool)
        {
            Vector3 p = t.localPosition;
            p.y -= speed * dt;
            if (p.y < 0f) p.y += height;
            t.localPosition = p;
        }
    }

    public static void WipePreview(GameObject parent)
    {
        foreach (var rain in parent.GetComponentsInChildren<DomeRain>())
            Object.DestroyImmediate(rain.gameObject);
        if (streakMesh != null) { Object.DestroyImmediate(streakMesh); streakMesh = null; }
        if (streakMat != null) { Object.DestroyImmediate(streakMat); streakMat = null; }
    }
}
