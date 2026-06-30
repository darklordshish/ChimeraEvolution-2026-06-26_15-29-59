using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Конструктор (срез) — data-driven аугументы, 6 слотов MVP (хоткеи 1–6).
/// Экономика по GDD: ПУЛ мутагена = бюджет (ставить можно, пока сумма цен надетых ≤ пул);
/// РОДСТВО = скидка на цену (больше родства-вида → дешевле его органы → влезает больше зверя).
/// Тинт тела растёт по числу звериных слотов («шкала мозга»). Дальше: нормальный UI, лаборатория-зона.
/// </summary>
[RequireComponent(typeof(PlayerAttack))]
[RequireComponent(typeof(PlayerController))]
[RequireComponent(typeof(Health))]
public class ChimeraBody : MonoBehaviour
{
    [SerializeField] string species = "Волк";
    [SerializeField] Color wolfTint = new Color(0.5f, 0.38f, 0.36f); // цвет при полном озверении

    [Header("Экономика — фаза 1: скидка (0…80 родства)")]
    [SerializeField] int mutagenPool = 10;              // бюджет очков
    [SerializeField] float discountPerAffinity = 0.01f; // −1% к цене за единицу родства (потолок к ~80 родства)
    [SerializeField] float maxDiscount = 0.8f;          // при 80 родства все 6 органов влезают (~7/10)

    [Header("Экономика — фаза 2: мощь (80…100 родства)")]
    [SerializeField] float bonusStartAffinity = 80f;    // ниже — бонус ×1; здесь скидка уже на капе
    [SerializeField] float bonusFullAffinity = 100f;    // полное родство — бонус на максимуме
    [SerializeField] float maxBonusMult = 2f;           // эффекты органов ×2 на 100 родства (×1.5 на 90)

    [Header("База (человек)")]
    [SerializeField] int baseDamage = 10;
    [SerializeField] float baseRange = 1.6f;
    [SerializeField] float baseAtkCooldown = 0.45f;
    [SerializeField] float baseMoveSpeed = 6f;
    [SerializeField] float baseDashSpeed = 20f;
    [SerializeField] float baseDashCooldown = 0.7f;
    [SerializeField] int baseMaxHp = 100;
    [SerializeField] float baseRegenOOC = 1f;   // человек: медленный реген вне боя (1/с)

    class Augment
    {
        public string name, key;
        public int cost; // базовая цена в пуле
        public int dDamage, dMaxHp, dLifeSteal;
        public float dRange, dAtkCd, dMove, dDash, dDashCd, dReduce, dRegen;
        public bool enablesBite;
        public InputAction action;
        public bool equipped;
    }

    Augment[] augments;
    PlayerAttack attack;
    PlayerController move;
    Health health;
    PlayerBite bite;
    Renderer[] renderers;
    Color[] baseColors;
    MaterialPropertyBlock mpb;
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    public int Pool => mutagenPool;
    public int PoolUsed { get { int s = 0; if (augments != null) foreach (var a in augments) if (a.equipped) s += EffectiveCost(a); return s; } }
    public int MaxSlots => augments != null ? augments.Length : 0;
    public int BeastSlots { get { int n = 0; if (augments != null) foreach (var a in augments) if (a.equipped) n++; return n; } }

    public string SlotsInfo
    {
        get
        {
            if (augments == null) return "";
            var lines = new List<string>();
            foreach (var a in augments)
                lines.Add($"{a.key} {a.name} — {EffectiveCost(a)}{(a.equipped ? "  ✓" : "")}");
            return string.Join("\n", lines);
        }
    }

    int EffectiveCost(Augment a)
    {
        float discount = Mathf.Clamp(AffinityTracker.Get(species) * discountPerAffinity, 0f, maxDiscount);
        return Mathf.Max(1, Mathf.CeilToInt(a.cost * (1f - discount)));
    }

    // Фаза 2: множитель эффектов органов. ×1 до bonusStartAffinity, линейно до maxBonusMult к bonusFullAffinity.
    public float BonusMult => BonusMultiplier();
    float BonusMultiplier()
    {
        float span = Mathf.Max(1f, bonusFullAffinity - bonusStartAffinity);
        float t = Mathf.Clamp01((AffinityTracker.Get(species) - bonusStartAffinity) / span);
        return Mathf.Lerp(1f, maxBonusMult, t);
    }

    void Awake()
    {
        attack = GetComponent<PlayerAttack>();
        move = GetComponent<PlayerController>();
        health = GetComponent<Health>();
        bite = GetComponent<PlayerBite>();

        augments = new[]
        {
            new Augment { name = "Когти",  key = "1", cost = 4, dDamage = 8, dRange = -0.1f },
            new Augment { name = "Ноги",   key = "2", cost = 4, dMove = 3f, dDash = 10f },
            new Augment { name = "Сердце", key = "3", cost = 6, dAtkCd = -0.15f, dMaxHp = 50, dRegen = 2f },
            new Augment { name = "Чутьё",  key = "4", cost = 3, dDashCd = -0.25f },
            new Augment { name = "Пасть",  key = "5", cost = 5, enablesBite = true },
            new Augment { name = "Шкура",  key = "6", cost = 4, dReduce = 0.3f },
        };
        foreach (var a in augments)
        {
            a.action = new InputAction(a.name, InputActionType.Button);
            a.action.AddBinding($"<Keyboard>/{a.key}");
        }

        renderers = GetComponentsInChildren<Renderer>();
        baseColors = new Color[renderers.Length];
        mpb = new MaterialPropertyBlock();
        for (int i = 0; i < renderers.Length; i++)
        {
            var m = renderers[i].sharedMaterial;
            baseColors[i] = (m != null && m.HasProperty(BaseColor)) ? m.GetColor(BaseColor) : Color.gray;
        }
    }

    void OnEnable() { foreach (var a in augments) a.action.Enable(); }
    void OnDisable() { foreach (var a in augments) a.action.Disable(); }

    void Start() => Recompute(); // выставить базу (человек)

    int lastAffinity = -1;

    void Update()
    {
        foreach (var a in augments)
            if (a.action.WasPressedThisFrame()) Toggle(a);

        // родство выросло (убил волка) → пересчитать скидку/множитель бонусов
        int aff = AffinityTracker.Get(species);
        if (aff != lastAffinity) { lastAffinity = aff; Recompute(); }
    }

    void Toggle(Augment a)
    {
        if (!a.equipped)
        {
            int cost = EffectiveCost(a);
            if (PoolUsed + cost > mutagenPool)
            {
                Debug.Log($"Не хватает пула для «{a.name}»: нужно {cost}, свободно {mutagenPool - PoolUsed}");
                return;
            }
        }
        a.equipped = !a.equipped;
        Debug.Log((a.equipped ? "Поставлен орган: " : "Снят орган: ") + a.name);
        Recompute();
    }

    void Recompute()
    {
        float mult = BonusMultiplier(); // родство 80→100 усиливает эффекты органов (×1 … ×2; на 90 ×1.5)

        int dmg = baseDamage, maxHp = baseMaxHp, life = 0, beast = 0;
        float rng = baseRange, atkCd = baseAtkCooldown, mv = baseMoveSpeed, dash = baseDashSpeed, dashCd = baseDashCooldown, reduce = 0f, regen = 0f;
        bool biteOn = false;

        foreach (var a in augments)
        {
            if (!a.equipped) continue;
            dmg += Mathf.RoundToInt(a.dDamage * mult); maxHp += Mathf.RoundToInt(a.dMaxHp * mult); life += Mathf.RoundToInt(a.dLifeSteal * mult);
            rng += a.dRange; // дальность не масштабируем — остаётся фикс. трейдофф когтя
            atkCd += a.dAtkCd * mult; mv += a.dMove * mult; dash += a.dDash * mult; dashCd += a.dDashCd * mult; reduce += a.dReduce * mult; regen += a.dRegen * mult;
            if (a.enablesBite) biteOn = true;
            beast++;
        }

        if (bite != null) bite.BiteEnabled = biteOn;

        attack.SetMelee(dmg, Mathf.Max(0.5f, rng));
        attack.SetCooldown(Mathf.Max(0.05f, atkCd));
        attack.SetLifeSteal(life);
        move.SetLegs(mv, dash);
        move.SetDashCooldown(Mathf.Max(0.05f, dashCd));
        health.SetMaxHealth(maxHp);
        health.DamageReduction = Mathf.Clamp01(reduce);
        health.RegenPerSecond = regen;         // слот «Сердце»: реген всегда (в т.ч. в бою)
        health.OutOfCombatRegen = baseRegenOOC; // человек: реген вне боя

        UpdateTint(beast);
    }

    void UpdateTint(int beast)
    {
        float k = MaxSlots > 0 ? (float)beast / MaxSlots : 0f;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            renderers[i].GetPropertyBlock(mpb);
            mpb.SetColor(BaseColor, Color.Lerp(baseColors[i], wolfTint, k));
            renderers[i].SetPropertyBlock(mpb);
        }
    }
}
