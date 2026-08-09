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

    // — РОДСТВО (аффинити: кривые скидки/мощи, словарь, Power) вынесено в CreatureBody.Affinity.cs (partial-split #2) —

    [Header("Капы овершута мощи (глушим 2з−ч на 100 родства)")]
    [SerializeField] float maxDamageReduction = 0.6f;   // потолок брони (иначе Blend(0,0.4,2)=0.8 = 80% резист)
    [SerializeField] float minAtkCooldown = 0.2f;       // пол скорострельности (иначе волчье сердце 0.30→0.15 = пулемёт)

    // — ХИМЕРНЫЕ СЛОТЫ (chimeraSlots/chimeraSlotMult) вынесены в CreatureBody.Slots.cs (partial-split #5) —

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

    // — ТИПЫ Slot/Variant + поле slots вынесены в CreatureBody.Slots.cs (partial-split #5) —
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

    [SerializeField] float assistFeedRadius = 14f; // сытость помощникам: тела своего вида в этом радиусе от убийцы делят добычу (радиус стаи)
    const float KillMeal = 0.7f;                   // насколько убийство наполняет ШКАЛУ сытости (0..1); глотающий целиком — на всю (1)

    public static CreatureBody PlayerBody { get; private set; } // тело ИГРОКА: HUD/dev/спавнеры читают его родство

    /// <summary>Дорос ли носитель до СТАНА в вое (порог задан органом Пасти, см. Organ.howlStunAt).</summary>
    public bool HowlStuns { get; private set; }

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

        // ЛИЦО игрока (глаза/брови/борода из PlayerModel) тинтом состава НЕ красим — черты остаются читаемыми
        // ЗУБЫ тоже вне тинта: у модели игрока они отдельной деталью со своим костяным материалом, и
        // без исключения зеленели бы вместе с телом — оскал должен читаться на любом составе
        renderers = System.Array.FindAll(GetComponentsInChildren<Renderer>(), r =>
            r.name != "EyeL" && r.name != "EyeR" && r.name != "BrowL" && r.name != "BrowR"
            && r.name != "Beard" && r.name != "Teeth");
        mpb = new MaterialPropertyBlock();
    }

    void Start() => Recompute();

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
             insightOn = false, keenEarOn = false,
             thornsOn = false, venomResistOn = false, quillVolleyOn = false, bleedResistOn = false, curlOn = false;
        float volleyMult = 0f;
        float earMult = 0f;
        int constrictCap = 0; // макс кап стадии захвата среди надетых грэпл-органов (0 = захвата нет)
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
            digestOn |= c.digest; bellowOn |= c.bellow; antlerOn |= c.antler; chargeOn |= c.charge; rollOn |= c.roll; curlOn |= c.curl;
            constrictCap = Mathf.Max(constrictCap, c.constrictCap); insightOn |= c.insight;
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
        if (constrictAb != null) // ИГРОК: драйвер PlayerConstrict оборачивает машину
        {
            constrictAb.ConstrictEnabled = constrictOn;               // обхват — фича Хвоста (химерный слот)
            constrictAb.SetMaxStage(Mathf.Max(1, constrictCap));      // кап из данных: constrictStage органа × nativeChassis (свой → полный, чужой → min 2)
        }
        // NPC-ПРОВИЗИЯ ЗАХВАТА: тело гарантирует машину и кап из данных (constrictStage×nativeChassis).
        // Психика перестала капить — только драйвит; get-or-add робастен к порядку Awake/Start, чужеродной химере даёт хват по её органу
        else if (move == null && constrictOn)
        {
            if (!TryGetComponent<Constrict>(out var grabM)) grabM = gameObject.AddComponent<Constrict>();
            grabM.SetMaxStage(Mathf.Max(1, constrictCap));
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
        SetCurl(curlOn);              // клубок (chassisOnly-орган ежа): тело вешает CurlDefense, психика драйвит
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

        // МОРФОЛОГИЯ (ось 2): пересобрать куб-модель из состава (слоты шасси раньше химерных → шасси-фёрст) +
        // пере-собрать renderers (морф-части новые), чтобы тинт их покрасил. Только у видов со скелетом (Волк/Человек)
        // МОРФОЛОГИЯ: NPC/химеры собираются кубами из состава; игрок — своя PlayerModel (worn=null → Build лишь
        // СНОСИТ старый Morph, если остался от прежнего билда, и не строит). Гейт по скелету (Волк/Человек).
        // !installAllBeast: вервольф-босс остаётся при своей РУЧНОЙ детальной модели (WerewolfPrefab), не морфим
        if (!installAllBeast && chassis != null && chassis.sockets != null && chassis.sockets.Length > 0)
        {
            var worn = new System.Collections.Generic.List<Organ>();
            foreach (var sl in slots) if (!sl.Empty && sl.Worn != null) worn.Add(sl.Worn); // слоты шасси раньше химерных → шасси-фёрст
            MorphBuilder.Build(transform, chassis, worn); // ИГРОК СТРОИТСЯ ТАК ЖЕ: его тело — такая же химера, без исключений
            renderers = System.Array.FindAll(GetComponentsInChildren<Renderer>(), r =>
                r.name != "EyeL" && r.name != "EyeR" && r.name != "BrowL" && r.name != "BrowR"
                && r.name != "Beard" && r.name != "Teeth");
            if (TryGetComponent<Telegraph>(out var tg)) tg.RebuildRenderers(); // морф-части новые → телеграф пере-соберёт (иначе замах не красится)
            if (TryGetComponent<SnakeBodyChain>(out var chain)) chain.RebuildFromMorph(); // [ANIM] цепь тела берёт новые звенья (и заново гасит свои коллайдеры)
            if (move != null) move.ReapplyFirstPerson(); // и своя голова снова спрятана от первого лица (части-то новые)
        }

        if (move != null || tintComposition) UpdateTint(); // игрок ВСЕГДА; NPC — только тест-химера (флаг tintComposition); обычные NPC — запечённый материал (не драться с Telegraph)

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

    // КЛУБОК как компонент: тело вешает/снимает CurlDefense по флагу enablesCurl (chassisOnly-орган ежа).
    // Провизия здесь, РЕШЕНИЯ (когда свернуться/катиться) — на психике ежа. Без кэш-поля: носитель один
    void SetCurl(bool on)
    {
        if (on && !TryGetComponent<CurlDefense>(out _)) gameObject.AddComponent<CurlDefense>();
        else if (!on && TryGetComponent<CurlDefense>(out var c)) Destroy(c);
    }

    // МАССА как маркер: тело ДОБАВЛЯЕТ Massive по флагу шасси (лось). ADD-ONLY, НЕ снимает: масса — статичное
    // свойство шасси (в MVP не меняется), а на боссах с ОБЩИМ шасси (вервольф на человечьем) Massive висит с
    // ПРЕФАБА — снятие по флагу=false сорвало бы его. Потребители (Knockback/Constrict/RequiredPack/змея) уже чекают GetComponent<Massive>
    void SetMassive(bool on)
    {
        if (on && !TryGetComponent<Massive>(out _)) gameObject.AddComponent<Massive>();
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

    // ─── ПАЛИТРА ТЕЛА (UpdateTint/CompositionTint) вынесена в CreatureBody.Tint.cs (partial-split #4) ───
    // ─── ВИДОВАЯ ИДЕНТИЧНОСТЬ вынесена в CreatureBody.Identity.cs (partial-split #1, поведение то же) ───

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
