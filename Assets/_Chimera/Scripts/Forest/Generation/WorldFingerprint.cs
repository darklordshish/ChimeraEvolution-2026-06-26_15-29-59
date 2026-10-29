using UnityEngine;

/// <summary>
/// Отпечаток собранного мира: пол, POI, счётчики и план.
/// Тот же (seed, version, flags) должен давать тот же отпечаток.
/// </summary>
public static class WorldFingerprint
{
    const ulong FnvOffset = 14695981039346656037UL;
    const ulong FnvPrime = 1099511628211UL;

    public static ulong Compute(ForestBuilder b)
    {
        if (b == null) return 0UL;
        ulong h = FnvOffset;
        int n = 32;
        float d = Mathf.Max(1f, b.mapDiameter);
        for (int iz = 0; iz < n; iz++)
            for (int ix = 0; ix < n; ix++)
            {
                float x = -d * 0.5f + (ix + 0.5f) * d / n;
                float z = -d * 0.5f + (iz + 0.5f) * d / n;
                Mix(ref h, Q(b.SampleGround(x, z)));
            }

        Mix(ref h, Q(b.Entry.x)); Mix(ref h, Q(b.Entry.z));
        Mix(ref h, Q(b.LabDoor.x)); Mix(ref h, Q(b.LabDoor.z));
        Mix(ref h, Q(b.LabAtrium.x)); Mix(ref h, Q(b.LabAtrium.z));
        Mix(ref h, b.Chunks); Mix(ref h, b.Trees);
        Mix(ref h, b.Lairs != null ? b.Lairs.Count : 0);
        Mix(ref h, b.ResolvedSeed);

        if (b.Hydro != null)
        {
            Mix(ref h, Q(b.Hydro.lakeCenter.x));
            Mix(ref h, Q(b.Hydro.lakeCenter.y));
            Mix(ref h, Q(b.Hydro.lakeRadius));
        }
        if (b.LabPlan != null)
        {
            MixString(ref h, b.LabPlan.template);
            Mix(ref h, b.LabPlan.version);
        }
        return h;
    }

    public static string Hex(ulong value) => value.ToString("x16");

    static int Q(float v) => Mathf.RoundToInt(v * 1000f);

    static void Mix(ref ulong h, int v)
    {
        h ^= (uint)v;
        h *= FnvPrime;
    }

    static void MixString(ref ulong h, string s)
    {
        if (string.IsNullOrEmpty(s)) { Mix(ref h, 0); return; }
        foreach (char c in s) Mix(ref h, c);
    }
}
