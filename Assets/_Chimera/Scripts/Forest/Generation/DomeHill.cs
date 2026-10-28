using UnityEngine;

/// <summary>
/// Холм станции: lift-слой вокруг подземного бункера. Не декор и не камни —
/// высота входит в SampleGround, поэтому чанки, NavMesh и флора видят тот же пол.
/// Внутри пятна станции площадка/рампа важнее холма (DomeWorld.SampleGround).
/// </summary>
public static class DomeHill
{
    public struct State
    {
        public Vector2 center;
        public float radius;
        public float height;
        public float blend;
        public Vector2 notch;
        public float notchRadius;
        public bool valid;
    }

    const int SaltRadius = 9301;
    const int SaltHeight = 9302;
    const int SaltBlend = 9303;

    const float RadiusMin = 38f;
    const float RadiusMax = 50f;
    const float HeightMin = 7f;
    const float HeightMax = 10f;
    const float BlendMin = 12f;
    const float BlendMax = 18f;

    public static State Build(DomeGenConfigSO cfg, long seed, Vector2 labCenter,
        Vector2 notch = default, float notchRadius = 0f)
    {
        var none = default(State);
        if (cfg == null) return none;
        float r = Mathf.Lerp(RadiusMin, RadiusMax, Hash01(seed, SaltRadius));
        float h = Mathf.Lerp(HeightMin, HeightMax, Hash01(seed, SaltHeight));
        float b = Mathf.Lerp(BlendMin, BlendMax, Hash01(seed, SaltBlend));
        return new State
        {
            center = labCenter,
            radius = r,
            height = h,
            blend = b,
            notch = notch,
            notchRadius = Mathf.Max(0f, notchRadius),
            valid = true,
        };
    }

    static float Hash01(long seed, int salt) =>
        SeededHash.ToFloat01(SeededHash.Hash(seed, salt, 0));

    /// <summary>Тождество вне пятна; внутри — подъём; на кромке — плавный blend.</summary>
    public static float Apply(float raw, State s, float x, float z)
    {
        if (!s.valid) return raw;
        if (s.notchRadius > 0f)
        {
            float nx = x - s.notch.x;
            float nz = z - s.notch.y;
            if (nx * nx + nz * nz <= s.notchRadius * s.notchRadius) return raw;
        }
        float dx = x - s.center.x;
        float dz = z - s.center.y;
        float d = Mathf.Sqrt(dx * dx + dz * dz);
        float reach = s.radius + s.blend;
        if (d >= reach) return raw;
        if (reach <= 1e-4f) return raw;
        float k = 1f - d / reach;
        k = k * k * (3f - 2f * k);
        return raw + s.height * k;
    }

    public static bool Contains(State s, float x, float z, float margin = 0f)
    {
        if (!s.valid) return false;
        float reach = s.radius + Mathf.Max(0f, s.blend) + Mathf.Max(0f, margin);
        float dx = x - s.center.x;
        float dz = z - s.center.y;
        return dx * dx + dz * dz <= reach * reach;
    }
}
