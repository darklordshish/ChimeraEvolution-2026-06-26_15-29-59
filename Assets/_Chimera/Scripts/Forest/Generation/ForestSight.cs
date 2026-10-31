using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Детектор видимости: рельеф и стволы отдельно, чтобы было видно, что именно рвёт луч.
/// Не заменяет Perception: это измерительный слой для карты и тестов.
/// </summary>
public static class ForestSight
{
    public static bool TerrainBlocks(Vector3 a, Vector3 b,
        Func<float, float, float> ground, float eyeHeight = 1.6f, int samples = 48)
    {
        if (ground == null) return false;
        for (int i = 1; i < samples; i++)
        {
            float t = i / (float)samples;
            float x = Mathf.Lerp(a.x, b.x, t);
            float z = Mathf.Lerp(a.z, b.z, t);
            float rayY = Mathf.Lerp(a.y, b.y, t);
            if (ground(x, z) + eyeHeight > rayY + 0.15f) return true;
        }
        return false;
    }

    public static bool TrunksBlock(Vector3 a, Vector3 b,
        IReadOnlyList<Vector3> trunks, float radius = 0.45f, float trunkHeight = 14f)
    {
        if (trunks == null || trunks.Count == 0) return false;
        float minY = Mathf.Min(a.y, b.y);
        float maxY = Mathf.Max(a.y, b.y);
        Vector2 a2 = new Vector2(a.x, a.z);
        Vector2 b2 = new Vector2(b.x, b.z);
        Vector2 ab = b2 - a2;
        float len2 = ab.sqrMagnitude;
        for (int i = 0; i < trunks.Count; i++)
        {
            Vector3 t = trunks[i];
            if (maxY < t.y || minY > t.y + trunkHeight) continue;
            Vector2 p = new Vector2(t.x, t.z);
            float u = len2 < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a2, ab) / len2);
            if ((p - (a2 + ab * u)).sqrMagnitude < radius * radius) return true;
        }
        return false;
    }
}