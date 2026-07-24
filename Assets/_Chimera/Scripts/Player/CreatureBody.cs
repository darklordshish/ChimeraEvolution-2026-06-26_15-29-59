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

    // РОДСТВО — ЕДИНАЯ РАВНОМЕРНАЯ КРИВАЯ 0→100 (решение пользователя): и скидка, и мощь растут ЛИНЕЙНО
    // на всём диапазоне. Прежде было ступенчато (скидка 0–80, мощь только 80–100) — и грайнд 0–80 не давал
    // силы вовсе, лишь дешевизну. Теперь КАЖДОЕ убийство делает орган и дешевле, И сильнее — прогресс ровный.
    // Концы те же: 0 родства = полная цена ×1 мощь, 100 = −80% цена ×2 мощь
    [SerializeField] float discountPerAffinity = 0.008f; // −0.8% за единицу → −80% на 100 родства
    [SerializeField] float maxDiscount = 0.8f;
    [SerializeField] float bonusStartAffinity = 0f;      // мощь копится С НУЛЯ (было 80 — «мёртвая зона» силы)
    [SerializeField] float bonusFullAffinity = 100f;
    [SerializeField] float maxBonusMult = 2f;            // звериная часть органа ×2 на 100 родства

    [Header("Капы овершута мощи (глушим 2з−ч на 100 родства)")]
    [SerializeField] float maxDamageReduction = 0.6f;   // потолок брони (иначе Blend(0,0.4,2)=0.8 = 80% резист)
    [SerializeField] float minAtkCooldown = 0.2f;       // пол скорострельности (иначе волчье сердце 0.30→0.15 = пулемёт)

    [Header("Химерные слоты (мета: награда суперхимеры)")]
    [SerializeField, Min(0)] int chimeraSlots;    // выданные универсальные слоты (dev-кнопка / SuperBossReward)
    [SerializeField] float chimeraSlotMult = 2f;  // множитель цены органа в химерном слоте (не-нативный «графт»)

    [Header("NPC-режим (тело как данные)")]
    [SerializeField] bool installAllBeast;    // все звериные органы надеты с рождения (вервольф — застывшая химера)
    [FormerlySerializedAs("fixedBonusMult")]
    [Header("Чувства игрока (профиль Senses; у NPC — на префабе)")]
    [SerializeField] float sightRange = 30f;   // ЗРЕНИЕ игрока: глаза при нём всегда, органом не выдаётся
    [SerializeField] float hearingRange = 20f; // СЛУХ: уши тоже при нём всегда (лосиный орган позже расширит)
    [SerializeField] float scentRange = 22f;   // дальность НЮХА, когда надето волчье Чутьё (тюнинг здесь, не в органе)

    // UNITY-ГОЧА: НОВОЕ сериализованное поле у компонента, УЖЕ лежащего в сцене/префабе, приходит НУЛЁМ —
    // инициализатор из кода применяется только к свежесозданным объектам. Поэтому 0 читаем как «не настроено»
    // и подставляем дефолт, иначе чувство молча выключается (зрение 0 = слепой игрок без единой ошибки в консоли)
    float SightRange => sightRange > 0f ? sightRange : 30f;
    float HearingRange => hearingRange > 0f ? hearingRange : 20f;
    float ScentRange => scentRange > 0f ? scentRange : 22f;

    // ТА ЖЕ ГОЧА У АССЕТА ВИДА: `baseHp` появился позже — в неперегенерённом ассете он 0, и существо
    // осталось бы с 1 HP. Читаем 0 как «бутстрап не прогнан» и подставляем человеческую норму:
    // ошибка становится видна как странное число в дев-панели, а не как мгновенная смерть от щелчка
    float BaseHp => chassis != null && chassis.baseHp > 0 ? chassis.baseHp : 75f;
    float BaseStamina => chassis != null && chassis.baseStamina > 0 ? chassis.baseStamina : 100f;
    float BaseStaminaRegen => chassis != null && chassis.baseStaminaRegen > 0f ? chassis.baseStaminaRegen : 12f;

    [SerializeField] float expression;        // ЭКСПРЕССИЯ: насколько раскрыты гены зверя. 0 = авто (кривая родства);
                                              // >0 фикс: вервольф 2 (= потолок игрока), природный волк ~0.45 (без сыворотки)
    [SerializeField] bool applyVitals = true; // false: HP/броня/реген — «конституция» психики, тело их не трогает

    // вариант органа для слота: орган конкретного вида (мультидонор: волчий ИЛИ змеиный на один слот).
    // native — орган ШАССИ («человеческий»): он и выбираемый вариант, и база экспрессии слота
    class Variant { public Organ organ; public string species; public bool native; }

    class Slot
    {
        public string name, hotkey;                     // хоткей задаёт ШАССИ — раскладка стабильна при любых донорах
        public bool chimera;                            // универсальный доп-слот: любой орган, цена ×chimeraSlotMult
        public readonly List<Variant> variants = new(); // ВСЕ варианты, включая человеческий (у не-химерного он индекс 0)
        public int current = -1;                        // индекс варианта; -1 = ПУСТО (бывает только у химерного)

        public Variant Pick => current >= 0 ? variants[current] : null;
        public Organ Worn => current >= 0 ? variants[current].organ : null;   // что реально надето (в т.ч. человеческое)
        public bool Empty => current < 0;                                     // пустой химерный слот

        // «Installed» = надет ЗВЕРИНЫЙ орган (человеческий не считается химеризацией — шкала мозга не дрейфует)
        public bool Installed => current >= 0 && !variants[current].native;
        public Organ Beast => Installed ? variants[current].organ : null;
        public string DonorSpecies => Installed ? variants[current].species : null;
    }

    Slot[] slots;
    PlayerAttack attack;
    PlayerController move;
    Health health;
    Stamina stamina;   // бак дыхалки — кор-механика у ВСЕХ тел, как и Health
    PlayerBite bite;
    PlayerKick kick;
    PlayerHowl howl;
    PlayerConstrict constrictAb;
    SpawnVariance variance; // разброс особи: HP учитываем при раздаче витальности (иначе гонка Start'ов)
    ColdBlooded cold;       // холоднокровность (Сердце змеи) — компонент-маркер, вешаем/снимаем по сборке
    Camouflage camoComp;    // камуфляж-в-неподвижности (Чешуя змеи) — вешаем/снимаем по сборке
    Thorns thornsComp;      // иглы-ответка (Шкура ежа) — тем же паттерном
    VenomResist venomResistComp; // ядоупорность (Сердце ежа)
    BleedResist bleedResistComp; // кровеупорность (Лосиное сердце)
    Satiety satietyComp;    // шкала сытости-голода — у любого тела; распад зависит от однородности (метаболизм химеры)
    PlayerBellow bellowAb;  // рёв (Глотка лося) — до-создаём игроку в Awake, включаем сборкой
    PlayerAntler antlerAb;  // рога (придаток лося, химерный слот) — до-создаём игроку, включаем сборкой
    PlayerCharge chargeAb;  // таран (Лосиные ноги) — до-создаём игроку, включаем сборкой
    PlayerRoll rollAb;      // перекат (Ежиные ноги) — до-создаём игроку, включаем сборкой
    PlayerQuillVolley volleyAb; // залп игл (придаток «Игломёт» ежа, химерный слот) — до-создаём игроку, включаем сборкой
    Betrayal betrayal;      // подрыв признания: удар по кину копит эрозию (только у игрока)
    Senses senses;          // профиль чувств ИГРОКА (каналы от сборки); у NPC он приходит с префаба
    // ГЛОТАЕТ ЦЕЛИКОМ (Тело-хвост змеи, chassisOnly): убитая добыча даёт ПОЛНУЮ сытость (крупная трапеза),
    // а не долю. Само лечение — общий `Satiety` (переваривание больше не отдельный компонент); поведение
    // «прячусь переваривать на насест» — на психике змеи (читает Satiety.IsSated)
    public bool DigestsWhole { get; private set; }
    Renderer[] renderers;
    MaterialPropertyBlock mpb;
    int lastAffinitySum = -1;
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    public const int AffinityCap = 100; // потолок родства на вид (дальше некуда: скидка и мощь на полке)

    [SerializeField] float assistFeedRadius = 14f; // сытость помощникам: тела своего вида в этом радиусе от убийцы делят добычу (радиус стаи)
    const float KillMeal = 0.7f;                   // насколько убийство наполняет ШКАЛУ сытости (0..1); глотающий целиком — на всю (1)

    // РОДСТВО — ЛОКАЛЬНОЕ, в теле КАЖДОГО существа (не в глобальном трекере: «ЦНС» игры не грузим).
    // Все звери мира — эксперименты с сывороткой (или съевшие их) → родство = база любого тела.
    // Задел эволюции химер-NPC: змея, съевшая волков, копит родство-волк (пока ничего с ним не делает).
    readonly Dictionary<string, int> affinity = new();

    public static CreatureBody PlayerBody { get; private set; } // тело ИГРОКА: HUD/dev/спавнеры читают его родство

    /// <summary>МОЩЬ носителя — ось экспрессии как ЧИСЛО: у игрока множитель родства (1..2), у NPC —
    /// фикс. экспрессия вида (волк 0.45, вервольф 2). Общая ручка: на неё вешаются и масштабы (радиус
    /// голоса), и ПОРОГИ-ФИЧИ («что вообще открывается»). Переиспользуемо химерами и новыми видами.</summary>
    public float Power => move != null ? BonusMult : expression;

    /// <summary>Дорос ли носитель до СТАНА в вое (порог задан органом Пасти, см. Organ.howlStunAt).</summary>
    public bool HowlStuns { get; private set; }

    public int GetAffinity(string species) { affinity.TryGetValue(species, out int v); return v; }
    public void AddAffinity(string species, int n) => affinity[species] = Mathf.Clamp(GetAffinity(species) + n, 0, AffinityCap);
    public void SetAffinity(string species, int v) => affinity[species] = Mathf.Clamp(v, 0, AffinityCap);
    public IEnumerable<KeyValuePair<string, int>> AllAffinity => affinity;

    int poolBonus; // расширение пула наградами (SuperBossReward) — рантайм-бонус, ассет шасси не трогаем

    public int Pool => (chassis != null ? chassis.mutagenPool : 0) + poolBonus;

    /// <summary>Расширить пул мутагена (награда суперхимеры). Живо в рантайме.</summary>
    public void ExpandPool(int n) { poolBonus += n; Recompute(); }
    public int PoolUsed { get { int s = 0; if (slots != null) foreach (var sl in slots) s += SlotCost(sl); return s; } }
    // каждый слот занимает пул; РОДНОЙ орган — тоже со скидкой родства с шасси (честно: человек — вид,
    // просто ты на нём на 100 родства → −80%). Пустой химерный = 0. Одна ветка: родной орган — обычный вариант
    int SlotCost(Slot sl) => sl.Empty ? 0 : CostOf(sl, sl.variants[sl.current]);
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
                string cur = sl.Empty ? "—" : sl.Worn.organName; // «—» = пустой химерный
                lines.Add($"{sl.hotkey} {sl.name}: {cur} ({SlotCost(sl)}){(sl.Installed ? "  ✓" : "")}");
            }
            return string.Join("\n", lines);
        }
    }

    // ── публичный слепок слота для UI ─────────────────────────────────────────
    public struct SlotView
    {
        public string slot, hotkey, organName, nextName, species; // species — вид надетого органа (морф гнезда)
        public int cost, nextCost;
        public int unaffordable; // сколько вариантов скрыто по цене (цикл их пропускает МОЛЧА — UI должен сказать)
        public bool chimera;    // универсальный слот (родного органа нет)
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
            chimera = sl.chimera, // универсальный слот: родного органа нет — UI не подписывает его «Кистью»
            species = sl.Empty ? "" : (sl.Pick.native ? (chassis != null ? chassis.speciesName : "") : sl.DonorSpecies), // вид надетого — для морфа гнезда в UI
            organName = sl.Empty ? "—" : sl.Worn.organName, // «—» = пустой химерный слот
            cost = SlotCost(sl),
            installed = sl.Installed,
            hasBeast = sl.variants.Count > 1,               // родной орган тоже вариант → «есть выбор» от двух
            canToggle = sl.variants.Count > 0 && next != sl.current,
            nextName = next >= 0 ? sl.variants[next].organ.organName : "—",
            nextCost = next >= 0 ? CostOf(sl, sl.variants[next]) : 0,
        };
    }

    /// <summary>Слепок ВАРИАНТА для UI — одна «звезда»: орган вида, цена со скидкой родства, доступность.</summary>
    public struct VariantView
    {
        public string organName, species, slotType;
        public int cost;
        public bool native;     // родной орган шасси (звезда в созвездии человека)
        public bool worn;       // сейчас надет в ЭТОМ слоте
        public bool duplicate;  // этот же орган уже надет в ДРУГОМ слоте — повтор бессмыслен
        public bool affordable; // влезает в пул с учётом возврата за снимаемый
    }

    /// <summary>Все варианты слота — для ПРЯМОГО выбора (клик/дроп по звезде) вместо цикла по кругу.
    /// Цикл остаётся на хоткеях 1–6, но перестаёт быть единственным способом собрать тело.</summary>
    public List<VariantView> GetVariants(int i)
    {
        var res = new List<VariantView>();
        if (slots == null || i < 0 || i >= slots.Length) return res;
        var sl = slots[i];
        for (int v = 0; v < sl.variants.Count; v++)
        {
            var vr = sl.variants[v];
            res.Add(new VariantView
            {
                organName = vr.organ.organName,
                species = vr.species,
                slotType = vr.organ.slot,   // РОДНОЙ тип слота органа — для группировки звёзд по строкам-слотам
                cost = CostOf(sl, vr),
                native = vr.native,
                worn = sl.current == v,
                duplicate = WornElsewhere(sl, vr),
                affordable = CanInstall(sl, v),
            });
        }
        return res;
    }

    /// <summary>Поставить КОНКРЕТНЫЙ вариант в слот (звезда → гнездо). Не влезает в пул — отказ (гнездо краснеет).</summary>
    public bool Install(int slot, int variant)
    {
        if (slots == null || slot < 0 || slot >= slots.Length) return false;
        var sl = slots[slot];
        if (variant < 0 || variant >= sl.variants.Count) return false;
        if (sl.current == variant) return true;      // уже надет — жест не отказ, просто ничего не меняет
        if (!Available(sl, variant)) return false;   // не по карману ИЛИ такой же орган уже носится
        sl.current = variant;
        Recompute();
        return true;
    }

    /// <summary>Опустошить ХИМЕРНЫЙ слот (звезда обратно в небо). У обычного слота «снять» = надеть РОДНОЙ
    /// орган — это `Install` его варианта, отдельной операции больше нет.</summary>
    public bool Remove(int slot)
    {
        if (slots == null || slot < 0 || slot >= slots.Length) return false;
        var sl = slots[slot];
        if (!sl.chimera || sl.Empty) return false;
        sl.current = -1;
        Recompute();
        return true;
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

    // химерный слот: ДОП-орган сверх шасси — вытеснять нечего, поэтому базы экспрессии нет (раскрытие от нуля).
    // Принимает ЛЮБОЙ орган ЛЮБОГО вида, ВКЛЮЧАЯ РОДНОЙ (второе человеческое сердце — валидный графт:
    // «усилиться, не звереть», шкала мозга не дрейфует). Стартует пустым.
    Slot MakeChimeraSlot(int index)
    {
        var sl = new Slot { name = "Химерный", hotkey = (index + 1).ToString(), chimera = true };
        AddVariants(sl, chassis, null, native: true);   // родные органы — такие же звёзды, как донорские
        if (donors != null)
            foreach (var d in donors) AddVariants(sl, d, null, native: false);
        return sl;
    }

    // варианты одного вида в слот: только органы нужного типа (slotFilter) или все (null — химерный слот).
    // chassisOnly-органы (ходовая часть шасси) аугументом не крадутся никогда
    void AddVariants(Slot sl, SpeciesSO species, string slotFilter, bool native)
    {
        if (species == null || species.organs == null) return;
        foreach (var o in species.organs)
        {
            if (o.chassisOnly) continue;
            if (slotFilter != null && o.slot != slotFilter) continue;
            sl.variants.Add(new Variant { organ = o, species = species.speciesName, native = native });
        }
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

    /// <summary>ОДИН ЭКЗЕМПЛЯР ОРГАНА НА ТЕЛО: тот же орган того же вида уже надет в другом слоте.
    /// Повтор бессмыслен — величины схлопывает супремум, а фичи и так работают с одного экземпляра.
    /// Дубль ДРУГОГО вида в тот же ТИП слота при этом разрешён и осмыслен: волчья Пасть (вой) в родном
    /// слоте + змеиные клыки (яд) в химерном = обе фичи. Состав химерного слота из-за этого ДИНАМИЧЕН:
    /// список вариантов стабилен (индексы не едут), но надетое где-то ещё выпадает из доступных.</summary>
    bool WornElsewhere(Slot self, Variant v)
    {
        if (slots == null) return false;
        foreach (var sl in slots)
        {
            if (sl == self || sl.Empty) continue;
            var w = sl.Pick;
            if (w.organ == v.organ && w.species == v.species) return true;
        }
        return false;
    }

    // доступен ли вариант СЕЙЧАС: влезает в пул И не носится в другом слоте
    bool Available(Slot sl, int idx) => CanInstall(sl, idx) && !WornElsewhere(sl, sl.variants[idx]);

    // цена органа со скидкой от НАШЕГО родства с ЕГО видом (родство теперь локальное — у каждого тела своё)
    int EffectiveCost(Organ organ, string species)
    {
        if (organ == null) return 0;
        float discount = Mathf.Clamp(GetAffinity(species) * discountPerAffinity, 0f, maxDiscount);
        return Mathf.Max(1, Mathf.CeilToInt(organ.cost * (1f - discount)));
    }

    // следующий достижимый шаг цикла: по вариантам по кругу (родной орган — такой же шаг, как донорские).
    // Химерный слот дополнительно можно ОПУСТОШИТЬ (-1); обычный пустым не бывает — без органа нет части тела.
    // Не влезающие в пул варианты пропускаются молча (UI сообщает об этом через SlotView.unaffordable)
    int NextStep(Slot sl)
    {
        int n = sl.variants.Count;
        if (n == 0) return sl.current;
        int lo = sl.chimera ? -1 : 0;
        int idx = sl.current;
        for (int step = 0; step <= n; step++)
        {
            idx = idx + 1 >= n ? lo : idx + 1;
            if (idx == -1 || Available(sl, idx)) return idx; // надетое в другом слоте цикл пропускает
        }
        return sl.current;
    }

    // ЭКСПРЕССИЯ звериной части: у игрока РАВНОМЕРНО ×1 (родство 0) → maxBonusMult (родство 100), линейно
    // на всём диапазоне (мутаген раскрывает гены с каждым убийством). У NPC — фиксированная (поле expression).
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
        // МОРАЛЬ — УНИВЕРСАЛЬНАЯ механика (шкала страх↔ярость): тело даёт её любому NPC (волк/лось/будущие
        // стадные). Холоднокровные (сердце змеи) имеют компонент, но ColdBlooded делает его инертным — вне морали
        if (move == null && !TryGetComponent<Morale>(out _)) gameObject.AddComponent<Morale>();
        // ШУМ — физика любого тела (ось звука): движение слышно, сам меряет скорость. Уши — у кого есть канал Hearing
        if (!TryGetComponent<Noise>(out _)) gameObject.AddComponent<Noise>();
        // СЫТОСТЬ↔ГОЛОД — движитель у ЛЮБОГО животного (все едят): шкала тает с рождения, психики читают её
        // как мотивацию (M3). Едой наполняют CreditKiller (хищник) и кормёжка (травоядный, M2)
        if (!TryGetComponent(out satietyComp)) satietyComp = gameObject.AddComponent<Satiety>();
        // ЗАПАХ — тоже физика любого тела: след вешает ТЕЛО (не каждая психика поштучно — лось однажды остался
        // без запаха). Цвет = состав (Recompute), сила — тюнинг психики (змея приглушает SetStrength)
        if (!TryGetComponent<ScentTrail>(out _)) gameObject.AddComponent<ScentTrail>();
        // ЧУВСТВА ИГРОКА: он такое же существо, как NPC — со своим профилем каналов, только дальности ему
        // задаёт СБОРКА, а не вид. NPC получают профиль с префаба/психики, поэтому вешаем лишь игроку
        if (move != null)
        {
            if (!TryGetComponent(out senses)) senses = gameObject.AddComponent<Senses>();
            Perception.PlayerSenses = senses;
        }
        // ЭМОЦ-ИНДИКАЦИЯ — тоже тело: ярость/страх подкрашивают (у холоднокровных эмоций нет — тинт молчит сам)
        if (!TryGetComponent<EmotionTint>(out _)) gameObject.AddComponent<EmotionTint>();
        TryGetComponent(out health);
        TryGetComponent(out bite);
        TryGetComponent(out kick);
        TryGetComponent(out howl);
        TryGetComponent(out constrictAb);
        // обхват/рёв — новые способности: достраиваем скелет сами (руками не повесить = кнопка молча мертва).
        // Крутилки видны на добавленном компоненте в рантайме; для перманентного тюнинга добавь в редакторе.
        if (move != null && constrictAb == null) constrictAb = gameObject.AddComponent<PlayerConstrict>();
        TryGetComponent(out bellowAb);
        if (move != null && bellowAb == null) bellowAb = gameObject.AddComponent<PlayerBellow>();
        TryGetComponent(out antlerAb);
        if (move != null && antlerAb == null) antlerAb = gameObject.AddComponent<PlayerAntler>();
        TryGetComponent(out chargeAb);
        if (move != null && chargeAb == null) chargeAb = gameObject.AddComponent<PlayerCharge>();
        TryGetComponent(out rollAb);
        if (move != null && rollAb == null) rollAb = gameObject.AddComponent<PlayerRoll>();
        TryGetComponent(out volleyAb);
        if (move != null && volleyAb == null) volleyAb = gameObject.AddComponent<PlayerQuillVolley>();
        TryGetComponent(out betrayal);
        if (move != null && betrayal == null) betrayal = gameObject.AddComponent<Betrayal>(); // эрозия признания — только у игрока
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

        // ЛИЦО игрока (глаза/брови/борода из PlayerModel) тинтом состава НЕ красим — черты остаются читаемыми
        // ЗУБЫ тоже вне тинта: у модели игрока они отдельной деталью со своим костяным материалом, и
        // без исключения зеленели бы вместе с телом — оскал должен читаться на любом составе
        renderers = System.Array.FindAll(GetComponentsInChildren<Renderer>(), r =>
            r.name != "EyeL" && r.name != "EyeR" && r.name != "BrowL" && r.name != "BrowR"
            && r.name != "Beard" && r.name != "Teeth");
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
            var sl = new Slot { name = h.slot, hotkey = h.hotkey }; // раскладку задаёт шасси
            // РОДНОЙ орган — вариант №0: «снять звериное» стало тем же жестом, что «надеть». chassisOnly
            // (ходовая часть шасси) живёт в СВОЁМ слоте законно — красть её нельзя, а носить себе можно
            sl.variants.Add(new Variant { organ = h, species = chassis.speciesName, native = true });
            if (donors != null)
                foreach (var d in donors) AddVariants(sl, d, h.slot, native: false);
            sl.current = 0; // по умолчанию носим своё
            list.Add(sl);
        }
        for (int i = 0; i < chimeraSlots; i++) list.Add(MakeChimeraSlot(list.Count)); // выданные химерные слоты
        slots = list.ToArray();

        if (installAllBeast) // застывшая химера (вервольф): весь лоадаут ПЕРВОГО донора надет с рождения
            foreach (var sl in slots)
            {
                if (sl.chimera) continue;
                for (int i = 0; i < sl.variants.Count; i++)
                    if (!sl.variants[i].native) { sl.current = i; break; } // первый ЗВЕРИНЫЙ вариант
            }
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

        // СЫТОСТЬ — ЕДИНАЯ механика «поел → бонус-реген» (экосистема самоподдерживается, победитель не
        // остаётся сразу добычей). Только ХИЩНИКУ (лось-травоядное добычей не восстанавливается). Змея
        // ГЛОТАЕТ ЦЕЛИКОМ → полная трапеза (сильнее); убийце полный бонус, стае рядом (делили тушу) —
        // половина. Поведение сытого (змея прячется на насест) — на психике, читает Satiety.IsSated
        if (killer.chassis != null && killer.chassis.eatsMeat)
        {
            Feed(killer, killer.DigestsWhole ? 1f : KillMeal); // глотающий целиком (змея) наедается на всю шкалу

            // МАССИВНАЯ добыча (лось, вервольф) велика — кормит и СТАЮ: кины, участвовавшие в бою (рядом
            // с тушей), получают половину. Мелкую жертву делить нечего — ест только убийца
            if (GetComponent<Massive>() != null)
                foreach (var col in Physics.OverlapSphere(killer.transform.position, assistFeedRadius, ~0, QueryTriggerInteraction.Ignore))
                {
                    var ally = col.GetComponentInParent<CreatureBody>();
                    if (ally == null || ally == killer || ally == this) continue;
                    if (ally.chassis != killer.chassis || !ally.chassis.eatsMeat) continue;
                    Feed(ally, KillMeal * 0.5f); // стая делит тушу — половина трапезы
                }
        }

        var present = new HashSet<string>();
        if (chassis != null) present.Add(chassis.speciesName);
        if (slots != null)
            foreach (var sl in slots)
                if (sl.Installed && sl.DonorSpecies != null) present.Add(sl.DonorSpecies);
        foreach (var species in present) killer.AddAffinity(species, 1);
    }

    void OnDestroy() { if (PlayerBody == this) PlayerBody = null; }

    // цикл слота (хоткеи 1–6): родной → доноры по кругу → родной (не по карману — пропускаются).
    // Прямой выбор живёт в Install/Remove; цикл удобен для двух вариантов и вязнет на многих
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
        public float dmg, hpBonus, stam, stamRegen, life, rng, atkCd, mv, dash, dashDur, dashCd, reduce, regen, regenOOC, thermal, howlR, howlStunAt;
        public int venom, bleed;
        public bool bite, scent, kick, howl, cold, camo, thermalOn, constrict, digest, bellow, antler, charge, roll;
        public bool thorns, venomResist, quillVolley; // иглы-ответка, ядоупорность, залп (ёж)
        public bool bleedResist;  // кровеупорность (лосиное сердце)
        public float volleyMult; // мощь залпа от родства с ежом (0 = залпа нет)
        public bool insight; // ЧУТЬЁ УЧЁНОГО: распознавание намерений + числа состояний (человеческое Чутьё)
        public bool keenEar;  // ОСТРЫЙ СЛУХ: различение вида источника + волны звука на экране
        public float earMult; // множитель дальности слуха (супремум дублей)
        public bool constrictNative; // хват на РОДНОМ шасси (nativeChassis == шасси тела) → открыта ст.3 удушения

        // СУПРЕМУМ дублей одного типа слота: скаляры — max (кулдауны — min: меньше = лучше), флаги — OR.
        // Дубль оси силу НЕ растит (второе сердце ≠ ×2 регена) — окупается только НОВЫМ направлением.
        public static Contribution Sup(Contribution a, Contribution b) => new()
        {
            dmg = Mathf.Max(a.dmg, b.dmg), hpBonus = Mathf.Max(a.hpBonus, b.hpBonus), life = Mathf.Max(a.life, b.life),
            stam = Mathf.Max(a.stam, b.stam), stamRegen = Mathf.Max(a.stamRegen, b.stamRegen),
            rng = Mathf.Max(a.rng, b.rng), atkCd = Mathf.Min(a.atkCd, b.atkCd),
            mv = Mathf.Max(a.mv, b.mv), dash = Mathf.Max(a.dash, b.dash), dashDur = Mathf.Max(a.dashDur, b.dashDur), dashCd = Mathf.Min(a.dashCd, b.dashCd),
            reduce = Mathf.Max(a.reduce, b.reduce), regen = Mathf.Max(a.regen, b.regen),
            regenOOC = Mathf.Max(a.regenOOC, b.regenOOC), thermal = Mathf.Max(a.thermal, b.thermal),
            howlR = Mathf.Max(a.howlR, b.howlR),
            howlStunAt = Mathf.Max(a.howlStunAt, b.howlStunAt),
            venom = Mathf.Max(a.venom, b.venom), bleed = Mathf.Max(a.bleed, b.bleed),
            bite = a.bite || b.bite, scent = a.scent || b.scent, kick = a.kick || b.kick,
            howl = a.howl || b.howl, cold = a.cold || b.cold, camo = a.camo || b.camo,
            thermalOn = a.thermalOn || b.thermalOn, constrict = a.constrict || b.constrict,
            constrictNative = a.constrictNative || b.constrictNative,
            digest = a.digest || b.digest, bellow = a.bellow || b.bellow, antler = a.antler || b.antler,
            charge = a.charge || b.charge, roll = a.roll || b.roll, insight = a.insight || b.insight,
            keenEar = a.keenEar || b.keenEar, earMult = Mathf.Max(a.earMult, b.earMult),
            thorns = a.thorns || b.thorns, venomResist = a.venomResist || b.venomResist,
            quillVolley = a.quillVolley || b.quillVolley, volleyMult = Mathf.Max(a.volleyMult, b.volleyMult),
            bleedResist = a.bleedResist || b.bleedResist,
        };
    }

    static readonly Organ EmptyOrgan = new(); // «нет базы»: бленд от нуля = чистый орган × мощь

    // родной орган шасси для типа слота — ТО, ЧТО ДОНОРСКИЙ ВЫТЕСНЯЕТ (база экспрессии).
    // Живёт в теле, а не полем слота: база — функция шасси, слот её только использует
    Organ ChassisOrgan(string slotName)
    {
        if (chassis == null || chassis.organs == null) return null;
        foreach (var o in chassis.organs) if (o.slot == slotName) return o;
        return null;
    }

    /// <summary>ЕДИНОЕ ПРАВИЛО ЭКСПРЕССИИ для любого органа в любом слоте (было двумя копипаст-ветками).
    /// База — то, что орган ВЫТЕСНИЛ:
    ///  • РОДНОЙ орган шасси не вытесняет ничего (он и есть оригинал) → раскрывается ОТ СЕБЯ: величины ×мощь;
    ///  • ДОНОРСКИЙ вытесняет родной орган этого слота → блендится ОТ НЕГО (низкое родство ≈ человек,
    ///    высокое ≈ зверь, овершут — за зверя). В ХИМЕРНОМ слоте вытеснять нечего (орган ДОПОЛНИТЕЛЬНЫЙ) → от нуля.
    /// Режим решает `native`, а НЕ «есть ли база»: иначе донорский орган в химерном слоте поменял бы
    /// поведение времён и вне-боевого регена (там база пуста, но раскрытие всё равно «графтовое»).</summary>
    Contribution Express(Slot sl)
    {
        var pick = sl.Pick;
        Organ w = pick.organ;
        bool own = pick.native;   // родной орган шасси — раскрывается от себя
        Organ h = own || sl.chimera ? EmptyOrgan : ChassisOrgan(sl.name) ?? EmptyOrgan;
        float m = BonusMultiplier(pick.species); // у родного варианта species = вид шасси → та же ручка

        float Scaled(float hv, float wv) => own ? wv * m : Blend(hv, wv, m);
        float Timed(float hv, float wv) => own ? wv : Blend(hv, wv, m); // СВОЁ время не растягиваем: ×2 на кулдаун = наказание за свой вид

        return new Contribution
        {
            dmg = Scaled(h.damage, w.damage),
            hpBonus = Scaled(h.hpBonus, w.hpBonus), // ДОЛЯ базы шасси — экспрессия раскрывает бонус, не тело
            stam = Scaled(h.staminaBonus, w.staminaBonus),
            stamRegen = Scaled(h.staminaRegenBonus, w.staminaRegenBonus),
            life = Scaled(h.lifeSteal, w.lifeSteal),
            atkCd = Timed(h.atkCooldown, w.atkCooldown),
            mv = Scaled(h.moveSpeed, w.moveSpeed),
            dash = Scaled(h.dashSpeed, w.dashSpeed),
            dashCd = Timed(h.dashCooldown, w.dashCooldown),
            reduce = Scaled(h.damageReduction, w.damageReduction),
            regen = Scaled(h.regen, w.regen),
            // вне-боя реген: у ДОНОРСКОГО как есть (бленд на мощи 2 уводит в минус), у РОДНОГО раскрываем —
            // иначе человеческое сердце единственное не растёт с родством к своему виду
            regenOOC = own ? w.regenOOC * m : w.regenOOC,
            // ДИСКРЕТНОЕ — всегда у надетого как есть: фичи не «раскрываются», они либо есть, либо нет
            venom = w.venomStacks, bleed = w.bleedStacks,
            rng = w.range, dashDur = w.dashDuration, thermal = w.thermalRange,
            howlR = w.howlRadius, howlStunAt = w.howlStunAt,
            bite = w.enablesBite, scent = w.enablesScent, kick = w.enablesKick,
            howl = w.enablesHowl, cold = w.coldBlooded, camo = w.camo, thermalOn = w.enablesThermal,
            constrict = w.enablesConstrict, digest = w.digestion, bellow = w.enablesBellow,
            antler = w.enablesAntler, charge = w.enablesCharge, roll = w.enablesRoll, insight = w.insight,
            keenEar = w.keenHearing, earMult = w.hearingMult,
            thorns = w.thorns, venomResist = w.venomResist, quillVolley = w.enablesQuillVolley,
            volleyMult = w.enablesQuillVolley ? m : 0f, // мощь залпа = экспрессия органа-придатка (родство с ежом)
            bleedResist = w.bleedResist,
            constrictNative = w.enablesConstrict && chassis != null && w.nativeChassis == chassis.speciesName,
        };
    }

    void Recompute()
    {
        if (slots == null || slots.Length == 0) return; // нет данных — не трогаем статы компонентов

        // Вклады группируются по РОДНОМУ ТИПУ СЛОТА органа: дубли (второе Сердце в химерном слоте)
        // схлопываются супремумом, группы суммируются. Без дублей == прежняя сумма по слотам.
        var groups = new Dictionary<string, Contribution>();
        int beast = 0;

        foreach (var sl in slots)
        {
            if (sl.Empty) continue;      // пустой химерный слот — вклада нет
            var c = Express(sl);         // ОДНО правило раскрытия на все случаи (см. Express)
            if (sl.Installed) beast++;   // шкалу мозга двигает только ЗВЕРИНЫЙ орган: родной — не химеризация
            string key = sl.Worn.slot;   // дубль типа (второе Сердце) идёт в ту же группу — супремум
            groups[key] = groups.TryGetValue(key, out var prev) ? Contribution.Sup(prev, c) : c;
        }

        // суммирование групп; урон группы «Пасть» принадлежит УКУСУ, не мечу
        float dmgF = 0f, dmgBiteF = 0f, hpBonusF = 0f, stamF = 0f, stamRegF = 0f, lifeF = 0f;
        float rng = 0f, atkCd = 0f, mv = 0f, dash = 0f, dashDur = 0f, dashCd = 0f, reduce = 0f, regen = 0f, regenOOC = 0f, thermal = 0f, howlR = 0f, howlStunAt = 0f;
        int venom = 0, bleed = 0;
        bool biteOn = false, scentOn = false, kickOn = false, howlOn = false, coldOn = false, camoOn = false,
             thermalOn = false, constrictOn = false, digestOn = false, bellowOn = false, antlerOn = false, chargeOn = false, rollOn = false,
             constrictNativeOn = false, insightOn = false, keenEarOn = false,
             thornsOn = false, venomResistOn = false, quillVolleyOn = false, bleedResistOn = false;
        float volleyMult = 0f;
        float earMult = 0f;
        foreach (var kv in groups)
        {
            var c = kv.Value;
            if (kv.Key == "Пасть") dmgBiteF += c.dmg; else dmgF += c.dmg;
            hpBonusF += c.hpBonus; stamF += c.stam; stamRegF += c.stamRegen; lifeF += c.life; venom += c.venom; bleed += c.bleed;
            rng += c.rng; atkCd += c.atkCd; mv += c.mv; dash += c.dash; dashDur += c.dashDur; dashCd += c.dashCd;
            reduce += c.reduce; regen += c.regen; regenOOC += c.regenOOC; thermal += c.thermal;
            howlR = Mathf.Max(howlR, c.howlR);
            howlStunAt = Mathf.Max(howlStunAt, c.howlStunAt);
            biteOn |= c.bite; scentOn |= c.scent; kickOn |= c.kick; howlOn |= c.howl;
            coldOn |= c.cold; camoOn |= c.camo; thermalOn |= c.thermalOn; constrictOn |= c.constrict;
            digestOn |= c.digest; bellowOn |= c.bellow; antlerOn |= c.antler; chargeOn |= c.charge; rollOn |= c.roll;
            constrictNativeOn |= c.constrictNative; insightOn |= c.insight;
            keenEarOn |= c.keenEar; earMult = Mathf.Max(earMult, c.earMult);
            thornsOn |= c.thorns; venomResistOn |= c.venomResist; quillVolleyOn |= c.quillVolley;
            bleedResistOn |= c.bleedResist;
            volleyMult = Mathf.Max(volleyMult, c.volleyMult);
        }
        int dmg = Mathf.RoundToInt(dmgF), dmgBite = Mathf.RoundToInt(dmgBiteF);
        int life = Mathf.RoundToInt(lifeF);

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
        float voiceMult = Mathf.Max(1f, Power); // радиус: норму вниз не штрафуем (взрослый волк воет как волк)
        float howlReach = howlR * voiceMult;
        // ПОРОГ-ФИЧА (3-я ось экспрессии): стан открывается, только если МОЩЬ носителя доросла до порога органа.
        // Рядовой волк (Э 0.45) лишь зовёт стаю; вервольф (Э 2) и игрок на 100 родства — глушат. Через данные,
        // без флагов «это игрок»: один вой на всех, разница — в составе носителя
        HowlStuns = howlStunAt > 0f && Power >= howlStunAt;
        if (howl != null) { howl.HowlEnabled = howlOn; howl.SetReach(howlReach); howl.StunUnlocked = HowlStuns; }
        if (constrictAb != null)
        {
            constrictAb.ConstrictEnabled = constrictOn;               // обхват — фича Хвоста (химерный слот)
            constrictAb.SetMaxStage(constrictNativeOn ? 3 : 2);       // РОДНОЕ ШАССИ (nativeChassis): своё → ст.3 (партер+чок), чужое → кап ст.2
        }
        if (bellowAb != null) bellowAb.BellowEnabled = bellowOn;             // РЁВ — фича Глотки лося (K2)
        if (antlerAb != null) antlerAb.AntlerEnabled = antlerOn;             // РОГА — фича придатка «Рога» (химерный слот)
        if (chargeAb != null) chargeAb.ChargeEnabled = chargeOn;             // ТАРАН — фича «Лосиных ног» (рывок горит)
        // ПЕРЕКАТ — КРОСС-СЛОТ СЕТ (спека §0-бис «дожд-ролл с иглами»): Ноги дают ФОРМУ (кувырок+i-frames),
        // Шкура-иглы дают ЖАЛО. Колется только при обоих; одни ноги = защитный уворот без урона (i-frames
        // рывка и так у любых ног). У ежа-NPC иглы есть всегда → его перекат всегда колючий
        if (rollAb != null) rollAb.RollEnabled = rollOn && thornsOn;
        if (volleyAb != null) { volleyAb.VolleyEnabled = quillVolleyOn; volleyAb.SetPower(volleyMult); } // ЗАЛП — фича придатка «Игломёт»; мощь растёт с родством к ежу
        if (satietyComp != null) satietyComp.SetMetabolism(Homogeneity); // МЕТАБОЛИЗМ по тирам: чистый держит сытость дольше, химера сгорает
        SetColdBlooded(coldOn); // холоднокровность (Сердце змеи): невидимость для термозрения врагов
        SetCamouflage(camoOn);  // камуфляж (Чешуя змеи): невидимость в неподвижности
        DigestsWhole = digestOn; // «глотает целиком» (Тело-хвост змеи): убил → ПОЛНАЯ сытость (см. CreditKiller)
        SetThorns(thornsOn);          // иглы (Шкура ежа): ударил в упор — порезался
        SetVenomResist(venomResistOn); // ядоупорность (Сердце ежа): яд не накапливается
        SetBleedResist(bleedResistOn); // кровеупорность (Лосиное сердце): кровь не накапливается
        if (move != null) // чувства игрока меняет ТОЛЬКО тело игрока (NPC-тело не должно включать их игроку)
        {
            Perception.WolfScent = scentOn;
            Perception.SnakeThermal = thermalOn; // термозрение (Пит-орган): тепло сквозь стены
            Perception.ThermalRange = thermal;
            Perception.Insight = insightOn;      // ЧУТЬЁ УЧЁНОГО: распознавание намерений + числа состояний
            Perception.KeenHearing = keenEarOn;  // ОСТРЫЙ СЛУХ: различение вида + ВОЛНЫ звука на экране

            // ПРОФИЛЬ ЧУВСТВ ИГРОКА — от сборки, как у любого существа: зрение при тебе всегда (глаза),
            // запах и тепло открывают органы слота Чутьё. Снял орган — канал закрылся, картина мира сузилась
            if (senses != null)
            {
                senses.Set(SenseKind.Sight, SightRange);
                // уши, как и глаза, при тебе всегда; лосиный орган их УСИЛИВАЕТ (×hearingMult), а не открывает
                senses.Set(SenseKind.Hearing, HearingRange * (earMult > 0f ? earMult : 1f));
                senses.Set(SenseKind.Scent, scentOn ? ScentRange : 0f);
                senses.Set(SenseKind.Thermal, thermalOn ? thermal : 0f);
            }
        }
        if (attack != null)
        {
            attack.SetMelee(dmg, Mathf.Max(0.5f, rng));
            attack.SetCooldown(Mathf.Max(minAtkCooldown, atkCd)); // пол — глушим овершут скорострельности
            attack.SetLifeSteal(life);
        }
        if (move != null)
        {
            move.SetLegs(mv, dash, dashDur);
            move.SetDashCooldown(Mathf.Max(0.05f, dashCd));
        }
        if (health != null && applyVitals)
        {
            // ВИТАЛЬНОСТЬ = БАЗА ШАССИ × (1 + бонусы органов × экспрессия) × разброс особи.
            // База — «тело как таковое», её экспрессия НЕ трогает: раньше всё было абсолютами и
            // масштабировалось целиком, отчего «Э 0.45» значило «волк на 45% статов» (полудохлый),
            // а числа органов подбирались, лишь бы это скомпенсировать. Теперь Э честно раскрывает БОНУС
            int hp = Mathf.RoundToInt(BaseHp * (1f + hpBonusF) * (variance != null ? variance.HpMult : 1f));
            health.SetMaxHealth(Mathf.Max(1, hp));
            health.DamageReduction = Mathf.Min(maxDamageReduction, Mathf.Clamp01(reduce)); // потолок — глушим овершут брони
            health.RegenPerSecond = regen;
            health.OutOfCombatRegen = regenOOC;
        }

        // СТАМИНА — по той же формуле и у ВСЕХ тел (кор-механика, не фича вида). Компонент до-создаём сами:
        // бак обязан быть у каждого существа, иначе потребители (рывок, таран, клубок) пришлось бы обвешивать
        // проверками «а есть ли дыхалка» — а это ровно то, как расползаются правила
        if (stamina == null && !TryGetComponent(out stamina)) stamina = gameObject.AddComponent<Stamina>();
        if (applyVitals)
        {
            stamina.SetMax(BaseStamina * (1f + stamF));
            stamina.RegenPerSecond = BaseStaminaRegen * (1f + stamRegF);
        }

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

    // ИГЛЫ как компонент: тем же паттерном — снял Шкуру ежа, и ответка исчезла вместе с ней
    void SetThorns(bool on)
    {
        if (on && thornsComp == null) thornsComp = gameObject.AddComponent<Thorns>();
        else if (!on && thornsComp != null) { Destroy(thornsComp); thornsComp = null; }
    }

    // ЯДОУПОРНОСТЬ как маркер: опрашивается ядом при добавлении стака (по образцу ColdBlooded)
    void SetVenomResist(bool on)
    {
        if (on && venomResistComp == null) venomResistComp = gameObject.AddComponent<VenomResist>();
        else if (!on && venomResistComp != null) { Destroy(venomResistComp); venomResistComp = null; }
    }

    // КРОВЕУПОРНОСТЬ как маркер: опрашивается кровотечением при добавлении стака (зеркало ядоупорности)
    void SetBleedResist(bool on)
    {
        if (on && bleedResistComp == null) bleedResistComp = gameObject.AddComponent<BleedResist>();
        else if (!on && bleedResistComp != null) { Destroy(bleedResistComp); bleedResistComp = null; }
    }

    // камуфляж-в-неподвижности как компонент: вешаем/снимаем по итогу сборки (живо на смене Шкуры у игрока)
    void SetCamouflage(bool on)
    {
        if (on && camoComp == null) camoComp = gameObject.AddComponent<Camouflage>();
        else if (!on && camoComp != null) { Destroy(camoComp); camoComp = null; }
    }

    // переваривание как компонент-маркер: физиология змеиного шасси (chassisOnly — аугументом не крадётся)
    // единый вход сытости: до-создаёт шкалу Satiety на теле и наполняет её долей (см. CreditKiller).
    // Одна ось сытости-голода на всех, кто ест (хищник — убийством, травоядный — обгладыванием, M2)
    static void Feed(CreatureBody body, float amount) =>
        (body.GetComponent<Satiety>() ?? body.gameObject.AddComponent<Satiety>()).Feed(amount);

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
            if (sl.Empty) continue;                                                  // пустой химерный слот не считаем
            Color t = sl.Installed ? SpeciesTint(sl.DonorSpecies)                    // звериный орган → тинт его вида
                                   : (chassis != null ? chassis.tint : Color.gray);  // родной → телесный
            r += t.r; g += t.g; b += t.b; n++;
        }
        return n > 0 ? new Color(r / n, g / n, b / n) : (chassis != null ? chassis.tint : Color.gray);
    }

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

    /// <summary>ЕДИНЫЙ ГЛАГОЛ РОДСТВА: как `observer` признаёт вид (шасси) существа `target`. None, если один
    /// из тел отсутствует или у цели нет шасси. Заменяет дословный повтор `PlayerBody.Tier(body.Chassis)`,
    /// разбросанный по психикам (волк/лось) и голосам (вой/рёв) — теперь правило признания живёт ОДНИМ местом
    /// (задел под термо-поправку змеи: у неё «свой/добыча» по теплу — ляжет сюда же).</summary>
    public static KinTier Regard(CreatureBody observer, CreatureBody target) =>
        observer != null && target != null && target.Chassis != null ? observer.Tier(target.Chassis) : KinTier.None;

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

/// <summary>
/// НПС-потребитель статов тела: психика получает деривированное из органов (урон, скорость, эффекты
/// укуса, ГОЛОС — радиус воя × мощь; 0 = Пасть не воет) и раздаёт своим доставкам/приёмам.
/// Витальность (HP/броня/реген) — отдельно («конституция», applyVitals).
/// </summary>
public interface IBodyStatConsumer
{
    void OnBodyStats(int damage, float moveSpeed, int venom, int bleed, float howlRange);
}
