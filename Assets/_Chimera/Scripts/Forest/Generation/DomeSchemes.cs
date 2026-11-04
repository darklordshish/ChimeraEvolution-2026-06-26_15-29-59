using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// G7 шаг 3: три схемы — СЛОВО силуэта, а не набор новых форм.
/// Схема выбирается по сиду среди прошедших жёсткие факты поля; герой приходит
/// вместе со схемой, станция и её резерв схемой НЕ выбираются (1A/stationReserve).
/// </summary>
public static class DomeSchemes
{
    public const string TwoCapesSaddle = "two-capes-saddle";
    public const string BowlRidge = "bowl-ridge";
    public const string LakeCauldron = "lake-cauldron";

    public sealed class Scheme
    {
        public string id;
        public string hero;
        public string word;
        public List<string> requires = new List<string>();
        public List<string> forbids = new List<string>();
        public string[] near;
        public bool fallback;
    }

    public static readonly Scheme[] All =
    {
        new Scheme
        {
            id = TwoCapesSaddle,
            hero = "saddle-between-capes",
            word = "подъём → проём → подъём → плато",
            requires = { "field" },
            near = new[] { "ridge", "hollow", "cape", "hybrid" },
        },
        new Scheme
        {
            id = BowlRidge,
            hero = "far-bowl-rim",
            word = "борт → спуск → дно → подъём → гряда",
            requires = { "field" },
            near = new[] { "ridge", "hollow", "hybrid" },
        },
        new Scheme
        {
            id = LakeCauldron,
            hero = "cauldron-rim",
            word = "спуск → вода → подъём",
            requires = { "field", "lake" },
            near = new[] { "hollow", "ridge", "hybrid" },
        },
    };

    /// <summary>Факты поля, на которые опирается дерево. Без UnityEngine.Random.</summary>
    public static List<string> Facts(Vector2 entry, DomeHydro.State hydro,
        Func<float, float, float> ground = null)
    {
        var facts = new List<string> { "field" };
        if (hydro != null)
        {
            if (hydro.lakeRadius > 0f) facts.Add("lake");
            if (hydro.riverPts != null && hydro.riverPts.Count >= 2) facts.Add("river");
            if (ground != null)
            {
                if (ground(entry.x, entry.y) > hydro.lakeLevel + 1f) facts.Add("dry");
            }
            else facts.Add("dry");
        }
        return facts;
    }

    /// <summary>
    /// Селектор по сиду: сначала вето/requires, затем детерминированный выбор
    /// среди прошедших. Не «монетка»: непрошедший кандидат не участвует.
    /// </summary>
    public static Scheme Select(DomeGenConfigSO cfg, long seed, Vector2 entry,
        DomeHydro.State hydro, Func<float, float, float> ground = null)
    {
        var facts = Facts(entry, hydro, ground);
        var eligible = new List<Scheme>();
        foreach (var s in All)
            if (MapPlanRules.Applies(ToVariant(s), facts)) eligible.Add(s);
        if (eligible.Count == 0)
        {
            var fb = All[0];
            var copy = new Scheme
            {
                id = fb.id, hero = fb.hero, word = fb.word,
                requires = fb.requires, forbids = fb.forbids, near = fb.near,
                fallback = true,
            };
            return copy;
        }
        int index = (int)(SeededHash.Hash(seed, 4410, 1) % (ulong)eligible.Count);
        return eligible[index];
    }

    public static string SelectNear(Scheme scheme, long seed)
    {
        if (scheme == null || scheme.near == null || scheme.near.Length == 0) return "ridge";
        int index = (int)(SeededHash.Hash(seed, 4411, 1) % (ulong)scheme.near.Length);
        return scheme.near[index];
    }

    /// <summary>Сторона раскладки — от воды/входа, а не «где больше форм встало».</summary>
    public static bool FlipV(Vector2 entry, Vector2 door, DomeHydro.State hydro, long seed)
    {
        Vector2 u = door - entry;
        if (u.sqrMagnitude < 1e-6f) u = Vector2.up;
        u.Normalize();
        Vector2 v = new Vector2(-u.y, u.x);
        if (hydro != null && hydro.lakeRadius > 0f)
            return Vector2.Dot(hydro.lakeCenter - entry, v) > 0f;
        return (SeededHash.Hash(seed, 4412, 1) & 1UL) != 0UL;
    }

    /// <summary>
    /// Ближняя форма 0–150 м: 2 plateau-формы по бокам оси, чтобы был проход.
    /// H 2–6 (cape 4–8), грань 22–28°; ось не перекрыта (боковой зазор ≥30 м).
    /// </summary>
    /// <summary>
    /// Герой/роли схемы, которые меняют ГЕОМЕТРИЮ, а не только путь.
    /// two-capes — существующий S2-набор; bowl-ridge и lake-cauldron добавляют
    /// свои формы; станция и её резерв в это не входят.
    /// </summary>
    public static DomeForm[] HeroForms(string scheme, long seed, Vector2 entry,
        Vector2 uAxis, Vector2 vAxis, DomeHydro.State hydro)
    {
        if (scheme == TwoCapesSaddle)
            return new[]
            {
                DomeComposition.CapeA(), DomeComposition.CapeB(),
                DomeComposition.SaddleShoulder(),
            };
        if (scheme == BowlRidge)
        {
            var bowl = DomeForms.Plateau(entry + uAxis * 650f + vAxis * 115f,
                220f, 60f, 12f, DomeFormSign.Carve, "Чаша схемы");
            var ridge = DomeForms.Capsule(entry + uAxis * 1080f + vAxis * 40f,
                vAxis, 130f, 62f, 16f, DomeFormSign.Lift, "Гряда схемы");
            return new[] { bowl, ridge };
        }
        if (scheme == LakeCauldron)
        {
            Vector2 c = hydro != null && hydro.lakeRadius > 0f
                ? hydro.lakeCenter
                : entry + uAxis * 620f + vAxis * 110f;
            float r = hydro != null && hydro.lakeRadius > 0f ? hydro.lakeRadius : 100f;
            var cauldron = DomeForms.Plateau(c, r * 2.4f, r * 1.1f, 8f,
                DomeFormSign.Carve, "Котёл схемы");
            var rim = DomeForms.Capsule(c, Vector2.right, r * 0.85f, 70f, 13f,
                DomeFormSign.Lift, "Кромка котла");
            return new[] { cauldron, rim };
        }
        return new DomeForm[0];
    }

    public static DomeForm[] NearForms(string scheme, string near, long seed,
        Vector2 entry, Vector2 uAxis, Vector2 vAxis, DomeHydro.State hydro,
        Func<float, float, float> ground = null)
    {
        // near реально зависит от ветви, схемы, сида и воды.
        float j = SeededHash.ToFloatSigned(SeededHash.Hash(seed, 4415, 1)) * 4f;
        float schemeShift = scheme == BowlRidge ? -6f : scheme == LakeCauldron ? 6f : 0f;
        float lakeShift = hydro != null && hydro.lakeRadius > 0f ? 3f : 0f;
        float nearShift = near == "hollow" ? 6f : near == "cape" ? -6f : near == "hybrid" ? 3f : 0f;
        float latShift = near == "cape" ? 4f : near == "hollow" ? -4f : 0f;
        float u = 70f + j + schemeShift + lakeShift + nearShift;
        float lateral = 80f + latShift;
        Vector2 inward = Vector2.Dot(-entry, vAxis) >= 0f ? vAxis : -vAxis;
        float lake = hydro != null ? hydro.lakeLevel : float.NegativeInfinity;
        if (ground != null)
        {
            bool found = false;
            for (int iu = 0; iu < 18 && !found; iu++)
                for (int iv = 0; iv < 8; iv++)
                {
                    float uu = 10f + iu * 8f + nearShift;
                    float vv = 70f + iv * 10f + latShift;
                    Vector2 p = entry + uAxis * uu + inward * vv;
                    if (ground(p.x, p.y) > lake + 1.5f) { u = uu; lateral = vv; found = true; break; }
                }
        }
        bool hollow = near == "hollow";
        float h = hollow ? 2.0f : 2.0f;
        // Три низкие формы в одном пятне: каждая ≤9.2° и C¹-шаг ≤2°,
        // сумма даёт грань 22–28° (M13), а не второй гейт.
        var a = LowForm("Ближняя L1", entry + uAxis * u + inward * lateral, h,
            hollow ? DomeFormSign.Carve : DomeFormSign.Lift);
        var b = LowForm("Ближняя L2", entry + uAxis * u + inward * lateral, h,
            DomeFormSign.Lift);
        var c = LowForm("Ближняя L3", entry + uAxis * u + inward * lateral, h,
            DomeFormSign.Lift);
        return new[] { a, b, c };
    }

    static DomeForm LowForm(string name, Vector2 center, float height, DomeFormSign sign)
    {
        // run = 1.5H/tan11°; D = 4·run (верх = D/2) ⇒ max|∇H| = 11°, шаг <2°.
        float run = 1.5f * height / Mathf.Tan(9.1f * Mathf.Deg2Rad);
        float d = 4f * run;
        return new DomeForm
        {
            name = name,
            sign = sign,
            kind = DomeFormKind.Plateau,
            center = center,
            axis = Vector2.right,
            diameter = d,
            crest = d * 0.5f,
            height = Mathf.Abs(height),
        };
    }

    static DomeForm NearForm(string name, Vector2 center, float height,
        DomeFormSign sign)
    {
        // D = 2*run, run = 1.5H/tan24° ⇒ H/run даёт грань ~24° (22–28°).
        float d = Mathf.Max(30f, 13.5f * height);
        float crest = d * 0.5f;
        return new DomeForm
        {
            name = name,
            sign = sign,
            kind = DomeFormKind.Plateau,
            center = center,
            axis = Vector2.right,
            diameter = d,
            crest = crest,
            height = Mathf.Abs(height),
        };
    }

    static Variant ToVariant(Scheme s)
    {
        var v = new Variant { id = s.id, weight = 1f, fallback = "none" };
        if (s.requires != null) v.requires.AddRange(s.requires);
        if (s.forbids != null) v.forbids.AddRange(s.forbids);
        return v;
    }

    public static DecisionNode SchemeNode()
    {
        var n = new DecisionNode { id = "scheme", parent = "" };
        foreach (var s in All)
            n.variants.Add(ToVariant(s));
        return n;
    }

    public static DecisionNode NearNode(string schemeId)
    {
        var n = new DecisionNode { id = "near", parent = "scheme" };
        var s = Find(schemeId);
        string[] ids = s != null ? s.near : new[] { "ridge", "hollow", "cape", "hybrid" };
        foreach (string id in ids)
            n.variants.Add(new Variant { id = id, weight = 1f, fallback = "none" });
        return n;
    }

    public static Scheme Find(string id)
    {
        foreach (var s in All) if (s.id == id) return s;
        return null;
    }
}
