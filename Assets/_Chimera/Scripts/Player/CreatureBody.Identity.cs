using UnityEngine;

// PARTIAL-SPLIT #1 (рефактор ядра): ВИДОВАЯ ИДЕНТИЧНОСТЬ вынесена сюда из CreatureBody.cs — тот же класс,
// НОЛЬ изменений поведения (partial делит все поля/методы; сериализация цела — поля по имени). Концерн:
// «кем тебя считают ПО СОСТАВУ тела» (доля вида в массе → Tier/Regard/однородность). Аффинити (родство,
// копится убийствами) — отдельный концерн, живёт пока в CreatureBody.cs (переплетён с пулом/слотами).
public partial class CreatureBody
{
    // ───────── ВИДОВАЯ ИДЕНТИЧНОСТЬ (K1, спека 2026-07-15): кем тебя считают ПО СОСТАВУ тела ─────────

    [SerializeField, Range(0f, 0.3f)] float chassisIdentityWeight = 0.1f; // вес шасси: чужое шасси капит признание
                                                                          // на «среднем» — встроенная цена химеризации
    [SerializeField] float weakAt = 0.65f;   // СЛАБОЕ признание = порог БЕЗОПАСНОСТИ («в основном свой», люфт на 1-2 примеси)
    [SerializeField] float mediumAt = 0.85f; // СРЕДНЕЕ = порог ВЛАСТИ (безупречный облик — чистый моно-сет; примесь сбрасывает)

    public SpeciesSO Chassis => chassis; // вид существа (для кин-проверок психик: «мой вид» = шасси)

    /// <summary>Идентичность 0..1 к виду = ДОЛЯ его частей в МАССЕ тела (перенормировка по составу —
    /// пользователь, 2026-07-18). Части весят в «юнитах» СВОЕГО вида: орган = 0.9/N (N — аугумент-пул
    /// вида, равные доли; chassisOnly — плоть шасси, живёт в его весе), шасси = 0.1. Правила из построения:
    /// «все аугументы вида = 90%» у любого вида (у змеи 5 органов по 18%, у волка 6 по 15%); ПРИМЕСЬ
    /// в химерном слоте РАЗБАВЛЯЕТ чужую чистоту (моно-волк 90% → +пит-орган → 76%: признание падает);
    /// Σ идентичностей = 100%. Родство НЕ входит (экономика ≠ удостоверение).</summary>
    public float Identity(SpeciesSO species)
    {
        if (species == null || slots == null || slots.Length == 0) return 0f;

        float mass = chassisIdentityWeight;                              // масса тела в юнитах (шасси всегда в ней)
        float mine = chassis == species ? chassisIdentityWeight : 0f;    // юниты искомого вида
        foreach (var sl in slots)
        {
            if (sl.Empty) continue;
            SpeciesSO owner;
            if (sl.Installed) owner = FindSpecies(sl.DonorSpecies);
            else if (!sl.Worn.chassisOnly) owner = chassis; // родная часть шасси (chassisOnly — плоть, живёт в весе шасси)
            else continue;
            if (owner == null) continue;

            float unit = (1f - chassisIdentityWeight) / AugPool(owner);
            mass += unit;
            if (owner == species) mine += unit;
        }
        return mass > 0f ? Mathf.Clamp01(mine / mass) : 0f;
    }

    /// <summary>ОДНОРОДНОСТЬ 0..1 — макс. идентичность среди присутствующих видов (насколько тело «чистое»).
    /// Чистый вид = 1, полная химера = меньше. Метаболизм сытости зависит от неё: чем разнороднее, тем
    /// быстрее голод (цена химеризации). Разнородность = 1 − однородность.</summary>
    public float Homogeneity
    {
        get
        {
            float max = chassis != null ? Identity(chassis) : 0f;
            if (donors != null) foreach (var d in donors) if (d != null) max = Mathf.Max(max, Identity(d));
            return max;
        }
    }

    // аугумент-пул вида: сколько его органов можно надеть (chassisOnly не в счёт)
    static int AugPool(SpeciesSO s)
    {
        int n = 0;
        if (s.organs != null) foreach (var o in s.organs) if (!o.chassisOnly) n++;
        return Mathf.Max(1, n);
    }

    // вид по имени среди известных телу (шасси + доноры)
    SpeciesSO FindSpecies(string name)
    {
        if (chassis != null && chassis.speciesName == name) return chassis;
        if (donors != null)
            foreach (var d in donors)
                if (d != null && d.speciesName == name) return d;
        return null;
    }

    /// <summary>Градация признания вида по ЭФФЕКТИВНОЙ идентичности (база − эрозия предательства): нет /
    /// слабое (≥weakAt) / среднее (≥mediumAt) / сильное (≈1). Кины судят игрока именно так — эрозия от
    /// ударов по своим просаживает признание, и «свой» становится чужим (Betrayal).</summary>
    public KinTier Tier(SpeciesSO species)
    {
        float k = Identity(species) - (betrayal != null ? betrayal.Erosion(species) : 0f);
        return k >= 0.999f ? KinTier.Strong : k >= mediumAt ? KinTier.Medium : k >= weakAt ? KinTier.Weak : KinTier.None;
    }

    /// <summary>ЕДИНЫЙ ГЛАГОЛ РОДСТВА: как `observer` признаёт существо `target` — по ДОМИНАНТЕ состава (MostKin). None, если один
    /// из тел отсутствует или у цели нет шасси. Заменяет дословный повтор `PlayerBody.Tier(body.Chassis)`,
    /// разбросанный по психикам (волк/лось) и голосам (вой/рёв) — теперь правило признания живёт ОДНИМ местом
    /// (задел под термо-поправку змеи: у неё «свой/добыча» по теплу — ляжет сюда же).</summary>
    public static KinTier Regard(CreatureBody observer, CreatureBody target)
    {
        if (observer == null || target == null) return KinTier.None;
        var dom = target.MostKin(out _);                        // идентичность цели = ДОМИНАНТА состава (не шасси): волк-по-идентичности для стаи волк
        return dom != null ? observer.Tier(dom) : KinTier.None; // истинная химера (dom==null) — никому не своя (согласуется с химерой-альфой)
    }

    /// <summary>Признаёт ли ЭТО тело (this) во ВСТРЕЧНОМ (other) СВОЕГО — тир-осознанно (мост тип-B → идентичность, #4a).
    /// Направление: `Regard(other, this)` = «сколько МОЕГО вида (this.Chassis) в other» = насколько other похож на меня.
    /// Порог Weak по умолчанию — внутривидовое узнавание ЛЕНИВО (свои читают тебя рано). У чистых видов: свой →
    /// Strong, чужой → None (любой порог даёт то же). Химере/химеризующемуся игроку даёт градиент по составу.
    /// Психики зовут это вместо `GetComponentInParent&lt;XxxPsyche&gt;()` в кин-местах.</summary>
    public bool IsKin(CreatureBody other, KinTier minTier = KinTier.Weak) => other != null && Regard(other, this) >= minTier;

    /// <summary>УДАР ПО ЦЕЛИ: если по составу это мой вид — подорвать признание своего вида в её глазах
    /// (стак эрозии, см. Betrayal). Гейт по СЫРОЙ идентичности (не эффективной): пока ты их вида —
    /// непрерывные удары держат признание просевшим; перестал бить — стаки гаснут, признание вернулось.
    /// Химера (ни к кому не кин) не копит ничего — своих у неё нет.</summary>
    public void NoteHit(Health other)
    {
        if (betrayal == null || other == null) return;
        var ob = other.GetComponent<CreatureBody>();
        var sp = ob != null ? ob.Chassis : null;
        if (sp != null && Identity(sp) >= weakAt) betrayal.Hit(sp); // копим только против видов, что признают по составу
    }

    /// <summary>КИН-ЦЕЛЬ: самый близкий признанный вид (для rally носителей K2/K3). null = ХИМЕРА —
    /// кин ни к кому: союзников нет, зато контроль вокализа ляжет на всех («монстр для всех»).</summary>
    public SpeciesSO MostKin(out KinTier tier)
    {
        SpeciesSO best = null; float bestK = 0f;
        void Consider(SpeciesSO s)
        {
            if (s == null) return;
            float k = Identity(s);
            if (k > bestK) { bestK = k; best = s; }
        }
        Consider(chassis);
        if (donors != null) foreach (var d in donors) Consider(d);
        tier = best != null ? Tier(best) : KinTier.None;
        return tier == KinTier.None ? null : best;
    }

    /// <summary>Дебаг-строка идентичностей по известным видам (HUD): «Волк 62% · Человек 28% (кин: —)».</summary>
    public string IdentityInfo
    {
        get
        {
            var sb = new System.Text.StringBuilder();
            void Row(SpeciesSO s)
            {
                if (s == null) return;
                if (sb.Length > 0) sb.Append(" · ");
                sb.Append($"{s.speciesName} {Identity(s) * 100f:0}%");
            }
            Row(chassis);
            if (donors != null) foreach (var d in donors) Row(d);
            var kin = MostKin(out var tier);
            string tierRu = tier == KinTier.Strong ? "сильное" : tier == KinTier.Medium ? "среднее" : tier == KinTier.Weak ? "слабое" : "—";
            sb.Append($"   (кин: {(kin != null ? kin.speciesName : "ХИМЕРА")}, признание {tierRu})");
            return sb.ToString();
        }
    }

    /// <summary>Цвет вида по имени — для UI (морф гнезда в цвет донора). Публичная обёртка над тинтом.</summary>
    public Color SpeciesColor(string species) => SpeciesTint(species);

    /// <summary>Идентичность к виду по ИМЕНИ (0..1) — для UI: яркость морфа = насколько ты этот вид.</summary>
    public float IdentityOf(string species)
    {
        var so = FindSpecies(species);
        return so != null ? Identity(so) : 0f;
    }

    // тинт вида по имени (шасси или донор) — для смеси палитры
    Color SpeciesTint(string species)
    {
        if (chassis != null && chassis.speciesName == species) return chassis.tint;
        if (donors != null)
            foreach (var d in donors)
                if (d != null && d.speciesName == species) return d.tint;
        return chassis != null ? chassis.tint : Color.gray;
    }
}

/// <summary>Градация ПРИЗНАНИЯ вида по идентичности состава (K1): пороги 75/90/100.</summary>
public enum KinTier { None, Weak, Medium, Strong }
