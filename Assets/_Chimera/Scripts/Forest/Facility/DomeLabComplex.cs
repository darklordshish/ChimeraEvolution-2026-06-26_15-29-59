using System;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Лаборатория как ЕДИНЫЙ закрытый бункер (B0.1 + уточнение 25.10). Одна плита 46×36,
/// четыре функции вокруг закрытого зала 14×14 с обходом 2.5 м: арена — север, хирургия —
/// запад, клетки — восток, отдых — юг (входная полоса и комната отдыха в южном крыле).
/// <para>
/// Перекрытие СПЛОШНОЕ над залом и комнатами, балки под ним, свет искусственный:
/// холодные потолочные полосы + тёплые лампы у дверей и рабочих мест. Открытого неба,
/// атриума, внутреннего сада и скал в комнатах нет.
/// </para>
/// <para>
/// План интерьера ОДИН, стиль меняет только внешнюю оболочку и вход. <b>Bastion</b> —
/// низкий наземный блокгауз: плита, общий кровельный пояс/парапет, тяжёлая рама входа.
/// <b>InSlope</b> — тот же интерьер: сзади и по бокам подпорные массы/скальная оболочка,
/// толща портала, без пещерного лабиринта. Числа комнат, дверей и внутренних деталей
/// у стилей совпадают; <see cref="Result.interiorParts"/> считается ДО обкладки.
/// </para>
/// <para>
/// Строится кодом как дети корня мира, поэтому NavMesh запекается существующим
/// <c>DomeWorld.BuildNavMesh</c> (collectObjects = Children): пол, стены и перемычки —
/// коллайдеры; крыша тоже с коллайдерами (камера/физика), но помечена
/// <see cref="NavMeshModifier.ignoreFromBuild"/>, чтобы агент не ходил по крыше.
/// </para>
/// <para>
/// Локальный кадр: +X — вдоль плиты, +Z — ОТ чаши (в кольцо), −Z — фасад к центру/входу;
/// y = 0 — площадка мира, верх плиты на <see cref="Plinth"/> (12 см: иначе верх плиты и
/// меш рельефа лежали бы в одной плоскости и мигали). Меши — <see cref="FloraMeshKit.Slab"/>,
/// цвет — <see cref="DomePalette"/>: язык крупных граней тот же, что у флоры и корпусов.
/// </para>
/// </summary>
public static class DomeLabComplex
{
    /// <summary>Набор отделки комплекса (один материал на вид, кэш статический).</summary>
    public enum Kit
    {
        Wall,
        Concrete,
        ConcreteDark,
        Floor,
        Metal,
        Rust,
        Glass,
        Dark,
        Rock,
        Olive,
        PanelCold,
        PanelWarm,
    }

    /// <summary>Что построено. Ссылки на корень и точка портала нужны сцене и B1.</summary>
    public class Result
    {
        public Vector2 center;
        public float yawDeg;
        public LabStyle style;
        /// <summary>Портал с фасада, координаты поля (как <c>DomeWorld.Entry</c>).</summary>
        public Vector3 door;
        /// <summary>Точка внутри зала: цель маршрута Entry → LabDoor → LabAtrium.</summary>
        public Vector3 atrium;
        /// <summary>Северная дверь A; default, если проёма нет.</summary>
        public Vector3 northDoor;
        /// <summary>Все детали, включая внешнюю оболочку стиля.</summary>
        public int parts;
        /// <summary>Детали ОДНОГО интерьера: одинаковы у Bastion и InSlope (сторожит план).</summary>
        public int interiorParts;
        /// <summary>Только внешняя оболочка/вход — здесь стили и различаются.</summary>
        public int exteriorParts;
        public int lights;
        public bool berm;
        /// <summary>Тип входа: Rock или Underground.</summary>
        public LabEntrance entrance;
        /// <summary>Подход мокрый: построен настил от двери до сухого места.</summary>
        public bool wetApproach;
        /// <summary>Следы происшествия B0.2c: подпись выбранного варианта и число деталей.</summary>
        public string damageSignature;
        public int damageParts;
        /// <summary>B0.3: план, по которому собран интерьер.</summary>
        public LabPlan plan;
        public string planTemplate;
        public int planVersion;
        public bool planFallback;
        public GameObject root;
    }

    // ---- числа плана: один источник для мира, площадки (DomeWorld) и поиска места ----
    public const float HalfWidth = 23f;          // плита 46
    public const float HalfDepth = 18f;          // плита 36
    /// <summary>Ось стены зала: 19×19 = зал 14×14 + обход 2.5 м по каждой стороне.</summary>
    public const float HallHalf = 9.5f;
    public const float Plinth = 0.12f;           // верх плиты над площадкой
    public const float PlateThickness = 0.5f;    // низ утоплен в землю на 38 см
    /// <summary>
    /// Толщина стен — 0.5, а не «как у старого лабиринта» 0.2–0.3: NavMesh печётся
    /// вокселем 0.35, и стена тоньше вокселя местами перестаёт быть преградой.
    /// </summary>
    public const float Wall = 0.5f;
    public const float RoomHeight = 4.4f;        // комнаты: перекрытие снизу (рост 3.55 + запас)
    public const float HallHeight = 4.8f;        // зал: выше комнат, под рост 3.55 и балки
    /// <summary>
    /// Двери 4.0 × 4.0: крупнейшая химера 3.55 м + запас; 2 м — нижняя граница,
    /// а не достаточный проём. Комнаты 4.4 оставляют над проёмом 0.4 м перемычки.
    /// Портал — 4.2 × 4.0 в проёме, рама снаружи шире.
    /// </summary>
    public const float DoorWidth = 4.0f;
    public const float DoorHeight = 4.0f;
    public const float PortalWidth = 4.2f;
    public const float RoofThickness = 0.25f;
    /// <summary>Свес крыши за плиту (м): под ним стоит парапет/скала, щели нет.</summary>
    public const float RoofOverhang = 0.3f;
    /// <summary>Площадка под комплекс: полугабариты плиты плюс запас.</summary>
    public const float PadMargin = 2f;
    public const float PadHalfWidth = HalfWidth + PadMargin;   // 25
    public const float PadHalfDepth = HalfDepth + PadMargin;   // 20
    /// <summary>Ширина плавного схода площадки в сырое поле (м).</summary>
    public const float PadBlend = 6f;
    public const int RoomCount = 4;
    /// <summary>Перегородка «отдых | входная полоса» в южном крыле (локальный X).</summary>
    public const float RestPartitionX = -3f;
    /// <summary>Дверь хирургии в зал (локальный Z) и стеклянная перегородка рядом с ней.</summary>
    public const float SurgeryDoorZ = 6f;
    public const float SurgeryGlassZ = -2f;
    public const float SurgeryGlassWidth = 5f;
    /// <summary>Подземный вход B0.2b: перепад и длина наклонной части рампы (м).</summary>
    public const float UndergroundDrop = 2.5f;
    /// <summary>Полностью заглублённый бункер: плита ниже земли на высоту интерьера + крыша.</summary>
    public const float BuriedDepth = 4.8f;
    public const float RampLength = 24f;
    public const float CreviceLength = 12f;
    public const float CreviceWidth = 4.2f;

    public static float DepthFor(LabEntrance entrance) =>
        entrance == LabEntrance.Buried ? BuriedDepth :
        entrance == LabEntrance.Underground ? UndergroundDrop : 0f;
    public const float RampWidth = 6f;

    static readonly Dictionary<string, Mesh> meshCache = new Dictionary<string, Mesh>();
    static readonly Dictionary<Kit, Material> matCache = new Dictionary<Kit, Material>();
    static Mesh icoUnit;

    /// <summary>
    /// Собрать комплекс под <paramref name="parent"/> (корень мира). <paramref name="groundY"/>
    /// — верх площадки в координатах поля: плита встаёт на него, не пересчитывая землю.
    /// </summary>
    public static Result Build(GameObject parent, Vector2 center, float groundY, float yawDeg,
        LabStyle style, LabEntrance entrance = LabEntrance.Rock, bool wetApproach = false,
        LabPlan plan = null, bool northDoor = false)
    {
        if (parent == null) throw new ArgumentNullException(nameof(parent));
        if (style == LabStyle.Auto) style = LabStyle.Bastion;
        if (entrance == LabEntrance.Auto) entrance = LabEntrance.Rock;
        plan = plan ?? LabPlanFixed.Create();
        var planErrors = LabPlanRules.Validate(plan);
        if (planErrors.Count > 0)
        {
            Debug.LogWarning($"[Forest] план бункера невалиден ({string.Join("; ", planErrors)}); беру fixed");
            plan = LabPlanFixed.Create();
            plan.fallback = true;
            plan.note = "fallback: invalid plan";
        }

        var result = new Result
        {
            plan = plan,
            planTemplate = plan.template,
            planVersion = plan.version,
            planFallback = plan.fallback,
            center = center,
            yawDeg = yawDeg,
            style = style,
            entrance = entrance,
            door = DoorPoint(center, groundY, yawDeg),
            atrium = new Vector3(center.x, groundY + Plinth + 1.7f, center.y),
            northDoor = northDoor && style == LabStyle.Bastion
                ? NorthDoorPoint(center, groundY, yawDeg) : default,
            berm = style == LabStyle.InSlope,
            wetApproach = wetApproach,
        };

        var root = new GameObject("LabComplex");
        root.transform.SetParent(parent.transform, false);
        // Корень — в координатах поля: дети ниже считают в СВОЁМ кадре (центр, поворот).
        root.transform.localPosition = new Vector3(center.x, groundY, center.y);
        root.transform.localRotation = Quaternion.Euler(0f, yawDeg, 0f);
        result.root = root;

        // Зеркальный план меняет стороны хирургии и клеток: fixed — surgery слева (x=-16),
        // mirror — surgery справа (x=+16). flip = +1 для fixed, -1 для mirror.
        int flip = plan.Room("surgery").x > 0f ? -1 : 1;
        var b = new Builder(root.transform);
        BuildPlate(b);
        if (plan.version >= 2)
        {
            BuildPlanInterior(b, plan, northDoor && style == LabStyle.Bastion);
            BuildRoofs(b, entrance == LabEntrance.Buried);
            BuildBeams(b, flip);
            result.lights = BuildLights(b, flip);
        }
        else
        {
            BuildShell(b, flip, northDoor && style == LabStyle.Bastion);
            BuildRoofs(b, entrance == LabEntrance.Buried);
            BuildBeams(b, flip);
            BuildHall(b);
            BuildSurgery(b, flip, plan);
            BuildCells(b, flip, plan);
            BuildRest(b);
            BuildEntryLobby(b);
            BuildCeilingFixtures(b, flip);
            result.lights = BuildLights(b, flip);
        }

        // Всё, что выше, — ОДИН интерьер. Ниже — только внешняя оболочка стиля.
        result.interiorParts = b.parts;
        if (wetApproach && entrance != LabEntrance.Underground) BuildWetApproach(b);
        if (entrance == LabEntrance.Underground || entrance == LabEntrance.Buried)
            BuildUndergroundDressing(b, DepthFor(entrance), northDoor && style == LabStyle.Bastion);
        else if (style == LabStyle.InSlope) BuildSlopeDressing(b);
        else BuildBastionDressing(b);
        result.exteriorParts = b.parts - result.interiorParts;

        result.parts = b.parts;
        return result;
    }

    /// <summary>
    /// Точка портала на фасаде (координаты поля). Одна формула на всех: тем же числом
    /// пользуется поиск места (<see cref="DomeLabSite"/>) и мир (<c>DomeWorld.LabDoor</c>).
    /// </summary>
    public static Vector3 DoorPoint(Vector2 center, float groundY, float yawDeg)
    {
        float yaw = yawDeg * Mathf.Deg2Rad;
        var facade = new Vector2(-Mathf.Sin(yaw), -Mathf.Cos(yaw)); // −Z кадра = к чаше
        return new Vector3(center.x + facade.x * HalfDepth, groundY + Plinth,
            center.y + facade.y * HalfDepth);
    }

    /// <summary>Северный выход: противоположная стена, +Z в локальных координатах.</summary>
    public static Vector3 NorthDoorPoint(Vector2 center, float groundY, float yawDeg)
    {
        float yaw = yawDeg * Mathf.Deg2Rad;
        var back = new Vector2(Mathf.Sin(yaw), Mathf.Cos(yaw));
        return new Vector3(center.x + back.x * HalfDepth, groundY + Plinth,
            center.y + back.y * HalfDepth);
    }

    // ------------------------------------------------------------------ плита, стены, крыша

    static void BuildPlate(Builder b)
    {
        // ОДНА плита 46×36. Верх на Plinth (не вровень с рельефом — иначе копланарные
        // поверхности мигают), низ на 38 см в земле: под плитой не бывает щели.
        b.Add("LabPlate", Kit.Floor, HalfWidth * 2f, PlateThickness, HalfDepth * 2f,
            0f, Plinth - PlateThickness, 0f, true);
    }

    static void BuildPlanInterior(Builder b, LabPlan plan, bool northDoor)
    {
        // Версия 2: внешняя оболочка и комнаты читаются из плана, а не из констант.
        float y0 = Plinth;
        b.WallRun(true, -HalfDepth, -HalfWidth, HalfWidth, y0, RoomHeight,
            Kit.Concrete, 0f, PortalWidth);
        b.WallRun(true, HalfDepth, -HalfWidth, HalfWidth, y0, RoomHeight,
            Kit.Concrete, 0f, northDoor ? PortalWidth : 0f);
        b.WallRun(false, -HalfWidth, -HalfDepth, HalfDepth, y0, RoomHeight,
            Kit.Concrete, 0f, 0f);
        b.WallRun(false, HalfWidth, -HalfDepth, HalfDepth, y0, RoomHeight,
            Kit.Concrete, 0f, 0f);

        foreach (var room in plan.rooms)
        {
            BuildPlanRoomWalls(b, plan, room);
            BuildPlanRoomFurniture(b, room);
        }
    }

    static void BuildPlanRoomWalls(Builder b, LabPlan plan, LabRoom r)
    {
        float x0 = r.x - r.width * 0.5f, x1 = r.x + r.width * 0.5f;
        float z0 = r.z - r.depth * 0.5f, z1 = r.z + r.depth * 0.5f;
        float y0 = Plinth;
        if (z1 < HalfDepth - 0.01f)
            WallWithDoor(b, plan, r.id, true, z1, x0, x1, y0);
        if (z0 > -HalfDepth + 0.01f)
            WallWithDoor(b, plan, r.id, true, z0, x0, x1, y0);
        if (x1 < HalfWidth - 0.01f)
            WallWithDoor(b, plan, r.id, false, x1, z0, z1, y0);
        if (x0 > -HalfWidth + 0.01f)
            WallWithDoor(b, plan, r.id, false, x0, z0, z1, y0);
    }

    static void WallWithDoor(Builder b, LabPlan plan, string roomId, bool alongX,
        float at, float from, float to, float y0)
    {
        float doorAt = 0f, doorWidth = 0f;
        foreach (var d in plan.doors)
        {
            if (d.from != roomId && d.to != roomId) continue;
            if (d.alongX != alongX) continue;
            float normal = alongX ? d.z : d.x;
            float along = alongX ? d.x : d.z;
            // Дверь BSP стоит в коридорном зазоре между комнатами; режем
            // ближайшую стену комнаты, а не только совпадающую по координате.
            if (Mathf.Abs(normal - at) > 4f) continue;
            if (along < from || along > to) continue;
            doorAt = along;
            doorWidth = d.width;
            break;
        }
        b.WallRun(alongX, at, from, to, y0, RoomHeight, Kit.Wall, doorAt, doorWidth);
    }

    static void BuildPlanRoomFurniture(Builder b, LabRoom r)
    {
        if (r.function == LabFunction.Surgery)
            b.Add("PlanSurgeryTable", Kit.Metal, 2.2f, 0.8f, 1.1f, r.x, Plinth, r.z, true);
        else if (r.function == LabFunction.Cells)
            b.Add("PlanCellBench", Kit.ConcreteDark, 1.2f, 0.45f, 2.2f, r.x, Plinth, r.z, true);
        else if (r.function == LabFunction.Rest)
            b.Add("PlanRestBench", Kit.Olive, 2.0f, 0.45f, 1.0f, r.x, Plinth, r.z, true);
        else if (r.function == LabFunction.Lobby)
            b.Add("PlanLobbyDesk", Kit.ConcreteDark, 2.4f, 0.75f, 1.1f, r.x, Plinth, r.z, true);
    }
    static void BuildShell(Builder b, int flip, bool northDoor = false)
    {
        float y0 = Plinth;
        float ox = HalfWidth - Wall * 0.5f;   // ось внешней стены 22.75
        float oz = HalfDepth - Wall * 0.5f;   // 17.75

        // Внешний контур: фасад (юг, −Z) с порталом, остальные три стороны глухие.
        b.WallRun(true, -oz, -HalfWidth, HalfWidth, y0, RoomHeight, Kit.Concrete, 0f, PortalWidth);
        b.WallRun(true, oz, -HalfWidth, HalfWidth, y0, RoomHeight, Kit.Concrete, 0f,
            northDoor ? PortalWidth : 0f);
        b.WallRun(false, ox, -HalfDepth, HalfDepth, y0, RoomHeight, Kit.Concrete, 0f, 0f);
        b.WallRun(false, -ox, -HalfDepth, HalfDepth, y0, RoomHeight, Kit.Concrete, 0f, 0f);

        // Северная стена: боковые части — стены арены, центральная — стена зала с дверью.
        b.WallRun(true, HallHalf, -HalfWidth, -HallHalf, y0, RoomHeight, Kit.Wall, 0f, 0f);
        b.WallRun(true, HallHalf, -HallHalf, HallHalf, y0, HallHeight, Kit.Wall, 0f, DoorWidth);
        b.WallRun(true, HallHalf, HallHalf, HalfWidth, y0, RoomHeight, Kit.Wall, 0f, 0f);

        // Южная стена: отдых/входная полоса — стены, центральная — стена зала с дверью.
        b.WallRun(true, -HallHalf, -HalfWidth, -HallHalf, y0, RoomHeight, Kit.Wall, 0f, 0f);
        b.WallRun(true, -HallHalf, -HallHalf, HallHalf, y0, HallHeight, Kit.Wall, 0f, DoorWidth);
        b.WallRun(true, -HallHalf, HallHalf, HalfWidth, y0, RoomHeight, Kit.Wall, 0f, 0f);

        // Восточная стена зала: дверь в клетки.
        b.WallRun(false, -flip * HallHalf, -HallHalf, HallHalf, y0, HallHeight, Kit.Wall, 0f, DoorWidth);

        // Западная стена зала: дверь в хирургию + стеклянная перегородка.
        BuildSurgeryWall(b, flip);

        // Южное крыло разделено на комнату отдыха (запад) и входную полосу (восток).
        b.WallRun(false, RestPartitionX, -HalfDepth, -HallHalf, y0, RoomHeight,
            Kit.Wall, -13.5f, DoorWidth);
    }

    /// <summary>
    /// Западная стена зала: дверь в хирургию (Z = +6) и стеклянная перегородка (Z = −2).
    /// Стекло вставлено в НАСТОЯЩИЙ проём стены: подоконник снизу, перемычка сверху, стекло
    /// с коллайдером — сквозь него не пройти, но работу видно из зала.
    /// </summary>
    static void BuildSurgeryWall(Builder b, int flip)
    {
        float y0 = Plinth;
        float h = HallHeight;
        float x = flip * -HallHalf;
        float glassA = SurgeryGlassZ - SurgeryGlassWidth * 0.5f; // −4.5
        float glassC = SurgeryGlassZ + SurgeryGlassWidth * 0.5f; // +0.5
        float doorA = SurgeryDoorZ - DoorWidth * 0.5f;           // +4.5
        float doorC = SurgeryDoorZ + DoorWidth * 0.5f;           // +7.5

        b.WallRun(false, x, -HallHalf, glassA, y0, h, Kit.Wall, 0f, 0f);
        b.WallRun(false, x, glassC, doorA, y0, h, Kit.Wall, 0f, 0f);
        b.WallRun(false, x, doorC, HallHalf, y0, h, Kit.Wall, 0f, 0f);

        // Перемычка двери (0.8 м) и проём стекла: подоконник 1.0, стекло 2.0, перемычка 1.0.
        b.Add("SurgeryDoorLintel", Kit.Wall, Wall, h - DoorHeight, DoorWidth,
            x, y0 + DoorHeight, SurgeryDoorZ, true);
        b.Add("SurgerySill", Kit.Wall, Wall, 1f, SurgeryGlassWidth,
            x, y0, SurgeryGlassZ, true);
        b.Add("SurgeryHeader", Kit.Wall, Wall, h - 3f, SurgeryGlassWidth,
            x, y0 + 3f, SurgeryGlassZ, true);
        b.Add("SurgeryGlass", Kit.Glass, 0.12f, 2f, SurgeryGlassWidth,
            x, y0 + 1f, SurgeryGlassZ, true);
        // Стекло тонкое (0.12), а воксель NavMesh — 0.35: без сплошной преграды
        // агент прошёл бы «сквозь окно». Стекло остаётся видимым, преграда — в толще стены.
        b.Barrier("SurgeryGlassBarrier", Wall, 2f, SurgeryGlassWidth,
            x, y0 + 1f, SurgeryGlassZ);
    }

    static void BuildRoofs(Builder b, bool hideRender = false)
    {
        float roomY = Plinth + RoomHeight;
        float hallY = Plinth + HallHeight;
        float outerX = HalfWidth + RoofOverhang;       // 23.3
        float outerZ = HalfDepth + RoofOverhang;       // 18.3
        float hallEdge = HallHalf + Wall * 0.5f;       // 9.75

        float midZ = (hallEdge + outerZ) * 0.5f;
        float midZSize = outerZ - hallEdge;
        float midX = (hallEdge + outerX) * 0.5f;
        float midXSize = outerX - hallEdge;

        // Четыре крыла + зал: перекрытие СПЛОШНОЕ, ни одного открытого проёма.
        AddRoof(b, "LabRoofN", outerX * 2f, midZSize, 0f, roomY, midZ, hideRender);
        AddRoof(b, "LabRoofS", outerX * 2f, midZSize, 0f, roomY, -midZ, hideRender);
        AddRoof(b, "LabRoofW", midXSize, hallEdge * 2f, -midX, roomY, 0f, hideRender);
        AddRoof(b, "LabRoofE", midXSize, hallEdge * 2f, midX, roomY, 0f, hideRender);
        AddRoof(b, "LabRoofHall", hallEdge * 2f, hallEdge * 2f, 0f, hallY, 0f, hideRender);
    }

    static void AddRoof(Builder b, string name, float sizeX, float sizeZ,
        float x, float y, float z, bool hideRender = false)
    {
        // Коллайдер — для камеры и физики; NavMeshModifier — чтобы по крыше не ходили.
        var go = b.AddNoNav(name, Kit.Concrete, sizeX, RoofThickness, sizeZ, x, y, z, true);
        // Только крыша кастит тень: она закрывает интерьер от солнца павильона.
        var roofRenderer = go.GetComponent<MeshRenderer>();
        roofRenderer.shadowCastingMode = ShadowCastingMode.On;
        roofRenderer.enabled = !hideRender;
    }

    static void BuildBeams(Builder b, int flip)
    {
        float hallBeamY = Plinth + HallHeight - 0.35f;
        for (int i = -1; i <= 1; i++)
            b.Add("HallBeam", Kit.ConcreteDark, HallHalf * 2f - 0.4f, 0.35f, 0.4f,
                0f, hallBeamY, i * 5.5f);

        float roomBeamY = Plinth + RoomHeight - 0.3f;
        float arenaZ = (HallHalf + Wall * 0.5f + HalfDepth - Wall * 0.5f) * 0.5f;
        float arenaDepth = HalfDepth - Wall - HallHalf - 0.2f;
        for (int i = -1; i <= 1; i++)
            b.Add("ArenaBeam", Kit.ConcreteDark, 0.4f, 0.3f, arenaDepth,
                i * 15f, roomBeamY, arenaZ);

        b.Add("SurgeryBeam", Kit.ConcreteDark, 0.4f, 0.3f, 18.3f,
            flip * -16.5f, roomBeamY, 0f);
        b.Add("CellsBeam", Kit.ConcreteDark, 0.4f, 0.3f, 18.3f,
            flip * 13.5f, roomBeamY, 0f);
        b.Add("RestBeam", Kit.ConcreteDark, 19f, 0.3f, 0.4f,
            -12.9f, roomBeamY, -13.5f);
        b.Add("LobbyBeam", Kit.ConcreteDark, 25f, 0.3f, 0.4f,
            9.9f, roomBeamY, -13.5f);
    }

    // ------------------------------------------------------------------ интерьер

    /// <summary>Зал: пустой центр без колонны, разметка 14×14 и две простые скамьи у стен.</summary>
    static void BuildHall(Builder b)
    {
        float y = Plinth;
        // Тонкая разметка центрального квадрата: пол остаётся пустым, колонны нет.
        b.Add("HallLineN", Kit.Dark, 14f, 0.04f, 0.12f, 0f, y, 7f);
        b.Add("HallLineS", Kit.Dark, 14f, 0.04f, 0.12f, 0f, y, -7f);
        b.Add("HallLineW", Kit.Dark, 0.12f, 0.04f, 14f, -7f, y, 0f);
        b.Add("HallLineE", Kit.Dark, 0.12f, 0.04f, 14f, 7f, y, 0f);
        b.Add("HallBenchW", Kit.Concrete, 0.6f, 0.45f, 3f, -8.9f, y, -6.5f, true);
        b.Add("HallBenchE", Kit.Concrete, 0.6f, 0.45f, 3f, 8.9f, y, -6.5f, true);
    }

    /// <summary>Хирургия: стол, тумба, шкаф, рабочая лампа; стекло и дверь — в стене зала.</summary>
    static void BuildSurgery(Builder b, int flip, LabPlan plan)
    {
        float y = Plinth;
        const float cx = -16f, cz = 0f;
        float F(float v) => v * flip;
        bool deep = LabPlanGeometry.IsDeepWings(plan);
        float farZ = deep ? LabPlanGeometry.FarZ(plan.Room("surgery"), 1.5f) : 5f;
        float nearZ = deep ? -farZ : -6f;
        b.Add("SurgeryTableBase", Kit.Metal, 1.2f, 0.6f, 1.8f, F(cx), y, cz, true);
        b.Add("SurgeryTableTop", Kit.Metal, 1.4f, 0.2f, 2.4f, F(cx), y + 0.6f, cz, true);
        b.Add("SurgeryTray", Kit.Metal, 0.6f, 0.08f, 0.4f, F(cx - 0.2f), y + 0.8f, cz + 0.7f, true);
        b.Add("SurgeryStool", Kit.Metal, 0.5f, 0.45f, 0.5f, F(cx + 1.8f), y, cz + 1.8f, true);
        b.Add("SurgeryCabinet", Kit.Concrete, 0.7f, 1.8f, 1.4f, F(-22.1f), y, farZ, true);
        b.Add("SurgeryLocker", Kit.Metal, 0.6f, 2f, 1.2f, F(-22.1f), y, nearZ, true);
        b.Add("SurgeryWorkLamp", Kit.PanelWarm, 1.2f, 0.15f, 0.8f, F(cx), y + 3f, cz);
    }

    /// <summary>
    /// Клетки: четыре глубоких отсека у восточной стены, бетонные разделители и решётки.
    /// Отсеки ПУСТЫЕ (ни раковин, ни поилок); проход перед решётками свободен.
    /// </summary>
    static void BuildCells(Builder b, int flip, LabPlan plan)
    {
        float y = Plinth;
        const float barX = 17.5f;
        float backX = HalfWidth - Wall;          // 22.5
        float depth = backX - barX;              // 5.0
        float midX = (barX + backX) * 0.5f;      // 20.0
        float F(float v) => v * flip;
        bool deep = LabPlanGeometry.IsDeepWings(plan);
        float farZ = deep ? LabPlanGeometry.FarZ(plan.Room("cells"), 1.5f) : 6.5f;
        float nearZ = deep ? -farZ : -6.5f;
        float[] centers = { -6.3f, -2.1f, 2.1f, 6.3f };

        for (int i = 0; i < 5; i++)
        {
            float dz = -8.4f + i * 4.2f;
            b.Add("CellDivider", Kit.Concrete, depth, RoomHeight, 0.2f, F(midX), y, dz, true);
        }

        foreach (float zc in centers)
        {
            // Решётка: восемь прутьев, верхняя/нижняя обвязка; коллайдер — отдельной
            // невидимой плитой (иначе NavMesh просачивается в пустой отсек через щели).
            for (int k = 0; k < 8; k++)
            {
                float bz = zc - 1.8f + k * (3.6f / 7f);
                b.Add("CellBar", Kit.Metal, 0.08f, 3f, 0.08f, F(barX), y, bz);
            }
            b.Add("CellRailTop", Kit.Metal, 0.14f, 0.12f, 4f, F(barX), y + 3f, zc);
            b.Add("CellRailBottom", Kit.Metal, 0.14f, 0.08f, 4f, F(barX), y, zc);
            b.Add("CellHeader", Kit.Concrete, 0.3f, RoomHeight - 3f, 4f, F(barX), y + 3f, zc, true);
            b.Add("CellTrim", Kit.Rust, 0.06f, 0.12f, 4f, F(barX - 0.18f), y + 2.95f, zc);
            b.Barrier("CellBarrier", 0.5f, 3f, 4f, F(barX), y, zc);
            b.Add("CellLamp", Kit.PanelWarm, 0.18f, 0.25f, 0.6f, F(barX - 0.28f), y + 2.85f, zc);
        }

        b.Add("CellsDesk", Kit.Metal, 0.7f, 0.75f, 2f, F(11f), y, nearZ, true);
        b.Add("CellsStool", Kit.Olive, 0.45f, 0.45f, 0.45f, F(11.9f), y, nearZ, true);
        b.Add("CellsLocker", Kit.Metal, 0.6f, 2f, 1.2f, F(10.2f), y, farZ, true);
    }

    /// <summary>
    /// Отдых (запад южного крыла): две низкие лежанки в нишах, стол и табуреты.
    /// Вход — из входной полосы южного крыла, не через зал.
    /// </summary>
    static void BuildRest(Builder b)
    {
        float y = Plinth;
        Bunk(b, -21.5f, -15.4f, y);
        Bunk(b, -21.5f, -11.6f, y);
        b.Add("RestHeadA", Kit.Concrete, 0.2f, 1.2f, 1.4f, -22.4f, y, -15.4f, true);
        b.Add("RestHeadB", Kit.Concrete, 0.2f, 1.2f, 1.4f, -22.4f, y, -11.6f, true);
        b.Add("RestDivider", Kit.Concrete, 1.8f, 1f, 0.2f, -21.4f, y, -13.5f, true);

        b.Add("RestTable", Kit.Metal, 1.2f, 0.75f, 2f, -15f, y, -13.5f, true);
        b.Add("RestStoolA", Kit.Olive, 0.45f, 0.45f, 0.45f, -13.4f, y, -13.5f, true);
        b.Add("RestStoolB", Kit.Olive, 0.45f, 0.45f, 0.45f, -16.6f, y, -13.5f, true);
        b.Add("RestLockers", Kit.Metal, 3f, 2f, 0.5f, -8f, y, -17.15f, true);

        b.Add("RestLampA", Kit.PanelWarm, 0.15f, 0.3f, 0.5f, -22.2f, y + 2.2f, -15.4f);
        b.Add("RestLampB", Kit.PanelWarm, 0.15f, 0.3f, 0.5f, -22.2f, y + 2.2f, -11.6f);
    }

    static void Bunk(Builder b, float x, float z, float y)
    {
        b.Add("BunkBase", Kit.Metal, 2f, 0.35f, 1.2f, x, y, z, true);
        b.Add("BunkMattress", Kit.Olive, 1.95f, 0.18f, 1.1f, x, y + 0.35f, z, true);
    }

    /// <summary>Входная полоса: портал и дверь зала на одной оси, по бокам — скамья и шкафчики.</summary>
    static void BuildEntryLobby(Builder b)
    {
        float y = Plinth;
        b.Add("LobbyBench", Kit.Concrete, 0.6f, 0.45f, 3f, 21.6f, y, -13.5f, true);
        b.Add("LobbyLockers", Kit.Metal, 0.5f, 2f, 2.4f, 21.8f, y, -16.2f, true);
        b.Add("LobbyDesk", Kit.Metal, 0.8f, 0.75f, 2.4f, 12f, y, -13.5f, true);
        b.Add("LobbyStool", Kit.Olive, 0.45f, 0.45f, 0.45f, 13.1f, y, -13.5f, true);
        b.Add("LobbyLampA", Kit.PanelWarm, 0.15f, 0.3f, 0.5f, -2f, y + 2.6f, -17.2f);
        b.Add("LobbyLampB", Kit.PanelWarm, 0.15f, 0.3f, 0.5f, 2f, y + 2.6f, -17.2f);
    }

    /// <summary>
    /// Светильники: холодные полосы ПОД сплошным перекрытием (не дыры в крыше) и тёплые
    /// маркеры у дверей/рабочих мест. Корпуса — детали; фактические источники — BuildLights.
    /// </summary>
    static void BuildCeilingFixtures(Builder b, int flip)
    {
        float roomY = Plinth + RoomHeight - 0.12f;
        float hallY = Plinth + HallHeight - 0.12f;

        b.Add("HallPanelA", Kit.PanelCold, 6f, 0.12f, 0.5f, 0f, hallY, -3f);
        b.Add("HallPanelB", Kit.PanelCold, 6f, 0.12f, 0.5f, 0f, hallY, 3f);
        b.Add("ArenaPanel", Kit.PanelCold, 6f, 0.12f, 0.5f, 7f, roomY, 13.5f);
        b.Add("SurgeryPanel", Kit.PanelCold, 0.5f, 0.12f, 6f, flip * -12.5f, roomY, 0f);
        b.Add("CellsPanel", Kit.PanelCold, 0.5f, 0.12f, 6f, flip * 11f, roomY, 0f);
        b.Add("RestPanel", Kit.PanelCold, 6f, 0.12f, 0.5f, -13f, roomY, -11f);
        b.Add("LobbyPanel", Kit.PanelCold, 6f, 0.12f, 0.5f, 10f, roomY, -11f);

        // Тёплые сконсы у четырёх дверей зала (свет — в BuildLights, здесь корпус).
        b.Add("DoorLampN", Kit.PanelWarm, 0.18f, 0.5f, 0.3f,
            2.4f, Plinth + 2.3f, HallHalf - 0.2f);
        b.Add("DoorLampS", Kit.PanelWarm, 0.18f, 0.5f, 0.3f,
            2.4f, Plinth + 2.3f, -HallHalf + 0.2f);
        b.Add("DoorLampW", Kit.PanelWarm, 0.3f, 0.5f, 0.18f,
            flip * (-HallHalf + 0.2f), Plinth + 2.3f, SurgeryDoorZ + 1.7f);
        b.Add("DoorLampE", Kit.PanelWarm, 0.3f, 0.5f, 0.18f,
            -flip * (HallHalf - 0.2f), Plinth + 2.3f, 2f);
    }

    // ------------------------------------------------------------------ свет

    /// <summary>
    /// Шесть холодных общих ламп (зал + четыре комнаты + входная полоса) и четыре тёплых
    /// рабочих/входных. БЕЗ теней и без объёмного света: интерьер читается пятнами,
    /// а не светится как улица. <c>DomeSkyRig</c> гасит только Directional, поэтому ночью
    /// лаборатория остаётся освещённой.
    /// </summary>
    static int BuildLights(Builder b, int flip)
    {
        float yHall = Plinth + HallHeight - 0.7f;
        float yRoom = Plinth + RoomHeight - 0.7f;
        int n = 0;
        n += b.Light("LabLight_Hall", new Vector3(0f, yHall, 0f), 14f, 1.5f, DomePalette.LabCold);
        n += b.Light("LabLight_Arena", new Vector3(0f, yRoom, 13.5f), 12f, 1f, DomePalette.LabCold);
        n += b.Light("LabLight_Surgery", new Vector3(flip * -16f, yRoom, 0f), 8f, 0.9f, DomePalette.LabCold);
        n += b.Light("LabLight_Cells", new Vector3(flip * 13f, yRoom, 0f), 10f, 0.9f, DomePalette.LabCold);
        n += b.Light("LabLight_Rest", new Vector3(-13f, yRoom, -13.5f), 8f, 0.9f, DomePalette.LabCold);
        n += b.Light("LabLight_Lobby", new Vector3(10f, yRoom, -13.5f), 10f, 0.9f, DomePalette.LabCold);
        n += b.Light("LabLight_SurgeryWork", new Vector3(flip * -16f, Plinth + 2.8f, 0f),
            3.5f, 1f, DomePalette.LabWarm);
        n += b.Light("LabLight_RestWarm", new Vector3(-13f, Plinth + 2.4f, -13.5f),
            4f, 0.8f, DomePalette.LabWarm);
        n += b.Light("LabLight_Entrance", new Vector3(0f, Plinth + 2.8f, -16.5f),
            5f, 1f, DomePalette.LabWarm);
        n += b.Light("LabLight_CellsWarm", new Vector3(flip * 17f, Plinth + 2.7f, 0f),
            9f, 0.7f, DomePalette.LabWarm);
        return n;
    }

    // ------------------------------------------------------------------ обкладка стилей

    /// <summary>
    /// Мокрый/низкий подход: бетонный настил от порога до сухого места. Верх на Plinth,
    /// чтобы не было ступени; коллайдер — чтобы NavMesh вёл по настилу, а не по воде.
    /// </summary>
    static void BuildWetApproach(Builder b)
    {
        const float length = 26f;
        float z = -HalfDepth - length * 0.5f;
        b.Add("WetApproach", Kit.Floor, 6f, Plinth, length, 0f, 0f, z, true);
        b.Add("WetApproachCurbW", Kit.Concrete, 0.3f, 0.3f, length,
            -3f, Plinth, z, true);
        b.Add("WetApproachCurbE", Kit.Concrete, 0.3f, 0.3f, length,
            3f, Plinth, z, true);
    }

    /// <summary>
    /// Подземный вход B0.2b: пологая рампа от двери вверх к лесной площадке,
    /// подпорные стены, земляная крыша над рампой и низкий портал. Внутри — тот же
    /// закрытый интерьер; снаружи читается спуск, а не фасад.
    /// </summary>
    static void BuildUndergroundDressing(Builder b, float depth, bool northCrevice)
    {
        float angle = Mathf.Atan2(depth, RampLength) * Mathf.Rad2Deg;
        float rampLen = Mathf.Sqrt(RampLength * RampLength + depth * depth);
        float rampZ = -HalfDepth - RampLength * 0.5f;

        b.AddRotated("RampFloor", Kit.Floor, RampWidth, 0.25f, rampLen,
            new Vector3(0f, Plinth + depth * 0.5f, rampZ),
            Quaternion.Euler(angle, 0f, 0f), true);
        b.Add("RampWallW", Kit.Rock, 1.2f, 2.4f, RampLength,
            -RampWidth * 0.5f - 0.4f, 0f, rampZ, true);
        b.Add("RampWallE", Kit.Rock, 1.2f, 2.4f, RampLength,
            RampWidth * 0.5f + 0.4f, 0f, rampZ, true);

        b.AddRock("RampBermW1", 4.2f, -5.4f, 0f, -HalfDepth - 6f);
        b.AddRock("RampBermW2", 3.8f, -5.0f, 0f, -HalfDepth - 15f);
        b.AddRock("RampBermE1", 4.4f, 5.6f, 0f, -HalfDepth - 7f);
        b.AddRock("RampBermE2", 3.6f, 5.2f, 0f, -HalfDepth - 16f);

        // У Buried южный спуск — открытая выемка в холме, без бетонной рамы:
        // рама на вершине читается воротами на кургане, а не входом в землю.
        if (!northCrevice)
        {
            float zOuter = -HalfDepth - RampLength + 0.6f;
            b.Add("RampOuterW", Kit.Concrete, 1.1f, 3.4f, 1.4f,
                -RampWidth * 0.5f - 0.2f, depth, zOuter, true);
            b.Add("RampOuterE", Kit.Concrete, 1.1f, 3.4f, 1.4f,
                RampWidth * 0.5f + 0.2f, depth, zOuter, true);
            b.Add("RampOuterLintel", Kit.Concrete, RampWidth + 2.2f, 0.8f, 1.6f,
                0f, depth + 3.2f, zOuter, true);
            b.Add("RampOuterCanopy", Kit.Concrete, RampWidth + 2.2f, 0.3f, 2.4f,
                0f, depth + 3.4f, zOuter - 0.8f, true);
        }

        if (northCrevice)
        {
            // Земляная крыша с разрывом под расщелину: станция больше не коробка на газоне.
            float gap = CreviceWidth * 0.5f + 0.5f;
            float sideW = (HalfWidth * 2f - gap * 2f) * 0.5f;
            float roofY = Plinth + RoomHeight + RoofThickness + 0.3f;
            // Рваные каменные всхолмления вместо плоской плиты: крыша — холм,
            // а не бетонный стол. Коллайдеров нет — это только силуэт.
            b.AddMound("BuriedMoundE", 11f, 3.4f, 10f, 0f, roofY - 0.8f, 0f);
            b.AddRock("BuriedCrownW", 4.2f, -21f, roofY - 1.0f, -17f);
            b.AddRock("BuriedCrownE", 4.4f, 21f, roofY - 1.0f, -17f);
            b.AddRock("BuriedCrownS", 3.8f, -20f, roofY - 0.8f, 17f);
            b.AddRock("BuriedCrownN", 3.8f, 20f, roofY - 0.8f, 17f);

            // Северная расщелина: пол поднимается от двери к поверхности, борта — скала.
            float rise = Mathf.Max(0.1f, depth - Plinth);
            float creviceLen = Mathf.Sqrt(CreviceLength * CreviceLength + rise * rise);
            float creviceAngle = Mathf.Atan2(rise, CreviceLength) * Mathf.Rad2Deg;
            float z0 = HalfDepth;
            float z1 = HalfDepth + CreviceLength;
            float cz = (z0 + z1) * 0.5f;
            b.AddRotated("CreviceFloor", Kit.Floor, CreviceWidth, 0.25f, creviceLen,
                new Vector3(0f, Plinth + rise * 0.5f, cz),
                Quaternion.Euler(-creviceAngle, 0f, 0f), true);
            b.Add("CreviceWallW", Kit.Rock, 1.1f, 3.4f, CreviceLength,
                -CreviceWidth * 0.5f - 0.55f, Plinth + 1.4f, cz, true);
            b.Add("CreviceWallE", Kit.Rock, 1.1f, 3.4f, CreviceLength,
                CreviceWidth * 0.5f + 0.55f, Plinth + 1.4f, cz, true);
            b.AddRock("CreviceMouthW", 1.4f, -3.4f, depth - 0.2f, z1 + 0.8f);
            b.AddRock("CreviceMouthE", 1.2f, 3.3f, depth - 0.2f, z1 + 0.8f);
            b.Light("LabLight_Crevice", new Vector3(0f, depth + 1.8f, z1 - 0.8f),
                9f, 0.85f, DomePalette.LabCold);

            // Насыпь вокруг расщелины даёт StationHill/камни; плоские коробки
            // UndergroundHillW/E здесь были «столом» и убраны.
        }
        else
        {
            b.Add("UndergroundHill", Kit.Rock, 62f, 10f, 18f, 0f, -2f, 23f, true);
        }

        b.AddRock("UndergroundRockW", 6f, -12f, Plinth + RoomHeight, 2f);
        b.AddRock("UndergroundRockE", 5.5f, 12f, Plinth + RoomHeight, 1f);
        if (!northCrevice) b.AddRock("UndergroundRockN", 7f, 0f, Plinth + RoomHeight, 10f);
        b.AddRock("UndergroundRockC", 6.5f, 0f, Plinth + RoomHeight, 0f);
        b.AddRock("UndergroundRockS", 5f, 4f, Plinth + RoomHeight, -8f);
        b.AddRock("UndergroundRockSW", 4.5f, -9f, Plinth + RoomHeight, -7f);
        if (!northCrevice)
        {
            b.Add("UndergroundWingW", Kit.Rock, 8f, 7f, 30f, -26f, -2f, 2f, true);
            b.Add("UndergroundWingE", Kit.Rock, 8f, 7f, 30f, 26f, -2f, 2f, true);
        }
    }

    /// <summary>
    /// Bastion: низкий наземный блокгауз. Одна плита, общий кровельный пояс/парапет,
    /// тяжёлая рама входа. Открытого двора нет — крыша сплошная.
    /// </summary>
    static void BuildBastionDressing(Builder b)
    {
        float roofTop = Plinth + RoomHeight + RoofThickness;
        float px = HalfWidth + 0.1f;                          // 23.1
        float pz = HalfDepth + 0.1f;                          // 18.1
        b.AddNoNav("ParapetN", Kit.Concrete, HalfWidth * 2f + 0.4f, 0.55f, 0.4f,
            0f, roofTop, pz, true);
        b.AddNoNav("ParapetS", Kit.Concrete, HalfWidth * 2f + 0.4f, 0.55f, 0.4f,
            0f, roofTop, -pz, true);
        b.AddNoNav("ParapetW", Kit.Concrete, 0.4f, 0.55f, HalfDepth * 2f + 0.4f,
            -px, roofTop, 0f, true);
        b.AddNoNav("ParapetE", Kit.Concrete, 0.4f, 0.55f, HalfDepth * 2f + 0.4f,
            px, roofTop, 0f, true);

        // Портал: пилоны стоят на земле СНАРУЖИ плиты (y = 0, не Plinth) и заходят на
        // стену на 20 см — щели между рамой и фасадом нет.
        float gz = -(HalfDepth + 0.4f);
        float frameX = PortalWidth * 0.5f + 0.5f;
        b.Add("PortalL", Kit.Concrete, 1f, 4.8f, 1.2f, -frameX, 0f, gz, true);
        b.Add("PortalR", Kit.Concrete, 1f, 4.8f, 1.2f, frameX, 0f, gz, true);
        b.Add("PortalLintel", Kit.Dark, frameX * 2f + 1f, 0.8f, 1.4f, 0f, 4.0f, gz, true);
        b.Add("PortalBandL", Kit.Rust, 0.9f, 0.25f, 0.06f, -frameX, 1.2f, gz - 0.6f);
        b.Add("PortalBandR", Kit.Rust, 0.9f, 0.25f, 0.06f, frameX, 1.2f, gz - 0.6f);
    }

    /// <summary>
    /// InSlope: тот же интерьер и вход, но сзади и по бокам — подпорные массы/скальная
    /// оболочка, а портал — толща камня. Один прямой проход, не пещерный лабиринт.
    /// </summary>
    static void BuildSlopeDressing(Builder b)
    {
        // Скальный вход B0.2a: портал В ТОЛЩЕ, а не надстройка над крышей.
        // Снаружи — крупные каменные массы по бокам и над рамой, спереди ровная
        // площадка подхода; внутри — тот же закрытый интерьер.
        const float sink = -2f;
        float roofTop = Plinth + RoomHeight + RoofThickness;
        float frontZ = -HalfDepth - 2.0f;
        float sideX = PortalWidth * 0.5f + 0.8f;
        // Не одна плоская стена: боковые щёки короче, дальше — рваные каменные массы.
        float faceW = 12f;
        float faceX = sideX + faceW * 0.5f;

        // Задний массив и бока: комплекс читается врезанным в скалу.
        b.Add("CliffHill", Kit.Rock, 60f, 9f, 14f, 0f, sink, 24f, true);
        b.Add("CliffHillHigh", Kit.Rock, 40f, 12f, 10f, -4f, sink, 30f, true);
        b.Add("CliffWingW", Kit.Rock, 8f, 7f, 32f, -26.5f, sink, 2f, true);
        b.Add("CliffWingE", Kit.Rock, 8f, 7f, 32f, 26.5f, sink, 2f, true);
        // Скала над северным крылом — НА крыше, не в комнате.
        b.AddNoNav("CliffRoof", Kit.Rock, 46f, 2.2f, 12f, 0f, roofTop, 12f, true);

        // Каменные щёки портала: проём между ними 4.2 м, над ним — скала.
        b.Add("CliffFaceW", Kit.Rock, faceW, 8f, 4.6f, -faceX, 0f, frontZ, true);
        b.Add("CliffFaceE", Kit.Rock, faceW, 8f, 4.6f, faceX, 0f, frontZ, true);
        b.Add("CliffLintelRock", Kit.Rock, sideX * 2f + 1.2f, 4.2f, 4.6f,
            0f, 4.35f, frontZ, true);
        // Рваные массы по краям: плоская стена читается сараем, а не скалой.
        b.AddRock("CliffMassW1", 6.5f, -20f, sink, -19f);
        b.AddRock("CliffMassW2", 4.2f, -14f, 0f, -19.5f);
        b.AddRock("CliffMassE1", 6.8f, 21f, sink, -19f);
        b.AddRock("CliffMassE2", 4.5f, 14f, 0f, -19.5f);
        b.AddRock("CliffBrowW", 5.2f, -7.5f, 4.8f, -20.2f);
        b.AddRock("CliffBrowE", 5.4f, 8.2f, 4.6f, -20.2f);
        b.AddRock("CliffCrown", 6.8f, 0f, 7.0f, -21.0f);

        // Бетонная рама в толще: глубокие откосы, козырёк, без коллайдера поперёк.
        float jambX = PortalWidth * 0.5f + 0.35f;
        b.Add("RockPortalW", Kit.Concrete, 0.7f, 4.8f, 1.5f,
            -jambX, 0f, -HalfDepth - 0.25f, true);
        b.Add("RockPortalE", Kit.Concrete, 0.7f, 4.8f, 1.5f,
            jambX, 0f, -HalfDepth - 0.25f, true);
        b.Add("RockPortalLintel", Kit.Dark, PortalWidth + 1.4f, 0.8f, 1.7f,
            0f, 4.0f, -HalfDepth - 0.25f, true);
        b.Add("RockCanopy", Kit.Concrete, 7.2f, 0.35f, 2.6f,
            0f, 4.35f, -HalfDepth - 1.3f, true);
        b.Add("RockThreshold", Kit.Concrete, PortalWidth + 0.6f, 0.12f, 1.2f,
            0f, 0f, -HalfDepth - 0.7f, true);

        // Площадка подхода: ровная, без ступени; верх на Plinth.
        b.Add("RockApproach", Kit.Floor, 8f, Plinth, 10f,
            0f, 0f, -HalfDepth - 5f);

        // Обломки — у щёк, не в проходе; без коллайдеров.
        b.AddRock("RockFallW", 1.8f, -4.2f, 0f, -21.2f);
        b.AddRock("RockFallE", 1.4f, 4.5f, 0f, -20.6f);
        b.AddRock("RockFallW2", 1.1f, -5.4f, 0f, -19.4f);
        b.AddRock("BoulderW", 2.6f, -14f, sink, 21f);
        b.AddRock("BoulderE", 3.2f, 12f, sink, 22f);
    }

    // ------------------------------------------------------------------ кэши и снос

    /// <summary>Рантайм: Destroy отложен, редактор — DestroyImmediate. Зовёт <c>DomeWorld.Clear</c>.</summary>
    public static IReadOnlyList<Material> Materials => new List<Material>(matCache.Values);

    /// <summary>Точка внутри закрытого объёма станции (локальный бокс, без триггеров).</summary>
    public static bool Contains(Result result, Vector3 worldPos)
    {
        if (result == null || result.root == null) return false;
        Vector3 p = result.root.transform.InverseTransformPoint(worldPos);
        return Mathf.Abs(p.x) <= HalfWidth - Wall * 0.5f + 0.01f
            && Mathf.Abs(p.z) <= HalfDepth - Wall * 0.5f + 0.01f
            && p.y >= Plinth - 0.1f
            && p.y <= Plinth + Mathf.Max(HallHeight, RoomHeight) + RoofThickness + 0.2f;
    }
    public static void ReleaseCaches()
    {
        bool immediate = !Application.isPlaying;
        foreach (var mat in matCache.Values)
            if (mat != null) DestroyObject(mat, immediate);
        matCache.Clear();
        foreach (var mesh in meshCache.Values)
            if (mesh != null) DestroyObject(mesh, immediate);
        meshCache.Clear();
        if (icoUnit != null) DestroyObject(icoUnit, immediate);
        icoUnit = null;
    }

    static void DestroyObject(UnityEngine.Object obj, bool immediate)
    {
        if (obj == null) return;
        if (immediate) UnityEngine.Object.DestroyImmediate(obj);
        else UnityEngine.Object.Destroy(obj);
    }

    static Mesh SlabCached(float sx, float sy, float sz)
    {
        string key = $"slab|{sx:F2}|{sy:F2}|{sz:F2}";
        if (meshCache.TryGetValue(key, out var mesh) && mesh != null) return mesh;
        mesh = FloraMeshKit.Slab(sx * 0.5f, sy, sz * 0.5f);
        meshCache[key] = mesh;
        return mesh;
    }

    /// <summary>Общий материал отдела — для слоя разрушений (тот же кэш).</summary>
    public static Material MaterialOf(Kit kit) => KitMat(kit);

    static Material KitMat(Kit kit)
    {
        if (matCache.TryGetValue(kit, out var mat) && mat != null) return mat;
        mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        Color color = ColorOf(kit);
        mat.color = color;
        mat.SetFloat("_Smoothness", kit == Kit.Glass ? 0.6f : 0f);
        mat.enableInstancing = true;

        if (kit == Kit.Glass)
        {
            // Настоящее стекло перегородки: сквозь него видно стол, но не пройти.
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_SURFACE_TYPE_OPAQUE");
            mat.renderQueue = (int)RenderQueue.Transparent;
        }
        else if (kit == Kit.PanelCold || kit == Kit.PanelWarm)
        {
            // Плита светится сама: это лампа под перекрытием, а не окно в крыше.
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color * 0.75f);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        matCache[kit] = mat;
        return mat;
    }

    static Color ColorOf(Kit kit)
    {
        switch (kit)
        {
            case Kit.Wall: return DomePalette.Wall;
            case Kit.Concrete: return DomePalette.Concrete;
            case Kit.ConcreteDark: return DomePalette.ConcreteDark;
            case Kit.Floor: return DomePalette.Floor;
            case Kit.Metal: return DomePalette.Metal;
            case Kit.Rust: return DomePalette.Rust;
            case Kit.Glass:
                Color glass = DomePalette.Glass;
                glass.a = 0.32f;
                return glass;
            case Kit.Dark: return DomePalette.Dark;
            case Kit.Rock: return DomePalette.Rock;
            case Kit.Olive: return DomePalette.Olive;
            case Kit.PanelCold: return DomePalette.LabCold;
            case Kit.PanelWarm: return DomePalette.LabWarm;
            default: return DomePalette.Concrete;
        }
    }

    // ------------------------------------------------------------------ сборщик

    /// <summary>Мелкий сборщик: размер — полные габариты коробки, y — ОСНОВАНИЕ коробки.</summary>
    class Builder
    {
        readonly Transform root;
        public int parts;

        public Builder(Transform root)
        {
            this.root = root;
        }

        public GameObject Add(string name, Kit kit, float sx, float sy, float sz,
            float x, float y, float z, bool collider = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(x, y, z);
            go.AddComponent<MeshFilter>().sharedMesh = SlabCached(sx, sy, sz);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = KitMat(kit);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            if (collider)
            {
                var box = go.AddComponent<BoxCollider>();
                // Меш плиты растёт от основания вверх — коллайдер обязан совпасть с ним,
                // а не встать центром в ноль (иначе половина стены без коллайдера).
                box.center = new Vector3(0f, sy * 0.5f, 0f);
                box.size = new Vector3(sx, sy, sz);
            }
            parts++;
            return go;
        }

        /// <summary>Наклонная деталь (рампа): размер — полные габариты, поворот локальный.</summary>
        public GameObject AddRotated(string name, Kit kit, float sx, float sy, float sz,
            Vector3 pos, Quaternion rot, bool collider = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.AddComponent<MeshFilter>().sharedMesh = SlabCached(sx, sy, sz);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = KitMat(kit);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            if (collider)
            {
                var box = go.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, sy * 0.5f, 0f);
                box.size = new Vector3(sx, sy, sz);
            }
            parts++;
            return go;
        }

        /// <summary>Деталь с коллайдером, но БЕЗ участия в NavMesh (крыша, парапет).</summary>
        public GameObject AddNoNav(string name, Kit kit, float sx, float sy, float sz,
            float x, float y, float z, bool collider = false)
        {
            var go = Add(name, kit, sx, sy, sz, x, y, z, collider);
            var mod = go.AddComponent<NavMeshModifier>();
            mod.ignoreFromBuild = true;
            mod.applyToChildren = true;
            return go;
        }

        /// <summary>
        /// Невидимая преграда: решётка клетки рисуется прутьями, а NavMesh должен видеть
        /// сплошную плиту — иначе агент проходит между прутьями в пустой отсек.
        /// </summary>
        public void Barrier(string name, float sx, float sy, float sz,
            float x, float y, float z)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(x, y, z);
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, sy * 0.5f, 0f);
            box.size = new Vector3(sx, sy, sz);
            parts++;
        }

        /// <summary>Глыба: икосаэдр радиуса r стоит на земле (центр на r над y).</summary>
        public void AddMound(string name, float sx, float sy, float sz,
            float x, float y, float z)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            // Не сплюснутый «колпак»: наклон, разброс высоты и посадка в землю.
            // Иначе широкие икосаэдры сливаются в одну плоскую плиту.
            float u = Mathf.Abs(Mathf.Sin(x * 12.9898f + z * 78.233f));
            float v = Mathf.Abs(Mathf.Sin(x * 37.719f + z * 11.135f));
            float rx = Mathf.Lerp(12f, 28f, u);
            float rz = Mathf.Lerp(-24f, 24f, v);
            float ry = Mathf.Lerp(0f, 360f, u * 0.7f + v * 0.3f);
            float sink = Mathf.Lerp(1.3f, 2.3f, v);
            go.transform.localPosition = new Vector3(x, y + sy * 0.35f - sink, z);
            go.transform.localRotation = Quaternion.Euler(rx, ry, rz);
            go.transform.localScale = new Vector3(sx, sy * 0.58f, sz);
            go.AddComponent<MeshFilter>().sharedMesh = IcoUnit();
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = KitMat(Kit.Rock);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            parts++;
        }

        public void AddRock(string name, float r, float x, float y, float z)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(x, y + r * 0.75f, z);
            go.transform.localScale = Vector3.one * r;
            go.AddComponent<MeshFilter>().sharedMesh = IcoUnit();
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = KitMat(Kit.Rock);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            parts++;
        }

        public void WallRun(bool alongX, float at, float from, float to, float y0, float h,
            Kit kit, float doorAt, float doorWidth)
        {
            if (to - from < 0.01f) return;
            if (doorWidth <= 0f)
            {
                Segment(alongX, at, from, to, y0, h, kit);
                return;
            }
            float a = doorAt - doorWidth * 0.5f;
            float c = doorAt + doorWidth * 0.5f;
            if (a > from) Segment(alongX, at, from, a, y0, h, kit);
            if (to > c) Segment(alongX, at, c, to, y0, h, kit);
            // Перемычка над проёмом: без неё дверь — щель до крыши.
            if (h > DoorHeight + 0.01f)
                Segment(alongX, at, Mathf.Max(a, from), Mathf.Min(c, to), y0 + DoorHeight,
                    h - DoorHeight, kit);
        }

        void Segment(bool alongX, float at, float from, float to, float y0, float h, Kit kit)
        {
            float len = to - from;
            if (len <= 0.01f) return;
            float mid = (from + to) * 0.5f;
            if (alongX) Add("LabWall", kit, len, h, Wall, mid, y0, at, true);
            else Add("LabWall", kit, Wall, h, len, at, y0, mid, true);
        }

        public int Light(string name, Vector3 local, float range, float intensity, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.localPosition = local;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            // Без теней — решение B0.1/25.10: свет читает интерьер и не стоит кадра.
            light.shadows = LightShadows.None;
            parts++;
            return 1;
        }
    }

    static Mesh IcoUnit()
    {
        if (icoUnit == null) icoUnit = FloraMeshKit.Icosahedron(1f);
        return icoUnit;
    }
}
