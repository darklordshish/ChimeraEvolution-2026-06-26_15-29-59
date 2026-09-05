using System.Collections.Generic;
using UnityEngine;

// PARTIAL-SPLIT #5 (рефактор ядра): СЛОТЫ/КОНСТРУКТОР вынесены сюда из CreatureBody.cs — тот же класс,
// НОЛЬ изменений поведения. Концерн: «из чего собирается тело» — типы Slot/Variant, сборка слотов из шасси+
// доноров (BuildSlots), экономика пула (Pool/SlotCost/EffectiveCost), публичный API конструктора (GetSlot/
// GetVariants/Install/Remove/цикл), химерные слоты (мета-награда). Читает chassis/donors/installAllBeast и
// зовёт Recompute() — всё в CreatureBody.cs (partial-доступ); скидку родства берёт из CreatureBody.Affinity.cs.
public partial class CreatureBody
{
    [Header("Химерные слоты (мета: награда суперхимеры)")]
    [SerializeField, Min(0)] int chimeraSlots;    // выданные универсальные слоты (dev-кнопка / SuperBossReward)
    [SerializeField] float chimeraSlotMult = 2f;  // множитель цены органа в химерном слоте (не-нативный «графт»)

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
            bool dup = WornElsewhere(sl, vr);
            res.Add(new VariantView
            {
                organName = vr.organ.organName,
                species = vr.species,
                slotType = vr.organ.slot,   // РОДНОЙ тип слота органа — для группировки звёзд по строкам-слотам
                cost = CostOf(sl, vr),
                native = vr.native,
                worn = sl.current == v,
                duplicate = dup,
                affordable = !dup && CanInstall(sl, v),
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
            if (w == null || v == null || w.organ == null || v.organ == null) continue;
            if (w.organ.organName == v.organ.organName && w.organ.slot == v.organ.slot && w.species == v.species) return true;
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
}
