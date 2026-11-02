using System.Collections.Generic;
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
        // Версия разметки: v1 — замороженная B-lite, v2 — открытые зоны S3. Для A/C/B0
        // (разметка выключена) версия не входит вовсе, поэтому старые отпечатки целы.
        int layout = b.patchyFlora ? ForestSpace.LayoutFor(b.WorldVersion) : 0;
        Mix(ref h, layout);
        Mix(ref h, b.Zones != null ? b.Zones.Count : 0);
        if (b.Zones != null)
            foreach (var c in b.Zones)
            {
                Mix(ref h, Q(c.center.x)); Mix(ref h, Q(c.center.y));
                Mix(ref h, Q(c.radius));
                // Поворот зоны — часть v2-разметки; в v1 его нет, и отпечаток B-lite
                // обязан остаться побитово прежним.
                if (layout >= ForestSpace.LayoutVersion) Mix(ref h, Q(c.yawDeg));
            }
        Mix(ref h, b.Links != null ? b.Links.Count : 0);
        if (b.Links != null)
            foreach (var c in b.Links)
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

        // S1: поле v2 и его формы — часть мира, как разметка B-lite. Входит в отпечаток
        // ТОЛЬКО при worldVersion > 1: правило живёт в одном месте — в MixRelief, —
        // поэтому v1-профили сохраняют замороженный отпечаток S0 бит-в-бит.
        // v2: формы S2; v1: старый P5-lite (FormsOf). Для v1 MixRelief — тождество.
        h = MixRelief(h, b.WorldVersion,
            b.CompositionForms != null ? b.CompositionForms : FormsOf(b.Relief),
            b.Contract.walkMaxDegrees, b.Contract.wallMinDegrees, b.Contract.classEpsilon);
        // S4: версия эрозии, сумма дельты и дороги — часть v2-мира.
        if (b.WorldVersion > 1)
        {
            var e = b.Erosion;
            Mix(ref h, e != null ? e.erosionVersion : 0);
            if (e != null) MixULong(ref h, e.deltaSum);
            var r = b.Roads;
            Mix(ref h, r != null ? r.roads.Count : 0);
            if (r != null)
            {
                Mix(ref h, Q(r.length));
                foreach (var road in r.roads)
                    foreach (var p in road.points)
                    { Mix(ref h, Q(p.x)); Mix(ref h, Q(p.y)); }
            }
        }
        return h;
    }

    /// <summary>Формы рельефа как список; null, если рельефа нет (v1-профили).</summary>
    public static List<DomeForm> FormsOf(DomeRelief.State relief)
    {
        if (relief == null || relief.hills == null) return null;
        var forms = new List<DomeForm>(relief.hills.Count);
        foreach (var hill in relief.hills) forms.Add(DomeRelief.ToForm(hill));
        return forms;
    }

    /// <summary>
    /// S1 §6.2 <c>Fingerprint_IncludesRelief</c>: версия поля, границы классов и список
    /// форм обязаны менять отпечаток. Смена порога меняет разметку мира так же, как высота.
    /// <para>
    /// P0-2: при <paramref name="worldVersion"/> ≤ 1 функция — ТОЖДЕСТВО. Так S0-константы
    /// A/C/B0/Pavilion не могут сдвинуться от появления полей v2 в коде, и отдельного
    /// поднятия <see cref="Version"/> для этого не требуется.
    /// </para>
    /// </summary>
    public static ulong MixRelief(ulong h, int worldVersion, IReadOnlyList<DomeForm> forms,
        float walkMaxDegrees, float wallMinDegrees, float classEpsilon = 0f)
    {
        if (worldVersion <= 1) return h;
        Mix(ref h, worldVersion);
        Mix(ref h, Q(walkMaxDegrees));
        Mix(ref h, Q(wallMinDegrees));
        if (classEpsilon > 0f) Mix(ref h, Q(classEpsilon));
        if (forms != null)
            foreach (var f in forms)
                MixForm(ref h, f);
        return h;
    }

    static void MixForm(ref ulong h, DomeForm f)
    {
        MixString(ref h, f.name);
        Mix(ref h, (int)f.sign);
        Mix(ref h, (int)f.kind);
        Mix(ref h, Q(f.center.x)); Mix(ref h, Q(f.center.y));
        Mix(ref h, Q(f.axis.x)); Mix(ref h, Q(f.axis.y));
        Mix(ref h, Q(f.halfLength));
        Mix(ref h, Q(f.diameter)); Mix(ref h, Q(f.crest)); Mix(ref h, Q(f.height));
    }

    public static string Hex(ulong value) => value.ToString("x16");

    static int Q(float v) => Mathf.RoundToInt(v * 1000f);

    static void MixULong(ref ulong h, ulong v)
    {
        h ^= v + 0x9E3779B97F4A7C15UL + (h << 6) + (h >> 2);
    }

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
