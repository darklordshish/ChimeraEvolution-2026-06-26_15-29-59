using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Поле запаха (данные для ИИ): игрок роняет точки следа (ScentEmitter), они выцветают за `lifetime`.
/// Враг, потерявший игрока из виду, спрашивает ближайшую СВЕЖУЮ точку и идёт по тропе → выходит из-за угла.
/// Авто-синглтон. Визуал запаха — отдельно (ScentTrail). Пока только след игрока; вид/фракции добавим, когда понадобятся.
/// </summary>
public class ScentField : MonoBehaviour
{
    [SerializeField] float lifetime = 12f; // сколько живёт точка следа

    struct Point { public Vector3 pos; public float born; }
    readonly List<Point> points = new();

    static ScentField instance;
    public static ScentField Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<ScentField>();
                if (instance == null) instance = new GameObject("ScentField").AddComponent<ScentField>();
            }
            return instance;
        }
    }

    public void Drop(Vector3 pos) => points.Add(new Point { pos = pos, born = Time.time });

    public void Clear() => points.Clear(); // dev-призрак: сброс интереса — старый след стирается

    void Update()
    {
        float cutoff = Time.time - lifetime;
        points.RemoveAll(p => p.born < cutoff); // чистим выцветшие
    }

    // ближайшая СВЕЖАЯ точка следа в радиусе; идём к ней → продвигаемся по тропе к игроку
    public bool TryFollow(Vector3 from, float radius, out Vector3 target)
    {
        float best = -1f, r2 = radius * radius;
        target = from;
        bool found = false;
        foreach (var p in points)
        {
            if ((p.pos - from).sqrMagnitude > r2) continue;
            if (p.born > best) { best = p.born; target = p.pos; found = true; }
        }
        return found;
    }
}
