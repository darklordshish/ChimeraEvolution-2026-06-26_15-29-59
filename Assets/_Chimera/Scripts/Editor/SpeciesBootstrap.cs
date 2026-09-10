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
            new Organ { organName = "Хребет", slot = "хребет", chassisOnly = true, visualParts = new[] {
                new OrganPart { scale = new Vector3(1.00f, 0.20f, 0.62f), offset = new Vector3(0.00f, 0.36f, 0.04f), shape = PartShape.Sphere }, // плечевой пояс («вешалка» ключиц) — размах 2H = 0.47
                new OrganPart { scale = new Vector3(0.80f, 0.30f, 0.66f), offset = new Vector3(0.00f, -0.32f, 0.00f), shape = PartShape.Sphere }, // таз — клин 1.51H, снова шире талии (но уже плеч: мужской силуэт)
            } }, // СКЕЛЕТ: несущая структура шасси. chassisOnly — её не крадут графтом, как «Тело-хвост»
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
                new OrganPart { scale = new Vector3(0.72f, 0.07f, 0.74f), offset = new Vector3(0.00f, -0.30f, 0.00f), shape = PartShape.Sphere }, // колено
                new OrganPart { scale = new Vector3(0.64f, 0.50f, 0.64f), offset = new Vector3(0.00f, -0.22f, -0.01f), shape = PartShape.Capsule }, // голень
                new OrganPart { scale = new Vector3(0.80f, 0.16f, 0.78f), offset = new Vector3(0.00f, -0.13f, -0.06f), shape = PartShape.Sphere }, // икра
                new OrganPart { scale = new Vector3(0.78f, 0.10f, 1.55f), offset = new Vector3(0.00f, -0.46f, 0.20f) }, // стопа
            } },
            new Organ { organName = "Сердце", slot = "Сердце", hotkey = "3", cost = 6, atkCooldown = 0.45f, hpBonus = 0.5f, staminaBonus = 0.5f, staminaRegenBonus = 0.5f, regen = 0f, regenOOC = 0.75f, visualParts = new[] {
                new OrganPart { scale = new Vector3(0.90f, 0.86f, 0.70f), offset = new Vector3(0.00f, 0.00f, 0.04f), shape = PartShape.Sphere }, // грудная клетка ЧЕЛОВЕКА: ПЛОСКАЯ и широкая — рёбра сходятся на грудине
            } }, // СЕРДЦЕ ЛЕПИТ ГРУДЬ: форма ушла из торса в орган, поэтому чужое сердце перестраивает силуэт
            new Organ { organName = "Чутьё",  slot = "Чутьё",  hotkey = "4", cost = 3, dashCooldown = 0.7f, insight = true, visualParts = new[] {
                new OrganPart { scale = new Vector3(1.00f, 1.00f, 1.00f), shape = PartShape.Sphere, role = PartRole.Nose }, // нос
                new OrganPart { scale = new Vector3(1.00f, 1.00f, 0.70f), offset = new Vector3(0.00f, 0.00f, 0.00f), role = PartRole.Ear, shape = PartShape.Sphere }, // раковина
                new OrganPart { scale = new Vector3(0.80f, 0.42f, 0.60f), offset = new Vector3(0.00f, -0.40f, 0.06f), role = PartRole.Ear, shape = PartShape.Sphere }, // мочка
                new OrganPart { scale = new Vector3(1.00f, 1.00f, 1.00f), shape = PartShape.Sphere, role = PartRole.Eye, color = new Color(0.35f, 0.65f, 0.95f, 1f) }, // ЦВЕТ ГЛАЗА = КАНАЛ: прозрение — читает числа и намерения
            } }, // внутренний: сокет `inner` — своей детали нет, но форма органа проступает (цвет глаза = канал восприятия)
            new Organ { organName = "Рот",    slot = "Пасть",  hotkey = "5", cost = 3, enablesBite = false, enablesScream = true }, // лицо/пасть — ОТДЕЛЬНО от черепа: волчья Пасть сядет сюда же → морда вервольфа.
            //     БОЕВОЙ КЛИЧ: слот больше не мёртвый. Кусать человек не умеет, но кричит — ярость по своей крови (PlayerScream)
            new Organ { organName = "Кожа",   slot = "Шкура",  hotkey = "6", cost = 3, damageReduction = 0f },
        };
        // СОКЕТ-ПЛАН человека (прямоходящий). ИМЯ СОКЕТА = Organ.slot — одно и то же имя держит механику и
        // визуал, разойтись не могут. Те же имена у зверей → волчьи органы садятся на человечьи места (вервольф).
        // mirrorX — парное место (2 руки/ноги); inner — внутреннее (видно только формой органа); graft — закрытое место
        human.sockets = new[]
        {
            new BodySocket { name = "хребет", baseEuler = new Vector3(-10.000f, 0.000f, 0.000f), attachOffset = new Vector3(0.000f, -1.977f, 0.383f), parent = "шея", attach = 1.000f, baseSize = new Vector3(0.470f, 0.600f, 0.240f) }, // НЕСУЩИЙ ЦЕНТР: форму даёт орган «Хребет» (chassisOnly), поэтому место больше не служебное  // ЕДИНЫЙ ПЛАН ТЕЛА (спека 2026-08-27): ось от головы назад. Числа посчитаны Anatomy/tools/reroot.py и самопроверены — тело осталось на месте
            // ГОЛОВА БАЛАНСИРУЕТ НА ПОЗВОНОЧНИКЕ, а не стоит на нём сверху: позвонок входит в затылочное
            // отверстие — точка опоры лежит ПОД УШАМИ, лицо висит впереди неё, затылок нависает сзади.
            // Пока голова крепилась геометрическим низом, она садилась «шапкой на палку» (attachOffset.z был
            // −0.076, то есть ещё и назад). baseEuler −10 гасит наклон шеи: взгляд остаётся горизонтальным
            new BodySocket { name = "нос", parent = "голова", attach = 0.500f, attachOffset = new Vector3(0.00f, -0.10f, 0.36f), sizeRel = new Vector3(0.20f, 0.22f, 0.18f), formFrom = "Чутьё", formRole = PartRole.Nose }, // АДРЕС НОСА: форму даёт орган Чутья — сменил его, сменился и нос
            new BodySocket { name = "голова", localPos = new Vector3(0.000f, 1.698f, 0.029f), baseSize = new Vector3(0.178f, 0.270f, 0.216f), baseEuler = new Vector3(0.000f, 0.000f, 0.000f), parts = new[] {
                // ЭТЮД ГОЛОВЫ (Лумис): шар мозгового черепа + челюстной блок, между ними — плоскости лица.
                // Череп ОКРУГЛЫЙ (кость свода), лицевые плоскости ГРАНЁНЫЕ — то же правило «плоть/кость», что у зверей
                new OrganPart { scale = new Vector3(1.00f, 0.64f, 0.90f), offset = new Vector3(0.00f, 0.19f, -0.05f), shape = PartShape.Sphere }, // мозговой череп
                new OrganPart { scale = new Vector3(0.80f, 0.44f, 0.44f), offset = new Vector3(0.00f, 0.09f, -0.23f), shape = PartShape.Sphere }, // затылок — поджат и придвинут к своду: отдельным шаром он торчал «яйцом назад», между ним и черепом читалась борозда
                new OrganPart { scale = new Vector3(0.86f, 0.34f, 0.44f), offset = new Vector3(0.00f, 0.08f, 0.20f), shape = PartShape.Sphere }, // лоб — лобная кость ОКРУГЛА, кубом она резала череп плитой
                new OrganPart { scale = new Vector3(0.26f, 0.08f, 0.16f), offset = new Vector3(0.20f, 0.00f, 0.29f), shape = PartShape.Sphere }, // надбровная дуга (пр) — их ДВЕ, с переносицей между; сплошная полоса через лицо анатомии не знает
                new OrganPart { scale = new Vector3(0.26f, 0.08f, 0.16f), offset = new Vector3(-0.20f, 0.00f, 0.29f), shape = PartShape.Sphere }, // надбровная дуга (лев)
                new OrganPart { scale = new Vector3(0.94f, 0.30f, 0.60f), offset = new Vector3(0.00f, -0.05f, 0.04f), shape = PartShape.Sphere }, // скулы — самая широкая точка головы
                new OrganPart { scale = new Vector3(0.66f, 0.30f, 0.44f), offset = new Vector3(0.00f, -0.12f, 0.22f), shape = PartShape.Sphere }, // верхняя челюсть (лицевая масса)
                // НИЖНЯЯ ЧЕЛЮСТЬ — подкова из ДВУХ звеньев. Одним шаром она сидела на 2.6 см ПОЗАДИ верхней
                // челюсти, и лицо кончалось носом: классический «убегающий подбородок»
                new OrganPart { scale = new Vector3(0.78f, 0.30f, 0.40f), offset = new Vector3(0.00f, -0.20f, -0.04f), shape = PartShape.Sphere }, // ветвь челюсти — от угла вверх к уху
                new OrganPart { scale = new Vector3(0.66f, 0.26f, 0.52f), offset = new Vector3(0.00f, -0.30f, 0.14f), shape = PartShape.Sphere }, // тело челюсти — вперёд-вниз к подбородку
                new OrganPart { scale = new Vector3(0.34f, 0.18f, 0.24f), offset = new Vector3(0.00f, -0.33f, 0.28f), shape = PartShape.Sphere }, // подбородок — костный выступ, чуть позади кончика носа
            } }, // телесное место (органа нет). Ширина/высота 0.69 и глубина/высота 0.89 — череп по канону (0.65 / 0.80) плюс вынос носа
            new BodySocket { name = "Пасть", parent = "голова", attach = 1.000f, attachOffset = new Vector3(0.000f, -0.704f, 0.467f),  baseSize = new Vector3(0.100f, 0.080f, 0.060f), sizeRel = new Vector3(0.562f, 0.296f, 0.278f), parts = new[] {
                new OrganPart { scale = new Vector3(0.70f, 0.70f, 0.70f), offset = new Vector3(0.00f, 0.00f, -0.30f), shape = PartShape.Sphere }, // лицевая масса
            } },
            // УШИ — их у человека НЕ БЫЛО ВОВСЕ, при том что есть у волка, лося и ежа. Голова без них
            // теряет ~2 см ширины в самом заметном месте и анфас читается мельче, чем она есть по канону
            new BodySocket { name = "глаза", parent = "голова", attach = 0.500f, attachOffset = new Vector3(0.350f, 0.000f, 0.200f), baseSize = new Vector3(0.035f, 0.035f, 0.035f), sizeRel = new Vector3(0.197f, 0.130f, 0.162f), mirrorX = true, formFrom = "Чутьё", formRole = PartRole.Eye, parts = new[] {
                new OrganPart { scale = new Vector3(1.00f, 1.00f, 1.00f), shape = PartShape.Sphere, role = PartRole.Eye, color = new Color(0.10f, 0.10f, 0.12f, 1f) }, // ФОЛБЭК: тварь без Чутья не слепа, но глаз тускл
            } }, // МЕСТО НА КОЖЕ головы (посчитано лучом из её центра) — форму и цвет даёт ЧУТЬЁ
            new BodySocket { name = "уши", parent = "голова", attach = 0.500f, attachOffset = new Vector3(0.480f, -0.020f, -0.100f),  baseSize = new Vector3(0.022f, 0.062f, 0.032f), sizeRel = new Vector3(0.124f, 0.230f, 0.148f), baseEuler = new Vector3(-15f, 0f, 0f), mirrorX = true, formFrom = "Чутьё", formRole = PartRole.Ear },   // АДРЕС УХА: раковину рисует орган слуха, а не шасси — привил чужое Чутьё, и ухо стало чужим

            new BodySocket { name = "шея", parent = "голова", attach = 0.000f, attachOffset = new Vector3(0.000f, 0.041f, -0.089f),    baseSize = new Vector3(0.126f, 0.134f, 0.126f), sizeRel = new Vector3(0.708f, 0.496f, 0.583f), baseEuler = new Vector3(10.000f, 0.000f, 0.000f), parts = new[] {
                new OrganPart { scale = new Vector3(1.00f, 1.00f, 1.00f), offset = new Vector3(0.00f, 0.00f, 0.00f), shape = PartShape.Capsule }, // шея — ЦИЛИНДР, а не шар
                new OrganPart { scale = new Vector3(1.05f, 0.55f, 0.85f), offset = new Vector3(0.00f, 0.46f, -0.14f), shape = PartShape.Sphere }, // ЗАТЫЛОЧНО-ШЕЙНЫЙ ПЕРЕХОД: спереди череп накрывала челюсть, а СЗАДИ под нависающим затылком зияло 3 см пустоты. У человека там мышечный массив (полуостистая + верх трапеции) — им и закрываем
                new OrganPart { scale = new Vector3(1.10f, 0.42f, 1.20f), offset = new Vector3(0.00f, -0.44f, -0.06f), shape = PartShape.Sphere }, // переход к трапеции — шея не втыкается в грудь торцом
            } }, // УКОРОЧЕНА 0.185 → 0.150: голова не мала (7.4 головы в росте — канон), мал был её вклад в силуэт рядом с 20-см голой колонной шеи. У живого человека наружу торчит лишь ВЕРХ шеи, низ съеден трапецией
            new BodySocket { name = "Шкура",  parent = "хребет", attach = 0.500f,     baseSize = new Vector3(0.470f, 0.600f, 0.240f), sizeRel = new Vector3(1.000f, 1.000f, 1.000f), parts = new[] {
                // ЭТЮД ТОРСА: два объёма (грудная клетка-яйцо и таз-клин) + перемычка талии между ними, сверху
                // «вешалка» плечевого пояса. Мышцы — ТОНКИЕ НАКЛАДКИ поверх костных объёмов, а не отдельные шары:
                // силуэт держит скелет, мышца лишь читается на нём. Числа — от канона 8 голов (H = 0.234 м)
                // грудная клетка УШЛА В ОРГАН «Сердце» — теперь её лепит сердце, и чужое перестраивает силуэт
                new OrganPart { scale = new Vector3(0.56f, 0.30f, 0.68f), offset = new Vector3(0.00f, -0.06f, 0.00f), shape = PartShape.Sphere }, // талия — перемычка 1.12H, САМОЕ узкое место торса
                new OrganPart { scale = new Vector3(0.38f, 0.26f, 0.24f), offset = new Vector3(0.00f, -0.06f, 0.24f), shape = PartShape.Sphere }, // пресс — накладка вровень с животом
                new OrganPart { scale = new Vector3(0.34f, 0.24f, 0.42f), offset = new Vector3(0.18f, -0.37f, -0.15f), shape = PartShape.Sphere }, // ягодица (пр)
                new OrganPart { scale = new Vector3(0.34f, 0.24f, 0.42f), offset = new Vector3(-0.18f, -0.37f, -0.15f), shape = PartShape.Sphere }, // ягодица (лев)
            } },
            new BodySocket { name = "Руки", parent = "хребет", attach = 0.900f, attachOffset = new Vector3(0.409f, -0.658f, 0.000f),   baseSize = new Vector3(0.115f, 0.810f, 0.120f), sizeRel = new Vector3(0.245f, 1.350f, 0.500f), mirrorX = true },
            new BodySocket { name = "Ноги", parent = "хребет", attach = 0.050f, attachOffset = new Vector3(0.213f, -0.797f, 0.000f),   baseSize = new Vector3(0.160f, 0.985f, 0.210f), sizeRel = new Vector3(0.340f, 1.642f, 0.875f), mirrorX = true },
            // СЕРДЦЕ — ВНУТРЕННЕЕ, НО С МЕСТОМ: своего силуэта нет, проступает формой органа (грудная клетка).
            // Калибр и посадка — те же, что у торса, поэтому доли частей читаются 1:1 и родное сердце даёт
            // ровно прежнюю грудь; чужое приносит СВОЮ форму и перестраивает силуэт
            new BodySocket { name = "Сердце", inner = true, parent = "хребет", attach = 0.500f, attachOffset = new Vector3(0.000f, 0.150f, 0.000f), baseSize = new Vector3(0.380f, 0.340f, 0.280f), sizeRel = new Vector3(0.809f, 0.567f, 1.167f), parts = new[] {
                // МЯСО ПОВЕРХ КОСТИ. Мышцы груди живут В ДОЛЯХ ГРУДНОЙ КОРОБКИ, а её форму приносит Сердце —
                // значит чужое сердце перестраивает и мышцы, они не спорят с костью. Пока мышцы сидели в торсе,
                // кость приходилось раздувать, чтобы её было видно из-под них, и силуэт выходил пузатым
                new OrganPart { scale = new Vector3(0.97f, 0.60f, 0.53f), offset = new Vector3(0.00f, 0.44f, -0.05f), shape = PartShape.Sphere }, // трапеция — подводка от шеи к плечам
                new OrganPart { scale = new Vector3(0.77f, 0.39f, 0.26f), offset = new Vector3(0.00f, 0.16f, 0.26f), shape = PartShape.Sphere }, // грудные (пекторали) — плита поверх рёбер
                new OrganPart { scale = new Vector3(1.04f, 0.60f, 0.41f), offset = new Vector3(0.00f, -0.09f, -0.09f), shape = PartShape.Sphere }, // широчайшие — шире рёбер, дают конус к талии
            } }, // ГРУДНАЯ КОРОБКА человека: шире, чем глубже (рёбра сходятся на грудине) // ГРУДНАЯ КОРОБКА человека: шире, чем глубже (рёбра сходятся на грудине)
            new BodySocket { name = "Чутьё",  inner = true, parent = "голова", attach = 0.500f, sizeRel = new Vector3(0.500f, 0.500f, 0.500f) },  // ЧУВСТВА ЖИВУТ В ГОЛОВЕ. Своей формы у места нет, и деталь не родится сама собой — но АДРЕС нужен заранее: дашь органу форму (термо-ямки), и без родителя с калибром она сядет метровым кубом в начало координат. Доля от головы — одна на все виды
            // ЗАКРЫТЫЕ МЕСТА (graft — пустыми НЕ рисуются): у человека нет хвоста/рогов/игломёта, но привил
            // змеиный Хвост / лосиные Рога / ежиный Игломёт — и они проступают на теле
            new BodySocket { name = "Хвост", parent = "хребет", attach = 0.100f, attachOffset = new Vector3(0.000f, -0.033f, -0.563f),   baseSize = new Vector3(0.120f, 0.120f, 0.120f), sizeRel = new Vector3(0.255f, 0.200f, 0.500f), baseEuler = new Vector3(25.00f, 0.00f, 0.00f), graft = true },  // КАЛИБР места; форму (сегментность) несёт орган
            new BodySocket { name = "Рога", parent = "голова", attach = 0.750f, attachOffset = new Vector3(0.405f, -0.029f, 0.044f),    baseSize = new Vector3(0.100f, 0.100f, 0.100f), sizeRel = new Vector3(0.562f, 0.370f, 0.463f), mirrorX = true, graft = true }, // КАЛИБР: НАД макушкой (верх головы 1.88) и наружу — лопасть не врастает в череп
            new BodySocket { name = "Игломёт", parent = "хребет", attach = 0.650f, attachOffset = new Vector3(0.000f, 0.217f, -0.521f), baseSize = new Vector3(0.160f, 0.160f, 0.160f), sizeRel = new Vector3(0.340f, 0.267f, 0.667f), baseEuler = new Vector3(-135.00f, 0.00f, 0.00f), graft = true }, // КАЛИБР + ОРИЕНТАЦИЯ: батарея растёт СО СПИНЫ веером ВВЕРХ-НАЗАД (поворот −135° разворачивает форму целиком). Уровень ЛОПАТОК и калибр крупнее — ниже она терялась за гребнем игл
        };

        human.bones = new[]
        {
            // СКЕЛЕТ ВИДА «human» — ВЫГРУЖЕНО из Tools/Blender/species/human.py (tools/bones_to_cs.py).
            // Правится ТАМ, по анатомическим точкам; здесь только результат. Костей: 73, калибр (холка) 1.850 м
            new Bone { name = "грудина", socket = "Сердце", origin = new Vector3(0f, 1.36f, 0.2f), length = 0.21f, dir = new Vector3(-6.55f, 0f, 0f), r0 = 0.022f, r1 = 0.027f, section = 1.6f, depth = 0.45f },   // грудина: ПЛОСКАЯ и широкая, в отличие от килевидной волчьей
            new Bone { name = "ключица", socket = "Сердце", origin = new Vector3(0f, 1.569f, 0.176f), length = 0.262f, dir = new Vector3(-111.08f, 0f, -51.03f), r0 = 0.018f, r1 = 0.015f, mirrorX = true },   // ключица: распорка между грудиной и лопаткой. Волчьей ключицы в модели НЕТ — там рудимент, хрящевой узелок в мышце, силуэта он не задаёт
            new Bone { name = "череп", socket = "голова", origin = new Vector3(0f, 1.674f, -0.102f), length = 0.213f, dir = new Vector3(64.68f, 0f, 0f), r0 = 0.03f },   // мозговой череп ОБОЛОЧКОЙ по 25 сечениям, снятым с 4 аспектов (шаблон wolf_skull_data)
            new Bone { name = "скула", socket = "голова", origin = new Vector3(0.083f, 1.695f, -0.028f), length = 0.098f, dir = new Vector3(75.98f, 0f, -38.61f), r0 = 0.018f, r1 = 0.017f, section = 0.75f, depth = 1.25f, mirrorX = true },   // задняя ветвь скуловой дуги
            new Bone { name = "глазница.р", socket = "голова", origin = new Vector3(0.07f, 1.724f, 0.056f), length = 0.047f, dir = new Vector3(-78.57f, 0f, 52.63f), r0 = 0.052f, r1 = 0.013f, depth = 1.1f, mirrorX = true },   // глазница: конус внутрь черепа
            new Bone { name = "лёгкие", socket = "Сердце", origin = new Vector3(0f, 1.309f, 0.035f), length = 0.261f, dir = new Vector3(-4.67f, 0f, 0f), r0 = 0.152f, r1 = 0.13f, section = 1.1f, depth = 0.85f, layer = BodyLayer.Muscle },   // содержимое грудной полости. У человека клетка ШИРЕ, чем глубока: section > depth, у волка наоборот — это и есть разница торсов в одной строке
            new Bone { name = "брюшина", socket = "хребет", origin = new Vector3(0f, 1.309f, 0.035f), length = 0.289f, dir = new Vector3(-173.01f, 0f, 0f), r0 = 0.144f, r1 = 0.126f, section = 1.05f, depth = 0.9f, layer = BodyLayer.Muscle },   // содержимое брюшной полости
            new Bone { name = "ухо.п", socket = "Чутьё", origin = new Vector3(0.089f, 1.698f, -0.059f), length = 0.051f, dir = new Vector3(4.23f, 0f, -8.4f), r0 = 0.03f, r1 = 0.022f, section = 0.35f, mirrorX = true, layer = BodyLayer.Feature },   // ушная раковина: ПРИЖАТАЯ пластина у черепа. Признак чувства принадлежит органу — привил человеку волчий Нюх, получил волчьи уши
            new Bone { name = "затылочный", socket = "голова", parent = "череп", attach = 0f, length = 0f, dir = new Vector3(0f, 0f, 0f), r0 = 0.044f, r1 = 0.037f },   // затылочная кость: несёт свод от атланта — как у волка, связывает шею с оболочкой
            new Bone { name = "скула_п", socket = "голова", parent = "скула", attach = 1f, length = 0.053f, dir = new Vector3(78.79f, 0f, 43.67f), r0 = 0.017f, r1 = 0.015f, section = 0.75f, depth = 1.2f, mirrorX = true },   // передняя ветвь дуги
            new Bone { name = "шея", socket = "шея", parent = "затылочный", length = 0.115f, dir = new Vector3(138.97f, 0f, 0f), r0 = 0.027f, r1 = 0.03f, section = 1.2f },   // шейный отдел одним звеном: у человека он короче волчьего вчетверо
            new Bone { name = "челюсть", socket = "Пасть", parent = "затылочный", length = 0.118f, dir = new Vector3(58.64f, 0f, -48.87f), r0 = 0.033f, r1 = 0.026f, section = 0.55f, depth = 1.1f, mirrorX = true },   // ветвь нижней челюсти
            new Bone { name = "грудной", socket = "хребет", parent = "шея", attach = 1f, length = 0.311f, dir = new Vector3(-27.06f, 0f, 0f), r0 = 0.032f, r1 = 0.035f, section = 1.3f },   // грудной отдел: назад и вверх
            new Bone { name = "челюсть_тело", socket = "Пасть", parent = "челюсть", attach = 1f, length = 0.182f, dir = new Vector3(-69.69f, 0f, 65.38f), r0 = 0.03f, r1 = 0.022f, section = 0.55f, depth = 1.1f, mirrorX = true },   // тело челюсти: подбородок — передняя точка
            new Bone { name = "поясница", socket = "хребет", parent = "грудной", attach = 1f, length = 0.083f, dir = new Vector3(-23.14f, 0f, 0f), r0 = 0.035f, r1 = 0.041f, section = 1.4f },   // поясничный отдел
            new Bone { name = "лопатка", socket = "Руки", parent = "грудной", attach = 0f, length = 0.272f, dir = new Vector3(-67.41f, 0f, -48.47f), r0 = 0.048f, r1 = 0.024f, section = 0.45f, depth = 1.5f, mirrorX = true },   // лопатка: ПЛАСТИНА на спине. Поверхность её рисует корпус (SURFACE_BONE), а слот остаётся `Руки` — она несёт руку
            new Bone { name = "ребро1", socket = "Сердце", parent = "грудной", attach = 0.05f, length = 0.102f, dir = new Vector3(-72.5f, 0f, -42.45f), r0 = 0.011f, r1 = 0.009f, section = 1.5f, depth = 0.55f, mirrorX = true },
            new Bone { name = "ребро2", socket = "Сердце", parent = "грудной", attach = 0.144f, length = 0.111f, dir = new Vector3(-73.8f, 0f, -45.89f), r0 = 0.011f, r1 = 0.009f, section = 1.5f, depth = 0.55f, mirrorX = true },
            new Bone { name = "ребро3", socket = "Сердце", parent = "грудной", attach = 0.24f, length = 0.12f, dir = new Vector3(-75.06f, 0f, -48.55f), r0 = 0.011f, r1 = 0.009f, section = 1.5f, depth = 0.55f, mirrorX = true },
            new Bone { name = "ребро4", socket = "Сердце", parent = "грудной", attach = 0.34f, length = 0.125f, dir = new Vector3(-76.36f, 0f, -49.89f), r0 = 0.011f, r1 = 0.009f, section = 1.5f, depth = 0.55f, mirrorX = true },
            new Bone { name = "ребро5", socket = "Сердце", parent = "грудной", attach = 0.44f, length = 0.128f, dir = new Vector3(-77.73f, 0f, -50.9f), r0 = 0.011f, r1 = 0.009f, section = 1.5f, depth = 0.55f, mirrorX = true },
            new Bone { name = "ребро6", socket = "Сердце", parent = "грудной", attach = 0.54f, length = 0.13f, dir = new Vector3(-79.21f, 0f, -51.49f), r0 = 0.011f, r1 = 0.009f, section = 1.5f, depth = 0.55f, mirrorX = true },
            new Bone { name = "ребро7", socket = "Сердце", parent = "грудной", attach = 0.64f, length = 0.128f, dir = new Vector3(-80.41f, 0f, -51.72f), r0 = 0.011f, r1 = 0.009f, section = 1.5f, depth = 0.55f, mirrorX = true },
            new Bone { name = "ребро8", socket = "Сердце", parent = "грудной", attach = 0.74f, length = 0.125f, dir = new Vector3(-81.44f, 0f, -51.72f), r0 = 0.011f, r1 = 0.009f, section = 1.5f, depth = 0.55f, mirrorX = true },
            new Bone { name = "ребро9", socket = "Сердце", parent = "грудной", attach = 0.84f, length = 0.118f, dir = new Vector3(-82.8f, 0f, -50.9f), r0 = 0.011f, r1 = 0.009f, section = 1.5f, depth = 0.55f, mirrorX = true },
            new Bone { name = "ребро10", socket = "Сердце", parent = "грудной", attach = 0.94f, length = 0.111f, dir = new Vector3(-84.33f, 0f, -49.75f), r0 = 0.011f, r1 = 0.009f, section = 1.5f, depth = 0.55f, mirrorX = true },
            new Bone { name = "крестец", socket = "хребет", parent = "поясница", attach = 1f, length = 0.1f, dir = new Vector3(48.35f, 0f, 0f), r0 = 0.041f, r1 = 0.054f, section = 1.5f },   // крестец: корень графа, как у всех видов
            new Bone { name = "ребро1с", socket = "Сердце", parent = "ребро1", length = 0.111f, dir = new Vector3(0.64f, 0f, 17.94f), r0 = 0.009f, r1 = 0.008f, section = 1.5f, depth = 0.55f, mirrorX = true },
            new Bone { name = "ребро2с", socket = "Сердце", parent = "ребро2", length = 0.117f, dir = new Vector3(0.58f, 0f, 18.66f), r0 = 0.009f, r1 = 0.008f, section = 1.5f, depth = 0.55f, mirrorX = true },
            new Bone { name = "ребро3с", socket = "Сердце", parent = "ребро3", length = 0.122f, dir = new Vector3(0.51f, 0f, 19.08f), r0 = 0.009f, r1 = 0.008f, section = 1.5f, depth = 0.55f, mirrorX = true },
            new Bone { name = "ребро4с", socket = "Сердце", parent = "ребро4", length = 0.125f, dir = new Vector3(0.45f, 0f, 19.24f), r0 = 0.009f, r1 = 0.008f, section = 1.5f, depth = 0.55f, mirrorX = true },
            new Bone { name = "ребро5с", socket = "Сердце", parent = "ребро5", length = 0.127f, dir = new Vector3(0.39f, 0f, 19.33f), r0 = 0.009f, r1 = 0.008f, section = 1.5f, depth = 0.55f, mirrorX = true },
            new Bone { name = "ребро6с", socket = "Сердце", parent = "ребро6", length = 0.127f, dir = new Vector3(0.33f, 0f, 19.37f), r0 = 0.009f, r1 = 0.008f, section = 1.5f, depth = 0.55f, mirrorX = true },
            new Bone { name = "ребро7с", socket = "Сердце", parent = "ребро7", length = 0.125f, dir = new Vector3(0.27f, 0f, 19.38f), r0 = 0.009f, r1 = 0.008f, section = 1.5f, depth = 0.55f, mirrorX = true },
            new Bone { name = "ребро8с", socket = "Сердце", parent = "ребро8", length = 0.122f, dir = new Vector3(0.23f, 0f, 19.38f), r0 = 0.009f, r1 = 0.008f, section = 1.5f, depth = 0.55f, mirrorX = true },
            new Bone { name = "ребро9с", socket = "Сердце", parent = "ребро9", length = 0.117f, dir = new Vector3(0.17f, 0f, 19.3f), r0 = 0.009f, r1 = 0.008f, section = 1.5f, depth = 0.55f, mirrorX = true },
            new Bone { name = "ребро10с", socket = "Сердце", parent = "ребро10", length = 0.111f, dir = new Vector3(0.1f, 0f, 19.19f), r0 = 0.009f, r1 = 0.008f, section = 1.5f, depth = 0.55f, mirrorX = true },
            new Bone { name = "плечо", socket = "Руки", parent = "лопатка", attach = 1f, length = 0.332f, dir = new Vector3(68.81f, 0f, -1.23f), r0 = 0.033f, r1 = 0.028f, mirrorX = true },
            new Bone { name = "трапециевидная.м", socket = "хребет", parent = "грудной", attach = 0.1f, length = 0.239f, dir = new Vector3(-78.24f, 0f, -50.13f), r0 = 0.056f, section = 0.9f, depth = 0.8f, mirrorX = true, layer = BodyLayer.Muscle },   // трапециевидная: от шеи к плечу, покатость надплечья
            new Bone { name = "ребро1н", socket = "Сердце", parent = "ребро1с", length = 0.111f, dir = new Vector3(15.29f, 0f, 68.68f), r0 = 0.008f, r1 = 0.007f, section = 1.2f, depth = 0.6f, mirrorX = true },
            new Bone { name = "ребро2н", socket = "Сердце", parent = "ребро2с", length = 0.121f, dir = new Vector3(18.76f, 0f, 74.87f), r0 = 0.008f, r1 = 0.007f, section = 1.2f, depth = 0.6f, mirrorX = true },
            new Bone { name = "ребро3н", socket = "Сердце", parent = "ребро3с", length = 0.13f, dir = new Vector3(24.19f, 0f, 79.7f), r0 = 0.008f, r1 = 0.007f, section = 1.2f, depth = 0.6f, mirrorX = true },
            new Bone { name = "ребро4н", socket = "Сердце", parent = "ребро4с", length = 0.134f, dir = new Vector3(27.72f, 0f, 82.04f), r0 = 0.008f, r1 = 0.007f, section = 1.2f, depth = 0.6f, mirrorX = true },
            new Bone { name = "ребро5н", socket = "Сердце", parent = "ребро5с", length = 0.137f, dir = new Vector3(31.19f, 0f, 83.84f), r0 = 0.008f, r1 = 0.007f, section = 1.2f, depth = 0.6f, mirrorX = true },
            new Bone { name = "ребро6н", socket = "Сердце", parent = "ребро6с", length = 0.138f, dir = new Vector3(31.92f, 0f, 84.97f), r0 = 0.008f, r1 = 0.007f, section = 1.2f, depth = 0.6f, mirrorX = true },
            new Bone { name = "ребро7н", socket = "Сердце", parent = "ребро7с", length = 0.134f, dir = new Vector3(27.32f, 0f, 85.09f), r0 = 0.008f, r1 = 0.007f, section = 1.2f, depth = 0.6f, mirrorX = true },
            new Bone { name = "ребро8н", socket = "Сердце", parent = "ребро8с", length = 0.128f, dir = new Vector3(20.07f, 0f, 84.4f), r0 = 0.008f, r1 = 0.007f, section = 1.2f, depth = 0.6f, mirrorX = true },
            new Bone { name = "ребро9н", socket = "Сердце", parent = "ребро9с", length = 0.116f, dir = new Vector3(9.87f, 0f, 81.25f), r0 = 0.008f, r1 = 0.007f, section = 1.2f, depth = 0.6f, mirrorX = true },
            new Bone { name = "ребро10н", socket = "Сердце", parent = "ребро10с", length = 0.103f, dir = new Vector3(4.06f, 0f, 76.51f), r0 = 0.008f, r1 = 0.007f, section = 1.2f, depth = 0.6f, mirrorX = true },
            new Bone { name = "подвздошная", socket = "Ноги", parent = "крестец", attach = 1f, length = 0.185f, dir = new Vector3(-148.43f, 0f, -64.16f), r0 = 0.056f, r1 = 0.044f, section = 0.4f, depth = 1.6f, mirrorX = true },   // крыло подвздошной: широкая чаша, стоит почти вертикально
            new Bone { name = "предплечье", socket = "Руки", parent = "плечо", attach = 1f, length = 0.283f, dir = new Vector3(-1.13f, 0f, 14.54f), r0 = 0.031f, r1 = 0.018f, section = 1.1f, depth = 0.9f, mirrorX = true },   // две кости одним звеном: мясо вверху, сухожилия внизу
            new Bone { name = "дельтовидная.м", socket = "Руки", parent = "ключица", attach = 0.75f, length = 0.19f, dir = new Vector3(-42.57f, 0f, 7.06f), r0 = 0.063f, mirrorX = true, layer = BodyLayer.Muscle },   // дельтовидная: ШАПКА плеча. Ею человек и читается плечистым — у волка её работы не видно
            new Bone { name = "грудная.м", socket = "Сердце", parent = "грудина", attach = 0.55f, length = 0.279f, dir = new Vector3(-90.86f, 0f, -52.12f), r0 = 0.067f, section = 1.2f, depth = 0.7f, mirrorX = true, layer = BodyLayer.Muscle },   // большая грудная: ПЛАСТ на груди от грудины к плечу
            new Bone { name = "широчайшая.м", socket = "хребет", parent = "поясница", attach = 0.45f, length = 0.347f, dir = new Vector3(-124.69f, 0f, -38.78f), r0 = 0.078f, section = 0.8f, mirrorX = true, layer = BodyLayer.Muscle },   // широчайшая: треугольник от поясницы к плечу, задаёт V-образную спину
            new Bone { name = "вертлуж", socket = "Ноги", parent = "подвздошная", attach = 1f, length = 0.166f, dir = new Vector3(118.36f, 0f, 2.6f), r0 = 0.044f, r1 = 0.039f, section = 0.55f, depth = 1.1f, mirrorX = true },   // тело подвздошной до вертлужной впадины
            new Bone { name = "кисть", socket = "Руки", parent = "предплечье", attach = 1f, length = 0.158f, dir = new Vector3(3.8f, 0f, 0.6f), r0 = 0.026f, r1 = 0.013f, section = 1.7f, depth = 0.45f, mirrorX = true },   // КИСТЬ — ГЛАВНАЯ ТОЧКА СВАПА игры и ближайшая к камере от первого лица. Здесь она одним звеном: подробность придёт, когда дойдёт до неё черёд
            new Bone { name = "трицепс.м", socket = "Руки", parent = "лопатка", attach = 0.3f, length = 0.453f, dir = new Vector3(45.72f, 0f, -0.41f), r0 = 0.056f, section = 0.85f, depth = 1.1f, mirrorX = true, layer = BodyLayer.Muscle },   // трицепс: задняя масса плеча
            new Bone { name = "бицепс.м", socket = "Руки", parent = "лопатка", length = 0.387f, dir = new Vector3(68.65f, 0f, 0.88f), r0 = 0.048f, section = 0.95f, mirrorX = true, layer = BodyLayer.Muscle },   // двуглавая: передняя масса плеча
            new Bone { name = "косая_живота.м", socket = "хребет", parent = "ребро8с", attach = 0.7f, length = 0.206f, dir = new Vector3(103.64f, 0f, -13.32f), r0 = 0.063f, section = 0.7f, depth = 0.9f, mirrorX = true, layer = BodyLayer.Muscle },   // наружная косая: бок и талия
            new Bone { name = "седалищная", socket = "Ноги", parent = "вертлуж", attach = 1f, length = 0.177f, dir = new Vector3(90.17f, 0f, -65.61f), r0 = 0.039f, r1 = 0.033f, section = 0.7f, mirrorX = true },
            new Bone { name = "лобковая", socket = "Ноги", parent = "вертлуж", attach = 1f, length = 0.168f, dir = new Vector3(-4.85f, 0f, 36.14f), r0 = 0.028f, r1 = 0.022f, section = 0.9f, depth = 0.7f, mirrorX = true },   // лобковая ветвь: пол таза. Без неё брюшная полость течёт по средней линии — мина, найденная на волке 28.08
            new Bone { name = "бедро", socket = "Ноги", parent = "вертлуж", attach = 1f, length = 0.444f, dir = new Vector3(-10.41f, 0f, -32.66f), r0 = 0.043f, r1 = 0.036f, section = 0.95f, mirrorX = true },
            new Bone { name = "сгибатели_п.м", socket = "Руки", parent = "предплечье", attach = 0.05f, length = 0.292f, dir = new Vector3(0.31f, 0f, 0.05f), r0 = 0.048f, depth = 0.9f, mirrorX = true, layer = BodyLayer.Muscle },   // сгибатели предплечья: мясо у локтя, сухожилия у запястья
            new Bone { name = "голень", socket = "Ноги", parent = "бедро", attach = 1f, length = 0.442f, dir = new Vector3(3.42f, 0f, -5.23f), r0 = 0.035f, r1 = 0.025f, section = 0.9f, depth = 1.1f, mirrorX = true },
            new Bone { name = "прямая_живота.м", socket = "хребет", parent = "грудина", attach = 0.1f, length = 0.429f, dir = new Vector3(-161.7f, 0f, -1.86f), r0 = 0.056f, section = 1.3f, depth = 0.5f, mirrorX = true, layer = BodyLayer.Muscle },   // прямая живота: передняя стенка от груди к лобку
            new Bone { name = "ягодичная.м", socket = "Ноги", parent = "подвздошная", attach = 0.55f, length = 0.231f, dir = new Vector3(95.51f, 0f, -10.37f), r0 = 0.085f, depth = 1.2f, mirrorX = true, layer = BodyLayer.Muscle },   // большая ягодичная: у человека ОГРОМНА и задаёт зад — прямохождение держится на ней
            new Bone { name = "пяточная", socket = "Ноги", parent = "голень", attach = 1f, length = 0.126f, dir = new Vector3(24.51f, 0f, -36.59f), r0 = 0.031f, r1 = 0.024f, section = 0.85f, mirrorX = true },   // пяточная кость: у человека она ОПОРНАЯ и лежит на земле
            new Bone { name = "стопа", socket = "Ноги", parent = "голень", attach = 1f, length = 0.186f, dir = new Vector3(-44.76f, 0f, 48.5f), r0 = 0.029f, r1 = 0.024f, section = 1.6f, depth = 0.7f, mirrorX = true },   // предплюсна и плюсна: широкий свод
            new Bone { name = "четырёхглавая.м", socket = "Ноги", parent = "вертлуж", attach = 0.4f, length = 0.551f, dir = new Vector3(-8.16f, 0f, -27.36f), r0 = 0.085f, depth = 1.1f, mirrorX = true, layer = BodyLayer.Muscle },   // четырёхглавая: передняя масса бедра
            new Bone { name = "двуглавая_бедра.м", socket = "Ноги", parent = "седалищная", attach = 0.95f, length = 0.462f, dir = new Vector3(-82.38f, 0f, -23.43f), r0 = 0.074f, section = 0.85f, mirrorX = true, layer = BodyLayer.Muscle },   // задняя группа бедра
            new Bone { name = "икроножная.м", socket = "Ноги", parent = "бедро", attach = 0.95f, length = 0.265f, dir = new Vector3(3.13f, 0f, -4.79f), r0 = 0.07f, depth = 1.2f, mirrorX = true, layer = BodyLayer.Muscle },   // икроножная: у человека она ВЫСОКО и толста — оттого голень мясистая сверху и сухая снизу
            new Bone { name = "пальцы_н", socket = "Ноги", parent = "стопа", attach = 1f, length = 0.111f, dir = new Vector3(-22.96f, 0f, 17.53f), r0 = 0.019f, r1 = 0.012f, section = 1.8f, depth = 0.5f, mirrorX = true },
            new Bone { name = "ахилл.м", socket = "Ноги", parent = "голень", attach = 0.62f, length = 0.257f, dir = new Vector3(8.24f, 0f, -14.38f), r0 = 0.026f, section = 0.9f, depth = 1.1f, mirrorX = true, layer = BodyLayer.Muscle },   // ахиллово сухожилие: тонкий тяж к пятке. Урок с волка — ниже мышцы кость нельзя оставлять голой, иначе обмер даёт голяшку тоньше собственной кости
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
            new Organ { organName = "Хребет", slot = "хребет", chassisOnly = true, visualParts = new[] {
                new OrganPart { scale = new Vector3(0.851f, 0.639f, 0.600f), offset = new Vector3(0.000f, 0.160f, 0.200f), shape = PartShape.Sphere }, // ХОЛКА — высшая точка 1.170 (опора всех долей: «холка» это ОНА)
                new OrganPart { scale = new Vector3(0.851f, 0.839f, 0.420f), offset = new Vector3(0.000f, -0.084f, -0.300f), shape = PartShape.Sphere }, // круп — верх 1.100: ХОЛКА ВЫШЕ КРУПА на 6%, спад к хвосту плавный
                new OrganPart { scale = new Vector3(0.400f, 0.800f, 0.300f), offset = new Vector3(0.280f, -0.221f, -0.300f), shape = PartShape.Sphere }, // бедро (пр)
                new OrganPart { scale = new Vector3(0.400f, 0.800f, 0.300f), offset = new Vector3(-0.280f, -0.221f, -0.300f), shape = PartShape.Sphere }, // бедро (лев)
                new OrganPart { scale = new Vector3(0.380f, 0.720f, 0.260f), offset = new Vector3(0.270f, -0.140f, 0.260f), shape = PartShape.Sphere }, // лопатка (пр)
                new OrganPart { scale = new Vector3(0.380f, 0.720f, 0.260f), offset = new Vector3(-0.270f, -0.140f, 0.260f), shape = PartShape.Sphere }, // лопатка (лев)
            } }, // СКЕЛЕТ: несущая структура шасси. chassisOnly — её не крадут графтом, как «Тело-хвост».
                 // ШИРИНА 0.85 калибра (была 0.90): волк УЗКИЙ анфас — шары одинаковой ширины и глубины лепили борова
            // ВОЛК ЛАПОЙ НЕ БЬЁТ, И ЭТО НЕ НЕДОДЕЛКА. Псовые — «ротовые» хищники: всё оружие в челюстях,
            // передние лапы тормозят на повороте, копают и ПРИЖИМАЮТ добычу, пока её рвёт пасть. Удар лапой —
            // специализация кошачьих (втяжной коготь, перестроенная конечность) и медведей.
            //     Поэтому волку НЕ вешается `LimbStrikeAbility` (её носитель — лось с копытом, оно у него и есть
            // главное оружие против хищника). Урон здесь работает только у ИГРОКА через `PlayerAttack`: человек
            // с волчьими лапами дерётся ими осознанно, и это законная условность конструктора, а не ошибка данных.
            //     Захват волку даёт ПАСТЬ (`constrictStage = 1`), а не лапы: заводить лапам свой грэпл значило бы
            // дать одному зверю два захватывающих органа.
            new Organ { organName = "Коготь",        slot = "Руки",   hotkey = "1", cost = 4, damage = 18, range = 1.5f, visualScale = new Vector3(1f, 1f, 1.2f), visualParts = new[] {
                new OrganPart { scale = new Vector3(0.850f, 0.221f, 0.900f), offset = new Vector3(0.000f, 0.410f, 0.059f), shape = PartShape.Sphere }, // лопаточная мышца
                new OrganPart { scale = new Vector3(0.977f, 0.124f, 0.833f), offset = new Vector3(0.000f, 0.452f, 0.059f), euler = new Vector3(-21f, 0f, 0f), shape = PartShape.Capsule }, // лопатка→плечевой
                new OrganPart { scale = new Vector3(0.859f, 0.124f, 0.745f), offset = new Vector3(0.000f, 0.342f, 0.018f), euler = new Vector3(23f, 0f, 0f), shape = PartShape.Capsule }, // плечевой→ЛОКОТЬ (0.585 = 0.50 холки)
                new OrganPart { scale = new Vector3(0.719f, 0.483f, 0.598f), offset = new Vector3(0.000f, 0.066f, 0.062f), euler = new Vector3(-5f, 0f, 0f), shape = PartShape.Capsule }, // локоть→запястье: длинная прямая колонна
                new OrganPart { scale = new Vector3(0.547f, 0.221f, 0.441f), offset = new Vector3(0.000f, -0.266f, 0.088f), euler = new Vector3(-4f, 0f, 0f), shape = PartShape.Capsule }, // запястье→путовый
                new OrganPart { scale = new Vector3(0.820f, 0.116f, 0.735f), offset = new Vector3(0.000f, -0.430f, 0.118f) }, // лапа с когтями: низ РОВНО на земле
            } }, // ПЕРЕДНЯЯ КОНЕЧНОСТЬ по референсу: колонна от земли (0.000) до плечевого сустава (0.725 = 0.62 холки)
            new Organ { organName = "Волчьи ноги",   slot = "Ноги",   hotkey = "2", cost = 4, moveSpeed = 9f, dashSpeed = 30f, visualScale = new Vector3(1f, 1f, 1.2f), visualParts = new[] {
                new OrganPart { scale = new Vector3(0.850f, 0.294f, 0.880f), offset = new Vector3(0.000f, 0.408f, -0.060f), shape = PartShape.Sphere }, // мышца бедра
                new OrganPart { scale = new Vector3(0.950f, 0.320f, 0.762f), offset = new Vector3(0.000f, 0.381f, -0.060f), euler = new Vector3(-13f, 0f, 0f), shape = PartShape.Capsule }, // таз→колено (0.560)
                new OrganPart { scale = new Vector3(0.700f, 0.374f, 0.578f), offset = new Vector3(0.000f, 0.074f, -0.141f), euler = new Vector3(23f, 0f, 0f), shape = PartShape.Capsule }, // колено→СКАКАТЕЛЬНЫЙ (0.300 = 0.26 холки)
                new OrganPart { scale = new Vector3(0.500f, 0.307f, 0.415f), offset = new Vector3(0.000f, -0.246f, -0.251f), euler = new Vector3(-6f, 0f, 0f), shape = PartShape.Capsule }, // пятка→плюсна: почти вертикаль
                new OrganPart { scale = new Vector3(0.379f, 0.100f, 0.243f), offset = new Vector3(0.000f, -0.099f, -0.352f) }, // пяточный отросток
                new OrganPart { scale = new Vector3(0.800f, 0.120f, 0.762f), offset = new Vector3(0.000f, -0.440f, -0.191f) }, // лапа: низ РОВНО на земле
            } }, // ЗАДНЯЯ: та же колонна до тазобедренного (0.749 = 0.64 холки), излом на скакательном
            new Organ { organName = "Волчье сердце", slot = "Сердце", hotkey = "3", cost = 6, atkCooldown = 0.30f, hpBonus = 1.75f, staminaBonus = 0.5f, staminaRegenBonus = 0.25f, regen = 3f, regenOOC = 0f, visualParts = new[] {
                new OrganPart { scale = new Vector3(1.000f, 0.750f, 1.000f), offset = new Vector3(0.000f, 0.116f, 0.000f), shape = PartShape.Sphere }, // грудная клетка ВОЛКА, ВЕРХ: рёберный свод 0.27 шириной при 0.42 глубины — СЕЧЕНИЕ ОВАЛ 1:2, анфас волк почти плоский
                new OrganPart { scale = new Vector3(0.760f, 0.895f, 0.983f), offset = new Vector3(0.000f, -0.613f, 0.702f), shape = PartShape.Sphere }, // ...и КИЛЬ к подреберью: низ груди 0.575 — НИЖЕ ЛОКТЯ (0.585), у бегуна рёбра сходятся книзу за локтем
            } }, // «заживает как на собаке»: реген 2→3, чтобы босс вернул свои 6/с (Blend на Э=2), а волки затягивали раны на глазах. +175%: лёгкое тело, огромный мотор → волк-NPC 68 HP, вервольф ровно 300. Постоянный реген ВМЕСТО тихого в покое (вне-боя — фича человеческого сердца)
            new Organ { organName = "Нюх",           slot = "Чутьё",  hotkey = "4", cost = 3, dashCooldown = 0.45f, enablesScent = true, visualParts = new[] {
                new OrganPart { scale = new Vector3(1.00f, 1.00f, 1.00f), shape = PartShape.Sphere, role = PartRole.Nose }, // нос
                new OrganPart { scale = new Vector3(1.00f, 0.42f, 0.90f), offset = new Vector3(0.00f, -0.30f, 0.00f), euler = new Vector3(-6f, 0f, -7f), role = PartRole.Ear, shape = PartShape.Sphere }, // основание уха — широкое
                new OrganPart { scale = new Vector3(0.70f, 0.40f, 0.62f), offset = new Vector3(0.02f, 0.02f, -0.02f), euler = new Vector3(-6f, 0f, -7f), role = PartRole.Ear, shape = PartShape.Sphere }, // середина — сужается
                new OrganPart { scale = new Vector3(0.50f, 0.36f, 0.40f), offset = new Vector3(0.04f, 0.32f, -0.04f), euler = new Vector3(-6f, 0f, -7f), role = PartRole.Ear, shape = PartShape.Sphere }, // ВЕРХУШКА ЗАКРУГЛЁННАЯ (0.36→0.50): у волка ухо треугольник с тупым концом, а не остриё
                new OrganPart { scale = new Vector3(1.00f, 1.00f, 1.00f), shape = PartShape.Sphere, role = PartRole.Eye, color = new Color(0.45f, 0.30f, 0.12f, 1f) }, // ЦВЕТ ГЛАЗА = КАНАЛ: нюх
            } },
            new Organ { organName = "Пасть",         slot = "Пасть",  hotkey = "5", cost = 5, enablesBite = true, enablesHowl = true, bleedStacks = 2, howlRadius = 14f, howlStunAt = 2f, enablesConstrict = true, constrictStage = 1, nativeChassis = "Волк", visualParts = new[] {
                new OrganPart { scale = new Vector3(0.154f, 0.689f, 0.077f), offset = new Vector3(0.262f, -0.111f, 0.318f), color = new Color(0.95f, 0.94f, 0.90f, 1f) }, // КЛЫК верхний (пр): 0.13L, тип уходит НИЖЕ линии губы и НАРУЖУ от тела челюсти — иначе зуб тонет в морде
                new OrganPart { scale = new Vector3(0.154f, 0.689f, 0.077f), offset = new Vector3(-0.262f, -0.111f, 0.318f), color = new Color(0.95f, 0.94f, 0.90f, 1f) }, // клык верхний (лев)
                new OrganPart { scale = new Vector3(0.131f, 0.578f, 0.068f), offset = new Vector3(0.231f, -0.133f, 0.445f), color = new Color(0.95f, 0.94f, 0.90f, 1f) }, // клык нижний (пр): 0.11L, стоит ПЕРЕД верхним (снаружи), как в референсе черепа
                new OrganPart { scale = new Vector3(0.131f, 0.578f, 0.068f), offset = new Vector3(-0.231f, -0.133f, 0.445f), color = new Color(0.95f, 0.94f, 0.90f, 1f) }, // клык нижний (лев)
                new OrganPart { scale = new Vector3(0.108f, 0.378f, 0.118f), offset = new Vector3(0.385f, -0.133f, -0.114f), color = new Color(0.92f, 0.90f, 0.84f, 1f) }, // щёчный ХИЩНИЧЕСКИЙ (пр) — крупнейший в ряду, на ⅔ ряда
                new OrganPart { scale = new Vector3(0.108f, 0.378f, 0.118f), offset = new Vector3(-0.385f, -0.133f, -0.114f), color = new Color(0.92f, 0.90f, 0.84f, 1f) }, // щёчный хищнический (лев)
                new OrganPart { scale = new Vector3(0.092f, 0.311f, 0.100f), offset = new Vector3(0.377f, -0.111f, -0.364f), color = new Color(0.92f, 0.90f, 0.84f, 1f) }, // щёчный задний (пр)
                new OrganPart { scale = new Vector3(0.092f, 0.311f, 0.100f), offset = new Vector3(-0.377f, -0.111f, -0.364f), color = new Color(0.92f, 0.90f, 0.84f, 1f) }, // щёчный задний (лев)
            } }, // укус + кровь + ГОЛОС + ХВАТ пастью; МОРДА: на человечьем шасси садится на его «лицо» → морда вервольфа.
                 // РЕЗЦЫ НЕ РИСУЕМ: при сомкнутой пасти они целиком внутри морды — деталь, которой не видно ни с одного ракурса
            new Organ { organName = "Шкура",         slot = "Шкура",  hotkey = "6", cost = 4, damageReduction = 0.3f },
        };
        // СОКЕТ-ПЛАН волка (4-ногий): ТЕ ЖЕ имена, что у человека (имя = Organ.slot), позиции из BuildBlocky.
        // mirrorX даёт ЧЕТЫРЕ лапы и ДВА уха одной записью (раньше пара выглядела единым блоком)
        wolf.sockets = new[]
        {
            new BodySocket { name = "хребет", localPos = new Vector3(0.000f, 0.937f, 0.000f), baseSize = new Vector3(0.335f, 0.485f, 1.295f) }, // НЕСУЩИЙ ЦЕНТР: форму даёт орган «Хребет» (chassisOnly), поэтому место больше не служебное
            new BodySocket { name = "нос", parent = "голова", attach = 0.500f, attachOffset = new Vector3(0.000f, -0.136f, 0.512f), sizeRel = new Vector3(0.179f, 0.164f, 0.100f), formFrom = "Чутьё", formRole = PartRole.Nose }, // МОЧКА на конце спинки носа, выступает на 3 см и свешена вниз
            // ГОЛОВА = ГАБАРИТ ЧЕРЕПА ПО РЕФЕРЕНСУ: L × 0.56L × 0.47L, то есть 0.469 × 0.263 × 0.220. Калибр ШИРЕ ВЫСОТЫ —
            // прежние 0.190×0.260 давали голову УЖЕ, чем выше: узкий шарик вместо волчьей головы. Все доли частей ниже
            // читаются от L напрямую (0.38L коробка, 0.28L морда у основания, 0.20L у мочки)
            // ГОЛОВА СИДИТ НА КОСТИ ШЕИ (скелет забрал место «шея», но само место осталось — по нему
            // по-прежнему считается КАЛИБР головы). Смещение здесь В МЕТРАХ, а не в калибрах родителя:
            // у кости нет габаритной коробки, делить не на что. baseEuler гасит наклон шеи (−7.5°),
            // чтобы морда держалась горизонтально, — то же правило, что и было, просто число новое
            new BodySocket { name = "голова", parent = "шея", attach = 1.000f, attachOffset = new Vector3(0.000f, -0.124f, 0.111f),    baseSize = new Vector3(0.263f, 0.220f, 0.469f), sizeRel = new Vector3(1.195f, 0.733f, 1.234f), baseEuler = new Vector3(0.8f, 0f, 0f), parts = new[] {
                new OrganPart { scale = new Vector3(0.677f, 0.682f, 0.450f), offset = new Vector3(0.000f, 0.091f, -0.275f), shape = PartShape.Sphere }, // МОЗГОВАЯ КОРОБКА — задние 45% длины, 0.38L шириной, сверху округлая
                new OrganPart { scale = new Vector3(0.221f, 0.355f, 0.224f), offset = new Vector3(0.000f, 0.327f, -0.405f), shape = PartShape.Sphere }, // затылочный гребень — киль сверху сзади, выступает
                new OrganPart { scale = new Vector3(0.290f, 0.300f, 0.320f), offset = new Vector3(0.355f, -0.082f, -0.032f), shape = PartShape.Sphere }, // СКУЛОВАЯ ДУГА (пр): ОТДЕЛЬНОЕ КРЫЛО вбок — она и даёт волчью голову анфас (макс. ширина 0.56L). ДУГА ВИСЕЛА В ВОЗДУХЕ, не касаясь НИ ОДНОЙ детали головы (9.6 мм пустоты): разрыв шёл по Y, а не по X, поэтому мало было расширить её внутрь (0.209→0.290 при сдвинутом адресе, наружный край держит те же 0.1315) — пришлось добрать и высоту 0.227→0.300, чтобы дуга дошла снизу до тела челюсти, а сверху до надбровья
                new OrganPart { scale = new Vector3(0.290f, 0.300f, 0.320f), offset = new Vector3(-0.355f, -0.082f, -0.032f), shape = PartShape.Sphere }, // скуловая дуга (лев)
                new OrganPart { scale = new Vector3(0.215f, 0.215f, 0.190f), offset = new Vector3(0.232f, 0.140f, -0.072f), shape = PartShape.Sphere }, // надбровье (пр): закрывает височную яму между коробкой и дугой — И ДЕРЖИТ ГЛАЗ (без него глаз висит в воздухе)
                new OrganPart { scale = new Vector3(0.215f, 0.215f, 0.190f), offset = new Vector3(-0.232f, 0.140f, -0.072f), shape = PartShape.Sphere }, // надбровье (лев)
                new OrganPart { scale = new Vector3(0.498f, 0.477f, 0.373f), offset = new Vector3(0.000f, 0.036f, 0.085f), shape = PartShape.Sphere }, // СПИНКА НОСА, основание — 0.28L шириной, прямая
                new OrganPart { scale = new Vector3(0.357f, 0.391f, 0.288f), offset = new Vector3(0.000f, -0.009f, 0.362f), shape = PartShape.Sphere },
                new OrganPart { scale = new Vector3(0.430f, 0.430f, 0.300f), offset = new Vector3(0.000f, 0.012f, 0.240f), shape = PartShape.Sphere }, // ПЕРЕХОД спинки носа: между основанием и передом зиял разрыв — шов читался уступом
                new OrganPart { scale = new Vector3(0.640f, 0.640f, 0.330f), offset = new Vector3(0.000f, 0.060f, -0.440f), shape = PartShape.Sphere }, // ЗАТЫЛОЧНЫЙ ПЕРЕХОД: череп в шею, иначе голова садится на шею уступом // ...и перед: сужение до 0.20L идёт В ПЛАНЕ, высота почти не падает
                new OrganPart { scale = new Vector3(0.426f, 0.727f, 0.203f), offset = new Vector3(0.000f, -0.136f, -0.235f), shape = PartShape.Sphere }, // ЧЕЛЮСТЬ: восходящая ветвь сзади, 0.34L высотой, почти вертикальная
                new OrganPart { scale = new Vector3(0.365f, 0.282f, 0.640f), offset = new Vector3(0.000f, -0.336f, 0.117f), shape = PartShape.Sphere }, // ...и тело челюсти: длинное, прямое и УЖЕ морды — в зазор выходят клыки
            } }, // 45% коробка / 55% морда; стоп ПОЛОГИЙ. baseEuler −4 гасит наклон шеи: морда держится горизонтально

            new BodySocket { name = "Пасть", parent = "голова", attach = 1.000f, attachOffset = new Vector3(0.000f, -0.400f, 0.119f), baseSize = new Vector3(0.130f, 0.090f, 0.220f), sizeRel = new Vector3(0.494f, 0.409f, 0.469f) }, // АДРЕС ПАСТИ: зубы рисует ОРГАН, место лишь держит калибр. ОПУЩЕНО НА 3 СМ: в морде из костей клыки утонули (кончик на 8 мм внутри поверхности) — зубы должны выходить из-под губы, иначе пасть без зубов
            new BodySocket { name = "глаза", parent = "голова", attach = 0.500f, attachOffset = new Vector3(0.456f, 0.218f, 0.019f), baseSize = new Vector3(0.032f, 0.032f, 0.032f), sizeRel = new Vector3(0.122f, 0.145f, 0.068f), mirrorX = true, formFrom = "Чутьё", formRole = PartRole.Eye, parts = new[] { // ГЛАЗ ВЫНЕСЕН НА ПОВЕРХНОСТЬ ПОЛЯ. Череп из костей полнее прежнего черепа из коробок, и на старом адресе глаз оказался УТОПЛЕН на 3.7 см — снаружи это читалось как «глаза закрыты кожей». Вынос считался по нормали поля до нуля плюс запас: теперь шар выступает наполовину
                                                                                                                                                                                                                                                                                                     // ЩЕЛЬ ГЛАЗА ЗДЕСЬ НЕ ЛЕЧИТСЯ ПОДГОНКОЙ (откат 08.09). Была попытка поставить z = −1.50 «подбором» под порог детектора: это −1.5 калибра головы, то есть сдвиг ~0.70 м у зверя с головой 0.30 м — глаз уезжал в грудную клетку, а «зазор 0.018» мерился уже до шеи, а не до черепа. Детектор так обыгрывают, а не удовлетворяют.
                                                                                                                                                                                                                                                                                                     //     Дефект под этим настоящий и записан в паспорте волка п.0: причина — расхождение КОСТИ и МЕСТА (голова волка рисуется костями черепа, а глаз садится на сокет). Чинить систему координат, а не множитель; снимается вместе с переходом волка на клетку
                new OrganPart { scale = new Vector3(1.00f, 1.00f, 1.00f), shape = PartShape.Sphere, role = PartRole.Eye, color = new Color(0.10f, 0.10f, 0.12f, 1f) }, // ФОЛБЭК: тварь без Чутья не слепа, но глаз тускл
            } }, // МЕСТО НА КОЖЕ головы — форму и цвет даёт ЧУТЬЁ
            new BodySocket { name = "уши", parent = "голова", attach = 0.500f, attachOffset = new Vector3(0.285f, 0.545f, -0.362f),    baseSize = new Vector3(0.075f, 0.165f, 0.035f), sizeRel = new Vector3(0.285f, 0.750f, 0.075f), baseEuler = new Vector3(-6f, 45f, -15f), mirrorX = true, formFrom = "Чутьё", formRole = PartRole.Ear },   // УХО НА ЗАДНЕЙ ТРЕТИ КОРОБКИ, основание 0.19L (широкое), верхушка чуть НАРУЖУ (−15°): прежние +25° сводили уши домиком

            new BodySocket { name = "шея", parent = "хребет", attach = 1.000f, attachOffset = new Vector3(0.000f, 0.202f, -0.025f),       baseSize = new Vector3(0.220f, 0.300f, 0.380f), sizeRel = new Vector3(0.657f, 0.619f, 0.293f), baseEuler = new Vector3(4f, 0f, 0f), parts = new[] {
                new OrganPart { scale = new Vector3(1.000f, 1.133f, 0.711f), offset = new Vector3(0.000f, 0.000f, 0.000f), euler = new Vector3(90f, 0f, 0f), shape = PartShape.Capsule }, // ШЕЯ ОДНОЙ МАССОЙ: длина 0.34, глубина 0.27, ширина 0.22 — мощная и короткая
                new OrganPart { scale = new Vector3(1.068f, 0.467f, 0.789f), offset = new Vector3(0.000f, 0.173f, -0.313f), shape = PartShape.Sphere }, // загривок — сходится с холкой, стыка не видно
            } }, // ОСЬ ШЕИ ВПЕРЁД-ВНИЗ (+4°, было −22° вверх): волк несёт голову на уровне линии спины, а не задирает её
            new BodySocket { name = "Шкура", parent = "хребет", attach = 0.500f,    baseSize = new Vector3(0.335f, 0.485f, 1.296f), sizeRel = new Vector3(1.000f, 1.000f, 1.001f), parts = new[] {
                new OrganPart { scale = new Vector3(0.762f, 0.788f, 0.921f), offset = new Vector3(0.000f, -0.014f, -0.020f), shape = PartShape.Sphere }, // КОРПУС: 0.285 ширины при 0.40 глубины — овал, не бочка; верх 1.130 (между холкой 1.170 и крупом 1.100), спина ровная
                // грудная клетка УШЛА В ОРГАН «Волчье сердце» — её лепит сердце, чужое перестраивает силуэт
            } },
            // НОГА = КОЛОННА ОТ ЗЕМЛИ. Габарит места 0.725/0.749 = 0.62/0.64 ХОЛКИ (референс дан в долях холки, а не в метрах:
            // прежние 0.620 м = 0.53 холки — из-за этого зверь висел лапами в 23 см над землёй, и это не видно ни по одной ошибке)
            new BodySocket { name = "Руки", parent = "хребет", attach = 0.780f, attachOffset = new Vector3(0.299f, -1.185f, 0.061f),   baseSize = new Vector3(0.128f, 0.725f, 0.170f), sizeRel = new Vector3(0.382f, 1.495f, 0.131f), mirrorX = true }, // ПЕРЕДНИЕ: низ колонны РОВНО на земле, локоть 0.585 = 0.50 холки
            new BodySocket { name = "Ноги", parent = "хребет", attach = 0.220f, attachOffset = new Vector3(0.319f, -1.160f, -0.061f),   baseSize = new Vector3(0.140f, 0.749f, 0.199f), sizeRel = new Vector3(0.418f, 1.544f, 0.154f), mirrorX = true }, // ЗАДНИЕ: чуть длиннее (толчковые); вынос вбок 0.107 — лапы ПОЧТИ ПОД КОРПУСОМ, не враскоряку
            // ТО ЖЕ САМОЕ, ЗЕРКАЛЬНО: крестец развернулся, его прежнее начало стало концом. Хвост есть
            // ПРОДОЛЖЕНИЕ позвоночника и обязан расти из дальнего от головы конца — иначе уезжает в круп
            new BodySocket { name = "Хвост", parent = "крестец", attach = 1.000f, linkDiameter = 0.130f, linkLength = 0.088f, linkTaper = 0.900f, chain = 6, baseEuler = new Vector3(-33.2f, 0f, 0f) }, // ХВОСТ — ЦЕПЬ ЗВЕНЬЕВ, как у змеи: капсула + шар-сустав на каждом, сужение одним числом.
                                                                                                                                                                                                     //     СМЕЩЕНИЯ НЕТ ВОВСЕ, и это принципиально: хвост — ПРОДОЛЖЕНИЕ позвоночника, он начинается ровно там, где кончается крестец (attach 0). Любое смещение здесь превращается в щель, потому что цепь уходит от точки ПРОЧЬ от тела: прежние +0.05 по вертикали выносили первое звено выше мяса крупа, и хвост висел сам по себе. Теперь звено утоплено в круп на 0.101; −33.2 даёт прежний свес −38 с учётом наклона крестца
            //     Был стопкой КУБОВ с диском-шарниром — читался доской и ломал стиль (всё тело из сфер и капсул).
            //     Основание УТОПЛЕНО глубже в круп (offset.y −0.300 против −0.274): шарнир должен входить в тело, а не торчать

            new BodySocket { name = "Сердце", inner = true, parent = "хребет", attach = 0.500f, attachOffset = new Vector3(0.000f, -0.169f, 0.093f), baseSize = new Vector3(0.270f, 0.560f, 0.560f), sizeRel = new Vector3(0.806f, 1.155f, 0.432f) }, // ГРУДНАЯ КОРОБКА волка: 0.27 ширины на 0.56 глубины — ОВАЛ 1:2, глубокий и узкий. Сдвинута ВПЕРЁД и ВНИЗ: киль приходится на локоть
            new BodySocket { name = "Чутьё",  inner = true, parent = "голова", attach = 0.500f, sizeRel = new Vector3(0.500f, 0.500f, 0.500f) },  // ЧУВСТВА ЖИВУТ В ГОЛОВЕ. Своей формы у места нет, и деталь не родится сама собой — но АДРЕС нужен заранее: дашь органу форму (термо-ямки), и без родителя с калибром она сядет метровым кубом в начало координат. Доля от головы — одна на все виды
            // закрытые места (пустыми не рисуются): волк с лосиными рогами / ежиным игломётом читается сразу
            new BodySocket { name = "Рога", parent = "голова", attach = 0.600f, attachOffset = new Vector3(0.285f, 0.591f, -0.228f),    baseSize = new Vector3(0.101f, 0.127f, 0.142f), sizeRel = new Vector3(0.384f, 0.577f, 0.303f), mirrorX = true, graft = true }, // КАЛИБР: над мозговой коробкой и наружу — лопасть не врастает в череп
            new BodySocket { name = "Игломёт", parent = "грудной", attach = 0.500f, attachOffset = new Vector3(0.000f, 0.191f, 0.001f),    baseSize = new Vector3(0.198f, 0.198f, 0.198f), sizeRel = new Vector3(0.591f, 0.408f, 0.153f), baseEuler = new Vector3(-8.1f, 0.00f, 0.00f), graft = true }, // КАЛИБР; сидит на середине грудного отдела и выступает над спиной на 0.16
        };
        // СКЕЛЕТ (спека 2026-08-18): НЕСУЩАЯ ОСЬ И ЧЕТЫРЕ НОГИ. Череп пока остаётся местом и переезжает
        // следующим шагом — он же и есть проверка обобщаемости метода.
        //     ЧИСЛА НЕ ПОДБИРАЛИСЬ РУКАМИ. Задавались АНАТОМИЧЕСКИЕ ТОЧКИ (сустав → сустав) по референсу
        // при холке 1.170, а длины и углы посчитаны из них: плечевой 0.725, локоть 0.585, тазобедренный
        // 0.749, колено 0.560, скакательный 0.300, низ груди 0.575, лапы на земле. Поэтому в данных нет
        // ни одного «подогнанного» числа — есть только следствия точек.
        //     МЯСО НАРАСТАЕТ ВОКРУГ КОСТИ СИММЕТРИЧНО, и это правило имеет цену: корпус нельзя получить,
        // раздув позвоночник, — спина уедет вверх ровно настолько же, насколько живот вниз. Объём зверя
        // висит ПОД осью, поэтому его держат отдельные кости: грудь — рёбра, живот — поясничные отростки,
        // преднагрудье — грудина, переход к горлу — подгрудок. Без двух последних профиль тела прыгал с
        // 0.60 на 0.99 за десять сантиметров — это и есть глазами «грудь отваливается отдельным телом».
        wolf.bones = new[]
        {
            // СКЕЛЕТ ВИДА «wolf» — ВЫГРУЖЕНО из Tools/Blender/species/wolf.py (tools/bones_to_cs.py).
            // Правится ТАМ, по анатомическим точкам; здесь только результат. Костей: 130, калибр (холка) 1.170 м
            new Bone { name = "грудина", socket = "Сердце", origin = new Vector3(0f, 0.674f, -0.238f), length = 0.396f, dir = new Vector3(79.82f, 0f, 0f), r0 = 0.018f, r1 = 0.02f, section = 0.85f, depth = 1.1f, chain = 3 },   // грудина держит НИЗ груди. Без неё дуги висят концами в воздухе
            new Bone { name = "лёгкие", socket = "Сердце", origin = new Vector3(0f, 0.869f, -0.324f), length = 0.43f, dir = new Vector3(88.07f, 0f, 0f), r0 = 0.115f, r1 = 0.103f, depth = 1.48f, layer = BodyLayer.Muscle },   // содержимое грудной полости: лёгкие и сердце. Поперечник изнутри рёбер (дуга 0.115 холки), высота от позвонка до грудины
            new Bone { name = "брюшина", socket = "хребет", origin = new Vector3(0f, 0.869f, -0.324f), length = 0.517f, dir = new Vector3(-83.45f, 0f, 0f), r0 = 0.103f, r1 = 0.082f, depth = 1.3f, layer = BodyLayer.Muscle },   // содержимое брюшной полости. Уже грудной и сходит на конус к тазу — отсюда подобранная талия, которую иначе рисовать нечем
            new Bone { name = "череп", socket = "голова", origin = new Vector3(0f, 1.358f, 0.485f), length = 0.386f, dir = new Vector3(125.39f, 0f, 0f), r0 = 0.03f },   // мозговой череп ОБОЛОЧКОЙ по 49 сечениям, снятым с пластины
            new Bone { name = "скула", socket = "голова", origin = new Vector3(0.058f, 1.257f, 0.545f), length = 0.083f, dir = new Vector3(129.09f, 0f, -28.91f), r0 = 0.013f, r1 = 0.012f, section = 0.75f, depth = 1.35f, mirrorX = true },   // задняя ветвь скуловой дуги: от височной кости наружу-вперёд
            new Bone { name = "глазница.р", socket = "голова", origin = new Vector3(0.125f, 1.228f, 0.68f), length = 0.099f, dir = new Vector3(-105f, 0f, 77.46f), r0 = 0.035f, r1 = 0.009f, depth = 1.1f, mirrorX = true },   // глазница: конус, сужающийся внутрь черепа. Смотрит вперёд-вбок, как у хищника
            new Bone { name = "ухо.п", socket = "Чутьё", origin = new Vector3(0.05f, 1.312f, 0.609f), length = 0.16f, dir = new Vector3(-9.54f, 0f, -5.01f), r0 = 0.054f, r1 = 0.014f, depth = 0.52f, mirrorX = true, layer = BodyLayer.Feature },   // ухо: широкое у основания, к кончику сходит на нет, поперёк ПЛОСКОЕ. Стоячее ухо — половина того, чем волк опознаётся издали
            new Bone { name = "затылочный", socket = "голова", parent = "череп", attach = 0f, length = 0.086f, dir = new Vector3(64.57f, 0f, 0f), r0 = 0.028f, r1 = 0.023f },   // затылочная кость: несёт свод от сустава с атлантом. Внутри оболочки, снаружи не видна
            new Bone { name = "скула_п", socket = "голова", parent = "скула", attach = 1f, length = 0.104f, dir = new Vector3(-34.3f, 0f, 45.5f), r0 = 0.012f, r1 = 0.011f, section = 0.75f, depth = 1.25f, mirrorX = true },   // передняя ветвь дуги: сходится к верхнечелюстной под глазницей
            new Bone { name = "нч_подвес", socket = "Пасть", parent = "череп", attach = 0.241f, length = 0.082f, dir = new Vector3(84.61f, 0f, -39.57f), r0 = 0.009f, mirrorX = true },   // вынос сустава от оси к нижнечелюстной ямке. Кость-связка: начало ребёнка всегда лежит на родителе, поэтому боковую посадку приходится проходить отдельным звеном
            new Bone { name = "клык_в", socket = "Пасть", parent = "череп", attach = 0.905f, length = 0.09f, dir = new Vector3(46.48f, 0f, -22.99f), r0 = 0.013f, r1 = 0.002f, section = 0.85f, depth = 0.85f, mirrorX = true, layer = BodyLayer.Feature },   // верхний клык FEATURE: вниз и чуть вперёд (на черепе)
            new Bone { name = "шея_в", socket = "шея", parent = "затылочный", attach = 1f, length = 0.114f, dir = new Vector3(30.43f, 0f, 0f), r0 = 0.025f, r1 = 0.029f, section = 1.12f, chain = 2 },   // атлант и эпистрофей: несут голову, самое подвижное звено
            new Bone { name = "ветвь", socket = "Пасть", parent = "нч_подвес", attach = 0.99f, length = 0.045f, dir = new Vector3(35.47f, 0f, 35.95f), r0 = 0.035f, r1 = 0.026f, section = 0.34f, depth = 1.1f, mirrorX = true },   // ветвь челюсти: от сустава вниз-назад к угловому отростку
            new Bone { name = "венечный", socket = "Пасть", parent = "нч_подвес", attach = 0.99f, length = 0.071f, dir = new Vector3(-148.92f, 0f, -28.95f), r0 = 0.03f, r1 = 0.009f, section = 0.22f, depth = 1.25f, mirrorX = true },   // венечный отросток: пластина ВНУТРИ скуловой дуги. К ней крепится височная мышца — она и даёт волку силу укуса
            new Bone { name = "шея_3", socket = "шея", parent = "шея_в", attach = 1f, length = 0.126f, dir = new Vector3(9.45f, 0f, 0f), r0 = 0.029f, r1 = 0.032f, section = 1.18f, chain = 2 },   // C3–C2: здесь шея начинает задираться к голове
            new Bone { name = "челюсть", socket = "Пасть", parent = "ветвь", attach = 1f, length = 0.293f, dir = new Vector3(-120.99f, 0f, 25.71f), r0 = 0.025f, r1 = 0.016f, section = 0.5f, depth = 1.15f, mirrorX = true },   // тело челюсти с зубным рядом: узкое вбок, высокое в профиль
            new Bone { name = "грудиночелюстная.м", socket = "шея", parent = "грудина", attach = 0.92f, length = 0.634f, dir = new Vector3(-41.94f, 0f, -4.58f), r0 = 0.03f, section = 0.65f, mirrorX = true, layer = BodyLayer.Muscle },   // грудиночелюстная: линия горла. Она отделяет шею от груди
            new Bone { name = "жевательная.м", socket = "Пасть", parent = "скула", attach = 0.55f, length = 0.073f, dir = new Vector3(148.31f, 0f, -0.7f), r0 = 0.033f, section = 0.8f, mirrorX = true, layer = BodyLayer.Muscle },   // жевательная: щека. Заполняет угол между дугой и челюстью
            new Bone { name = "височная.м", socket = "голова", parent = "череп", attach = 0.3f, length = 0.046f, dir = new Vector3(47.29f, 0f, -80.07f), r0 = 0.04f, section = 0.7f, depth = 1.1f, mirrorX = true, layer = BodyLayer.Muscle },   // височная: заполняет височную яму под дугой. Ею голова хищника шире в скулах, чем в своде
            new Bone { name = "шея_2", socket = "шея", parent = "шея_3", attach = 1f, length = 0.124f, dir = new Vector3(17.93f, 0f, 0f), r0 = 0.032f, r1 = 0.036f, section = 1.22f, chain = 2 },   // C5–C4: самое глубокое место шеи
            new Bone { name = "клык_н", socket = "Пасть", parent = "челюсть", attach = 0.92f, length = 0.056f, dir = new Vector3(-120.66f, 0f, -38.53f), r0 = 0.012f, r1 = 0.002f, section = 0.85f, depth = 0.85f, mirrorX = true, layer = BodyLayer.Feature },   // нижний клык FEATURE: ВВЕРХ, встаёт перед верхним (на челюсти, открывается)
            new Bone { name = "шея", socket = "шея", parent = "шея_2", attach = 1f, length = 0.13f, dir = new Vector3(6.16f, 0f, 0f), r0 = 0.036f, r1 = 0.04f, section = 1.25f, chain = 2 },   // C7–C6: выходит из холки ПОЛОГО, почти горизонтально
            new Bone { name = "холка", socket = "хребет", parent = "шея", attach = 1f, length = 0.176f, dir = new Vector3(22.27f, 0f, 0f), r0 = 0.023f, r1 = 0.024f, section = 1.3f, chain = 3 },   // передний грудной отдел. Отдельной костью потому, что ХОЛКА — это его остистые отростки, и править её высоту надо, не трогая длину всей грудной клетки
            new Bone { name = "грудной", socket = "хребет", parent = "холка", attach = 1f, length = 0.296f, dir = new Vector3(-1.94f, 0f, 0f), r0 = 0.024f, r1 = 0.025f, section = 1.35f, chain = 4 },   // задний грудной отдел: несёт восемь каудальных рёбер
            new Bone { name = "остистый1", socket = "хребет", parent = "холка", attach = 0.054f, length = 0.131f, dir = new Vector3(65.8f, 0f, 0f), r0 = 0.019f, r1 = 0.012f, section = 0.42f, depth = 0.95f },
            new Bone { name = "остистый2", socket = "хребет", parent = "холка", attach = 0.376f, length = 0.157f, dir = new Vector3(57.8f, 0f, 0f), r0 = 0.019f, r1 = 0.012f, section = 0.42f, depth = 0.95f },
            new Bone { name = "остистый3", socket = "хребет", parent = "холка", attach = 0.752f, length = 0.154f, dir = new Vector3(55.8f, 0f, 0f), r0 = 0.019f, r1 = 0.012f, section = 0.42f, depth = 0.95f },
            new Bone { name = "ребро1", socket = "Сердце", parent = "холка", attach = 0.054f, length = 0.094f, dir = new Vector3(-104.26f, 0f, -25.11f), r0 = 0.012f, r1 = 0.01f, section = 1.6f, depth = 0.55f, mirrorX = true },   // головка и шейка ребра: вбок круче всего
            new Bone { name = "ребро2", socket = "Сердце", parent = "холка", attach = 0.23f, length = 0.104f, dir = new Vector3(-103.04f, 0f, -27.83f), r0 = 0.012f, r1 = 0.01f, section = 1.6f, depth = 0.55f, mirrorX = true },   // головка и шейка ребра: вбок круче всего
            new Bone { name = "ребро3", socket = "Сердце", parent = "холка", attach = 0.41f, length = 0.113f, dir = new Vector3(-102.02f, 0f, -29.96f), r0 = 0.012f, r1 = 0.01f, section = 1.6f, depth = 0.55f, mirrorX = true },   // головка и шейка ребра: вбок круче всего
            new Bone { name = "ребро4", socket = "Сердце", parent = "холка", attach = 0.615f, length = 0.12f, dir = new Vector3(-101.32f, 0f, -31.52f), r0 = 0.012f, r1 = 0.01f, section = 1.6f, depth = 0.55f, mirrorX = true },   // головка и шейка ребра: вбок круче всего
            new Bone { name = "ребро5", socket = "Сердце", parent = "холка", attach = 0.83f, length = 0.126f, dir = new Vector3(-100.75f, 0f, -32.61f), r0 = 0.012f, r1 = 0.01f, section = 1.6f, depth = 0.55f, mirrorX = true },   // головка и шейка ребра: вбок круче всего
            new Bone { name = "пластыревидная.м", socket = "шея", parent = "холка", attach = 0.25f, length = 0.535f, dir = new Vector3(142.78f, 0f, 0f), r0 = 0.034f, section = 0.75f, mirrorX = true, layer = BodyLayer.Muscle },   // пластыревидная: несёт голову, наполняет загривок за черепом
            new Bone { name = "поясница", socket = "хребет", parent = "грудной", attach = 1f, length = 0.356f, dir = new Vector3(-8.77f, 0f, 0f), r0 = 0.025f, r1 = 0.028f, section = 1.45f, chain = 4 },   // поясничный отдел дугой вверх: от него подобранность талии
            new Bone { name = "остистый4", socket = "хребет", parent = "грудной", attach = 0.108f, length = 0.138f, dir = new Vector3(59.74f, 0f, 0f), r0 = 0.019f, r1 = 0.012f, section = 0.42f, depth = 0.95f },
            new Bone { name = "остистый5", socket = "хребет", parent = "грудной", attach = 0.395f, length = 0.115f, dir = new Vector3(63.74f, 0f, 0f), r0 = 0.019f, r1 = 0.012f, section = 0.42f, depth = 0.95f },
            new Bone { name = "остистый6", socket = "хребет", parent = "грудной", attach = 0.713f, length = 0.091f, dir = new Vector3(69.74f, 0f, 0f), r0 = 0.019f, r1 = 0.012f, section = 0.42f, depth = 0.95f },
            new Bone { name = "остистый7", socket = "хребет", parent = "грудной", attach = 0.936f, length = 0.077f, dir = new Vector3(77.74f, 0f, 0f), r0 = 0.019f, r1 = 0.012f, section = 0.42f, depth = 0.95f },
            new Bone { name = "ребро1с", socket = "Сердце", parent = "ребро1", length = 0.123f, dir = new Vector3(-11.17f, 0f, 9.33f), r0 = 0.01f, r1 = 0.009f, section = 1.6f, depth = 0.55f, mirrorX = true },   // тело ребра: здесь бок шире всего
            new Bone { name = "ребро2с", socket = "Сердце", parent = "ребро2", length = 0.133f, dir = new Vector3(-9.55f, 0f, 10.1f), r0 = 0.01f, r1 = 0.009f, section = 1.6f, depth = 0.55f, mirrorX = true },   // тело ребра: здесь бок шире всего
            new Bone { name = "ребро3с", socket = "Сердце", parent = "ребро3", length = 0.142f, dir = new Vector3(-8.16f, 0f, 10.65f), r0 = 0.01f, r1 = 0.009f, section = 1.6f, depth = 0.55f, mirrorX = true },   // тело ребра: здесь бок шире всего
            new Bone { name = "ребро4с", socket = "Сердце", parent = "ребро4", length = 0.148f, dir = new Vector3(-7.18f, 0f, 11.03f), r0 = 0.01f, r1 = 0.009f, section = 1.6f, depth = 0.55f, mirrorX = true },   // тело ребра: здесь бок шире всего
            new Bone { name = "ребро5с", socket = "Сердце", parent = "ребро5", length = 0.155f, dir = new Vector3(-6.38f, 0f, 11.29f), r0 = 0.01f, r1 = 0.009f, section = 1.6f, depth = 0.55f, mirrorX = true },   // тело ребра: здесь бок шире всего
            new Bone { name = "ребро6", socket = "Сердце", parent = "грудной", attach = 0.038f, length = 0.131f, dir = new Vector3(-98.43f, 0f, -33.05f), r0 = 0.012f, r1 = 0.01f, section = 1.6f, depth = 0.55f, mirrorX = true },   // головка и шейка ребра: вбок круче всего
            new Bone { name = "ребро7", socket = "Сердце", parent = "грудной", attach = 0.177f, length = 0.135f, dir = new Vector3(-98.17f, 0f, -33.15f), r0 = 0.012f, r1 = 0.01f, section = 1.6f, depth = 0.55f, mirrorX = true },   // головка и шейка ребра: вбок круче всего
            new Bone { name = "ребро8", socket = "Сердце", parent = "грудной", attach = 0.316f, length = 0.136f, dir = new Vector3(-98.03f, 0f, -32.95f), r0 = 0.012f, r1 = 0.01f, section = 1.6f, depth = 0.55f, mirrorX = true },   // головка и шейка ребра: вбок круче всего
            new Bone { name = "ребро9", socket = "Сердце", parent = "грудной", attach = 0.455f, length = 0.134f, dir = new Vector3(-97.97f, 0f, -32.6f), r0 = 0.012f, r1 = 0.01f, section = 1.6f, depth = 0.55f, mirrorX = true },   // головка и шейка ребра: вбок круче всего
            new Bone { name = "ребро10", socket = "Сердце", parent = "грудной", attach = 0.594f, length = 0.131f, dir = new Vector3(-97.94f, 0f, -32.13f), r0 = 0.012f, r1 = 0.01f, section = 1.6f, depth = 0.55f, mirrorX = true },   // головка и шейка ребра: вбок круче всего
            new Bone { name = "ребро11", socket = "Сердце", parent = "грудной", attach = 0.733f, length = 0.124f, dir = new Vector3(-98.21f, 0f, -31.55f), r0 = 0.012f, r1 = 0.01f, section = 1.6f, depth = 0.55f, mirrorX = true },   // головка и шейка ребра: вбок круче всего
            new Bone { name = "ребро12", socket = "Сердце", parent = "грудной", attach = 0.873f, length = 0.116f, dir = new Vector3(-98.59f, 0f, -30.82f), r0 = 0.012f, r1 = 0.01f, section = 1.6f, depth = 0.55f, mirrorX = true },   // головка и шейка ребра: вбок круче всего
            new Bone { name = "лопатка", socket = "Руки", parent = "остистый2", attach = 0.418f, length = 0.315f, dir = new Vector3(164.11f, 0f, -13.76f), r0 = 0.049f, r1 = 0.016f, section = 0.3f, depth = 1.3f, mirrorX = true },   // плоская лопасть, лежащая НА рёбрах: даёт покатое плечо и переход холки в ногу
            new Bone { name = "выйная.м", socket = "шея", parent = "остистый1", attach = 0.85f, length = 0.517f, dir = new Vector3(82.73f, 0f, 0f), r0 = 0.029f, section = 0.68f, layer = BodyLayer.Muscle },   // выйная связка с пластыревидной: ГРЕБЕНЬ шеи. Без неё шея проваливается к позвонкам
            new Bone { name = "крестец", socket = "хребет", parent = "поясница", attach = 1f, length = 0.121f, dir = new Vector3(-1.66f, 0f, 0f), r0 = 0.032f, r1 = 0.035f, section = 1.55f },   // крестец: ПОСЛЕДНЕЕ звено позвоночника, на нём сходятся таз и хвост. Корнем графа он был до 08.09 — теперь корень голова (спека 27.08, И1), и цепь идёт от черепа назад
            new Bone { name = "ост_пояс1", socket = "хребет", parent = "поясница", attach = 0.84f, length = 0.068f, dir = new Vector3(108.51f, 0f, 0f), r0 = 0.018f, r1 = 0.011f, section = 0.42f, depth = 0.95f },
            new Bone { name = "ост_пояс2", socket = "хребет", parent = "поясница", attach = 0.54f, length = 0.072f, dir = new Vector3(104.51f, 0f, 0f), r0 = 0.018f, r1 = 0.011f, section = 0.42f, depth = 0.95f },
            new Bone { name = "ост_пояс3", socket = "хребет", parent = "поясница", attach = 0.26f, length = 0.07f, dir = new Vector3(100.51f, 0f, 0f), r0 = 0.018f, r1 = 0.011f, section = 0.42f, depth = 0.95f },
            new Bone { name = "ост_пояс4", socket = "хребет", parent = "поясница", attach = 0.05f, length = 0.066f, dir = new Vector3(96.51f, 0f, 0f), r0 = 0.018f, r1 = 0.011f, section = 0.42f, depth = 0.95f },
            new Bone { name = "попереч1", socket = "хребет", parent = "поясница", attach = 0.8f, length = 0.069f, dir = new Vector3(-107.29f, 0f, -80.99f), r0 = 0.015f, r1 = 0.009f, section = 0.9f, depth = 0.55f, mirrorX = true },
            new Bone { name = "попереч2", socket = "хребет", parent = "поясница", attach = 0.52f, length = 0.073f, dir = new Vector3(-107.29f, 0f, -81.55f), r0 = 0.015f, r1 = 0.009f, section = 0.9f, depth = 0.55f, mirrorX = true },
            new Bone { name = "попереч3", socket = "хребет", parent = "поясница", attach = 0.24f, length = 0.069f, dir = new Vector3(-107.29f, 0f, -80.99f), r0 = 0.015f, r1 = 0.009f, section = 0.9f, depth = 0.55f, mirrorX = true },
            new Bone { name = "ребро1н", socket = "Сердце", parent = "ребро1с", length = 0.106f, dir = new Vector3(-15.54f, 0f, 37.58f), r0 = 0.009f, r1 = 0.008f, section = 1.3f, depth = 0.6f, mirrorX = true },   // рёберный хрящ: заворачивает к грудине
            new Bone { name = "ребро2н", socket = "Сердце", parent = "ребро2с", length = 0.115f, dir = new Vector3(-15.77f, 0f, 44.44f), r0 = 0.009f, r1 = 0.008f, section = 1.3f, depth = 0.6f, mirrorX = true },   // рёберный хрящ: заворачивает к грудине
            new Bone { name = "ребро3н", socket = "Сердце", parent = "ребро3с", length = 0.124f, dir = new Vector3(-15.71f, 0f, 49.98f), r0 = 0.009f, r1 = 0.008f, section = 1.3f, depth = 0.6f, mirrorX = true },   // рёберный хрящ: заворачивает к грудине
            new Bone { name = "ребро4н", socket = "Сердце", parent = "ребро4с", length = 0.132f, dir = new Vector3(-15.6f, 0f, 54.04f), r0 = 0.009f, r1 = 0.008f, section = 1.3f, depth = 0.6f, mirrorX = true },   // рёберный хрящ: заворачивает к грудине
            new Bone { name = "ребро5н", socket = "Сердце", parent = "ребро5с", length = 0.139f, dir = new Vector3(-15.21f, 0f, 56.86f), r0 = 0.009f, r1 = 0.008f, section = 1.3f, depth = 0.6f, mirrorX = true },   // рёберный хрящ: заворачивает к грудине
            new Bone { name = "ребро6с", socket = "Сердце", parent = "ребро6", length = 0.16f, dir = new Vector3(-5.84f, 0f, 11.38f), r0 = 0.01f, r1 = 0.009f, section = 1.6f, depth = 0.55f, mirrorX = true },   // тело ребра: здесь бок шире всего
            new Bone { name = "ребро7с", socket = "Сердце", parent = "ребро7", length = 0.163f, dir = new Vector3(-5.5f, 0f, 11.39f), r0 = 0.01f, r1 = 0.009f, section = 1.6f, depth = 0.55f, mirrorX = true },   // тело ребра: здесь бок шире всего
            new Bone { name = "ребро8с", socket = "Сердце", parent = "ребро8", length = 0.165f, dir = new Vector3(-5.31f, 0f, 11.34f), r0 = 0.01f, r1 = 0.009f, section = 1.6f, depth = 0.55f, mirrorX = true },   // тело ребра: здесь бок шире всего
            new Bone { name = "ребро9с", socket = "Сердце", parent = "ребро9", length = 0.164f, dir = new Vector3(-5.22f, 0f, 11.25f), r0 = 0.01f, r1 = 0.009f, section = 1.6f, depth = 0.55f, mirrorX = true },   // тело ребра: здесь бок шире всего
            new Bone { name = "ребро10с", socket = "Сердце", parent = "ребро10", length = 0.161f, dir = new Vector3(-5.18f, 0f, 11.12f), r0 = 0.01f, r1 = 0.009f, section = 1.6f, depth = 0.55f, mirrorX = true },   // тело ребра: здесь бок шире всего
            new Bone { name = "ребро11с", socket = "Сердце", parent = "ребро11", length = 0.152f, dir = new Vector3(-5.58f, 0f, 10.99f), r0 = 0.01f, r1 = 0.009f, section = 1.6f, depth = 0.55f, mirrorX = true },   // тело ребра: здесь бок шире всего
            new Bone { name = "ребро12с", socket = "Сердце", parent = "ребро12", length = 0.143f, dir = new Vector3(-6.12f, 0f, 10.81f), r0 = 0.01f, r1 = 0.009f, section = 1.6f, depth = 0.55f, mirrorX = true },   // тело ребра: здесь бок шире всего
            new Bone { name = "плечо", socket = "Руки", parent = "лопатка", attach = 1f, length = 0.275f, dir = new Vector3(75.65f, 0f, 4.55f), r0 = 0.026f, r1 = 0.024f, section = 0.85f, mirrorX = true },
            new Bone { name = "зубчатая.м", socket = "Сердце", parent = "ребро4с", attach = 0.45f, length = 0.199f, dir = new Vector3(-162.1f, 0f, 1.58f), r0 = 0.054f, section = 0.6f, depth = 1.1f, mirrorX = true, layer = BodyLayer.Muscle },   // зубчатая вентральная: ПОДВЕС корпуса между лопатками. Ею тело буквально висит на ногах
            new Bone { name = "трапециевидная.м", socket = "хребет", parent = "остистый3", attach = 0.7f, length = 0.203f, dir = new Vector3(155.77f, 0f, -7.42f), r0 = 0.04f, section = 0.55f, mirrorX = true, layer = BodyLayer.Muscle },   // трапециевидная: покатое плечо, переход холки в лопатку
            new Bone { name = "ромбовидная.м", socket = "хребет", parent = "остистый1", attach = 0.55f, length = 0.051f, dir = new Vector3(-103.81f, 0f, -8.55f), r0 = 0.035f, section = 0.55f, mirrorX = true, layer = BodyLayer.Muscle },   // ромбовидная: прижимает верх лопатки к холке, наполняет загривок
            new Bone { name = "межрёберная1.м", socket = "Сердце", parent = "ребро1с", attach = 0.45f, length = 0.036f, dir = new Vector3(91.57f, 0f, -15.04f), r0 = 0.037f, section = 0.4f, mirrorX = true, layer = BodyLayer.Muscle },   // межрёберная: заполняет промежуток между соседними дугами
            new Bone { name = "межрёберная2.м", socket = "Сердце", parent = "ребро2с", attach = 0.45f, length = 0.036f, dir = new Vector3(89.86f, 0f, -13.71f), r0 = 0.037f, section = 0.4f, mirrorX = true, layer = BodyLayer.Muscle },   // межрёберная: заполняет промежуток между соседними дугами
            new Bone { name = "межрёберная3.м", socket = "Сердце", parent = "ребро3с", attach = 0.45f, length = 0.039f, dir = new Vector3(94.49f, 0f, -10.83f), r0 = 0.037f, section = 0.4f, mirrorX = true, layer = BodyLayer.Muscle },   // межрёберная: заполняет промежуток между соседними дугами
            new Bone { name = "межрёберная4.м", socket = "Сердце", parent = "ребро4с", attach = 0.45f, length = 0.04f, dir = new Vector3(94.39f, 0f, -8.93f), r0 = 0.037f, section = 0.4f, mirrorX = true, layer = BodyLayer.Muscle },   // межрёберная: заполняет промежуток между соседними дугами
            new Bone { name = "ребро6н", socket = "Сердце", parent = "ребро6с", length = 0.144f, dir = new Vector3(-14.52f, 0f, 57.99f), r0 = 0.009f, r1 = 0.008f, section = 1.3f, depth = 0.6f, mirrorX = true },   // рёберный хрящ: заворачивает к грудине
            new Bone { name = "ребро7н", socket = "Сердце", parent = "ребро7с", length = 0.146f, dir = new Vector3(-13.7f, 0f, 57.93f), r0 = 0.009f, r1 = 0.008f, section = 1.3f, depth = 0.6f, mirrorX = true },   // рёберный хрящ: заворачивает к грудине
            new Bone { name = "ребро8н", socket = "Сердце", parent = "ребро8с", length = 0.145f, dir = new Vector3(-12.83f, 0f, 56.75f), r0 = 0.009f, r1 = 0.008f, section = 1.3f, depth = 0.6f, mirrorX = true },   // рёберный хрящ: заворачивает к грудине
            new Bone { name = "ребро9н", socket = "Сердце", parent = "ребро9с", length = 0.141f, dir = new Vector3(-11.95f, 0f, 54.57f), r0 = 0.009f, r1 = 0.008f, section = 1.3f, depth = 0.6f, mirrorX = true },   // рёберный хрящ: заворачивает к грудине
            new Bone { name = "ребро10н", socket = "Сердце", parent = "ребро10с", length = 0.134f, dir = new Vector3(-11.07f, 0f, 51.63f), r0 = 0.009f, r1 = 0.008f, section = 1.3f, depth = 0.6f, mirrorX = true },   // рёберный хрящ: заворачивает к грудине
            new Bone { name = "ребро11н", socket = "Сердце", parent = "ребро11с", length = 0.122f, dir = new Vector3(-10.7f, 0f, 46.65f), r0 = 0.009f, r1 = 0.008f, section = 1.3f, depth = 0.6f, mirrorX = true },   // рёберный хрящ: заворачивает к грудине
            new Bone { name = "ребро12н", socket = "Сердце", parent = "ребро12с", length = 0.111f, dir = new Vector3(-10.35f, 0f, 40.15f), r0 = 0.009f, r1 = 0.008f, section = 1.3f, depth = 0.6f, mirrorX = true },   // рёберный хрящ: заворачивает к грудине
            new Bone { name = "предплечье", socket = "Руки", parent = "плечо", attach = 1f, length = 0.314f, dir = new Vector3(-49.84f, 0f, 9.58f), r0 = 0.023f, r1 = 0.017f, section = 0.8f, depth = 1.15f, mirrorX = true },   // луч и локтевая вместе: спереди узкое, в профиль широкое
            new Bone { name = "локтевой_отр", socket = "Руки", parent = "плечо", length = 0.058f, dir = new Vector3(88.62f, 0f, -13.35f), r0 = 0.019f, r1 = 0.013f, section = 0.8f, mirrorX = true },   // локтевой отросток: острый угол локтя сзади, читается в профиль
            new Bone { name = "подвздошная", socket = "Ноги", parent = "крестец", attach = 0.29f, length = 0.236f, dir = new Vector3(-43.11f, 0f, -22.44f), r0 = 0.047f, r1 = 0.029f, section = 0.5f, depth = 1.25f, mirrorX = true },   // крыло подвздошной несёт КРУП: его наклон и есть линия зада
            new Bone { name = "хвост1", socket = "Хвост", parent = "крестец", attach = 1f, length = 0.224f, dir = new Vector3(-49.93f, 0f, 0f), r0 = 0.04f, r1 = 0.035f, section = 0.95f, chain = 2 },
            new Bone { name = "плечеголовная.м", socket = "шея", parent = "шея_3", attach = 0.65f, length = 0.521f, dir = new Vector3(-18.1f, 0f, -7.87f), r0 = 0.042f, section = 0.7f, mirrorX = true, layer = BodyLayer.Muscle },   // плечеголовная: передняя линия шеи от головы к плечу — самый заметный тяж на шее зверя
            new Bone { name = "широчайшая.м", socket = "хребет", parent = "поясница", attach = 0.28f, length = 0.689f, dir = new Vector3(-151.76f, 0f, -6.11f), r0 = 0.059f, section = 0.55f, mirrorX = true, layer = BodyLayer.Muscle },   // широчайшая: косой парус от поясницы к плечу — задняя граница лопатки в силуэте
            new Bone { name = "грудная.м", socket = "Сердце", parent = "грудина", attach = 0.55f, length = 0.129f, dir = new Vector3(2.66f, 0f, -33.86f), r0 = 0.044f, section = 0.75f, mirrorX = true, layer = BodyLayer.Muscle },   // грудная: преднагрудье между передними ногами, ширина груди спереди
            new Bone { name = "дельтовидная.м", socket = "Руки", parent = "лопатка", attach = 0.72f, length = 0.181f, dir = new Vector3(47.34f, 0f, 3.46f), r0 = 0.03f, section = 0.8f, mirrorX = true, layer = BodyLayer.Muscle },   // дельтовидная: округлость плечевого сустава
            new Bone { name = "длиннейшая.м", socket = "хребет", parent = "ост_пояс1", attach = 0.45f, length = 0.661f, dir = new Vector3(73.36f, 0f, 0f), r0 = 0.061f, section = 0.85f, mirrorX = true, layer = BodyLayer.Muscle },   // длиннейшая спины: валик вдоль позвоночника, ровная линия верха между холкой и крупом
            new Bone { name = "межрёберная5.м", socket = "Сердце", parent = "ребро5с", attach = 0.45f, length = 0.043f, dir = new Vector3(95.75f, 0f, -5.59f), r0 = 0.037f, section = 0.4f, mirrorX = true, layer = BodyLayer.Muscle },   // межрёберная: заполняет промежуток между соседними дугами
            new Bone { name = "межрёберная6.м", socket = "Сердце", parent = "ребро6с", attach = 0.45f, length = 0.042f, dir = new Vector3(96.92f, 0f, -3.23f), r0 = 0.037f, section = 0.4f, mirrorX = true, layer = BodyLayer.Muscle },   // межрёберная: заполняет промежуток между соседними дугами
            new Bone { name = "межрёберная7.м", socket = "Сердце", parent = "ребро7с", attach = 0.45f, length = 0.042f, dir = new Vector3(99.9f, 0f, -1.36f), r0 = 0.037f, section = 0.4f, mirrorX = true, layer = BodyLayer.Muscle },   // межрёберная: заполняет промежуток между соседними дугами
            new Bone { name = "межрёберная8.м", socket = "Сердце", parent = "ребро8с", attach = 0.45f, length = 0.042f, dir = new Vector3(105.29f, 0f, 0.08f), r0 = 0.037f, section = 0.4f, mirrorX = true, layer = BodyLayer.Muscle },   // межрёберная: заполняет промежуток между соседними дугами
            new Bone { name = "межрёберная9.м", socket = "Сердце", parent = "ребро9с", attach = 0.45f, length = 0.042f, dir = new Vector3(107.95f, 0f, 0.94f), r0 = 0.037f, section = 0.4f, mirrorX = true, layer = BodyLayer.Muscle },   // межрёберная: заполняет промежуток между соседними дугами
            new Bone { name = "межрёберная10.м", socket = "Сердце", parent = "ребро10с", attach = 0.45f, length = 0.043f, dir = new Vector3(117.33f, 0f, 2.46f), r0 = 0.037f, section = 0.4f, mirrorX = true, layer = BodyLayer.Muscle },   // межрёберная: заполняет промежуток между соседними дугами
            new Bone { name = "межрёберная11.м", socket = "Сердце", parent = "ребро11с", attach = 0.45f, length = 0.043f, dir = new Vector3(119.77f, 0f, 3.05f), r0 = 0.037f, section = 0.4f, mirrorX = true, layer = BodyLayer.Muscle },   // межрёберная: заполняет промежуток между соседними дугами
            new Bone { name = "подвздошно_рёберная.м", socket = "хребет", parent = "ребро9с", attach = 0.22f, length = 0.326f, dir = new Vector3(134.5f, 0f, 2.68f), r0 = 0.054f, section = 0.58f, mirrorX = true, layer = BodyLayer.Muscle },   // подвздошно-рёберная: валик над последними рёбрами, переход клетки в поясницу
            new Bone { name = "пясть", socket = "Руки", parent = "предплечье", attach = 1f, length = 0.182f, dir = new Vector3(7.84f, 0f, -1.27f), r0 = 0.02f, r1 = 0.018f, section = 1.35f, depth = 0.75f, mirrorX = true },   // четыре пясти пучком: поперёк шире, чем в глубину
            new Bone { name = "седалищная", socket = "Ноги", parent = "подвздошная", attach = 1f, length = 0.07f, dir = new Vector3(105.91f, 0f, 11.51f), r0 = 0.029f, r1 = 0.026f, section = 0.7f, mirrorX = true },   // седалищный бугор — задняя точка тела: им кончается круп
            new Bone { name = "лобковая", socket = "Ноги", parent = "подвздошная", attach = 1f, length = 0.12f, dir = new Vector3(-118.82f, 0f, 41.5f), r0 = 0.017f, r1 = 0.012f, section = 0.9f, depth = 0.7f, mirrorX = true },   // лобковая ветвь: ПОЛ ТАЗА до симфиза. Держит прямую живота и замыкает брюшную полость снизу — без неё полость течёт по средней линии и не заливается
            new Bone { name = "бедро", socket = "Ноги", parent = "подвздошная", attach = 1f, length = 0.328f, dir = new Vector3(-76.71f, 0f, 5.42f), r0 = 0.03f, r1 = 0.027f, section = 0.8f, mirrorX = true },
            new Bone { name = "хвост2", socket = "Хвост", parent = "хвост1", attach = 1f, length = 0.213f, dir = new Vector3(-21.69f, 0f, 0f), r0 = 0.035f, r1 = 0.03f, section = 0.95f, chain = 2 },
            new Bone { name = "трицепс.м", socket = "Руки", parent = "лопатка", attach = 0.28f, length = 0.373f, dir = new Vector3(48.63f, 0f, 1.53f), r0 = 0.066f, section = 0.72f, mirrorX = true, layer = BodyLayer.Muscle },   // трицепс: БОЛЬШОЙ ТРЕУГОЛЬНИК за плечом. Главная масса передней ноги в профиль
            new Bone { name = "бицепс.м", socket = "Руки", parent = "лопатка", attach = 0.92f, length = 0.349f, dir = new Vector3(60.48f, 0f, 6.72f), r0 = 0.03f, section = 0.8f, mirrorX = true, layer = BodyLayer.Muscle },   // двуглавая плеча: передняя выпуклость плеча
            new Bone { name = "косая_живота.м", socket = "хребет", parent = "ребро11с", attach = 0.75f, length = 0.582f, dir = new Vector3(109.8f, 0f, 1.49f), r0 = 0.051f, section = 0.52f, mirrorX = true, layer = BodyLayer.Muscle },   // наружная косая живота: БОК и подрыв паха — она делает талию подобранной
            new Bone { name = "поперечная_живота.м", socket = "хребет", parent = "ребро10н", attach = 0.7f, length = 0.691f, dir = new Vector3(117.74f, 0f, 23.36f), r0 = 0.047f, section = 0.46f, depth = 0.92f, mirrorX = true, layer = BodyLayer.Muscle },   // поперечная живота: стенка между рёберной дугой и тазом. Без неё бок за клеткой пуст
            new Bone { name = "лапа_п", socket = "Руки", parent = "пясть", attach = 1f, length = 0.13f, dir = new Vector3(-18.27f, 0f, 2.91f), r0 = 0.019f, r1 = 0.021f, section = 1.45f, depth = 0.8f, mirrorX = true },
            new Bone { name = "голень", socket = "Ноги", parent = "бедро", attach = 1f, length = 0.309f, dir = new Vector3(64.02f, 0f, 19.79f), r0 = 0.027f, r1 = 0.017f, section = 0.78f, depth = 1.2f, mirrorX = true },
            new Bone { name = "хвост3", socket = "Хвост", parent = "хвост2", attach = 1f, length = 0.192f, dir = new Vector3(-14.3f, 0f, 0f), r0 = 0.03f, r1 = 0.019f, section = 0.95f, chain = 2 },
            new Bone { name = "разгибатели.м", socket = "Руки", parent = "предплечье", attach = 0.08f, length = 0.344f, dir = new Vector3(1.25f, 0f, -0.2f), r0 = 0.035f, section = 0.85f, mirrorX = true, layer = BodyLayer.Muscle },   // разгибатели предплечья: мясо вверху голяшки, сухожилие внизу — оттого нога сужается к лапе
            new Bone { name = "прямая_живота.м", socket = "хребет", parent = "грудина", attach = 0.12f, length = 0.706f, dir = new Vector3(-158.06f, 0f, -0.73f), r0 = 0.037f, section = 0.66f, mirrorX = true, layer = BodyLayer.Muscle },   // прямая живота: нижняя линия от груди к паху. Идёт к ЛОБКУ по средней линии, а не к суставу вбок, — иначе левая и правая половины не смыкаются и живота у зверя нет
            new Bone { name = "ягодичная.м", socket = "Ноги", parent = "крестец", attach = 0.42f, length = 0.266f, dir = new Vector3(-66.62f, 0f, -19.8f), r0 = 0.044f, section = 0.7f, mirrorX = true, layer = BodyLayer.Muscle },   // ягодичная: КРУП. Его округлость целиком её работа
            new Bone { name = "пяточный", socket = "Ноги", parent = "голень", length = 0.061f, dir = new Vector3(106.08f, 0f, 9f), r0 = 0.02f, r1 = 0.015f, section = 0.8f, mirrorX = true },   // пяточный бугор: острый угол скакательного — главный признак задней ноги в профиль
            new Bone { name = "плюсна", socket = "Ноги", parent = "голень", attach = 1f, length = 0.231f, dir = new Vector3(-27.18f, 0f, -4.31f), r0 = 0.02f, r1 = 0.018f, section = 1.3f, depth = 0.75f, mirrorX = true },
            new Bone { name = "сухожилия_п.м", socket = "Руки", parent = "пясть", attach = 0.02f, length = 0.223f, dir = new Vector3(-3.68f, 0f, 0.6f), r0 = 0.015f, depth = 1.35f, mirrorX = true, layer = BodyLayer.Muscle },   // сухожилия сгибателей пясти: ТОНКИЙ ТЯЖ ДО ПАЛЬЦЕВ. Без него нога ниже запястья остаётся голой костью, а у неё профиль `long` — диафиз поджат до 52%, и в обмере пясть выходила 1.6 см толщиной, тоньше собственной кости. Сечение глубокое, а не круглое: сзади тяж, спереди кость
            new Bone { name = "напрягатель.м", socket = "Ноги", parent = "подвздошная", attach = 0.22f, length = 0.439f, dir = new Vector3(-47.93f, 0f, 5.87f), r0 = 0.04f, section = 0.52f, mirrorX = true, layer = BodyLayer.Muscle },   // напрягатель широкой фасции: передний край бедра, треугольник перед коленом
            new Bone { name = "четырёхглавая.м", socket = "Ноги", parent = "подвздошная", attach = 0.62f, length = 0.371f, dir = new Vector3(-60.51f, 0f, 5.88f), r0 = 0.049f, section = 0.78f, mirrorX = true, layer = BodyLayer.Muscle },   // четырёхглавая: передняя масса бедра, выносит колено вперёд в силуэте
            new Bone { name = "двуглавая_бедра.м", socket = "Ноги", parent = "седалищная", attach = 0.18f, length = 0.384f, dir = new Vector3(-170.92f, 0f, 20.53f), r0 = 0.047f, section = 0.58f, mirrorX = true, layer = BodyLayer.Muscle },   // двуглавая бедра: ЗАДНЯЯ ЛИНИЯ ЗВЕРЯ от крупа до скакательного. Самая крупная мышца тела
            new Bone { name = "полусухожильная.м", socket = "Ноги", parent = "седалищная", attach = 0.5f, length = 0.423f, dir = new Vector3(-168.01f, 0f, 20.55f), r0 = 0.035f, section = 0.54f, mirrorX = true, layer = BodyLayer.Muscle },   // полусухожильная: за двуглавой, даёт «штаны» на бедре
            new Bone { name = "лапа_з", socket = "Ноги", parent = "плюсна", attach = 1f, length = 0.096f, dir = new Vector3(-50.04f, 0f, -6.4f), r0 = 0.018f, r1 = 0.02f, section = 1.4f, depth = 0.8f, mirrorX = true },
            new Bone { name = "икроножная.м", socket = "Ноги", parent = "бедро", attach = 0.88f, length = 0.312f, dir = new Vector3(66.2f, 0f, 20.11f), r0 = 0.042f, section = 0.85f, mirrorX = true, layer = BodyLayer.Muscle },   // икроножная: голень спереди мясистая, сзади тянется в пяточное сухожилие
            new Bone { name = "сгибатели_з.м", socket = "Ноги", parent = "голень", attach = 0.18f, length = 0.317f, dir = new Vector3(-5.73f, 0f, -0.94f), r0 = 0.028f, section = 0.85f, mirrorX = true, layer = BodyLayer.Muscle },   // сгибатели плюсны: остаток мяса на голяшке ниже колена
            new Bone { name = "сухожилия_з.м", socket = "Ноги", parent = "плюсна", attach = 0.02f, length = 0.249f, dir = new Vector3(-5.86f, 0f, -0.86f), r0 = 0.015f, depth = 1.35f, mirrorX = true, layer = BodyLayer.Muscle },   // сухожилия сгибателей плюсны: тот же тяж на задней ноге. У волка он идёт до пальцев, и именно им плюсна держит глубину при малой ширине — «сухая» голяшка канида
        };
        // ЧТО ТЕПЕРЬ РИСУЕТ СКЕЛЕТ. Места остаются в данных: их имена — это `Organ.slot` (механика жива) и
        // АДРЕС С КАЛИБРОМ для детей (глаза, уши, нос и зубной ряд по-прежнему считаются от «головы»).
        // Скелет снимает с них только форму.
        //     «Пасти» здесь НЕТ, хотя челюсть принадлежит её модулю: подави мы место — исчезли бы зубы,
        // и пропажу было бы видно только глазом на скриншоте. Принадлежность кости слоту и подавление
        // места — разные вещи, потому и живут порознь
        // Уши, нос и хвост тоже ушли к костям: местами-шарами они не участвовали в поле и потому торчали
        // отдельными предметами на гладком теле. Цена перехода названа честно — привитый чужой хвост
        // (змеиный) пока не покажется, как и чужие уши: у ОРГАНОВ костей ещё нет
        wolf.skeletonHides = new[] { "хребет", "Шкура", "шея", "Сердце", "Руки", "Ноги", "голова", "уши", "нос", "Хвост" };
        // ШКУРА ПО ПОЛЮ — два числа на всего зверя. Слияние 0.09 выбрано ЗАМЕРОМ, а не на глаз: волнистость
        // бока (сумма скачков ширины вдоль тела) при трубах встык 0.616, при 0.05 — 0.164, при 0.09 — 0.040.
        // Дальше поднимать нельзя: тело начнёт оплывать, а перехват талии (0.39 против 0.62 в груди) исчезнет
        // ШАГ СЕТКИ 0.014 вместо 0.02: рот и ноздри — детали шириной около сантиметра, а щель УЖЕ ячейки
        // полигонизатор просто не видит. Цена разовая: меши кэшируются по виду
        wolf.skinCell = 0.014f;
        wolf.skinBlend = 0.09f;
        // ШУБА. Ость на спине и загривке у волка 60–80 мм при росте 0.78 — в нашем масштабе это ~0.045
        // равномерного слоя поверх мяса. Именно её отсутствия не хватало силуэту: по обхвату груди мы
        // даже избыточны, просто масса лежала не там, где её ищет глаз. Слой задаётся сдвигом
        // изоповерхности, поэтому ложится на ВСЁ тело сразу и ничего не деформирует
        // ШУБА ПОКА СНЯТА (0): сперва выверяем чистый скелет. Слой работает и включается одним числом,
        // но поверх недоделанной анатомии он просто заливает силуэт — что и вышло на прошлом прогоне
        wolf.skinFur = 0f;
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
            new Organ { organName = "Пит-орган",            slot = "Чутьё",  hotkey = "4", cost = 3, dashCooldown = 0.7f, enablesThermal = true, thermalRange = 14f, visualParts = new[] {
                new OrganPart { scale = new Vector3(1.00f, 1.00f, 1.00f), shape = PartShape.Sphere, role = PartRole.Pit }, // термоямка
                new OrganPart { scale = new Vector3(1.00f, 1.00f, 1.00f), shape = PartShape.Sphere, role = PartRole.Eye, color = new Color(1.00f, 0.45f, 0.12f, 1f) }, // ЦВЕТ ГЛАЗА = КАНАЛ: термо — видит тепло сквозь стены
            } }, // тепло сквозь стены; dashCd обязателен (0 = спам рывка)
            new Organ { organName = "Погремушка",          slot = "Погремушка", hotkey = "9", cost = 2, chassisOnly = true, visualParts = new[] {
                // СТОПКА РОГОВЫХ КОЛЕЦ вдоль хребта: шайбы-цилиндры с плоскими торцами (они и гремят стуком
                // друг о друга), доворот 90° кладёт ось цилиндра из Y в Z. Шаг 0.6 калибра = 0.09 м, толщина
                // та же — кольца стоят вплотную; сужение к кончику 1 → 0.61. Прежние доли раскладывали их
                // по Y (наследие поворота, жившего в данных) — стопка вставала торчком поперёк тела
                // ПО РЕФЕРЕНСУ: кольца — не диски, а ВЛОЖЕННЫЕ ЧАШЕЧКИ. Каждое = перетяжка (узкая шейка) +
                // ободок (широкий задний край), отсюда зубчатый силуэт «еловой шишки». Гладкий конус читался
                // просто как кончик хвоста: погремушку опознаёт РЕЛЬЕФ, а не диаметр. Профиль тоже не конус,
                // а веретено-маракас — 0.80 → 1.00 → 0.68: у хвоста перетяжка (погремушка отделена, а не
                // продолжает его), к середине раздутие, к концу сходит на нет
                new OrganPart { scale = new Vector3(0.576f, 0.178f, 0.576f), offset = new Vector3(0.00f, 0.00f,  1.297f), euler = new Vector3(90f, 0f, 0f), shape = PartShape.Cylinder, color = new Color(0.87f, 0.82f, 0.62f, 1f) }, // 1 шейка — перетяжка у хвоста (0.075)
                new OrganPart { scale = new Vector3(0.800f, 0.218f, 0.800f), offset = new Vector3(0.00f, 0.00f,  1.099f), euler = new Vector3(90f, 0f, 0f), shape = PartShape.Cylinder, color = new Color(0.87f, 0.82f, 0.62f, 1f) }, // 1 ободок (0.104)
                new OrganPart { scale = new Vector3(0.684f, 0.178f, 0.684f), offset = new Vector3(0.00f, 0.00f,  0.901f), euler = new Vector3(90f, 0f, 0f), shape = PartShape.Cylinder, color = new Color(0.87f, 0.82f, 0.62f, 1f) }, // 2 шейка
                new OrganPart { scale = new Vector3(0.950f, 0.218f, 0.950f), offset = new Vector3(0.00f, 0.00f,  0.703f), euler = new Vector3(90f, 0f, 0f), shape = PartShape.Cylinder, color = new Color(0.87f, 0.82f, 0.62f, 1f) }, // 2 ободок
                new OrganPart { scale = new Vector3(0.720f, 0.178f, 0.720f), offset = new Vector3(0.00f, 0.00f,  0.505f), euler = new Vector3(90f, 0f, 0f), shape = PartShape.Cylinder, color = new Color(0.87f, 0.82f, 0.62f, 1f) }, // 3 шейка
                new OrganPart { scale = new Vector3(1.000f, 0.218f, 1.000f), offset = new Vector3(0.00f, 0.00f,  0.307f), euler = new Vector3(90f, 0f, 0f), shape = PartShape.Cylinder, color = new Color(0.87f, 0.82f, 0.62f, 1f) }, // 3 ободок — САМОЕ широкое (0.130)
                new OrganPart { scale = new Vector3(0.706f, 0.178f, 0.706f), offset = new Vector3(0.00f, 0.00f,  0.109f), euler = new Vector3(90f, 0f, 0f), shape = PartShape.Cylinder, color = new Color(0.87f, 0.82f, 0.62f, 1f) }, // 4 шейка
                new OrganPart { scale = new Vector3(0.980f, 0.218f, 0.980f), offset = new Vector3(0.00f, 0.00f, -0.089f), euler = new Vector3(90f, 0f, 0f), shape = PartShape.Cylinder, color = new Color(0.87f, 0.82f, 0.62f, 1f) }, // 4 ободок
                new OrganPart { scale = new Vector3(0.662f, 0.178f, 0.662f), offset = new Vector3(0.00f, 0.00f, -0.287f), euler = new Vector3(90f, 0f, 0f), shape = PartShape.Cylinder, color = new Color(0.87f, 0.82f, 0.62f, 1f) }, // 5 шейка
                new OrganPart { scale = new Vector3(0.920f, 0.218f, 0.920f), offset = new Vector3(0.00f, 0.00f, -0.485f), euler = new Vector3(90f, 0f, 0f), shape = PartShape.Cylinder, color = new Color(0.87f, 0.82f, 0.62f, 1f) }, // 5 ободок
                new OrganPart { scale = new Vector3(0.590f, 0.178f, 0.590f), offset = new Vector3(0.00f, 0.00f, -0.683f), euler = new Vector3(90f, 0f, 0f), shape = PartShape.Cylinder, color = new Color(0.87f, 0.82f, 0.62f, 1f) }, // 6 шейка
                new OrganPart { scale = new Vector3(0.820f, 0.218f, 0.820f), offset = new Vector3(0.00f, 0.00f, -0.881f), euler = new Vector3(90f, 0f, 0f), shape = PartShape.Cylinder, color = new Color(0.87f, 0.82f, 0.62f, 1f) }, // 6 ободок
                new OrganPart { scale = new Vector3(0.490f, 0.178f, 0.490f), offset = new Vector3(0.00f, 0.00f, -1.079f), euler = new Vector3(90f, 0f, 0f), shape = PartShape.Cylinder, color = new Color(0.87f, 0.82f, 0.62f, 1f) }, // 7 шейка
                new OrganPart { scale = new Vector3(0.680f, 0.218f, 0.680f), offset = new Vector3(0.00f, 0.00f, -1.277f), euler = new Vector3(90f, 0f, 0f), shape = PartShape.Cylinder, color = new Color(0.87f, 0.82f, 0.62f, 1f) }, // 7 ободок — кончик (0.088)
            } }, // КОНЧИК ХВОСТА отдельным органом: цепь повторяет одну форму на всех звеньях, особый кончик ею не выразить. chassisOnly — принадлежность змеиного шасси
            new Organ { organName = "Хвост",                slot = "Хвост",  hotkey = "8", cost = 5, enablesConstrict = true, constrictStage = 3, nativeChassis = "Змея", visualScale = new Vector3(1f, 1f, 1f), visualSegments = 3, visualTaper = 0.82f, visualParts = new[] {
                new OrganPart { scale = new Vector3(1.000f, 1.200f, 0.586f), offset = new Vector3(0.00f, 0.00f, 0.00f), shape = PartShape.Capsule }, // звено В СТИЛЕ ТЕЛА
                new OrganPart { scale = new Vector3(1.000f, 1.000f, 0.586f), offset = new Vector3(0.00f, 0.00f, 0.00f), shape = PartShape.Sphere }, // СУСТАВ-шар: хвост собирается той же парой «звено + шарнир», что и тело (орган ПЕРЕБИВАЕТ форму места — правя только место, хвост не менялся вовсе)
            } }, // ХВОСТ СЕГМЕНТЕН: привитый — цепочка звеньев (≈треть змеиных сегментов), масштаб под человека, а не волчий обрубок. АУГУМЕНТ игроку (обхват); constrictStage=3 + nativeChassis=Змея → ст.3 удушения только на змеином шасси (у человека кап min(2,3)=2). «Тело-хвост» выше — ходовая часть ШАССИ змеи, не путать
        };
        // СОКЕТ-ПЛАН змеи — ТОЛЬКО ГНЁЗДА-ГРАФТЫ. Своё тело морфология НЕ строит и не трогает:
        //  • туловище и хвост — ЦЕПЬ СЕГМЕНТОВ (`SnakeBodyChain` расставляет их в МИРОВЫХ координатах каждый
        //    кадр, они ползут следом и лезут по стенам) — это локомоция, планом тела не выразить;
        //  • голова — статичные дети с ВКЛЮЧЁННЫМ коллайдером (поверхность попаданий), сносить нельзя.
        // Поэтому всё родное ведёт КОД (`codeDriven`), а морфология даёт химере на змеином шасси ВИДИМЫЕ конечности
        snake.sockets = new[]
        {
            // ── ГРАФ ЗМЕИ: голова — корень, дальше хребет цепью звеньев (спека 4.1: трети шея/туловище/хвост;
            // змея ДРЕВЕСНАЯ, её хвост длинный и цепкий — им же работает Constrict). Числа согласованы
            // с SnakePrefab (сегменты 0.55→0.42, шаг 0.62), чтобы новое тело совпало с уже ползающим.
            // [ANIM] codeDriven ПОКА ОСТАЁТСЯ: без переписанного SnakeBodyChain морф построил бы статичное
            // тело ПОВЕРХ префабной цепи — на арене оказалось бы две змеи. Снимается вместе с ним
            new BodySocket { name = "ямки", parent = "голова", attach = 0.500f, attachOffset = new Vector3(0.26f, -0.06f, 0.28f), sizeRel = new Vector3(0.14f, 0.16f, 0.12f), mirrorX = true, formFrom = "Чутьё", formRole = PartRole.Pit }, // АДРЕС ТЕРМОЯМОК: единственный внешний признак термочувства — форма Пит-органа
            new BodySocket { name = "голова", localPos = new Vector3(0.000f, 0.300f, 0.000f), baseSize = new Vector3(0.240f, 0.163f, 0.430f), codeDriven = true, solid = true, parts = new[] {
                // ТРЕУГОЛЬНЫЙ ЧЕРЕП ЯМКОГОЛОВОЙ: широкий затылок с ядовитыми железами → резкое сужение → тупая морда
                new OrganPart { scale = new Vector3(1.00f, 1.00f, 0.46f), offset = new Vector3(0.00f, 0.00f, -0.22f), shape = PartShape.Sphere }, // затылок с железами — САМОЕ широкое место, шире шеи
                new OrganPart { scale = new Vector3(0.86f, 0.34f, 0.52f), offset = new Vector3(0.00f, 0.26f, -0.14f) }, // ПЛОСКОЕ ТЕМЯ в щитках — кость гранёная
                new OrganPart { scale = new Vector3(0.70f, 0.88f, 0.34f), offset = new Vector3(0.00f, -0.02f, 0.10f), shape = PartShape.Sphere }, // сужение за глазами
                new OrganPart { scale = new Vector3(0.50f, 0.66f, 0.26f), offset = new Vector3(0.00f, -0.04f, 0.34f) }, // ТУПАЯ МОРДА (рострум) — обрублена, а не заострена
                new OrganPart { scale = new Vector3(0.30f, 0.16f, 0.30f), offset = new Vector3(0.30f, 0.24f, -0.04f) }, // надглазничный щиток (пр) — козырёк над глазом
                new OrganPart { scale = new Vector3(0.30f, 0.16f, 0.30f), offset = new Vector3(-0.30f, 0.24f, -0.04f) }, // надглазничный щиток (лев)
                new OrganPart { scale = new Vector3(0.10f, 0.10f, 0.08f), offset = new Vector3(0.16f, 0.02f, 0.44f), shape = PartShape.Sphere }, // ноздря (пр)
                new OrganPart { scale = new Vector3(0.10f, 0.10f, 0.08f), offset = new Vector3(-0.16f, 0.02f, 0.44f), shape = PartShape.Sphere }, // ноздря (лев)
            } },
            new BodySocket { name = "Пасть",  parent = "голова", attach = 1.000f, attachOffset = new Vector3(0.000f, -0.150f, 0.120f), baseSize = new Vector3(0.170f, 0.080f, 0.240f), sizeRel = new Vector3(0.708f, 0.491f, 0.558f), codeDriven = true, solid = true, parts = new[] {
                new OrganPart { scale = new Vector3(1.00f, 1.00f, 1.00f), offset = new Vector3(0.00f, 0.00f, 0.00f), shape = PartShape.Sphere }, // челюсть — ТЕПЕРЬ МЕСТО С ФОРМОЙ: змеиная морда наконец участвует в морфе
            } },
            // ЩЕЛЬ ЗА ГОЛОВОЙ: место головы 0.43 длиной, но НАРИСОВАННЫЙ затылок кончается на 0.194 от центра
            // (сфера 0.46 калибра, смещённая на −0.22) — цепь, поставленная по габариту места, висела в 5 см
            // позади черепа. Отсюда +0.035: шея начинается на 0.18, входя в затылок на полтора сантиметра
            new BodySocket { name = "глаза", parent = "голова", attach = 0.500f, attachOffset = new Vector3(0.400f, 0.180f, 0.000f), baseSize = new Vector3(0.056f, 0.056f, 0.056f), sizeRel = new Vector3(0.233f, 0.344f, 0.130f), mirrorX = true, formFrom = "Чутьё", formRole = PartRole.Eye, parts = new[] {
                new OrganPart { scale = new Vector3(1.00f, 1.00f, 1.00f), shape = PartShape.Sphere, role = PartRole.Eye, color = new Color(0.10f, 0.10f, 0.12f, 1f) }, // ФОЛБЭК: тварь без Чутья не слепа, но глаз тускл
            } }, // МЕСТО НА КОЖЕ головы (посчитано лучом из её центра) — форму и цвет даёт ЧУТЬЁ
            new BodySocket { name = "шея", parent = "голова", attach = 0.000f, attachOffset = new Vector3(0.000f, 0.000f, 0.035f), codeDriven = true, solid = true, linkDiameter = 0.215f, linkLength = 0.360f, linkTaper = 1.118f, chain = 4 }, // шея: от толщины головы РАСТЁТ к телу (taper > 1) — последнее звено ровно в тело
            // ДИАМЕТР НЕ ЗАДАН — наследуется: тело выходит из шеи (0.300), хвост из тела (0.266). Прежде числа
            // дублировались, и хвост стартовал с 0.300, то есть был ТОЛЩЕ туловища, из которого растёт
            new BodySocket { name = "Тело", parent = "шея", attach = 0.000f, attachOffset = new Vector3(0.000f, 0.000f, 0.000f), codeDriven = true, solid = true, linkLength = 0.360f, linkTaper = 0.970f, chain = 5 }, // туловище: самое массивное, чуть сходит к хвосту
            // ЗВЕНЬЯ ХВОСТА МЕЛЬЧЕ ТЕЛЕСНЫХ (0.24 против 0.36) — как хвостовые позвонки у змей. При общей
            // длине звена 0.36 кончик выходил втрое длиннее своей толщины, то есть тонкой прямой палочкой:
            // суставов на метр столько же, что у туловища, а контур из длинных отрезков читается жёстким.
            // Теперь 6 звеньев вместо 4: длина хвоста та же 1.44, суставов на метр 4.2 против 2.8
            new BodySocket { name = "Хвост", parent = "Тело", attach = 0.000f, attachOffset = new Vector3(0.000f, 0.000f, 0.000f), codeDriven = true, solid = true, linkLength = 0.240f, linkTaper = 0.843f, chain = 6 }, // хвост: подхватывает толщину тела (0.266) и уходит на конус к 0.113
            // ПОГРЕМУШКА — НЕ ЗВЕНО, и правильно, что не звено: другой орган (шасси-онли), цельный, со своим
            // смыслом — трещотка. Форму даёт ОРГАН (стопка роговых колец), место лишь держит калибр. Цепью
            // её описывать было ошибкой: кольца становились сегментами, и движок растаскивал их по пути
            // головы с общим шагом 0.36 при своей длине 0.09. Теперь морф вешает её ПОТОМКОМ последнего
            // звена хвоста (attach 0) — едет со звеном сама, цельная по построению, а не по списку имён.
            // Сдвиг на полустопки (0.18 м) — место центрирует её, а начинаться она должна от конца хвоста.
            // ВНИМАНИЕ: смещение задано в КАЛИБРАХ ХВОСТА, а его калибр вдоль хребта = длина звена. Мельчим
            // звено — надо пересчитать и это число: 0.75 × 0.24 = 0.18 (при звене 0.36 стояло 0.5)
            new BodySocket { name = "Погремушка", parent = "Хвост", attach = 0.000f, attachOffset = new Vector3(0.000f, 0.000f, -0.750f), baseSize = new Vector3(0.130f, 0.130f, 0.130f), solid = true },
            new BodySocket { name = "Шкура",  inner = true, parent = "Тело", attach = 0.500f, baseSize = new Vector3(0.300f, 0.300f, 1.200f) }, // чешуя — покров всей ЦЕПИ, своей детали нет. Но АДРЕС нужен под ЧУЖОЙ покров: Иглы ежа со своей формой сядут вдоль середины тела, а не метровым калибром в нуле. Раскладка игл по сегментам позвоночника — отдельная фича, не сейчас, и морф рисовал ей базовый куб в начале координат — тот самый ящик на голове
            new BodySocket { name = "Сердце", inner = true, parent = "Тело", attach = 0.700f, baseSize = new Vector3(0.220f, 0.220f, 0.400f) },  // у змеи хребта нет — сердце сидит на ТЕЛЕ-цепи, ближе к голове (0.7) и вытянуто вдоль неё. Калибр свой: у цепного родителя он выводится из звена, долей от него не возьмёшь
            new BodySocket { name = "Чутьё",  inner = true, parent = "голова", attach = 0.500f, sizeRel = new Vector3(0.500f, 0.500f, 0.500f) },  // ЧУВСТВА ЖИВУТ В ГОЛОВЕ. Своей формы у места нет, и деталь не родится сама собой — но АДРЕС нужен заранее: дашь органу форму (термо-ямки), и без родителя с калибром она сядет метровым кубом в начало координат. Доля от головы — одна на все виды
            // ГРАФТЫ: змея, отрастившая лапы/рога/иглы — читается сразу
            new BodySocket { name = "Руки",   parent = "Тело", attach = 0.880f, attachOffset = new Vector3(0.600f, -0.300f, 0.000f), baseSize = new Vector3(0.068f, 0.231f, 0.080f), mirrorX = true, graft = true }, // передняя пара — сразу за шеей
            new BodySocket { name = "Ноги",   parent = "Тело", attach = 0.220f, attachOffset = new Vector3(0.600f, -0.300f, 0.000f), baseSize = new Vector3(0.068f, 0.231f, 0.080f), mirrorX = true, graft = true }, // задняя пара — у перехода в хвост
            new BodySocket { name = "Рога",   parent = "голова", attach = 0.700f, attachOffset = new Vector3(0.500f, 0.600f, -0.200f), baseSize = new Vector3(0.061f, 0.061f, 0.072f), mirrorX = true, graft = true }, // КАЛИБР; на черепе, как у рогатых
            new BodySocket { name = "Игломёт",parent = "Тело", attach = 0.550f, attachOffset = new Vector3(0.000f, 0.600f, 0.000f),      baseSize = new Vector3(0.088f, 0.088f, 0.104f), baseEuler = new Vector3(-10f, 0f, 0f), graft = true }, // КАЛИБР; на спине, едет со звеном
        };

        snake.bones = new[]
        {
            // СКЕЛЕТ ВИДА «snake» — ВЫГРУЖЕНО из Tools/Blender/species/snake.py (tools/bones_to_cs.py).
            // Правится ТАМ, по анатомическим точкам; здесь только результат. Костей: 21, калибр (холка) 1.600 м
            new Bone { name = "череп", socket = "голова", origin = new Vector3(0f, 0.095f, 0.64f), length = 0.14f, dir = new Vector3(90f, 0f, 0f), r0 = 0.054f, r1 = 0.038f, section = 1.45f, depth = 1.1f },   // мозговой череп ямкоголовой: широкий затылок с железами, тупая морда
            new Bone { name = "ухо.п", socket = "Чутьё", origin = new Vector3(0.03f, 0.105f, 0.658f), length = 0.025f, dir = new Vector3(-106.93f, 0f, -11.75f), r0 = 0.019f, r1 = 0.013f, section = 0.45f, depth = 0.7f, mirrorX = true, layer = BodyLayer.Feature },   // термоямка / устье: видимый признак Пит-органа; привил волчий Нюх — сменится ухом
            new Bone { name = "глаз.п", socket = "голова", origin = new Vector3(0.032f, 0.1f, 0.7f), length = 0f, dir = new Vector3(0f, 0f, 0f), r0 = 0.013f, mirrorX = true, layer = BodyLayer.Feature },   // глаз — адрес на голове, форму даст Чутьё при графте (role Eye)
            new Bone { name = "нч_подвес", socket = "Пасть", parent = "череп", attach = 0.08f, length = 0.027f, dir = new Vector3(127.48f, 0f, -55.5f), r0 = 0.01f, mirrorX = true },   // вынос сустава к квадратной кости (боковая посадка)
            new Bone { name = "шея", socket = "шея", parent = "череп", attach = 0f, length = 0.08f, dir = new Vector3(176.42f, 0f, 0f), r0 = 0.048f, r1 = 0.045f, section = 1.15f, depth = 1.05f, chain = 2 },   // С1: выходит из затылка, толще головы — переход в тело
            new Bone { name = "ветвь", socket = "Пасть", parent = "нч_подвес", attach = 1f, length = 0.024f, dir = new Vector3(57.95f, 0f, 51.29f), r0 = 0.022f, r1 = 0.018f, section = 0.6f, depth = 0.9f, mirrorX = true },
            new Bone { name = "шея_2", socket = "шея", parent = "шея", attach = 0.999f, length = 0.12f, dir = new Vector3(1.2f, 0f, 0f), r0 = 0.045f, r1 = 0.042f, section = 1.15f, depth = 1.05f, chain = 2 },
            new Bone { name = "челюсть", socket = "Пасть", parent = "ветвь", attach = 1.002f, length = 0.153f, dir = new Vector3(-177.08f, 0f, 25.68f), r0 = 0.019f, r1 = 0.013f, section = 0.55f, depth = 0.85f, mirrorX = true },   // тело челюсти, узкая вбок — пасть открывается
            new Bone { name = "шея_3", socket = "шея", parent = "шея_2", attach = 1f, length = 0.12f, dir = new Vector3(-0.01f, 0f, 0f), r0 = 0.042f, r1 = 0.038f, section = 1.15f, depth = 1.05f, chain = 2 },
            new Bone { name = "шея_в", socket = "шея", parent = "шея_3", attach = 1f, length = 0.14f, dir = new Vector3(-0.88f, 0f, 0f), r0 = 0.038f, r1 = 0.035f, section = 1.15f, depth = 1.05f, chain = 2 },   // последнее шейное — переходит в туловище без шва
            new Bone { name = "тело1", socket = "Тело", parent = "шея_в", attach = 1f, length = 0.16f, dir = new Vector3(1.84f, 0f, 0f), r0 = 0.045f, r1 = 0.051f, chain = 3 },
            new Bone { name = "тело2", socket = "Тело", parent = "тело1", attach = 1f, length = 0.14f, dir = new Vector3(-0.21f, 0f, 0f), r0 = 0.051f, r1 = 0.054f, chain = 3 },
            new Bone { name = "тело3", socket = "Тело", parent = "тело2", attach = 1f, length = 0.16f, dir = new Vector3(0.21f, 0f, 0f), r0 = 0.054f, r1 = 0.048f, chain = 3 },
            new Bone { name = "тело4", socket = "Тело", parent = "тело3", attach = 1f, length = 0.18f, dir = new Vector3(-0.16f, 0f, 0f), r0 = 0.048f, r1 = 0.042f, chain = 3 },
            new Bone { name = "тело5", socket = "Тело", parent = "тело4", attach = 1f, length = 0.08f, dir = new Vector3(-1.99f, 0f, 0f), r0 = 0.042f, r1 = 0.035f, chain = 3 },   // последнее туловищное — стык с хвостом
            new Bone { name = "хвост1", socket = "Хвост", parent = "тело5", attach = 0.999f, length = 0.08f, dir = new Vector3(1.43f, 0f, 0f), r0 = 0.035f, r1 = 0.03f, chain = 2 },
            new Bone { name = "хвост2", socket = "Хвост", parent = "хвост1", attach = 0.999f, length = 0.07f, dir = new Vector3(-1.12f, 0f, 0f), r0 = 0.03f, r1 = 0.026f, chain = 2 },
            new Bone { name = "хвост3", socket = "Хвост", parent = "хвост2", attach = 1f, length = 0.06f, dir = new Vector3(0.41f, 0f, 0f), r0 = 0.026f, r1 = 0.021f, chain = 2 },
            new Bone { name = "хвост4", socket = "Хвост", parent = "хвост3", attach = 1f, length = 0.05f, dir = new Vector3(-1.72f, 0f, 0f), r0 = 0.021f, r1 = 0.018f, chain = 2 },
            new Bone { name = "хвост5", socket = "Хвост", parent = "хвост4", attach = 0.999f, length = 0.04f, dir = new Vector3(-1.13f, 0f, 0f), r0 = 0.018f, r1 = 0.014f, chain = 2 },
            new Bone { name = "хвост6", socket = "Хвост", parent = "хвост5", attach = 1f, length = 0.03f, dir = new Vector3(1.9f, 0f, 0f), r0 = 0.018f, r1 = 0.014f, chain = 2 },   // кончик с погремушкой (форма погремушки — орган)
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
            new Organ { organName = "Хребет", slot = "хребет", chassisOnly = true, visualParts = new[] {
                new OrganPart { scale = new Vector3(0.92f, 0.86f, 0.44f), offset = new Vector3(0.00f, 0.06f, -0.32f), shape = PartShape.Sphere }, // круп
                new OrganPart { scale = new Vector3(0.50f, 0.82f, 0.44f), offset = new Vector3(0.33f, -0.20f, -0.32f), shape = PartShape.Sphere }, // бедро (пр)
                new OrganPart { scale = new Vector3(0.50f, 0.82f, 0.44f), offset = new Vector3(-0.33f, -0.20f, -0.32f), shape = PartShape.Sphere }, // бедро (лев)
                new OrganPart { scale = new Vector3(0.44f, 0.74f, 0.38f), offset = new Vector3(0.31f, -0.12f, 0.28f), shape = PartShape.Sphere }, // лопатка (пр)
                new OrganPart { scale = new Vector3(0.44f, 0.74f, 0.38f), offset = new Vector3(-0.31f, -0.12f, 0.28f), shape = PartShape.Sphere }, // лопатка (лев)
            } }, // СКЕЛЕТ: несущая структура шасси. chassisOnly — её не крадут графтом, как «Тело-хвост»
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
            new Organ { organName = "Слух",           slot = "Чутьё",  hotkey = "4", cost = 3, dashCooldown = 0.7f, keenHearing = true, hearingMult = 2f, visualParts = new[] {
                new OrganPart { scale = new Vector3(1.00f, 1.00f, 1.00f), shape = PartShape.Sphere, role = PartRole.Nose }, // нос
                new OrganPart { scale = new Vector3(0.92f, 1.00f, 0.72f), offset = new Vector3(0.00f, 0.00f, 0.00f), role = PartRole.Ear }, // раковина
                new OrganPart { scale = new Vector3(0.58f, 0.52f, 0.50f), offset = new Vector3(0.00f, 0.44f, -0.08f), role = PartRole.Ear }, // кончик
                new OrganPart { scale = new Vector3(1.00f, 1.00f, 1.00f), shape = PartShape.Sphere, role = PartRole.Eye, color = new Color(0.78f, 0.82f, 0.86f, 1f) }, // ЦВЕТ ГЛАЗА = КАНАЛ: слух
            } }, // ОСТРЫЙ СЛУХ: вдвое дальше + различение вида + волны звука на экране (лось — слухач при слабом зрении)
            new Organ { organName = "Лосиное сердце", slot = "Сердце", hotkey = "3", cost = 6, hpBonus = 2f, staminaBonus = 0.6f, staminaRegenBonus = 0f, regen = 1f, regenOOC = 0f, atkCooldown = 0.5f, bleedResist = true, visualParts = new[] {
                new OrganPart { scale = new Vector3(1.06f, 1.00f, 1.10f), offset = new Vector3(0.00f, 0.00f, 0.04f), shape = PartShape.Sphere }, // грудная клетка ЛОСЯ
            } }, // +200% HP + КРОВЕУПОРНОСТЬ: сердце ТАНКА — явный HP-король (обгоняет волчьи 1.75); у массивного лося своё преимущество (гора HP), а кровь ему особенно опасна (% от макс HP)
            new Organ { organName = "Толстая шкура",  slot = "Шкура",  hotkey = "6", cost = 5, damageReduction = 0.35f, visualScale = new Vector3(1.15f, 1.1f, 1f) }, // броня против ПРЯМОГО урона (не крови)
            new Organ { organName = "Рога",           slot = "Рога",   hotkey = "8", cost = 5, enablesAntler = true, visualScale = new Vector3(1f, 1f, 1f), visualParts = new[] {
                new OrganPart { scale = new Vector3(0.75f, 0.60f, 0.75f), offset = new Vector3(0.05f, 0.00f, 0.00f), shape = PartShape.Sphere }, // розетка
                new OrganPart { scale = new Vector3(0.46f, 0.65f, 0.48f), offset = new Vector3(0.28f, 0.22f, 0.02f), euler = new Vector3(0f, 0f, -62f), shape = PartShape.Capsule }, // короткий ствол
                new OrganPart { scale = new Vector3(1.90f, 0.26f, 2.20f), offset = new Vector3(1.35f, 0.56f, 0.05f), euler = new Vector3(-34f, 0f, 24f) }, // лопата — УЖЕ прежней: у живого лося она изрезана глубокими вырезами, примитивом их не сделать, поэтому силуэт «гребёнки» держат отростки, а не доска
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
            new BodySocket { name = "хребет", baseEuler = new Vector3(38.000f, 0.000f, 0.000f), attachOffset = new Vector3(0.000f, 0.922f, -0.909f), parent = "шея", attach = 1.000f, baseSize = new Vector3(0.521f, 0.791f, 2.284f) }, // НЕСУЩИЙ ЦЕНТР: форму даёт орган «Хребет» (chassisOnly), поэтому место больше не служебное  // ЕДИНЫЙ ПЛАН ТЕЛА (спека 2026-08-27): ось от головы назад. Числа посчитаны Anatomy/tools/reroot.py и самопроверены — тело осталось на месте
            new BodySocket { name = "нос", parent = "голова", attach = 0.500f, attachOffset = new Vector3(0.00f, -0.12f, 0.42f), sizeRel = new Vector3(0.54f, 0.36f, 0.16f), formFrom = "Чутьё", formRole = PartRole.Nose }, // АДРЕС НОСА: мочку рисует орган Чутья
            new BodySocket { name = "голова", localPos = new Vector3(0.000f, 2.379f, 1.474f), baseSize = new Vector3(0.284f, 0.442f, 0.711f), baseEuler = new Vector3(2.000f, 0.000f, 0.000f), parts = new[] {
                new OrganPart { scale = new Vector3(1.00f, 0.64f, 0.34f), offset = new Vector3(0.00f, 0.14f, -0.33f), shape = PartShape.Sphere }, // КРУГ: мозговой отдел — у лося МАЛЫЙ и сдвинут назад
                new OrganPart { scale = new Vector3(0.74f, 0.54f, 0.66f), offset = new Vector3(0.00f, -0.04f, 0.12f), shape = PartShape.Sphere }, // КЛИН: морда — три четверти длины головы
                new OrganPart { scale = new Vector3(0.68f, 0.32f, 0.34f), offset = new Vector3(0.00f, 0.15f, 0.00f), shape = PartShape.Sphere }, // горбинка переносицы — римский профиль лося
                new OrganPart { scale = new Vector3(0.60f, 0.30f, 0.22f), offset = new Vector3(0.00f, -0.27f, 0.36f), shape = PartShape.Sphere }, // нависающая верхняя губа — примета лося
                new OrganPart { scale = new Vector3(0.46f, 0.26f, 0.52f), offset = new Vector3(0.00f, -0.28f, 0.08f), shape = PartShape.Sphere }, // нижняя челюсть
            } }, // 40 гасит наклон шеи (−38) до +2 — РОВНО столько же, сколько у Пасти: череп и морда в ОДНУ линию, излома нет
            new BodySocket { name = "Пасть", parent = "голова", attach = 1.000f, attachOffset = new Vector3(0.000f, -0.250f, -0.050f),  baseSize = new Vector3(0.195f, 0.221f, 0.337f), sizeRel = new Vector3(0.685f, 0.500f, 0.474f), baseEuler = new Vector3(0f, 0f, 0f), parts = new[] {
                new OrganPart { scale = new Vector3(0.94f, 0.30f, 0.92f), offset = new Vector3(0.00f, 0.00f, 0.00f), shape = PartShape.Sphere }, // ЛИНИЯ РТА — морду держит голова, пасть только смыкание челюстей
            } }, // длинная морда с горбинкой; излом к черепу УБРАН (0) — «голова задрана, моська прямо» шло именно от него
            new BodySocket { name = "глаза", parent = "голова", attach = 0.500f, attachOffset = new Vector3(0.420f, 0.130f, -0.240f), baseSize = new Vector3(0.058f, 0.058f, 0.058f), sizeRel = new Vector3(0.205f, 0.132f, 0.082f), mirrorX = true, formFrom = "Чутьё", formRole = PartRole.Eye, parts = new[] {
                new OrganPart { scale = new Vector3(1.00f, 1.00f, 1.00f), shape = PartShape.Sphere, role = PartRole.Eye, color = new Color(0.10f, 0.10f, 0.12f, 1f) }, // ФОЛБЭК: тварь без Чутья не слепа, но глаз тускл
            } }, // МЕСТО НА КОЖЕ головы (посчитано лучом из её центра) — форму и цвет даёт ЧУТЬЁ
            new BodySocket { name = "уши", parent = "голова", attach = 0.700f, attachOffset = new Vector3(0.590f, 0.341f, -0.511f),    baseSize = new Vector3(0.149f, 0.390f, 0.129f), sizeRel = new Vector3(0.525f, 0.882f, 0.182f), baseEuler = new Vector3(-24f, 0f, 26f), mirrorX = true, formFrom = "Чутьё", formRole = PartRole.Ear },   // АДРЕС УХА: раковину рисует орган слуха, а не шасси — привил чужое Чутьё, и ухо стало чужим

            new BodySocket { name = "шея", parent = "голова", attach = 0.000f, attachOffset = new Vector3(0.000f, -0.595f, -0.021f),    baseSize = new Vector3(0.420f, 0.560f, 0.700f), sizeRel = new Vector3(1.477f, 1.267f, 0.982f), baseEuler = new Vector3(-40.000f, 0.000f, 0.000f), parts = new[] {
                new OrganPart { scale = new Vector3(0.94f, 1.20f, 0.77f), offset = new Vector3(0.00f, 0.00f, 0.00f), euler = new Vector3(90f, 0f, 0f), shape = PartShape.Capsule }, // шея одной массой
                new OrganPart { scale = new Vector3(0.54f, 0.64f, 0.32f), offset = new Vector3(0.00f, -0.34f, 0.32f), euler = new Vector3(20f, 0f, 0f), shape = PartShape.Sphere }, // ПОДГРУДОК (висячая складка). Прошлый заход убрал «торпеду», но заодно срезал свес до 5 см — складка пропала. Свес вернул (10 см), а торпеду снимает УЗОСТЬ (0.23 при шее 0.40) и сдвиг ВПЕРЁД, к голове: у лося складка висит под челюстью, а не тянется вдоль всей шеи
            } },
            new BodySocket { name = "горб", parent = "хребет", attach = 0.735f, attachOffset = new Vector3(0.000f, 0.367f, -0.020f),   baseSize = new Vector3(0.396f, 0.443f, 0.640f), sizeRel = new Vector3(0.760f, 0.560f, 0.280f), parts = new[] {
                new OrganPart { scale = new Vector3(0.92f, 1.00f, 0.60f), offset = new Vector3(0.00f, 0.03f, 0.08f), shape = PartShape.Sphere }, // ПИК холки — ШИРОКИЙ (0.61 при груди 0.59): холка это массивный горб над лопатками. Узкий высокий гребень читался вертикальным плавником, а не телом
                new OrganPart { scale = new Vector3(1.00f, 0.80f, 0.72f), offset = new Vector3(0.00f, -0.20f, 0.06f), shape = PartShape.Sphere }, // плечевая масса — основание под пиком
                new OrganPart { scale = new Vector3(0.88f, 0.66f, 0.95f), offset = new Vector3(0.00f, -0.18f, -0.46f), shape = PartShape.Sphere }, // ДЛИННЫЙ задний скат к спине — он и читает силуэт
                new OrganPart { scale = new Vector3(0.80f, 0.66f, 0.44f), offset = new Vector3(0.00f, -0.16f, 0.40f), shape = PartShape.Sphere }, // передний скат — ВЫТЯНУТ вперёд поверх основания шеи: место удлинено 1.30 → 1.60, и скат теперь накрывает излом «шея↔холка», а не упирается в него торцом
            } }, // ХОЛКА: остистые отростки грудных позвонков + мышца. Выше крупа на ~10% — как у живого лося; читается СИЛУЭТОМ (резкий подъём от шеи, долгий спад к крестцу), а не узостью
            new BodySocket { name = "Шкура",  parent = "хребет", attach = 0.500f,   baseSize = new Vector3(0.521f, 0.791f, 2.284f), sizeRel = new Vector3(1.000f, 1.000f, 1.000f), parts = new[] {
                new OrganPart { scale = new Vector3(0.96f, 0.92f, 0.90f), offset = new Vector3(0.00f, 0.04f, -0.02f), shape = PartShape.Sphere }, // корпус
                // грудная клетка УШЛА В ОРГАН «Лосиное сердце» — её лепит сердце, чужое перестраивает силуэт
            } }, // корпус целиком (грудь+круп)
            new BodySocket { name = "Руки", parent = "хребет", attach = 0.780f, attachOffset = new Vector3(0.367f, -1.300f, 0.079f),   baseSize = new Vector3(0.170f, 1.520f, 0.193f), sizeRel = new Vector3(0.326f, 1.922f, 0.085f), mirrorX = true }, // передние ходули (Копыто)
            new BodySocket { name = "Ноги", parent = "хребет", attach = 0.220f, attachOffset = new Vector3(0.367f, -1.283f, -0.089f),   baseSize = new Vector3(0.205f, 1.550f, 0.228f), sizeRel = new Vector3(0.393f, 1.960f, 0.100f), mirrorX = true },
            new BodySocket { name = "Хвост", parent = "хребет", attach = 0.000f, attachOffset = new Vector3(0.000f, 0.310f, -0.010f),  baseSize = new Vector3(0.130f, 0.130f, 0.340f), sizeRel = new Vector3(0.250f, 0.164f, 0.149f), baseEuler = new Vector3(-28f, 0f, 0f), parts = new[] {
                new OrganPart { scale = new Vector3(1.15f, 1.15f, 0.48f), offset = new Vector3(0.00f, 0.00f, 0.34f), shape = PartShape.Sphere }, // ШАРНИР основания (та же схема, что у волка) — уменьшен, хвост выдвинут назад и поднят: вылет кончика за круп 8 → 21 см
                new OrganPart { scale = new Vector3(1.00f, 1.00f, 0.44f), offset = new Vector3(0.00f, 0.00f, -0.02f) }, // сегмент 1
                new OrganPart { scale = new Vector3(0.80f, 0.80f, 0.40f), offset = new Vector3(0.00f, 0.00f, -0.42f) }, // сегмент 2
                new OrganPart { scale = new Vector3(0.60f, 0.60f, 0.32f), offset = new Vector3(0.00f, 0.00f, -0.76f) }, // кисточка
            } }, // ЕДИНАЯ СХЕМА ХВОСТА (шар-шарнир + сегменты вдоль Z): как у волка, только короче — лосиный обрубок. Шарнир сидит в крупе, а кончик ВЫХОДИТ за него на 17 см: прошлый заход утопил хвост целиком (кончик −1.21 при заднем крае крупа −1.23)
            new BodySocket { name = "Рога", parent = "голова", attach = 0.700f, attachOffset = new Vector3(0.500f, 0.560f, -0.100f),   baseSize = new Vector3(0.415f, 0.416f, 0.416f), sizeRel = new Vector3(1.461f, 0.941f, 0.584f), baseEuler = new Vector3(-2f, 0f, 0f), mirrorX = true }, // СВОИ рога: −2 гасит наклон ветки «шея→голова» (+2) до нуля — лопата горизонтальна: КАЛИБР крупный. В ВИСКАХ (верх черепа 2.17) и ВБОК за габарит головы — раньше лопасти врастали в макушку и торчали из висков
            new BodySocket { name = "Сердце", inner = true, parent = "хребет", attach = 0.500f, attachOffset = new Vector3(0.000f, 0.040f, 0.000f), baseSize = new Vector3(0.375f, 0.617f, 0.731f), sizeRel = new Vector3(0.720f, 0.780f, 0.320f) }, // ГРУДНАЯ КОРОБКА лося: самая объёмная // внутреннее место: форму (грудную клетку) даёт орган
            new BodySocket { name = "Чутьё",  inner = true, parent = "голова", attach = 0.500f, sizeRel = new Vector3(0.500f, 0.500f, 0.500f) },  // ЧУВСТВА ЖИВУТ В ГОЛОВЕ. Своей формы у места нет, и деталь не родится сама собой — но АДРЕС нужен заранее: дашь органу форму (термо-ямки), и без родителя с калибром она сядет метровым кубом в начало координат. Доля от головы — одна на все виды
            new BodySocket { name = "Игломёт", parent = "хребет", attach = 0.550f, attachOffset = new Vector3(0.000f, 0.411f, -0.050f), baseSize = new Vector3(0.281f, 0.281f, 0.281f), sizeRel = new Vector3(0.539f, 0.355f, 0.123f), baseEuler = new Vector3(-10.00f, 0.00f, 0.00f), graft = true }, // КАЛИБР (крупная туша): НА спине (верх туши 2.10) — основания шипов входят в корпус; сдвинут назад, не спорит с горбом
        };

        moose.bones = new[]
        {
            // СКЕЛЕТ ВИДА «moose» — ВЫГРУЖЕНО из Tools/Blender/species/moose.py (tools/bones_to_cs.py).
            // Правится ТАМ, по анатомическим точкам; здесь только результат. Костей: 130, калибр (холка) 2.310 м
            new Bone { name = "грудина", socket = "Сердце", origin = new Vector3(0f, 1.33f, -0.416f), length = 0.708f, dir = new Vector3(74.43f, 0f, 0f), r0 = 0.035f, r1 = 0.04f, section = 0.85f, depth = 1.1f, chain = 3 },   // грудина держит НИЗ груди. Без неё дуги висят концами в воздухе
            new Bone { name = "лёгкие", socket = "Сердце", origin = new Vector3(0f, 1.715f, -0.567f), length = 0.754f, dir = new Vector3(85.86f, 0f, 0f), r0 = 0.226f, r1 = 0.203f, depth = 1.48f, layer = BodyLayer.Muscle },   // содержимое грудной полости: лёгкие и сердце. Поперечник изнутри рёбер (дуга 0.115 холки), высота от позвонка до грудины
            new Bone { name = "брюшина", socket = "хребет", origin = new Vector3(0f, 1.715f, -0.567f), length = 0.907f, dir = new Vector3(-82.62f, 0f, 0f), r0 = 0.203f, r1 = 0.162f, depth = 1.3f, layer = BodyLayer.Muscle },   // содержимое брюшной полости. Уже грудной и сходит на конус к тазу — отсюда подобранная талия, которую иначе рисовать нечем
            new Bone { name = "череп", socket = "голова", origin = new Vector3(0f, 2.646f, 0.845f), length = 0.574f, dir = new Vector3(125.39f, 0f, 0f), r0 = 0.03f },   // мозговой череп ОБОЛОЧКОЙ по 49 сечениям, снятым с пластины
            new Bone { name = "скула", socket = "голова", origin = new Vector3(0.058f, 2.497f, 0.934f), length = 0.115f, dir = new Vector3(129.09f, 0f, -20.38f), r0 = 0.025f, r1 = 0.023f, section = 0.75f, depth = 1.35f, mirrorX = true },   // задняя ветвь скуловой дуги: от височной кости наружу-вперёд
            new Bone { name = "глазница.р", socket = "голова", origin = new Vector3(0.125f, 2.453f, 1.135f), length = 0.102f, dir = new Vector3(-105f, 0f, 71.7f), r0 = 0.069f, r1 = 0.018f, depth = 1.1f, mirrorX = true },   // глазница: конус, сужающийся внутрь черепа. Смотрит вперёд-вбок, как у хищника
            new Bone { name = "ухо.п", socket = "Чутьё", origin = new Vector3(0.05f, 2.579f, 1.03f), length = 0.411f, dir = new Vector3(-24.65f, 0f, -6.29f), r0 = 0.106f, r1 = 0.028f, depth = 0.52f, mirrorX = true, layer = BodyLayer.Feature },   // ухо: широкое у основания, к кончику сходит на нет, поперёк ПЛОСКОЕ. Стоячее ухо — половина того, чем волк опознаётся издали
            new Bone { name = "затылочный", socket = "голова", parent = "череп", attach = 0f, length = 0.128f, dir = new Vector3(64.57f, 0f, 0f), r0 = 0.055f, r1 = 0.046f },   // затылочная кость: несёт свод от сустава с атлантом. Внутри оболочки, снаружи не видна
            new Bone { name = "скула_п", socket = "голова", parent = "скула", attach = 1f, length = 0.15f, dir = new Vector3(-28.83f, 0f, 32.1f), r0 = 0.023f, r1 = 0.021f, section = 0.75f, depth = 1.25f, mirrorX = true },   // передняя ветвь дуги: сходится к верхнечелюстной под глазницей
            new Bone { name = "нч_подвес", socket = "Пасть", parent = "череп", attach = 0.241f, length = 0.107f, dir = new Vector3(84.61f, 0f, -29.06f), r0 = 0.018f, mirrorX = true },   // вынос сустава от оси к нижнечелюстной ямке. Кость-связка: начало ребёнка всегда лежит на родителе, поэтому боковую посадку приходится проходить отдельным звеном
            new Bone { name = "клык_в", socket = "Пасть", parent = "череп", attach = 0.905f, length = 0.177f, dir = new Vector3(46.48f, 0f, -22.99f), r0 = 0.025f, r1 = 0.005f, section = 0.85f, depth = 0.85f, mirrorX = true, layer = BodyLayer.Feature },   // верхний клык FEATURE: вниз и чуть вперёд (на черепе)
            new Bone { name = "шея_в", socket = "шея", parent = "затылочный", attach = 1f, length = 0.22f, dir = new Vector3(26.18f, 0f, 0f), r0 = 0.049f, r1 = 0.056f, section = 1.12f, chain = 2 },   // атлант и эпистрофей: несут голову, самое подвижное звено
            new Bone { name = "ветвь", socket = "Пасть", parent = "нч_подвес", attach = 0.987f, length = 0.067f, dir = new Vector3(31.56f, 0f, 26.48f), r0 = 0.069f, r1 = 0.051f, section = 0.34f, depth = 1.1f, mirrorX = true },   // ветвь челюсти: от сустава вниз-назад к угловому отростку
            new Bone { name = "венечный", socket = "Пасть", parent = "нч_подвес", attach = 0.987f, length = 0.106f, dir = new Vector3(-150.73f, 0f, -21.79f), r0 = 0.06f, r1 = 0.018f, section = 0.22f, depth = 1.25f, mirrorX = true },   // венечный отросток: пластина ВНУТРИ скуловой дуги. К ней крепится височная мышца — она и даёт волку силу укуса
            new Bone { name = "шея_3", socket = "шея", parent = "шея_в", attach = 1f, length = 0.232f, dir = new Vector3(10.26f, 0f, 0f), r0 = 0.056f, r1 = 0.064f, section = 1.18f, chain = 2 },   // C3–C2: здесь шея начинает задираться к голове
            new Bone { name = "челюсть", socket = "Пасть", parent = "ветвь", attach = 1f, length = 0.435f, dir = new Vector3(-119.24f, 0f, 17.43f), r0 = 0.049f, r1 = 0.032f, section = 0.5f, depth = 1.15f, mirrorX = true },   // тело челюсти с зубным рядом: узкое вбок, высокое в профиль
            new Bone { name = "грудиночелюстная.м", socket = "шея", parent = "грудина", attach = 0.92f, length = 1.173f, dir = new Vector3(-39.61f, 0f, -2.47f), r0 = 0.06f, section = 0.65f, mirrorX = true, layer = BodyLayer.Muscle },   // грудиночелюстная: линия горла. Она отделяет шею от груди
            new Bone { name = "жевательная.м", socket = "Пасть", parent = "скула", attach = 0.55f, length = 0.104f, dir = new Vector3(146.71f, 0f, -0.49f), r0 = 0.065f, section = 0.8f, mirrorX = true, layer = BodyLayer.Muscle },   // жевательная: щека. Заполняет угол между дугой и челюстью
            new Bone { name = "височная.м", socket = "голова", parent = "череп", attach = 0.3f, length = 0.047f, dir = new Vector3(47.01f, 0f, -75.5f), r0 = 0.079f, section = 0.7f, depth = 1.1f, mirrorX = true, layer = BodyLayer.Muscle },   // височная: заполняет височную яму под дугой. Ею голова хищника шире в скулах, чем в своде
            new Bone { name = "шея_2", socket = "шея", parent = "шея_3", attach = 1f, length = 0.222f, dir = new Vector3(18.77f, 0f, 0f), r0 = 0.064f, r1 = 0.071f, section = 1.22f, chain = 2 },   // C5–C4: самое глубокое место шеи
            new Bone { name = "клык_н", socket = "Пасть", parent = "челюсть", attach = 0.92f, length = 0.111f, dir = new Vector3(-118.48f, 0f, -36.69f), r0 = 0.023f, r1 = 0.005f, section = 0.85f, depth = 0.85f, mirrorX = true, layer = BodyLayer.Feature },   // нижний клык FEATURE: ВВЕРХ, встаёт перед верхним (на челюсти, открывается)
            new Bone { name = "шея", socket = "шея", parent = "шея_2", attach = 1f, length = 0.23f, dir = new Vector3(6.87f, 0f, 0f), r0 = 0.071f, r1 = 0.078f, section = 1.25f, chain = 2 },   // C7–C6: выходит из холки ПОЛОГО, почти горизонтально
            new Bone { name = "холка", socket = "хребет", parent = "шея", attach = 1f, length = 0.308f, dir = new Vector3(24.85f, 0f, 0f), r0 = 0.045f, r1 = 0.047f, section = 1.3f, chain = 3 },   // передний грудной отдел. Отдельной костью потому, что ХОЛКА — это его остистые отростки, и править её высоту надо, не трогая длину всей грудной клетки
            new Bone { name = "грудной", socket = "хребет", parent = "холка", attach = 1f, length = 0.519f, dir = new Vector3(-2.02f, 0f, 0f), r0 = 0.047f, r1 = 0.049f, section = 1.35f, chain = 4 },   // задний грудной отдел: несёт восемь каудальных рёбер
            new Bone { name = "остистый1", socket = "хребет", parent = "холка", attach = 0.054f, length = 0.259f, dir = new Vector3(65.11f, 0f, 0f), r0 = 0.037f, r1 = 0.023f, section = 0.42f, depth = 0.95f },
            new Bone { name = "остистый2", socket = "хребет", parent = "холка", attach = 0.377f, length = 0.309f, dir = new Vector3(57.11f, 0f, 0f), r0 = 0.037f, r1 = 0.023f, section = 0.42f, depth = 0.95f },
            new Bone { name = "остистый3", socket = "хребет", parent = "холка", attach = 0.753f, length = 0.305f, dir = new Vector3(55.11f, 0f, 0f), r0 = 0.037f, r1 = 0.023f, section = 0.42f, depth = 0.95f },
            new Bone { name = "ребро1", socket = "Сердце", parent = "холка", attach = 0.054f, length = 0.391f, dir = new Vector3(-97.99f, 0f, -11.61f), r0 = 0.025f, r1 = 0.02f, section = 1.6f, depth = 0.55f, mirrorX = true },   // головка и шейка ребра: вбок круче всего
            new Bone { name = "ребро2", socket = "Сердце", parent = "холка", attach = 0.23f, length = 0.402f, dir = new Vector3(-98.23f, 0f, -13.72f), r0 = 0.025f, r1 = 0.02f, section = 1.6f, depth = 0.55f, mirrorX = true },   // головка и шейка ребра: вбок круче всего
            new Bone { name = "ребро3", socket = "Сердце", parent = "холка", attach = 0.411f, length = 0.413f, dir = new Vector3(-98.47f, 0f, -15.62f), r0 = 0.025f, r1 = 0.02f, section = 1.6f, depth = 0.55f, mirrorX = true },   // головка и шейка ребра: вбок круче всего
            new Bone { name = "ребро4", socket = "Сердце", parent = "холка", attach = 0.616f, length = 0.422f, dir = new Vector3(-98.8f, 0f, -17.05f), r0 = 0.025f, r1 = 0.02f, section = 1.6f, depth = 0.55f, mirrorX = true },   // головка и шейка ребра: вбок круче всего
            new Bone { name = "ребро5", socket = "Сердце", parent = "холка", attach = 0.831f, length = 0.43f, dir = new Vector3(-99.15f, 0f, -18.2f), r0 = 0.025f, r1 = 0.02f, section = 1.6f, depth = 0.55f, mirrorX = true },   // головка и шейка ребра: вбок круче всего
            new Bone { name = "пластыревидная.м", socket = "шея", parent = "холка", attach = 0.25f, length = 0.965f, dir = new Vector3(139.32f, 0f, 0f), r0 = 0.067f, section = 0.75f, mirrorX = true, layer = BodyLayer.Muscle },   // пластыревидная: несёт голову, наполняет загривок за черепом
            new Bone { name = "поясница", socket = "хребет", parent = "грудной", attach = 1f, length = 0.624f, dir = new Vector3(-10.03f, 0f, 0f), r0 = 0.049f, r1 = 0.056f, section = 1.45f, chain = 4 },   // поясничный отдел дугой вверх: от него подобранность талии
            new Bone { name = "остистый4", socket = "хребет", parent = "грудной", attach = 0.109f, length = 0.273f, dir = new Vector3(59.13f, 0f, 0f), r0 = 0.037f, r1 = 0.023f, section = 0.42f, depth = 0.95f },
            new Bone { name = "остистый5", socket = "хребет", parent = "грудной", attach = 0.395f, length = 0.226f, dir = new Vector3(63.13f, 0f, 0f), r0 = 0.037f, r1 = 0.023f, section = 0.42f, depth = 0.95f },
            new Bone { name = "остистый6", socket = "хребет", parent = "грудной", attach = 0.713f, length = 0.18f, dir = new Vector3(69.13f, 0f, 0f), r0 = 0.037f, r1 = 0.023f, section = 0.42f, depth = 0.95f },
            new Bone { name = "остистый7", socket = "хребет", parent = "грудной", attach = 0.936f, length = 0.152f, dir = new Vector3(77.13f, 0f, 0f), r0 = 0.037f, r1 = 0.023f, section = 0.42f, depth = 0.95f },
            new Bone { name = "ребро1с", socket = "Сердце", parent = "ребро1", length = 0.516f, dir = new Vector3(-1.65f, 0f, 4.43f), r0 = 0.02f, r1 = 0.019f, section = 1.6f, depth = 0.55f, mirrorX = true },   // тело ребра: здесь бок шире всего
            new Bone { name = "ребро2с", socket = "Сердце", parent = "ребро2", length = 0.528f, dir = new Vector3(-1.99f, 0f, 5.2f), r0 = 0.02f, r1 = 0.019f, section = 1.6f, depth = 0.55f, mirrorX = true },   // тело ребра: здесь бок шире всего
            new Bone { name = "ребро3с", socket = "Сердце", parent = "ребро3", length = 0.54f, dir = new Vector3(-2.35f, 0f, 5.89f), r0 = 0.02f, r1 = 0.019f, section = 1.6f, depth = 0.55f, mirrorX = true },   // тело ребра: здесь бок шире всего
            new Bone { name = "ребро4с", socket = "Сердце", parent = "ребро4", length = 0.549f, dir = new Vector3(-2.82f, 0f, 6.4f), r0 = 0.02f, r1 = 0.019f, section = 1.6f, depth = 0.55f, mirrorX = true },   // тело ребра: здесь бок шире всего
            new Bone { name = "ребро5с", socket = "Сердце", parent = "ребро5", length = 0.558f, dir = new Vector3(-3.34f, 0f, 6.81f), r0 = 0.02f, r1 = 0.019f, section = 1.6f, depth = 0.55f, mirrorX = true },   // тело ребра: здесь бок шире всего
            new Bone { name = "ребро6", socket = "Сердце", parent = "грудной", attach = 0.039f, length = 0.437f, dir = new Vector3(-97.56f, 0f, -18.86f), r0 = 0.025f, r1 = 0.02f, section = 1.6f, depth = 0.55f, mirrorX = true },   // головка и шейка ребра: вбок круче всего
            new Bone { name = "ребро7", socket = "Сердце", parent = "грудной", attach = 0.178f, length = 0.442f, dir = new Vector3(-98f, 0f, -19.18f), r0 = 0.025f, r1 = 0.02f, section = 1.6f, depth = 0.55f, mirrorX = true },   // головка и шейка ребра: вбок круче всего
            new Bone { name = "ребро8", socket = "Сердце", parent = "грудной", attach = 0.317f, length = 0.445f, dir = new Vector3(-98.46f, 0f, -19.14f), r0 = 0.025f, r1 = 0.02f, section = 1.6f, depth = 0.55f, mirrorX = true },   // головка и шейка ребра: вбок круче всего
            new Bone { name = "ребро9", socket = "Сердце", parent = "грудной", attach = 0.456f, length = 0.444f, dir = new Vector3(-98.93f, 0f, -18.76f), r0 = 0.025f, r1 = 0.02f, section = 1.6f, depth = 0.55f, mirrorX = true },   // головка и шейка ребра: вбок круче всего
            new Bone { name = "ребро10", socket = "Сердце", parent = "грудной", attach = 0.595f, length = 0.442f, dir = new Vector3(-99.42f, 0f, -18.19f), r0 = 0.025f, r1 = 0.02f, section = 1.6f, depth = 0.55f, mirrorX = true },   // головка и шейка ребра: вбок круче всего
            new Bone { name = "ребро11", socket = "Сердце", parent = "грудной", attach = 0.734f, length = 0.435f, dir = new Vector3(-100f, 0f, -17.11f), r0 = 0.025f, r1 = 0.02f, section = 1.6f, depth = 0.55f, mirrorX = true },   // головка и шейка ребра: вбок круче всего
            new Bone { name = "ребро12", socket = "Сердце", parent = "грудной", attach = 0.873f, length = 0.427f, dir = new Vector3(-100.61f, 0f, -15.88f), r0 = 0.025f, r1 = 0.02f, section = 1.6f, depth = 0.55f, mirrorX = true },   // головка и шейка ребра: вбок круче всего
            new Bone { name = "лопатка", socket = "Руки", parent = "остистый2", attach = 0.418f, length = 0.588f, dir = new Vector3(167.05f, 0f, -9.9f), r0 = 0.097f, r1 = 0.031f, section = 0.3f, depth = 1.3f, mirrorX = true },   // плоская лопасть, лежащая НА рёбрах: даёт покатое плечо и переход холки в ногу
            new Bone { name = "выйная.м", socket = "шея", parent = "остистый1", attach = 0.85f, length = 0.916f, dir = new Vector3(81.81f, 0f, 0f), r0 = 0.058f, section = 0.68f, layer = BodyLayer.Muscle },   // выйная связка с пластыревидной: ГРЕБЕНЬ шеи. Без неё шея проваливается к позвонкам
            new Bone { name = "крестец", socket = "хребет", parent = "поясница", attach = 1f, length = 0.211f, dir = new Vector3(-1.62f, 0f, 0f), r0 = 0.064f, r1 = 0.069f, section = 1.55f },   // на крестце сходятся таз, хвост и поясница — корень всего графа
            new Bone { name = "ост_пояс1", socket = "хребет", parent = "поясница", attach = 0.84f, length = 0.134f, dir = new Vector3(109.16f, 0f, 0f), r0 = 0.035f, r1 = 0.021f, section = 0.42f, depth = 0.95f },
            new Bone { name = "ост_пояс2", socket = "хребет", parent = "поясница", attach = 0.54f, length = 0.143f, dir = new Vector3(105.16f, 0f, 0f), r0 = 0.035f, r1 = 0.021f, section = 0.42f, depth = 0.95f },
            new Bone { name = "ост_пояс3", socket = "хребет", parent = "поясница", attach = 0.26f, length = 0.139f, dir = new Vector3(101.16f, 0f, 0f), r0 = 0.035f, r1 = 0.021f, section = 0.42f, depth = 0.95f },
            new Bone { name = "ост_пояс4", socket = "хребет", parent = "поясница", attach = 0.05f, length = 0.129f, dir = new Vector3(97.16f, 0f, 0f), r0 = 0.035f, r1 = 0.021f, section = 0.42f, depth = 0.95f },
            new Bone { name = "попереч1", socket = "хребет", parent = "поясница", attach = 0.8f, length = 0.134f, dir = new Vector3(-106.64f, 0f, -85.4f), r0 = 0.03f, r1 = 0.018f, section = 0.9f, depth = 0.55f, mirrorX = true },
            new Bone { name = "попереч2", socket = "хребет", parent = "поясница", attach = 0.52f, length = 0.144f, dir = new Vector3(-106.64f, 0f, -85.7f), r0 = 0.03f, r1 = 0.018f, section = 0.9f, depth = 0.55f, mirrorX = true },
            new Bone { name = "попереч3", socket = "хребет", parent = "поясница", attach = 0.24f, length = 0.134f, dir = new Vector3(-106.64f, 0f, -85.4f), r0 = 0.03f, r1 = 0.018f, section = 0.9f, depth = 0.55f, mirrorX = true },
            new Bone { name = "ребро1н", socket = "Сердце", parent = "ребро1с", length = 0.401f, dir = new Vector3(-1.87f, 0f, 23.57f), r0 = 0.019f, r1 = 0.016f, section = 1.3f, depth = 0.6f, mirrorX = true },   // рёберный хрящ: заворачивает к грудине
            new Bone { name = "ребро2н", socket = "Сердце", parent = "ребро2с", length = 0.417f, dir = new Vector3(-2.38f, 0f, 28.15f), r0 = 0.019f, r1 = 0.016f, section = 1.3f, depth = 0.6f, mirrorX = true },   // рёберный хрящ: заворачивает к грудине
            new Bone { name = "ребро3н", socket = "Сердце", parent = "ребро3с", length = 0.433f, dir = new Vector3(-2.97f, 0f, 32.2f), r0 = 0.019f, r1 = 0.016f, section = 1.3f, depth = 0.6f, mirrorX = true },   // рёберный хрящ: заворачивает к грудине
            new Bone { name = "ребро4н", socket = "Сердце", parent = "ребро4с", length = 0.448f, dir = new Vector3(-3.74f, 0f, 35.19f), r0 = 0.019f, r1 = 0.016f, section = 1.3f, depth = 0.6f, mirrorX = true },   // рёберный хрящ: заворачивает к грудине
            new Bone { name = "ребро5н", socket = "Сердце", parent = "ребро5с", length = 0.461f, dir = new Vector3(-4.59f, 0f, 37.47f), r0 = 0.019f, r1 = 0.016f, section = 1.3f, depth = 0.6f, mirrorX = true },   // рёберный хрящ: заворачивает к грудине
            new Bone { name = "ребро6с", socket = "Сердце", parent = "ребро6", length = 0.567f, dir = new Vector3(-3.94f, 0f, 7.04f), r0 = 0.02f, r1 = 0.019f, section = 1.6f, depth = 0.55f, mirrorX = true },   // тело ребра: здесь бок шире всего
            new Bone { name = "ребро7с", socket = "Сердце", parent = "ребро7", length = 0.574f, dir = new Vector3(-4.58f, 0f, 7.16f), r0 = 0.02f, r1 = 0.019f, section = 1.6f, depth = 0.55f, mirrorX = true },   // тело ребра: здесь бок шире всего
            new Bone { name = "ребро8с", socket = "Сердце", parent = "ребро8", length = 0.578f, dir = new Vector3(-5.24f, 0f, 7.15f), r0 = 0.02f, r1 = 0.019f, section = 1.6f, depth = 0.55f, mirrorX = true },   // тело ребра: здесь бок шире всего
            new Bone { name = "ребро9с", socket = "Сердце", parent = "ребро9", length = 0.58f, dir = new Vector3(-5.93f, 0f, 7.03f), r0 = 0.02f, r1 = 0.019f, section = 1.6f, depth = 0.55f, mirrorX = true },   // тело ребра: здесь бок шире всего
            new Bone { name = "ребро10с", socket = "Сердце", parent = "ребро10", length = 0.58f, dir = new Vector3(-6.62f, 0f, 6.85f), r0 = 0.02f, r1 = 0.019f, section = 1.6f, depth = 0.55f, mirrorX = true },   // тело ребра: здесь бок шире всего
            new Bone { name = "ребро11с", socket = "Сердце", parent = "ребро11", length = 0.576f, dir = new Vector3(-7.43f, 0f, 6.48f), r0 = 0.02f, r1 = 0.019f, section = 1.6f, depth = 0.55f, mirrorX = true },   // тело ребра: здесь бок шире всего
            new Bone { name = "ребро12с", socket = "Сердце", parent = "ребро12", length = 0.57f, dir = new Vector3(-8.28f, 0f, 6.06f), r0 = 0.02f, r1 = 0.019f, section = 1.6f, depth = 0.55f, mirrorX = true },   // тело ребра: здесь бок шире всего
            new Bone { name = "плечо", socket = "Руки", parent = "лопатка", attach = 1f, length = 0.524f, dir = new Vector3(69.33f, 0f, 4.19f), r0 = 0.051f, r1 = 0.047f, section = 0.85f, mirrorX = true },
            new Bone { name = "зубчатая.м", socket = "Сердце", parent = "ребро4с", attach = 0.45f, length = 0.703f, dir = new Vector3(-172.26f, 0f, 2.02f), r0 = 0.106f, section = 0.6f, depth = 1.1f, mirrorX = true, layer = BodyLayer.Muscle },   // зубчатая вентральная: ПОДВЕС корпуса между лопатками. Ею тело буквально висит на ногах
            new Bone { name = "трапециевидная.м", socket = "хребет", parent = "остистый3", attach = 0.7f, length = 0.378f, dir = new Vector3(158.43f, 0f, -5.37f), r0 = 0.079f, section = 0.55f, mirrorX = true, layer = BodyLayer.Muscle },   // трапециевидная: покатое плечо, переход холки в лопатку
            new Bone { name = "ромбовидная.м", socket = "хребет", parent = "остистый1", attach = 0.55f, length = 0.092f, dir = new Vector3(-106.8f, 0f, -6.31f), r0 = 0.069f, section = 0.55f, mirrorX = true, layer = BodyLayer.Muscle },   // ромбовидная: прижимает верх лопатки к холке, наполняет загривок
            new Bone { name = "межрёберная1.м", socket = "Сердце", parent = "ребро1с", attach = 0.45f, length = 0.055f, dir = new Vector3(83.25f, 0f, -23.67f), r0 = 0.074f, section = 0.4f, mirrorX = true, layer = BodyLayer.Muscle },   // межрёберная: заполняет промежуток между соседними дугами
            new Bone { name = "межрёберная2.м", socket = "Сердце", parent = "ребро2с", attach = 0.45f, length = 0.055f, dir = new Vector3(84.49f, 0f, -22.02f), r0 = 0.074f, section = 0.4f, mirrorX = true, layer = BodyLayer.Muscle },   // межрёберная: заполняет промежуток между соседними дугами
            new Bone { name = "межрёберная3.м", socket = "Сердце", parent = "ребро3с", attach = 0.45f, length = 0.059f, dir = new Vector3(89.79f, 0f, -16.36f), r0 = 0.074f, section = 0.4f, mirrorX = true, layer = BodyLayer.Muscle },   // межрёберная: заполняет промежуток между соседними дугами
            new Bone { name = "межрёберная4.м", socket = "Сердце", parent = "ребро4с", attach = 0.45f, length = 0.061f, dir = new Vector3(91.76f, 0f, -13.57f), r0 = 0.074f, section = 0.4f, mirrorX = true, layer = BodyLayer.Muscle },   // межрёберная: заполняет промежуток между соседними дугами
            new Bone { name = "ребро6н", socket = "Сердце", parent = "ребро6с", length = 0.472f, dir = new Vector3(-5.54f, 0f, 38.61f), r0 = 0.019f, r1 = 0.016f, section = 1.3f, depth = 0.6f, mirrorX = true },   // рёберный хрящ: заворачивает к грудине
            new Bone { name = "ребро7н", socket = "Сердце", parent = "ребро7с", length = 0.479f, dir = new Vector3(-6.45f, 0f, 38.86f), r0 = 0.019f, r1 = 0.016f, section = 1.3f, depth = 0.6f, mirrorX = true },   // рёберный хрящ: заворачивает к грудине
            new Bone { name = "ребро8н", socket = "Сердце", parent = "ребро8с", length = 0.483f, dir = new Vector3(-7.28f, 0f, 38.23f), r0 = 0.019f, r1 = 0.016f, section = 1.3f, depth = 0.6f, mirrorX = true },   // рёберный хрящ: заворачивает к грудине
            new Bone { name = "ребро9н", socket = "Сердце", parent = "ребро9с", length = 0.483f, dir = new Vector3(-7.99f, 0f, 36.69f), r0 = 0.019f, r1 = 0.016f, section = 1.3f, depth = 0.6f, mirrorX = true },   // рёберный хрящ: заворачивает к грудине
            new Bone { name = "ребро10н", socket = "Сердце", parent = "ребро10с", length = 0.48f, dir = new Vector3(-8.59f, 0f, 34.64f), r0 = 0.019f, r1 = 0.016f, section = 1.3f, depth = 0.6f, mirrorX = true },   // рёберный хрящ: заворачивает к грудине
            new Bone { name = "ребро11н", socket = "Сердце", parent = "ребро11с", length = 0.472f, dir = new Vector3(-9.11f, 0f, 31.22f), r0 = 0.019f, r1 = 0.016f, section = 1.3f, depth = 0.6f, mirrorX = true },   // рёберный хрящ: заворачивает к грудине
            new Bone { name = "ребро12н", socket = "Сердце", parent = "ребро12с", length = 0.464f, dir = new Vector3(-9.55f, 0f, 27.36f), r0 = 0.019f, r1 = 0.016f, section = 1.3f, depth = 0.6f, mirrorX = true },   // рёберный хрящ: заворачивает к грудине
            new Bone { name = "предплечье", socket = "Руки", parent = "плечо", attach = 1f, length = 0.616f, dir = new Vector3(-45.21f, 0f, 6.14f), r0 = 0.046f, r1 = 0.033f, section = 0.8f, depth = 1.15f, mirrorX = true },   // луч и локтевая вместе: спереди узкое, в профиль широкое
            new Bone { name = "локтевой_отр", socket = "Руки", parent = "плечо", length = 0.114f, dir = new Vector3(91.82f, 0f, -9.23f), r0 = 0.037f, r1 = 0.025f, section = 0.8f, mirrorX = true },   // локтевой отросток: острый угол локтя сзади, читается в профиль
            new Bone { name = "подвздошная", socket = "Ноги", parent = "крестец", attach = 0.289f, length = 0.428f, dir = new Vector3(-45.78f, 0f, -16.43f), r0 = 0.093f, r1 = 0.057f, section = 0.5f, depth = 1.25f, mirrorX = true },   // крыло подвздошной несёт КРУП: его наклон и есть линия зада
            new Bone { name = "хвост1", socket = "Хвост", parent = "крестец", attach = 1f, length = 0.244f, dir = new Vector3(-46.72f, 0f, 0f), r0 = 0.079f, r1 = 0.069f, section = 0.95f, chain = 2 },
            new Bone { name = "плечеголовная.м", socket = "шея", parent = "шея_3", attach = 0.65f, length = 0.993f, dir = new Vector3(-17.66f, 0f, -5.59f), r0 = 0.083f, section = 0.7f, mirrorX = true, layer = BodyLayer.Muscle },   // плечеголовная: передняя линия шеи от головы к плечу — самый заметный тяж на шее зверя
            new Bone { name = "широчайшая.м", socket = "хребет", parent = "поясница", attach = 0.28f, length = 1.229f, dir = new Vector3(-148.46f, 0f, -4.62f), r0 = 0.116f, section = 0.55f, mirrorX = true, layer = BodyLayer.Muscle },   // широчайшая: косой парус от поясницы к плечу — задняя граница лопатки в силуэте
            new Bone { name = "грудная.м", socket = "Сердце", parent = "грудина", attach = 0.55f, length = 0.21f, dir = new Vector3(15.52f, 0f, -27.68f), r0 = 0.088f, section = 0.75f, mirrorX = true, layer = BodyLayer.Muscle },   // грудная: преднагрудье между передними ногами, ширина груди спереди
            new Bone { name = "дельтовидная.м", socket = "Руки", parent = "лопатка", attach = 0.72f, length = 0.355f, dir = new Vector3(43.6f, 0f, 3.09f), r0 = 0.06f, section = 0.8f, mirrorX = true, layer = BodyLayer.Muscle },   // дельтовидная: округлость плечевого сустава
            new Bone { name = "длиннейшая.м", socket = "хребет", parent = "ост_пояс1", attach = 0.45f, length = 1.149f, dir = new Vector3(72.98f, 0f, 0f), r0 = 0.12f, section = 0.85f, mirrorX = true, layer = BodyLayer.Muscle },   // длиннейшая спины: валик вдоль позвоночника, ровная линия верха между холкой и крупом
            new Bone { name = "межрёберная5.м", socket = "Сердце", parent = "ребро5с", attach = 0.45f, length = 0.064f, dir = new Vector3(94.12f, 0f, -8.41f), r0 = 0.074f, section = 0.4f, mirrorX = true, layer = BodyLayer.Muscle },   // межрёберная: заполняет промежуток между соседними дугами
            new Bone { name = "межрёберная6.м", socket = "Сердце", parent = "ребро6с", attach = 0.45f, length = 0.064f, dir = new Vector3(95.9f, 0f, -4.83f), r0 = 0.074f, section = 0.4f, mirrorX = true, layer = BodyLayer.Muscle },   // межрёберная: заполняет промежуток между соседними дугами
            new Bone { name = "межрёберная7.м", socket = "Сердце", parent = "ребро7с", attach = 0.45f, length = 0.063f, dir = new Vector3(99.41f, 0f, -1.24f), r0 = 0.074f, section = 0.4f, mirrorX = true, layer = BodyLayer.Muscle },   // межрёберная: заполняет промежуток между соседними дугами
            new Bone { name = "межрёберная8.м", socket = "Сердце", parent = "ребро8с", attach = 0.45f, length = 0.063f, dir = new Vector3(104.5f, 0f, 2.4f), r0 = 0.074f, section = 0.4f, mirrorX = true, layer = BodyLayer.Muscle },   // межрёберная: заполняет промежуток между соседними дугами
            new Bone { name = "межрёберная9.м", socket = "Сердце", parent = "ребро9с", attach = 0.45f, length = 0.064f, dir = new Vector3(107.73f, 0f, 4.38f), r0 = 0.074f, section = 0.4f, mirrorX = true, layer = BodyLayer.Muscle },   // межрёберная: заполняет промежуток между соседними дугами
            new Bone { name = "межрёберная10.м", socket = "Сердце", parent = "ребро10с", attach = 0.45f, length = 0.065f, dir = new Vector3(115.4f, 0f, 9.41f), r0 = 0.074f, section = 0.4f, mirrorX = true, layer = BodyLayer.Muscle },   // межрёберная: заполняет промежуток между соседними дугами
            new Bone { name = "межрёберная11.м", socket = "Сердце", parent = "ребро11с", attach = 0.45f, length = 0.066f, dir = new Vector3(118.04f, 0f, 10.65f), r0 = 0.074f, section = 0.4f, mirrorX = true, layer = BodyLayer.Muscle },   // межрёберная: заполняет промежуток между соседними дугами
            new Bone { name = "подвздошно_рёберная.м", socket = "хребет", parent = "ребро9с", attach = 0.22f, length = 0.788f, dir = new Vector3(151.33f, 0f, 3.12f), r0 = 0.106f, section = 0.58f, mirrorX = true, layer = BodyLayer.Muscle },   // подвздошно-рёберная: валик над последними рёбрами, переход клетки в поясницу
            new Bone { name = "пясть", socket = "Руки", parent = "предплечье", attach = 1f, length = 0.359f, dir = new Vector3(7.15f, 0f, -0.88f), r0 = 0.039f, r1 = 0.035f, section = 1.35f, depth = 0.75f, mirrorX = true },   // четыре пясти пучком: поперёк шире, чем в глубину
            new Bone { name = "седалищная", socket = "Ноги", parent = "подвздошная", attach = 1f, length = 0.128f, dir = new Vector3(109.5f, 0f, 6.53f), r0 = 0.057f, r1 = 0.051f, section = 0.7f, mirrorX = true },   // седалищный бугор — задняя точка тела: им кончается круп
            new Bone { name = "лобковая", socket = "Ноги", parent = "подвздошная", attach = 1f, length = 0.189f, dir = new Vector3(-103.8f, 0f, 37.75f), r0 = 0.033f, r1 = 0.024f, section = 0.9f, depth = 0.7f, mirrorX = true },   // лобковая ветвь: ПОЛ ТАЗА до симфиза. Держит прямую живота и замыкает брюшную полость снизу — без неё полость течёт по средней линии и не заливается
            new Bone { name = "бедро", socket = "Ноги", parent = "подвздошная", attach = 1f, length = 0.625f, dir = new Vector3(-70.08f, 0f, 5.74f), r0 = 0.06f, r1 = 0.053f, section = 0.8f, mirrorX = true },
            new Bone { name = "хвост2", socket = "Хвост", parent = "хвост1", attach = 1f, length = 0.265f, dir = new Vector3(-28.68f, 0f, 0f), r0 = 0.069f, r1 = 0.06f, section = 0.95f, chain = 2 },
            new Bone { name = "трицепс.м", socket = "Руки", parent = "лопатка", attach = 0.28f, length = 0.732f, dir = new Vector3(45.6f, 0f, 1.69f), r0 = 0.129f, section = 0.72f, mirrorX = true, layer = BodyLayer.Muscle },   // трицепс: БОЛЬШОЙ ТРЕУГОЛЬНИК за плечом. Главная масса передней ноги в профиль
            new Bone { name = "бицепс.м", socket = "Руки", parent = "лопатка", attach = 0.92f, length = 0.682f, dir = new Vector3(55.21f, 0f, 5.51f), r0 = 0.06f, section = 0.8f, mirrorX = true, layer = BodyLayer.Muscle },   // двуглавая плеча: передняя выпуклость плеча
            new Bone { name = "косая_живота.м", socket = "хребет", parent = "ребро11с", attach = 0.75f, length = 1.248f, dir = new Vector3(131.86f, 0f, 1.11f), r0 = 0.102f, section = 0.52f, mirrorX = true, layer = BodyLayer.Muscle },   // наружная косая живота: БОК и подрыв паха — она делает талию подобранной
            new Bone { name = "поперечная_живота.м", socket = "хребет", parent = "ребро10н", attach = 0.7f, length = 1.641f, dir = new Vector3(143.35f, 0f, 22.65f), r0 = 0.092f, section = 0.46f, depth = 0.92f, mirrorX = true, layer = BodyLayer.Muscle },   // поперечная живота: стенка между рёберной дугой и тазом. Без неё бок за клеткой пуст
            new Bone { name = "лапа_п", socket = "Руки", parent = "пясть", attach = 1f, length = 0.251f, dir = new Vector3(-16.78f, 0f, 2.01f), r0 = 0.037f, r1 = 0.041f, section = 1.45f, depth = 0.8f, mirrorX = true },
            new Bone { name = "голень", socket = "Ноги", parent = "бедро", attach = 1f, length = 0.595f, dir = new Vector3(58.71f, 0f, 13.26f), r0 = 0.053f, r1 = 0.033f, section = 0.78f, depth = 1.2f, mirrorX = true },
            new Bone { name = "хвост3", socket = "Хвост", parent = "хвост2", attach = 1f, length = 0.18f, dir = new Vector3(-11f, 0f, 0f), r0 = 0.06f, r1 = 0.037f, section = 0.95f, chain = 2 },
            new Bone { name = "разгибатели.м", socket = "Руки", parent = "предплечье", attach = 0.08f, length = 0.673f, dir = new Vector3(1.14f, 0f, -0.14f), r0 = 0.069f, section = 0.85f, mirrorX = true, layer = BodyLayer.Muscle },   // разгибатели предплечья: мясо вверху голяшки, сухожилие внизу — оттого нога сужается к лапе
            new Bone { name = "прямая_живота.м", socket = "хребет", parent = "грудина", attach = 0.12f, length = 1.242f, dir = new Vector3(-151.46f, 0f, -0.56f), r0 = 0.074f, section = 0.66f, mirrorX = true, layer = BodyLayer.Muscle },   // прямая живота: нижняя линия от груди к паху. Идёт к ЛОБКУ по средней линии, а не к суставу вбок, — иначе левая и правая половины не смыкаются и живота у зверя нет
            new Bone { name = "ягодичная.м", socket = "Ноги", parent = "крестец", attach = 0.42f, length = 0.503f, dir = new Vector3(-67.86f, 0f, -13.91f), r0 = 0.088f, section = 0.7f, mirrorX = true, layer = BodyLayer.Muscle },   // ягодичная: КРУП. Его округлость целиком её работа
            new Bone { name = "пяточный", socket = "Ноги", parent = "голень", length = 0.121f, dir = new Vector3(108.94f, 0f, 7.52f), r0 = 0.04f, r1 = 0.029f, section = 0.8f, mirrorX = true },   // пяточный бугор: острый угол скакательного — главный признак задней ноги в профиль
            new Bone { name = "плюсна", socket = "Ноги", parent = "голень", attach = 1f, length = 0.457f, dir = new Vector3(-24.67f, 0f, -3.33f), r0 = 0.039f, r1 = 0.035f, section = 1.3f, depth = 0.75f, mirrorX = true },
            new Bone { name = "сухожилия_п.м", socket = "Руки", parent = "пясть", attach = 0.02f, length = 0.436f, dir = new Vector3(-3.33f, 0f, 0.4f), r0 = 0.03f, depth = 1.35f, mirrorX = true, layer = BodyLayer.Muscle },   // сухожилия сгибателей пясти: ТОНКИЙ ТЯЖ ДО ПАЛЬЦЕВ. Без него нога ниже запястья остаётся голой костью, а у неё профиль `long` — диафиз поджат до 52%, и в обмере пясть выходила 1.6 см толщиной, тоньше собственной кости. Сечение глубокое, а не круглое: сзади тяж, спереди кость
            new Bone { name = "напрягатель.м", socket = "Ноги", parent = "подвздошная", attach = 0.22f, length = 0.86f, dir = new Vector3(-44.23f, 0f, 5.5f), r0 = 0.079f, section = 0.52f, mirrorX = true, layer = BodyLayer.Muscle },   // напрягатель широкой фасции: передний край бедра, треугольник перед коленом
            new Bone { name = "четырёхглавая.м", socket = "Ноги", parent = "подвздошная", attach = 0.62f, length = 0.722f, dir = new Vector3(-55.36f, 0f, 5.76f), r0 = 0.097f, section = 0.78f, mirrorX = true, layer = BodyLayer.Muscle },   // четырёхглавая: передняя масса бедра, выносит колено вперёд в силуэте
            new Bone { name = "двуглавая_бедра.м", socket = "Ноги", parent = "седалищная", attach = 0.18f, length = 0.746f, dir = new Vector3(-168.52f, 0f, 14.6f), r0 = 0.092f, section = 0.58f, mirrorX = true, layer = BodyLayer.Muscle },   // двуглавая бедра: ЗАДНЯЯ ЛИНИЯ ЗВЕРЯ от крупа до скакательного. Самая крупная мышца тела
            new Bone { name = "полусухожильная.м", socket = "Ноги", parent = "седалищная", attach = 0.5f, length = 0.825f, dir = new Vector3(-165.98f, 0f, 14.58f), r0 = 0.069f, section = 0.54f, mirrorX = true, layer = BodyLayer.Muscle },   // полусухожильная: за двуглавой, даёт «штаны» на бедре
            new Bone { name = "лапа_з", socket = "Ноги", parent = "плюсна", attach = 1f, length = 0.177f, dir = new Vector3(-46.53f, 0f, -5.25f), r0 = 0.036f, r1 = 0.039f, section = 1.4f, depth = 0.8f, mirrorX = true },
            new Bone { name = "икроножная.м", socket = "Ноги", parent = "бедро", attach = 0.88f, length = 0.602f, dir = new Vector3(61.32f, 0f, 13.6f), r0 = 0.083f, section = 0.85f, mirrorX = true, layer = BodyLayer.Muscle },   // икроножная: голень спереди мясистая, сзади тянется в пяточное сухожилие
            new Bone { name = "сгибатели_з.м", socket = "Ноги", parent = "голень", attach = 0.18f, length = 0.615f, dir = new Vector3(-5.33f, 0f, -0.74f), r0 = 0.055f, section = 0.85f, mirrorX = true, layer = BodyLayer.Muscle },   // сгибатели плюсны: остаток мяса на голяшке ниже колена
            new Bone { name = "сухожилия_з.м", socket = "Ноги", parent = "плюсна", attach = 0.02f, length = 0.492f, dir = new Vector3(-5.24f, 0f, -0.66f), r0 = 0.03f, depth = 1.35f, mirrorX = true, layer = BodyLayer.Muscle },   // сухожилия сгибателей плюсны: тот же тяж на задней ноге. У волка он идёт до пальцев, и именно им плюсна держит глубину при малой ширине — «сухая» голяшка канида
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
            new Organ { organName = "Хребет", slot = "хребет", chassisOnly = true, visualParts = new[] {
                new OrganPart { scale = new Vector3(0.66f, 0.74f, 0.42f), offset = new Vector3(0.00f, -0.10f, 0.32f), shape = PartShape.Sphere }, // плечи — перед сужен
                new OrganPart { scale = new Vector3(0.94f, 0.92f, 0.44f), offset = new Vector3(0.00f, 0.02f, -0.28f), shape = PartShape.Sphere }, // круп
                new OrganPart { scale = new Vector3(0.88f, 0.92f, 0.42f), offset = new Vector3(0.00f, -0.02f, 0.13f), shape = PartShape.Sphere }, // грудь
                new OrganPart { scale = new Vector3(1.00f, 1.00f, 0.46f), offset = new Vector3(0.00f, 0.02f, -0.09f), shape = PartShape.Sphere }, // середина — шире всех
                new OrganPart { scale = new Vector3(0.34f, 0.52f, 0.34f), offset = new Vector3(0.30f, -0.30f, -0.28f), shape = PartShape.Sphere }, // бедро (пр)
                new OrganPart { scale = new Vector3(0.34f, 0.52f, 0.34f), offset = new Vector3(-0.30f, -0.30f, -0.28f), shape = PartShape.Sphere }, // бедро (лев)
                new OrganPart { scale = new Vector3(0.30f, 0.46f, 0.30f), offset = new Vector3(0.28f, -0.28f, 0.26f), shape = PartShape.Sphere }, // плечо (пр)
                new OrganPart { scale = new Vector3(0.30f, 0.46f, 0.30f), offset = new Vector3(-0.28f, -0.28f, 0.26f), shape = PartShape.Sphere }, // плечо (лев)
            } }, // СКЕЛЕТ: несущая структура шасси. chassisOnly — её не крадут графтом, как «Тело-хвост»
            new Organ { organName = "Игломёт",           slot = "Игломёт", hotkey = "8", cost = 4, enablesQuillVolley = true, visualScale = new Vector3(1f, 1f, 1f), visualParts = new[] {
                // БАТАРЕЯ: длинные иглы ВПЕРЁД — куда смотрят стволы, туда и летит залп (читаемость
                // важнее биологии: игрок сразу видит, что тварь плюётся иглами). Лёгкий веер
                new OrganPart { scale = new Vector3(0.13f, 0.13f, 1.7f), offset = new Vector3(-0.42f, 0.12f, 0.55f), euler = new Vector3(-10f, -13.0f, 0f) },
                new OrganPart { scale = new Vector3(0.13f, 0.13f, 1.7f), offset = new Vector3(-0.14f, 0.12f, 0.55f), euler = new Vector3(-10f, -4.5f, 0f) },
                new OrganPart { scale = new Vector3(0.13f, 0.13f, 1.7f), offset = new Vector3(0.14f, 0.12f, 0.55f), euler = new Vector3(-10f, 4.5f, 0f) },
                new OrganPart { scale = new Vector3(0.13f, 0.13f, 1.7f), offset = new Vector3(0.42f, 0.12f, 0.55f), euler = new Vector3(-10f, 13.0f, 0f) },
            } }, // ФОРМА ЕЖИНАЯ (игольчатая плита вдоль хребта) — у органа; вертикальный хребет человека доворачивает МЕСТО // ПРИДАТОК: дальний бой игрока (химерный слот)
            new Organ { organName = "Иглы",              slot = "Шкура",  hotkey = "6", cost = 5, damageReduction = 0.2f, thorns = true, visualAlignToBody = true, visualParts = new[] {
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
            } }, // ОТВЕТКА: броня умеренная — иглы это ответ, а не панцирь
            new Organ { organName = "Ежиные ноги",       slot = "Ноги",   hotkey = "2", cost = 4, moveSpeed = 6f, dashSpeed = 18f, dashDuration = 0.14f, dashCooldown = 0.35f, enablesRoll = true, enablesCurl = true, nativeChassis = "Ёж", visualParts = new[] {
                new OrganPart { scale = new Vector3(0.95f, 0.44f, 0.95f), offset = new Vector3(0.00f, 0.27f, -0.06f), euler = new Vector3(-10f, 0f, 0f), shape = PartShape.Capsule }, // бедро
                new OrganPart { scale = new Vector3(0.78f, 0.46f, 0.82f), offset = new Vector3(0.00f, -0.13f, 0.06f), euler = new Vector3(12f, 0f, 0f), shape = PartShape.Capsule }, // голень
                new OrganPart { scale = new Vector3(1.06f, 0.64f, 1.06f), offset = new Vector3(0.00f, 0.08f, 0.00f), shape = PartShape.Sphere }, // колено
                new OrganPart { scale = new Vector3(0.80f, 0.18f, 1.55f), offset = new Vector3(0.00f, -0.41f, 0.22f) }, // стопа (ёж СТОПОХОДЯЩИЙ)
            } }, // ёж НЕ догоняла, а ПИННЕР: на Э 0.5 = 3.0 — медленнее уползающей змеи (3.75), сам не догонит. Ловит КИТОМ: залп замедляет → подошёл → схватил. ПЕРЕКАТ (enablesRoll): рывок «в клубке» режет иглами кого прокатил — третий профиль ног
            //     КЛУБОК ЗДЕСЬ ЖЕ, ЧЕРЕЗ nativeChassis. Перекат и клубок — одна способность на двух глубинах: рывок «в клубке» (кувырок с i-frames) и полный шар (броня + катание-таран).
            //     Раньше клубок жил отдельным органом «Игольчатое тело» на фиктивном месте `Тело` (hidden, без единой детали) — место существовало только чтобы флагу было куда сесть.
            //     Теперь: украл ежиные ноги на человечьем шасси → перекат есть, шара нет; ноги дома → раскрываются целиком. Локомоция и есть свойство шасси (тот же закон, что у chassisOnly)
            new Organ { organName = "Цепкая пасть",      slot = "Пасть",  hotkey = "5", cost = 4, damage = 22, enablesBite = true, enablesConstrict = true, constrictStage = 1, nativeChassis = "Ёж" }, // ДОБИВАНИЕ + ПИН пастью (ст.1): та же челюсть грабит и кусает прижатую добычу. 22 (≈11 на Э 0.5) даёт ежу грабнуть-и-добить
            new Organ { organName = "Ядоупорное сердце", slot = "Сердце", hotkey = "3", cost = 6, hpBonus = 1.2f, staminaBonus = 0.4f, staminaRegenBonus = 0.3f, regen = 0.5f, atkCooldown = 0.5f, venomResist = true }, // РЕЗИСТ ЯДА (медоед-конституция) — делает ежа контр-видом змеи
            new Organ { organName = "Пятак",             slot = "Чутьё",  hotkey = "4", cost = 3, dashCooldown = 0.5f, enablesScent = true, keenHearing = true, hearingMult = 1.6f, visualParts = new[] {
                new OrganPart { scale = new Vector3(1.00f, 1.00f, 1.00f), shape = PartShape.Sphere, role = PartRole.Nose }, // нос
                new OrganPart { scale = new Vector3(0.95f, 0.65f, 1.00f), offset = new Vector3(0.00f, -0.18f, 0.00f), role = PartRole.Ear, shape = PartShape.Sphere }, // раковина
                new OrganPart { scale = new Vector3(1.00f, 0.70f, 0.85f), offset = new Vector3(0.00f, 0.24f, -0.04f), role = PartRole.Ear, shape = PartShape.Sphere }, // верх
                new OrganPart { scale = new Vector3(1.00f, 1.00f, 1.00f), shape = PartShape.Sphere, role = PartRole.Eye, color = new Color(0.61f, 0.56f, 0.49f, 1f) }, // ЦВЕТ ГЛАЗА = КАНАЛ: нюх и слух: смесь поровну — ночной зверь
            } }, // НОЧНОЙ ЗВЕРЬ: подвижный нос и большие уши — нюх и слух остры (цена в зрении придёт со слайсом сенсорики)
        };
        // СОКЕТ-ПЛАН ежа (приземистый и широкий; иглы по хребту — главный силуэт). Числа из HedgehogPrefab.
        // «Руки» — обычное место (передние лапки рисуются), но органа Руки у ежа НЕТ → слота нет, только графтом
        hog.sockets = new[]
        {
            // ── ЕДИНЫЙ ПЛАН ТЕЛА (спека 2026-08-27): ось идёт ОТ ГОЛОВЫ назад, голова — корень графа.
            // Голова единственное место, которое есть у всех пяти видов: хребта нет у змеи, и корень
            // «хребет» универсальным быть не может по построению. Ёж — первый развёрнутый вид.
            //     ЧИСЛА ВЫВЕДЕНЫ ИЗ ЗАМЕРА, а не подобраны. Формула привязки, проверенная на двух
            // стыках: центр места = центр родителя + (attach−0.5)×длина_родителя_по_оси +
            // attachOffset × калибр родителя. Целевые центры взяты прежние — разворот не должен
            // двигать то, что уже стоит (инвариант И5)
            new BodySocket { name = "хребет", parent = "шея", attach = 0.000f, attachOffset = new Vector3(0.000f, 0.556f, -1.850f), baseSize = new Vector3(0.76f, 0.60f, 1.05f) }, // НЕСУЩИЙ ЦЕНТР: форму даёт орган «Хребет» (chassisOnly), поэтому место больше не служебное
            // ШЕЯ У ЕЖА ЕСТЬ. Её отсутствие было не анатомией, а недоделкой: ежом занимались мало,
            // и голова садилась прямо на хребет. У настоящего ежа шея короткая и утоплена в иглы,
            // но она есть — и без неё голова не может ни опускаться к земле (кормёжка), ни
            // прятаться при сворачивании.
            //     ДЛИНА 0.20 ПРИ ХРЕБТЕ 1.05 — 19%, против 29% у волка и 53% у человека: короткая,
            // как и положено. Но НЕ КОРОЧЕ: длинная ось места выбирается по максимальной стороне, и
            // при Z меньше 0.18 ось молча ушла бы в Y, развернув всю ветку головы вверх. Запас 10.8%
            //     СДВИГ НАЗАД (z −0.052) — чтобы голова осталась там же, где стояла: вставка звена не
            // должна удлинять зверя. Число не угадано, а выведено из замера карты: центр места =
            // конец родителя + attachOffset × калибр родителя. Конец хребта 0.475, нужен конец шеи
            // 0.520 (там стояла голова) → центр шеи 0.420 → (0.420−0.475)/1.05 = −0.052
            new BodySocket { name = "шея", parent = "голова", attach = 0.000f, attachOffset = new Vector3(0.000f, 0.000f, 0.187f), baseSize = new Vector3(0.170f, 0.180f, 0.200f), sizeRel = new Vector3(0.810f, 0.804f, 0.625f), parts = new[] { // ДОЛЯ ОТ ГОЛОВЫ (была от хребта): sizeRel считается от родителя, и при развороте её надо пересчитывать — иначе место схлопнется
                new OrganPart { scale = new Vector3(1.00f, 1.15f, 0.80f), offset = new Vector3(0.00f, 0.00f, 0.00f), euler = new Vector3(90f, 0f, 0f), shape = PartShape.Capsule }, // шея одной капсулой вдоль оси
                new OrganPart { scale = new Vector3(1.15f, 0.50f, 0.70f), offset = new Vector3(0.00f, 0.16f, -0.32f), shape = PartShape.Sphere }, // ЗАГРИВОК уходит под иглы: у ежа шея не читается снаружи, она скрыта покровом
            } }, // короткая шея: несёт голову, утоплена в иглы
            new BodySocket { name = "голова", localPos = new Vector3(0.000f, 0.400f, 0.520f), baseSize = new Vector3(0.210f, 0.224f, 0.320f), parts = new[] { // КОРЕНЬ ГРАФА: доли больше нет — у корня нет родителя, габарит читается из baseSize // ДОЛЯ ПЕРЕСЧИТАНА ПОД ШЕЮ (была 0.276/0.373/0.305 от хребта): sizeRel считается от РОДИТЕЛЯ, и при пересадке места на другого родителя старая доля схлопывает деталь — голова ужалась вчетверо вместе с пастью, носом, глазами и ушами
                new OrganPart { scale = new Vector3(1.00f, 1.00f, 1.00f), offset = new Vector3(0.00f, 0.00f, 0.00f), shape = PartShape.Sphere }, // череп
            } },
            new BodySocket { name = "нос", parent = "Пасть", attach = 0.500f, attachOffset = new Vector3(0.00f, -0.10f, 0.42f), sizeRel = new Vector3(0.24f, 0.22f, 0.18f), formFrom = "Чутьё", formRole = PartRole.Nose }, // АДРЕС НОСА: пятак рисует одноимённый орган, а не пасть
            new BodySocket { name = "Пасть", parent = "голова", attach = 1.000f, attachOffset = new Vector3(0.000f, -0.214f, 0.062f),  baseSize = new Vector3(0.220f, 0.200f, 0.280f), sizeRel = new Vector3(1.048f, 0.893f, 0.875f), parts = new[] {
                new OrganPart { scale = new Vector3(1.00f, 0.94f, 0.40f), offset = new Vector3(0.00f, 0.00f, -0.28f), shape = PartShape.Sphere }, // основание морды
                new OrganPart { scale = new Vector3(0.74f, 0.70f, 0.34f), offset = new Vector3(0.00f, -0.04f, -0.02f), shape = PartShape.Sphere }, // конус
                new OrganPart { scale = new Vector3(0.48f, 0.46f, 0.30f), offset = new Vector3(0.00f, -0.08f, 0.22f), shape = PartShape.Sphere }, // сужение
            } },
            new BodySocket { name = "глаза", parent = "голова", attach = 0.500f, attachOffset = new Vector3(0.400f, 0.100f, 0.010f), baseSize = new Vector3(0.027f, 0.027f, 0.027f), sizeRel = new Vector3(0.129f, 0.121f, 0.084f), mirrorX = true, formFrom = "Чутьё", formRole = PartRole.Eye, parts = new[] {
                new OrganPart { scale = new Vector3(1.00f, 1.00f, 1.00f), shape = PartShape.Sphere, role = PartRole.Eye, color = new Color(0.10f, 0.10f, 0.12f, 1f) }, // ФОЛБЭК: тварь без Чутья не слепа, но глаз тускл
            } }, // МЕСТО НА КОЖЕ головы (посчитано лучом из её центра) — форму и цвет даёт ЧУТЬЁ
            new BodySocket { name = "уши", parent = "голова", attach = 0.700f, attachOffset = new Vector3(0.350f, 0.571f, -0.325f),    baseSize = new Vector3(0.110f, 0.130f, 0.060f), sizeRel = new Vector3(0.524f, 0.580f, 0.188f), mirrorX = true, formFrom = "Чутьё", formRole = PartRole.Ear },   // АДРЕС УХА: раковину рисует орган слуха, а не шасси — привил чужое Чутьё, и ухо стало чужим

            new BodySocket { name = "Шкура",  parent = "хребет", attach = 0.500f,  baseSize = new Vector3(0.76f, 0.60f, 1.05f), sizeRel = new Vector3(1.000f, 1.000f, 1.000f), parts = new[] {
                new OrganPart { scale = new Vector3(0.96f, 0.96f, 0.86f), offset = new Vector3(0.00f, 0.02f, -0.04f), shape = PartShape.Sphere }, // корпус (единый объём)
            } },
            new BodySocket { name = "Руки", parent = "хребет", attach = 0.780f, attachOffset = new Vector3(0.316f, -0.617f, 0.072f),   baseSize = new Vector3(0.15f, 0.26f, 0.19f), sizeRel = new Vector3(0.197f, 0.433f, 0.181f), mirrorX = true, parts = new[] {
                new OrganPart { scale = new Vector3(0.92f, 0.42f, 0.92f), offset = new Vector3(0.00f, 0.27f, -0.04f), euler = new Vector3(-8f, 0f, 0f), shape = PartShape.Capsule }, // плечо
                new OrganPart { scale = new Vector3(0.76f, 0.44f, 0.80f), offset = new Vector3(0.00f, -0.14f, 0.06f), euler = new Vector3(10f, 0f, 0f), shape = PartShape.Capsule }, // предплечье
                new OrganPart { scale = new Vector3(1.02f, 0.60f, 1.02f), offset = new Vector3(0.00f, 0.08f, 0.01f), shape = PartShape.Sphere }, // локоть
                new OrganPart { scale = new Vector3(0.78f, 0.18f, 1.45f), offset = new Vector3(0.00f, -0.41f, 0.20f) }, // кисть (стопоходящий)
            } }, // передние лапки
            new BodySocket { name = "Ноги", parent = "хребет", attach = 0.220f, attachOffset = new Vector3(0.316f, -0.617f, 0.023f),   baseSize = new Vector3(0.15f, 0.26f, 0.19f), sizeRel = new Vector3(0.197f, 0.433f, 0.181f), mirrorX = true },
            new BodySocket { name = "Игломёт", parent = "хребет", attach = 0.550f, attachOffset = new Vector3(0.000f, 0.333f, 0.207f),baseSize = new Vector3(0.24f, 0.24f, 0.24f), sizeRel = new Vector3(0.316f, 0.400f, 0.229f), baseEuler = new Vector3(-10f, 0f, 0f) }, // СВОИ иглы: КАЛИБР (форма-плита у органа), хребет горизонтальный; ВЫШЕ корпуса (верх туши 0.81), иначе тонет в теле
            new BodySocket { name = "Сердце", inner = true, parent = "хребет", attach = 0.500f, attachOffset = new Vector3(0.000f, 0.080f, 0.000f), sizeRel = new Vector3(0.700f, 0.750f, 0.400f) },  // своей грудной клетки у ежа нет, но АДРЕС держим: чужое сердце (человечье/волчье/лосиное) со своей формой сядет в грудь, а не в начало координат
            new BodySocket { name = "Чутьё",  inner = true, parent = "голова", attach = 0.500f, sizeRel = new Vector3(0.500f, 0.500f, 0.500f) },  // ЧУВСТВА ЖИВУТ В ГОЛОВЕ. Своей формы у места нет, и деталь не родится сама собой — но АДРЕС нужен заранее: дашь органу форму (термо-ямки), и без родителя с калибром она сядет метровым кубом в начало координат. Доля от головы — одна на все виды
            new BodySocket { name = "Хвост", parent = "хребет", attach = 0.000f, attachOffset = new Vector3(0.000f, 0.100f, 0.030f),  baseSize = new Vector3(0.075f, 0.075f, 0.150f), sizeRel = new Vector3(0.099f, 0.125f, 0.143f), baseEuler = new Vector3(-40f, 0f, 0f), parts = new[] {
                new OrganPart { scale = new Vector3(1.35f, 1.35f, 0.55f), offset = new Vector3(0.00f, 0.00f, 0.32f), shape = PartShape.Sphere }, // ШАРНИР основания — единая схема хвоста
                new OrganPart { scale = new Vector3(1.00f, 1.00f, 0.60f), offset = new Vector3(0.00f, 0.00f, -0.10f) }, // сегмент
                new OrganPart { scale = new Vector3(0.72f, 0.72f, 0.40f), offset = new Vector3(0.00f, 0.00f, -0.60f) }, // кончик
            } }, // КАЛИБР
            new BodySocket { name = "Рога", parent = "голова", attach = 0.700f, attachOffset = new Vector3(0.233f, 0.429f, -0.263f),   baseSize = new Vector3(0.10f, 0.10f, 0.10f), sizeRel = new Vector3(0.476f, 0.446f, 0.312f), mirrorX = true, graft = true }, // КАЛИБР
        };

        hog.bones = new[]
        {
            // СКЕЛЕТ ВИДА «hedgehog» — ВЫГРУЖЕНО из Tools/Blender/species/hedgehog.py (tools/bones_to_cs.py).
            // Правится ТАМ, по анатомическим точкам; здесь только результат. Костей: 130, калибр (холка) 0.320 м
            new Bone { name = "грудина", socket = "Сердце", origin = new Vector3(0f, 0.184f, -0.065f), length = 0.109f, dir = new Vector3(79.93f, 0f, 0f), r0 = 0.005f, r1 = 0.006f, section = 0.85f, depth = 1.1f, chain = 3 },   // грудина держит НИЗ груди. Без неё дуги висят концами в воздухе
            new Bone { name = "лёгкие", socket = "Сердце", origin = new Vector3(0f, 0.237f, -0.088f), length = 0.118f, dir = new Vector3(88.05f, 0f, 0f), r0 = 0.031f, r1 = 0.028f, depth = 1.48f, layer = BodyLayer.Muscle },   // содержимое грудной полости: лёгкие и сердце. Поперечник изнутри рёбер (дуга 0.115 холки), высота от позвонка до грудины
            new Bone { name = "брюшина", socket = "хребет", origin = new Vector3(0f, 0.237f, -0.088f), length = 0.142f, dir = new Vector3(-83.33f, 0f, 0f), r0 = 0.028f, r1 = 0.022f, depth = 1.3f, layer = BodyLayer.Muscle },   // содержимое брюшной полости. Уже грудной и сходит на конус к тазу — отсюда подобранная талия, которую иначе рисовать нечем
            new Bone { name = "череп", socket = "голова", origin = new Vector3(0f, 0.362f, 0.131f), length = 0.063f, dir = new Vector3(125.39f, 0f, 0f), r0 = 0.03f },   // мозговой череп ОБОЛОЧКОЙ по 49 сечениям, снятым с пластины
            new Bone { name = "скула", socket = "голова", origin = new Vector3(0.058f, 0.345f, 0.141f), length = 0.042f, dir = new Vector3(129.09f, 0f, -73.44f), r0 = 0.004f, r1 = 0.003f, section = 0.75f, depth = 1.35f, mirrorX = true },   // задняя ветвь скуловой дуги: от височной кости наружу-вперёд
            new Bone { name = "глазница.р", socket = "голова", origin = new Vector3(0.125f, 0.341f, 0.163f), length = 0.097f, dir = new Vector3(-105f, 0f, 87.91f), r0 = 0.01f, r1 = 0.003f, depth = 1.1f, mirrorX = true },   // глазница: конус, сужающийся внутрь черепа. Смотрит вперёд-вбок, как у хищника
            new Bone { name = "ухо.п", socket = "Чутьё", origin = new Vector3(0.05f, 0.354f, 0.152f), length = 0.058f, dir = new Vector3(8.48f, 0f, 33.67f), r0 = 0.015f, r1 = 0.004f, depth = 0.52f, mirrorX = true, layer = BodyLayer.Feature },   // ухо: широкое у основания, к кончику сходит на нет, поперёк ПЛОСКОЕ. Стоячее ухо — половина того, чем волк опознаётся издали
            new Bone { name = "затылочный", socket = "голова", parent = "череп", attach = 0f, length = 0.014f, dir = new Vector3(64.57f, 0f, 0f), r0 = 0.008f, r1 = 0.006f },   // затылочная кость: несёт свод от сустава с атлантом. Внутри оболочки, снаружи не видна
            new Bone { name = "скула_п", socket = "голова", parent = "скула", attach = 1.001f, length = 0.039f, dir = new Vector3(-167.4f, 0f, 37.98f), r0 = 0.003f, r1 = 0.003f, section = 0.75f, depth = 1.25f, mirrorX = true },   // передняя ветвь дуги: сходится к верхнечелюстной под глазницей
            new Bone { name = "нч_подвес", socket = "Пасть", parent = "череп", attach = 0.241f, length = 0.053f, dir = new Vector3(84.61f, 0f, -78.76f), r0 = 0.003f, mirrorX = true },   // вынос сустава от оси к нижнечелюстной ямке. Кость-связка: начало ребёнка всегда лежит на родителе, поэтому боковую посадку приходится проходить отдельным звеном
            new Bone { name = "клык_в", socket = "Пасть", parent = "череп", attach = 0.905f, length = 0.025f, dir = new Vector3(46.48f, 0f, -22.99f), r0 = 0.004f, r1 = 0.001f, section = 0.85f, depth = 0.85f, mirrorX = true, layer = BodyLayer.Feature },   // верхний клык FEATURE: вниз и чуть вперёд (на черепе)
            new Bone { name = "шея_в", socket = "шея", parent = "затылочный", attach = 0.998f, length = 0.032f, dir = new Vector3(31.23f, 0f, 0f), r0 = 0.007f, r1 = 0.008f, section = 1.12f, chain = 2 },   // атлант и эпистрофей: несут голову, самое подвижное звено
            new Bone { name = "ветвь", socket = "Пасть", parent = "нч_подвес", attach = 1f, length = 0.007f, dir = new Vector3(101.09f, 0f, 62.08f), r0 = 0.01f, r1 = 0.007f, section = 0.34f, depth = 1.1f, mirrorX = true },   // ветвь челюсти: от сустава вниз-назад к угловому отростку
            new Bone { name = "венечный", socket = "Пасть", parent = "нч_подвес", attach = 1f, length = 0.014f, dir = new Vector3(-151.72f, 0f, -38.02f), r0 = 0.008f, r1 = 0.003f, section = 0.22f, depth = 1.25f, mirrorX = true },   // венечный отросток: пластина ВНУТРИ скуловой дуги. К ней крепится височная мышца — она и даёт волку силу укуса
            new Bone { name = "шея_3", socket = "шея", parent = "шея_в", attach = 1f, length = 0.034f, dir = new Vector3(8.57f, 0f, 0f), r0 = 0.008f, r1 = 0.009f, section = 1.18f, chain = 2 },   // C3–C2: здесь шея начинает задираться к голове
            new Bone { name = "челюсть", socket = "Пасть", parent = "ветвь", attach = 1.006f, length = 0.057f, dir = new Vector3(110.45f, 0f, 44.02f), r0 = 0.007f, r1 = 0.004f, section = 0.5f, depth = 1.15f, mirrorX = true },   // тело челюсти с зубным рядом: узкое вбок, высокое в профиль
            new Bone { name = "грудиночелюстная.м", socket = "шея", parent = "грудина", attach = 0.92f, length = 0.181f, dir = new Vector3(-44f, 0f, -16.34f), r0 = 0.008f, section = 0.65f, mirrorX = true, layer = BodyLayer.Muscle },   // грудиночелюстная: линия горла. Она отделяет шею от груди
            new Bone { name = "жевательная.м", socket = "Пасть", parent = "скула", attach = 0.55f, length = 0.031f, dir = new Vector3(168.24f, 0f, -0.63f), r0 = 0.009f, section = 0.8f, mirrorX = true, layer = BodyLayer.Muscle },   // жевательная: щека. Заполняет угол между дугой и челюстью
            new Bone { name = "височная.м", socket = "голова", parent = "череп", attach = 0.3f, length = 0.045f, dir = new Vector3(48.02f, 0f, -88.34f), r0 = 0.011f, section = 0.7f, depth = 1.1f, mirrorX = true, layer = BodyLayer.Muscle },   // височная: заполняет височную яму под дугой. Ею голова хищника шире в скулах, чем в своде
            new Bone { name = "шея_2", socket = "шея", parent = "шея_3", attach = 0.999f, length = 0.034f, dir = new Vector3(17.49f, 0f, 0f), r0 = 0.009f, r1 = 0.01f, section = 1.22f, chain = 2 },   // C5–C4: самое глубокое место шеи
            new Bone { name = "клык_н", socket = "Пасть", parent = "челюсть", attach = 0.92f, length = 0.015f, dir = new Vector3(127.77f, 0f, -18.86f), r0 = 0.003f, r1 = 0.001f, section = 0.85f, depth = 0.85f, mirrorX = true, layer = BodyLayer.Feature },   // нижний клык FEATURE: ВВЕРХ, встаёт перед верхним (на челюсти, открывается)
            new Bone { name = "шея", socket = "шея", parent = "шея_2", attach = 1f, length = 0.036f, dir = new Vector3(8.33f, 0f, 0f), r0 = 0.01f, r1 = 0.011f, section = 1.25f, chain = 2 },   // C7–C6: выходит из холки ПОЛОГО, почти горизонтально
            new Bone { name = "холка", socket = "хребет", parent = "шея", attach = 1.001f, length = 0.047f, dir = new Vector3(20.49f, 0f, 0f), r0 = 0.006f, r1 = 0.006f, section = 1.3f, chain = 3 },   // передний грудной отдел. Отдельной костью потому, что ХОЛКА — это его остистые отростки, и править её высоту надо, не трогая длину всей грудной клетки
            new Bone { name = "грудной", socket = "хребет", parent = "холка", attach = 0.999f, length = 0.081f, dir = new Vector3(-1.83f, 0f, 0f), r0 = 0.006f, r1 = 0.007f, section = 1.35f, chain = 4 },   // задний грудной отдел: несёт восемь каудальных рёбер
            new Bone { name = "остистый1", socket = "хребет", parent = "холка", attach = 0.054f, length = 0.036f, dir = new Vector3(65.93f, 0f, 0f), r0 = 0.005f, r1 = 0.003f, section = 0.42f, depth = 0.95f },
            new Bone { name = "остистый2", socket = "хребет", parent = "холка", attach = 0.381f, length = 0.043f, dir = new Vector3(57.93f, 0f, 0f), r0 = 0.005f, r1 = 0.003f, section = 0.42f, depth = 0.95f },
            new Bone { name = "остистый3", socket = "хребет", parent = "холка", attach = 0.763f, length = 0.042f, dir = new Vector3(55.93f, 0f, 0f), r0 = 0.005f, r1 = 0.003f, section = 0.42f, depth = 0.95f },
            new Bone { name = "ребро1", socket = "Сердце", parent = "холка", attach = 0.054f, length = 0.14f, dir = new Vector3(90.68f, 0f, -6.05f), r0 = 0.003f, r1 = 0.003f, section = 1.6f, depth = 0.55f, mirrorX = true },   // головка и шейка ребра: вбок круче всего
            new Bone { name = "ребро2", socket = "Сердце", parent = "холка", attach = 0.233f, length = 0.134f, dir = new Vector3(89.4f, 0f, -7.65f), r0 = 0.003f, r1 = 0.003f, section = 1.6f, depth = 0.55f, mirrorX = true },   // головка и шейка ребра: вбок круче всего
            new Bone { name = "ребро3", socket = "Сердце", parent = "холка", attach = 0.416f, length = 0.129f, dir = new Vector3(87.96f, 0f, -9.3f), r0 = 0.003f, r1 = 0.003f, section = 1.6f, depth = 0.55f, mirrorX = true },   // головка и шейка ребра: вбок круче всего
            new Bone { name = "ребро4", socket = "Сердце", parent = "холка", attach = 0.624f, length = 0.125f, dir = new Vector3(86.26f, 0f, -10.64f), r0 = 0.003f, r1 = 0.003f, section = 1.6f, depth = 0.55f, mirrorX = true },   // головка и шейка ребра: вбок круче всего
            new Bone { name = "ребро5", socket = "Сердце", parent = "холка", attach = 0.842f, length = 0.122f, dir = new Vector3(84.43f, 0f, -11.87f), r0 = 0.003f, r1 = 0.003f, section = 1.6f, depth = 0.55f, mirrorX = true },   // головка и шейка ребра: вбок круче всего
            new Bone { name = "пластыревидная.м", socket = "шея", parent = "холка", attach = 0.25f, length = 0.145f, dir = new Vector3(144.24f, 0f, 0f), r0 = 0.009f, section = 0.75f, mirrorX = true, layer = BodyLayer.Muscle },   // пластыревидная: несёт голову, наполняет загривок за черепом
            new Bone { name = "поясница", socket = "хребет", parent = "грудной", attach = 1f, length = 0.097f, dir = new Vector3(-8.96f, 0f, 0f), r0 = 0.007f, r1 = 0.008f, section = 1.45f, chain = 4 },   // поясничный отдел дугой вверх: от него подобранность талии
            new Bone { name = "остистый4", socket = "хребет", parent = "грудной", attach = 0.115f, length = 0.038f, dir = new Vector3(59.76f, 0f, 0f), r0 = 0.005f, r1 = 0.003f, section = 0.42f, depth = 0.95f },
            new Bone { name = "остистый5", socket = "хребет", parent = "грудной", attach = 0.4f, length = 0.031f, dir = new Vector3(63.76f, 0f, 0f), r0 = 0.005f, r1 = 0.003f, section = 0.42f, depth = 0.95f },
            new Bone { name = "остистый6", socket = "хребет", parent = "грудной", attach = 0.716f, length = 0.025f, dir = new Vector3(69.76f, 0f, 0f), r0 = 0.005f, r1 = 0.003f, section = 0.42f, depth = 0.95f },
            new Bone { name = "остистый7", socket = "хребет", parent = "грудной", attach = 0.937f, length = 0.021f, dir = new Vector3(77.76f, 0f, 0f), r0 = 0.005f, r1 = 0.003f, section = 0.42f, depth = 0.95f },
            new Bone { name = "ребро1с", socket = "Сердце", parent = "ребро1", length = 0.193f, dir = new Vector3(9.71f, 0f, 2.37f), r0 = 0.003f, r1 = 0.003f, section = 1.6f, depth = 0.55f, mirrorX = true },   // тело ребра: здесь бок шире всего
            new Bone { name = "ребро2с", socket = "Сердце", parent = "ребро2", length = 0.182f, dir = new Vector3(7.98f, 0f, 2.97f), r0 = 0.003f, r1 = 0.003f, section = 1.6f, depth = 0.55f, mirrorX = true },   // тело ребра: здесь бок шире всего
            new Bone { name = "ребро3с", socket = "Сердце", parent = "ребро3", length = 0.172f, dir = new Vector3(5.95f, 0f, 3.59f), r0 = 0.003f, r1 = 0.003f, section = 1.6f, depth = 0.55f, mirrorX = true },   // тело ребра: здесь бок шире всего
            new Bone { name = "ребро4с", socket = "Сердце", parent = "ребро4", length = 0.166f, dir = new Vector3(3.47f, 0f, 4.07f), r0 = 0.003f, r1 = 0.003f, section = 1.6f, depth = 0.55f, mirrorX = true },   // тело ребра: здесь бок шире всего
            new Bone { name = "ребро5с", socket = "Сердце", parent = "ребро5", length = 0.16f, dir = new Vector3(0.74f, 0f, 4.52f), r0 = 0.003f, r1 = 0.003f, section = 1.6f, depth = 0.55f, mirrorX = true },   // тело ребра: здесь бок шире всего
            new Bone { name = "ребро6", socket = "Сердце", parent = "грудной", attach = 0.046f, length = 0.119f, dir = new Vector3(84.29f, 0f, -12.8f), r0 = 0.003f, r1 = 0.003f, section = 1.6f, depth = 0.55f, mirrorX = true },   // головка и шейка ребра: вбок круче всего
            new Bone { name = "ребро7", socket = "Сердце", parent = "грудной", attach = 0.184f, length = 0.118f, dir = new Vector3(82.32f, 0f, -13.31f), r0 = 0.003f, r1 = 0.003f, section = 1.6f, depth = 0.55f, mirrorX = true },   // головка и шейка ребра: вбок круче всего
            new Bone { name = "ребро8", socket = "Сердце", parent = "грудной", attach = 0.322f, length = 0.118f, dir = new Vector3(80.42f, 0f, -13.37f), r0 = 0.003f, r1 = 0.003f, section = 1.6f, depth = 0.55f, mirrorX = true },   // головка и шейка ребра: вбок круче всего
            new Bone { name = "ребро9", socket = "Сердце", parent = "грудной", attach = 0.46f, length = 0.119f, dir = new Vector3(78.65f, 0f, -12.92f), r0 = 0.003f, r1 = 0.003f, section = 1.6f, depth = 0.55f, mirrorX = true },   // головка и шейка ребра: вбок круче всего
            new Bone { name = "ребро10", socket = "Сердце", parent = "грудной", attach = 0.598f, length = 0.122f, dir = new Vector3(77.02f, 0f, -12.19f), r0 = 0.003f, r1 = 0.003f, section = 1.6f, depth = 0.55f, mirrorX = true },   // головка и шейка ребра: вбок круче всего
            new Bone { name = "ребро11", socket = "Сердце", parent = "грудной", attach = 0.736f, length = 0.129f, dir = new Vector3(75.93f, 0f, -10.7f), r0 = 0.003f, r1 = 0.003f, section = 1.6f, depth = 0.55f, mirrorX = true },   // головка и шейка ребра: вбок круче всего
            new Bone { name = "ребро12", socket = "Сердце", parent = "грудной", attach = 0.874f, length = 0.136f, dir = new Vector3(75.04f, 0f, -9.23f), r0 = 0.003f, r1 = 0.003f, section = 1.6f, depth = 0.55f, mirrorX = true },   // головка и шейка ребра: вбок круче всего
            new Bone { name = "лопатка", socket = "Руки", parent = "остистый2", attach = 0.411f, length = 0.18f, dir = new Vector3(-172.25f, 0f, -8.95f), r0 = 0.013f, r1 = 0.004f, section = 0.3f, depth = 1.3f, mirrorX = true },   // плоская лопасть, лежащая НА рёбрах: даёт покатое плечо и переход холки в ногу
            new Bone { name = "выйная.м", socket = "шея", parent = "остистый1", attach = 0.85f, length = 0.137f, dir = new Vector3(86.01f, 0f, 0f), r0 = 0.008f, section = 0.68f, layer = BodyLayer.Muscle },   // выйная связка с пластыревидной: ГРЕБЕНЬ шеи. Без неё шея проваливается к позвонкам
            new Bone { name = "крестец", socket = "хребет", parent = "поясница", attach = 1f, length = 0.033f, dir = new Vector3(-0.47f, 0f, 0f), r0 = 0.009f, r1 = 0.01f, section = 1.55f },   // на крестце сходятся таз, хвост и поясница — корень всего графа
            new Bone { name = "ост_пояс1", socket = "хребет", parent = "поясница", attach = 0.84f, length = 0.019f, dir = new Vector3(108.72f, 0f, 0f), r0 = 0.005f, r1 = 0.003f, section = 0.42f, depth = 0.95f },
            new Bone { name = "ост_пояс2", socket = "хребет", parent = "поясница", attach = 0.54f, length = 0.02f, dir = new Vector3(104.72f, 0f, 0f), r0 = 0.005f, r1 = 0.003f, section = 0.42f, depth = 0.95f },
            new Bone { name = "ост_пояс3", socket = "хребет", parent = "поясница", attach = 0.26f, length = 0.019f, dir = new Vector3(100.72f, 0f, 0f), r0 = 0.005f, r1 = 0.003f, section = 0.42f, depth = 0.95f },
            new Bone { name = "ост_пояс4", socket = "хребет", parent = "поясница", attach = 0.05f, length = 0.018f, dir = new Vector3(96.72f, 0f, 0f), r0 = 0.005f, r1 = 0.003f, section = 0.42f, depth = 0.95f },
            new Bone { name = "попереч1", socket = "хребет", parent = "поясница", attach = 0.8f, length = 0.021f, dir = new Vector3(-107.08f, 0f, -59.93f), r0 = 0.004f, r1 = 0.003f, section = 0.9f, depth = 0.55f, mirrorX = true },
            new Bone { name = "попереч2", socket = "хребет", parent = "поясница", attach = 0.52f, length = 0.022f, dir = new Vector3(-107.08f, 0f, -61.46f), r0 = 0.004f, r1 = 0.003f, section = 0.9f, depth = 0.55f, mirrorX = true },
            new Bone { name = "попереч3", socket = "хребет", parent = "поясница", attach = 0.24f, length = 0.021f, dir = new Vector3(-107.08f, 0f, -59.93f), r0 = 0.004f, r1 = 0.003f, section = 0.9f, depth = 0.55f, mirrorX = true },
            new Bone { name = "ребро1н", socket = "Сердце", parent = "ребро1с", length = 0.153f, dir = new Vector3(8.9f, 0f, 2.23f), r0 = 0.003f, r1 = 0.002f, section = 1.3f, depth = 0.6f, mirrorX = true },   // рёберный хрящ: заворачивает к грудине
            new Bone { name = "ребро2н", socket = "Сердце", parent = "ребро2с", length = 0.142f, dir = new Vector3(7.59f, 0f, 4.11f), r0 = 0.003f, r1 = 0.002f, section = 1.3f, depth = 0.6f, mirrorX = true },   // рёберный хрящ: заворачивает к грудине
            new Bone { name = "ребро3н", socket = "Сердце", parent = "ребро3с", length = 0.132f, dir = new Vector3(5.89f, 0f, 6.16f), r0 = 0.003f, r1 = 0.002f, section = 1.3f, depth = 0.6f, mirrorX = true },   // рёберный хрящ: заворачивает к грудине
            new Bone { name = "ребро4н", socket = "Сердце", parent = "ребро4с", length = 0.124f, dir = new Vector3(3.56f, 0f, 8.09f), r0 = 0.003f, r1 = 0.002f, section = 1.3f, depth = 0.6f, mirrorX = true },   // рёберный хрящ: заворачивает к грудине
            new Bone { name = "ребро5н", socket = "Сердце", parent = "ребро5с", length = 0.119f, dir = new Vector3(0.79f, 0f, 9.68f), r0 = 0.003f, r1 = 0.002f, section = 1.3f, depth = 0.6f, mirrorX = true },   // рёберный хрящ: заворачивает к грудине
            new Bone { name = "ребро6с", socket = "Сердце", parent = "ребро6", length = 0.157f, dir = new Vector3(-2.19f, 0f, 4.87f), r0 = 0.003f, r1 = 0.003f, section = 1.6f, depth = 0.55f, mirrorX = true },   // тело ребра: здесь бок шире всего
            new Bone { name = "ребро7с", socket = "Сердце", parent = "ребро7", length = 0.156f, dir = new Vector3(-5.07f, 0f, 5.07f), r0 = 0.003f, r1 = 0.003f, section = 1.6f, depth = 0.55f, mirrorX = true },   // тело ребра: здесь бок шире всего
            new Bone { name = "ребро8с", socket = "Сердце", parent = "ребро8", length = 0.158f, dir = new Vector3(-7.75f, 0f, 5.13f), r0 = 0.003f, r1 = 0.003f, section = 1.6f, depth = 0.55f, mirrorX = true },   // тело ребра: здесь бок шире всего
            new Bone { name = "ребро9с", socket = "Сердце", parent = "ребро9", length = 0.163f, dir = new Vector3(-10.15f, 0f, 5f), r0 = 0.003f, r1 = 0.003f, section = 1.6f, depth = 0.55f, mirrorX = true },   // тело ребра: здесь бок шире всего
            new Bone { name = "ребро10с", socket = "Сердце", parent = "ребро10", length = 0.17f, dir = new Vector3(-12.23f, 0f, 4.78f), r0 = 0.003f, r1 = 0.003f, section = 1.6f, depth = 0.55f, mirrorX = true },   // тело ребра: здесь бок шире всего
            new Bone { name = "ребро11с", socket = "Сердце", parent = "ребро11", length = 0.182f, dir = new Vector3(-13.55f, 0f, 4.24f), r0 = 0.003f, r1 = 0.003f, section = 1.6f, depth = 0.55f, mirrorX = true },   // тело ребра: здесь бок шире всего
            new Bone { name = "ребро12с", socket = "Сердце", parent = "ребро12", length = 0.196f, dir = new Vector3(-14.58f, 0f, 3.69f), r0 = 0.003f, r1 = 0.003f, section = 1.6f, depth = 0.55f, mirrorX = true },   // тело ребра: здесь бок шире всего
            new Bone { name = "плечо", socket = "Руки", parent = "лопатка", attach = 1f, length = 0.053f, dir = new Vector3(69.13f, 0f, 5.37f), r0 = 0.007f, r1 = 0.006f, section = 0.85f, mirrorX = true },
            new Bone { name = "зубчатая.м", socket = "Сердце", parent = "ребро4с", attach = 0.45f, length = 0.209f, dir = new Vector3(174.35f, 0f, 0.95f), r0 = 0.015f, section = 0.6f, depth = 1.1f, mirrorX = true, layer = BodyLayer.Muscle },   // зубчатая вентральная: ПОДВЕС корпуса между лопатками. Ею тело буквально висит на ногах
            new Bone { name = "трапециевидная.м", socket = "хребет", parent = "остистый3", attach = 0.7f, length = 0.084f, dir = new Vector3(176.55f, 0f, -6.69f), r0 = 0.011f, section = 0.55f, mirrorX = true, layer = BodyLayer.Muscle },   // трапециевидная: покатое плечо, переход холки в лопатку
            new Bone { name = "ромбовидная.м", socket = "хребет", parent = "остистый1", attach = 0.55f, length = 0.022f, dir = new Vector3(-129.72f, 0f, -7.44f), r0 = 0.01f, section = 0.55f, mirrorX = true, layer = BodyLayer.Muscle },   // ромбовидная: прижимает верх лопатки к холке, наполняет загривок
            new Bone { name = "межрёберная1.м", socket = "Сердце", parent = "ребро1с", attach = 0.45f, length = 0.02f, dir = new Vector3(-132.55f, 0f, -13.72f), r0 = 0.01f, section = 0.4f, mirrorX = true, layer = BodyLayer.Muscle },   // межрёберная: заполняет промежуток между соседними дугами
            new Bone { name = "межрёберная2.м", socket = "Сердце", parent = "ребро2с", attach = 0.45f, length = 0.02f, dir = new Vector3(-126.97f, 0f, -13.41f), r0 = 0.01f, section = 0.4f, mirrorX = true, layer = BodyLayer.Muscle },   // межрёберная: заполняет промежуток между соседними дугами
            new Bone { name = "межрёберная3.м", socket = "Сердце", parent = "ребро3с", attach = 0.45f, length = 0.021f, dir = new Vector3(-113.7f, 0f, -10.29f), r0 = 0.01f, section = 0.4f, mirrorX = true, layer = BodyLayer.Muscle },   // межрёберная: заполняет промежуток между соседними дугами
            new Bone { name = "межрёберная4.м", socket = "Сердце", parent = "ребро4с", attach = 0.45f, length = 0.021f, dir = new Vector3(-107.7f, 0f, -8.88f), r0 = 0.01f, section = 0.4f, mirrorX = true, layer = BodyLayer.Muscle },   // межрёберная: заполняет промежуток между соседними дугами
            new Bone { name = "ребро6н", socket = "Сердце", parent = "ребро6с", length = 0.117f, dir = new Vector3(-2.31f, 0f, 10.44f), r0 = 0.003f, r1 = 0.002f, section = 1.3f, depth = 0.6f, mirrorX = true },   // рёберный хрящ: заворачивает к грудине
            new Bone { name = "ребро7н", socket = "Сердце", parent = "ребро7с", length = 0.118f, dir = new Vector3(-5.24f, 0f, 9.88f), r0 = 0.003f, r1 = 0.002f, section = 1.3f, depth = 0.6f, mirrorX = true },   // рёберный хрящ: заворачивает к грудине
            new Bone { name = "ребро8н", socket = "Сердце", parent = "ребро8с", length = 0.122f, dir = new Vector3(-7.68f, 0f, 8.09f), r0 = 0.003f, r1 = 0.002f, section = 1.3f, depth = 0.6f, mirrorX = true },   // рёберный хрящ: заворачивает к грудине
            new Bone { name = "ребро9н", socket = "Сердце", parent = "ребро9с", length = 0.129f, dir = new Vector3(-9.45f, 0f, 5.34f), r0 = 0.003f, r1 = 0.002f, section = 1.3f, depth = 0.6f, mirrorX = true },   // рёберный хрящ: заворачивает к грудине
            new Bone { name = "ребро10н", socket = "Сердце", parent = "ребро10с", length = 0.139f, dir = new Vector3(-10.66f, 0f, 2.31f), r0 = 0.003f, r1 = 0.002f, section = 1.3f, depth = 0.6f, mirrorX = true },   // рёберный хрящ: заворачивает к грудине
            new Bone { name = "ребро11н", socket = "Сердце", parent = "ребро11с", length = 0.153f, dir = new Vector3(-11.16f, 0f, -1.6f), r0 = 0.003f, r1 = 0.002f, section = 1.3f, depth = 0.6f, mirrorX = true },   // рёберный хрящ: заворачивает к грудине
            new Bone { name = "ребро12н", socket = "Сердце", parent = "ребро12с", length = 0.168f, dir = new Vector3(-11.49f, 0f, -5.03f), r0 = 0.003f, r1 = 0.002f, section = 1.3f, depth = 0.6f, mirrorX = true },   // рёберный хрящ: заворачивает к грудине
            new Bone { name = "предплечье", socket = "Руки", parent = "плечо", attach = 1f, length = 0.052f, dir = new Vector3(-80.26f, 0f, 7.89f), r0 = 0.006f, r1 = 0.005f, section = 0.8f, depth = 1.15f, mirrorX = true },   // луч и локтевая вместе: спереди узкое, в профиль широкое
            new Bone { name = "локтевой_отр", socket = "Руки", parent = "плечо", length = 0.016f, dir = new Vector3(71.24f, 0f, -8.61f), r0 = 0.005f, r1 = 0.004f, section = 0.8f, mirrorX = true },   // локтевой отросток: острый угол локтя сзади, читается в профиль
            new Bone { name = "подвздошная", socket = "Ноги", parent = "крестец", attach = 0.301f, length = 0.065f, dir = new Vector3(-43.89f, 0f, -22.73f), r0 = 0.013f, r1 = 0.008f, section = 0.5f, depth = 1.25f, mirrorX = true },   // крыло подвздошной несёт КРУП: его наклон и есть линия зада
            new Bone { name = "хвост1", socket = "Хвост", parent = "крестец", attach = 1.001f, length = 0.061f, dir = new Vector3(-51.12f, 0f, 0f), r0 = 0.011f, r1 = 0.01f, section = 0.95f, chain = 2 },
            new Bone { name = "плечеголовная.м", socket = "шея", parent = "шея_3", attach = 0.65f, length = 0.22f, dir = new Vector3(-29.78f, 0f, -6.94f), r0 = 0.011f, section = 0.7f, mirrorX = true, layer = BodyLayer.Muscle },   // плечеголовная: передняя линия шеи от головы к плечу — самый заметный тяж на шее зверя
            new Bone { name = "широчайшая.м", socket = "хребет", parent = "поясница", attach = 0.28f, length = 0.244f, dir = new Vector3(-130.13f, 0f, -6.43f), r0 = 0.016f, section = 0.55f, mirrorX = true, layer = BodyLayer.Muscle },   // широчайшая: косой парус от поясницы к плечу — задняя граница лопатки в силуэте
            new Bone { name = "грудная.м", socket = "Сердце", parent = "грудина", attach = 0.55f, length = 0.094f, dir = new Vector3(81.67f, 0f, -16.64f), r0 = 0.012f, section = 0.75f, mirrorX = true, layer = BodyLayer.Muscle },   // грудная: преднагрудье между передними ногами, ширина груди спереди
            new Bone { name = "дельтовидная.м", socket = "Руки", parent = "лопатка", attach = 0.72f, length = 0.065f, dir = new Vector3(22.51f, 0f, 2.21f), r0 = 0.008f, section = 0.8f, mirrorX = true, layer = BodyLayer.Muscle },   // дельтовидная: округлость плечевого сустава
            new Bone { name = "длиннейшая.м", socket = "хребет", parent = "ост_пояс1", attach = 0.45f, length = 0.18f, dir = new Vector3(73.21f, 0f, 0f), r0 = 0.017f, section = 0.85f, mirrorX = true, layer = BodyLayer.Muscle },   // длиннейшая спины: валик вдоль позвоночника, ровная линия верха между холкой и крупом
            new Bone { name = "межрёберная5.м", socket = "Сердце", parent = "ребро5с", attach = 0.45f, length = 0.022f, dir = new Vector3(-100.07f, 0f, -6.18f), r0 = 0.01f, section = 0.4f, mirrorX = true, layer = BodyLayer.Muscle },   // межрёберная: заполняет промежуток между соседними дугами
            new Bone { name = "межрёберная6.м", socket = "Сердце", parent = "ребро6с", attach = 0.45f, length = 0.021f, dir = new Vector3(-91.33f, 0f, -3.39f), r0 = 0.01f, section = 0.4f, mirrorX = true, layer = BodyLayer.Muscle },   // межрёберная: заполняет промежуток между соседними дугами
            new Bone { name = "межрёберная7.м", socket = "Сердце", parent = "ребро7с", attach = 0.45f, length = 0.021f, dir = new Vector3(-81.99f, 0f, -0.19f), r0 = 0.01f, section = 0.4f, mirrorX = true, layer = BodyLayer.Muscle },   // межрёберная: заполняет промежуток между соседними дугами
            new Bone { name = "межрёберная8.м", socket = "Сердце", parent = "ребро8с", attach = 0.45f, length = 0.021f, dir = new Vector3(-68.17f, 0f, 3.24f), r0 = 0.01f, section = 0.4f, mirrorX = true, layer = BodyLayer.Muscle },   // межрёберная: заполняет промежуток между соседними дугами
            new Bone { name = "межрёберная9.м", socket = "Сердце", parent = "ребро9с", attach = 0.45f, length = 0.021f, dir = new Vector3(-59.53f, 0f, 5.26f), r0 = 0.01f, section = 0.4f, mirrorX = true, layer = BodyLayer.Muscle },   // межрёберная: заполняет промежуток между соседними дугами
            new Bone { name = "межрёберная10.м", socket = "Сердце", parent = "ребро10с", attach = 0.45f, length = 0.023f, dir = new Vector3(-40.61f, 0f, 10.43f), r0 = 0.01f, section = 0.4f, mirrorX = true, layer = BodyLayer.Muscle },   // межрёберная: заполняет промежуток между соседними дугами
            new Bone { name = "межрёберная11.м", socket = "Сердце", parent = "ребро11с", attach = 0.45f, length = 0.023f, dir = new Vector3(-35.14f, 0f, 10.64f), r0 = 0.01f, section = 0.4f, mirrorX = true, layer = BodyLayer.Muscle },   // межрёберная: заполняет промежуток между соседними дугами
            new Bone { name = "подвздошно_рёберная.м", socket = "хребет", parent = "ребро9с", attach = 0.22f, length = 0.152f, dir = new Vector3(-146.26f, 0f, 4.27f), r0 = 0.015f, section = 0.58f, mirrorX = true, layer = BodyLayer.Muscle },   // подвздошно-рёберная: валик над последними рёбрами, переход клетки в поясницу
            new Bone { name = "пясть", socket = "Руки", parent = "предплечье", attach = 0.999f, length = 0.028f, dir = new Vector3(13.48f, 0f, -0.83f), r0 = 0.005f, r1 = 0.005f, section = 1.35f, depth = 0.75f, mirrorX = true },   // четыре пясти пучком: поперёк шире, чем в глубину
            new Bone { name = "седалищная", socket = "Ноги", parent = "подвздошная", attach = 1f, length = 0.019f, dir = new Vector3(107.26f, 0f, 13.01f), r0 = 0.008f, r1 = 0.007f, section = 0.7f, mirrorX = true },   // седалищный бугор — задняя точка тела: им кончается круп
            new Bone { name = "лобковая", socket = "Ноги", parent = "подвздошная", attach = 1f, length = 0.033f, dir = new Vector3(-119.55f, 0f, 41.72f), r0 = 0.005f, r1 = 0.003f, section = 0.9f, depth = 0.7f, mirrorX = true },   // лобковая ветвь: ПОЛ ТАЗА до симфиза. Держит прямую живота и замыкает брюшную полость снизу — без неё полость течёт по средней линии и не заливается
            new Bone { name = "бедро", socket = "Ноги", parent = "подвздошная", attach = 1f, length = 0.156f, dir = new Vector3(-61.33f, 0f, 8.24f), r0 = 0.008f, r1 = 0.007f, section = 0.8f, mirrorX = true },
            new Bone { name = "хвост2", socket = "Хвост", parent = "хвост1", attach = 1f, length = 0.059f, dir = new Vector3(-20.84f, 0f, 0f), r0 = 0.01f, r1 = 0.008f, section = 0.95f, chain = 2 },
            new Bone { name = "трицепс.м", socket = "Руки", parent = "лопатка", attach = 0.28f, length = 0.15f, dir = new Vector3(23.1f, 0f, 1.26f), r0 = 0.018f, section = 0.72f, mirrorX = true, layer = BodyLayer.Muscle },   // трицепс: БОЛЬШОЙ ТРЕУГОЛЬНИК за плечом. Главная масса передней ноги в профиль
            new Bone { name = "бицепс.м", socket = "Руки", parent = "лопатка", attach = 0.92f, length = 0.067f, dir = new Vector3(44.54f, 0f, 6.19f), r0 = 0.008f, section = 0.8f, mirrorX = true, layer = BodyLayer.Muscle },   // двуглавая плеча: передняя выпуклость плеча
            new Bone { name = "косая_живота.м", socket = "хребет", parent = "ребро11с", attach = 0.75f, length = 0.292f, dir = new Vector3(-142f, 0f, -1.52f), r0 = 0.014f, section = 0.52f, mirrorX = true, layer = BodyLayer.Muscle },   // наружная косая живота: БОК и подрыв паха — она делает талию подобранной
            new Bone { name = "поперечная_живота.м", socket = "хребет", parent = "ребро10н", attach = 0.7f, length = 0.401f, dir = new Vector3(-143.08f, 0f, -0.52f), r0 = 0.013f, section = 0.46f, depth = 0.92f, mirrorX = true, layer = BodyLayer.Muscle },   // поперечная живота: стенка между рёберной дугой и тазом. Без неё бок за клеткой пуст
            new Bone { name = "лапа_п", socket = "Руки", parent = "пясть", attach = 0.999f, length = 0.025f, dir = new Vector3(-26.81f, 0f, 1.55f), r0 = 0.005f, r1 = 0.006f, section = 1.45f, depth = 0.8f, mirrorX = true },
            new Bone { name = "голень", socket = "Ноги", parent = "бедро", attach = 1f, length = 0.059f, dir = new Vector3(65.09f, 0f, 19.31f), r0 = 0.007f, r1 = 0.005f, section = 0.78f, depth = 1.2f, mirrorX = true },
            new Bone { name = "хвост3", socket = "Хвост", parent = "хвост2", attach = 0.999f, length = 0.053f, dir = new Vector3(-15.01f, 0f, 0f), r0 = 0.008f, r1 = 0.005f, section = 0.95f, chain = 2 },
            new Bone { name = "разгибатели.м", socket = "Руки", parent = "предплечье", attach = 0.08f, length = 0.056f, dir = new Vector3(2.02f, 0f, -0.13f), r0 = 0.01f, section = 0.85f, mirrorX = true, layer = BodyLayer.Muscle },   // разгибатели предплечья: мясо вверху голяшки, сухожилие внизу — оттого нога сужается к лапе
            new Bone { name = "прямая_живота.м", socket = "хребет", parent = "грудина", attach = 0.12f, length = 0.194f, dir = new Vector3(-158.05f, 0f, -0.73f), r0 = 0.01f, section = 0.66f, mirrorX = true, layer = BodyLayer.Muscle },   // прямая живота: нижняя линия от груди к паху. Идёт к ЛОБКУ по средней линии, а не к суставу вбок, — иначе левая и правая половины не смыкаются и живота у зверя нет
            new Bone { name = "ягодичная.м", socket = "Ноги", parent = "крестец", attach = 0.42f, length = 0.092f, dir = new Vector3(-71.32f, 0f, -17.14f), r0 = 0.012f, section = 0.7f, mirrorX = true, layer = BodyLayer.Muscle },   // ягодичная: КРУП. Его округлость целиком её работа
            new Bone { name = "пяточный", socket = "Ноги", parent = "голень", length = 0.017f, dir = new Vector3(89.62f, 0f, 5.53f), r0 = 0.006f, r1 = 0.004f, section = 0.8f, mirrorX = true },   // пяточный бугор: острый угол скакательного — главный признак задней ноги в профиль
            new Bone { name = "плюсна", socket = "Ноги", parent = "голень", attach = 1f, length = 0.035f, dir = new Vector3(-40.41f, 0f, -3.59f), r0 = 0.005f, r1 = 0.005f, section = 1.3f, depth = 0.75f, mirrorX = true },
            new Bone { name = "сухожилия_п.м", socket = "Руки", parent = "пясть", attach = 0.02f, length = 0.036f, dir = new Vector3(-6.32f, 0f, 0.38f), r0 = 0.004f, depth = 1.35f, mirrorX = true, layer = BodyLayer.Muscle },   // сухожилия сгибателей пясти: ТОНКИЙ ТЯЖ ДО ПАЛЬЦЕВ. Без него нога ниже запястья остаётся голой костью, а у неё профиль `long` — диафиз поджат до 52%, и в обмере пясть выходила 1.6 см толщиной, тоньше собственной кости. Сечение глубокое, а не круглое: сзади тяж, спереди кость
            new Bone { name = "напрягатель.м", socket = "Ноги", parent = "подвздошная", attach = 0.22f, length = 0.19f, dir = new Vector3(-45.81f, 0f, 7.6f), r0 = 0.011f, section = 0.52f, mirrorX = true, layer = BodyLayer.Muscle },   // напрягатель широкой фасции: передний край бедра, треугольник перед коленом
            new Bone { name = "четырёхглавая.м", socket = "Ноги", parent = "подвздошная", attach = 0.62f, length = 0.171f, dir = new Vector3(-52.98f, 0f, 7.98f), r0 = 0.013f, section = 0.78f, mirrorX = true, layer = BodyLayer.Muscle },   // четырёхглавая: передняя масса бедра, выносит колено вперёд в силуэте
            new Bone { name = "двуглавая_бедра.м", socket = "Ноги", parent = "седалищная", attach = 0.18f, length = 0.166f, dir = new Vector3(-162.83f, 0f, 22.14f), r0 = 0.013f, section = 0.58f, mirrorX = true, layer = BodyLayer.Muscle },   // двуглавая бедра: ЗАДНЯЯ ЛИНИЯ ЗВЕРЯ от крупа до скакательного. Самая крупная мышца тела
            new Bone { name = "полусухожильная.м", socket = "Ноги", parent = "седалищная", attach = 0.5f, length = 0.174f, dir = new Vector3(-161.71f, 0f, 21.91f), r0 = 0.01f, section = 0.54f, mirrorX = true, layer = BodyLayer.Muscle },   // полусухожильная: за двуглавой, даёт «штаны» на бедре
            new Bone { name = "лапа_з", socket = "Ноги", parent = "плюсна", attach = 1.001f, length = 0.021f, dir = new Vector3(-68.72f, 0f, -3.92f), r0 = 0.005f, r1 = 0.005f, section = 1.4f, depth = 0.8f, mirrorX = true },
            new Bone { name = "икроножная.м", socket = "Ноги", parent = "бедро", attach = 0.88f, length = 0.066f, dir = new Vector3(61.78f, 0f, 18.14f), r0 = 0.011f, section = 0.85f, mirrorX = true, layer = BodyLayer.Muscle },   // икроножная: голень спереди мясистая, сзади тянется в пяточное сухожилие
            new Bone { name = "сгибатели_з.м", socket = "Ноги", parent = "голень", attach = 0.18f, length = 0.057f, dir = new Vector3(-6.93f, 0f, -0.67f), r0 = 0.008f, section = 0.85f, mirrorX = true, layer = BodyLayer.Muscle },   // сгибатели плюсны: остаток мяса на голяшке ниже колена
            new Bone { name = "сухожилия_з.м", socket = "Ноги", parent = "плюсна", attach = 0.02f, length = 0.038f, dir = new Vector3(-10.64f, 0f, -0.78f), r0 = 0.004f, depth = 1.35f, mirrorX = true, layer = BodyLayer.Muscle },   // сухожилия сгибателей плюсны: тот же тяж на задней ноге. У волка он идёт до пальцев, и именно им плюсна держит глубину при малой ширине — «сухая» голяшка канида
        };
        EditorUtility.SetDirty(hog);

        ValidateSockets(new[] { human, wolf, snake, moose, hog }); // сверка сокет-плана: молчит, пока всё сходится

        // ПРАВИЛА ТЕЛА (спека 2026-08-10): объективные поломки — в консоль. Ловит то, что раньше молчало
        // и находилось глазами через дни: ось на грани переключения, цепь без диаметра, нулевой калибр,
        // цикл в графе. Анатомию НЕ проверяем — стилизация решение геймдизайнера, её место в карте тел
        // ...и ПО ЗАМЕРАМ: тело собирается настоящим билдером и обмеряется. Проверка по данным не видит
        // «место висит на пустоте» — по графу родитель есть, а рисует ли он что-нибудь, знает только
        // сборка. Ровно этот дефект держал скелет на покрове и стоил недели правок по скриншотам
        var all = new[] { human, wolf, snake, moose, hog };
        foreach (var sp in all)
        {
            var issues = BodyRules.CheckData(sp);
            issues.AddRange(BodyRules.CheckParts(sp, BodyProbe.Measure(sp)));
            issues.AddRange(BodyRules.CheckBudget(sp));      // бюджет клетки: сумма (M−1)·N против 324 квадов
            foreach (var issue in issues)
            {
                string line = $"[тело] {issue.species} · {issue.where}: {issue.text}";
                if (issue.error) Debug.LogError(line); else Debug.LogWarning(line);
            }
        }

        // ОДИНАКОВОСТЬ M×N МЕЖДУ ВИДАМИ — условие, ради которого клетка вообще существует: меш химеры
        // получается покомпонентным средним таблиц, а среднее определено только при равной размерности.
        // Проверка была написана и не вызывалась ниоткуда, а рантайм на расхождении МОЛЧА выбрасывает
        // донора (`MorphBlend`: `if (!chassisCage.SameTopology(c)) continue;`) — то есть химера тихо
        // теряла бы вид, и по гоче проекта такую фичу не диагностируют
        for (int i = 0; i < all.Length; i++)
            for (int j = i + 1; j < all.Length; j++)
                foreach (var issue in BodyRules.CheckCages(all[i], all[j]))
                {
                    string line = $"[клетка] {issue.species} · {issue.where}: {issue.text}";
                    if (issue.error) Debug.LogError(line); else Debug.LogWarning(line);
                }

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
                // РОДИТЕЛЕМ МОЖЕТ БЫТЬ И КОСТЬ, НЕ ТОЛЬКО МЕСТО. Спрашивать `places` здесь — ложная
                // тревога: у волка Хвост висит на «крестце», Игломёт на «грудном», и оба родителя
                // законны. Это ЧЕТВЁРТЫЙ инструмент с той же ошибкой (три уже исправлены), поэтому
                // знание не дублируется, а берётся из `MorphBuilder` — там же, где его читает билдер.
                //     Валидатор, кричащий на намеренное, хуже молчащего: он приучает не смотреть на красное.
                if (!MorphBuilder.ParentExists(chassis, s.parent))
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
