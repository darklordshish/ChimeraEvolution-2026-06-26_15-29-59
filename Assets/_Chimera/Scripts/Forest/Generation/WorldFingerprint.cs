using UnityEngine;

/// <summary>
/// Отпечаток собранного мира: пол, POI, счётчики и план.
/// Тот же (seed, version, flags) должен давать тот же отпечаток.
/// </summary>
public static class WorldFingerprint
{
    /// <summary>
    /// Версия формулы отпечатка. Меняется ТОЛЬКО вместе с новыми эталонными
    /// константами (S0/S1): иначе «тот же сид — тот же мир» перестаёт что-либо
    /// доказывать. v1 — формула, замороженная 2026-11-01 на HEAD 961f854d.
    /// </summary>
    public const int Version = 1;

    const ulong FnvOffset = 14695981039346656037UL;
    const ulong FnvPrime = 1099511628211UL;

    public static ulong Compute(ForestBuilder b)
    {
        if (b == null) return 0UL;
        ulong h = FnvOffset;
        Mix(ref h, b.FingerprintVersion); // 0 в сцене → v1, а не «версия 0»
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

        // B-lite: разметка пространства — часть мира, а не украшение. Тот же сид с другой
        // разметкой (поляны/коридоры/поды/дорожки) обязан дать другой отпечаток.
        // ВЕРСИЯ РАЗМЕТКИ ВХОДИТ В ОТПЕЧАТОК ТОЛЬКО ТАМ, ГДЕ РАЗМЕТКА ЖИВЁТ: A/C/B0
        // (patchyFlora = false) не должны «поехать» от будущего подъёма LayoutVersion.
        Mix(ref h, b.patchyFlora ? 1 : 0);
        Mix(ref h, b.patchyFlora ? ForestSpace.LayoutVersion : 0);
        Mix(ref h, b.Clearings != null ? b.Clearings.Count : 0);
        if (b.Clearings != null)
            foreach (var c in b.Clearings)
            {
                Mix(ref h, Q(c.center.x)); Mix(ref h, Q(c.center.y));
                Mix(ref h, Q(c.radius));
            }
        Mix(ref h, b.Corridors != null ? b.Corridors.Count : 0);
        if (b.Corridors != null)
            foreach (var c in b.Corridors)
            {
                Mix(ref h, Q(c.a.x)); Mix(ref h, Q(c.a.y));
                Mix(ref h, Q(c.b.x)); Mix(ref h, Q(c.b.y));
                Mix(ref h, Q(c.width));
            }
        Mix(ref h, b.CoverPods != null ? b.CoverPods.Count : 0);
        if (b.CoverPods != null)
            foreach (var p in b.CoverPods)
            {
                Mix(ref h, Q(p.center.x)); Mix(ref h, Q(p.center.y));
                Mix(ref h, Q(p.radius));
            }
        Mix(ref h, b.CoverPodsBuilt);

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
