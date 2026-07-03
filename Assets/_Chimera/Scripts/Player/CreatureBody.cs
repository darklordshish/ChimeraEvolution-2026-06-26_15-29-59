using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// ТЕЛО существа — общее для игрока и (в будущих фазах) NPC. ШАССИ (SpeciesSO) задаёт слоты + пул +
/// органы по умолчанию; ДОНОРЫ дают звериные альтернативы; химеризация = размен органа в слоте.
/// Статы = сумма надетых органов по слотам; раздаются ТЕМ компонентам, какие есть на объекте
/// (игрок: PlayerAttack/PlayerController/Health/PlayerBite; NPC-потребители придут в Ф4/5).
/// Ввод тело НЕ читает — переключением слотов рулит водитель (PlayerInputDriver / UI конструктора).
/// Родство: фаза 1 (0–80) — скидка на цену; фаза 2 (80–100) — множитель «звериной» части
/// (насколько орган уходит от человеческого). Дальность из-под множителя исключена. Тинт по числу звериных слотов.
/// </summary>
public class CreatureBody : MonoBehaviour
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

    [Header("NPC-режим (тело как данные)")]
    [SerializeField] bool installAllBeast;    // все звериные органы надеты с рождения (вервольф — застывшая химера)
    [FormerlySerializedAs("fixedBonusMult")]
    [SerializeField] float expression;        // ЭКСПРЕССИЯ: насколько раскрыты гены зверя. 0 = авто (кривая родства);
                                              // >0 фикс: вервольф 2 (= потолок игрока), природный волк ~0.45 (без сыворотки)
    [SerializeField] bool applyVitals = true; // false: HP/броня/реген — «конституция» психики, тело их не трогает

    class Slot
    {
        public string name, hotkey, donorSpecies;
        public Organ human;       // орган шасси (дефолт слота)
        public Organ beast;       // орган донора (альтернатива) или null
        public bool installed;    // надет звериный?
    }

    Slot[] slots;
    PlayerAttack attack;
    PlayerController move;
    Health health;
    PlayerBite bite;
    PlayerKick kick;
    PlayerHowl howl;
    SpawnVariance variance; // разброс особи: HP учитываем при раздаче витальности (иначе гонка Start'ов)
    ColdBlooded cold;       // холоднокровность (Сердце змеи) — компонент-маркер, вешаем/снимаем по сборке
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

    // ЭКСПРЕССИЯ звериной части: у игрока авто — ×1 до bonusStartAffinity, линейно до maxBonusMult
    // к bonusFullAffinity (мутаген раскрывает гены с родством). У NPC — фиксированная.
    float BonusMultiplier(string species)
    {
        if (expression > 0f) return expression;
        float span = Mathf.Max(1f, bonusFullAffinity - bonusStartAffinity);
        float t = Mathf.Clamp01((AffinityTracker.Get(species) - bonusStartAffinity) / span);
        return Mathf.Lerp(1f, maxBonusMult, t);
    }

    void Awake()
    {
        // тело не предполагает игрока: берём тех потребителей, какие есть на объекте
        TryGetComponent(out attack);
        TryGetComponent(out move);
        TryGetComponent(out health);
        TryGetComponent(out bite);
        TryGetComponent(out kick);
        TryGetComponent(out howl);
        TryGetComponent(out variance);
        TryGetComponent(out cold);

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
            Debug.LogWarning("CreatureBody: не назначено шасси (SpeciesSO). Конструктор спит — компоненты работают на своих значениях.");
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
            list.Add(sl);
        }
        slots = list.ToArray();

        if (installAllBeast) // застывшая химера (вервольф): весь звериный лоадаут надет с рождения
            foreach (var sl in slots) sl.installed = sl.beast != null;
    }

    void Start() => Recompute();

    void Update()
    {
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
        bool biteOn = false, scentOn = false, kickOn = false, howlOn = false, coldOn = false;

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
                regenOOC += b.regenOOC; // вне-боя реген не блендим — фича органа (как дальность): иначе на Э=2 уходит в минус
                if (b.enablesBite) biteOn = true;
                if (b.enablesScent) scentOn = true;
                if (b.enablesKick) kickOn = true;
                if (b.enablesHowl) howlOn = true;
                if (b.coldBlooded) coldOn = true;
                beast++;
            }
            else
            {
                // Природная особь (фикс. экспрессия): органы записаны в МУТАГЕННОЙ шкале — без сыворотки
                // раскрыты лишь на Э (волк ~0.45). У игрока (Э авто) человеческая база идёт как есть (e=1).
                // Времена (кулдауны) и дальность не скейлим — как и в химерном бленде.
                float e = expression > 0f ? expression : 1f;
                dmg += Mathf.RoundToInt(h.damage * e); maxHp += Mathf.RoundToInt(h.maxHp * e); life += Mathf.RoundToInt(h.lifeSteal * e);
                rng += h.range; atkCd += h.atkCooldown; mv += h.moveSpeed * e; dash += h.dashSpeed * e;
                dashCd += h.dashCooldown; reduce += h.damageReduction * e; regen += h.regen * e; regenOOC += h.regenOOC * e;
                if (h.enablesBite) biteOn = true;
                if (h.enablesScent) scentOn = true;
                if (h.enablesKick) kickOn = true;
                if (h.enablesHowl) howlOn = true;
                if (h.coldBlooded) coldOn = true;
            }
        }

        if (bite != null) bite.BiteEnabled = biteOn;
        if (kick != null) kick.KickEnabled = kickOn; // пинок — фича человеческих ног: с волчьими пропадает
        if (howl != null) howl.HowlEnabled = howlOn; // вой-стан — фича волчьей Пасти
        SetColdBlooded(coldOn); // холоднокровность (Сердце змеи): невидимость для термозрения врагов
        if (move != null) Perception.WolfScent = scentOn; // чутьё игрока меняет ТОЛЬКО тело игрока (NPC-тело не должно включать игроку запах)
        if (attack != null)
        {
            attack.SetMelee(dmg, Mathf.Max(0.5f, rng));
            attack.SetCooldown(Mathf.Max(0.05f, atkCd));
            attack.SetLifeSteal(life);
        }
        if (move != null)
        {
            move.SetLegs(mv, dash);
            move.SetDashCooldown(Mathf.Max(0.05f, dashCd));
        }
        if (health != null && applyVitals) // у босса витальность — «конституция» психики
        {
            health.SetMaxHealth(Mathf.Max(1, Mathf.RoundToInt(maxHp * (variance != null ? variance.HpMult : 1f))));
            health.DamageReduction = Mathf.Clamp01(reduce);
            health.RegenPerSecond = regen;
            health.OutOfCombatRegen = regenOOC;
        } // maxHp здесь уже с разбросом особи (SpawnVariance.HpMult)

        // НПС-потребители (психика): тело отдаёт деривированное — урон и скорость из органов
        foreach (var c in GetComponents<IBodyStatConsumer>()) c.OnBodyStats(dmg, mv);

        if (!installAllBeast) UpdateTint(beast); // застывшая химера красится своим материалом, не тинтом
    }

    // холоднокровность как компонент-маркер: вешаем/снимаем по итогу сборки (живо на смене Сердца у игрока)
    void SetColdBlooded(bool on)
    {
        if (on && cold == null) cold = gameObject.AddComponent<ColdBlooded>();
        else if (!on && cold != null) { Destroy(cold); cold = null; }
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

/// <summary>
/// НПС-потребитель статов тела: психика получает деривированное из органов (урон, скорость)
/// и раздаёт своим доставкам. Витальность (HP/броня/реген) — отдельно («конституция», applyVitals).
/// </summary>
public interface IBodyStatConsumer
{
    void OnBodyStats(int damage, float moveSpeed);
}
