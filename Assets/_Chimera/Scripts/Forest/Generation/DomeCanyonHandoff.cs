using UnityEngine;

/// <summary>
/// Решение B: каньон v2/1337 — авторский graded-меш, а не процедурный heightfield.
/// Ось и кромка взяты из handoff (20 точек), ось — мировые XZ от устья к липу.
/// Процедурный <see cref="DomeCanyon.Apply"/> в этом режиме не участвует в рельефе;
/// terrain вырезается по <see cref="Wout"/>, шов закрывает юбка префаба.
/// </summary>
public static class DomeCanyonHandoff
{
    public const long Seed = 1337L;
    public const float Wout = 11.758f;
    public const float OriginY = -7.5925f;
    public const string ResourcePath = "Canyon/CanyonAuthorGraded";

    /// <summary>20 точек handoff в мировых XZ; t=0 — устье, t=1 — лип.</summary>
    public static readonly Vector2[] Axis =
    {
        new Vector2(-76.443f, -404.831f),
        new Vector2(-67.619f, -407.853f),
        new Vector2(-58.826f, -410.967f),
        new Vector2(-50.114f, -414.271f),
        new Vector2(-42.030f, -418.924f),
        new Vector2(-34.599f, -424.491f),
        new Vector2(-27.644f, -430.703f),
        new Vector2(-21.070f, -437.319f),
        new Vector2(-14.027f, -443.418f),
        new Vector2(-6.535f, -448.916f),
        new Vector2(1.759f, -453.184f),
        new Vector2(10.464f, -456.484f),
        new Vector2(19.271f, -459.556f),
        new Vector2(28.029f, -462.766f),
        new Vector2(36.201f, -467.225f),
        new Vector2(43.859f, -472.476f),
        new Vector2(50.865f, -478.634f),
        new Vector2(57.476f, -485.213f),
        new Vector2(64.373f, -491.471f),
        new Vector2(71.644f, -497.314f),
    };

    public static Vector2 Mouth => Axis[0];
    public static Vector2 Lip => Axis[Axis.Length - 1];

    /// <summary>Yaw по хорде устье→лип; локальный +Z префаба смотрит к липу.</summary>
    public static float YawDeg =>
        Mathf.Atan2(Lip.x - Mouth.x, Lip.y - Mouth.y) * Mathf.Rad2Deg;

    public static Quaternion Rotation => Quaternion.Euler(0f, YawDeg, 0f);
    public static Vector3 RootPosition => new Vector3(Mouth.x, OriginY, Mouth.y);

    public static bool IsHandoff(long seed) => seed == Seed;

    public static GameObject LoadPrefab() =>
        Resources.Load<GameObject>(ResourcePath);

    /// <summary>Точка внутри выреза: строго внутри полосы Wout вокруг 20-точечной оси.</summary>
    public static bool Inside(float x, float z, float margin = 0f)
    {
        return DistanceToAxis(new Vector2(x, z)) < Wout + margin;
    }

    /// <summary>Чанк может задеть вырез — только тогда ему нужен clipped-путь.</summary>
    public static bool IntersectsChunk(float x0, float z0, float x1, float z1)
    {
        float minX = x0 - Wout, maxX = x1 + Wout;
        float minZ = z0 - Wout, maxZ = z1 + Wout;
        for (int i = 0; i < Axis.Length; i++)
        {
            var p = Axis[i];
            if (p.x >= minX && p.x <= maxX && p.y >= minZ && p.y <= maxZ) return true;
        }
        return DistanceToAxis(new Vector2((x0 + x1) * 0.5f, (z0 + z1) * 0.5f)) < Wout;
    }

    public static float DistanceToAxis(Vector2 p)
    {
        float best = float.PositiveInfinity;
        for (int i = 0; i < Axis.Length - 1; i++)
        {
            float d = DistanceToSegment(p, Axis[i], Axis[i + 1]);
            if (d < best) best = d;
        }
        return best;
    }

    static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float len2 = ab.sqrMagnitude;
        if (len2 < 1e-8f) return Vector2.Distance(p, a);
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
        return Vector2.Distance(p, a + ab * t);
    }
}
