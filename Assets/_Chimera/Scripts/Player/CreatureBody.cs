using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// ТЕЛО существа — общее для игрока и NPC. ШАССИ (SpeciesSO) задаёт слоты + пул + органы по умолчанию;
/// ДОНОРЫ дают альтернативы: слот ЦИКЛИРУЕТ по вариантам всех доноров (человек → волчий → змеиный → человек).
/// Статы = сумма надетых органов по слотам; урон Пасти уходит в укус (PlayerBite), не в оружие Рук.
/// Раздаются ТЕМ компонентам, какие есть на объекте (игрок: PlayerAttack/PlayerController/Health/PlayerBite).
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

    // вариант органа для слота: орган конкретного донора (мультидонор: волчий ИЛИ змеиный на один слот)
    class Variant { public Organ organ; public string species; }

    class Slot
    {
        public string name, hotkey;                     // хоткей задаёт ШАССИ — раскладка стабильна при любых донорах
        public Organ human;                             // орган шасси (дефолт слота)
        public readonly List<Variant> variants = new(); // звериные альтернативы по всем донорам
        public int current = -1;                        // -1 = человеческий; иначе индекс в variants

        public bool Installed => current >= 0;
        public Organ Beast => current >= 0 ? variants[current].organ : null;
        public string DonorSpecies => current >= 0 ? variants[current].species : null;
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
    Camouflage camoComp;    // камуфляж-в-неподвижности (Чешуя змеи) — вешаем/снимаем по сборке
    Renderer[] renderers;
    Color[] baseColors;
    MaterialPropertyBlock mpb;
    int lastAffinitySum = -1;
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    public int Pool => chassis != null ? chassis.mutagenPool : 0;
    public int PoolUsed { get { int s = 0; if (slots != null) foreach (var sl in slots) s += SlotCost(sl); return s; } }
    int SlotCost(Slot sl) => sl.Installed ? EffectiveCost(sl.Beast, sl.DonorSpecies) : (sl.human != null ? sl.human.cost : 0); // каждый слот занимает пул (человеческий орган тоже)
    public int MaxSlots => slots != null ? slots.Length : 0;
    public int BeastSlots { get { int n = 0; if (slots != null) foreach (var sl in slots) if (sl.Installed) n++; return n; } }
    public float BonusMult => donors != null && donors.Length > 0 && donors[0] != null ? BonusMultiplier(donors[0].speciesName) : 1f;

    public string SlotsInfo
    {
        get
        {
            if (slots == null) return "";
            var lines = new List<string>();
            foreach (var sl in slots)
            {
                string cur = sl.Installed ? sl.Beast.organName : sl.human.organName;
                lines.Add($"{sl.hotkey} {sl.name}: {cur} ({SlotCost(sl)}){(sl.Installed ? "  ✓" : "")}");
            }
            return string.Join("\n", lines);
        }
    }

    // ── публичный слепок слота для UI ─────────────────────────────────────────
    public struct SlotView
    {
        public string slot, hotkey, organName, nextName; // nextName — куда приведёт следующий клик (цикл по донорам)
        public int cost, nextCost;
        public bool installed;  // надет звериный (не человеческий) орган
        public bool hasBeast;   // есть ли альтернативы (иначе слот фиксирован)
        public bool canToggle;  // есть ли достижимый следующий шаг цикла (иначе всё не по карману)
    }

    public int SlotCount => slots != null ? slots.Length : 0;

    public SlotView GetSlot(int i)
    {
        var sl = slots[i];
        int next = NextStep(sl);
        return new SlotView
        {
            slot = sl.name,
            hotkey = sl.hotkey,
            organName = sl.Installed ? sl.Beast.organName : sl.human.organName,
            cost = SlotCost(sl),
            installed = sl.Installed,
            hasBeast = sl.variants.Count > 0,
            canToggle = sl.variants.Count > 0 && next != sl.current,
            nextName = next >= 0 ? sl.variants[next].organ.organName : (sl.human != null ? sl.human.organName : null),
            nextCost = next >= 0 ? EffectiveCost(sl.variants[next].organ, sl.variants[next].species) : (sl.human != null ? sl.human.cost : 0),
        };
    }

    public void ToggleSlot(int i)
    {
        if (slots != null && i >= 0 && i < slots.Length) Toggle(slots[i]);
    }

    // влезает ли вариант в пул: снимаем текущий орган слота (вернуть его цену), ставим вариант idx
    bool CanInstall(Slot sl, int idx)
    {
        var v = sl.variants[idx];
        return PoolUsed - SlotCost(sl) + EffectiveCost(v.organ, v.species) <= Pool;
    }

    // цена органа со скидкой от родства ЕГО вида (волчьи дешевеют от родства-волк, змеиные — от родства-змея)
    int EffectiveCost(Organ organ, string species)
    {
        if (organ == null) return 0;
        float discount = Mathf.Clamp(AffinityTracker.Get(species) * discountPerAffinity, 0f, maxDiscount);
        return Mathf.Max(1, Mathf.CeilToInt(organ.cost * (1f - discount)));
    }

    // следующий достижимый шаг цикла слота: человек → вариант0 → вариант1 → … → человек;
    // не влезающие в пул варианты пропускаются (снятие в человеческий доступно всегда)
    int NextStep(Slot sl)
    {
        int n = sl.variants.Count;
        int idx = sl.current;
        for (int step = 0; step <= n; step++)
        {
            idx = idx + 1 >= n ? -1 : idx + 1;
            if (idx == -1 || CanInstall(sl, idx)) return idx;
        }
        return sl.current;
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
        TryGetComponent(out camoComp);

        BuildSlots();

        // РОДСТВО: NPC (не игрок) на смерть даёт +1 за каждый УНИКАЛЬНЫЙ видо-флаг тела (шасси + доноры с органами)
        if (health != null && move == null) health.onDeath.AddListener(GrantAffinityOnDeath);

        // игрок: родство с СОБСТВЕННЫМ шасси полное с рождения (мы и есть человек). Мета-шкала
        // человечности (падение/возврат через социальность) — потом, когда появятся люди.
        if (move != null && chassis != null) AffinityTracker.Set(chassis.speciesName, AffinityTracker.Cap);

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
            var sl = new Slot { name = h.slot, human = h, hotkey = h.hotkey }; // раскладку задаёт шасси
            if (donors != null)
                foreach (var d in donors)
                {
                    if (d == null || d.organs == null) continue;
                    foreach (var o in d.organs)
                        if (o.slot == h.slot) sl.variants.Add(new Variant { organ = o, species = d.speciesName }); // ВСЕ доноры (мультидонор)
                }
            list.Add(sl);
        }
        slots = list.ToArray();

        if (installAllBeast) // застывшая химера (вервольф): весь лоадаут ПЕРВОГО донора надет с рождения
            foreach (var sl in slots) sl.current = sl.variants.Count > 0 ? 0 : -1;
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

    // РОДСТВО на смерть (только NPC): +1 за каждый УНИКАЛЬНЫЙ видо-флаг тела — шасси + каждый вид-донор с ≥1 надетым
    // органом. Волк→+1 Волк; змея→+1 Змея; вервольф (человек+волчьи ауги)→+1 Человек, +1 Волк. Дубли по виду не растят.
    void GrantAffinityOnDeath()
    {
        var present = new HashSet<string>();
        if (chassis != null) present.Add(chassis.speciesName);
        if (slots != null)
            foreach (var sl in slots)
                if (sl.Installed && sl.DonorSpecies != null) present.Add(sl.DonorSpecies);
        foreach (var species in present) AffinityTracker.Add(species, 1);
    }

    // цикл слота: человеческий → варианты доноров по кругу → человеческий (не по карману — пропускаются)
    void Toggle(Slot sl)
    {
        if (sl.variants.Count == 0) return;
        int next = NextStep(sl);
        if (next == sl.current) return; // некуда шагнуть (все варианты не влезают в пул)
        sl.current = next;
        Recompute();
    }

    void Recompute()
    {
        if (slots == null || slots.Length == 0) return; // нет данных — не трогаем статы компонентов

        int dmg = 0, dmgBite = 0, maxHp = 0, life = 0, venom = 0, beast = 0;
        float rng = 0f, atkCd = 0f, mv = 0f, dash = 0f, dashCd = 0f, reduce = 0f, regen = 0f, regenOOC = 0f, thermal = 0f;
        bool biteOn = false, scentOn = false, kickOn = false, howlOn = false, coldOn = false, camoOn = false, thermalOn = false;

        foreach (var sl in slots)
        {
            Organ h = sl.human;
            bool maw = sl.name == "Пасть"; // урон Пасти принадлежит УКУСУ, не мечу — клыки не усиливают оружие Рук
            if (sl.Installed)
            {
                Organ b = sl.Beast;
                float m = BonusMultiplier(sl.DonorSpecies);
                int d = Mathf.RoundToInt(Blend(h.damage, b.damage, m));
                if (maw) dmgBite += d; else dmg += d;
                maxHp += Mathf.RoundToInt(Blend(h.maxHp, b.maxHp, m));
                life += Mathf.RoundToInt(Blend(h.lifeSteal, b.lifeSteal, m));
                venom += b.venomStacks; // дискретная фича органа (как флаги) — не блендим
                rng += b.range; // дальность не масштабируем — фикс. трейдофф
                atkCd += Blend(h.atkCooldown, b.atkCooldown, m);
                mv += Blend(h.moveSpeed, b.moveSpeed, m);
                dash += Blend(h.dashSpeed, b.dashSpeed, m);
                dashCd += Blend(h.dashCooldown, b.dashCooldown, m);
                reduce += Blend(h.damageReduction, b.damageReduction, m);
                regen += Blend(h.regen, b.regen, m);
                regenOOC += b.regenOOC; // вне-боя реген не блендим — фича органа (как дальность): иначе на Э=2 уходит в минус
                thermal += b.thermalRange; // фикс-фича органа (как range) — не блендим
                if (b.enablesBite) biteOn = true;
                if (b.enablesScent) scentOn = true;
                if (b.enablesKick) kickOn = true;
                if (b.enablesHowl) howlOn = true;
                if (b.coldBlooded) coldOn = true;
                if (b.camo) camoOn = true;
                if (b.enablesThermal) thermalOn = true;
                beast++;
            }
            else
            {
                // Природная особь (фикс. экспрессия): органы записаны в МУТАГЕННОЙ шкале — без сыворотки
                // раскрыты лишь на Э (волк ~0.45). У игрока (Э авто) человеческая база идёт как есть (e=1).
                // Времена (кулдауны) и дальность не скейлим — как и в химерном бленде.
                float e = expression > 0f ? expression : 1f;
                int d = Mathf.RoundToInt(h.damage * e);
                if (maw) dmgBite += d; else dmg += d;
                maxHp += Mathf.RoundToInt(h.maxHp * e); life += Mathf.RoundToInt(h.lifeSteal * e);
                venom += h.venomStacks;
                rng += h.range; atkCd += h.atkCooldown; mv += h.moveSpeed * e; dash += h.dashSpeed * e;
                dashCd += h.dashCooldown; reduce += h.damageReduction * e; regen += h.regen * e; regenOOC += h.regenOOC * e;
                thermal += h.thermalRange;
                if (h.enablesBite) biteOn = true;
                if (h.enablesScent) scentOn = true;
                if (h.enablesKick) kickOn = true;
                if (h.enablesHowl) howlOn = true;
                if (h.coldBlooded) coldOn = true;
                if (h.camo) camoOn = true;
                if (h.enablesThermal) thermalOn = true;
            }
        }

        if (bite != null)
        {
            bite.BiteEnabled = biteOn;
            bite.SetDamage(dmgBite); // 0 = органы молчат → PlayerBite остаётся на своём дефолте
            bite.SetVenom(venom);    // яд змеиных клыков на укусе игрока
        }
        if (kick != null) kick.KickEnabled = kickOn; // пинок — фича человеческих ног: с волчьими пропадает
        if (howl != null) howl.HowlEnabled = howlOn; // вой-стан — фича волчьей Пасти
        SetColdBlooded(coldOn); // холоднокровность (Сердце змеи): невидимость для термозрения врагов
        SetCamouflage(camoOn);  // камуфляж (Чешуя змеи): невидимость в неподвижности
        if (move != null) // чувства игрока меняет ТОЛЬКО тело игрока (NPC-тело не должно включать их игроку)
        {
            Perception.WolfScent = scentOn;
            Perception.SnakeThermal = thermalOn; // термозрение (Пит-орган): тепло сквозь стены
            Perception.ThermalRange = thermal;
        }
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

        // НПС-потребители (психика): тело отдаёт деривированное — урон (суммарный: их мили и есть укус) и скорость
        foreach (var c in GetComponents<IBodyStatConsumer>()) c.OnBodyStats(dmg + dmgBite, mv);

        if (!installAllBeast) UpdateTint(beast); // застывшая химера красится своим материалом, не тинтом
    }

    // холоднокровность как компонент-маркер: вешаем/снимаем по итогу сборки (живо на смене Сердца у игрока)
    void SetColdBlooded(bool on)
    {
        if (on && cold == null) cold = gameObject.AddComponent<ColdBlooded>();
        else if (!on && cold != null) { Destroy(cold); cold = null; }
    }

    // камуфляж-в-неподвижности как компонент: вешаем/снимаем по итогу сборки (живо на смене Шкуры у игрока)
    void SetCamouflage(bool on)
    {
        if (on && camoComp == null) camoComp = gameObject.AddComponent<Camouflage>();
        else if (!on && camoComp != null) { Destroy(camoComp); camoComp = null; }
    }

    // человеч.значение + (звериное − человеч.) × множитель: на ×1 = звериное, на ×2 = вдвое дальше от человека
    static float Blend(float human, float beast, float mult) => human + (beast - human) * mult;

    void UpdateTint(int beast)
    {
        // тинт — динамика ИГРОКА (лерп к тинту донора по числу звериных слотов). У NPC доноров нет → k=0,
        // цвет берётся из ЗАПЕЧЁННОГО материала префаба (генератор красит в тинт вида) — чтобы не драться с Telegraph.
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
