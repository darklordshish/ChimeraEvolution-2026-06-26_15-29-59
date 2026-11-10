using System.Collections.Generic;
using UnityEngine;

/// <summary>КОЖА ОРГАНА ПРОЕКЦИЕЙ (спека `2026-11-10-kozha-organa-proekciej.md`): прививка Шкуры вида-донора надевает на тело шасси
/// плитку этого вида — цвет и карту высоты, шейдер `Chimera/KozhaProekciej` проецирует их по позе покоя (uv2/uv3 меша, их кладёт
/// `BodyMesh.Place`). Тело не пересобирается и не копируется под донора: меняется материал и вес.
///     ПЛИТКИ ЛЕЖАТ ПО ДОНОРУ: `Resources/Кожа/<вид>/cvet` и `rel`. Нет плитки — кожи нет, тело рисуется как раньше (вид без плитки
/// не ломает химеру). Видна кожа ПЕРВОГО органа слота `Шкура` в порядке отрисовки (решение 14 спеки двух слоёв); свой орган шасси
/// кожей не считается — родное тело и так нарисовано мешем.</summary>
public static class KozhaOrgana
{
    public const string ShaderName = "Chimera/KozhaProekciej";
    public const string Slot = "Шкура";
    public static readonly int SkinWeight = Shader.PropertyToID("_SkinWeight");
    public static readonly int BodyHeight = Shader.PropertyToID("_BodyHeight");

    static readonly Dictionary<string, Material> cache = new();

    /// <summary>Материал кожи донора или null, если плитки у вида нет. Кэш по донору; мёртвую запись (выгрузка сцены) пересобираем.</summary>
    public static Material For(SpeciesSO donorSpecies)
    {
        string donor = donorSpecies != null ? donorSpecies.speciesName : null;
        if (string.IsNullOrEmpty(donor)) return null;
        if (cache.TryGetValue(donor, out var hit) && hit != null) { hit.SetColor("_SkinTint", Tint(donorSpecies)); return hit; }
        var color = Resources.Load<Texture2D>("Кожа/" + donor + "/cvet");
        var relief = Resources.Load<Texture2D>("Кожа/" + donor + "/rel");
        var shader = Shader.Find(ShaderName);
        if (color == null || relief == null || shader == null) { cache.Remove(donor); return null; }
        var m = new Material(shader) { name = "Кожа · " + donor, hideFlags = HideFlags.HideAndDontSave };
        m.SetTexture("_SkinTex", color);
        m.SetTexture("_SkinHeight", relief);
        m.SetColor("_SkinMean", MeanLinear(color));
        ApplyPattern(m, Resources.Load<Texture2D>("Кожа/" + donor + "/uzor"));
        m.SetColor("_SkinTint", Tint(donorSpecies));
        cache[donor] = m;
        return m;
    }

    public const float PatternContrast = 0.6f;
    public static readonly int PatternTex = Shader.PropertyToID("_PatternTex");
    public static readonly int PatternContrastId = Shader.PropertyToID("_PatternContrast");

    /// <summary>КРУПНЫЙ УЗОР (`uzor.png`, необязателен): сёдла змеи поверх мелкой чешуи. Нет плитки — контраст 0, слоя нет.</summary>
    public static void ApplyPattern(Material m, Texture2D pattern)
    {
        if (pattern != null) m.SetTexture(PatternTex, pattern);
        m.SetFloat(PatternContrastId, pattern != null ? PatternContrast : 0f);
    }

    /// <summary>Цвет вида-донора — тот же `SpeciesSO.tint`, из которого `TintMixer` собирает цвет по составу.</summary>
    static Color Tint(SpeciesSO s) { var c = s.tint; c.a = 1f; return c; }

    /// <summary>СРЕДНЕЕ ПЛИТКИ В ЛИНЕЙНОМ ПРОСТРАНСТВЕ (как её видит шейдер): плитка делится на него, чтобы кожа давала контраст
    /// швов, а не темнила тело. Считается при загрузке по каждому четвёртому пикселю; PNG не трогаем (нужна галка Read/Write).</summary>
    static Color MeanLinear(Texture2D t)
    {
        Color32[] px;
        try { px = t.GetPixels32(); } catch { Debug.LogWarning($"[кожа] плитка «{t.name}» нечитаема (Read/Write) — среднее принято 0.5"); return new Color(0.5f, 0.5f, 0.5f, 1f); }
        double r = 0, g = 0, b = 0; int n = 0;
        for (int i = 0; i < px.Length; i += 4)
        {
            r += Mathf.GammaToLinearSpace(px[i].r / 255f); g += Mathf.GammaToLinearSpace(px[i].g / 255f); b += Mathf.GammaToLinearSpace(px[i].b / 255f); n++;
        }
        return n == 0 ? Color.gray : new Color((float)(r / n), (float)(g / n), (float)(b / n), 1f);
    }

    /// <summary>Какая кожа на теле: первый орган слота `Шкура` в порядке отрисовки, чей донор — не шасси и у которого есть плитка.
    /// Вес — 1: прививка включает кожу целиком (параметр `organObjects` оставлен для будущего роста графта).</summary>
    public static Material Of(SpeciesSO chassis, IReadOnlyList<Organ> worn, IReadOnlyDictionary<string, float> organObjects, out float weight, out string donorName)
    {
        weight = 0f; donorName = null;
        if (chassis == null || worn == null) return null;
        foreach (var o in worn)
        {
            if (o == null || o.chassisOnly || o.slot != Slot) continue;
            if (chassis.organs != null && System.Array.IndexOf(chassis.organs, o) >= 0) continue;   // свой орган шасси
            var owner = ChainSwap.OwnerOf(o);
            if (owner == null || owner == chassis) continue;
            var mat = For(owner);
            if (mat == null) continue;   // у этого донора плитки нет — смотрим следующий орган слота
            weight = 1f;   // надетый графт Шкуры включает кожу полностью: это орган, а не доля идентичности (координатор 10.11)
            donorName = owner.speciesName;
            return mat;
        }
        return null;
    }
}
