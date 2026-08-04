using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Утилита разработки: создаёт/обновляет дефолтные виды (Человек, Волк) как ассеты с готовыми числами
/// и прицепляет их к CreatureBody в открытой сцене. Идемпотентно — повторный запуск обновляет значения
/// существующих ассетов (удобно гонять баланс). Меню: Chimera → Создать дефолтные виды. Editor-only.
/// </summary>
public static class SpeciesBootstrap
{
    const string Dir = "Assets/_Chimera/Data";

    [MenuItem("Chimera/Создать дефолтные виды (Человек, Волк, Змея, Лось, Ёж)")]
    public static void CreateDefaults()
    {
        if (!AssetDatabase.IsValidFolder(Dir))
            AssetDatabase.CreateFolder("Assets/_Chimera", "Data");

        // ── Человек: шасси + органы по умолчанию. Человеческие органы ТОЖЕ занимают пул (цена 2),
        //    чистый человек = 12/16 → свободно 4 = стартовый бюджет химеризации ──
        var human = GetOrCreate("Человек");
        human.speciesName = "Человек";
        human.tint = new Color(0.9f, 0.72f, 0.62f); // телесный — база палитры (все органы человечьи → этот цвет)
        human.mutagenPool = 16;
        human.baseHp = 75;  // БАЗА ТЕЛА (см. CreatureBody: итог = база × (1 + бонусы × экспрессия)).
                            // Человек — эталон калибра: остальные базы читаются относительно него
        human.baseStamina = 100;      // ЧЕЛОВЕК — ФАВОРИТ ДЫХАЛКИ: по HP он слабейший, зато самый
        human.baseStaminaRegen = 14f; // неутомимый. «Остаться человеком» — выбор, а не отказ от силы
        human.organs = new[]
        {
            // Человек = ПОЛНОЦЕННЫЙ вид (просто стартовое шасси). Цены СЫРЫЕ, как у всех; дёшевы ДЛЯ ТЕБЯ
            // потому что ты на 100 родства с Человеком (−80% скидка, честно через EffectiveCost). Мощь ×2 (100
            // родства), база ×0.75 → нетто ≈ ×1.5. Кулдауны/дальность не масштабируются.
            new Organ { organName = "Кисть",  slot = "Руки",   hotkey = "1", cost = 3, damage = 8, range = 1.6f },
            new Organ { organName = "Ноги",   slot = "Ноги",   hotkey = "2", cost = 3, moveSpeed = 4.5f, dashSpeed = 15f, enablesKick = true },
            new Organ { organName = "Сердце", slot = "Сердце", hotkey = "3", cost = 6, atkCooldown = 0.45f, hpBonus = 0.5f, staminaBonus = 0.5f, staminaRegenBonus = 0.5f, regen = 0f, regenOOC = 0.75f }, // внутренний: сокет hidden — места на теле нет
            new Organ { organName = "Чутьё",  slot = "Чутьё",  hotkey = "4", cost = 3, dashCooldown = 0.7f, insight = true }, // внутренний: сокет hidden — места на теле нет
            new Organ { organName = "Рот",    slot = "Пасть",  hotkey = "5", cost = 3, enablesBite = false }, // лицо/пасть — ОТДЕЛЬНО от черепа: волчья Пасть сядет сюда же → морда вервольфа
            new Organ { organName = "Кожа",   slot = "Шкура",  hotkey = "6", cost = 3, damageReduction = 0f },
        };
        // СОКЕТ-ПЛАН человека (прямоходящий). ИМЯ СОКЕТА = Organ.slot — одно и то же имя держит механику и
        // визуал, разойтись не могут. Те же имена у зверей → волчьи органы садятся на человечьи места (вервольф).
        // mirrorX — парное место (2 руки/ноги); hidden — внутренний (места на теле нет); graft — закрытое место
        human.sockets = new[]
        {
            new BodySocket { name = "голова", localPos = new Vector3(0f, 1.70f, 0f),     baseSize = new Vector3(0.32f, 0.36f, 0.32f) }, // телесное место (органа нет)
            new BodySocket { name = "Пасть",  localPos = new Vector3(0f, 1.66f, 0.18f),  baseSize = new Vector3(0.20f, 0.16f, 0.10f) },
            new BodySocket { name = "шея",    localPos = new Vector3(0f, 1.48f, 0f),     baseSize = new Vector3(0.14f, 0.16f, 0.14f) }, // телесное место
            new BodySocket { name = "Шкура",  localPos = new Vector3(0f, 1.12f, 0f),     baseSize = new Vector3(0.48f, 0.72f, 0.28f) },
            new BodySocket { name = "Руки",   localPos = new Vector3(0.31f, 1.15f, 0f),  baseSize = new Vector3(0.13f, 0.62f, 0.15f), mirrorX = true },
            new BodySocket { name = "Ноги",   localPos = new Vector3(0.13f, 0.42f, 0f),  baseSize = new Vector3(0.16f, 0.82f, 0.20f), mirrorX = true },
            new BodySocket { name = "Сердце", hidden = true },
            new BodySocket { name = "Чутьё",  hidden = true },
            // ЗАКРЫТЫЕ МЕСТА (graft — пустыми НЕ рисуются): у человека нет хвоста/рогов/игломёта, но привил
            // змеиный Хвост / лосиные Рога / ежиный Игломёт — и они проступают на теле
            new BodySocket { name = "Хвост",   localPos = new Vector3(0f, 0.85f, -0.24f), baseSize = new Vector3(0.13f, 0.13f, 0.13f), baseEuler = new Vector3(25f, 0f, 0f), graft = true },  // КАЛИБР места; форму (сегментность) несёт орган
            new BodySocket { name = "Рога",    localPos = new Vector3(0.15f, 1.92f, 0.02f), baseSize = new Vector3(0.13f, 0.13f, 0.13f), baseEuler = new Vector3(0f, 0f, 25f), mirrorX = true, graft = true }, // КАЛИБР: НАД макушкой (верх головы 1.88) и наружу — лопасть не врастает в череп
            new BodySocket { name = "Игломёт", localPos = new Vector3(0f, 1.15f, -0.26f), baseSize = new Vector3(0.14f, 0.14f, 0.14f), baseEuler = new Vector3(90f, 0f, 0f), graft = true }, // КАЛИБР + ОРИЕНТАЦИЯ: хребет человека ВЕРТИКАЛЬНЫЙ, поворот 90° ставит плиту органа вдоль позвоночника
        };
        EditorUtility.SetDirty(human);

        // ── Волк: донор органов (абсолютные значения = человек + прежняя дельта) ──
        var wolf = GetOrCreate("Волк");
        wolf.speciesName = "Волк";
        wolf.tint = new Color(0.5f, 0.5f, 0.52f);   // серый — по-волчьи и отличимо от бурого лося
        wolf.mutagenPool = 16;
        wolf.baseHp = 38;   // тело волка вдвое легче человеческого (~40 кг против ~75) — зато сердце зверское
        wolf.baseStamina = 70;       // гончий: дыхалка хорошая, но человеку уступает
        wolf.baseStaminaRegen = 9f;
        wolf.organs = new[]
        {
            new Organ { organName = "Коготь",        slot = "Руки",   hotkey = "1", cost = 4, damage = 18, range = 1.5f, visualScale = new Vector3(1f, 1f, 1.2f) },
            new Organ { organName = "Волчьи ноги",   slot = "Ноги",   hotkey = "2", cost = 4, moveSpeed = 9f, dashSpeed = 30f, visualScale = new Vector3(1f, 1f, 1.2f) },
            new Organ { organName = "Волчье сердце", slot = "Сердце", hotkey = "3", cost = 6, atkCooldown = 0.30f, hpBonus = 1.75f, staminaBonus = 0.5f, staminaRegenBonus = 0.25f, regen = 3f, regenOOC = 0f }, // «заживает как на собаке»: реген 2→3, чтобы босс вернул свои 6/с (Blend на Э=2), а волки затягивали раны на глазах. +175%: лёгкое тело, огромный мотор → волк-NPC 68 HP, вервольф ровно 300. Постоянный реген ВМЕСТО тихого в покое (вне-боя — фича человеческого сердца)
            new Organ { organName = "Нюх",           slot = "Чутьё",  hotkey = "4", cost = 3, dashCooldown = 0.45f, enablesScent = true },
            new Organ { organName = "Пасть",         slot = "Пасть",  hotkey = "5", cost = 5, enablesBite = true, enablesHowl = true, bleedStacks = 2, howlRadius = 14f, howlStunAt = 2f, enablesConstrict = true, constrictStage = 1, nativeChassis = "Волк", visualScale = new Vector3(1.1f, 1f, 1.25f) }, // укус + кровь + ГОЛОС + ХВАТ пастью; МОРДА: на человечьем шасси садится на его «лицо» → морда вервольфа
            new Organ { organName = "Шкура",         slot = "Шкура",  hotkey = "6", cost = 4, damageReduction = 0.3f },
        };
        // СОКЕТ-ПЛАН волка (4-ногий): ТЕ ЖЕ имена, что у человека (имя = Organ.slot), позиции из BuildBlocky.
        // mirrorX даёт ЧЕТЫРЕ лапы и ДВА уха одной записью (раньше пара выглядела единым блоком)
        wolf.sockets = new[]
        {
            new BodySocket { name = "голова", localPos = new Vector3(0f, 1.15f, 0.90f),    baseSize = new Vector3(0.35f, 0.35f, 0.38f) }, // телесное место
            new BodySocket { name = "Пасть",  localPos = new Vector3(0f, 1.11f, 1.20f),    baseSize = new Vector3(0.20f, 0.24f, 0.28f) },
            new BodySocket { name = "уши",    localPos = new Vector3(0.10f, 1.32f, 0.83f), baseSize = new Vector3(0.11f, 0.11f, 0.06f), baseEuler = new Vector3(0f, 0f, 45f), mirrorX = true }, // телесное место
            new BodySocket { name = "шея",    localPos = new Vector3(0f, 0.98f, 0.63f),    baseSize = new Vector3(0.32f, 0.32f, 0.42f), baseEuler = new Vector3(-40f, 0f, 0f) }, // телесное место
            new BodySocket { name = "Шкура",  localPos = new Vector3(0f, 0.78f, 0.08f),    baseSize = new Vector3(0.54f, 0.60f, 1.05f) },
            new BodySocket { name = "Руки",   localPos = new Vector3(0.20f, 0.26f, 0.42f), baseSize = new Vector3(0.14f, 0.48f, 0.15f), mirrorX = true },
            new BodySocket { name = "Ноги",   localPos = new Vector3(0.19f, 0.30f, -0.45f),baseSize = new Vector3(0.17f, 0.55f, 0.20f), mirrorX = true },
            new BodySocket { name = "Хвост",  localPos = new Vector3(0f, 1.00f, -0.62f),   baseSize = new Vector3(0.15f, 0.15f, 0.55f), baseEuler = new Vector3(30f, 0f, 0f) }, // СВОЙ хвост (не графт): крепится сверху крупа, продолжением позвоночника
            new BodySocket { name = "Сердце", hidden = true },
            new BodySocket { name = "Чутьё",  hidden = true },
            // закрытые места (пустыми не рисуются): волк с лосиными рогами / ежиным игломётом читается сразу
            new BodySocket { name = "Рога",    localPos = new Vector3(0.15f, 1.37f, 0.88f), baseSize = new Vector3(0.13f, 0.13f, 0.13f), baseEuler = new Vector3(0f, 0f, 25f), mirrorX = true, graft = true }, // КАЛИБР: над черепом и наружу
            new BodySocket { name = "Игломёт", localPos = new Vector3(0f, 1.18f, 0.05f),    baseSize = new Vector3(0.20f, 0.20f, 0.20f), graft = true }, // КАЛИБР; хребет ГОРИЗОНТАЛЬНЫЙ — доворот не нужен, выше спины (верх туши 1.08)
        };
        EditorUtility.SetDirty(wolf);

        // ── Змея: соло-засадный вид (NPC-шасси; органы в мутагенной шкале, природная особь на Э~0.5) ──
        var snake = GetOrCreate("Змея");
        snake.speciesName = "Змея";
        snake.tint = new Color(0.35f, 0.5f, 0.3f);
        snake.mutagenPool = 20;
        snake.baseHp = 60;  // длинное тело, но лёгкое
        snake.baseStamina = 55;      // засадник: удушение оказалось коротким, прибавка бака не понадобилась
        snake.baseStaminaRegen = 7f;
        snake.organs = new[]
        {
            new Organ { organName = "Ядовитые клыки",       slot = "Пасть",  hotkey = "5", cost = 5, damage = 24, enablesBite = true, venomStacks = 1 }, // укус игрока травит
            new Organ { organName = "Хладнокровное сердце", slot = "Сердце", hotkey = "3", cost = 5, hpBonus = 1.35f, staminaBonus = 0.3f, staminaRegenBonus = 0.2f, regen = 0f, regenOOC = 2f, atkCooldown = 0.5f, coldBlooded = true }, // ХОЛОДНЫЙ МЕТАБОЛИЗМ: в бою НЕ регенит (regen 0), вне боя восстанавливается ЛУЧШЕ человека (regenOOC 2 > 1). Кулдаун ОБЯЗАТЕЛЕН (0 в бленде = меч-пулемёт)
            new Organ { organName = "Тело-хвост",           slot = "Тело",   hotkey = "7", cost = 5, moveSpeed = 10f, dashSpeed = 20f, chassisOnly = true, digestion = true }, // ходовая часть ШАССИ змеи: аугументом не крадётся (локомоция = свойство шасси) + ПЕРЕВАРИВАНИЕ (глотание целиком = свойство змеиного тела)
            new Organ { organName = "Чешуя",                slot = "Шкура",  hotkey = "6", cost = 4, damageReduction = 0.25f, camo = true }, // лёгкая броня: стелс+яд+одиночная охота компенсируют (D-тюнинг)
            new Organ { organName = "Пит-орган",            slot = "Чутьё",  hotkey = "4", cost = 3, dashCooldown = 0.7f, enablesThermal = true, thermalRange = 14f }, // тепло сквозь стены; dashCd обязателен (0 = спам рывка)
            new Organ { organName = "Хвост",                slot = "Хвост",  hotkey = "8", cost = 5, enablesConstrict = true, constrictStage = 3, nativeChassis = "Змея", visualScale = new Vector3(0.9f, 0.9f, 2.4f), visualSegments = 3, visualTaper = 0.82f }, // ХВОСТ СЕГМЕНТЕН: привитый — цепочка звеньев (≈треть змеиных сегментов), масштаб под человека, а не волчий обрубок. АУГУМЕНТ игроку (обхват); constrictStage=3 + nativeChassis=Змея → ст.3 удушения только на змеином шасси (у человека кап min(2,3)=2). «Тело-хвост» выше — ходовая часть ШАССИ змеи, не путать
        };
        // СОКЕТ-ПЛАН змеи — ТОЛЬКО ГНЁЗДА-ГРАФТЫ. Своё тело морфология НЕ строит и не трогает:
        //  • туловище и хвост — ЦЕПЬ СЕГМЕНТОВ (`SnakeBodyChain` расставляет их в МИРОВЫХ координатах каждый
        //    кадр, они ползут следом и лезут по стенам) — это локомоция, планом тела не выразить;
        //  • голова — статичные дети с ВКЛЮЧЁННЫМ коллайдером (поверхность попаданий), сносить нельзя.
        // Поэтому всё родное — hidden, а морфология даёт химере на змеином шасси ВИДИМЫЕ конечности
        snake.sockets = new[]
        {
            new BodySocket { name = "Пасть",  hidden = true }, // голова статична (с коллайдером)
            new BodySocket { name = "Шкура",  hidden = true }, // «кожа» змеи — это сегменты цепи
            new BodySocket { name = "Тело",   hidden = true }, // Тело-хвост — ходовая часть шасси (цепь)
            new BodySocket { name = "Хвост",  hidden = true }, // хвост — конец той же цепи
            new BodySocket { name = "Сердце", hidden = true },
            new BodySocket { name = "Чутьё",  hidden = true },
            // ГРАФТЫ: змея, отрастившая лапы/рога/иглы — читается сразу
            new BodySocket { name = "Руки",   localPos = new Vector3(0.24f, 0.26f, 0.12f), baseSize = new Vector3(0.10f, 0.34f, 0.10f), mirrorX = true, graft = true },
            new BodySocket { name = "Ноги",   localPos = new Vector3(0.24f, 0.26f, -0.28f),baseSize = new Vector3(0.10f, 0.34f, 0.10f), mirrorX = true, graft = true },
            new BodySocket { name = "Рога",   localPos = new Vector3(0.10f, 0.55f, 0.40f), baseSize = new Vector3(0.09f, 0.09f, 0.09f), baseEuler = new Vector3(0f, 0f, 25f), mirrorX = true, graft = true }, // КАЛИБР
            new BodySocket { name = "Игломёт",localPos = new Vector3(0f, 0.56f, 0.0f),     baseSize = new Vector3(0.14f, 0.14f, 0.14f), graft = true }, // КАЛИБР
        };
        EditorUtility.SetDirty(snake);

        // ── Лось: массивный травоядный-таран (NPC-шасси; экспрессия 0.5). Рёв/рога — срезы A2/D ──
        var moose = GetOrCreate("Лось");
        moose.speciesName = "Лось";
        moose.tint = new Color(0.42f, 0.32f, 0.22f); // тёмно-бурый
        moose.mutagenPool = 24;
        moose.eatsMeat = false; // ТРАВОЯДНЫЙ: волков не ест, добычей не восстанавливается (его еда — кормёжка по карте, будущий слайс)
        moose.massive = true;   // МАССИВНАЯ ТУША: обхват слабее, нокбэк не берёт, стае нужно больше (масса из данных, было на префабе)
        moose.baseHp = 70;  // туша, но витальность лося больше в сердце, чем в самом теле
        moose.baseStamina = 140;     // ОГРОМНЫЙ бак при СЛАБОМ регене: прёт долго, а отходит медленно —
        moose.baseStaminaRegen = 5f; // загнанный лось потому и страшен, что запас у него кончается не сразу
        moose.organs = new[]
        {
            new Organ { organName = "Копыто",         slot = "Руки",   hotkey = "1", cost = 5, damage = 22, range = 1.8f }, // удар копытом — оружие
            new Organ { organName = "Лосиные ноги",   slot = "Ноги",   hotkey = "2", cost = 5, moveSpeed = 5f, dashSpeed = 35f, dashDuration = 0.38f, enablesCharge = true }, // длинные ноги: шаг ровный, а рывок = ДЛИННЫЙ мощный ТАРАН (35 > волчьих 30 + вдвое дольше → прёт быстро и далеко)
            new Organ { organName = "Глотка",         slot = "Пасть",  hotkey = "5", cost = 4, enablesBellow = true }, // РЁВ (K2): кин-лоси в берсерк на месте, чужим страх
            new Organ { organName = "Слух",           slot = "Чутьё",  hotkey = "4", cost = 3, dashCooldown = 0.7f, keenHearing = true, hearingMult = 2f }, // ОСТРЫЙ СЛУХ: вдвое дальше + различение вида + волны звука на экране (лось — слухач при слабом зрении)
            new Organ { organName = "Лосиное сердце", slot = "Сердце", hotkey = "3", cost = 6, hpBonus = 2f, staminaBonus = 0.6f, staminaRegenBonus = 0f, regen = 1f, regenOOC = 0f, atkCooldown = 0.5f, bleedResist = true }, // +200% HP + КРОВЕУПОРНОСТЬ: сердце ТАНКА — явный HP-король (обгоняет волчьи 1.75); у массивного лося своё преимущество (гора HP), а кровь ему особенно опасна (% от макс HP)
            new Organ { organName = "Толстая шкура",  slot = "Шкура",  hotkey = "6", cost = 5, damageReduction = 0.35f, visualScale = new Vector3(1.15f, 1.1f, 1f) }, // броня против ПРЯМОГО урона (не крови)
            new Organ { organName = "Рога",           slot = "Рога",   hotkey = "8", cost = 5, enablesAntler = true, visualScale = new Vector3(2.0f, 0.85f, 2.6f), visualEuler = new Vector3(0f, 32f, 0f) }, // ФОРМА ЛОСИНАЯ — задана ОРГАНОМ, одна на все шасси (место даёт лишь калибр). Лопасть РАЗВЕДЕНА НАРУЖУ (рыскание 32°, зеркалится сама): вдоль тела она читалась козырьком над мордой, а не рогами // ПРИДАТОК (химерный слот): удар рогами — откидывание + кровь. Форма ЛОСИНАЯ (лопасть-лопата) задана местом у каждого шасси — масштаб свой, вид один
        };
        // СОКЕТ-ПЛАН лося (ходульная туша: ноги ≈ полроста, горб над холкой, рога веером над головой).
        // Числа перенесены из статичной сборки MoosePrefab (ходульность lift=0.5 уже вживлена в координаты)
        moose.sockets = new[]
        {
            new BodySocket { name = "голова", localPos = new Vector3(0f, 2.65f, 1.55f),   baseSize = new Vector3(0.42f, 0.42f, 0.50f) },
            new BodySocket { name = "Пасть",  localPos = new Vector3(0f, 2.53f, 1.95f),   baseSize = new Vector3(0.30f, 0.32f, 0.62f), baseEuler = new Vector3(22f, 0f, 0f) }, // длинная морда с горбинкой
            new BodySocket { name = "уши",    localPos = new Vector3(0.24f, 2.82f, 1.45f),baseSize = new Vector3(0.10f, 0.22f, 0.08f), mirrorX = true },
            new BodySocket { name = "шея",    localPos = new Vector3(0f, 2.40f, 1.25f),   baseSize = new Vector3(0.35f, 0.38f, 0.80f), baseEuler = new Vector3(-35f, 0f, 0f) },
            new BodySocket { name = "горб",   localPos = new Vector3(0f, 2.60f, 0.45f),   baseSize = new Vector3(0.85f, 0.35f, 0.90f) }, // холка — читаемый профиль лося
            new BodySocket { name = "Шкура",  localPos = new Vector3(0f, 1.98f, 0.05f),   baseSize = new Vector3(1.05f, 1.02f, 2.30f) }, // корпус целиком (грудь+круп)
            new BodySocket { name = "Руки",   localPos = new Vector3(0.35f, 0.80f, 0.85f),baseSize = new Vector3(0.18f, 1.50f, 0.18f), mirrorX = true }, // передние ходули (Копыто)
            new BodySocket { name = "Ноги",   localPos = new Vector3(0.35f, 0.85f, -0.86f),baseSize = new Vector3(0.22f, 1.60f, 0.26f), mirrorX = true },
            new BodySocket { name = "Хвост",  localPos = new Vector3(0f, 2.20f, -1.08f),  baseSize = new Vector3(0.12f, 0.28f, 0.14f), baseEuler = new Vector3(-25f, 0f, 0f) }, // вплотную к крупу (корпус кончается на z≈-1.10)
            new BodySocket { name = "Рога",   localPos = new Vector3(0.30f, 2.97f, 1.50f),baseSize = new Vector3(0.26f, 0.26f, 0.26f), baseEuler = new Vector3(0f, 0f, 25f), mirrorX = true }, // СВОИ рога: КАЛИБР крупный. ВЫШЕ макушки (верх черепа 2.86) и ВБОК за габарит головы — раньше лопасти врастали в макушку и торчали из висков
            new BodySocket { name = "Сердце", hidden = true },
            new BodySocket { name = "Чутьё",  hidden = true },
            new BodySocket { name = "Игломёт", localPos = new Vector3(0f, 2.66f, -0.25f), baseSize = new Vector3(0.32f, 0.32f, 0.32f), graft = true }, // КАЛИБР (крупная туша): выше спины, сдвинут назад — не спорит с горбом
        };
        EditorUtility.SetDirty(moose);

        // ── Ёж «Хеджхалк»: колючий анти-контроль и будущий стрелок (спека 2026-07-22). Лабораторный
        //    мутант — крупнее и агрессивнее природного, лор-ковер оправдывает фантазию ──
        var hog = GetOrCreate("Ёж");
        hog.speciesName = "Ёж";
        hog.tint = new Color(0.58f, 0.33f, 0.28f);  // ржаво-кирпичный: тёмный и красный, не спутать с телесным
                                                    // человеком (был песочный — сливались) и бурым лосём
        hog.mutagenPool = 18;
        hog.baseHp = 52;             // 45 было мало: ёж трейдит в захвате и должен ВЫИГРЫВАТЬ у змеи (он её хищник).
                                     // 52 × сердце 1.2 на Э 0.5 ≈ 83 HP — переживает размен, с ответкой 0.55 берёт верх
        hog.baseStamina = 60;        // СПРИНТЕР, НЕ МАРАФОНЕЦ (биология): бак мал, зато отходит быстро
        hog.baseStaminaRegen = 8f;
        hog.organs = new[]
        {
            // СЛОТ «РУКИ» ПУСТ: хватки у ежа нет (5 коротких когтей, противопоставленных пальцев нет —
            // мелкая моторика это тенрек, которого путают с ежом). Вид не обязан закрывать все слоты.
            // ЗАЛП — отдельным ПРИДАТКОМ «Игломёт» (химерный слот, как Рога/Хвост): дальний бой ≠ ближний,
            // разные типы атаки не должны делить слот-оружие (иначе бьёшь-стреляешь по кинам без разбора).
            // Аддитивен: игрок берёт копыта/коготь В РУКИ И «Игломёт» отдельно — две кнопки, два приёма.
            // У NPC залп — компонентом на префабе; орган нужен ИГРОКУ-донору
            // ИМЕНА ОРГАНОВ УНИКАЛЬНЫ ПО ВСЕМ ВИДАМ: в конструкторе они рядом в одном списке
            new Organ { organName = "Игломёт",           slot = "Игломёт", hotkey = "8", cost = 4, enablesQuillVolley = true, visualScale = new Vector3(3.2f, 1.4f, 4.0f) }, // ФОРМА ЕЖИНАЯ (игольчатая плита вдоль хребта) — у органа; вертикальный хребет человека доворачивает МЕСТО // ПРИДАТОК: дальний бой игрока (химерный слот)
            new Organ { organName = "Иглы",              slot = "Шкура",  hotkey = "6", cost = 5, damageReduction = 0.2f, thorns = true }, // ОТВЕТКА: броня умеренная — иглы это ответ, а не панцирь
            new Organ { organName = "Ежиные ноги",       slot = "Ноги",   hotkey = "2", cost = 4, moveSpeed = 6f, dashSpeed = 18f, dashDuration = 0.14f, dashCooldown = 0.35f, enablesRoll = true }, // ёж НЕ догоняла, а ПИННЕР: на Э 0.5 = 3.0 — медленнее уползающей змеи (3.75), сам не догонит. Ловит КИТОМ: залп замедляет → подошёл → схватил. ПЕРЕКАТ (enablesRoll): рывок «в клубке» режет иглами кого прокатил — третий профиль ног
            new Organ { organName = "Цепкая пасть",      slot = "Пасть",  hotkey = "5", cost = 4, damage = 22, enablesBite = true, enablesConstrict = true, constrictStage = 1, nativeChassis = "Ёж" }, // ДОБИВАНИЕ + ПИН пастью (ст.1): та же челюсть грабит и кусает прижатую добычу. 22 (≈11 на Э 0.5) даёт ежу грабнуть-и-добить
            new Organ { organName = "Ядоупорное сердце", slot = "Сердце", hotkey = "3", cost = 6, hpBonus = 1.2f, staminaBonus = 0.4f, staminaRegenBonus = 0.3f, regen = 0.5f, atkCooldown = 0.5f, venomResist = true }, // РЕЗИСТ ЯДА (медоед-конституция) — делает ежа контр-видом змеи
            new Organ { organName = "Пятак",             slot = "Чутьё",  hotkey = "4", cost = 3, dashCooldown = 0.5f, enablesScent = true, keenHearing = true, hearingMult = 1.6f }, // НОЧНОЙ ЗВЕРЬ: подвижный нос и большие уши — нюх и слух остры (цена в зрении придёт со слайсом сенсорики)
            new Organ { organName = "Игольчатое тело",   slot = "Тело",   hotkey = "7", cost = 4, chassisOnly = true, enablesCurl = true }, // ходовая ФОРМА шасси ежа: сворачивание в шар (клубок/катание). chassisOnly — аугументом не крадётся, как змеиное «Тело-хвост»
        };
        // СОКЕТ-ПЛАН ежа (приземистый и широкий; иглы по хребту — главный силуэт). Числа из HedgehogPrefab.
        // «Руки» — обычное место (передние лапки рисуются), но органа Руки у ежа НЕТ → слота нет, только графтом
        hog.sockets = new[]
        {
            new BodySocket { name = "голова", localPos = new Vector3(0f, 0.40f, 0.52f),   baseSize = new Vector3(0.30f, 0.28f, 0.32f) },
            new BodySocket { name = "Пасть",  localPos = new Vector3(0f, 0.34f, 0.74f),   baseSize = new Vector3(0.26f, 0.24f, 0.34f) },
            new BodySocket { name = "уши",    localPos = new Vector3(0.15f, 0.56f, 0.42f),baseSize = new Vector3(0.09f, 0.11f, 0.05f), mirrorX = true },
            new BodySocket { name = "Шкура",  localPos = new Vector3(0f, 0.50f, -0.05f),  baseSize = new Vector3(0.86f, 0.62f, 1.05f) },
            new BodySocket { name = "Руки",   localPos = new Vector3(0.26f, 0.11f, 0.34f),baseSize = new Vector3(0.16f, 0.22f, 0.20f), mirrorX = true }, // передние лапки
            new BodySocket { name = "Ноги",   localPos = new Vector3(0.26f, 0.11f, -0.34f),baseSize = new Vector3(0.16f, 0.22f, 0.20f), mirrorX = true },
            new BodySocket { name = "Игломёт",localPos = new Vector3(0f, 0.90f, -0.08f),  baseSize = new Vector3(0.28f, 0.28f, 0.28f) }, // СВОИ иглы: КАЛИБР (форма-плита у органа), хребет горизонтальный; ВЫШЕ корпуса (верх туши 0.81), иначе тонет в теле
            new BodySocket { name = "Сердце", hidden = true },
            new BodySocket { name = "Чутьё",  hidden = true },
            new BodySocket { name = "Тело",   hidden = true }, // Игольчатое тело — ФОРМА (клубок), своей детали нет
            new BodySocket { name = "Хвост",  localPos = new Vector3(0f, 0.48f, -0.60f),  baseSize = new Vector3(0.09f, 0.09f, 0.09f), baseEuler = new Vector3(20f, 0f, 0f), graft = true }, // КАЛИБР
            new BodySocket { name = "Рога",   localPos = new Vector3(0.11f, 0.575f, 0.50f),baseSize = new Vector3(0.10f, 0.10f, 0.10f), baseEuler = new Vector3(0f, 0f, 25f), mirrorX = true, graft = true }, // КАЛИБР
        };
        EditorUtility.SetDirty(hog);

        ValidateSockets(new[] { human, wolf, snake, moose, hog }); // сверка сокет-плана: молчит, пока всё сходится

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // ── авто-привязка к телу ИГРОКА в открытой сцене (в сцене бывают и NPC-тела — ищем через контроллер) ──
        var pc = Object.FindAnyObjectByType<PlayerController>();
        var body = pc != null ? pc.GetComponent<CreatureBody>() : null;
        if (body != null)
        {
            var so = new SerializedObject(body);
            so.FindProperty("chassis").objectReferenceValue = human;
            var donorsProp = so.FindProperty("donors");
            donorsProp.arraySize = 4; // мультидонор: человек → волчий → змеиный → лосиный → ЕЖОВЫЙ
            donorsProp.GetArrayElementAtIndex(0).objectReferenceValue = wolf;
            donorsProp.GetArrayElementAtIndex(1).objectReferenceValue = snake;
            donorsProp.GetArrayElementAtIndex(2).objectReferenceValue = moose; // донор-лось открыт (эксперимент идентичности)
            donorsProp.GetArrayElementAtIndex(3).objectReferenceValue = hog;   // ёж: иглы-ответка и ядоупорное сердце
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(body);
            EditorSceneManager.MarkSceneDirty(body.gameObject.scene);
            Debug.Log("Виды обновлены в " + Dir + " и привязаны к CreatureBody. Сохрани сцену (Ctrl+S).");
        }
        else
        {
            Debug.Log("Виды обновлены в " + Dir + ". CreatureBody в сцене не найден — назначь chassis/donors вручную.");
        }
    }

    /// <summary>СВЕРКА СОКЕТ-ПЛАНА (страховка от молчания). Место органа следует из его `slot`; нет сокета с
    /// таким именем — орган либо не встанет вовсе (в родных слотах его нет), либо встанет ЧЕРЕЗ ХИМЕРНЫЙ слот
    /// и будет НЕВИДИМ. Раньше это молчало: опечатка в имени = деталь просто не рисуется, без единой ошибки.
    /// Виды без сокет-плана (пока Змея/Лось/Ёж) не проверяем — у них свой визуал префаба.</summary>
    static void ValidateSockets(SpeciesSO[] all)
    {
        foreach (var chassis in all)
        {
            if (chassis == null || chassis.sockets == null || chassis.sockets.Length == 0 || chassis.organs == null) continue;

            var places = new System.Collections.Generic.HashSet<string>();
            foreach (var s in chassis.sockets)
                if (s != null && !string.IsNullOrEmpty(s.name)) places.Add(s.name);

            // РОДНЫЕ органы: без места не рисуется часть тела САМОГО вида
            foreach (var o in chassis.organs)
                if (o != null && !places.Contains(o.slot))
                    Debug.LogWarning($"Сокет-план «{chassis.speciesName}»: родной орган «{o.organName}» (слот «{o.slot}») БЕЗ МЕСТА — часть тела не рисуется.");

            // ЧУЖИЕ органы: встанут химерным слотом, но окажутся невидимыми (chassisOnly не крадётся — не в счёт)
            var missing = new System.Collections.Generic.List<string>();
            foreach (var donor in all)
            {
                if (donor == null || donor == chassis || donor.organs == null) continue;
                foreach (var o in donor.organs)
                    if (o != null && !o.chassisOnly && !places.Contains(o.slot))
                        missing.Add($"{o.organName} ({donor.speciesName} → слот «{o.slot}»)");
            }
            if (missing.Count > 0)
                Debug.LogWarning($"Сокет-план «{chassis.speciesName}»: графты БЕЗ МЕСТА (встанут в химерный слот, но будут невидимы): {string.Join(", ", missing)}");
        }
    }

    // загрузить существующий ассет или создать новый (идемпотентность)
    static SpeciesSO GetOrCreate(string name)
    {
        string path = $"{Dir}/{name}.asset";
        var so = AssetDatabase.LoadAssetAtPath<SpeciesSO>(path);
        if (so == null)
        {
            so = ScriptableObject.CreateInstance<SpeciesSO>();
            AssetDatabase.CreateAsset(so, path);
        }
        return so;
    }
}
