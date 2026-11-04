using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// G7 шаг 1: план мира как ДАННЫЕ. Рецепты, а не позиции: путь, семейства форм,
/// категориальные параметры и стабильные id. Числа в метрах сюда не входят —
/// их даёт форма/слой, а не дерево решений. Сериализуется через JsonUtility.
/// </summary>
[Serializable]
public sealed class MapPlan
{
    public string path = "";
    public string identity = "";
    public string signature = "";
    public List<DecisionNode> nodes = new List<DecisionNode>();
    public List<FormRecipe> forms = new List<FormRecipe>();
    public List<PoiIntent> poi = new List<PoiIntent>();
    public List<MaskIntent> masks = new List<MaskIntent>();
    public ParamBlock parameters = new ParamBlock();

    public string ToJson() => JsonUtility.ToJson(this, true);

    public static MapPlan FromJson(string json) =>
        string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<MapPlan>(json);

    /// <summary>
    /// Текущий (legacy) мир: путь `scheme=legacy/near=current/station=current/canyon=current`.
    /// Адаптер только описывает уже построенную композицию; геометрию не меняет.
    /// </summary>
    public static MapPlan FromLegacy(DomeComposition.State state, DomeGenConfigSO cfg = null,
        int terrainQuads = 0, bool buildTerrace = false, bool buildCanyon = false,
        bool buildAsh = false, bool buildLabComplex = false)
    {
        var p = new MapPlan();
        var par = p.parameters;
        par.scheme = "legacy";
        par.hero = "current";
        par.near = "current";
        par.station = "current";
        par.canyon = "current";
        par.flipV = state != null && state.flipped;
        FillParams(par, cfg, terrainQuads, buildTerrace, buildCanyon, buildAsh, buildLabComplex);
        p.path = PathOf(par);
        FillForms(p, state);
        p.nodes = LegacyNodes(par);
        p.signature = SignatureOf(p);
        p.identity = IdentityOf(p);
        return p;
    }

    /// <summary>
    /// G7 шаг 3: план, выбранный деревом схем. Путь называет схему и ветвь `near`;
    /// герой приходит со схемой, а станция остаётся шагом 2 (не герой).
    /// </summary>
    public static MapPlan FromScheme(DomeComposition.State state, DomeGenConfigSO cfg,
        string scheme, string near, int terrainQuads = 0, bool buildTerrace = false,
        bool buildCanyon = false, bool buildAsh = false, bool buildLabComplex = false,
        string canyon = null)
    {
        var p = new MapPlan();
        var par = p.parameters;
        par.scheme = scheme;
        par.hero = DomeSchemes.Find(scheme) != null
            ? DomeSchemes.Find(scheme).hero
            : "current";
        par.near = near;
        par.station = "current";
        par.canyon = string.IsNullOrEmpty(canyon) ? "current" : canyon;
        par.flipV = state != null && state.flipped;
        FillParams(par, cfg, terrainQuads, buildTerrace, buildCanyon, buildAsh, buildLabComplex);
        p.path = PathOf(par);
        FillForms(p, state);
        p.nodes = new List<DecisionNode>
        {
            DomeSchemes.SchemeNode(),
            DomeSchemes.NearNode(scheme),
            new DecisionNode
            {
                id = "station", parent = "scheme",
                variants = { new Variant { id = par.station, fallback = "none" } },
            },
            new DecisionNode
            {
                id = "canyon", parent = "scheme",
                variants = { new Variant { id = par.canyon, fallback = "none" } },
            },
        };
        p.signature = SignatureOf(p);
        p.identity = IdentityOf(p);
        return p;
    }

    static void FillParams(ParamBlock par, DomeGenConfigSO cfg, int terrainQuads,
        bool buildTerrace, bool buildCanyon, bool buildAsh, bool buildLabComplex)
    {
        if (cfg == null) return;
        par.treeCount = cfg.treeCount;
        par.treeDensityTarget = cfg.treeDensityTarget;
        par.forestThreshold = cfg.forestThreshold;
        par.buildRelief = cfg.buildRelief;
        par.terrainQuads = terrainQuads;
        par.buildTerrace = buildTerrace;
        par.buildCanyon = buildCanyon;
        par.buildAsh = buildAsh;
        par.buildLabComplex = buildLabComplex;
    }

    static FormRecipe RecipeOf(DomeForm f) => new FormRecipe
    {
        role = f.name,
        name = f.name,
        family = f.kind.ToString(),
        sign = f.sign.ToString(),
    };

    static void FillForms(MapPlan p, DomeComposition.State state)
    {
        if (state == null) return;
        foreach (var f in state.forms)
            p.forms.Add(RecipeOf(f));
        foreach (var d in state.declared)
            p.forms.Add(new FormRecipe
            {
                role = d.role.ToString(),
                name = d.name,
                family = "declared",
                sign = d.applied ? "applied" : "skipped",
            });
    }

    public static string PathOf(ParamBlock p) =>
        $"scheme={p.scheme}/near={p.near}/station={p.station}/canyon={p.canyon}";

    public static string SignatureOf(MapPlan p)
    {
        var sb = new StringBuilder(p.path);
        sb.Append("|hero=").Append(p.parameters.hero);
        sb.Append("|flipV=").Append(p.parameters.flipV ? 1 : 0);
        sb.Append("|forms=");
        if (p.forms != null)
            foreach (var f in p.forms)
                sb.Append(f.role).Append(':').Append(f.family).Append(':').Append(f.sign).Append(';');
        return sb.ToString();
    }

    /// <summary>Hash(path + параметры + имена форм); без UnityEngine.Random.</summary>
    public static string IdentityOf(MapPlan p)
    {
        ulong h = 14695981039346656037UL;
        HashText(ref h, p.path);
        HashText(ref h, "|");
        HashText(ref h, p.parameters.Canonical());
        if (p.forms != null)
            foreach (var f in p.forms)
                HashText(ref h, "|" + f.role + ":" + f.name + ":" + f.family + ":" + f.sign);
        return h.ToString("x16");
    }

    public static ulong HashText(string s)
    {
        ulong h = 14695981039346656037UL;
        HashText(ref h, s);
        return h;
    }

    static void HashText(ref ulong h, string s)
    {
        if (string.IsNullOrEmpty(s))
        {
            h ^= 0x9E3779B97F4A7C15UL;
            h *= 1099511628211UL;
            return;
        }
        foreach (char c in s)
        {
            h ^= c;
            h *= 1099511628211UL;
        }
    }

    static List<DecisionNode> LegacyNodes(ParamBlock p)
    {
        var root = new DecisionNode { id = "scheme", parent = "" };
        root.variants.Add(new Variant { id = p.scheme, fallback = "" });
        var near = new DecisionNode { id = "near", parent = "scheme" };
        near.variants.Add(new Variant { id = p.near, fallback = "" });
        var station = new DecisionNode { id = "station", parent = "scheme" };
        station.variants.Add(new Variant { id = p.station, fallback = "" });
        var canyon = new DecisionNode { id = "canyon", parent = "scheme" };
        canyon.variants.Add(new Variant { id = p.canyon, fallback = "" });
        return new List<DecisionNode> { root, near, station, canyon };
    }
}

[Serializable]
public sealed class DecisionNode
{
    public string id = "";
    public string parent = "";
    public List<Variant> variants = new List<Variant>();
}

[Serializable]
public sealed class Variant
{
    public string id = "";
    public List<string> requires = new List<string>();
    public List<string> forbids = new List<string>();
    public float weight = 1f;
    public List<FormRecipe> forms = new List<FormRecipe>();
    public List<PoiIntent> poi = new List<PoiIntent>();
    public string fallback = "";
}

[Serializable]
public sealed class FormRecipe
{
    public string role = "";
    public string name = "";
    public string family = "";
    public string sign = "";
}

[Serializable]
public sealed class PoiIntent
{
    public string kind = "";
    public string role = "";
    public int count;
}

[Serializable]
public sealed class MaskIntent
{
    public string name = "";
    public string role = "";
}

[Serializable]
public sealed class ParamBlock
{
    public string scheme = "legacy";
    public string hero = "current";
    public string near = "current";
    public string station = "current";
    public string canyon = "current";
    public bool flipV;
    public int terrainQuads;
    public int treeCount;
    public float treeDensityTarget;
    public float forestThreshold;
    public bool buildRelief;
    public bool buildTerrace;
    public bool buildCanyon;
    public bool buildAsh;
    public bool buildLabComplex;

    public string Canonical() =>
        $"scheme={scheme};hero={hero};near={near};station={station};canyon={canyon};" +
        $"flipV={(flipV ? 1 : 0)};quads={terrainQuads};trees={treeCount};" +
        $"treeDensity={treeDensityTarget.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)};" +
        $"forestThreshold={forestThreshold.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)};" +
        $"relief={(buildRelief ? 1 : 0)};terrace={(buildTerrace ? 1 : 0)};" +
        $"canyon={(buildCanyon ? 1 : 0)};ash={(buildAsh ? 1 : 0)};lab={(buildLabComplex ? 1 : 0)}";
}
