using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Конструктор. ШАССИ (SpeciesSO) задаёт слоты + пул + органы по умолчанию (человеческие);
/// ДОНОРЫ дают звериные альтернативы. Чистый человек = все слоты человеческими органами;
/// химеризация = в слоте заменить человеческий орган звериным (хоткеи 1–6).
/// Статы = сумма надетых органов по слотам (каждый стат «принадлежит» своему слоту).
/// Родство: фаза 1 (0–80) — скидка на цену; фаза 2 (80–100) — множитель «звериной» части
/// (насколько орган уходит от человеческого). Дальность из-под множителя исключена. Тинт по числу звериных слотов.
/// </summary>
[RequireComponent(typeof(PlayerAttack))]
[RequireComponent(typeof(PlayerController))]
[RequireComponent(typeof(Health))]
public class ChimeraBody : MonoBehaviour
{
    [Header("Виды")]
    [SerializeField] SpeciesSO chassis;    // базовое тело (MVP: Человек) — слоты, пул, дефолт-органы
    [SerializeField] SpeciesSO[] donors;   // доноры органов (MVP: [Волк])

    [Header("Родство — фаза 1: скидка (0…80)")]
    [SerializeField] float discountPerAffinity = 0.01f; // −1% к цене за единицу родства
    [SerializeField] float maxDiscount = 0.8f;          // потолок скидки на ~80 родства

    [Header("Родство — фаза 2: мощь (80…100)")]
    [SerializeField] float bonusStartAffinity = 80f;
    [SerializeField] float bonusFullAffinity = 100f;
    [SerializeField] float maxBonusMult = 2f;           // звериная часть органа ×2 на 100 родства

    class Slot
    {
        public string name, hotkey, donorSpecies;
        public Organ human;       // орган шасси (дефолт слота)
        public Organ beast;       // орган донора (альтернатива) или null
        public InputAction action;
        public bool installed;    // надет звериный?
    }

    Slot[] slots;
    PlayerAttack attack;
    PlayerController move;
    Health health;
    PlayerBite bite;
    Renderer[] renderers;
    Color[] baseColors;
    MaterialPropertyBlock mpb;
    int lastAffinitySum = -1;
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    public int Pool => chassis != null ? chassis.mutagenPool : 0;
    public int PoolUsed { get { int s = 0; if (slots != null) foreach (var sl in slots) s += SlotCost(sl); return s; } }
    int SlotCost(Slot sl) => sl.installed && sl.beast != null ? EffectiveCost(sl) : (sl.human != null ? sl.human.cost : 0); // каждый слот занимает пул (человеческий орган тоже)
    public int MaxSlots => slots != null ? slots.Length : 0;
    public int BeastSlots { get { int n = 0; if (slots != null) foreach (var sl in slots) if (sl.installed) n++; return n; } }
    public float BonusMult => donors != null && donors.Length > 0 && donors[0] != null ? BonusMultiplier(donors[0].speciesName) : 1f;

    public string SlotsInfo
    {
        get
        {
            if (slots == null) return "";
            var lines = new List<string>();
            foreach (var sl in slots)
            {
                string cur = sl.installed && sl.beast != null ? sl.beast.organName : sl.human.organName;
                lines.Add($"{sl.hotkey} {sl.name}: {cur} ({SlotCost(sl)}){(sl.installed ? "  ✓" : "")}");
            }
            return string.Join("\n", lines);
        }
    }

    // ── публичный слепок слота для UI ─────────────────────────────────────────
    public struct SlotView
    {
        public string slot, hotkey, organName, humanName, beastName;
        public int cost;
        public bool installed;  // надет звериный орган
        public bool hasBeast;   // есть ли звериная альтернатива (иначе слот фиксирован)
        public bool canToggle;  // можно ли сейчас переключить (снять — всегда; надеть — если влезает в пул)
    }

    public int SlotCount => slots != null ? slots.Length : 0;

    public SlotView GetSlot(int i)
    {
        var sl = slots[i];
        bool installed = sl.installed && sl.beast != null;
        return new SlotView
        {
            slot = sl.name,
            hotkey = sl.hotkey,
            organName = installed ? sl.beast.organName : sl.human.organName,
            humanName = sl.human != null ? sl.human.organName : null,
            beastName = sl.beast != null ? sl.beast.organName : null,
            cost = SlotCost(sl),
            installed = installed,
            hasBeast = sl.beast != null,
            canToggle = installed || CanInstall(sl),
        };
    }

    public void ToggleSlot(int i)
    {
        if (slots != null && i >= 0 && i < slots.Length) Toggle(slots[i]);
    }

    // влезает ли химеризация слота в пул: снимаем человеческий орган (вернуть цену), ставим звериный
    bool CanInstall(Slot sl)
    {
        if (sl.beast == null) return false;
        int humanCost = sl.human != null ? sl.human.cost : 0;
        return PoolUsed - humanCost + EffectiveCost(sl) <= Pool;
    }

    int EffectiveCost(Slot sl)
    {
        if (sl.beast == null) return 0;
        float discount = Mathf.Clamp(AffinityTracker.Get(sl.donorSpecies) * discountPerAffinity, 0f, maxDiscount);
        return Mathf.Max(1, Mathf.CeilToInt(sl.beast.cost * (1f - discount)));
    }

    // Фаза 2: множитель звериной части. ×1 до bonusStartAffinity, линейно до maxBonusMult к bonusFullAffinity.
    float BonusMultiplier(string species)
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

        BuildSlots();

        renderers = GetComponentsInChildren<Renderer>();
        baseColors = new Color[renderers.Length];
        mpb = new MaterialPropertyBlock();
        for (int i = 0; i < renderers.Length; i++)
        {
            var m = renderers[i].sharedMaterial;
            baseColors[i] = (m != null && m.HasProperty(BaseColor)) ? m.GetColor(BaseColor) : Color.gray;
        }
    }

    // Слоты собираем из органов шасси; к каждому подбираем звериную альтернативу из доноров по имени слота.
    void BuildSlots()
    {
        if (chassis == null || chassis.organs == null)
        {
            slots = new Slot[0];
            Debug.LogWarning("ChimeraBody: не назначено шасси (SpeciesSO). Конструктор спит — компоненты работают на своих значениях.");
            return;
        }

        var list = new List<Slot>();
        foreach (var h in chassis.organs)
        {
            var sl = new Slot { name = h.slot, human = h };
            if (donors != null)
                foreach (var d in donors)
                {
                    if (d == null || d.organs == null) continue;
                    foreach (var o in d.organs)
                        if (o.slot == h.slot) { sl.beast = o; sl.donorSpecies = d.speciesName; break; }
                    if (sl.beast != null) break;
                }

            sl.hotkey = sl.beast != null ? sl.beast.hotkey : h.hotkey;
            if (sl.beast != null && !string.IsNullOrEmpty(sl.hotkey))
            {
                sl.action = new InputAction(sl.name, InputActionType.Button);
                sl.action.AddBinding($"<Keyboard>/{sl.hotkey}");
            }
            list.Add(sl);
        }
        slots = list.ToArray();
    }

    void OnEnable() { if (slots != null) foreach (var sl in slots) sl.action?.Enable(); }
    void OnDisable() { if (slots != null) foreach (var sl in slots) sl.action?.Disable(); }

    void Start() => Recompute();

    void Update()
    {
        if (slots != null)
            foreach (var sl in slots)
                if (sl.action != null && sl.action.WasPressedThisFrame()) Toggle(sl);

        int affSum = AffinitySum();
        if (affSum != lastAffinitySum) { lastAffinitySum = affSum; Recompute(); } // родство выросло → пересчёт
    }

    int AffinitySum()
    {
        int s = 0;
        if (donors != null) foreach (var d in donors) if (d != null) s += AffinityTracker.Get(d.speciesName);
        return s;
    }

    void Toggle(Slot sl)
    {
        if (sl.beast == null) return;
        if (!sl.installed && !CanInstall(sl)) return; // размен не влезает в пул
        sl.installed = !sl.installed;
        Recompute();
    }

    void Recompute()
    {
        if (slots == null || slots.Length == 0) return; // нет данных — не трогаем статы компонентов

        int dmg = 0, maxHp = 0, life = 0, beast = 0;
        float rng = 0f, atkCd = 0f, mv = 0f, dash = 0f, dashCd = 0f, reduce = 0f, regen = 0f, regenOOC = 0f;
        bool biteOn = false, scentOn = false;

        foreach (var sl in slots)
        {
            Organ h = sl.human;
            if (sl.installed && sl.beast != null)
            {
                Organ b = sl.beast;
                float m = BonusMultiplier(sl.donorSpecies);
                dmg += Mathf.RoundToInt(Blend(h.damage, b.damage, m));
                maxHp += Mathf.RoundToInt(Blend(h.maxHp, b.maxHp, m));
                life += Mathf.RoundToInt(Blend(h.lifeSteal, b.lifeSteal, m));
                rng += b.range; // дальность не масштабируем — фикс. трейдофф
                atkCd += Blend(h.atkCooldown, b.atkCooldown, m);
                mv += Blend(h.moveSpeed, b.moveSpeed, m);
                dash += Blend(h.dashSpeed, b.dashSpeed, m);
                dashCd += Blend(h.dashCooldown, b.dashCooldown, m);
                reduce += Blend(h.damageReduction, b.damageReduction, m);
                regen += Blend(h.regen, b.regen, m);
                regenOOC += Blend(h.regenOOC, b.regenOOC, m);
                if (b.enablesBite) biteOn = true;
                if (b.enablesScent) scentOn = true;
                beast++;
            }
            else
            {
                dmg += h.damage; maxHp += h.maxHp; life += h.lifeSteal;
                rng += h.range; atkCd += h.atkCooldown; mv += h.moveSpeed; dash += h.dashSpeed;
                dashCd += h.dashCooldown; reduce += h.damageReduction; regen += h.regen; regenOOC += h.regenOOC;
                if (h.enablesBite) biteOn = true;
                if (h.enablesScent) scentOn = true;
            }
        }

        if (bite != null) bite.BiteEnabled = biteOn;
        Perception.WolfScent = scentOn; // слот «Чутьё»: видно запах волков
        attack.SetMelee(dmg, Mathf.Max(0.5f, rng));
        attack.SetCooldown(Mathf.Max(0.05f, atkCd));
        attack.SetLifeSteal(life);
        move.SetLegs(mv, dash);
        move.SetDashCooldown(Mathf.Max(0.05f, dashCd));
        health.SetMaxHealth(maxHp);
        health.DamageReduction = Mathf.Clamp01(reduce);
        health.RegenPerSecond = regen;
        health.OutOfCombatRegen = regenOOC;

        UpdateTint(beast);
    }

    // человеч.значение + (звериное − человеч.) × множитель: на ×1 = звериное, на ×2 = вдвое дальше от человека
    static float Blend(float human, float beast, float mult) => human + (beast - human) * mult;

    void UpdateTint(int beast)
    {
        Color target = donors != null && donors.Length > 0 && donors[0] != null ? donors[0].tint : Color.gray;
        float k = MaxSlots > 0 ? (float)beast / MaxSlots : 0f;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            renderers[i].GetPropertyBlock(mpb);
            mpb.SetColor(BaseColor, Color.Lerp(baseColors[i], target, k));
            renderers[i].SetPropertyBlock(mpb);
        }
    }
}
