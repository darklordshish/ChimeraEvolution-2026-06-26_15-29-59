using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// S0 ландшафтного пакета (2026-11-01, HEAD 961f854d) — базлайн ДО правок мира.
    /// Здесь только ЧИСТЫЕ величины: хэш поля H0 v1 и контракт выборки. Полный
    /// <see cref="WorldFingerprint"/> и высотный хэш <c>SampleGround</c> требуют собранного
    /// мира, поэтому их константы заморожены в PlayMode <c>ForestBaselineTests</c>.
    /// Числа сняты с фактической сборки/поля на указанном HEAD, не из документов.
    /// <para>
    /// ВАЖНО: <c>DomeHeightField.SampleGridHash</c> — это <b>H0 v1</b> (сырое поле, шаг 1 м),
    /// и он НЕ равен высотному хэшу <c>SampleGround</c> (тот считается по контракту
    /// <see cref="SamplingContract.meshStep"/> и включает площадку/рельеф). Держим их
    /// разными константами и разными тестами: смешав, нельзя понять, что именно сдвинулось.
    /// </para>
    /// </summary>
    public class BaselineFingerprintTests
    {
        /// <summary>Дата и HEAD, на которых снят базлайн. Менять только вместе с числами.</summary>
        const string CapturedAt = "2026-11-01 @ 961f854d";

        const float HashStepMeters = 1f;

        // DomeHeightField.SampleGridHash(cfg v1, resolution, step = 1 м) — ТОЛЬКО H0 v1.
        const ulong H0V1_Seed1337_R128 = 16901156773121752757UL;
        const ulong H0V1_Seed1337_R512 = 14698614694802551876UL;
        const ulong H0V1_Seed9_R128 = 16216543706081559679UL;
        const ulong H0V1_Seed9_R512 = 13045960235710047884UL;

        static DomeGenConfigSO Config(long seed, float diameter = 2000f, float chunk = 250f)
        {
            var cfg = ScriptableObject.CreateInstance<DomeGenConfigSO>();
            cfg.seed = seed;
            cfg.randomSeed = false;
            cfg.mapDiameter = diameter;
            cfg.chunkSize = chunk;
            return cfg;
        }

        static ulong Hash(long seed, int resolution)
        {
            var cfg = Config(seed);
            try
            {
                return DomeHeightField.SampleGridHash(cfg, resolution, HashStepMeters);
            }
            finally
            {
                Object.DestroyImmediate(cfg);
            }
        }

        [Test]
        public void H0_Seed1337_V1_GridHash_Frozen()
        {
            Assert.AreEqual(H0V1_Seed1337_R128, Hash(1337L, 128),
                $"{CapturedAt}: H0 v1 сида 1337 (A/C/B-lite) на 128 изменился — S1 не имеет права трогать v1");
            Assert.AreEqual(H0V1_Seed1337_R512, Hash(1337L, 512),
                $"{CapturedAt}: H0 v1 сида 1337 на 512 изменился");
        }

        [Test]
        public void H0_Seed9_V1_GridHash_Frozen()
        {
            Assert.AreEqual(H0V1_Seed9_R128, Hash(9L, 128),
                $"{CapturedAt}: H0 v1 сида 9 (B0) на 128 изменился");
            Assert.AreEqual(H0V1_Seed9_R512, Hash(9L, 512),
                $"{CapturedAt}: H0 v1 сида 9 (B0) на 512 изменился");
        }

        [Test]
        public void SamplingContract_A_IsFrozen()
        {
            var cfg = Config(1337L, 2000f, 250f);
            try
            {
                var c = SamplingContract.From(cfg, 16);
                Assert.AreEqual(15.625f, c.meshStep, 1e-4f, "A: meshStep");
                Assert.AreEqual(7.8125f, c.slopeEpsilon, 1e-4f, "A: slopeEpsilon");
                Assert.AreEqual(15.625f, c.routeCell, 1e-4f, "A: routeCell");
                Assert.AreEqual(0.35f, c.navVoxel, 1e-4f, "A: navVoxel");
            }
            finally
            {
                Object.DestroyImmediate(cfg);
            }
        }

        [Test]
        public void SamplingContract_B0_IsFrozen()
        {
            var cfg = Config(9L, 400f, 100f);
            try
            {
                var c = SamplingContract.From(cfg, 24);
                Assert.AreEqual(4.166667f, c.meshStep, 1e-4f, "B0: meshStep");
                Assert.AreEqual(2.083333f, c.slopeEpsilon, 1e-4f, "B0: slopeEpsilon");
                Assert.AreEqual(3.125f, c.routeCell, 1e-4f, "B0: routeCell");
                Assert.AreEqual(0.35f, c.navVoxel, 1e-4f, "B0: navVoxel");
            }
            finally
            {
                Object.DestroyImmediate(cfg);
            }
        }

        [Test]
        public void SamplingContract_Pavilion_IsFrozen()
        {
            var cfg = Config(1337L, 2000f, 250f);
            try
            {
                var c = SamplingContract.From(cfg, 48);
                Assert.AreEqual(5.208333f, c.meshStep, 1e-4f, "Pavilion: meshStep");
                Assert.AreEqual(2.604167f, c.slopeEpsilon, 1e-4f, "Pavilion: slopeEpsilon");
                Assert.AreEqual(15.625f, c.routeCell, 1e-4f, "Pavilion: routeCell");
                Assert.AreEqual(0.35f, c.navVoxel, 1e-4f, "Pavilion: navVoxel");
            }
            finally
            {
                Object.DestroyImmediate(cfg);
            }
        }

        /// <summary>
        /// S0 НЕ вводит worldVersion: сейчас версию поля задаёт только <c>DomeGenConfigSO.version</c>.
        /// Фиксируем факт v1, чтобы S1 менял его осознанно, а не «по дороге».
        /// </summary>
        [Test]
        public void WorldVersion_IsV1()
        {
            var cfg = Config(1337L);
            try
            {
                Assert.AreEqual(1, cfg.version, $"{CapturedAt}: версия поля уже не v1");
            }
            finally
            {
                Object.DestroyImmediate(cfg);
            }
        }

        /// <summary>Разметка B-lite входит в отпечаток мира — её версия тоже заморожена.</summary>
        [Test]
        public void SpaceLayoutVersion_IsV1()
        {
            Assert.AreEqual(1, ForestSpace.LayoutVersion,
                $"{CapturedAt}: версия разметки пространства изменилась — отпечатки A/C/B0/Pavilion сдвинутся");
        }

        /// <summary>
        /// Формула отпечатка версионируется отдельно от поля и разметки. Пока версия 1,
        /// PlayMode-константы <c>ForestBaselineTests</c> обязаны совпадать с фактом.
        /// </summary>
        [Test]
        public void FingerprintVersion_IsV1()
        {
            Assert.AreEqual(1, WorldFingerprint.Version,
                $"{CapturedAt}: версия формулы отпечатка изменилась — нужен новый эталон, иначе «тот же сид» ничего не доказывает");
        }

        /// <summary>
        /// Гоча проекта: новое сериализованное поле у старой сцены приходит НУЛЁМ.
        /// Для <c>fingerprintVersion</c> 0 обязан читаться как действующая v1,
        /// а не как «версия 0» (иначе A/C/B0 молча получат другую формулу).
        /// </summary>
        [Test]
        public void FingerprintVersion_ZeroMeansV1()
        {
            var go = new GameObject("~FingerprintVersionProbe");
            try
            {
                var b = go.AddComponent<ForestBuilder>();
                b.fingerprintVersion = 0;
                Assert.AreEqual(WorldFingerprint.Version, b.FingerprintVersion,
                    $"{CapturedAt}: 0 в поле — «не настроено», действует v1");
                b.fingerprintVersion = 7;
                Assert.AreEqual(7, b.FingerprintVersion,
                    $"{CapturedAt}: явная версия поля не подменяется");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>
        /// Действующий конфиг сцен A/C/B0/Pavilion — дефолты <see cref="DomeGenConfigSO"/>
        /// (в сценах <c>config = null</c>; это проверяет <c>ForestSceneBaselineTests</c>).
        /// Заморожены, потому что любое из этих чисел меняет пол даже без правок кода мира.
        /// </summary>
        [Test]
        public void Config_Defaults_AreFrozen()
        {
            var cfg = ScriptableObject.CreateInstance<DomeGenConfigSO>();
            try
            {
                Assert.AreEqual(6f, cfg.amplitude, 1e-6f, $"{CapturedAt}: amplitude");
                Assert.AreEqual(0.02f, cfg.baseFrequency, 1e-6f, $"{CapturedAt}: baseFrequency");
                Assert.AreEqual(5, cfg.octaves, $"{CapturedAt}: octaves");
                Assert.AreEqual(5, cfg.ridgedOctaves, $"{CapturedAt}: ridgedOctaves");
                Assert.AreEqual(4, cfg.warpOctaves, $"{CapturedAt}: warpOctaves");
                Assert.AreEqual(6f, cfg.warpStrength, 1e-6f, $"{CapturedAt}: warpStrength");
                Assert.AreEqual(35f, cfg.maxSlopeDegrees, 1e-6f, $"{CapturedAt}: maxSlopeDegrees");
                Assert.AreEqual(-1.5f, cfg.waterLevel, 1e-6f, $"{CapturedAt}: waterLevel");
                Assert.AreEqual(0.35f, cfg.navVoxelSize, 1e-6f, $"{CapturedAt}: navVoxelSize");
                Assert.AreEqual(112f, cfg.navTileSize, 1e-6f, $"{CapturedAt}: navTileSize");
            }
            finally
            {
                Object.DestroyImmediate(cfg);
            }
        }
    }
}
