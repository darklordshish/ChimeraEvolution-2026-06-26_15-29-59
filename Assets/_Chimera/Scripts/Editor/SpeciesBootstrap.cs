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
            new Organ { organName = "Кисть",  slot = "Руки",   hotkey = "1", cost = 3, damage = 8, range = 1.6f, visualParts = new[] {
                new OrganPart { scale = new Vector3(1.45f, 0.17f, 1.40f), offset = new Vector3(0.00f, 0.44f, 0.00f), shape = PartShape.Sphere }, // дельта
                new OrganPart { scale = new Vector3(1.00f, 0.40f, 1.00f), offset = new Vector3(0.00f, 0.26f, 0.00f), shape = PartShape.Capsule }, // плечо
                new OrganPart { scale = new Vector3(1.18f, 0.15f, 1.16f), offset = new Vector3(0.00f, 0.26f, 0.03f), shape = PartShape.Sphere }, // бицепс
                new OrganPart { scale = new Vector3(0.86f, 0.44f, 0.86f), offset = new Vector3(0.00f, -0.12f, 0.02f), shape = PartShape.Capsule }, // предплечье
                new OrganPart { scale = new Vector3(0.84f, 0.16f, 1.10f), offset = new Vector3(0.00f, -0.43f, 0.05f) }, // кисть
            } },
            new Organ { organName = "Ноги",   slot = "Ноги",   hotkey = "2", cost = 3, moveSpeed = 4.5f, dashSpeed = 15f, enablesKick = true, visualParts = new[] {
                new OrganPart { scale = new Vector3(1.26f, 0.26f, 1.20f), offset = new Vector3(0.00f, 0.28f, 0.02f), shape = PartShape.Sphere }, // квадрицепс
                new OrganPart { scale = new Vector3(1.05f, 0.50f, 1.02f), offset = new Vector3(0.00f, 0.24f, 0.00f), shape = PartShape.Capsule }, // бедро
                new OrganPart { scale = new Vector3(0.94f, 0.07f, 0.96f), offset = new Vector3(0.00f, 0.00f, 0.02f), shape = PartShape.Sphere }, // колено
                new OrganPart { scale = new Vector3(0.85f, 0.48f, 0.86f), offset = new Vector3(0.00f, -0.22f, -0.01f), shape = PartShape.Capsule }, // голень
                new OrganPart { scale = new Vector3(1.02f, 0.18f, 1.02f), offset = new Vector3(0.00f, -0.14f, -0.07f), shape = PartShape.Sphere }, // икра
                new OrganPart { scale = new Vector3(0.88f, 0.10f, 1.55f), offset = new Vector3(0.00f, -0.46f, 0.26f) }, // стопа
                new OrganPart { scale = new Vector3(0.86f, 0.10f, 0.40f), offset = new Vector3(0.00f, -0.46f, -0.12f) }, // пятка
            } },
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
            new BodySocket { name = "голова", localPos = new Vector3(0.000f, 1.762f, 0.000f),     baseSize = new Vector3(0.185f, 0.235f, 0.225f), parts = new[] {
                new OrganPart { scale = new Vector3(1.00f, 0.62f, 1.00f), offset = new Vector3(0.00f, 0.19f, 0.00f) }, // череп
                new OrganPart { scale = new Vector3(0.88f, 0.44f, 0.94f), offset = new Vector3(0.00f, -0.20f, 0.03f) }, // челюсть
            } }, // телесное место (органа нет)
            new BodySocket { name = "Пасть",  localPos = new Vector3(0.000f, 1.700f, 0.105f),  baseSize = new Vector3(0.100f, 0.080f, 0.060f) },
            new BodySocket { name = "шея",    localPos = new Vector3(0.000f, 1.590f, 0.000f),     baseSize = new Vector3(0.125f, 0.130f, 0.125f), parts = new[] {
                new OrganPart { scale = new Vector3(1.00f, 1.00f, 1.00f), offset = new Vector3(0.00f, 0.00f, 0.00f) }, // шея
                new OrganPart { scale = new Vector3(2.30f, 0.50f, 1.50f), offset = new Vector3(0.00f, -0.60f, -0.10f) }, // трапеции
            } }, // телесное место
            new BodySocket { name = "Шкура",  localPos = new Vector3(0.000f, 1.240f, 0.000f),     baseSize = new Vector3(0.470f, 0.600f, 0.240f), parts = new[] {
                new OrganPart { scale = new Vector3(1.16f, 0.20f, 0.98f), offset = new Vector3(0.00f, 0.40f, 0.00f) }, // плечевой пояс
                new OrganPart { scale = new Vector3(1.00f, 0.46f, 1.12f), offset = new Vector3(0.00f, 0.18f, 0.02f), shape = PartShape.Sphere }, // грудная клетка
                new OrganPart { scale = new Vector3(0.78f, 0.30f, 0.84f), offset = new Vector3(0.00f, -0.10f, 0.00f), shape = PartShape.Sphere }, // талия (узкая)
                new OrganPart { scale = new Vector3(0.96f, 0.30f, 0.96f), offset = new Vector3(0.00f, -0.35f, 0.00f), shape = PartShape.Sphere }, // таз
            } },
            new BodySocket { name = "Руки",   localPos = new Vector3(0.245f, 1.135f, 0.000f),  baseSize = new Vector3(0.115f, 0.810f, 0.120f), mirrorX = true },
            new BodySocket { name = "Ноги",   localPos = new Vector3(0.100f, 0.470f, 0.000f),  baseSize = new Vector3(0.160f, 0.940f, 0.210f), mirrorX = true },
            new BodySocket { name = "Сердце", hidden = true },
            new BodySocket { name = "Чутьё",  hidden = true },
            // ЗАКРЫТЫЕ МЕСТА (graft — пустыми НЕ рисуются): у человека нет хвоста/рогов/игломёта, но привил
            // змеиный Хвост / лосиные Рога / ежиный Игломёт — и они проступают на теле
            new BodySocket { name = "Хвост",   localPos = new Vector3(0.000f, 0.980f, -0.135f), baseSize = new Vector3(0.120f, 0.120f, 0.120f), baseEuler = new Vector3(25.00f, 0.00f, 0.00f), graft = true },  // КАЛИБР места; форму (сегментность) несёт орган
            new BodySocket { name = "Рога",    localPos = new Vector3(0.075f, 1.800f, 0.010f), baseSize = new Vector3(0.100f, 0.100f, 0.100f), mirrorX = true, graft = true }, // КАЛИБР: НАД макушкой (верх головы 1.88) и наружу — лопасть не врастает в череп
            new BodySocket { name = "Игломёт", localPos = new Vector3(0.000f, 1.460f, -0.125f), baseSize = new Vector3(0.160f, 0.160f, 0.160f), baseEuler = new Vector3(-135.00f, 0.00f, 0.00f), graft = true }, // КАЛИБР + ОРИЕНТАЦИЯ: батарея растёт СО СПИНЫ веером ВВЕРХ-НАЗАД (поворот −135° разворачивает форму целиком). Уровень ЛОПАТОК и калибр крупнее — ниже она терялась за гребнем игл
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
            new Organ { organName = "Коготь",        slot = "Руки",   hotkey = "1", cost = 4, damage = 18, range = 1.5f, visualScale = new Vector3(1f, 1f, 1.2f), visualParts = new[] {
                new OrganPart { scale = new Vector3(1.14f, 0.25f, 1.12f), offset = new Vector3(0.00f, 0.38f, 0.06f), euler = new Vector3(-21f, 0f, 0f), shape = PartShape.Capsule }, // лопатка→плечевой
                new OrganPart { scale = new Vector3(1.06f, 0.21f, 1.06f), offset = new Vector3(0.00f, 0.17f, 0.07f), euler = new Vector3(23f, 0f, 0f), shape = PartShape.Capsule }, // плечевой→локоть
                new OrganPart { scale = new Vector3(0.82f, 0.33f, 0.84f), offset = new Vector3(0.00f, -0.08f, 0.04f), euler = new Vector3(-5f, 0f, 0f), shape = PartShape.Capsule }, // локоть→запястье
                new OrganPart { scale = new Vector3(0.62f, 0.15f, 0.64f), offset = new Vector3(0.00f, -0.32f, 0.07f), euler = new Vector3(-4f, 0f, 0f), shape = PartShape.Capsule }, // запястье→путовый
                new OrganPart { scale = new Vector3(1.30f, 0.30f, 1.26f), offset = new Vector3(0.00f, 0.34f, 0.08f), shape = PartShape.Sphere }, // плечо (мышца)
                new OrganPart { scale = new Vector3(1.09f, 0.22f, 1.09f), offset = new Vector3(0.00f, 0.08f, 0.03f), shape = PartShape.Sphere }, // локоть
                new OrganPart { scale = new Vector3(0.85f, 0.17f, 0.87f), offset = new Vector3(0.00f, -0.25f, 0.06f), shape = PartShape.Sphere }, // запястье
                new OrganPart { scale = new Vector3(0.92f, 0.12f, 1.05f), offset = new Vector3(0.00f, -0.45f, 0.12f) }, // лапа с когтями
            } },
            new Organ { organName = "Волчьи ноги",   slot = "Ноги",   hotkey = "2", cost = 4, moveSpeed = 9f, dashSpeed = 30f, visualScale = new Vector3(1f, 1f, 1.2f), visualParts = new[] {
                new OrganPart { scale = new Vector3(1.22f, 0.36f, 1.20f), offset = new Vector3(0.00f, 0.33f, -0.06f), euler = new Vector3(-13f, 0f, 0f), shape = PartShape.Capsule }, // таз→колено
                new OrganPart { scale = new Vector3(0.96f, 0.36f, 0.98f), offset = new Vector3(0.00f, -0.01f, -0.10f), euler = new Vector3(23f, 0f, 0f), shape = PartShape.Capsule }, // колено→пятка
                new OrganPart { scale = new Vector3(0.66f, 0.28f, 0.68f), offset = new Vector3(0.00f, -0.30f, -0.16f), euler = new Vector3(-6f, 0f, 0f), shape = PartShape.Capsule }, // пятка→плюсна
                new OrganPart { scale = new Vector3(1.42f, 0.42f, 1.36f), offset = new Vector3(0.00f, 0.30f, -0.06f), shape = PartShape.Sphere }, // мышца бедра
                new OrganPart { scale = new Vector3(1.10f, 0.30f, 1.10f), offset = new Vector3(0.00f, 0.15f, -0.02f), shape = PartShape.Sphere }, // колено
                new OrganPart { scale = new Vector3(0.78f, 0.22f, 0.80f), offset = new Vector3(0.00f, -0.16f, -0.17f), shape = PartShape.Sphere }, // пятка
                new OrganPart { scale = new Vector3(0.46f, 0.10f, 0.36f), offset = new Vector3(0.00f, -0.15f, -0.26f) }, // пяточный отросток
                new OrganPart { scale = new Vector3(0.90f, 0.12f, 1.05f), offset = new Vector3(0.00f, -0.45f, -0.09f) }, // лапа
            } },
            new Organ { organName = "Волчье сердце", slot = "Сердце", hotkey = "3", cost = 6, atkCooldown = 0.30f, hpBonus = 1.75f, staminaBonus = 0.5f, staminaRegenBonus = 0.25f, regen = 3f, regenOOC = 0f }, // «заживает как на собаке»: реген 2→3, чтобы босс вернул свои 6/с (Blend на Э=2), а волки затягивали раны на глазах. +175%: лёгкое тело, огромный мотор → волк-NPC 68 HP, вервольф ровно 300. Постоянный реген ВМЕСТО тихого в покое (вне-боя — фича человеческого сердца)
            new Organ { organName = "Нюх",           slot = "Чутьё",  hotkey = "4", cost = 3, dashCooldown = 0.45f, enablesScent = true },
            new Organ { organName = "Пасть",         slot = "Пасть",  hotkey = "5", cost = 5, enablesBite = true, enablesHowl = true, bleedStacks = 2, howlRadius = 14f, howlStunAt = 2f, enablesConstrict = true, constrictStage = 1, nativeChassis = "Волк", visualScale = new Vector3(1.1f, 1f, 1.25f) }, // укус + кровь + ГОЛОС + ХВАТ пастью; МОРДА: на человечьем шасси садится на его «лицо» → морда вервольфа
            new Organ { organName = "Шкура",         slot = "Шкура",  hotkey = "6", cost = 4, damageReduction = 0.3f },
        };
        // СОКЕТ-ПЛАН волка (4-ногий): ТЕ ЖЕ имена, что у человека (имя = Organ.slot), позиции из BuildBlocky.
        // mirrorX даёт ЧЕТЫРЕ лапы и ДВА уха одной записью (раньше пара выглядела единым блоком)
        wolf.sockets = new[]
        {
            new BodySocket { name = "голова", localPos = new Vector3(0.000f, 1.090f, 1.067f),    baseSize = new Vector3(0.265f, 0.300f, 0.360f), parts = new[] {
                new OrganPart { scale = new Vector3(1.00f, 1.00f, 0.72f), offset = new Vector3(0.00f, 0.00f, -0.12f), shape = PartShape.Sphere }, // череп
                new OrganPart { scale = new Vector3(0.66f, 0.62f, 0.52f), offset = new Vector3(0.00f, -0.06f, 0.26f) }, // переносица
            } }, // телесное место
            new BodySocket { name = "Пасть",  localPos = new Vector3(0.000f, 1.150f, 1.330f),    baseSize = new Vector3(0.200f, 0.200f, 0.330f), parts = new[] {
                new OrganPart { scale = new Vector3(0.96f, 0.82f, 0.80f), offset = new Vector3(0.00f, 0.04f, 0.00f) }, // морда
                new OrganPart { scale = new Vector3(0.60f, 0.52f, 0.22f), offset = new Vector3(0.00f, 0.06f, 0.42f), shape = PartShape.Sphere }, // мочка носа
                new OrganPart { scale = new Vector3(0.86f, 0.42f, 0.70f), offset = new Vector3(0.00f, -0.26f, -0.04f) }, // нижняя челюсть
            } },
            new BodySocket { name = "уши",    localPos = new Vector3(0.107f, 1.280f, 0.960f), baseSize = new Vector3(0.137f, 0.168f, 0.076f), baseEuler = new Vector3(0.00f, 0.00f, 25.00f), mirrorX = true, parts = new[] {
                new OrganPart { scale = new Vector3(1.00f, 0.55f, 1.00f), offset = new Vector3(0.00f, -0.22f, 0.00f) }, // раковина
                new OrganPart { scale = new Vector3(0.50f, 0.65f, 0.75f), offset = new Vector3(0.00f, 0.32f, 0.00f) }, // кончик
            } }, // телесное место
            new BodySocket { name = "шея",    localPos = new Vector3(0.000f, 1.128f, 0.716f),    baseSize = new Vector3(0.305f, 0.351f, 0.396f), baseEuler = new Vector3(-22f, 0f, 0f), parts = new[] {
                new OrganPart { scale = new Vector3(0.90f, 0.90f, 1.00f), offset = new Vector3(0.00f, 0.00f, 0.00f) }, // шея
                new OrganPart { scale = new Vector3(1.05f, 0.42f, 0.62f), offset = new Vector3(0.00f, 0.34f, -0.16f) }, // загривок
            } }, // телесное место
            new BodySocket { name = "Шкура",  localPos = new Vector3(0.000f, 0.938f, 0.000f),    baseSize = new Vector3(0.427f, 0.625f, 1.296f), parts = new[] {
                new OrganPart { scale = new Vector3(0.98f, 0.94f, 0.88f), offset = new Vector3(0.00f, 0.05f, -0.02f), shape = PartShape.Sphere }, // корпус (единый объём)
                new OrganPart { scale = new Vector3(1.00f, 1.00f, 0.52f), offset = new Vector3(0.00f, -0.05f, 0.24f), shape = PartShape.Sphere }, // грудная клетка
                new OrganPart { scale = new Vector3(0.86f, 0.72f, 0.40f), offset = new Vector3(0.00f, 0.20f, 0.20f), shape = PartShape.Sphere }, // холка над лопатками
                new OrganPart { scale = new Vector3(0.94f, 0.88f, 0.44f), offset = new Vector3(0.00f, 0.08f, -0.32f), shape = PartShape.Sphere }, // круп
            } },
            new BodySocket { name = "Руки",   localPos = new Vector3(0.168f, 0.312f, 0.442f), baseSize = new Vector3(0.152f, 0.655f, 0.183f), mirrorX = true },
            new BodySocket { name = "Ноги",   localPos = new Vector3(0.183f, 0.328f, -0.442f),baseSize = new Vector3(0.183f, 0.686f, 0.229f), mirrorX = true },
            new BodySocket { name = "Хвост",  localPos = new Vector3(0.000f, 1.006f, -0.655f),   baseSize = new Vector3(0.152f, 0.152f, 0.732f), baseEuler = new Vector3(-25f, 0f, 0f), parts = new[] {
                new OrganPart { scale = new Vector3(1.00f, 1.00f, 0.38f), offset = new Vector3(0.00f, 0.00f, 0.31f) }, // репица
                new OrganPart { scale = new Vector3(0.86f, 0.86f, 0.38f), offset = new Vector3(0.00f, 0.00f, -0.01f) }, // середина
                new OrganPart { scale = new Vector3(0.66f, 0.66f, 0.36f), offset = new Vector3(0.00f, 0.00f, -0.32f) }, // кисточка
            } }, // СВОЙ хвост (не графт): крепится сверху крупа, продолжением позвоночника
            new BodySocket { name = "Сердце", hidden = true },
            new BodySocket { name = "Чутьё",  hidden = true },
            // закрытые места (пустыми не рисуются): волк с лосиными рогами / ежиным игломётом читается сразу
            new BodySocket { name = "Рога",    localPos = new Vector3(0.107f, 1.235f, 1.037f), baseSize = new Vector3(0.137f, 0.137f, 0.137f), mirrorX = true, graft = true }, // КАЛИБР: над черепом и наружу
            new BodySocket { name = "Игломёт", localPos = new Vector3(0.000f, 1.204f, 0.122f),    baseSize = new Vector3(0.198f, 0.198f, 0.198f), baseEuler = new Vector3(-10.00f, 0.00f, 0.00f), graft = true }, // КАЛИБР; хребет ГОРИЗОНТАЛЬНЫЙ — доворот не нужен, выше спины (верх туши 0.85)
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
            new BodySocket { name = "Рога",   localPos = new Vector3(0.08f, 0.50f, 0.40f), baseSize = new Vector3(0.09f, 0.09f, 0.09f), mirrorX = true, graft = true }, // КАЛИБР
            new BodySocket { name = "Игломёт",localPos = new Vector3(0f, 0.48f, 0.18f),    baseSize = new Vector3(0.13f, 0.13f, 0.13f), baseEuler = new Vector3(-10f, 0f, 0f), graft = true }, // КАЛИБР
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
            new Organ { organName = "Копыто",         slot = "Руки",   hotkey = "1", cost = 5, damage = 22, range = 1.8f, visualParts = new[] {
                new OrganPart { scale = new Vector3(1.16f, 0.32f, 1.14f), offset = new Vector3(0.00f, 0.35f, 0.07f), euler = new Vector3(-19f, 0f, 0f), shape = PartShape.Capsule }, // лопатка→плечевой
                new OrganPart { scale = new Vector3(1.00f, 0.18f, 1.02f), offset = new Vector3(0.00f, 0.11f, 0.08f), euler = new Vector3(24f, 0f, 0f), shape = PartShape.Capsule }, // плечевой→локоть
                new OrganPart { scale = new Vector3(0.74f, 0.21f, 0.78f), offset = new Vector3(0.00f, -0.08f, 0.05f), euler = new Vector3(-6f, 0f, 0f), shape = PartShape.Capsule }, // локоть→запястье
                new OrganPart { scale = new Vector3(0.50f, 0.18f, 0.54f), offset = new Vector3(0.00f, -0.27f, 0.06f), euler = new Vector3(-3f, 0f, 0f), shape = PartShape.Capsule }, // запястье→путовый
                new OrganPart { scale = new Vector3(1.32f, 0.34f, 1.28f), offset = new Vector3(0.00f, 0.32f, 0.08f), shape = PartShape.Sphere }, // плечо (мышца)
                new OrganPart { scale = new Vector3(1.03f, 0.16f, 1.05f), offset = new Vector3(0.00f, 0.02f, 0.04f), shape = PartShape.Sphere }, // локоть
                new OrganPart { scale = new Vector3(0.76f, 0.12f, 0.80f), offset = new Vector3(0.00f, -0.18f, 0.06f), shape = PartShape.Sphere }, // запястье
                new OrganPart { scale = new Vector3(0.48f, 0.09f, 0.50f), offset = new Vector3(0.00f, -0.40f, 0.07f), euler = new Vector3(16f, 0f, 0f), shape = PartShape.Capsule }, // путо
                new OrganPart { scale = new Vector3(0.68f, 0.07f, 0.86f), offset = new Vector3(0.00f, -0.47f, 0.09f) }, // копыто
            } }, // удар копытом — оружие
            new Organ { organName = "Лосиные ноги",   slot = "Ноги",   hotkey = "2", cost = 5, moveSpeed = 5f, dashSpeed = 35f, dashDuration = 0.38f, enablesCharge = true, visualParts = new[] {
                new OrganPart { scale = new Vector3(1.24f, 0.38f, 1.22f), offset = new Vector3(0.00f, 0.32f, -0.05f), euler = new Vector3(-14f, 0f, 0f), shape = PartShape.Capsule }, // таз→колено
                new OrganPart { scale = new Vector3(0.90f, 0.20f, 0.94f), offset = new Vector3(0.00f, 0.04f, -0.06f), euler = new Vector3(28f, 0f, 0f), shape = PartShape.Capsule }, // колено→пятка
                new OrganPart { scale = new Vector3(0.56f, 0.28f, 0.60f), offset = new Vector3(0.00f, -0.20f, -0.13f), euler = new Vector3(-5f, 0f, 0f), shape = PartShape.Capsule }, // пятка→плюсна
                new OrganPart { scale = new Vector3(1.44f, 0.44f, 1.38f), offset = new Vector3(0.00f, 0.28f, -0.05f), shape = PartShape.Sphere }, // мышца бедра
                new OrganPart { scale = new Vector3(1.08f, 0.20f, 1.10f), offset = new Vector3(0.00f, 0.13f, 0.02f), shape = PartShape.Sphere }, // колено
                new OrganPart { scale = new Vector3(0.68f, 0.14f, 0.72f), offset = new Vector3(0.00f, -0.06f, -0.14f), shape = PartShape.Sphere }, // пятка
                new OrganPart { scale = new Vector3(0.42f, 0.08f, 0.34f), offset = new Vector3(0.00f, -0.05f, -0.21f) }, // пяточный отросток
                new OrganPart { scale = new Vector3(0.46f, 0.09f, 0.48f), offset = new Vector3(0.00f, -0.40f, -0.07f), euler = new Vector3(14f, 0f, 0f), shape = PartShape.Capsule }, // путо
                new OrganPart { scale = new Vector3(0.66f, 0.07f, 0.84f), offset = new Vector3(0.00f, -0.47f, -0.05f) }, // копыто
            } }, // длинные ноги: шаг ровный, а рывок = ДЛИННЫЙ мощный ТАРАН (35 > волчьих 30 + вдвое дольше → прёт быстро и далеко)
            new Organ { organName = "Глотка",         slot = "Пасть",  hotkey = "5", cost = 4, enablesBellow = true }, // РЁВ (K2): кин-лоси в берсерк на месте, чужим страх
            new Organ { organName = "Слух",           slot = "Чутьё",  hotkey = "4", cost = 3, dashCooldown = 0.7f, keenHearing = true, hearingMult = 2f }, // ОСТРЫЙ СЛУХ: вдвое дальше + различение вида + волны звука на экране (лось — слухач при слабом зрении)
            new Organ { organName = "Лосиное сердце", slot = "Сердце", hotkey = "3", cost = 6, hpBonus = 2f, staminaBonus = 0.6f, staminaRegenBonus = 0f, regen = 1f, regenOOC = 0f, atkCooldown = 0.5f, bleedResist = true }, // +200% HP + КРОВЕУПОРНОСТЬ: сердце ТАНКА — явный HP-король (обгоняет волчьи 1.75); у массивного лося своё преимущество (гора HP), а кровь ему особенно опасна (% от макс HP)
            new Organ { organName = "Толстая шкура",  slot = "Шкура",  hotkey = "6", cost = 5, damageReduction = 0.35f, visualScale = new Vector3(1.15f, 1.1f, 1f) }, // броня против ПРЯМОГО урона (не крови)
            new Organ { organName = "Рога",           slot = "Рога",   hotkey = "8", cost = 5, enablesAntler = true, visualScale = new Vector3(1f, 1f, 1f), visualParts = new[] {
                new OrganPart { scale = new Vector3(0.75f, 0.60f, 0.75f), offset = new Vector3(0.05f, 0.00f, 0.00f), shape = PartShape.Sphere }, // розетка
                new OrganPart { scale = new Vector3(0.46f, 0.65f, 0.48f), offset = new Vector3(0.28f, 0.22f, 0.02f), euler = new Vector3(0f, 0f, -62f), shape = PartShape.Capsule }, // короткий ствол
                new OrganPart { scale = new Vector3(1.35f, 0.26f, 2.10f), offset = new Vector3(0.85f, 0.40f, 0.10f), euler = new Vector3(0f, 0f, -16f) }, // лопата ближняя
                new OrganPart { scale = new Vector3(1.25f, 0.24f, 2.60f), offset = new Vector3(1.80f, 0.68f, 0.05f), euler = new Vector3(0f, 0f, -16f) }, // лопата дальняя
                new OrganPart { scale = new Vector3(0.26f, 0.60f, 0.28f), offset = new Vector3(0.40f, 0.20f, 0.95f), euler = new Vector3(56f, 0f, -14f), shape = PartShape.Capsule }, // глазной отросток
                new OrganPart { scale = new Vector3(0.24f, 0.58f, 0.26f), offset = new Vector3(1.20f, 0.76f, 0.95f), euler = new Vector3(0f, 0f, -8f), shape = PartShape.Capsule }, // палец 1
                new OrganPart { scale = new Vector3(0.24f, 0.66f, 0.26f), offset = new Vector3(1.85f, 1.04f, 0.62f), euler = new Vector3(0f, 0f, -8f), shape = PartShape.Capsule }, // палец 2
                new OrganPart { scale = new Vector3(0.24f, 0.64f, 0.26f), offset = new Vector3(2.15f, 1.10f, -0.12f), euler = new Vector3(0f, 0f, -8f), shape = PartShape.Capsule }, // палец 3
                new OrganPart { scale = new Vector3(0.24f, 0.54f, 0.26f), offset = new Vector3(1.95f, 1.00f, -0.85f), euler = new Vector3(0f, 0f, -8f), shape = PartShape.Capsule }, // палец 4
            } }, // ФОРМА ЛОСИНАЯ — задана ОРГАНОМ, одна на все шасси (место даёт лишь калибр). Лопасть РАЗВЕДЕНА НАРУЖУ (рыскание 32°, зеркалится сама): вдоль тела она читалась козырьком над мордой, а не рогами // ПРИДАТОК (химерный слот): удар рогами — откидывание + кровь. Форма ЛОСИНАЯ (лопасть-лопата) задана местом у каждого шасси — масштаб свой, вид один
        };
        // СОКЕТ-ПЛАН лося (ходульная туша: ноги ≈ полроста, горб над холкой, рога веером над головой).
        // Числа перенесены из статичной сборки MoosePrefab (ходульность lift=0.5 уже вживлена в координаты)
        moose.sockets = new[]
        {
            new BodySocket { name = "голова", localPos = new Vector3(0.000f, 2.850f, 1.360f),   baseSize = new Vector3(0.300f, 0.340f, 0.460f), parts = new[] {
                new OrganPart { scale = new Vector3(0.82f, 0.76f, 0.45f), offset = new Vector3(0.00f, -0.04f, 0.27f) }, // лоб
                new OrganPart { scale = new Vector3(1.00f, 1.00f, 0.68f), offset = new Vector3(0.00f, 0.00f, -0.16f) }, // череп
            } },
            new BodySocket { name = "Пасть",  localPos = new Vector3(0.000f, 2.720f, 1.660f),   baseSize = new Vector3(0.270f, 0.320f, 0.460f), baseEuler = new Vector3(20f, 0f, 0f), parts = new[] {
                new OrganPart { scale = new Vector3(0.94f, 0.62f, 0.26f), offset = new Vector3(0.00f, -0.14f, 0.37f) }, // верхняя губа
                new OrganPart { scale = new Vector3(1.00f, 0.88f, 0.44f), offset = new Vector3(0.00f, -0.04f, 0.07f) }, // морда
                new OrganPart { scale = new Vector3(0.88f, 1.00f, 0.40f), offset = new Vector3(0.00f, 0.08f, -0.30f) }, // переносица с горбинкой
                new OrganPart { scale = new Vector3(0.46f, 0.58f, 0.28f), offset = new Vector3(0.00f, -0.54f, -0.12f) }, // серьга-подвес
            } }, // длинная морда с горбинкой
            new BodySocket { name = "уши",    localPos = new Vector3(0.230f, 3.000f, 1.230f),baseSize = new Vector3(0.100f, 0.260f, 0.090f), baseEuler = new Vector3(0f, 0f, 26f), mirrorX = true },
            new BodySocket { name = "шея",    localPos = new Vector3(0.000f, 2.520f, 0.940f),   baseSize = new Vector3(0.420f, 0.560f, 0.700f), baseEuler = new Vector3(-38f, 0f, 0f), parts = new[] {
                new OrganPart { scale = new Vector3(1.10f, 1.08f, 0.44f), offset = new Vector3(0.00f, 0.02f, -0.26f), shape = PartShape.Sphere }, // основание у холки
                new OrganPart { scale = new Vector3(0.92f, 0.94f, 0.42f), offset = new Vector3(0.00f, 0.00f, 0.00f), shape = PartShape.Sphere }, // середина шеи
                new OrganPart { scale = new Vector3(0.78f, 0.82f, 0.38f), offset = new Vector3(0.00f, -0.02f, 0.26f), shape = PartShape.Sphere }, // к затылку
                new OrganPart { scale = new Vector3(0.54f, 0.58f, 0.34f), offset = new Vector3(0.00f, -0.44f, 0.06f), shape = PartShape.Sphere }, // подгрудок
            } },
            new BodySocket { name = "горб",   localPos = new Vector3(0.000f, 2.261f, 0.656f),   baseSize = new Vector3(0.656f, 0.398f, 0.937f), parts = new[] {
                new OrganPart { scale = new Vector3(1.00f, 1.00f, 0.42f), offset = new Vector3(0.00f, 0.02f, 0.26f), shape = PartShape.Sphere }, // гребень над лопатками — высшая точка
                new OrganPart { scale = new Vector3(0.90f, 0.80f, 0.34f), offset = new Vector3(0.00f, -0.10f, 0.00f), shape = PartShape.Sphere }, // отростки короче
                new OrganPart { scale = new Vector3(0.76f, 0.54f, 0.32f), offset = new Vector3(0.00f, -0.24f, -0.28f), shape = PartShape.Sphere }, // сходит на нет к пояснице
            } }, // холка — читаемый профиль лося
            new BodySocket { name = "Шкура",  localPos = new Vector3(0.000f, 1.722f, 0.000f),   baseSize = new Vector3(0.703f, 0.984f, 2.284f), parts = new[] {
                new OrganPart { scale = new Vector3(0.98f, 0.94f, 0.90f), offset = new Vector3(0.00f, 0.04f, -0.02f), shape = PartShape.Sphere }, // корпус (единый объём)
                new OrganPart { scale = new Vector3(1.00f, 1.00f, 0.54f), offset = new Vector3(0.00f, -0.06f, 0.24f), shape = PartShape.Sphere }, // грудная клетка
                new OrganPart { scale = new Vector3(0.94f, 0.88f, 0.46f), offset = new Vector3(0.00f, 0.08f, -0.32f), shape = PartShape.Sphere }, // круп
            } }, // корпус целиком (грудь+круп)
            new BodySocket { name = "Руки",   localPos = new Vector3(0.258f, 0.633f, 0.820f),baseSize = new Vector3(0.170f, 1.265f, 0.193f), mirrorX = true }, // передние ходули (Копыто)
            new BodySocket { name = "Ноги",   localPos = new Vector3(0.258f, 0.650f, -0.843f),baseSize = new Vector3(0.205f, 1.300f, 0.228f), mirrorX = true },
            new BodySocket { name = "Хвост",  localPos = new Vector3(0.000f, 1.991f, -1.148f),  baseSize = new Vector3(0.117f, 0.258f, 0.129f), baseEuler = new Vector3(-30f, 0f, 0f), parts = new[] {
                new OrganPart { scale = new Vector3(1.00f, 0.59f, 1.00f), offset = new Vector3(0.00f, 0.21f, 0.00f) }, // репица
                new OrganPart { scale = new Vector3(0.72f, 0.48f, 0.72f), offset = new Vector3(0.00f, -0.26f, 0.00f) }, // кончик
            } }, // вплотную к крупу (корпус кончается на z≈-1.00)
            new BodySocket { name = "Рога",   localPos = new Vector3(0.140f, 3.000f, 1.400f),baseSize = new Vector3(0.300f, 0.300f, 0.300f), mirrorX = true }, // СВОИ рога: КАЛИБР крупный. В ВИСКАХ (верх черепа 2.17) и ВБОК за габарит головы — раньше лопасти врастали в макушку и торчали из висков
            new BodySocket { name = "Сердце", hidden = true },
            new BodySocket { name = "Чутьё",  hidden = true },
            new BodySocket { name = "Игломёт", localPos = new Vector3(0.000f, 2.202f, 0.000f), baseSize = new Vector3(0.281f, 0.281f, 0.281f), baseEuler = new Vector3(-10.00f, 0.00f, 0.00f), graft = true }, // КАЛИБР (крупная туша): НА спине (верх туши 2.10) — основания шипов входят в корпус; сдвинут назад, не спорит с горбом
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
            new Organ { organName = "Игломёт",           slot = "Игломёт", hotkey = "8", cost = 4, enablesQuillVolley = true, visualScale = new Vector3(1f, 1f, 1f), visualParts = new[] {
                // БАТАРЕЯ: длинные иглы ВПЕРЁД — куда смотрят стволы, туда и летит залп (читаемость
                // важнее биологии: игрок сразу видит, что тварь плюётся иглами). Лёгкий веер
                new OrganPart { scale = new Vector3(0.13f, 0.13f, 1.7f), offset = new Vector3(-0.42f, 0.12f, 0.55f), euler = new Vector3(-10f, -13.0f, 0f) },
                new OrganPart { scale = new Vector3(0.13f, 0.13f, 1.7f), offset = new Vector3(-0.14f, 0.12f, 0.55f), euler = new Vector3(-10f, -4.5f, 0f) },
                new OrganPart { scale = new Vector3(0.13f, 0.13f, 1.7f), offset = new Vector3(0.14f, 0.12f, 0.55f), euler = new Vector3(-10f, 4.5f, 0f) },
                new OrganPart { scale = new Vector3(0.13f, 0.13f, 1.7f), offset = new Vector3(0.42f, 0.12f, 0.55f), euler = new Vector3(-10f, 13.0f, 0f) },
            } }, // ФОРМА ЕЖИНАЯ (игольчатая плита вдоль хребта) — у органа; вертикальный хребет человека доворачивает МЕСТО // ПРИДАТОК: дальний бой игрока (химерный слот)
            new Organ { organName = "Иглы",              slot = "Шкура",  hotkey = "6", cost = 5, damageReduction = 0.2f, thorns = true, visualAlignToBody = true, visualParts = new[] {
                new OrganPart { scale = new Vector3(0.62f, 0.70f, 0.34f), offset = new Vector3(0.00f, -0.14f, 0.33f), shape = PartShape.Sphere }, // плечи — ниже и уже
                new OrganPart { scale = new Vector3(0.86f, 0.88f, 0.42f), offset = new Vector3(0.00f, -0.04f, 0.13f), shape = PartShape.Sphere }, // грудь
                new OrganPart { scale = new Vector3(1.00f, 1.00f, 0.46f), offset = new Vector3(0.00f, 0.04f, -0.09f), shape = PartShape.Sphere }, // середина — вершина дуги
                new OrganPart { scale = new Vector3(0.92f, 0.90f, 0.44f), offset = new Vector3(0.00f, -0.02f, -0.31f), shape = PartShape.Sphere }, // круп
                new OrganPart { scale = new Vector3(0.07f, 0.62f, 0.07f), offset = new Vector3(-0.26f, 0.48f, 0.30f), euler = new Vector3(-22f, 0f, -20f) }, // шип
                new OrganPart { scale = new Vector3(0.07f, 0.62f, 0.07f), offset = new Vector3(-0.09f, 0.53f, 0.30f), euler = new Vector3(-22f, 0f, -7f) }, // шип
                new OrganPart { scale = new Vector3(0.07f, 0.62f, 0.07f), offset = new Vector3(0.09f, 0.53f, 0.30f), euler = new Vector3(-22f, 0f, 7f) }, // шип
                new OrganPart { scale = new Vector3(0.07f, 0.62f, 0.07f), offset = new Vector3(0.26f, 0.48f, 0.30f), euler = new Vector3(-22f, 0f, 20f) }, // шип
                new OrganPart { scale = new Vector3(0.07f, 0.62f, 0.07f), offset = new Vector3(-0.26f, 0.54f, 0.12f), euler = new Vector3(-30f, 0f, -20f) }, // шип
                new OrganPart { scale = new Vector3(0.07f, 0.62f, 0.07f), offset = new Vector3(-0.09f, 0.59f, 0.12f), euler = new Vector3(-30f, 0f, -7f) }, // шип
                new OrganPart { scale = new Vector3(0.07f, 0.62f, 0.07f), offset = new Vector3(0.09f, 0.59f, 0.12f), euler = new Vector3(-30f, 0f, 7f) }, // шип
                new OrganPart { scale = new Vector3(0.07f, 0.62f, 0.07f), offset = new Vector3(0.26f, 0.54f, 0.12f), euler = new Vector3(-30f, 0f, 20f) }, // шип
                new OrganPart { scale = new Vector3(0.07f, 0.62f, 0.07f), offset = new Vector3(-0.26f, 0.54f, -0.10f), euler = new Vector3(-30f, 0f, -20f) }, // шип
                new OrganPart { scale = new Vector3(0.07f, 0.62f, 0.07f), offset = new Vector3(-0.09f, 0.59f, -0.10f), euler = new Vector3(-30f, 0f, -7f) }, // шип
                new OrganPart { scale = new Vector3(0.07f, 0.62f, 0.07f), offset = new Vector3(0.09f, 0.59f, -0.10f), euler = new Vector3(-30f, 0f, 7f) }, // шип
                new OrganPart { scale = new Vector3(0.07f, 0.62f, 0.07f), offset = new Vector3(0.26f, 0.54f, -0.10f), euler = new Vector3(-30f, 0f, 20f) }, // шип
                new OrganPart { scale = new Vector3(0.07f, 0.62f, 0.07f), offset = new Vector3(-0.26f, 0.46f, -0.30f), euler = new Vector3(-44f, 0f, -20f) }, // шип
                new OrganPart { scale = new Vector3(0.07f, 0.62f, 0.07f), offset = new Vector3(-0.09f, 0.50f, -0.30f), euler = new Vector3(-44f, 0f, -7f) }, // шип
                new OrganPart { scale = new Vector3(0.07f, 0.62f, 0.07f), offset = new Vector3(0.09f, 0.50f, -0.30f), euler = new Vector3(-44f, 0f, 7f) }, // шип
                new OrganPart { scale = new Vector3(0.07f, 0.62f, 0.07f), offset = new Vector3(0.26f, 0.46f, -0.30f), euler = new Vector3(-44f, 0f, 20f) }, // шип
            } }, // ОТВЕТКА: броня умеренная — иглы это ответ, а не панцирь
            new Organ { organName = "Ежиные ноги",       slot = "Ноги",   hotkey = "2", cost = 4, moveSpeed = 6f, dashSpeed = 18f, dashDuration = 0.14f, dashCooldown = 0.35f, enablesRoll = true, visualParts = new[] {
                new OrganPart { scale = new Vector3(0.95f, 0.44f, 0.95f), offset = new Vector3(0.00f, 0.27f, -0.06f), euler = new Vector3(-10f, 0f, 0f), shape = PartShape.Capsule }, // бедро
                new OrganPart { scale = new Vector3(0.78f, 0.46f, 0.82f), offset = new Vector3(0.00f, -0.13f, 0.06f), euler = new Vector3(12f, 0f, 0f), shape = PartShape.Capsule }, // голень
                new OrganPart { scale = new Vector3(1.06f, 0.64f, 1.06f), offset = new Vector3(0.00f, 0.08f, 0.00f), shape = PartShape.Sphere }, // колено
                new OrganPart { scale = new Vector3(0.80f, 0.18f, 1.55f), offset = new Vector3(0.00f, -0.41f, 0.22f) }, // стопа (ёж СТОПОХОДЯЩИЙ)
            } }, // ёж НЕ догоняла, а ПИННЕР: на Э 0.5 = 3.0 — медленнее уползающей змеи (3.75), сам не догонит. Ловит КИТОМ: залп замедляет → подошёл → схватил. ПЕРЕКАТ (enablesRoll): рывок «в клубке» режет иглами кого прокатил — третий профиль ног
            new Organ { organName = "Цепкая пасть",      slot = "Пасть",  hotkey = "5", cost = 4, damage = 22, enablesBite = true, enablesConstrict = true, constrictStage = 1, nativeChassis = "Ёж" }, // ДОБИВАНИЕ + ПИН пастью (ст.1): та же челюсть грабит и кусает прижатую добычу. 22 (≈11 на Э 0.5) даёт ежу грабнуть-и-добить
            new Organ { organName = "Ядоупорное сердце", slot = "Сердце", hotkey = "3", cost = 6, hpBonus = 1.2f, staminaBonus = 0.4f, staminaRegenBonus = 0.3f, regen = 0.5f, atkCooldown = 0.5f, venomResist = true }, // РЕЗИСТ ЯДА (медоед-конституция) — делает ежа контр-видом змеи
            new Organ { organName = "Пятак",             slot = "Чутьё",  hotkey = "4", cost = 3, dashCooldown = 0.5f, enablesScent = true, keenHearing = true, hearingMult = 1.6f }, // НОЧНОЙ ЗВЕРЬ: подвижный нос и большие уши — нюх и слух остры (цена в зрении придёт со слайсом сенсорики)
            new Organ { organName = "Игольчатое тело",   slot = "Тело",   hotkey = "7", cost = 4, chassisOnly = true, enablesCurl = true }, // ходовая ФОРМА шасси ежа: сворачивание в шар (клубок/катание). chassisOnly — аугументом не крадётся, как змеиное «Тело-хвост»
        };
        // СОКЕТ-ПЛАН ежа (приземистый и широкий; иглы по хребту — главный силуэт). Числа из HedgehogPrefab.
        // «Руки» — обычное место (передние лапки рисуются), но органа Руки у ежа НЕТ → слота нет, только графтом
        hog.sockets = new[]
        {
            new BodySocket { name = "голова", localPos = new Vector3(0f, 0.40f, 0.52f),   baseSize = new Vector3(0.30f, 0.28f, 0.32f), parts = new[] {
                new OrganPart { scale = new Vector3(1.00f, 1.00f, 1.00f), offset = new Vector3(0.00f, 0.00f, 0.00f), shape = PartShape.Sphere }, // череп
            } },
            new BodySocket { name = "Пасть",  localPos = new Vector3(0.000f, 0.340f, 0.700f),   baseSize = new Vector3(0.220f, 0.200f, 0.280f), parts = new[] {
                new OrganPart { scale = new Vector3(1.00f, 0.94f, 0.52f), offset = new Vector3(0.00f, 0.00f, -0.20f), shape = PartShape.Sphere }, // основание морды
                new OrganPart { scale = new Vector3(0.62f, 0.58f, 0.46f), offset = new Vector3(0.00f, -0.06f, 0.14f) }, // клин морды
                new OrganPart { scale = new Vector3(0.30f, 0.28f, 0.22f), offset = new Vector3(0.00f, -0.12f, 0.38f), shape = PartShape.Sphere }, // нос-пуговка
            } },
            new BodySocket { name = "уши",    localPos = new Vector3(0.13f, 0.58f, 0.56f),baseSize = new Vector3(0.12f, 0.14f, 0.06f), mirrorX = true, parts = new[] {
                new OrganPart { scale = new Vector3(0.95f, 0.65f, 1.00f), offset = new Vector3(0.00f, -0.18f, 0.00f), shape = PartShape.Sphere }, // раковина
                new OrganPart { scale = new Vector3(1.00f, 0.70f, 0.85f), offset = new Vector3(0.00f, 0.24f, -0.04f), shape = PartShape.Sphere }, // верх
            } },
            new BodySocket { name = "Шкура",  localPos = new Vector3(0.00f, 0.50f, -0.05f),  baseSize = new Vector3(0.76f, 0.60f, 1.05f), parts = new[] {
                new OrganPart { scale = new Vector3(0.96f, 0.96f, 0.86f), offset = new Vector3(0.00f, 0.02f, -0.04f), shape = PartShape.Sphere }, // корпус (единый объём)
                new OrganPart { scale = new Vector3(0.66f, 0.74f, 0.42f), offset = new Vector3(0.00f, -0.10f, 0.32f), shape = PartShape.Sphere }, // плечи — перед сужен
                new OrganPart { scale = new Vector3(0.94f, 0.92f, 0.44f), offset = new Vector3(0.00f, 0.02f, -0.28f), shape = PartShape.Sphere }, // круп
            } },
            new BodySocket { name = "Руки",   localPos = new Vector3(0.24f, 0.13f, 0.32f),baseSize = new Vector3(0.15f, 0.26f, 0.19f), mirrorX = true, parts = new[] {
                new OrganPart { scale = new Vector3(0.92f, 0.42f, 0.92f), offset = new Vector3(0.00f, 0.27f, -0.04f), euler = new Vector3(-8f, 0f, 0f), shape = PartShape.Capsule }, // плечо
                new OrganPart { scale = new Vector3(0.76f, 0.44f, 0.80f), offset = new Vector3(0.00f, -0.14f, 0.06f), euler = new Vector3(10f, 0f, 0f), shape = PartShape.Capsule }, // предплечье
                new OrganPart { scale = new Vector3(1.02f, 0.60f, 1.02f), offset = new Vector3(0.00f, 0.08f, 0.01f), shape = PartShape.Sphere }, // локоть
                new OrganPart { scale = new Vector3(0.78f, 0.18f, 1.45f), offset = new Vector3(0.00f, -0.41f, 0.20f) }, // кисть (стопоходящий)
            } }, // передние лапки
            new BodySocket { name = "Ноги",   localPos = new Vector3(0.24f, 0.13f, -0.32f),baseSize = new Vector3(0.15f, 0.26f, 0.19f), mirrorX = true },
            new BodySocket { name = "Игломёт",localPos = new Vector3(0f, 0.70f, 0.22f),   baseSize = new Vector3(0.24f, 0.24f, 0.24f), baseEuler = new Vector3(-10f, 0f, 0f) }, // СВОИ иглы: КАЛИБР (форма-плита у органа), хребет горизонтальный; ВЫШЕ корпуса (верх туши 0.81), иначе тонет в теле
            new BodySocket { name = "Сердце", hidden = true },
            new BodySocket { name = "Чутьё",  hidden = true },
            new BodySocket { name = "Тело",   hidden = true }, // Игольчатое тело — ФОРМА (клубок), своей детали нет
            new BodySocket { name = "Хвост",  localPos = new Vector3(0.000f, 0.400f, -0.560f),  baseSize = new Vector3(0.045f, 0.045f, 0.080f), baseEuler = new Vector3(-40f, 0f, 0f), parts = new[] {
                new OrganPart { scale = new Vector3(1.00f, 1.00f, 1.00f), offset = new Vector3(0.00f, 0.00f, 0.00f), shape = PartShape.Capsule }, // хвостик
            } }, // КАЛИБР
            new BodySocket { name = "Рога",   localPos = new Vector3(0.07f, 0.52f, 0.50f),baseSize = new Vector3(0.10f, 0.10f, 0.10f), mirrorX = true, graft = true }, // КАЛИБР
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
