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

    [Header("Химерные слоты (мета: награда суперхимеры)")]
    [SerializeField, Min(0)] int chimeraSlots;    // выданные универсальные слоты (dev-кнопка / SuperBossReward)
    [SerializeField] float chimeraSlotMult = 2f;  // множитель цены органа в химерном слоте (не-нативный «графт»)

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
        public Organ human;                             // орган шасси (дефолт слота); у ХИМЕРНОГО слота = null (пусто)
        public bool chimera;                            // универсальный доп-слот: любой донорский орган, цена ×chimeraSlotMult
        public readonly List<Variant> variants = new(); // звериные альтернативы по всем донорам
        public int current = -1;                        // -1 = человеческий/пусто; иначе индекс в variants

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
    PlayerConstrict constrictAb;
    SpawnVariance variance; // разброс особи: HP учитываем при раздаче витальности (иначе гонка Start'ов)
    ColdBlooded cold;       // холоднокровность (Сердце змеи) — компонент-маркер, вешаем/снимаем по сборке
    Camouflage camoComp;    // камуфляж-в-неподвижности (Чешуя змеи) — вешаем/снимаем по сборке
    Digestion digestComp;   // переваривание (Тело-хвост змеи, chassisOnly) — вешаем/снимаем по сборке
    Renderer[] renderers;
    MaterialPropertyBlock mpb;
    int lastAffinitySum = -1;
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    public const int AffinityCap = 100; // потолок родства на вид (дальше некуда: скидка и мощь на полке)

    // РОДСТВО — ЛОКАЛЬНОЕ, в теле КАЖДОГО существа (не в глобальном трекере: «ЦНС» игры не грузим).
    // Все звери мира — эксперименты с сывороткой (или съевшие их) → родство = база любого тела.
    // Задел эволюции химер-NPC: змея, съевшая волков, копит родство-волк (пока ничего с ним не делает).
    readonly Dictionary<string, int> affinity = new();

    public static CreatureBody PlayerBody { get; private set; } // тело ИГРОКА: HUD/dev/спавнеры читают его родство

    public int GetAffinity(string species) { affinity.TryGetValue(species, out int v); return v; }
    public void AddAffinity(string species, int n) => affinity[species] = Mathf.Clamp(GetAffinity(species) + n, 0, AffinityCap);
    public void SetAffinity(string species, int v) => affinity[species] = Mathf.Clamp(v, 0, AffinityCap);
    public IEnumerable<KeyValuePair<string, int>> AllAffinity => affinity;

    int poolBonus; // расширение пула наградами (SuperBossReward) — рантайм-бонус, ассет шасси не трогаем

    public int Pool => (chassis != null ? chassis.mutagenPool : 0) + poolBonus;

    /// <summary>Расширить пул мутагена (награда суперхимеры). Живо в рантайме.</summary>
    public void ExpandPool(int n) { poolBonus += n; Recompute(); }
    public int PoolUsed { get { int s = 0; if (slots != null) foreach (var sl in slots) s += SlotCost(sl); return s; } }
    int SlotCost(Slot sl) => sl.Installed ? CostOf(sl, sl.variants[sl.current]) : (sl.human != null ? sl.human.cost : 0); // каждый слот занимает пул (человеческий орган тоже; пустой химерный = 0)
    int CostOf(Slot sl, Variant v) => Mathf.CeilToInt(EffectiveCost(v.organ, v.species) * (sl.chimera ? chimeraSlotMult : 1f)); // химерный слот — дорогой «графт»
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
                string cur = sl.Installed ? sl.Beast.organName : (sl.human != null ? sl.human.organName : "—"); // пустой химерный
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
        public int unaffordable; // сколько вариантов скрыто по цене (цикл их пропускает МОЛЧА — UI должен сказать)
        public bool installed;  // надет звериный (не человеческий) орган
        public bool hasBeast;   // есть ли альтернативы (иначе слот фиксирован)
        public bool canToggle;  // есть ли достижимый следующий шаг цикла (иначе всё не по карману)
    }

    public int SlotCount => slots != null ? slots.Length : 0;

    public SlotView GetSlot(int i)
    {
        var sl = slots[i];
        int next = NextStep(sl);
        int unaffordable = 0;
        for (int v = 0; v < sl.variants.Count; v++)
            if (v != sl.current && !CanInstall(sl, v)) unaffordable++;
        return new SlotView
        {
            unaffordable = unaffordable,
            slot = sl.name,
            hotkey = sl.hotkey,
            organName = sl.Installed ? sl.Beast.organName : (sl.human != null ? sl.human.organName : "—"), // пустой химерный слот
            cost = SlotCost(sl),
            installed = sl.Installed,
            hasBeast = sl.variants.Count > 0,
            canToggle = sl.variants.Count > 0 && next != sl.current,
            nextName = next >= 0 ? sl.variants[next].organ.organName : (sl.human != null ? sl.human.organName : "—"),
            nextCost = next >= 0 ? CostOf(sl, sl.variants[next]) : (sl.human != null ? sl.human.cost : 0),
        };
    }

    /// <summary>Выдать универсальный ХИМЕРНЫЙ слот (награда суперхимеры / dev). Живо в рантайме.</summary>
    public void GrantChimeraSlot()
    {
        chimeraSlots++;
        var list = new List<Slot>(slots) { MakeChimeraSlot(slots.Length) };
        slots = list.ToArray();
        Recompute();
    }

    public int ChimeraSlots => chimeraSlots;

    /// <summary>Убрать последний химерный слот (dev). Надетый в нём орган снимается, пул вернётся сам.</summary>
    public void RemoveChimeraSlot()
    {
        if (chimeraSlots <= 0) return;
        chimeraSlots--;
        var list = new List<Slot>(slots);
        for (int i = list.Count - 1; i >= 0; i--)
            if (list[i].chimera) { list.RemoveAt(i); break; }
        slots = list.ToArray();
        Recompute();
    }

    // химерный слот: без человеческой базы, принимает ЛЮБОЙ орган ЛЮБОГО донора (в т.ч. дубль занятого слота)
    Slot MakeChimeraSlot(int index)
    {
        var sl = new Slot { name = "Химерный", hotkey = (index + 1).ToString(), chimera = true };
        if (donors != null)
            foreach (var d in donors)
            {
                if (d == null || d.organs == null) continue;
                foreach (var o in d.organs)
                    if (!o.chassisOnly) sl.variants.Add(new Variant { organ = o, species = d.speciesName }); // локомоция шасси не аугумент
            }
        return sl;
    }

    public void ToggleSlot(int i)
    {
        if (slots != null && i >= 0 && i < slots.Length) Toggle(slots[i]);
    }

    // влезает ли вариант в пул: снимаем текущий орган слота (вернуть его цену), ставим вариант idx
    bool CanInstall(Slot sl, int idx)
    {
        return PoolUsed - SlotCost(sl) + CostOf(sl, sl.variants[idx]) <= Pool;
    }

    // цена органа со скидкой от НАШЕГО родства с ЕГО видом (родство теперь локальное — у каждого тела своё)
    int EffectiveCost(Organ organ, string species)
    {
        if (organ == null) return 0;
        float discount = Mathf.Clamp(GetAffinity(species) * discountPerAffinity, 0f, maxDiscount);
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
        float t = Mathf.Clamp01((GetAffinity(species) - bonusStartAffinity) / span);
        return Mathf.Lerp(1f, maxBonusMult, t);
    }

    void Awake()
    {
        // тело не предполагает игрока: берём тех потребителей, какие есть на объекте
        TryGetComponent(out attack);
        TryGetComponent(out move);
        // авто-РАЗБРОС ПОВЕДЕНИЯ: любой NPC-вид (не игрок) получает Личность от ТЕЛА — психики её только ЧИТАЮТ
        // (в Start, после этого Awake). Новый вид разбрасывается сам, без ручной проводки в каждой психике.
        if (move == null && !TryGetComponent<Personality>(out _)) gameObject.AddComponent<Personality>();
        // ШУМ — физика любого тела (ось звука): движение слышно, сам меряет скорость. Уши — у кого есть канал Hearing
        if (!TryGetComponent<Noise>(out _)) gameObject.AddComponent<Noise>();
        // ЗАПАХ — тоже физика любого тела: след вешает ТЕЛО (не каждая психика поштучно — лось однажды остался
        // без запаха). Цвет = состав (Recompute), сила — тюнинг психики (змея приглушает SetStrength)
        if (!TryGetComponent<ScentTrail>(out _)) gameObject.AddComponent<ScentTrail>();
        // ЭМОЦ-ИНДИКАЦИЯ — тоже тело: ярость/страх подкрашивают (у холоднокровных эмоций нет — тинт молчит сам)
        if (!TryGetComponent<EmotionTint>(out _)) gameObject.AddComponent<EmotionTint>();
        TryGetComponent(out health);
        TryGetComponent(out bite);
        TryGetComponent(out kick);
        TryGetComponent(out howl);
        TryGetComponent(out constrictAb);
        // обхват — новая способность: достраиваем скелет сами (руками не повесить = F молча мёртв).
        // Крутилки видны на добавленном компоненте в рантайме; для перманентного тюнинга добавь в редакторе.
        if (move != null && constrictAb == null) constrictAb = gameObject.AddComponent<PlayerConstrict>();
        TryGetComponent(out variance);
        TryGetComponent(out cold);
        TryGetComponent(out camoComp);

        BuildSlots();

        // РОДСТВО — УБИЙЦЕ: на нашу смерть кредитуем ТОГО, КТО УБИЛ (см. CreditKiller)
        if (health != null) health.onDeath.AddListener(CreditKiller);

        // каждое существо рождается с полным родством к СВОЕМУ шасси (волк уверен в своей волчности;
        // мета-шкала человечности игрока — потом, при социальном слое)
        if (chassis != null) SetAffinity(chassis.speciesName, AffinityCap);
        if (move != null) PlayerBody = this;

        renderers = GetComponentsInChildren<Renderer>();
        mpb = new MaterialPropertyBlock();
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
                        if (o.slot == h.slot && !o.chassisOnly) sl.variants.Add(new Variant { organ = o, species = d.speciesName }); // все доноры; ходовые части шасси не крадутся
                }
            list.Add(sl);
        }
        for (int i = 0; i < chimeraSlots; i++) list.Add(MakeChimeraSlot(list.Count)); // выданные химерные слоты
        slots = list.ToArray();

        if (installAllBeast) // застывшая химера (вервольф): весь лоадаут ПЕРВОГО донора надет с рождения
            foreach (var sl in slots)
                if (!sl.chimera) sl.current = sl.variants.Count > 0 ? 0 : -1;
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
        if (donors != null) foreach (var d in donors) if (d != null) s += GetAffinity(d.speciesName);
        return s;
    }

    // РОДСТВО — УБИЙЦЕ: на нашу смерть тело кредитует убийцу (+1 за каждый УНИКАЛЬНЫЙ видо-флаг НАШЕГО
    // тела: шасси + доноры с надетыми органами). Убийца — любой с телом: игрок ✓, змея-охотница ✓
    // (волк, задушенный змеёй, «достаётся змее» — задел эволюции химер-NPC). Наблюдатели не получают ничего.
    void CreditKiller()
    {
        var killer = health != null && health.LastAttacker != null
            ? health.LastAttacker.GetComponent<CreatureBody>() : null;
        if (killer == null || killer == this) return;

        // ПЕРЕВАРИВАНИЕ: убийца с телом-глотателем (шасси змеи) съедает добычу — тот же канал, что родство
        if (killer.TryGetComponent<Digestion>(out var dig)) dig.OnAte();

        var present = new HashSet<string>();
        if (chassis != null) present.Add(chassis.speciesName);
        if (slots != null)
            foreach (var sl in slots)
                if (sl.Installed && sl.DonorSpecies != null) present.Add(sl.DonorSpecies);
        foreach (var species in present) killer.AddAffinity(species, 1);
    }

    void OnDestroy() { if (PlayerBody == this) PlayerBody = null; }

    // цикл слота: человеческий → варианты доноров по кругу → человеческий (не по карману — пропускаются)
    void Toggle(Slot sl)
    {
        if (sl.variants.Count == 0) return;
        int next = NextStep(sl);
        if (next == sl.current) return; // некуда шагнуть (все варианты не влезают в пул)
        sl.current = next;
        Recompute();
    }

    // вклад одного надетого органа в статы тела (после бленда/экспрессии)
    struct Contribution
    {
        public float dmg, maxHp, life, rng, atkCd, mv, dash, dashCd, reduce, regen, regenOOC, thermal, howlR;
        public int venom, bleed;
        public bool bite, scent, kick, howl, cold, camo, thermalOn, constrict, digest;

        // СУПРЕМУМ дублей одного типа слота: скаляры — max (кулдауны — min: меньше = лучше), флаги — OR.
        // Дубль оси силу НЕ растит (второе сердце ≠ ×2 регена) — окупается только НОВЫМ направлением.
        public static Contribution Sup(Contribution a, Contribution b) => new()
        {
            dmg = Mathf.Max(a.dmg, b.dmg), maxHp = Mathf.Max(a.maxHp, b.maxHp), life = Mathf.Max(a.life, b.life),
            rng = Mathf.Max(a.rng, b.rng), atkCd = Mathf.Min(a.atkCd, b.atkCd),
            mv = Mathf.Max(a.mv, b.mv), dash = Mathf.Max(a.dash, b.dash), dashCd = Mathf.Min(a.dashCd, b.dashCd),
            reduce = Mathf.Max(a.reduce, b.reduce), regen = Mathf.Max(a.regen, b.regen),
            regenOOC = Mathf.Max(a.regenOOC, b.regenOOC), thermal = Mathf.Max(a.thermal, b.thermal),
            howlR = Mathf.Max(a.howlR, b.howlR),
            venom = Mathf.Max(a.venom, b.venom), bleed = Mathf.Max(a.bleed, b.bleed),
            bite = a.bite || b.bite, scent = a.scent || b.scent, kick = a.kick || b.kick,
            howl = a.howl || b.howl, cold = a.cold || b.cold, camo = a.camo || b.camo,
            thermalOn = a.thermalOn || b.thermalOn, constrict = a.constrict || b.constrict,
            digest = a.digest || b.digest,
        };
    }

    static readonly Organ EmptyOrgan = new(); // «нет базы» для химерного слота: бленд от нуля = чистый орган × м

    void Recompute()
    {
        if (slots == null || slots.Length == 0) return; // нет данных — не трогаем статы компонентов

        // Вклады группируются по РОДНОМУ ТИПУ СЛОТА органа: дубли (второе Сердце в химерном слоте)
        // схлопываются супремумом, группы суммируются. Без дублей == прежняя сумма по слотам.
        var groups = new Dictionary<string, Contribution>();
        int beast = 0;

        foreach (var sl in slots)
        {
            Contribution c;
            string key;
            if (sl.Installed)
            {
                Organ b = sl.Beast;
                Organ h = sl.human ?? EmptyOrgan;
                float m = BonusMultiplier(sl.DonorSpecies);
                c = new Contribution
                {
                    dmg = Blend(h.damage, b.damage, m),
                    maxHp = Blend(h.maxHp, b.maxHp, m),
                    life = Blend(h.lifeSteal, b.lifeSteal, m),
                    venom = b.venomStacks, bleed = b.bleedStacks, // дискретные фичи органа (как флаги) — не блендим
                    rng = b.range,                // дальность не масштабируем — фикс. трейдофф
                    howlR = b.howlRadius,         // голос-база органа (мощь домножит тело — эмиссия, не чувство)
                    atkCd = Blend(h.atkCooldown, b.atkCooldown, m),
                    mv = Blend(h.moveSpeed, b.moveSpeed, m),
                    dash = Blend(h.dashSpeed, b.dashSpeed, m),
                    dashCd = Blend(h.dashCooldown, b.dashCooldown, m),
                    reduce = Blend(h.damageReduction, b.damageReduction, m),
                    regen = Blend(h.regen, b.regen, m),
                    regenOOC = b.regenOOC,        // вне-боя реген не блендим — фича органа: иначе на Э=2 уходит в минус
                    thermal = b.thermalRange,     // фикс-фича органа (как range) — не блендим
                    bite = b.enablesBite, scent = b.enablesScent, kick = b.enablesKick,
                    howl = b.enablesHowl, cold = b.coldBlooded, camo = b.camo, thermalOn = b.enablesThermal,
                    constrict = b.enablesConstrict, digest = b.digestion,
                };
                key = b.slot; // дубль типа (второе Сердце) идёт в ту же группу — супремум
                beast++;
            }
            else if (sl.human != null)
            {
                // Природная особь (фикс. экспрессия): органы записаны в МУТАГЕННОЙ шкале — без сыворотки
                // раскрыты лишь на Э (волк ~0.45). У игрока (Э авто) человеческая база идёт как есть (e=1).
                // Времена (кулдауны) и дальность не скейлим — как и в химерном бленде.
                Organ h = sl.human;
                float e = expression > 0f ? expression : 1f;
                c = new Contribution
                {
                    dmg = h.damage * e, maxHp = h.maxHp * e, life = h.lifeSteal * e,
                    venom = h.venomStacks, bleed = h.bleedStacks,
                    rng = h.range, atkCd = h.atkCooldown, mv = h.moveSpeed * e, dash = h.dashSpeed * e,
                    dashCd = h.dashCooldown, reduce = h.damageReduction * e, regen = h.regen * e,
                    regenOOC = h.regenOOC * e, thermal = h.thermalRange, howlR = h.howlRadius,
                    bite = h.enablesBite, scent = h.enablesScent, kick = h.enablesKick,
                    howl = h.enablesHowl, cold = h.coldBlooded, camo = h.camo, thermalOn = h.enablesThermal,
                    constrict = h.enablesConstrict, digest = h.digestion,
                };
                key = h.slot;
            }
            else continue; // пустой химерный слот

            groups[key] = groups.TryGetValue(key, out var prev) ? Contribution.Sup(prev, c) : c;
        }

        // суммирование групп; урон группы «Пасть» принадлежит УКУСУ, не мечу
        float dmgF = 0f, dmgBiteF = 0f, maxHpF = 0f, lifeF = 0f;
        float rng = 0f, atkCd = 0f, mv = 0f, dash = 0f, dashCd = 0f, reduce = 0f, regen = 0f, regenOOC = 0f, thermal = 0f, howlR = 0f;
        int venom = 0, bleed = 0;
        bool biteOn = false, scentOn = false, kickOn = false, howlOn = false, coldOn = false, camoOn = false,
             thermalOn = false, constrictOn = false, digestOn = false;
        foreach (var kv in groups)
        {
            var c = kv.Value;
            if (kv.Key == "Пасть") dmgBiteF += c.dmg; else dmgF += c.dmg;
            maxHpF += c.maxHp; lifeF += c.life; venom += c.venom; bleed += c.bleed;
            rng += c.rng; atkCd += c.atkCd; mv += c.mv; dash += c.dash; dashCd += c.dashCd;
            reduce += c.reduce; regen += c.regen; regenOOC += c.regenOOC; thermal += c.thermal;
            howlR = Mathf.Max(howlR, c.howlR);
            biteOn |= c.bite; scentOn |= c.scent; kickOn |= c.kick; howlOn |= c.howl;
            coldOn |= c.cold; camoOn |= c.camo; thermalOn |= c.thermalOn; constrictOn |= c.constrict;
            digestOn |= c.digest;
        }
        int dmg = Mathf.RoundToInt(dmgF), dmgBite = Mathf.RoundToInt(dmgBiteF);
        int maxHp = Mathf.RoundToInt(maxHpF), life = Mathf.RoundToInt(lifeF);

        if (bite != null)
        {
            bite.BiteEnabled = biteOn;
            bite.SetDamage(dmgBite); // 0 = органы молчат → PlayerBite остаётся на своём дефолте
            bite.SetVenom(venom);    // яд змеиных клыков на укусе игрока
            bite.SetBleed(bleed);    // кровотечение волчьих клыков на укусе игрока
        }
        if (kick != null) kick.KickEnabled = kickOn; // пинок — фича человеческих ног: с волчьими пропадает
        // ГОЛОС — от данных: радиус = органная база × МОЩЬ-превосходство (игрок BonusMult ×1..2;
        // NPC max(1, Э) — норму вниз не штрафуем: взрослый волк воет как волк, вервольф Э2 — вдвое)
        float voiceMult = Mathf.Max(1f, move != null ? BonusMult : expression);
        float howlReach = howlR * voiceMult;
        if (howl != null) { howl.HowlEnabled = howlOn; howl.SetReach(howlReach); } // вой-стан — фича волчьей Пасти
        if (constrictAb != null) constrictAb.ConstrictEnabled = constrictOn; // обхват — фича Удушающего хвоста (химерный слот)
        SetColdBlooded(coldOn); // холоднокровность (Сердце змеи): невидимость для термозрения врагов
        SetCamouflage(camoOn);  // камуфляж (Чешуя змеи): невидимость в неподвижности
        SetDigestion(digestOn); // переваривание (Тело-хвост змеи): убил → сыт, бонус-реген до полного HP
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

        // НПС-потребители (психика): тело отдаёт деривированное — урон (суммарный: их мили и есть укус), скорость,
        // ЭФФЕКТЫ УКУСА (яд/кровь из Пасти) и ГОЛОС (радиус воя, уже × мощь) — всё data-driven как у игрока
        foreach (var c in GetComponents<IBodyStatConsumer>()) c.OnBodyStats(dmg + dmgBite, mv, venom, bleed, howlReach);

        if (move != null) UpdateTint(); // ТОЛЬКО игрок: тело = смесь тинтов видов надетых органов. NPC — запечённый материал (не драться с Telegraph)

        // ВИДОВОЙ ОТПЕЧАТОК В ЗАПАХЕ: след пахнет СОСТАВОМ — красится смесью тинтов шасси+аугументов
        // (природная особь → чистый тинт вида, химера → грязный микс). Волчье Чутьё читает, КТО прошёл,
        // прямо из цвета следа — у всех тел, не только у игрока
        if (TryGetComponent<ScentTrail>(out var scentTrail))
        {
            Color comp = CompositionTint();
            scentTrail.Configure(new Color(comp.r, comp.g, comp.b, 0.65f), move != null);
        }
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

    // переваривание как компонент-маркер: физиология змеиного шасси (chassisOnly — аугументом не крадётся)
    void SetDigestion(bool on)
    {
        if (on && digestComp == null) digestComp = gameObject.AddComponent<Digestion>();
        else if (!on && digestComp != null) { Destroy(digestComp); digestComp = null; }
    }

    // человеч.значение + (звериное − человеч.) × множитель: на ×1 = звериное, на ×2 = вдвое дальше от человека
    static float Blend(float human, float beast, float mult) => human + (beast - human) * mult;

    // цвет тела ИГРОКА = СМЕСЬ тинтов ВИДОВ надетых органов (CompositionTint). NPC сюда не заходят
    // (запечённый материал — не драться с Telegraph), но их ЗАПАХ красится той же смесью в Recompute.
    void UpdateTint()
    {
        Color body = CompositionTint();
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            renderers[i].GetPropertyBlock(mpb);
            mpb.SetColor(BaseColor, body);
            renderers[i].SetPropertyBlock(mpb);
        }
    }

    // СМЕСЬ тинтов ВИДОВ по составу тела: человеческий слот → тинт шасси (телесный), звериный → тинт
    // вида-донора. Отражает СОСТАВ (волчий билд серее, змеиный зеленее; чем химернее, тем «грязнее»/чуждее —
    // визуальная цена химеризации). ОБЩАЯ для палитры тела и запахового следа (видовой отпечаток в запахе)
    Color CompositionTint()
    {
        float r = 0f, g = 0f, b = 0f; int n = 0;
        foreach (var sl in slots)
        {
            Color? t = null;
            if (sl.Installed) t = SpeciesTint(sl.DonorSpecies);                          // звериный орган → тинт его вида
            else if (sl.human != null) t = chassis != null ? chassis.tint : Color.gray;  // человеческий → телесный
            if (t.HasValue) { r += t.Value.r; g += t.Value.g; b += t.Value.b; n++; }      // пустой химерный слот не считаем
        }
        return n > 0 ? new Color(r / n, g / n, b / n) : (chassis != null ? chassis.tint : Color.gray);
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

/// <summary>
/// НПС-потребитель статов тела: психика получает деривированное из органов (урон, скорость, эффекты
/// укуса, ГОЛОС — радиус воя × мощь; 0 = Пасть не воет) и раздаёт своим доставкам/приёмам.
/// Витальность (HP/броня/реген) — отдельно («конституция», applyVitals).
/// </summary>
public interface IBodyStatConsumer
{
    void OnBodyStats(int damage, float moveSpeed, int venom, int bleed, float howlRange);
}
