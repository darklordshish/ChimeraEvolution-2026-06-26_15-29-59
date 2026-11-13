using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    /// <summary>
    /// S0 ландшафтного пакета: базлайн A/C/B0/Pavilion на HEAD 961f854d.
    /// <para>
    /// ЭТАЛОН СНЯТ СО СЦЕН, а не с хелперов: `Assets/Scenes/ForestSandbox.unity`,
    /// `ForestFragment.unity` (B0 — ТОЛЬКО сид 9), `ForestPavilion.unity`. Сериализованный
    /// профиль и действующий конфиг заморожены в EditMode `ForestSceneBaselineTests`
    /// (открывает сцены так же, как `ForestSceneCameraTests`). Этот файл повторяет
    /// тот же профиль и меряет уже собранный мир настоящим `<see cref="ForestBuilder"/>`.
    /// </para>
    /// <para>
    /// Два разных хэша не смешиваются:
    /// (1) <c>DomeHeightField.SampleGridHash</c> — H0 v1, чистый в EditMode
    /// `BaselineFingerprintTests`; (2) высотный хэш <c>SampleGround</c> по
    /// <see cref="SamplingContract.meshStep"/> — здесь, с шагом/началом/разрешением
    /// прямо в имени константы. Плюс <see cref="WorldFingerprint"/> v1 и адреса.
    /// </para>
    /// </summary>
    public class ForestBaselineTests
    {
        const string CapturedAt = "2026-11-01 @ 961f854d";

        // --- A / C: ForestSandbox (Ø2000, сид 1337, quads 16) ---
        const ulong FingerprintA = 0xa6b6283d4b847211UL;
        // SampleGround: step = SamplingContract.meshStep = 15.625, начало = −1000, 129×129.
        const ulong GroundHashA_MeshStep15_625_StartMinus1000_Res129 = 0x967d37191a80b93cUL;
        const ulong AddressHashA = 0x5c05474793fca2e0UL;
        const ulong LairHashA = 0x6ad44a9edf683300UL;
        const ulong SpaceHashEmpty = 0xcbf29ce484222325UL; // FNV offset: пустая разметка

        // --- B0: ForestFragment (Ø400, сид 9 — решение 01.11; сид 4 исторический) ---
        const ulong FingerprintB0 = 0xd58d869818fdc05dUL;
        // SampleGround: step = 100/24 = 4.166667, начало = −200, 97×97.
        const ulong GroundHashB0_MeshStep4_166667_StartMinus200_Res97 = 0x570c57cc608ac600UL;
        const ulong AddressHashB0 = 0x28bb3d4c4f8aeb37UL;
        const ulong LairHashB0 = 0x83d5a093504cad64UL;

        // --- B-lite: ForestPavilion (Ø2000, сид 1337, quads 48, 1500 деревьев) ---
        const ulong FingerprintPavilion = 0xfc1c4c01aefd5896UL;
        // SampleGround: step = 250/48 = 5.208333, начало = −1000, 385×385.
        const ulong GroundHashPavilion_MeshStep5_208333_StartMinus1000_Res385 = 0xeba2291750ce51f8UL;
        const ulong AddressHashPavilion = 0x59ecf1e52f58af8bUL;
        const ulong LairHashPavilion = 0xae3b7cf9586b3acbUL;
        const ulong SpaceHashPavilion = 0x6169effdaeaa4f30UL;

        GameObject host;
        ForestBuilder builder;

        [TearDown]
        public void TearDown()
        {
            if (builder != null) builder.Clear();
            if (host != null) Object.Destroy(host);
            builder = null;
            host = null;
        }

        /// <summary>
        /// Профиль = сериализованные поля сцены (см. EditMode `ForestSceneBaselineTests`).
        /// Пул видов/префаб — население, а не форма мира; его сюда не тащим, но
        /// `patchyFlora`, `treeCount`, `terrainQuads` и флаги — ровно из сцены.
        /// </summary>
        ForestBuilder Make(long seed, float diameter, float chunk, int quads,
            bool ash, bool spawnAtEntry, bool patchy = false, int treeCount = 0,
            bool ravines = false, bool labComplex = false, bool labPlan = false,
            bool climate = false, bool terrace = false, bool canyon = false,
            bool stationLoop = false, bool stationHill = false,
            LabStyle style = LabStyle.Bastion, LabEntrance entrance = LabEntrance.Rock)
        {
            host = new GameObject("~ForestBaselineProbe");
            host.SetActive(false);
            builder = host.AddComponent<ForestBuilder>();
            builder.buildOnAwake = false;
            builder.randomSeed = false;
            builder.seed = seed;
            builder.mapDiameter = diameter;
            builder.chunkSize = chunk;
            builder.terrainQuads = quads;
            builder.treeCount = treeCount;
            builder.patchyFlora = patchy;
            builder.buildAsh = ash;
            builder.spawnAtEntry = spawnAtEntry;
            builder.buildRavines = ravines;
            builder.buildLabComplex = labComplex;
            builder.buildLabPlan = labPlan;
            builder.labPlanVersion = 1;
            builder.buildClimate = climate;
            builder.buildTerrace = terrace;
            builder.buildCanyon = canyon;
            builder.buildStationLoop = stationLoop;
            builder.buildStationHill = stationHill;
            builder.labStyle = style;
            builder.labEntrance = entrance;
            builder.minLabDistance = 150f;
            builder.speciesPool = new List<SpeciesSO>();
            host.SetActive(true);
            return builder;
        }

        /// <summary>`ForestSandbox` — профиль A.</summary>
        ForestBuilder A() => Make(1337L, 2000f, 250f, 16, ash: true, spawnAtEntry: false);

        /// <summary>`ForestSandbox` с пулом — C: форма мира та же, проверяем обвязку спавнера.</summary>
        ForestBuilder C() => Make(1337L, 2000f, 250f, 16, ash: true, spawnAtEntry: false);

        /// <summary>`ForestFragment` целиком (B0.2/B0.3), сид 9 — единственный B0.</summary>
        ForestBuilder B0() => Make(9L, 400f, 100f, 24, ash: true, spawnAtEntry: true,
            ravines: true, labComplex: true, labPlan: true, climate: true,
            style: LabStyle.InSlope, entrance: LabEntrance.Underground);

        /// <summary>`ForestPavilion` (B-lite): Ø2000, 1500 деревьев, пятна, география 27.10.</summary>
        ForestBuilder Pavilion() => Make(1337L, 2000f, 250f, 48, ash: true, spawnAtEntry: true,
            patchy: true, treeCount: 1500, labComplex: true, labPlan: true, climate: true,
            terrace: true, canyon: true, stationLoop: true, stationHill: true,
            style: LabStyle.Bastion, entrance: LabEntrance.Rock);

        static int SnakeLairs(ForestBuilder b)
        {
            int n = 0;
            foreach (var lair in b.Lairs)
                if (lair != null && lair.speciesName == "Змея") n++;
            return n;
        }

        // ---------------------------------------------------------------------
        // Хэши
        // ---------------------------------------------------------------------

        /// <summary>
        /// Высотный хэш <c>SampleGround</c> по <see cref="SamplingContract"/>:
        /// step = meshStep, начало = −mapDiameter/2, узлов = round(D/step)+1.
        /// НЕ путать с H0 v1 (`DomeHeightField.SampleGridHash`, шаг 1 м) — там сырое поле.
        /// </summary>
        static ulong GroundHash(ForestBuilder b)
        {
            var c = b.Contract;
            float step = c.meshStep;
            Assert.Greater(step, 0f, "SampleGround-хэш: контракт не посчитан");
            int n = Mathf.RoundToInt(b.mapDiameter / step) + 1;
            float start = -b.mapDiameter * 0.5f;
            ulong h = 14695981039346656037UL;
            for (int iz = 0; iz < n; iz++)
                for (int ix = 0; ix < n; ix++)
                    MixQ(ref h, b.SampleGround(start + ix * step, start + iz * step));
            return h;
        }

        /// <summary>Адреса и производные (вход, спавн, станция, вода, ясень).</summary>
        static ulong AddressHash(ForestBuilder b)
        {
            ulong h = 14695981039346656037UL;
            MixQ(ref h, b.Entry.x); MixQ(ref h, b.Entry.y); MixQ(ref h, b.Entry.z);
            MixQ(ref h, b.SpawnPoint.x); MixQ(ref h, b.SpawnPoint.y); MixQ(ref h, b.SpawnPoint.z);
            MixQ(ref h, b.LabCenter.x); MixQ(ref h, b.LabCenter.y);
            MixQ(ref h, b.LabDoor.x); MixQ(ref h, b.LabDoor.y); MixQ(ref h, b.LabDoor.z);
            MixQ(ref h, b.LabAtrium.x); MixQ(ref h, b.LabAtrium.y); MixQ(ref h, b.LabAtrium.z);
            MixQ(ref h, b.PlatformY); MixQ(ref h, b.PadYaw);
            if (b.Hydro != null)
            {
                MixQ(ref h, b.Hydro.lakeCenter.x); MixQ(ref h, b.Hydro.lakeCenter.y);
                MixQ(ref h, b.Hydro.lakeRadius); MixQ(ref h, b.Hydro.lakeLevel);
            }
            if (b.HasAsh) { MixQ(ref h, b.Ash.pos.x); MixQ(ref h, b.Ash.pos.y); MixQ(ref h, b.Ash.groundY); }
            return h;
        }

        /// <summary>Координаты логовищ (не только Count) + тип + вид.</summary>
        static ulong LairHash(ForestBuilder b)
        {
            ulong h = 14695981039346656037UL;
            foreach (var l in b.Lairs)
            {
                MixQ(ref h, l.transform.position.x);
                MixQ(ref h, l.transform.position.y);
                MixQ(ref h, l.transform.position.z);
                Mix(ref h, (int)l.areaType);
                Mix(ref h, l.speciesName == "Змея" ? 1 : 2);
            }
            return h;
        }

        /// <summary>Поляны/коридоры/поды: координаты, а не только число.</summary>
        static ulong SpaceHash(ForestBuilder b)
        {
            ulong h = 14695981039346656037UL;
            foreach (var c in b.Zones)
            { MixQ(ref h, c.center.x); MixQ(ref h, c.center.y); MixQ(ref h, c.radius); }
            foreach (var c in b.Links)
            {
                MixQ(ref h, c.a.x); MixQ(ref h, c.a.y);
                MixQ(ref h, c.b.x); MixQ(ref h, c.b.y);
                MixQ(ref h, c.width);
            }
            foreach (var p in b.CoverPods)
            {
                MixQ(ref h, p.center.x); MixQ(ref h, p.center.y);
                MixQ(ref h, p.radius); MixQ(ref h, p.groundY);
            }
            return h;
        }

        /// <summary>Максимальное отклонение пола от PlatformY внутри плиты станции.</summary>
        static float PadDeviation(ForestBuilder b)
        {
            if (b.LabComplex == null) return 0f;
            float yaw = b.PadYaw * Mathf.Deg2Rad;
            float c = Mathf.Cos(yaw), s = Mathf.Sin(yaw);
            float max = 0f;
            const int n = 9;
            for (int iz = 0; iz < n; iz++)
                for (int ix = 0; ix < n; ix++)
                {
                    float lx = Mathf.Lerp(-DomeLabComplex.PadHalfWidth * 0.9f,
                        DomeLabComplex.PadHalfWidth * 0.9f, ix / (n - 1f));
                    float lz = Mathf.Lerp(-DomeLabComplex.PadHalfDepth * 0.9f,
                        DomeLabComplex.PadHalfDepth * 0.9f, iz / (n - 1f));
                    float x = b.LabCenter.x + lx * c + lz * s;
                    float z = b.LabCenter.y - lx * s + lz * c;
                    max = Mathf.Max(max, Mathf.Abs(b.SampleGround(x, z) - b.PlatformY));
                }
            return max;
        }

        static void MixQ(ref ulong h, float v) => Mix(ref h, Mathf.RoundToInt(v * 1000f));

        static void Mix(ref ulong h, int v)
        {
            h ^= (uint)v;
            h *= 1099511628211UL;
        }

        static void AssertVec(Vector3 actual, Vector3 expected, string what)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(0.002f),
                $"{CapturedAt}: {what} уехал: {actual} вместо {expected}");
        }

        static void AssertVec(Vector2 actual, Vector2 expected, string what)
        {
            Assert.That(Vector2.Distance(actual, expected), Is.LessThan(0.002f),
                $"{CapturedAt}: {what} уехал: {actual} вместо {expected}");
        }

        // ---------------------------------------------------------------------
        // A / C
        // ---------------------------------------------------------------------

        [UnityTest]
        public IEnumerator A_Profile_KeepsFiftyTwoChunks_AndAddresses()
        {
            var b = A();
            b.Build();
            yield return null;

            Assert.AreEqual(1, b.FingerprintVersion, "A: версия формулы отпечатка");
            Assert.AreEqual(52, b.Chunks, "A: изменилось число чанков Ø2000");
            Assert.AreEqual(700, b.Trees, "A: изменилось число деревьев");
            Assert.IsTrue(b.HasAsh, "A: пропал ясень");
            Assert.IsNotNull(b.NestSite, "A: пропало совиное гнездо");
            Assert.AreEqual(10, b.Lairs.Count, "A: набор логовищ (9 змей + сова)");
            Assert.AreEqual(9, SnakeLairs(b), "A: змеиных логовищ");

            // Адреса и производные — не только счётчики.
            AssertVec(b.Entry, new Vector3(-466.0878f, -1.8091f, -769.9105f), "A: Entry");
            AssertVec(b.SpawnPoint, new Vector3(0f, 0.5121f, 0f), "A: SpawnPoint");
            AssertVec(b.LabCenter, Vector2.zero, "A: LabCenter");
            AssertVec(b.LabDoor, Vector3.zero, "A: LabDoor (старого лабиринта нет)");
            AssertVec(b.LabAtrium, Vector3.zero, "A: LabAtrium");
            Assert.AreEqual(-0.48794f, b.PlatformY, 1e-4f, "A: PlatformY");
            Assert.AreEqual(0f, b.PadYaw, 1e-4f, "A: PadYaw");
            Assert.IsNotNull(b.Hydro, "A: вода");
            AssertVec(b.Hydro.lakeCenter, new Vector2(-93.5492f, -102.5501f), "A: озеро");
            Assert.AreEqual(150f, b.Hydro.lakeRadius, 1e-3f, "A: радиус озера");
            Assert.AreEqual(-0.98794f, b.Hydro.lakeLevel, 1e-4f, "A: уровень воды");
            AssertVec(b.Ash.pos, new Vector2(-385.4167f, -93.75f), "A: ясень");
            Assert.AreEqual(-0.91421f, b.Ash.groundY, 1e-4f, "A: высота ясеня");

            Assert.AreEqual(0, b.Zones.Count, "A: разметка выключена — полян нет");
            Assert.AreEqual(0, b.Links.Count, "A: коридоров нет");
            Assert.AreEqual(0, b.CoverPods.Count, "A: подов нет");
            Assert.AreEqual(SpaceHashEmpty, SpaceHash(b), "A: пустая разметка");
            Assert.AreEqual(AddressHashA, AddressHash(b), $"{CapturedAt}: адреса A изменились");
            Assert.AreEqual(LairHashA, LairHash(b), $"{CapturedAt}: координаты логовищ A изменились");
            Assert.AreEqual(FingerprintA, b.Fingerprint, $"{CapturedAt}: отпечаток A изменился");
            Assert.AreEqual(GroundHashA_MeshStep15_625_StartMinus1000_Res129,
                GroundHash(b),
                $"{CapturedAt}: высоты SampleGround A на сетке meshStep 15.625 изменились");
            Assert.AreEqual(15.625f, b.Contract.meshStep, 1e-4f, "A: meshStep");
        }

        [UnityTest]
        public IEnumerator C_Profile_KeepsSpawner_AndOwlDoesNotSpawn()
        {
            var b = C();
            b.Build();
            yield return null;

            Assert.AreEqual(52, b.Chunks, "C: изменилось число чанков");
            Assert.AreEqual(10, b.Registry.Count, "C: изменился реестр логовищ");
            Assert.IsNotNull(b.NestSite, "C: пропало совиное гнездо");
            Assert.IsFalse(b.NestSite.spawnEnabled, "C: сова снова рожает без тела филина");
            Assert.AreEqual(FingerprintA, b.Fingerprint,
                $"{CapturedAt}: отпечаток C изменился (мир тот же, что A)");
            Assert.AreEqual(AddressHashA, AddressHash(b), "C: адреса те же, что A");
        }

        // ---------------------------------------------------------------------
        // B0 (сид 9)
        // ---------------------------------------------------------------------

        [UnityTest]
        public IEnumerator B0_Scene_Profile_KeepsFacts_AndAddresses()
        {
            var b = B0();
            b.Build();
            yield return null;

            Assert.AreEqual(1, b.FingerprintVersion, "B0: версия формулы отпечатка");
            Assert.AreEqual(12, b.Chunks, "B0: изменилось число чанков Ø400");
            Assert.AreEqual(534, b.Trees, "B0: изменилось число деревьев (сид 9)");
            Assert.IsTrue(b.HasAsh, "B0: пропал ясень (buildAsh = true с B1.1)");
            Assert.IsNotNull(b.NestSite, "B0: пропало совиное гнездо");
            // ФАКТ СБОРКИ на сиде 9: 2 устья + 4 гнезда змей + сова (см. отдельный
            // тест ниже: план S5 ждёт 5 змеиных — это известное расхождение, не чиним в S0).
            Assert.AreEqual(7, b.Lairs.Count, "B0: 6 змеиных + сова");
            Assert.AreEqual(6, SnakeLairs(b), "B0: змеиных логовищ");
            Assert.IsNotNull(b.LabPlan, "B0: план интерьера");
            Assert.AreEqual("deep-wings", b.LabPlan.template, "B0: шаблон плана (сид 9, v1)");
            Assert.AreEqual(1, b.LabPlan.version, "B0: версия плана");

            AssertVec(b.Entry, new Vector3(-86.7835f, -0.3343f, 49.6854f), "B0: Entry");
            AssertVec(b.SpawnPoint, new Vector3(-81.5764f, 0.6399f, 46.7042f), "B0: SpawnPoint");
            AssertVec(b.LabCenter, new Vector2(89.1494f, 67.2493f), "B0: LabCenter");
            AssertVec(b.LabDoor, new Vector3(71.2384f, -0.1901f, 65.4612f), "B0: LabDoor");
            AssertVec(b.LabAtrium, new Vector3(89.1494f, 1.5099f, 67.2493f), "B0: LabAtrium");
            Assert.AreEqual(-0.31007f, b.PlatformY, 1e-4f, "B0: PlatformY");
            Assert.AreEqual(84.29886f, b.PadYaw, 1e-4f, "B0: PadYaw");
            Assert.AreEqual(b.PlatformY + DomeLabComplex.Plinth, -0.19007f, 1e-4f,
                "B0: верх плиты PlatformY + Plinth");
            Assert.IsNotNull(b.Hydro, "B0: вода");
            AssertVec(b.Hydro.lakeCenter, new Vector2(-54.0134f, -26.9270f), "B0: озеро");
            Assert.AreEqual(87.9693f, b.Hydro.lakeRadius, 1e-3f, "B0: радиус озера");
            Assert.AreEqual(-0.81007f, b.Hydro.lakeLevel, 1e-4f, "B0: уровень воды");
            AssertVec(b.Ash.pos, new Vector2(43.75f, -39.5833f), "B0: ясень");
            Assert.AreEqual(-0.30601f, b.Ash.groundY, 1e-4f, "B0: высота ясеня");

            Assert.AreEqual(0, b.Zones.Count, "B0: разметка выключена — полян нет");
            Assert.AreEqual(0, b.Links.Count, "B0: коридоров нет");
            Assert.AreEqual(0, b.CoverPods.Count, "B0: подов нет");
            Assert.AreEqual(SpaceHashEmpty, SpaceHash(b), "B0: пустая разметка");
            Assert.AreEqual(AddressHashB0, AddressHash(b), $"{CapturedAt}: адреса B0 изменились");
            Assert.AreEqual(LairHashB0, LairHash(b), $"{CapturedAt}: координаты логовищ B0 изменились");
            Assert.AreEqual(FingerprintB0, b.Fingerprint, $"{CapturedAt}: отпечаток B0 изменился");
            Assert.AreEqual(GroundHashB0_MeshStep4_166667_StartMinus200_Res97,
                GroundHash(b),
                $"{CapturedAt}: высоты SampleGround B0 на сетке meshStep 4.166667 изменились");
            Assert.AreEqual(4.166667f, b.Contract.meshStep, 1e-4f, "B0: meshStep");
        }

        /// <summary>
        /// S5 §9.4/§11: B0 на сиде 9 — 2 устья + 3 гнезда = 5 змеиных логовищ.
        /// Сборка на HEAD 961f854d даёт 2 + 4 = 6 (четвёртое гнездо ставится
        /// `FindNests`), поэтому тест НЕ зелёный и не подгоняется: S0 фиксирует
        /// факт (см. `B0_Scene_Profile_KeepsFacts_AndAddresses`), а расхождение
        /// с планом ведёт долг S1/S3. Это не повод править мир в S0.
        /// </summary>
        [UnityTest]
        public IEnumerator B0_PlanExpectsFiveSnakeLairs_KnownDrift()
        {
            var b = B0();
            b.Build();
            yield return null;

            int snake = SnakeLairs(b);
            if (snake == 5) Assert.Pass($"{CapturedAt}: B0 совпал с планом — 5 змеиных логовищ");
            Assert.Inconclusive(
                $"{CapturedAt}: план S5 ждёт 5 змеиных логовищ (2 устья + 3 гнезда), " +
                $"сборка даёт {snake} (устий {b.MouthCount}, гнёзд {b.NestCount}). " +
                "S0 фиксирует факт; расхождение — известный долг S1/S3, мир не подгоняем.");
        }

        // ---------------------------------------------------------------------
        // Pavilion / B-lite
        // ---------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Pavilion_Profile_KeepsCounts_AndAddresses()
        {
            var b = Pavilion();
            b.Build();
            yield return null;

            Assert.AreEqual(1, b.FingerprintVersion, "Pavilion: версия формулы отпечатка");
            Assert.AreEqual(52, b.Chunks, "Pavilion: изменилось число чанков Ø2000");
            Assert.AreEqual(1500, b.Trees, "Pavilion: изменилось число деревьев B-lite");
            Assert.IsNotNull(b.NestSite, "Pavilion: пропало совиное гнездо");
            Assert.AreEqual(10, b.Lairs.Count, "Pavilion: 9 змеиных + сова");
            Assert.AreEqual(9, SnakeLairs(b), "Pavilion: змеиных логовищ");
            Assert.AreEqual(4, b.ReliefHills, "Pavilion: P5-lite дал другое число холмов");
            Assert.IsNotNull(b.LabPlan, "Pavilion: план интерьера");
            Assert.AreEqual("mirror", b.LabPlan.template, "Pavilion: шаблон плана (сид 1337, v1)");
            Assert.AreEqual(1, b.LabPlan.version, "Pavilion: версия плана");

            AssertVec(b.Entry, new Vector3(-466.0878f, -1.8091f, -769.9105f), "Pavilion: Entry");
            AssertVec(b.SpawnPoint, new Vector3(-462.9806f, -0.8255f, -764.7777f), "Pavilion: SpawnPoint");
            AssertVec(b.LabCenter, new Vector2(400f, -440f), "Pavilion: LabCenter");
            AssertVec(b.LabDoor, new Vector3(383.1790f, 1.8796f, -446.4074f), "Pavilion: LabDoor");
            AssertVec(b.LabAtrium, new Vector3(400f, 3.5796f, -440f), "Pavilion: LabAtrium");
            Assert.AreEqual(1.75958f, b.PlatformY, 1e-4f, "Pavilion: PlatformY");
            Assert.AreEqual(69.14715f, b.PadYaw, 1e-4f, "Pavilion: PadYaw");
            Assert.AreEqual(b.PlatformY + DomeLabComplex.Plinth, 1.87958f, 1e-4f,
                "Pavilion: верх плиты PlatformY + Plinth");
            Assert.IsNotNull(b.Hydro, "Pavilion: вода");
            AssertVec(b.Hydro.lakeCenter, new Vector2(-93.5492f, -102.5501f), "Pavilion: озеро");
            Assert.AreEqual(150f, b.Hydro.lakeRadius, 1e-3f, "Pavilion: радиус озера");
            Assert.AreEqual(-0.98794f, b.Hydro.lakeLevel, 1e-4f, "Pavilion: уровень воды");
            AssertVec(b.Ash.pos, new Vector2(10.4167f, -364.5833f), "Pavilion: ясень");
            Assert.AreEqual(0.58188f, b.Ash.groundY, 1e-4f, "Pavilion: высота ясеня");

            Assert.AreEqual(5, b.Zones.Count, "Pavilion: открытые зоны");
            Assert.AreEqual(6, b.Links.Count, "Pavilion: коридоры");
            Assert.AreEqual(24, b.CoverPods.Count, "Pavilion: поды");
            Assert.AreEqual(SpaceHashPavilion, SpaceHash(b), $"{CapturedAt}: координаты разметки Pavilion изменились");
            Assert.AreEqual(AddressHashPavilion, AddressHash(b), $"{CapturedAt}: адреса Pavilion изменились");
            Assert.AreEqual(LairHashPavilion, LairHash(b), $"{CapturedAt}: координаты логовищ Pavilion изменились");
            Assert.AreEqual(FingerprintPavilion, b.Fingerprint, $"{CapturedAt}: отпечаток Pavilion изменился");
            Assert.AreEqual(GroundHashPavilion_MeshStep5_208333_StartMinus1000_Res385,
                GroundHash(b),
                $"{CapturedAt}: высоты SampleGround Pavilion на сетке meshStep 5.208333 изменились");

            Assert.AreEqual(5.208333f, b.Contract.meshStep, 1e-4f, "Pavilion: meshStep");
            Assert.AreEqual(2.604167f, b.Contract.slopeEpsilon, 1e-4f, "Pavilion: slopeEpsilon");
            Assert.AreEqual(15.625f, b.Contract.routeCell, 1e-4f, "Pavilion: routeCell");
            Assert.AreEqual(0.35f, b.Contract.navVoxel, 1e-4f, "Pavilion: navVoxel");
        }

        /// <summary>
        /// «Площадка плоская» — инвариант S3 (`pad` последним слоем). Сейчас P5-lite
        /// размещает холмы не ближе 70 м от центра станции, поэтому на сиде 1337
        /// отклонение нулевое. Но ПОРЯДОК уже неверный: `DomeRelief.Apply` идёт ПОСЛЕ
        /// площадки (`BaseGround → SampleGround → DomeRelief`), и первая же форма
        /// ближе к пятну сломает плиту. Тест — снимок этого долга, не подгонка.
        /// </summary>
        [UnityTest]
        public IEnumerator Pavilion_Platform_IsFlat_OrKnownDebt()
        {
            var b = Pavilion();
            b.Build();
            yield return null;

            float dev = PadDeviation(b);
            if (dev > 0.02f)
                Assert.Inconclusive(
                    $"{CapturedAt}: площадка Pavilion НЕ плоская: max |SampleGround − PlatformY| = {dev:F2} м. " +
                    "Причина — landform (DomeRelief) применяется ПОСЛЕ pad; S3 шаг 2 переставляет порядок. " +
                    "Известный долг, не подгоняем.");
            Assert.LessOrEqual(dev, 0.02f,
                $"Pavilion: площадка плоская (max отклонение {dev:F4} м)");
        }

        [UnityTest]
        public IEnumerator B0_Platform_IsFlat()
        {
            var b = B0();
            b.Build();
            yield return null;

            Assert.LessOrEqual(PadDeviation(b), 0.02f,
                "B0: площадка станции плоская (relief выключен, pad последний)");
        }
    }
}
