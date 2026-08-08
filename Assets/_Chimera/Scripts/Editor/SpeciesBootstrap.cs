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
                new OrganPart { scale = new Vector3(1.55f, 0.22f, 1.45f), offset = new Vector3(0.00f, 0.44f, 0.00f), shape = PartShape.Sphere }, // дельта — шапка плеча, 18 см: она замыкает «вешалку» ключиц
                new OrganPart { scale = new Vector3(0.86f, 0.40f, 0.86f), offset = new Vector3(0.00f, 0.26f, 0.00f), shape = PartShape.Capsule }, // плечо
                new OrganPart { scale = new Vector3(1.15f, 0.18f, 1.10f), offset = new Vector3(0.00f, 0.28f, 0.04f), shape = PartShape.Sphere }, // бицепс
                new OrganPart { scale = new Vector3(0.70f, 0.44f, 0.70f), offset = new Vector3(0.00f, -0.12f, 0.02f), shape = PartShape.Capsule }, // предплечье
                new OrganPart { scale = new Vector3(0.68f, 0.17f, 0.98f), offset = new Vector3(0.00f, -0.42f, 0.04f) }, // кисть
            } },
            new Organ { organName = "Ноги",   slot = "Ноги",   hotkey = "2", cost = 3, moveSpeed = 4.5f, dashSpeed = 15f, enablesKick = true, visualParts = new[] {
                new OrganPart { scale = new Vector3(0.98f, 0.24f, 0.94f), offset = new Vector3(0.00f, 0.28f, 0.02f), shape = PartShape.Sphere }, // квадрицепс
                new OrganPart { scale = new Vector3(0.82f, 0.52f, 0.80f), offset = new Vector3(0.00f, 0.24f, 0.00f), shape = PartShape.Capsule }, // бедро
                new OrganPart { scale = new Vector3(0.72f, 0.07f, 0.74f), offset = new Vector3(0.00f, 0.00f, 0.02f), shape = PartShape.Sphere }, // колено
                new OrganPart { scale = new Vector3(0.64f, 0.50f, 0.64f), offset = new Vector3(0.00f, -0.22f, -0.01f), shape = PartShape.Capsule }, // голень
                new OrganPart { scale = new Vector3(0.80f, 0.16f, 0.78f), offset = new Vector3(0.00f, -0.13f, -0.06f), shape = PartShape.Sphere }, // икра
                new OrganPart { scale = new Vector3(0.78f, 0.10f, 1.55f), offset = new Vector3(0.00f, -0.46f, 0.20f) }, // стопа
            } },
            new Organ { organName = "Сердце", slot = "Сердце", hotkey = "3", cost = 6, atkCooldown = 0.45f, hpBonus = 0.5f, staminaBonus = 0.5f, staminaRegenBonus = 0.5f, regen = 0f, regenOOC = 0.75f, visualParts = new[] {
                new OrganPart { scale = new Vector3(0.90f, 0.86f, 0.70f), offset = new Vector3(0.00f, 0.00f, 0.04f), shape = PartShape.Sphere }, // грудная клетка ЧЕЛОВЕКА: ПЛОСКАЯ и широкая — рёбра сходятся на грудине
            } }, // СЕРДЦЕ ЛЕПИТ ГРУДЬ: форма ушла из торса в орган, поэтому чужое сердце перестраивает силуэт
            new Organ { organName = "Чутьё",  slot = "Чутьё",  hotkey = "4", cost = 3, dashCooldown = 0.7f, insight = true }, // внутренний: сокет hidden — места на теле нет
            new Organ { organName = "Рот",    slot = "Пасть",  hotkey = "5", cost = 3, enablesBite = false }, // лицо/пасть — ОТДЕЛЬНО от черепа: волчья Пасть сядет сюда же → морда вервольфа
            new Organ { organName = "Кожа",   slot = "Шкура",  hotkey = "6", cost = 3, damageReduction = 0f },
        };
        // СОКЕТ-ПЛАН человека (прямоходящий). ИМЯ СОКЕТА = Organ.slot — одно и то же имя держит механику и
        // визуал, разойтись не могут. Те же имена у зверей → волчьи органы садятся на человечьи места (вервольф).
        // mirrorX — парное место (2 руки/ноги); hidden — внутренний (места на теле нет); graft — закрытое место
        human.sockets = new[]
        {
            new BodySocket { name = "хребет", localPos = new Vector3(0.000f, 1.240f, 0.000f), baseSize = new Vector3(0.470f, 0.600f, 0.240f), hidden = true }, // ПОЗВОНОЧНИК: корень графа тела (у человека ВЕРТИКАЛЬНЫЙ — длинная ось Y). Служебный: не рисуется и НЕ слот органа, поэтому покров (Кожа/Иглы) не тащит за собой скелет
            // ГОЛОВА БАЛАНСИРУЕТ НА ПОЗВОНОЧНИКЕ, а не стоит на нём сверху: позвонок входит в затылочное
            // отверстие — точка опоры лежит ПОД УШАМИ, лицо висит впереди неё, затылок нависает сзади.
            // Пока голова крепилась геометрическим низом, она садилась «шапкой на палку» (attachOffset.z был
            // −0.076, то есть ещё и назад). baseEuler −10 гасит наклон шеи: взгляд остаётся горизонтальным
            new BodySocket { name = "голова", parent = "шея", attach = 1.000f, attachOffset = new Vector3(0.000f, 0.435f, -0.020f), baseSize = new Vector3(0.178f, 0.270f, 0.216f), baseEuler = new Vector3(-10f, 0f, 0f), parts = new[] {
                // ЭТЮД ГОЛОВЫ (Лумис): шар мозгового черепа + челюстной блок, между ними — плоскости лица.
                // Череп ОКРУГЛЫЙ (кость свода), лицевые плоскости ГРАНЁНЫЕ — то же правило «плоть/кость», что у зверей
                new OrganPart { scale = new Vector3(1.00f, 0.64f, 0.90f), offset = new Vector3(0.00f, 0.19f, -0.05f), shape = PartShape.Sphere }, // мозговой череп
                new OrganPart { scale = new Vector3(0.80f, 0.44f, 0.44f), offset = new Vector3(0.00f, 0.09f, -0.23f), shape = PartShape.Sphere }, // затылок — поджат и придвинут к своду: отдельным шаром он торчал «яйцом назад», между ним и черепом читалась борозда
                new OrganPart { scale = new Vector3(0.86f, 0.34f, 0.44f), offset = new Vector3(0.00f, 0.08f, 0.20f), shape = PartShape.Sphere }, // лоб — лобная кость ОКРУГЛА, кубом она резала череп плитой
                new OrganPart { scale = new Vector3(0.26f, 0.08f, 0.16f), offset = new Vector3(0.20f, 0.00f, 0.29f) }, // надбровная дуга (пр) — их ДВЕ, с переносицей между; сплошная полоса через лицо анатомии не знает
                new OrganPart { scale = new Vector3(0.26f, 0.08f, 0.16f), offset = new Vector3(-0.20f, 0.00f, 0.29f) }, // надбровная дуга (лев)
                new OrganPart { scale = new Vector3(0.94f, 0.30f, 0.60f), offset = new Vector3(0.00f, -0.05f, 0.04f), shape = PartShape.Sphere }, // скулы — самая широкая точка головы
                new OrganPart { scale = new Vector3(0.66f, 0.30f, 0.44f), offset = new Vector3(0.00f, -0.12f, 0.22f), shape = PartShape.Sphere }, // верхняя челюсть (лицевая масса)
                new OrganPart { scale = new Vector3(0.20f, 0.22f, 0.18f), offset = new Vector3(0.00f, -0.10f, 0.36f) }, // нос — САМАЯ передняя точка
                // НИЖНЯЯ ЧЕЛЮСТЬ — подкова из ДВУХ звеньев. Одним шаром она сидела на 2.6 см ПОЗАДИ верхней
                // челюсти, и лицо кончалось носом: классический «убегающий подбородок»
                new OrganPart { scale = new Vector3(0.78f, 0.30f, 0.40f), offset = new Vector3(0.00f, -0.20f, -0.04f), shape = PartShape.Sphere }, // ветвь челюсти — от угла вверх к уху
                new OrganPart { scale = new Vector3(0.66f, 0.26f, 0.52f), offset = new Vector3(0.00f, -0.30f, 0.14f), shape = PartShape.Sphere }, // тело челюсти — вперёд-вниз к подбородку
                new OrganPart { scale = new Vector3(0.34f, 0.18f, 0.24f), offset = new Vector3(0.00f, -0.33f, 0.28f) }, // подбородок — костный выступ, чуть позади кончика носа
            } }, // телесное место (органа нет). Ширина/высота 0.69 и глубина/высота 0.89 — череп по канону (0.65 / 0.80) плюс вынос носа
            new BodySocket { name = "Пасть", parent = "голова", attach = 1.000f, attachOffset = new Vector3(0.000f, -0.704f, 0.467f),  baseSize = new Vector3(0.100f, 0.080f, 0.060f), parts = new[] {
                new OrganPart { scale = new Vector3(0.70f, 0.70f, 0.70f), offset = new Vector3(0.00f, 0.00f, -0.30f), shape = PartShape.Sphere }, // лицевая масса
            } },
            // УШИ — их у человека НЕ БЫЛО ВОВСЕ, при том что есть у волка, лося и ежа. Голова без них
            // теряет ~2 см ширины в самом заметном месте и анфас читается мельче, чем она есть по канону
            new BodySocket { name = "уши", parent = "голова", attach = 0.500f, attachOffset = new Vector3(0.480f, -0.020f, -0.100f),  baseSize = new Vector3(0.022f, 0.062f, 0.032f), baseEuler = new Vector3(-15f, 0f, 0f), mirrorX = true, parts = new[] {
                new OrganPart { scale = new Vector3(1.00f, 1.00f, 0.70f), offset = new Vector3(0.00f, 0.00f, 0.00f), shape = PartShape.Sphere }, // раковина
                new OrganPart { scale = new Vector3(0.80f, 0.42f, 0.60f), offset = new Vector3(0.00f, -0.40f, 0.06f), shape = PartShape.Sphere }, // мочка
            } },
            // ОСТОРОЖНО С baseSize ШЕИ: длинная ось места выбирается АВТОМАТИЧЕСКИ по максимальной стороне.
            // Укоротил Y до 0.128 при глубине 0.132 — ось переключилась Y→Z, и голова поехала не вверх, а
            // вперёд (рост 1.85 → 1.76). Держим Y строго больше X и Z
            new BodySocket { name = "шея", parent = "хребет", attach = 1.000f, attachOffset = new Vector3(0.000f, 0.058f, 0.042f),    baseSize = new Vector3(0.126f, 0.134f, 0.126f), baseEuler = new Vector3(10f, 0f, 0f), parts = new[] {
                new OrganPart { scale = new Vector3(1.00f, 1.00f, 1.00f), offset = new Vector3(0.00f, 0.00f, 0.00f), shape = PartShape.Capsule }, // шея — ЦИЛИНДР, а не шар
                new OrganPart { scale = new Vector3(1.05f, 0.55f, 0.85f), offset = new Vector3(0.00f, 0.46f, -0.14f), shape = PartShape.Sphere }, // ЗАТЫЛОЧНО-ШЕЙНЫЙ ПЕРЕХОД: спереди череп накрывала челюсть, а СЗАДИ под нависающим затылком зияло 3 см пустоты. У человека там мышечный массив (полуостистая + верх трапеции) — им и закрываем
                new OrganPart { scale = new Vector3(1.10f, 0.42f, 1.20f), offset = new Vector3(0.00f, -0.44f, -0.06f), shape = PartShape.Sphere }, // переход к трапеции — шея не втыкается в грудь торцом
            } }, // УКОРОЧЕНА 0.185 → 0.150: голова не мала (7.4 головы в росте — канон), мал был её вклад в силуэт рядом с 20-см голой колонной шеи. У живого человека наружу торчит лишь ВЕРХ шеи, низ съеден трапецией
            new BodySocket { name = "Шкура",  parent = "хребет", attach = 0.500f,     baseSize = new Vector3(0.470f, 0.600f, 0.240f), parts = new[] {
                // ЭТЮД ТОРСА: два объёма (грудная клетка-яйцо и таз-клин) + перемычка талии между ними, сверху
                // «вешалка» плечевого пояса. Мышцы — ТОНКИЕ НАКЛАДКИ поверх костных объёмов, а не отдельные шары:
                // силуэт держит скелет, мышца лишь читается на нём. Числа — от канона 8 голов (H = 0.234 м)
                new OrganPart { scale = new Vector3(1.00f, 0.20f, 0.62f), offset = new Vector3(0.00f, 0.36f, 0.04f), shape = PartShape.Sphere }, // плечевой пояс («вешалка» ключиц) — размах 2H = 0.47
                // грудная клетка УШЛА В ОРГАН «Сердце» — теперь её лепит сердце, и чужое перестраивает силуэт
                new OrganPart { scale = new Vector3(0.56f, 0.30f, 0.68f), offset = new Vector3(0.00f, -0.06f, 0.00f), shape = PartShape.Sphere }, // талия — перемычка 1.12H, САМОЕ узкое место торса
                new OrganPart { scale = new Vector3(0.38f, 0.26f, 0.24f), offset = new Vector3(0.00f, -0.06f, 0.24f), shape = PartShape.Sphere }, // пресс — накладка вровень с животом
                new OrganPart { scale = new Vector3(0.80f, 0.30f, 0.66f), offset = new Vector3(0.00f, -0.32f, 0.00f), shape = PartShape.Sphere }, // таз — клин 1.51H, снова шире талии (но уже плеч: мужской силуэт)
                new OrganPart { scale = new Vector3(0.34f, 0.24f, 0.42f), offset = new Vector3(0.18f, -0.37f, -0.15f), shape = PartShape.Sphere }, // ягодица (пр)
                new OrganPart { scale = new Vector3(0.34f, 0.24f, 0.42f), offset = new Vector3(-0.18f, -0.37f, -0.15f), shape = PartShape.Sphere }, // ягодица (лев)
            } },
            new BodySocket { name = "Руки", parent = "хребет", attach = 0.900f, attachOffset = new Vector3(0.409f, -0.658f, 0.000f),   baseSize = new Vector3(0.115f, 0.810f, 0.120f), mirrorX = true },
            new BodySocket { name = "Ноги", parent = "хребет", attach = 0.050f, attachOffset = new Vector3(0.213f, -0.797f, 0.000f),   baseSize = new Vector3(0.160f, 0.985f, 0.210f), mirrorX = true },
            // СЕРДЦЕ — ВНУТРЕННЕЕ, НО С МЕСТОМ: своего силуэта нет, проступает формой органа (грудная клетка).
            // Калибр и посадка — те же, что у торса, поэтому доли частей читаются 1:1 и родное сердце даёт
            // ровно прежнюю грудь; чужое приносит СВОЮ форму и перестраивает силуэт
            new BodySocket { name = "Сердце", inner = true, parent = "хребет", attach = 0.500f, attachOffset = new Vector3(0.000f, 0.150f, 0.000f), baseSize = new Vector3(0.380f, 0.340f, 0.280f), parts = new[] {
                // МЯСО ПОВЕРХ КОСТИ. Мышцы груди живут В ДОЛЯХ ГРУДНОЙ КОРОБКИ, а её форму приносит Сердце —
                // значит чужое сердце перестраивает и мышцы, они не спорят с костью. Пока мышцы сидели в торсе,
                // кость приходилось раздувать, чтобы её было видно из-под них, и силуэт выходил пузатым
                new OrganPart { scale = new Vector3(0.97f, 0.60f, 0.53f), offset = new Vector3(0.00f, 0.44f, -0.05f), shape = PartShape.Sphere }, // трапеция — подводка от шеи к плечам
                new OrganPart { scale = new Vector3(0.77f, 0.39f, 0.26f), offset = new Vector3(0.00f, 0.16f, 0.26f), shape = PartShape.Sphere }, // грудные (пекторали) — плита поверх рёбер
                new OrganPart { scale = new Vector3(1.04f, 0.60f, 0.41f), offset = new Vector3(0.00f, -0.09f, -0.09f), shape = PartShape.Sphere }, // широчайшие — шире рёбер, дают конус к талии
            } }, // ГРУДНАЯ КОРОБКА человека: шире, чем глубже (рёбра сходятся на грудине) // ГРУДНАЯ КОРОБКА человека: шире, чем глубже (рёбра сходятся на грудине)
            new BodySocket { name = "Чутьё",  inner = true },  // формы у нюха нет, и деталь не родится сама собой; дай органу форму (термо-ямки) — место проступит, флаги править не придётся
            // ЗАКРЫТЫЕ МЕСТА (graft — пустыми НЕ рисуются): у человека нет хвоста/рогов/игломёта, но привил
            // змеиный Хвост / лосиные Рога / ежиный Игломёт — и они проступают на теле
            new BodySocket { name = "Хвост", parent = "хребет", attach = 0.100f, attachOffset = new Vector3(0.000f, -0.033f, -0.563f),   baseSize = new Vector3(0.120f, 0.120f, 0.120f), baseEuler = new Vector3(25.00f, 0.00f, 0.00f), graft = true },  // КАЛИБР места; форму (сегментность) несёт орган
            new BodySocket { name = "Рога", parent = "голова", attach = 0.750f, attachOffset = new Vector3(0.405f, -0.029f, 0.044f),    baseSize = new Vector3(0.100f, 0.100f, 0.100f), mirrorX = true, graft = true }, // КАЛИБР: НАД макушкой (верх головы 1.88) и наружу — лопасть не врастает в череп
            new BodySocket { name = "Игломёт", parent = "хребет", attach = 0.650f, attachOffset = new Vector3(0.000f, 0.217f, -0.521f), baseSize = new Vector3(0.160f, 0.160f, 0.160f), baseEuler = new Vector3(-135.00f, 0.00f, 0.00f), graft = true }, // КАЛИБР + ОРИЕНТАЦИЯ: батарея растёт СО СПИНЫ веером ВВЕРХ-НАЗАД (поворот −135° разворачивает форму целиком). Уровень ЛОПАТОК и калибр крупнее — ниже она терялась за гребнем игл
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
                new OrganPart { scale = new Vector3(1.30f, 0.22f, 1.24f), offset = new Vector3(0.00f, 0.40f, 0.06f), shape = PartShape.Sphere }, // лопаточная мышца
                new OrganPart { scale = new Vector3(0.98f, 0.25f, 0.98f), offset = new Vector3(0.00f, 0.38f, 0.06f), euler = new Vector3(-21f, 0f, 0f), shape = PartShape.Capsule }, // лопатка→плечевой
                new OrganPart { scale = new Vector3(0.86f, 0.21f, 0.88f), offset = new Vector3(0.00f, 0.17f, 0.07f), euler = new Vector3(23f, 0f, 0f), shape = PartShape.Capsule }, // плечевой→локоть
                new OrganPart { scale = new Vector3(0.68f, 0.33f, 0.70f), offset = new Vector3(0.00f, -0.08f, 0.04f), euler = new Vector3(-5f, 0f, 0f), shape = PartShape.Capsule }, // локоть→запястье
                new OrganPart { scale = new Vector3(0.50f, 0.15f, 0.52f), offset = new Vector3(0.00f, -0.32f, 0.07f), euler = new Vector3(-4f, 0f, 0f), shape = PartShape.Capsule }, // запястье→путовый
                new OrganPart { scale = new Vector3(0.82f, 0.10f, 0.95f), offset = new Vector3(0.00f, -0.45f, 0.12f) }, // лапа с когтями
            } },
            new Organ { organName = "Волчьи ноги",   slot = "Ноги",   hotkey = "2", cost = 4, moveSpeed = 9f, dashSpeed = 30f, visualScale = new Vector3(1f, 1f, 1.2f), visualParts = new[] {
                new OrganPart { scale = new Vector3(1.30f, 0.34f, 1.24f), offset = new Vector3(0.00f, 0.32f, -0.06f), shape = PartShape.Sphere }, // мышца бедра
                new OrganPart { scale = new Vector3(0.95f, 0.36f, 0.95f), offset = new Vector3(0.00f, 0.33f, -0.06f), euler = new Vector3(-13f, 0f, 0f), shape = PartShape.Capsule }, // таз→колено
                new OrganPart { scale = new Vector3(0.70f, 0.36f, 0.72f), offset = new Vector3(0.00f, -0.01f, -0.10f), euler = new Vector3(23f, 0f, 0f), shape = PartShape.Capsule }, // колено→пятка
                new OrganPart { scale = new Vector3(0.50f, 0.28f, 0.52f), offset = new Vector3(0.00f, -0.30f, -0.16f), euler = new Vector3(-6f, 0f, 0f), shape = PartShape.Capsule }, // пятка→плюсна
                new OrganPart { scale = new Vector3(0.38f, 0.09f, 0.30f), offset = new Vector3(0.00f, -0.15f, -0.25f) }, // пяточный отросток
                new OrganPart { scale = new Vector3(0.80f, 0.10f, 0.95f), offset = new Vector3(0.00f, -0.45f, -0.09f) }, // лапа
            } },
            new Organ { organName = "Волчье сердце", slot = "Сердце", hotkey = "3", cost = 6, atkCooldown = 0.30f, hpBonus = 1.75f, staminaBonus = 0.5f, staminaRegenBonus = 0.25f, regen = 3f, regenOOC = 0f, visualParts = new[] {
                new OrganPart { scale = new Vector3(0.78f, 0.86f, 1.22f), offset = new Vector3(0.00f, 0.20f, 0.06f), shape = PartShape.Sphere }, // грудная клетка ВОЛКА, ВЕРХ: рёберный свод — глубокий и сжатый с боков, сидит ВЫСОКО (мотор гончей)
                new OrganPart { scale = new Vector3(0.62f, 0.54f, 0.88f), offset = new Vector3(0.00f, -0.22f, 0.02f), shape = PartShape.Sphere }, // ...и СУЖЕНИЕ к подреберью: одним шаром грудь читалась пузом, а у бегуна рёбра сходятся книзу
            } }, // «заживает как на собаке»: реген 2→3, чтобы босс вернул свои 6/с (Blend на Э=2), а волки затягивали раны на глазах. +175%: лёгкое тело, огромный мотор → волк-NPC 68 HP, вервольф ровно 300. Постоянный реген ВМЕСТО тихого в покое (вне-боя — фича человеческого сердца)
            new Organ { organName = "Нюх",           slot = "Чутьё",  hotkey = "4", cost = 3, dashCooldown = 0.45f, enablesScent = true },
            new Organ { organName = "Пасть",         slot = "Пасть",  hotkey = "5", cost = 5, enablesBite = true, enablesHowl = true, bleedStacks = 2, howlRadius = 14f, howlStunAt = 2f, enablesConstrict = true, constrictStage = 1, nativeChassis = "Волк", visualScale = new Vector3(1.1f, 1f, 1.25f) }, // укус + кровь + ГОЛОС + ХВАТ пастью; МОРДА: на человечьем шасси садится на его «лицо» → морда вервольфа
            new Organ { organName = "Шкура",         slot = "Шкура",  hotkey = "6", cost = 4, damageReduction = 0.3f },
        };
        // СОКЕТ-ПЛАН волка (4-ногий): ТЕ ЖЕ имена, что у человека (имя = Organ.slot), позиции из BuildBlocky.
        // mirrorX даёт ЧЕТЫРЕ лапы и ДВА уха одной записью (раньше пара выглядела единым блоком)
        wolf.sockets = new[]
        {
            new BodySocket { name = "хребет", localPos = new Vector3(0.000f, 0.937f, 0.000f), baseSize = new Vector3(0.427f, 0.625f, 1.295f), hidden = true }, // ПОЗВОНОЧНИК: корень графа тела. Служебный — не рисуется и НЕ слот органа, поэтому покров (Шкура/Иглы) не может утащить за собой скелет
            new BodySocket { name = "голова", parent = "шея", attach = 1.000f, attachOffset = new Vector3(0.000f, -0.060f, 0.060f),    baseSize = new Vector3(0.300f, 0.345f, 0.420f), baseEuler = new Vector3(22f, 0f, 0f), parts = new[] {
                new OrganPart { scale = new Vector3(1.00f, 1.00f, 0.60f), offset = new Vector3(0.00f, 0.02f, -0.22f), shape = PartShape.Sphere }, // череп (эллипсоид)
                new OrganPart { scale = new Vector3(1.06f, 0.72f, 0.30f), offset = new Vector3(0.00f, -0.14f, -0.16f), shape = PartShape.Sphere }, // скулы
                new OrganPart { scale = new Vector3(0.80f, 0.78f, 0.26f), offset = new Vector3(0.00f, 0.02f, 0.08f) }, // лоб-стоп
                new OrganPart { scale = new Vector3(0.64f, 0.58f, 0.34f), offset = new Vector3(0.00f, -0.06f, 0.32f) }, // переносица
            } }, // телесное место
            new BodySocket { name = "Пасть", parent = "голова", attach = 1.000f, attachOffset = new Vector3(0.000f, -0.140f, 0.190f),     baseSize = new Vector3(0.190f, 0.215f, 0.260f), baseEuler = new Vector3(4f, 0f, 0f), parts = new[] {
                new OrganPart { scale = new Vector3(1.00f, 0.95f, 0.46f), offset = new Vector3(0.00f, 0.02f, -0.27f), shape = PartShape.Sphere }, // основание морды
                new OrganPart { scale = new Vector3(0.82f, 0.74f, 0.42f), offset = new Vector3(0.00f, 0.00f, 0.02f), shape = PartShape.Sphere }, // средняя часть
                new OrganPart { scale = new Vector3(0.60f, 0.54f, 0.32f), offset = new Vector3(0.00f, -0.04f, 0.28f), shape = PartShape.Sphere }, // кончик морды
                new OrganPart { scale = new Vector3(0.42f, 0.36f, 0.16f), offset = new Vector3(0.00f, -0.06f, 0.44f), shape = PartShape.Sphere }, // мочка носа
                new OrganPart { scale = new Vector3(0.70f, 0.32f, 0.58f), offset = new Vector3(0.00f, -0.28f, -0.06f) }, // нижняя челюсть
            } },
            new BodySocket { name = "уши", parent = "голова", attach = 0.620f, attachOffset = new Vector3(0.320f, 0.520f, -0.150f),    baseSize = new Vector3(0.110f, 0.185f, 0.070f), baseEuler = new Vector3(0.00f, 0.00f, 25.00f), mirrorX = true, parts = new[] {
                new OrganPart { scale = new Vector3(0.90f, 0.44f, 0.90f), offset = new Vector3(0.00f, -0.26f, 0.00f), euler = new Vector3(-6f, 0f, -7f) }, // основание уха
                new OrganPart { scale = new Vector3(0.62f, 0.40f, 0.70f), offset = new Vector3(0.02f, 0.04f, -0.02f), euler = new Vector3(-6f, 0f, -7f) }, // середина уха
                new OrganPart { scale = new Vector3(0.30f, 0.34f, 0.46f), offset = new Vector3(0.04f, 0.34f, -0.04f), euler = new Vector3(-6f, 0f, -7f) }, // острие уха
            } }, // телесное место
            new BodySocket { name = "шея", parent = "хребет", attach = 1.000f, attachOffset = new Vector3(0.000f, 0.219f, 0.009f),       baseSize = new Vector3(0.205f, 0.250f, 0.560f), baseEuler = new Vector3(-22f, 0f, 0f), parts = new[] {
                new OrganPart { scale = new Vector3(0.92f, 1.11f, 0.80f), offset = new Vector3(0.00f, 0.00f, 0.00f), euler = new Vector3(90f, 0f, 0f), shape = PartShape.Capsule }, // шея одной массой
                new OrganPart { scale = new Vector3(1.02f, 0.40f, 0.58f), offset = new Vector3(0.00f, 0.32f, -0.14f), shape = PartShape.Sphere }, // загривок
            } }, // телесное место
            new BodySocket { name = "Шкура", parent = "хребет", attach = 0.500f,    baseSize = new Vector3(0.427f, 0.625f, 1.296f), parts = new[] {
                new OrganPart { scale = new Vector3(0.92f, 0.88f, 0.92f), offset = new Vector3(0.00f, 0.05f, -0.02f), shape = PartShape.Sphere }, // корпус
                // грудная клетка УШЛА В ОРГАН «Волчье сердце» — её лепит сердце, чужое перестраивает силуэт
                new OrganPart { scale = new Vector3(0.90f, 0.64f, 0.60f), offset = new Vector3(0.00f, 0.16f, 0.20f), shape = PartShape.Sphere }, // холка (закрыта)
                new OrganPart { scale = new Vector3(0.90f, 0.84f, 0.42f), offset = new Vector3(0.00f, 0.06f, -0.30f), shape = PartShape.Sphere }, // круп
                new OrganPart { scale = new Vector3(0.52f, 0.80f, 0.46f), offset = new Vector3(0.34f, -0.22f, -0.30f), shape = PartShape.Sphere }, // бедро (пр)
                new OrganPart { scale = new Vector3(0.52f, 0.80f, 0.46f), offset = new Vector3(-0.34f, -0.22f, -0.30f), shape = PartShape.Sphere }, // бедро (лев)
                new OrganPart { scale = new Vector3(0.46f, 0.72f, 0.38f), offset = new Vector3(0.32f, -0.14f, 0.26f), shape = PartShape.Sphere }, // лопатка (пр)
                new OrganPart { scale = new Vector3(0.46f, 0.72f, 0.38f), offset = new Vector3(-0.32f, -0.14f, 0.26f), shape = PartShape.Sphere }, // лопатка (лев)
            } },
            new BodySocket { name = "Руки", parent = "хребет", attach = 0.780f, attachOffset = new Vector3(0.258f, -0.941f, 0.061f),   baseSize = new Vector3(0.128f, 0.740f, 0.170f), mirrorX = true },
            new BodySocket { name = "Ноги", parent = "хребет", attach = 0.220f, attachOffset = new Vector3(0.281f, -0.925f, -0.061f),   baseSize = new Vector3(0.140f, 0.760f, 0.200f), mirrorX = true },
            new BodySocket { name = "Хвост", parent = "хребет", attach = 0.000f, attachOffset = new Vector3(0.000f, -0.130f, -0.115f),    baseSize = new Vector3(0.150f, 0.150f, 0.880f), baseEuler = new Vector3(-42f, 0f, 0f), parts = new[] {
                new OrganPart { scale = new Vector3(1.35f, 1.35f, 0.15f), offset = new Vector3(0.00f, 0.00f, 0.42f), shape = PartShape.Sphere }, // ШАРНИР основания
                new OrganPart { scale = new Vector3(1.00f, 1.00f, 0.28f), offset = new Vector3(0.00f, 0.00f, 0.26f) }, // сегмент 1
                new OrganPart { scale = new Vector3(0.88f, 0.88f, 0.26f), offset = new Vector3(0.00f, 0.00f, 0.00f) }, // сегмент 2
                new OrganPart { scale = new Vector3(0.74f, 0.74f, 0.24f), offset = new Vector3(0.00f, 0.00f, -0.25f) }, // сегмент 3
                new OrganPart { scale = new Vector3(0.60f, 0.60f, 0.22f), offset = new Vector3(0.00f, 0.00f, -0.46f) }, // кисточка
            } }, // СВОЙ хвост (не графт): крепится сверху крупа, продолжением позвоночника
            new BodySocket { name = "Сердце", inner = true, parent = "хребет", attach = 0.500f, attachOffset = new Vector3(0.000f, 0.060f, 0.000f), baseSize = new Vector3(0.400f, 0.420f, 0.480f) }, // ГРУДНАЯ КОРОБКА волка: ГЛУБЖЕ, чем шире — грудь бегуна // внутреннее место: форму (грудную клетку) даёт орган
            new BodySocket { name = "Чутьё",  inner = true },  // формы у нюха нет, и деталь не родится сама собой; дай органу форму (термо-ямки) — место проступит, флаги править не придётся
            // закрытые места (пустыми не рисуются): волк с лосиными рогами / ежиным игломётом читается сразу
            new BodySocket { name = "Рога", parent = "голова", attach = 0.600f, attachOffset = new Vector3(0.320f, 0.500f, -0.100f),    baseSize = new Vector3(0.137f, 0.137f, 0.137f), mirrorX = true, graft = true }, // КАЛИБР: над черепом и наружу
            new BodySocket { name = "Игломёт", parent = "хребет", attach = 0.550f, attachOffset = new Vector3(0.000f, 0.426f, 0.044f),    baseSize = new Vector3(0.198f, 0.198f, 0.198f), baseEuler = new Vector3(-10.00f, 0.00f, 0.00f), graft = true }, // КАЛИБР; хребет ГОРИЗОНТАЛЬНЫЙ — доворот не нужен, выше спины (верх туши 0.85)
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
            new BodySocket { name = "Пасть",  codeDriven = true }, // [ANIM] ведёт SnakeBodyChain
            new BodySocket { name = "Шкура",  codeDriven = true }, // [ANIM] ведёт SnakeBodyChain
            new BodySocket { name = "Тело",   codeDriven = true }, // [ANIM] ведёт SnakeBodyChain
            new BodySocket { name = "Хвост",  codeDriven = true }, // [ANIM] ведёт SnakeBodyChain
            new BodySocket { name = "Сердце", inner = true },  // грудной клетки у этого вида пока нет — формы нет, деталь и не родится
            new BodySocket { name = "Чутьё",  inner = true },  // формы у нюха нет, и деталь не родится сама собой; дай органу форму (термо-ямки) — место проступит, флаги править не придётся
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
                new OrganPart { scale = new Vector3(1.44f, 0.44f, 1.38f), offset = new Vector3(0.00f, 0.28f, -0.05f), shape = PartShape.Sphere }, // мышца бедра
                new OrganPart { scale = new Vector3(1.24f, 0.38f, 1.22f), offset = new Vector3(0.00f, 0.32f, -0.05f), euler = new Vector3(-14f, 0f, 0f), shape = PartShape.Capsule }, // таз→колено
                new OrganPart { scale = new Vector3(1.08f, 0.20f, 1.10f), offset = new Vector3(0.00f, 0.13f, 0.02f), shape = PartShape.Sphere }, // колено
                new OrganPart { scale = new Vector3(0.90f, 0.20f, 0.94f), offset = new Vector3(0.00f, 0.04f, -0.06f), euler = new Vector3(28f, 0f, 0f), shape = PartShape.Capsule }, // колено→пятка
                new OrganPart { scale = new Vector3(0.68f, 0.14f, 0.72f), offset = new Vector3(0.00f, -0.06f, -0.14f), shape = PartShape.Sphere }, // пятка
                new OrganPart { scale = new Vector3(0.56f, 0.30f, 0.60f), offset = new Vector3(0.00f, -0.22f, -0.13f), euler = new Vector3(-5f, 0f, 0f), shape = PartShape.Capsule }, // пятка→плюсна
                new OrganPart { scale = new Vector3(0.42f, 0.08f, 0.34f), offset = new Vector3(0.00f, -0.05f, -0.21f) }, // пяточный отросток
                new OrganPart { scale = new Vector3(0.46f, 0.12f, 0.48f), offset = new Vector3(0.00f, -0.40f, -0.09f), euler = new Vector3(14f, 0f, 0f), shape = PartShape.Capsule }, // путо
                new OrganPart { scale = new Vector3(0.66f, 0.09f, 0.84f), offset = new Vector3(0.00f, -0.46f, -0.05f) }, // копыто
            } }, // длинные ноги: шаг ровный, а рывок = ДЛИННЫЙ мощный ТАРАН (35 > волчьих 30 + вдвое дольше → прёт быстро и далеко)
            new Organ { organName = "Глотка",         slot = "Пасть",  hotkey = "5", cost = 4, enablesBellow = true }, // РЁВ (K2): кин-лоси в берсерк на месте, чужим страх
            new Organ { organName = "Слух",           slot = "Чутьё",  hotkey = "4", cost = 3, dashCooldown = 0.7f, keenHearing = true, hearingMult = 2f }, // ОСТРЫЙ СЛУХ: вдвое дальше + различение вида + волны звука на экране (лось — слухач при слабом зрении)
            new Organ { organName = "Лосиное сердце", slot = "Сердце", hotkey = "3", cost = 6, hpBonus = 2f, staminaBonus = 0.6f, staminaRegenBonus = 0f, regen = 1f, regenOOC = 0f, atkCooldown = 0.5f, bleedResist = true, visualParts = new[] {
                new OrganPart { scale = new Vector3(1.06f, 1.00f, 1.10f), offset = new Vector3(0.00f, 0.00f, 0.04f), shape = PartShape.Sphere }, // грудная клетка ЛОСЯ
                new OrganPart { scale = new Vector3(0.70f, 0.56f, 1.02f), offset = new Vector3(0.00f, 0.52f, -0.20f), shape = PartShape.Sphere }, // ГОРБ (холка): РАСТЯНУТ вдоль спины и поднят — у лося это длинный гребень остистых отростков под тяжёлую голову с рогами, а не ком на загривке
            } }, // +200% HP + КРОВЕУПОРНОСТЬ: сердце ТАНКА — явный HP-король (обгоняет волчьи 1.75); у массивного лося своё преимущество (гора HP), а кровь ему особенно опасна (% от макс HP)
            new Organ { organName = "Толстая шкура",  slot = "Шкура",  hotkey = "6", cost = 5, damageReduction = 0.35f, visualScale = new Vector3(1.15f, 1.1f, 1f) }, // броня против ПРЯМОГО урона (не крови)
            new Organ { organName = "Рога",           slot = "Рога",   hotkey = "8", cost = 5, enablesAntler = true, visualScale = new Vector3(1f, 1f, 1f), visualParts = new[] {
                new OrganPart { scale = new Vector3(0.75f, 0.60f, 0.75f), offset = new Vector3(0.05f, 0.00f, 0.00f), shape = PartShape.Sphere }, // розетка
                new OrganPart { scale = new Vector3(0.46f, 0.65f, 0.48f), offset = new Vector3(0.28f, 0.22f, 0.02f), euler = new Vector3(0f, 0f, -62f), shape = PartShape.Capsule }, // короткий ствол
                new OrganPart { scale = new Vector3(1.90f, 0.26f, 2.20f), offset = new Vector3(1.35f, 0.56f, 0.05f), euler = new Vector3(0f, 0f, 16f) }, // лопата — УЖЕ прежней: у живого лося она изрезана глубокими вырезами, примитивом их не сделать, поэтому силуэт «гребёнки» держат отростки, а не доска
                new OrganPart { scale = new Vector3(0.32f, 1.10f, 0.34f), offset = new Vector3(0.50f, 0.80f, 1.00f), euler = new Vector3(56f, 0f, 14f), shape = PartShape.Capsule }, // глазной отросток
                new OrganPart { scale = new Vector3(0.32f, 1.10f, 0.34f), offset = new Vector3(1.55f, 1.05f, 1.05f), euler = new Vector3(0f, 0f, 10f), shape = PartShape.Capsule }, // палец 1
                new OrganPart { scale = new Vector3(0.32f, 1.35f, 0.34f), offset = new Vector3(1.95f, 1.29f, 0.70f), euler = new Vector3(0f, 0f, 10f), shape = PartShape.Capsule }, // палец 2
                new OrganPart { scale = new Vector3(0.32f, 1.50f, 0.34f), offset = new Vector3(2.15f, 1.43f, 0.05f), euler = new Vector3(0f, 0f, 10f), shape = PartShape.Capsule }, // палец 3
                new OrganPart { scale = new Vector3(0.32f, 1.35f, 0.34f), offset = new Vector3(1.95f, 1.29f, -0.70f), euler = new Vector3(0f, 0f, 10f), shape = PartShape.Capsule }, // палец 4
                new OrganPart { scale = new Vector3(0.32f, 1.10f, 0.34f), offset = new Vector3(1.55f, 1.05f, -1.05f), euler = new Vector3(0f, 0f, 10f), shape = PartShape.Capsule }, // палец 5
            } }, // ФОРМА ЛОСИНАЯ — задана ОРГАНОМ, одна на все шасси (место даёт лишь калибр). Лопасть РАЗВЕДЕНА НАРУЖУ (рыскание 32°, зеркалится сама): вдоль тела она читалась козырьком над мордой, а не рогами // ПРИДАТОК (химерный слот): удар рогами — откидывание + кровь. Форма ЛОСИНАЯ (лопасть-лопата) задана местом у каждого шасси — масштаб свой, вид один
        };
        // СОКЕТ-ПЛАН лося (ходульная туша: ноги ≈ полроста, горб над холкой, рога веером над головой).
        // Числа перенесены из статичной сборки MoosePrefab (ходульность lift=0.5 уже вживлена в координаты)
        moose.sockets = new[]
        {
            new BodySocket { name = "хребет", localPos = new Vector3(0.000f, 1.930f, 0.000f), baseSize = new Vector3(0.703f, 0.900f, 2.284f), hidden = true }, // ПОЗВОНОЧНИК: корень графа тела. Служебный — не рисуется и НЕ слот органа, поэтому покров (Толстая шкура) не тащит за собой скелет
            new BodySocket { name = "голова", parent = "шея", attach = 1.000f, attachOffset = new Vector3(0.000f, -0.066f, 0.148f), baseSize = new Vector3(0.390f, 0.440f, 0.450f), baseEuler = new Vector3(40f, 0f, 0f), parts = new[] {
                new OrganPart { scale = new Vector3(1.00f, 0.94f, 0.62f), offset = new Vector3(0.00f, 0.03f, -0.17f) }, // череп
                new OrganPart { scale = new Vector3(0.74f, 0.70f, 0.52f), offset = new Vector3(0.00f, -0.04f, 0.24f) }, // переносица
            } }, // 40 гасит наклон шеи (−38) до +2 — РОВНО столько же, сколько у Пасти: череп и морда в ОДНУ линию, излома нет
            new BodySocket { name = "Пасть", parent = "голова", attach = 1.000f, attachOffset = new Vector3(0.000f, -0.150f, 0.344f),  baseSize = new Vector3(0.300f, 0.350f, 0.470f), baseEuler = new Vector3(0f, 0f, 0f), parts = new[] {
                // МОРДА — СУЖАЮЩАЯСЯ ЦЕПЬ, а не ствол с нашлёпками: прежняя сборка (капсула + шар горбинки
                // сверху + шар губы) читалась разъехавшейся, потому что накладки были ШИРЕ и ГЛУБЖЕ ствола.
                // Теперь звенья идут встык по длине и сужаются 0.30 → 0.18 м (у лося нос ≈ 0.6 ширины основания)
                new OrganPart { scale = new Vector3(1.00f, 0.94f, 0.42f), offset = new Vector3(0.00f, 0.02f, -0.28f), shape = PartShape.Sphere }, // основание морды (у черепа)
                new OrganPart { scale = new Vector3(0.88f, 0.86f, 0.44f), offset = new Vector3(0.00f, 0.10f, -0.02f), shape = PartShape.Sphere }, // переносица с ГОРБИНКОЙ — римский профиль лося
                new OrganPart { scale = new Vector3(0.74f, 0.68f, 0.42f), offset = new Vector3(0.00f, 0.00f, 0.22f), shape = PartShape.Sphere }, // спинка носа — ниже и уже
                new OrganPart { scale = new Vector3(0.60f, 0.52f, 0.26f), offset = new Vector3(0.00f, -0.04f, 0.40f), shape = PartShape.Sphere }, // мочка с ноздрями
                new OrganPart { scale = new Vector3(0.66f, 0.46f, 0.34f), offset = new Vector3(0.00f, -0.26f, 0.34f), shape = PartShape.Sphere }, // нависающая верхняя губа — примета лося
                new OrganPart { scale = new Vector3(0.56f, 0.86f, 0.20f), offset = new Vector3(0.00f, -0.24f, -0.10f), euler = new Vector3(90f, 0f, 0f), shape = PartShape.Capsule }, // нижняя челюсть — короче морды
                // ПОДЪЁМ МЕСТА (attachOffset.y −0.318 → −0.150) убирает ЗИГЗАГ: морда висела на треть высоты
                // головы ниже центра черепа, и профиль ломался уступом. Теперь лоб и спинка носа — одна дуга
            } }, // длинная морда с горбинкой; излом к черепу УБРАН (0) — «голова задрана, моська прямо» шло именно от него
            new BodySocket { name = "уши", parent = "голова", attach = 0.700f, attachOffset = new Vector3(0.590f, 0.341f, -0.511f),    baseSize = new Vector3(0.115f, 0.300f, 0.100f), baseEuler = new Vector3(-24f, 0f, 26f), mirrorX = true, parts = new[] {
                new OrganPart { scale = new Vector3(0.92f, 1.00f, 0.72f), offset = new Vector3(0.00f, 0.00f, 0.00f) }, // раковина
                new OrganPart { scale = new Vector3(0.58f, 0.52f, 0.50f), offset = new Vector3(0.00f, 0.44f, -0.08f) }, // кончик
            } },
            new BodySocket { name = "шея", parent = "горб", attach = 1.000f, attachOffset = new Vector3(0.000f, 0.252f, -0.021f),    baseSize = new Vector3(0.420f, 0.560f, 0.700f), baseEuler = new Vector3(-38f, 0f, 0f), parts = new[] {
                new OrganPart { scale = new Vector3(0.94f, 1.20f, 0.77f), offset = new Vector3(0.00f, 0.00f, 0.00f), euler = new Vector3(90f, 0f, 0f), shape = PartShape.Capsule }, // шея одной массой
                new OrganPart { scale = new Vector3(0.54f, 0.64f, 0.32f), offset = new Vector3(0.00f, -0.34f, 0.32f), euler = new Vector3(20f, 0f, 0f), shape = PartShape.Sphere }, // ПОДГРУДОК (висячая складка). Прошлый заход убрал «торпеду», но заодно срезал свес до 5 см — складка пропала. Свес вернул (10 см), а торпеду снимает УЗОСТЬ (0.23 при шее 0.40) и сдвиг ВПЕРЁД, к голове: у лося складка висит под челюстью, а не тянется вдоль всей шеи
            } },
            new BodySocket { name = "горб", parent = "хребет", attach = 0.735f, attachOffset = new Vector3(0.000f, 0.367f, -0.020f),   baseSize = new Vector3(0.660f, 0.680f, 1.850f), parts = new[] {
                new OrganPart { scale = new Vector3(0.92f, 1.00f, 0.60f), offset = new Vector3(0.00f, 0.00f, 0.08f), shape = PartShape.Sphere }, // ПИК холки — ШИРОКИЙ (0.61 при груди 0.59): холка это массивный горб над лопатками. Узкий высокий гребень читался вертикальным плавником, а не телом
                new OrganPart { scale = new Vector3(1.00f, 0.80f, 0.72f), offset = new Vector3(0.00f, -0.20f, 0.06f), shape = PartShape.Sphere }, // плечевая масса — основание под пиком
                new OrganPart { scale = new Vector3(0.88f, 0.62f, 0.62f), offset = new Vector3(0.00f, -0.22f, -0.34f), shape = PartShape.Sphere }, // ДЛИННЫЙ задний скат к спине — он и читает силуэт
                new OrganPart { scale = new Vector3(0.80f, 0.66f, 0.44f), offset = new Vector3(0.00f, -0.16f, 0.40f), shape = PartShape.Sphere }, // передний скат — ВЫТЯНУТ вперёд поверх основания шеи: место удлинено 1.30 → 1.60, и скат теперь накрывает излом «шея↔холка», а не упирается в него торцом
            } }, // ХОЛКА: остистые отростки грудных позвонков + мышца. Выше крупа на ~10% — как у живого лося; читается СИЛУЭТОМ (резкий подъём от шеи, долгий спад к крестцу), а не узостью
            new BodySocket { name = "Шкура",  parent = "хребет", attach = 0.500f,   baseSize = new Vector3(0.703f, 0.900f, 2.284f), parts = new[] {
                new OrganPart { scale = new Vector3(0.96f, 0.92f, 0.90f), offset = new Vector3(0.00f, 0.04f, -0.02f), shape = PartShape.Sphere }, // корпус
                // грудная клетка УШЛА В ОРГАН «Лосиное сердце» — её лепит сердце, чужое перестраивает силуэт
                new OrganPart { scale = new Vector3(0.92f, 0.86f, 0.44f), offset = new Vector3(0.00f, 0.06f, -0.32f), shape = PartShape.Sphere }, // круп
                new OrganPart { scale = new Vector3(0.50f, 0.82f, 0.44f), offset = new Vector3(0.33f, -0.20f, -0.32f), shape = PartShape.Sphere }, // бедро (пр)
                new OrganPart { scale = new Vector3(0.50f, 0.82f, 0.44f), offset = new Vector3(-0.33f, -0.20f, -0.32f), shape = PartShape.Sphere }, // бедро (лев)
                new OrganPart { scale = new Vector3(0.44f, 0.74f, 0.38f), offset = new Vector3(0.31f, -0.12f, 0.28f), shape = PartShape.Sphere }, // лопатка (пр)
                new OrganPart { scale = new Vector3(0.44f, 0.74f, 0.38f), offset = new Vector3(-0.31f, -0.12f, 0.28f), shape = PartShape.Sphere }, // лопатка (лев)
            } }, // корпус целиком (грудь+круп)
            new BodySocket { name = "Руки", parent = "хребет", attach = 0.780f, attachOffset = new Vector3(0.367f, -1.300f, 0.079f),   baseSize = new Vector3(0.170f, 1.520f, 0.193f), mirrorX = true }, // передние ходули (Копыто)
            new BodySocket { name = "Ноги", parent = "хребет", attach = 0.220f, attachOffset = new Vector3(0.367f, -1.283f, -0.089f),   baseSize = new Vector3(0.205f, 1.550f, 0.228f), mirrorX = true },
            new BodySocket { name = "Хвост", parent = "хребет", attach = 0.000f, attachOffset = new Vector3(0.000f, 0.310f, -0.010f),  baseSize = new Vector3(0.130f, 0.130f, 0.340f), baseEuler = new Vector3(-28f, 0f, 0f), parts = new[] {
                new OrganPart { scale = new Vector3(1.15f, 1.15f, 0.48f), offset = new Vector3(0.00f, 0.00f, 0.34f), shape = PartShape.Sphere }, // ШАРНИР основания (та же схема, что у волка) — уменьшен, хвост выдвинут назад и поднят: вылет кончика за круп 8 → 21 см
                new OrganPart { scale = new Vector3(1.00f, 1.00f, 0.44f), offset = new Vector3(0.00f, 0.00f, -0.02f) }, // сегмент 1
                new OrganPart { scale = new Vector3(0.80f, 0.80f, 0.40f), offset = new Vector3(0.00f, 0.00f, -0.42f) }, // сегмент 2
                new OrganPart { scale = new Vector3(0.60f, 0.60f, 0.32f), offset = new Vector3(0.00f, 0.00f, -0.76f) }, // кисточка
            } }, // ЕДИНАЯ СХЕМА ХВОСТА (шар-шарнир + сегменты вдоль Z): как у волка, только короче — лосиный обрубок. Шарнир сидит в крупе, а кончик ВЫХОДИТ за него на 17 см: прошлый заход утопил хвост целиком (кончик −1.21 при заднем крае крупа −1.23)
            new BodySocket { name = "Рога", parent = "голова", attach = 0.700f, attachOffset = new Vector3(0.330f, 0.420f, -0.060f),   baseSize = new Vector3(0.320f, 0.320f, 0.320f), baseEuler = new Vector3(-2f, 0f, 0f), mirrorX = true }, // СВОИ рога: −2 гасит наклон ветки «шея→голова» (+2) до нуля — лопата горизонтальна: КАЛИБР крупный. В ВИСКАХ (верх черепа 2.17) и ВБОК за габарит головы — раньше лопасти врастали в макушку и торчали из висков
            new BodySocket { name = "Сердце", inner = true, parent = "хребет", attach = 0.500f, attachOffset = new Vector3(0.000f, 0.040f, 0.000f), baseSize = new Vector3(0.640f, 0.760f, 0.820f) }, // ГРУДНАЯ КОРОБКА лося: самая объёмная // внутреннее место: форму (грудную клетку) даёт орган
            new BodySocket { name = "Чутьё",  inner = true },  // формы у нюха нет, и деталь не родится сама собой; дай органу форму (термо-ямки) — место проступит, флаги править не придётся
            new BodySocket { name = "Игломёт", parent = "хребет", attach = 0.550f, attachOffset = new Vector3(0.000f, 0.411f, -0.050f), baseSize = new Vector3(0.281f, 0.281f, 0.281f), baseEuler = new Vector3(-10.00f, 0.00f, 0.00f), graft = true }, // КАЛИБР (крупная туша): НА спине (верх туши 2.10) — основания шипов входят в корпус; сдвинут назад, не спорит с горбом
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
                new OrganPart { scale = new Vector3(0.62f, 0.74f, 0.34f), offset = new Vector3(0.00f, -0.10f, 0.33f), shape = PartShape.Sphere }, // плечи — перед сужен
                new OrganPart { scale = new Vector3(0.88f, 0.92f, 0.42f), offset = new Vector3(0.00f, -0.02f, 0.13f), shape = PartShape.Sphere }, // грудь
                new OrganPart { scale = new Vector3(1.00f, 1.00f, 0.46f), offset = new Vector3(0.00f, 0.02f, -0.09f), shape = PartShape.Sphere }, // середина — шире всех
                new OrganPart { scale = new Vector3(0.94f, 0.92f, 0.44f), offset = new Vector3(0.00f, 0.00f, -0.31f), shape = PartShape.Sphere }, // круп куполом
                new OrganPart { scale = new Vector3(0.07f, 0.62f, 0.07f), offset = new Vector3(-0.26f, 0.41f, 0.26f), euler = new Vector3(-20f, 0f, -20f) }, // шип
                new OrganPart { scale = new Vector3(0.07f, 0.62f, 0.07f), offset = new Vector3(-0.09f, 0.51f, 0.26f), euler = new Vector3(-20f, 0f, -7f) }, // шип
                new OrganPart { scale = new Vector3(0.07f, 0.62f, 0.07f), offset = new Vector3(0.09f, 0.51f, 0.26f), euler = new Vector3(-20f, 0f, 7f) }, // шип
                new OrganPart { scale = new Vector3(0.07f, 0.62f, 0.07f), offset = new Vector3(0.26f, 0.41f, 0.26f), euler = new Vector3(-20f, 0f, 20f) }, // шип
                new OrganPart { scale = new Vector3(0.07f, 0.62f, 0.07f), offset = new Vector3(-0.26f, 0.49f, 0.10f), euler = new Vector3(-28f, 0f, -20f) }, // шип
                new OrganPart { scale = new Vector3(0.07f, 0.62f, 0.07f), offset = new Vector3(-0.09f, 0.60f, 0.10f), euler = new Vector3(-28f, 0f, -7f) }, // шип
                new OrganPart { scale = new Vector3(0.07f, 0.62f, 0.07f), offset = new Vector3(0.09f, 0.60f, 0.10f), euler = new Vector3(-28f, 0f, 7f) }, // шип
                new OrganPart { scale = new Vector3(0.07f, 0.62f, 0.07f), offset = new Vector3(0.26f, 0.49f, 0.10f), euler = new Vector3(-28f, 0f, 20f) }, // шип
                new OrganPart { scale = new Vector3(0.07f, 0.62f, 0.07f), offset = new Vector3(-0.26f, 0.49f, -0.06f), euler = new Vector3(-28f, 0f, -20f) }, // шип
                new OrganPart { scale = new Vector3(0.07f, 0.62f, 0.07f), offset = new Vector3(-0.09f, 0.60f, -0.06f), euler = new Vector3(-28f, 0f, -7f) }, // шип
                new OrganPart { scale = new Vector3(0.07f, 0.62f, 0.07f), offset = new Vector3(0.09f, 0.60f, -0.06f), euler = new Vector3(-28f, 0f, 7f) }, // шип
                new OrganPart { scale = new Vector3(0.07f, 0.62f, 0.07f), offset = new Vector3(0.26f, 0.49f, -0.06f), euler = new Vector3(-28f, 0f, 20f) }, // шип
                new OrganPart { scale = new Vector3(0.07f, 0.62f, 0.07f), offset = new Vector3(-0.26f, 0.44f, -0.24f), euler = new Vector3(-42f, 0f, -20f) }, // шип
                new OrganPart { scale = new Vector3(0.07f, 0.62f, 0.07f), offset = new Vector3(-0.09f, 0.54f, -0.24f), euler = new Vector3(-42f, 0f, -7f) }, // шип
                new OrganPart { scale = new Vector3(0.07f, 0.62f, 0.07f), offset = new Vector3(0.09f, 0.54f, -0.24f), euler = new Vector3(-42f, 0f, 7f) }, // шип
                new OrganPart { scale = new Vector3(0.07f, 0.62f, 0.07f), offset = new Vector3(0.26f, 0.44f, -0.24f), euler = new Vector3(-42f, 0f, 20f) }, // шип
                new OrganPart { scale = new Vector3(0.07f, 0.62f, 0.07f), offset = new Vector3(-0.26f, 0.33f, -0.40f), euler = new Vector3(-42f, 0f, -20f) }, // шип
                new OrganPart { scale = new Vector3(0.07f, 0.62f, 0.07f), offset = new Vector3(-0.09f, 0.42f, -0.40f), euler = new Vector3(-42f, 0f, -7f) }, // шип
                new OrganPart { scale = new Vector3(0.07f, 0.62f, 0.07f), offset = new Vector3(0.09f, 0.42f, -0.40f), euler = new Vector3(-42f, 0f, 7f) }, // шип
                new OrganPart { scale = new Vector3(0.07f, 0.62f, 0.07f), offset = new Vector3(0.26f, 0.33f, -0.40f), euler = new Vector3(-42f, 0f, 20f) }, // шип
                new OrganPart { scale = new Vector3(0.34f, 0.52f, 0.34f), offset = new Vector3(0.30f, -0.30f, -0.28f), shape = PartShape.Sphere }, // бедро (пр)
                new OrganPart { scale = new Vector3(0.34f, 0.52f, 0.34f), offset = new Vector3(-0.30f, -0.30f, -0.28f), shape = PartShape.Sphere }, // бедро (лев)
                new OrganPart { scale = new Vector3(0.30f, 0.46f, 0.30f), offset = new Vector3(0.28f, -0.28f, 0.26f), shape = PartShape.Sphere }, // плечо (пр)
                new OrganPart { scale = new Vector3(0.30f, 0.46f, 0.30f), offset = new Vector3(-0.28f, -0.28f, 0.26f), shape = PartShape.Sphere }, // плечо (лев)
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
            new BodySocket { name = "хребет", localPos = new Vector3(0.00f, 0.50f, -0.05f), baseSize = new Vector3(0.76f, 0.60f, 1.05f), hidden = true }, // ПОЗВОНОЧНИК: корень графа тела. Служебный — не рисуется и НЕ слот органа, поэтому Иглы (покров) не тащат за собой скелет
            new BodySocket { name = "голова", parent = "хребет", attach = 1.000f, attachOffset = new Vector3(0.000f, -0.167f, 0.043f), baseSize = new Vector3(0.30f, 0.28f, 0.32f), parts = new[] {
                new OrganPart { scale = new Vector3(1.00f, 1.00f, 1.00f), offset = new Vector3(0.00f, 0.00f, 0.00f), shape = PartShape.Sphere }, // череп
            } },
            new BodySocket { name = "Пасть", parent = "голова", attach = 1.000f, attachOffset = new Vector3(0.000f, -0.214f, 0.062f),  baseSize = new Vector3(0.220f, 0.200f, 0.280f), parts = new[] {
                new OrganPart { scale = new Vector3(1.00f, 0.94f, 0.40f), offset = new Vector3(0.00f, 0.00f, -0.28f), shape = PartShape.Sphere }, // основание морды
                new OrganPart { scale = new Vector3(0.74f, 0.70f, 0.34f), offset = new Vector3(0.00f, -0.04f, -0.02f), shape = PartShape.Sphere }, // конус
                new OrganPart { scale = new Vector3(0.48f, 0.46f, 0.30f), offset = new Vector3(0.00f, -0.08f, 0.22f), shape = PartShape.Sphere }, // сужение
                new OrganPart { scale = new Vector3(0.24f, 0.22f, 0.18f), offset = new Vector3(0.00f, -0.10f, 0.42f), shape = PartShape.Sphere }, // нос-пуговка
            } },
            new BodySocket { name = "уши", parent = "голова", attach = 0.700f, attachOffset = new Vector3(0.350f, 0.571f, -0.325f),    baseSize = new Vector3(0.110f, 0.130f, 0.060f), mirrorX = true, parts = new[] {
                new OrganPart { scale = new Vector3(0.95f, 0.65f, 1.00f), offset = new Vector3(0.00f, -0.18f, 0.00f), shape = PartShape.Sphere }, // раковина
                new OrganPart { scale = new Vector3(1.00f, 0.70f, 0.85f), offset = new Vector3(0.00f, 0.24f, -0.04f), shape = PartShape.Sphere }, // верх
            } },
            new BodySocket { name = "Шкура",  parent = "хребет", attach = 0.500f,  baseSize = new Vector3(0.76f, 0.60f, 1.05f), parts = new[] {
                new OrganPart { scale = new Vector3(0.96f, 0.96f, 0.86f), offset = new Vector3(0.00f, 0.02f, -0.04f), shape = PartShape.Sphere }, // корпус (единый объём)
                new OrganPart { scale = new Vector3(0.66f, 0.74f, 0.42f), offset = new Vector3(0.00f, -0.10f, 0.32f), shape = PartShape.Sphere }, // плечи — перед сужен
                new OrganPart { scale = new Vector3(0.94f, 0.92f, 0.44f), offset = new Vector3(0.00f, 0.02f, -0.28f), shape = PartShape.Sphere }, // круп
            } },
            new BodySocket { name = "Руки", parent = "хребет", attach = 0.780f, attachOffset = new Vector3(0.316f, -0.617f, 0.072f),   baseSize = new Vector3(0.15f, 0.26f, 0.19f), mirrorX = true, parts = new[] {
                new OrganPart { scale = new Vector3(0.92f, 0.42f, 0.92f), offset = new Vector3(0.00f, 0.27f, -0.04f), euler = new Vector3(-8f, 0f, 0f), shape = PartShape.Capsule }, // плечо
                new OrganPart { scale = new Vector3(0.76f, 0.44f, 0.80f), offset = new Vector3(0.00f, -0.14f, 0.06f), euler = new Vector3(10f, 0f, 0f), shape = PartShape.Capsule }, // предплечье
                new OrganPart { scale = new Vector3(1.02f, 0.60f, 1.02f), offset = new Vector3(0.00f, 0.08f, 0.01f), shape = PartShape.Sphere }, // локоть
                new OrganPart { scale = new Vector3(0.78f, 0.18f, 1.45f), offset = new Vector3(0.00f, -0.41f, 0.20f) }, // кисть (стопоходящий)
            } }, // передние лапки
            new BodySocket { name = "Ноги", parent = "хребет", attach = 0.220f, attachOffset = new Vector3(0.316f, -0.617f, 0.023f),   baseSize = new Vector3(0.15f, 0.26f, 0.19f), mirrorX = true },
            new BodySocket { name = "Игломёт", parent = "хребет", attach = 0.550f, attachOffset = new Vector3(0.000f, 0.333f, 0.207f),baseSize = new Vector3(0.24f, 0.24f, 0.24f), baseEuler = new Vector3(-10f, 0f, 0f) }, // СВОИ иглы: КАЛИБР (форма-плита у органа), хребет горизонтальный; ВЫШЕ корпуса (верх туши 0.81), иначе тонет в теле
            new BodySocket { name = "Сердце", inner = true },  // грудной клетки у этого вида пока нет — формы нет, деталь и не родится
            new BodySocket { name = "Чутьё",  inner = true },  // формы у нюха нет, и деталь не родится сама собой; дай органу форму (термо-ямки) — место проступит, флаги править не придётся
            new BodySocket { name = "Тело",   hidden = true }, // Игольчатое тело — ФОРМА (клубок), своей детали нет
            new BodySocket { name = "Хвост", parent = "хребет", attach = 0.000f, attachOffset = new Vector3(0.000f, 0.100f, 0.030f),  baseSize = new Vector3(0.075f, 0.075f, 0.150f), baseEuler = new Vector3(-40f, 0f, 0f), parts = new[] {
                new OrganPart { scale = new Vector3(1.35f, 1.35f, 0.55f), offset = new Vector3(0.00f, 0.00f, 0.32f), shape = PartShape.Sphere }, // ШАРНИР основания — единая схема хвоста
                new OrganPart { scale = new Vector3(1.00f, 1.00f, 0.60f), offset = new Vector3(0.00f, 0.00f, -0.10f) }, // сегмент
                new OrganPart { scale = new Vector3(0.72f, 0.72f, 0.40f), offset = new Vector3(0.00f, 0.00f, -0.60f) }, // кончик
            } }, // КАЛИБР
            new BodySocket { name = "Рога", parent = "голова", attach = 0.700f, attachOffset = new Vector3(0.233f, 0.429f, -0.263f),   baseSize = new Vector3(0.10f, 0.10f, 0.10f), mirrorX = true, graft = true }, // КАЛИБР
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

            // ГРАФ ХРЕБТА: висячий родитель = место молча уедет в корень (молчаливый провал, как когда-то
            // опечатка в visualPart); цикл = билдер упрётся в предохранитель глубины и соберёт мусор
            foreach (var s in chassis.sockets)
            {
                if (s == null || string.IsNullOrEmpty(s.parent)) continue;
                if (!places.Contains(s.parent))
                {
                    Debug.LogWarning($"Граф «{chassis.speciesName}»: у места «{s.name}» родитель «{s.parent}» НЕ НАЙДЕН — место встанет в корень.");
                    continue;
                }
                var seen = new System.Collections.Generic.HashSet<string> { s.name };
                for (var cur = s; !string.IsNullOrEmpty(cur.parent); )
                {
                    var next = System.Array.Find(chassis.sockets, x => x != null && x.name == cur.parent);
                    if (next == null) break;
                    if (!seen.Add(next.name))
                    {
                        Debug.LogError($"Граф «{chassis.speciesName}»: ЦИКЛ в цепи родителей через «{next.name}» — тело не соберётся.");
                        break;
                    }
                    cur = next;
                }
            }
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
