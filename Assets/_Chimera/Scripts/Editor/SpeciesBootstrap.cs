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
            new BodySocket { name = "хребет", baseEuler = new Vector3(-10.000f, 0.000f, 0.000f), attachOffset = new Vector3(0.000f, -1.977f, 0.383f), parent = "шея", attach = 0.000f, baseSize = new Vector3(0.470f, 0.600f, 0.240f) }, // НЕСУЩИЙ ЦЕНТР: форму даёт орган «Хребет» (chassisOnly), поэтому место больше не служебное  // ЕДИНЫЙ ПЛАН ТЕЛА (спека 2026-08-27): ось от головы назад. Числа посчитаны Anatomy/tools/reroot.py и самопроверены — тело осталось на месте
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
            new BodySocket { name = "голова", parent = "шея", attach = 1.000f, attachOffset = new Vector3(0.000f, -0.003f, 0.167f),    baseSize = new Vector3(0.263f, 0.220f, 0.469f), sizeRel = new Vector3(1.195f, 0.733f, 1.234f), baseEuler = new Vector3(0.8f, 0f, 0f), parts = new[] {
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
            new BodySocket { name = "Хвост", parent = "крестец", attach = 0.000f, linkDiameter = 0.130f, linkLength = 0.088f, linkTaper = 0.900f, chain = 6, baseEuler = new Vector3(-33.2f, 0f, 0f) }, // ХВОСТ — ЦЕПЬ ЗВЕНЬЕВ, как у змеи: капсула + шар-сустав на каждом, сужение одним числом.
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
            // ══ СЛОЙ 1: СКЕЛЕТ ══ `SpeciesSO.buildLayers` показывает срез: 1 — голый скелет, 2 — с мышцами,
            // 3 — с признаками, 4 — с прорезями.
            //     ИМЯ КОСТИ = АНАТОМИЯ + СЛОЙ. Одна единица живёт сразу в нескольких слоях: есть кость скулы и
            // мясо скулы, есть ветвь челюсти и жевательная поверх неё. Пока слой в имя не входил, такие соседи
            // сталкивались адресами — и не с ошибкой, а молча: словарь оставлял последнюю, и часть морды
            // строилась не там, где её считали данные (так было у «жевательной», заданной дважды). Суффикс
            // делает адрес уникальным ПО ПОСТРОЕНИЮ: `.м` — мышца, `.п` — признак, `.р` — рез. Скелет без
            // суффикса — он основа, на его имена ссылаются места, дети и мышцы.
            //     ШЕЯ СПАДАЕТ ВПЕРЁД-ВНИЗ. Прежде ось шла горизонтально (1.075 → 1.080), и зверь нёс голову на
            // уровне спины: контур верха превышал эталонный на 11-13 см от загривка до носа, отчего тело
            // читалось вытянутым бруском. Теперь основание 1.030, затылок 0.980 — и верх сошёлся с эталоном
            // в среднем на 1.8 см при холке 1.170.
            new Bone { name = "крестец", socket = "хребет", origin = new Vector3(0.000f, 1.003f, -0.560f), length = 0.181f, dir = new Vector3(84.6f, 0f, -0.0f), r0 = 0.078f, r1 = 0.082f, section = 1.75f },
            new Bone { name = "поясница", socket = "хребет", parent = "крестец", length = 0.401f, dir = new Vector3(1.4f, 0f, -0.0f), r0 = 0.082f, r1 = 0.086f, section = 1.75f },
            new Bone { name = "грудной", socket = "хребет", parent = "поясница", length = 0.400f, dir = new Vector3(6.6f, 0f, -0.0f), r0 = 0.086f, r1 = 0.090f, section = 1.65f },
            new Bone { name = "шея", socket = "шея", parent = "грудной", length = 0.383f, dir = new Vector3(4.9f, 0f, -0.0f), r0 = 0.134f, r1 = 0.116f, section = 1.05f },
            new Bone { name = "ребро1", socket = "Сердце", parent = "грудной", attach = 0.05f, length = 0.107f, dir = new Vector3(87.4f, 0f, -35.4f), r0 = 0.062f, r1 = 0.058f, section = 0.80f, mirrorX = true },
            new Bone { name = "ребро1с", socket = "Сердце", parent = "ребро1", length = 0.141f, dir = new Vector3(0.0f, 0f, 27.3f), r0 = 0.058f, r1 = 0.050f, section = 0.80f, mirrorX = true },
            new Bone { name = "ребро1н", socket = "Сердце", parent = "ребро1с", length = 0.074f, dir = new Vector3(0.0f, 0f, 44.4f), r0 = 0.050f, r1 = 0.040f, section = 0.80f, mirrorX = true },
            new Bone { name = "ребро2", socket = "Сердце", parent = "грудной", attach = 0.24f, length = 0.104f, dir = new Vector3(87.4f, 0f, -36.5f), r0 = 0.068f, r1 = 0.064f, section = 0.80f, mirrorX = true },
            new Bone { name = "ребро2с", socket = "Сердце", parent = "ребро2", length = 0.141f, dir = new Vector3(0.0f, 0f, 28.4f), r0 = 0.064f, r1 = 0.055f, section = 0.80f, mirrorX = true },
            new Bone { name = "ребро2н", socket = "Сердце", parent = "ребро2с", length = 0.114f, dir = new Vector3(0.0f, 0f, 30.8f), r0 = 0.055f, r1 = 0.044f, section = 0.80f, mirrorX = true },
            new Bone { name = "ребро3", socket = "Сердце", parent = "грудной", attach = 0.44f, length = 0.101f, dir = new Vector3(87.4f, 0f, -37.8f), r0 = 0.070f, r1 = 0.066f, section = 0.80f, mirrorX = true },
            new Bone { name = "ребро3с", socket = "Сердце", parent = "ребро3", length = 0.141f, dir = new Vector3(0.0f, 0f, 29.7f), r0 = 0.066f, r1 = 0.057f, section = 0.80f, mirrorX = true },
            new Bone { name = "ребро3н", socket = "Сердце", parent = "ребро3с", length = 0.137f, dir = new Vector3(0.0f, 0f, 26.8f), r0 = 0.057f, r1 = 0.046f, section = 0.80f, mirrorX = true },
            new Bone { name = "ребро4", socket = "Сердце", parent = "грудной", attach = 0.64f, length = 0.098f, dir = new Vector3(87.4f, 0f, -39.1f), r0 = 0.068f, r1 = 0.064f, section = 0.80f, mirrorX = true },
            new Bone { name = "ребро4с", socket = "Сердце", parent = "ребро4", length = 0.141f, dir = new Vector3(0.0f, 0f, 31.0f), r0 = 0.064f, r1 = 0.055f, section = 0.80f, mirrorX = true },
            new Bone { name = "ребро4н", socket = "Сердце", parent = "ребро4с", length = 0.142f, dir = new Vector3(0.0f, 0f, 26.2f), r0 = 0.055f, r1 = 0.044f, section = 0.80f, mirrorX = true },
            new Bone { name = "ребро5", socket = "Сердце", parent = "грудной", attach = 0.84f, length = 0.096f, dir = new Vector3(87.4f, 0f, -40.4f), r0 = 0.064f, r1 = 0.060f, section = 0.80f, mirrorX = true },
            new Bone { name = "ребро5с", socket = "Сердце", parent = "ребро5", length = 0.141f, dir = new Vector3(0.0f, 0f, 32.3f), r0 = 0.060f, r1 = 0.052f, section = 0.80f, mirrorX = true },
            new Bone { name = "ребро5н", socket = "Сердце", parent = "ребро5с", length = 0.118f, dir = new Vector3(0.0f, 0f, 29.9f), r0 = 0.052f, r1 = 0.042f, section = 0.80f, mirrorX = true },
            new Bone { name = "отросток1", socket = "хребет", parent = "поясница", attach = 0.10f, length = 0.141f, dir = new Vector3(94.0f, 0f, -38.7f), r0 = 0.030f, r1 = 0.024f, section = 0.80f, mirrorX = true },
            new Bone { name = "отросток2", socket = "хребет", parent = "поясница", attach = 0.38f, length = 0.149f, dir = new Vector3(94.0f, 0f, -36.3f), r0 = 0.030f, r1 = 0.024f, section = 0.80f, mirrorX = true },
            new Bone { name = "отросток3", socket = "хребет", parent = "поясница", attach = 0.66f, length = 0.149f, dir = new Vector3(94.0f, 0f, -36.3f), r0 = 0.030f, r1 = 0.024f, section = 0.80f, mirrorX = true },
            new Bone { name = "отросток4", socket = "хребет", parent = "поясница", attach = 0.92f, length = 0.141f, dir = new Vector3(94.0f, 0f, -38.7f), r0 = 0.030f, r1 = 0.024f, section = 0.80f, mirrorX = true },
            new Bone { name = "грудина", socket = "Сердце", parent = "грудной", attach = 0.95f, length = 0.283f, dir = new Vector3(62.2f, 0f, -0.0f), r0 = 0.085f, r1 = 0.062f, section = 0.85f },
            new Bone { name = "лопатка", socket = "Руки", parent = "ребро4", attach = 0.45f, length = 0.304f, dir = new Vector3(-42.3f, 0f, 18.8f), r0 = 0.088f, r1 = 0.070f, section = 0.65f, blend = 0.035f, mirrorX = true },
            new Bone { name = "плечо", socket = "Руки", parent = "лопатка", length = 0.316f, dir = new Vector3(91.4f, 0f, 25.4f), r0 = 0.062f, r1 = 0.046f, section = 0.80f, blend = 0.035f, mirrorX = true },
            new Bone { name = "предплечье", socket = "Руки", parent = "плечо", length = 0.317f, dir = new Vector3(-67.5f, 0f, 10.8f), r0 = 0.055f, r1 = 0.030f, section = 0.85f, blend = 0.035f, mirrorX = true },
            new Bone { name = "пясть", socket = "Руки", parent = "предплечье", length = 0.201f, dir = new Vector3(-2.0f, 0f, 0.1f), r0 = 0.030f, r1 = 0.026f, section = 0.85f, blend = 0.035f, mirrorX = true },
            new Bone { name = "лапа", socket = "Руки", parent = "пясть", length = 0.061f, dir = new Vector3(-17.1f, 0f, 1.3f), r0 = 0.026f, r1 = 0.032f, section = 1.15f, blend = 0.035f, mirrorX = true },
            new Bone { name = "коробка", socket = "голова", parent = "шея", length = 0.180f, dir = new Vector3(-15.8f, 0f, -0.0f), r0 = 0.074f, r1 = 0.068f, section = 1.15f },
            new Bone { name = "ростр", socket = "голова", parent = "коробка", length = 0.183f, dir = new Vector3(26.1f, 0f, -0.0f), r0 = 0.062f, r1 = 0.040f, section = 1.15f, depth = 0.62f },
            new Bone { name = "гребень", socket = "голова", parent = "коробка", attach = 0.05f, length = 0.057f, dir = new Vector3(-90.1f, 0f, -0.0f), r0 = 0.026f, r1 = 0.020f, section = 0.50f },
            new Bone { name = "скула", socket = "голова", parent = "коробка", length = 0.122f, dir = new Vector3(117.0f, 0f, -44.9f), r0 = 0.030f, r1 = 0.025f, section = 1.00f, mirrorX = true },
            new Bone { name = "ветвь", socket = "Пасть", parent = "коробка", attach = 0.25f, length = 0.126f, dir = new Vector3(86.5f, 0f, -21.5f), r0 = 0.034f, r1 = 0.028f, section = 0.85f, mirrorX = true },
            new Bone { name = "хвост1", socket = "Хвост", parent = "крестец", attach = 0.00f, length = 0.159f, dir = new Vector3(121.2f, 0f, -0.0f), r0 = 0.090f, r1 = 0.088f, section = 0.95f },
            new Bone { name = "хвост2", socket = "Хвост", parent = "хвост1", length = 0.159f, dir = new Vector3(-0.0f, 0f, -0.0f), r0 = 0.088f, r1 = 0.086f, section = 0.95f },
            new Bone { name = "хвост3", socket = "Хвост", parent = "хвост2", length = 0.160f, dir = new Vector3(-0.2f, 0f, -0.0f), r0 = 0.086f, r1 = 0.078f, section = 0.95f },
            new Bone { name = "хвост4", socket = "Хвост", parent = "хвост3", length = 0.159f, dir = new Vector3(0.2f, 0f, -0.0f), r0 = 0.078f, r1 = 0.060f, section = 0.95f },
            new Bone { name = "челюсть", socket = "Пасть", parent = "ветвь", length = 0.267f, dir = new Vector3(-86.5f, 0f, 7.9f), r0 = 0.032f, r1 = 0.024f, section = 0.85f, mirrorX = true },
            new Bone { name = "таз", socket = "Ноги", parent = "крестец", attach = 0.55f, length = 0.280f, dir = new Vector3(93.9f, 0f, -19.6f), r0 = 0.112f, r1 = 0.104f, section = 0.86f, blend = 0.035f, mirrorX = true },
            new Bone { name = "бедро", socket = "Ноги", parent = "таз", length = 0.319f, dir = new Vector3(-56.0f, 0f, 11.3f), r0 = 0.118f, r1 = 0.062f, section = 0.78f, blend = 0.035f, mirrorX = true },
            new Bone { name = "голень", socket = "Ноги", parent = "бедро", length = 0.350f, dir = new Vector3(92.9f, 0f, 16.1f), r0 = 0.060f, r1 = 0.032f, section = 0.85f, blend = 0.035f, mirrorX = true },
            new Bone { name = "плюсна", socket = "Ноги", parent = "голень", length = 0.221f, dir = new Vector3(-50.0f, 0f, 0.6f), r0 = 0.032f, r1 = 0.026f, section = 0.85f, blend = 0.035f, mirrorX = true },
            new Bone { name = "лапа_з", socket = "Ноги", parent = "плюсна", length = 0.054f, dir = new Vector3(-27.4f, 0f, 0.2f), r0 = 0.026f, r1 = 0.032f, section = 1.15f, blend = 0.035f, mirrorX = true },

            // ══ СЛОЙ 2: МЫШЦЫ ══ У мышцы ДВА крепления (`endBone`), и в этом вся разница с костью: кость при
            // движении меняет положение, мышца — ещё и форму. Ни длины, ни угла ей не задают: подвинул сустав —
            // подтянулась сама, рассогласовать нечем.
            new Bone { name = "длиннейшая.м", socket = "хребет", parent = "отросток1", length = 0.329f, dir = new Vector3(-93.1f, 0f, -2.5f), r0 = 0.058f, r1 = 0.058f, section = 1.00f, endBone = "отросток4", endAttach = 1.00f, layer = BodyLayer.Muscle, mirrorX = true },
            new Bone { name = "брюшная.м", socket = "хребет", parent = "ребро1н", length = 0.497f, dir = new Vector3(94.9f, 0f, -2.8f), r0 = 0.084f, r1 = 0.078f, section = 0.85f, endBone = "таз", endAttach = 0.88f, layer = BodyLayer.Muscle, mirrorX = true },
            new Bone { name = "косая_живота.м", socket = "хребет", parent = "ребро2с", attach = 0.85f, length = 0.573f, dir = new Vector3(91.6f, 0f, 2.0f), r0 = 0.082f, r1 = 0.072f, section = 0.80f, endBone = "таз", endAttach = 0.60f, layer = BodyLayer.Muscle, mirrorX = true },
            new Bone { name = "межрёберная.м", socket = "Сердце", parent = "ребро4с", attach = 0.60f, length = 0.236f, dir = new Vector3(89.9f, 0f, -0.0f), r0 = 0.070f, r1 = 0.066f, section = 0.85f, endBone = "ребро1с", endAttach = 0.60f, layer = BodyLayer.Muscle, mirrorX = true },
            new Bone { name = "грудная.м", socket = "Сердце", parent = "грудина", attach = 0.95f, length = 0.204f, dir = new Vector3(81.8f, 0f, -24.9f), r0 = 0.062f, r1 = 0.056f, section = 0.85f, endBone = "плечо", endAttach = 0.45f, layer = BodyLayer.Muscle, mirrorX = true },
            new Bone { name = "горловая.м", socket = "шея", parent = "шея", attach = 0.15f, length = 0.397f, dir = new Vector3(7.9f, 0f, -4.0f), r0 = 0.070f, r1 = 0.052f, section = 0.85f, endBone = "ветвь", endAttach = 0.60f, layer = BodyLayer.Muscle },
            new Bone { name = "грудиночелюстная.м", socket = "шея", parent = "грудина", attach = 0.90f, length = 0.369f, dir = new Vector3(-79.7f, 0f, -5.7f), r0 = 0.062f, r1 = 0.048f, section = 0.80f, endBone = "ветвь", endAttach = 0.80f, layer = BodyLayer.Muscle, mirrorX = true },
            new Bone { name = "трицепс.м", socket = "Руки", parent = "лопатка", attach = 0.30f, length = 0.404f, dir = new Vector3(51.5f, 0f, 21.2f), r0 = 0.078f, r1 = 0.044f, section = 0.72f, blend = 0.035f, endBone = "предплечье", endAttach = 0.10f, layer = BodyLayer.Muscle, mirrorX = true },
            new Bone { name = "плечеголовная.м", socket = "Руки", parent = "шея", attach = 0.35f, length = 0.463f, dir = new Vector3(116.5f, 0f, -10.7f), r0 = 0.050f, r1 = 0.058f, section = 0.70f, blend = 0.035f, endBone = "плечо", endAttach = 0.70f, layer = BodyLayer.Muscle, mirrorX = true },
            new Bone { name = "ягодичная.м", socket = "Ноги", parent = "крестец", attach = 0.60f, length = 0.302f, dir = new Vector3(89.5f, 0f, -18.1f), r0 = 0.100f, r1 = 0.082f, section = 0.80f, blend = 0.035f, endBone = "бедро", endAttach = 0.12f, layer = BodyLayer.Muscle, mirrorX = true },
            new Bone { name = "икроножная.м", socket = "Ноги", parent = "бедро", attach = 0.90f, length = 0.391f, dir = new Vector3(81.5f, 0f, 16.0f), r0 = 0.052f, r1 = 0.032f, section = 0.85f, blend = 0.035f, endBone = "плюсна", endAttach = 0.25f, layer = BodyLayer.Muscle, mirrorX = true },
            new Bone { name = "жевательная.м", socket = "голова", parent = "скула", attach = 0.70f, length = 0.077f, dir = new Vector3(0.5f, 0f, 60.8f), r0 = 0.040f, r1 = 0.032f, section = 0.95f, endBone = "челюсть", endAttach = 0.25f, layer = BodyLayer.Muscle, mirrorX = true },
            new Bone { name = "трапециевидная.м", socket = "хребет", parent = "шея", attach = 0.55f, length = 0.306f, dir = new Vector3(159.4f, 0f, -8.5f), r0 = 0.070f, r1 = 0.058f, section = 0.85f, endBone = "лопатка", endAttach = 0.30f, layer = BodyLayer.Muscle, mirrorX = true },
            new Bone { name = "длиннейшая_гр.м", socket = "хребет", parent = "ребро5", attach = 0.12f, length = 0.316f, dir = new Vector3(91.8f, 0f, -1.5f), r0 = 0.076f, r1 = 0.070f, section = 1.00f, endBone = "ребро1", endAttach = 0.12f, layer = BodyLayer.Muscle, mirrorX = true },
            new Bone { name = "ромбовидная.м", socket = "хребет", parent = "шея", attach = 0.30f, length = 0.243f, dir = new Vector3(164.1f, 0f, -7.9f), r0 = 0.062f, r1 = 0.052f, section = 0.80f, endBone = "лопатка", endAttach = 0.10f, layer = BodyLayer.Muscle, mirrorX = true },
            new Bone { name = "широчайшая.м", socket = "хребет", parent = "поясница", attach = 0.85f, length = 0.458f, dir = new Vector3(25.5f, 0f, -7.5f), r0 = 0.078f, r1 = 0.062f, section = 0.70f, endBone = "лопатка", endAttach = 0.55f, layer = BodyLayer.Muscle, mirrorX = true },
            new Bone { name = "зубчатая.м", socket = "Сердце", parent = "ребро3с", attach = 0.55f, length = 0.146f, dir = new Vector3(-117.1f, 0f, 8.5f), r0 = 0.070f, r1 = 0.056f, section = 0.70f, endBone = "лопатка", endAttach = 0.25f, layer = BodyLayer.Muscle, mirrorX = true },
            new Bone { name = "пластыревидная.м", socket = "шея", parent = "грудной", attach = 0.95f, length = 0.438f, dir = new Vector3(3.4f, 0f, -0.0f), r0 = 0.078f, r1 = 0.062f, section = 0.90f, endBone = "коробка", endAttach = 0.20f, layer = BodyLayer.Muscle, mirrorX = true },
            new Bone { name = "дельтовидная.м", socket = "Руки", parent = "лопатка", attach = 0.75f, length = 0.188f, dir = new Vector3(65.3f, 0f, 23.3f), r0 = 0.052f, r1 = 0.044f, section = 0.80f, blend = 0.035f, endBone = "плечо", endAttach = 0.55f, layer = BodyLayer.Muscle, mirrorX = true },
            new Bone { name = "бицепс.м", socket = "Руки", parent = "лопатка", attach = 0.90f, length = 0.370f, dir = new Vector3(70.7f, 0f, 26.8f), r0 = 0.048f, r1 = 0.038f, section = 0.80f, blend = 0.035f, endBone = "предплечье", endAttach = 0.30f, layer = BodyLayer.Muscle, mirrorX = true },
            new Bone { name = "сгибатели.м", socket = "Руки", parent = "предплечье", attach = 0.10f, length = 0.325f, dir = new Vector3(-0.2f, 0f, 0.0f), r0 = 0.044f, r1 = 0.028f, section = 0.85f, blend = 0.035f, endBone = "пясть", endAttach = 0.20f, layer = BodyLayer.Muscle, mirrorX = true },
            new Bone { name = "двуглавая.м", socket = "Ноги", parent = "крестец", attach = 0.35f, length = 0.593f, dir = new Vector3(71.9f, 0f, -9.1f), r0 = 0.098f, r1 = 0.062f, section = 0.78f, blend = 0.035f, endBone = "голень", endAttach = 0.35f, layer = BodyLayer.Muscle, mirrorX = true },
            new Bone { name = "четырёхглавая.м", socket = "Ноги", parent = "таз", attach = 0.75f, length = 0.367f, dir = new Vector3(-41.3f, 0f, 11.3f), r0 = 0.086f, r1 = 0.058f, section = 0.80f, blend = 0.035f, endBone = "голень", endAttach = 0.10f, layer = BodyLayer.Muscle, mirrorX = true },
            new Bone { name = "полусухожильная.м", socket = "Ноги", parent = "крестец", attach = 0.15f, length = 0.630f, dir = new Vector3(72.9f, 0f, -8.6f), r0 = 0.082f, r1 = 0.054f, section = 0.72f, blend = 0.035f, endBone = "голень", endAttach = 0.50f, layer = BodyLayer.Muscle, mirrorX = true },

            // ══ СЛОЙ 3: ПРИЗНАКИ ══ Мелкая форма поверх готового тела: уши, мочка, надбровье, объёмы черепа.
            new Bone { name = "ухо.п", socket = "Чутьё", parent = "коробка", attach = 0.10f, length = 0.190f, dir = new Vector3(-61.0f, 0f, -29.0f), r0 = 0.046f, r1 = 0.030f, section = 1.00f, depth = 0.40f, layer = BodyLayer.Feature, mirrorX = true },
            new Bone { name = "мочка.п", socket = "Чутьё", parent = "ростр", length = 0.025f, dir = new Vector3(27.2f, 0f, -0.0f), r0 = 0.030f, r1 = 0.024f, section = 1.10f, layer = BodyLayer.Feature },
            new Bone { name = "височная.п", socket = "голова", origin = new Vector3(0.038f, 0.996f, 0.824f), length = 0.080f, dir = new Vector3(92.9f, 0f, -2.9f), r0 = 0.030f, r1 = 0.034f, section = 1.00f, layer = BodyLayer.Feature, mirrorX = true },
            new Bone { name = "дуга.п", socket = "голова", origin = new Vector3(0.052f, 0.932f, 0.886f), length = 0.065f, dir = new Vector3(96.1f, 0f, -29.6f), r0 = 0.017f, r1 = 0.015f, section = 1.00f, depth = 0.50f, layer = BodyLayer.Feature, mirrorX = true },
            new Bone { name = "лоб_площадка.п", socket = "голова", origin = new Vector3(0.000f, 1.002f, 0.928f), length = 0.053f, dir = new Vector3(98.7f, 0f, -60.2f), r0 = 0.026f, r1 = 0.022f, section = 1.00f, depth = 0.55f, layer = BodyLayer.Feature, mirrorX = true },
            new Bone { name = "спинка_носа.п", socket = "голова", origin = new Vector3(0.000f, 0.982f, 0.985f), length = 0.159f, dir = new Vector3(99.4f, 0f, -0.0f), r0 = 0.026f, r1 = 0.019f, section = 1.40f, depth = 0.70f, layer = BodyLayer.Feature },
            new Bone { name = "вибриссы.п", socket = "Чутьё", origin = new Vector3(0.032f, 0.934f, 1.058f), length = 0.071f, dir = new Vector3(98.1f, 0f, 4.9f), r0 = 0.030f, r1 = 0.024f, section = 1.00f, layer = BodyLayer.Feature, mirrorX = true },
            new Bone { name = "губа_верх.п", socket = "Пасть", origin = new Vector3(0.030f, 0.916f, 1.040f), length = 0.087f, dir = new Vector3(94.0f, 0f, 5.3f), r0 = 0.016f, r1 = 0.013f, section = 1.10f, depth = 0.70f, layer = BodyLayer.Feature, mirrorX = true },
            new Bone { name = "подбородок_м.п", socket = "Пасть", origin = new Vector3(0.016f, 0.904f, 1.098f), length = 0.041f, dir = new Vector3(84.0f, 0f, 22.7f), r0 = 0.018f, r1 = 0.015f, section = 1.00f, layer = BodyLayer.Feature },
            new Bone { name = "затылок_бугор.п", socket = "голова", origin = new Vector3(0.000f, 0.996f, 0.788f), length = 0.047f, dir = new Vector3(-143.6f, 0f, -0.0f), r0 = 0.045f, r1 = 0.040f, section = 1.00f, layer = BodyLayer.Feature },
            new Bone { name = "надбровье.п", socket = "голова", parent = "коробка", attach = 0.80f, length = 0.060f, dir = new Vector3(-44.9f, 0f, -63.7f), r0 = 0.032f, r1 = 0.028f, section = 1.00f, depth = 0.75f, layer = BodyLayer.Feature, mirrorX = true },

            // ══ СЛОЙ 4: ВЫЧИТАНИЕ ══ Рот и ноздри: щель можно только вырезать.
            //     ГЛАЗНИЦУ ПРИШЛОСЬ УБРАТЬ. Глаз был поставлен по КОСТНОЙ орбите (x 0.058), а поверх неё
            // у зверя 74 мм мяса — щеки и виска. Вырез прорезал дыру до самого яблока, и получался чёрный
            // провал вместо глаза. Теперь глаз сидит на поверхности и утоплен ровно на 10 мм
            new Bone { name = "рот.р", socket = "Пасть", parent = "челюсть", attach = 0.22f, length = 0.232f, dir = new Vector3(-14.1f, 0f, -1.2f), r0 = 0.015f, r1 = 0.012f, section = 1.00f, depth = 0.40f, endBone = "ростр", endAttach = 0.94f, layer = BodyLayer.Cut, mirrorX = true },
            new Bone { name = "ноздря.р", socket = "Чутьё", parent = "ростр", attach = 0.96f, length = 0.021f, dir = new Vector3(17.8f, 0f, -0.0f), r0 = 0.014f, r1 = 0.011f, section = 1.00f, endBone = "мочка.п", endAttach = 0.55f, layer = BodyLayer.Cut, mirrorX = true },
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
            new BodySocket { name = "хребет", baseEuler = new Vector3(38.000f, 0.000f, 0.000f), attachOffset = new Vector3(0.000f, 0.922f, -0.909f), parent = "шея", attach = 0.000f, baseSize = new Vector3(0.521f, 0.791f, 2.284f) }, // НЕСУЩИЙ ЦЕНТР: форму даёт орган «Хребет» (chassisOnly), поэтому место больше не служебное  // ЕДИНЫЙ ПЛАН ТЕЛА (спека 2026-08-27): ось от головы назад. Числа посчитаны Anatomy/tools/reroot.py и самопроверены — тело осталось на месте
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
        EditorUtility.SetDirty(hog);

        ValidateSockets(new[] { human, wolf, snake, moose, hog }); // сверка сокет-плана: молчит, пока всё сходится

        // ПРАВИЛА ТЕЛА (спека 2026-08-10): объективные поломки — в консоль. Ловит то, что раньше молчало
        // и находилось глазами через дни: ось на грани переключения, цепь без диаметра, нулевой калибр,
        // цикл в графе. Анатомию НЕ проверяем — стилизация решение геймдизайнера, её место в карте тел
        // ...и ПО ЗАМЕРАМ: тело собирается настоящим билдером и обмеряется. Проверка по данным не видит
        // «место висит на пустоте» — по графу родитель есть, а рисует ли он что-нибудь, знает только
        // сборка. Ровно этот дефект держал скелет на покрове и стоил недели правок по скриншотам
        foreach (var sp in new[] { human, wolf, snake, moose, hog })
        {
            var issues = BodyRules.CheckData(sp);
            issues.AddRange(BodyRules.CheckParts(sp, BodyProbe.Measure(sp)));
            foreach (var issue in issues)
            {
                string line = $"[тело] {issue.species} · {issue.where}: {issue.text}";
                if (issue.error) Debug.LogError(line); else Debug.LogWarning(line);
            }
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
