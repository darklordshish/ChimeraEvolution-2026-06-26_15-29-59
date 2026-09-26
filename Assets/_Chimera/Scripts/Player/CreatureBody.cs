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
public partial class CreatureBody : MonoBehaviour
{
    [Header("Виды")]
    [SerializeField] SpeciesSO chassis;    // базовое тело (MVP: Человек) — слоты, пул, дефолт-органы
    [SerializeField] SpeciesSO[] donors;   // доноры органов (MVP: [Волк])

    // s6: ТОЧКА ВОЗВРАТА — логово-дом (ставит спавнер при рождении; пилот психики ведёт сюда сытого).
    // Не сериализуем в префаб: дом — состояние забега, а не вида.
    [System.NonSerialized] public LairSite home;

    // — РОДСТВО (аффинити: кривые скидки/мощи, словарь, Power) вынесено в CreatureBody.Affinity.cs (partial-split #2) —

    [Header("Капы овершута мощи (глушим 2з−ч на 100 родства)")]
    [SerializeField] float maxDamageReduction = 0.6f;   // потолок брони (иначе Blend(0,0.4,2)=0.8 = 80% резист)
    [SerializeField] float minAtkCooldown = 0.2f;       // пол скорострельности (иначе волчье сердце 0.30→0.15 = пулемёт)

    // — ХИМЕРНЫЕ СЛОТЫ (chimeraSlots/chimeraSlotMult) вынесены в CreatureBody.Slots.cs (partial-split #5) —

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
                                              // >0 фикс: природный волк ~0.45 (без сыворотки); 2 = потолок игрока
    [SerializeField] bool applyVitals = true; // false: HP/броня/реген — «конституция» психики, тело их не трогает

    // — ТИПЫ Slot/Variant + поле slots вынесены в CreatureBody.Slots.cs (partial-split #5) —
    PlayerAttack attack;
    PlayerController move;
    Health health;
    Stamina stamina;   // бак дыхалки — кор-механика у ВСЕХ тел, как и Health
    SpawnVariance variance; // разброс особи: HP учитываем при раздаче витальности (иначе гонка Start'ов)
    Bossness bossness;      // модуль боссовости: множитель HP опрашиваем так же, как разброс
    ColdBlooded cold;       // холоднокровность (Сердце змеи) — компонент-маркер, вешаем/снимаем по сборке
    Camouflage camoComp;    // камуфляж-в-неподвижности (Чешуя змеи) — вешаем/снимаем по сборке
    Thorns thornsComp;      // иглы-ответка (Шкура ежа) — тем же паттерном
    VenomResist venomResistComp; // ядоупорность (Сердце ежа)
    BleedResist bleedResistComp; // кровеупорность (Лосиное сердце)
    Satiety satietyComp;    // шкала сытости-голода — у любого тела; распад зависит от однородности (метаболизм химеры)
    Betrayal betrayal;      // подрыв признания: удар по кину копит эрозию (только у игрока)
    Senses senses;          // профиль чувств ИГРОКА (каналы от сборки); у NPC он приходит с префаба
    // ГЛОТАЕТ ЦЕЛИКОМ (Тело-хвост змеи, chassisOnly): убитая добыча даёт ПОЛНУЮ сытость (крупная трапеза),
    // а не долю. Само лечение — общий `Satiety` (переваривание больше не отдельный компонент); поведение
    // «прячусь переваривать на насест» — на психике змеи (читает Satiety.IsSated)
    public bool DigestsWhole { get; private set; }

    [SerializeField] float assistFeedRadius = 14f; // сытость помощникам: тела своего вида в этом радиусе от убийцы делят добычу (радиус стаи)
    const float KillMeal = 0.7f;                   // насколько убийство наполняет ШКАЛУ сытости (0..1); глотающий целиком — на всю (1)

    public static CreatureBody PlayerBody { get; private set; } // тело ИГРОКА: HUD/dev/спавнеры читают его родство
    public SpeciesSO[] Donors => donors; // из кого собираются химеры «такие же, как игрок» (босс: Человек + его доноры)


    // ─── СЛОТЫ/КОНСТРУКТОР (типы Slot/Variant, поле slots, экономика пула, публичный API конструктора,
    //     BuildSlots, Toggle) вынесены в CreatureBody.Slots.cs (partial-split #5, поведение то же) ───

    void Awake()
    {
        // тело не предполагает игрока: берём тех потребителей, какие есть на объекте
        TryGetComponent(out attack);
        TryGetComponent(out move);
        // авто-РАЗБРОС ПОВЕДЕНИЯ: любой NPC-вид (не игрок) получает Личность от ТЕЛА — психики её только ЧИТАЮТ
        // (в Start, после этого Awake). Новый вид разбрасывается сам, без ручной проводки в каждой психике.
        if (move == null && !TryGetComponent<Personality>(out _)) gameObject.AddComponent<Personality>();
        // РАЗБРОС ОСОБИ — тоже от тела, каждому NPC, а не тем психикам, что про него помнят: раньше его тянули волчья,
        // лосиная и змеиная, а ёж из диспатча и истинные химеры оставались клонами. Босса делает штучным Bossness
        if (move == null && !TryGetComponent<SpawnVariance>(out _)) gameObject.AddComponent<SpawnVariance>();
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
        TryGetComponent(out betrayal);
        if (move != null && betrayal == null) betrayal = gameObject.AddComponent<Betrayal>(); // эрозия признания — только у игрока
        TryGetComponent(out variance);
        TryGetComponent(out cold);
        TryGetComponent(out camoComp);

        // ЭВОЛЮЦИЯ: NPC встаёт на платформу «тело=данные» как игрок — все ветки-доноры + стартовое родство +
        // цвет по составу. Родство — ЕДИНАЯ ось (>0 → ветка доступна). Ставим ДО BuildSlots, чтобы слоты
        // собрались со всеми вариантами. Игрока (move != null) не трогаем — у него donors назначены сборкой
        if (move == null && chassis != null)
        {
            var evoCfg = EvolutionConfig.Instance;
            if (evoCfg != null && evoCfg.EvolveNpc && evoCfg.AllSpecies != null && evoCfg.AllSpecies.Length > 0)
            {
                donors = evoCfg.AllSpecies;                       // все виды — потенциальные доноры (родные надеты → чистый вид)
                tintComposition = true;                           // цвет по составу (развязка Telegraph.Rebase готова)
                foreach (var sp in evoCfg.AllSpecies)
                    if (sp != null && sp != chassis) SetAffinity(sp.speciesName, evoCfg.StartAffinity); // стартовое родство чужим
                if (GetComponent<Metamorph>() == null) gameObject.AddComponent<Metamorph>(); // ре-диспатч психики при сдвиге доминанты
            }
        }

        BuildSlots();

        // РОДСТВО — УБИЙЦЕ: на нашу смерть кредитуем ТОГО, КТО УБИЛ (см. CreditKiller)
        if (health != null) health.onDeath.AddListener(CreditKiller);

        // каждое существо рождается с полным родством к СВОЕМУ шасси (волк уверен в своей волчности;
        // мета-шкала человечности игрока — потом, при социальном слое)
        if (chassis != null) SetAffinity(chassis.speciesName, AffinityCap);
        if (move != null) PlayerBody = this;

        // ЦВЕТ — ЧЕРЕЗ МИКШЕР СЛОЁВ. Прежде тут собирался массив рендереров с исключениями ПО ИМЕНАМ
        // (EyeL/BrowR/Teeth — «черты лица не красить составом»), и список умер молча: морф-части
        // именуются по СОКЕТАМ, таких имён у них нет вовсе. Теперь деталь носит свой паспорт (`PartMark`),
        // а микшер сам решает, что чем красить — исключений не осталось (спека «язык цвета»)
        mixer = GetComponent<TintMixer>();
        if (mixer == null) mixer = gameObject.AddComponent<TintMixer>();
    }

    void Start()
    {
        // ШАССИ ПРОВЕРЯЕМ НА ПЕРВОМ КАДРЕ, А НЕ В AWAKE: рождённого составом (`ChimeraFactory`) Awake застаёт ещё без
        // шасси — его отдаёт `Configure` следующей строкой после `AddComponent`. Тревога — только если так никто и не дал
        if (chassis == null || chassis.organs == null)
            Debug.LogWarning("CreatureBody: не назначено шасси (SpeciesSO). Конструктор спит — компоненты работают на своих значениях.", this);
        Recompute();
    }

    /// <summary>РАНТАЙМ-СБОРКА (тест-химера / будущая стохастическая химеризация NPC): задать шасси+доноров и
    /// пересобрать. Обычные тела конфигурятся сериализацией (префаб/бутстрап); это — для рождённых на лету.
    /// tintFromComposition — красить тело смесью тинтов состава (заглушка-сфера без Telegraph, как тело игрока).</summary>
    public void Configure(SpeciesSO newChassis, SpeciesSO[] newDonors, bool tintFromComposition = false)
    {
        chassis = newChassis;
        donors = newDonors;
        tintComposition = tintFromComposition;
        BuildSlots();
        Recompute();
    }

    /// <summary>ЭКСПРЕССИЯ ИЗВНЕ — для модуля боссовости: босс раскрывает гены органов до потолка игрока, не заводя
    /// своего пути сборки. Ноль — «как задано по умолчанию» (у NPC — рядовая особь).</summary>
    public void SetExpression(float value) { expression = Mathf.Max(0f, value); Recompute(); }

    /// <summary>Пере-раздать статы компонентам (урон/скорость через OnBodyStats, HP и т.д.). Для психики,
    /// НАВЕШЕННОЙ В РАНТАЙМЕ ПОСЛЕ сборки (диспатч добавляет её после Recompute — Feed→OnBodyStats до неё
    /// не дошёл, урон/скорость не получены). Идемпотентно (просто пересчёт).</summary>
    public void Refeed() => Recompute();

    void Update()
    {
        int affSum = AffinitySum();
        if (affSum != lastAffinitySum) { lastAffinitySum = affSum; Recompute(); } // родство выросло → пересчёт
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

            // МАССИВНАЯ добыча (лось) велика — кормит и СТАЮ: кины, участвовавшие в бою (рядом
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

        // РОДСТВО — УБИЙЦЕ по СОСТАВУ трупа: +1 за шасси и КАЖДЫЙ орган (по виду органа) × AffinityPerOrgan.
        // Композиционно: «более волчья» тварь → больше волчьего родства; химера-NPC естественно раздаёт по видам.
        // Было: +1 за уникальный вид (пёстрый пёс = +1) → грайнд 100 killов; теперь ~25 (тюнинг AffinityPerOrgan)
        var tally = new Dictionary<string, int>();
        if (chassis != null) tally[chassis.speciesName] = 1;                    // шасси
        if (slots != null)
            foreach (var sl in slots)
                if (!sl.Empty && sl.Pick != null && sl.Pick.species != null)    // каждый надетый орган — по СВОЕМУ виду
                    tally[sl.Pick.species] = (tally.TryGetValue(sl.Pick.species, out var c) ? c : 0) + 1;
        foreach (var kv in tally)
            killer.AddAffinity(kv.Key, Mathf.Max(1, Mathf.RoundToInt(kv.Value * AffinityPerOrgan))); // ≥1: любой след вида регистрируется

        TryChimerize(killer); // ЭВОЛЮЦИЯ: шанс надеть убийце орган из нашего состава (родство = шанс)
    }

    void OnDestroy() { if (PlayerBody == this) PlayerBody = null; }

    // — СЛОТЫ/КОНСТРУКТОР (BuildSlots/Toggle/…) вынесены в CreatureBody.Slots.cs (partial-split #5) —

    // — ДВИЖОК ЭКСПРЕССИИ (Contribution/Sup/EmptyOrgan/ChassisOrgan/Express) вынесен в CreatureBody.Expression.cs (partial-split #3) —

    // МЕТАМОРФОЗА (эволюция NPC): доминанта состава сменилась → перевесить психику. Тело про психики НЕ знает,
    // только эмитит; слушатель Metamorph ре-диспатчит. lastDominant — для детекции смены в Recompute.
    SpeciesSO lastDominant;
    bool dominantInit;   // первый Recompute задаёт lastDominant БЕЗ метаморфозы (психика уже стоит с префаба/диспатча)
    public System.Action<SpeciesSO> onDominantChanged;

    void Recompute()
    {
        // EditMode: Awake мог не вызваться → поля-компоненты (health etc.) ещё null. Лениво подбираем.
        if (health == null) TryGetComponent(out health);
        if (attack == null) TryGetComponent(out attack);
        if (move == null) TryGetComponent(out move);
        if (variance == null) TryGetComponent(out variance);
        if (bossness == null) TryGetComponent(out bossness);
        if (cold == null) TryGetComponent(out cold);
        if (camoComp == null) TryGetComponent(out camoComp);
        if (mixer == null) mixer = GetComponent<TintMixer>();
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
        float hpBonusF = 0f, stamF = 0f, stamRegF = 0f;
        float atkCd = 0f, mv = 0f, dash = 0f, dashDur = 0f, dashCd = 0f, reduce = 0f, regen = 0f, regenOOC = 0f, thermal = 0f;
        bool scentOn = false, coldOn = false, camoOn = false,
             thermalOn = false, digestOn = false,
             insightOn = false, keenEarOn = false,
             thornsOn = false, venomResistOn = false, bleedResistOn = false;
        float earMult = 0f;
        foreach (var kv in groups)
        {
            var c = kv.Value;
            hpBonusF += c.hpBonus; stamF += c.stam; stamRegF += c.stamRegen;
            atkCd += c.atkCd; mv += c.mv; dash += c.dash; dashDur += c.dashDur; dashCd += c.dashCd;
            reduce += c.reduce; regen += c.regen; regenOOC += c.regenOOC; thermal += c.thermal;
            scentOn |= c.scent;
            coldOn |= c.cold; camoOn |= c.camo; thermalOn |= c.thermalOn;
            digestOn |= c.digest;
            insightOn |= c.insight;
            keenEarOn |= c.keenEar; earMult = Mathf.Max(earMult, c.earMult);
            thornsOn |= c.thorns; venomResistOn |= c.venomResist;
            bleedResistOn |= c.bleedResist;
        }

        // ПРИЁМЫ ИЗ ЗАПИСЕЙ ОРГАНОВ (спека 12.09): раскрыть, свести дубли, накормить носителей — один путь игроку и NPC.
        // Все удары, голос и захват (у NPC носитель — машина Constrict, у игрока — драйвер PlayerConstrict над ней)
        ProvisionAbilities();
        if (satietyComp != null) satietyComp.SetMetabolism(Homogeneity); // МЕТАБОЛИЗМ по тирам: чистый держит сытость дольше, химера сгорает
        SetColdBlooded(coldOn); // холоднокровность (Сердце змеи): невидимость для термозрения врагов
        SetCamouflage(camoOn);  // камуфляж (Чешуя змеи): невидимость в неподвижности
        DigestsWhole = digestOn; // «глотает целиком» (Тело-хвост змеи): убил → ПОЛНАЯ сытость (см. CreditKiller)
        SetThorns(thornsOn);          // иглы (Шкура ежа): ударил в упор — порезался
        SetMassive(chassis != null && chassis.massive); // масса (физ.свойство шасси): тело ДОБАВЛЯЕТ Massive по флагу (add-only — не срывает префаб-массу боссов)
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
        // ТЕМП АТАК от Сердца — свойство тела, модификатор к удару конечностью (числа удара — в записи органа «Руки»).
        // Пол глушит овершут скорострельности
        if (attack != null) attack.SetTempo(Mathf.Max(minAtkCooldown, atkCd));
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
            // × БОССОВОСТЬ: модуль опрашивается при КАЖДОМ пересчёте, а не вписывается в HP разово — иначе первая
            // же прививка или выданный химерный слот стирали бы бонус молча
            int hp = Mathf.RoundToInt(BaseHp * (1f + hpBonusF) * (variance != null ? variance.HpMult : 1f)
                                      * (bossness != null ? bossness.HpMult : 1f));
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

        // НПС-потребители (психика): тело отдаёт деривированное — скорость хода
        // Голос и числа приёмов психике не идут: доставки кормят записи органов, а голос психика читает у тела (Ability<T>)
        foreach (var c in GetComponents<IBodyStatConsumer>()) c.OnBodyStats(mv);

        // МОРФОЛОГИЯ (ось 2): пересобрать куб-модель из состава (слоты шасси раньше химерных → шасси-фёрст) +
        // пере-собрать renderers (морф-части новые), чтобы тинт их покрасил. Только у видов со скелетом (Волк/Человек)
        // МОРФОЛОГИЯ: NPC/химеры собираются кубами из состава; игрок — своя PlayerModel (worn=null → Build лишь
        // СНОСИТ старый Morph, если остался от прежнего билда, и не строит). Гейт по скелету (Волк/Человек).
        if (chassis != null && chassis.sockets != null && chassis.sockets.Length > 0)
        {
            var worn = new System.Collections.Generic.List<Organ>();
            foreach (var sl in slots) if (!sl.Empty && sl.Worn != null) worn.Add(sl.Worn); // слоты шасси раньше химерных → шасси-фёрст
            var blendedPlan = GetBlendedPlan(); // Ф6: смешение по Identity с локальностью (Пасть→голова, Руки/Ноги→хребет исключён); null = тождественность
            MorphBuilder.Build(transform, chassis, worn, blendedPlan); // ИГРОК СТРОИТСЯ ТАК ЖЕ: его тело — такая же химера, без исключений
            // ЧАСТИ НОВЫЕ — ВСЕ, КТО ДЕРЖИТ НА НИХ ССЫЛКИ, ПЕРЕ-СОБИРАЮТСЯ. Ссылка, снятая в Awake, к этому
            // моменту мертва: на этом уже сгорели телеграф (замах не красился) и камуфляж (змея перестала
            // исчезать — прятались префабные меши, которых нет)
            mixer?.Rebuild();
            if (TryGetComponent<Telegraph>(out var tg)) tg.RebuildRenderers(); // морф-части новые → телеграф пере-соберёт (иначе замах не красится)
            if (camoComp != null) camoComp.Rebuild();                          // и камуфляж — иначе прячет пустоту
            if (TryGetComponent<HitFlash>(out var hf)) hf.Rebuild();           // вспышка урона — иначе её нет вовсе
            if (TryGetComponent<HeatSignature>(out var hs)) hs.Rebuild();      // тепловая подпись — иначе термозрение слепо
            if (TryGetComponent<SnakeBodyChain>(out var chain)) chain.RebuildFromMorph(); // [ANIM] цепь тела берёт новые звенья (и заново гасит свои коллайдеры)
            // Здесь стоял вызов JawController.Rebind(). Сам компонент убран 08.09: он не висел ни на одном
            // префабе и ни в одной сцене, то есть не работал никогда, а внутри держал четыре независимых
            // дефекта (брал одну из двух зеркальных костей челюсти, цеплялся к «Morph~dead», зубы за костью
            // не шли — они примитивы органа, а не её потомки). Место под перепривязку челюсти правильное:
            // когда компонент вернётся рабочим, строка встанет ровно сюда, рядом с цепью змеи
            if (move != null) move.ReapplyFirstPerson(); // и своя голова снова спрятана от первого лица (части-то новые)
        }

        // ЦВЕТ ПО СОСТАВУ — У ВСЕХ, БЕЗУСЛОВНО. Это одна из главных индикаторных фич: по цвету читается,
        // из чего тварь собрана, наравне с формой. Прежде тинт стоял за гейтом `move != null ||
        // tintComposition`, то есть красились только игрок и тест-химера, а весь лес жил на запечённом
        // материале — и стоило выключить эволюцию NPC, как звери белели. Гейт был нужен, пока тинт дрался
        // с телеграфом за материал; теперь их развёл микшер (Apply → Rebase → Reapply), драться не за что
        UpdateTint();

        // ВИДОВОЙ ОТПЕЧАТОК В ЗАПАХЕ: след пахнет СОСТАВОМ — красится смесью тинтов шасси+аугументов
        // (природная особь → чистый тинт вида, химера → грязный микс). Волчье Чутьё читает, КТО прошёл,
        // прямо из цвета следа — у всех тел, не только у игрока
        if (TryGetComponent<ScentTrail>(out var scentTrail))
        {
            Color comp = CompositionTint();
            scentTrail.Configure(new Color(comp.r, comp.g, comp.b, 0.65f), move != null);
        }

        // МЕТАМОРФОЗА: целевой модуль психики = доминанта, если она уверенна (Medium+, гистерезис), ИНАЧЕ null
        // (размытие в истинную химеру → альфа). Сменился целевой модуль → сигнал. Первый Recompute — без метаморфозы.
        var dom = MostKin(out var domTier);
        var targetDom = domTier >= KinTier.Medium ? dom : null; // Medium+ → видовой модуль; иначе (None/размытие) → химера-альфа
        if (!dominantInit) { dominantInit = true; lastDominant = targetDom; }
        else if (targetDom != lastDominant)
        {
            lastDominant = targetDom;
            onDominantChanged?.Invoke(targetDom);
        }
    }

    /// <summary>Снос маркера, работающий И В РЕДАКТОРЕ (тот же приём, что `MorphBuilder.Kill`). Карта тел и матрица
    /// химер собирают тело вне Play: химера снимает с ежа Иглы — и `Destroy` компонента шипов писал ошибку в лог.
    /// В Play поведение прежнее: отложенный снос до конца кадра.</summary>
    static void Kill(Object o)
    {
        if (Application.isPlaying) Destroy(o);
        else DestroyImmediate(o);
    }

    // холоднокровность как компонент-маркер: вешаем/снимаем по итогу сборки (живо на смене Сердца у игрока)
    void SetColdBlooded(bool on)
    {
        if (on && cold == null) cold = gameObject.AddComponent<ColdBlooded>();
        else if (!on && cold != null) { Kill(cold); cold = null; }
    }

    // ИГЛЫ как компонент: тем же паттерном — снял Шкуру ежа, и ответка исчезла вместе с ней
    void SetThorns(bool on)
    {
        if (on && thornsComp == null) thornsComp = gameObject.AddComponent<Thorns>();
        else if (!on && thornsComp != null) { Kill(thornsComp); thornsComp = null; }
    }

    // МАССА как маркер: тело ДОБАВЛЯЕТ Massive по флагу шасси (лось). ADD-ONLY, НЕ снимает: масса — статичное
    // свойство шасси (в MVP не меняется). Add-only нужен и сейчас: босс на человечьем шасси
    // получает Massive от модуля боссовости — снятие по флагу шасси сорвало бы его. Потребители (Knockback/Constrict/RequiredPack/змея) уже чекают GetComponent<Massive>
    void SetMassive(bool on)
    {
        if (on && !TryGetComponent<Massive>(out _)) gameObject.AddComponent<Massive>();
    }

    // ЯДОУПОРНОСТЬ как маркер: опрашивается ядом при добавлении стака (по образцу ColdBlooded)
    void SetVenomResist(bool on)
    {
        if (on && venomResistComp == null) venomResistComp = gameObject.AddComponent<VenomResist>();
        else if (!on && venomResistComp != null) { Kill(venomResistComp); venomResistComp = null; }
    }

    // КРОВЕУПОРНОСТЬ как маркер: опрашивается кровотечением при добавлении стака (зеркало ядоупорности)
    void SetBleedResist(bool on)
    {
        if (on && bleedResistComp == null) bleedResistComp = gameObject.AddComponent<BleedResist>();
        else if (!on && bleedResistComp != null) { Kill(bleedResistComp); bleedResistComp = null; }
    }

    // камуфляж-в-неподвижности как компонент: вешаем/снимаем по итогу сборки (живо на смене Шкуры у игрока)
    void SetCamouflage(bool on)
    {
        if (on && camoComp == null) camoComp = gameObject.AddComponent<Camouflage>();
        else if (!on && camoComp != null) { Kill(camoComp); camoComp = null; }
    }

    // переваривание как компонент-маркер: физиология змеиного шасси (chassisOnly — аугументом не крадётся)
    // единый вход сытости: до-создаёт шкалу Satiety на теле и наполняет её долей (см. CreditKiller).
    // Одна ось сытости-голода на всех, кто ест (хищник — убийством, травоядный — обгладыванием, M2)
    static void Feed(CreatureBody body, float amount) =>
        (body.GetComponent<Satiety>() ?? body.gameObject.AddComponent<Satiety>()).Feed(amount);

    // ─── ПАЛИТРА ТЕЛА (UpdateTint/CompositionTint) вынесена в CreatureBody.Tint.cs (partial-split #4) ───
    // ─── ВИДОВАЯ ИДЕНТИЧНОСТЬ вынесена в CreatureBody.Identity.cs (partial-split #1, поведение то же) ───

}

/// <summary>
/// НПС-потребитель статов тела: психика получает от органов скорость хода. Числа приёмов сюда не идут — они в
/// записях органов: доставки и машину захвата кормит провизия, записи голоса психика читает у тела (`Ability<T>`).
/// Витальность (HP/броня/реген) — отдельно («конституция», applyVitals).
/// </summary>
public interface IBodyStatConsumer
{
    void OnBodyStats(float moveSpeed);
}
