using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// S0 ландшафтного пакета: эталон снимается СО СЦЕН, а не из хелперов тестов.
    /// Повод — PlayMode-хелпер собирался руками и мог незаметно разойтись с
    /// `ForestSandbox`/`ForestFragment`/`ForestPavilion`. Здесь читаются РЕАЛЬНЫЕ
    /// сериализованные поля <see cref="ForestBuilder"/> плюс действующий конфиг
    /// (в этих сценах `config = null`, то есть дефолты <see cref="DomeGenConfigSO"/>,
    /// замороженные в <c>BaselineFingerprintTests.Config_Defaults_AreFrozen</c>).
    /// PlayMode <c>ForestBaselineTests</c> повторяет ровно этот профиль.
    /// </summary>
    public class ForestSceneBaselineTests
    {
        const string CapturedAt = "2026-11-01 @ 961f854d";
        const string FirstZoneAt = "2026-11-13 (П-3: павильон — первая зона)";
        const string Sandbox = "Assets/Scenes/ForestSandbox.unity";
        const string Fragment = "Assets/Scenes/ForestFragment.unity";
        const string Pavilion = "Assets/Scenes/ForestPavilion.unity";

        [Test]
        public void Sandbox_Profile_IsFrozen()
        {
            ReadProfile(Sandbox, b =>
            {
                // A / C: Ø2000, сид 1337, quads 16, без новых флагов.
                Assert.AreEqual(1337L, b.seed, $"{CapturedAt}: {Sandbox} seed");
                Assert.IsFalse(b.randomSeed, $"{CapturedAt}: {Sandbox} randomSeed");
                Assert.AreEqual(2000f, b.mapDiameter, 1e-4f, $"{CapturedAt}: {Sandbox} mapDiameter");
                Assert.AreEqual(250f, b.chunkSize, 1e-4f, $"{CapturedAt}: {Sandbox} chunkSize");
                Assert.AreEqual(16, b.terrainQuads, $"{CapturedAt}: {Sandbox} terrainQuads");
                Assert.AreEqual(0, b.treeCount, $"{CapturedAt}: {Sandbox} treeCount");
                Assert.IsFalse(b.patchyFlora, $"{CapturedAt}: {Sandbox} patchyFlora");
                Assert.IsTrue(b.buildOnAwake, $"{CapturedAt}: {Sandbox} buildOnAwake");
                Assert.IsNull(b.config, $"{CapturedAt}: {Sandbox} назначенный config — это новый базлайн");
                Assert.IsFalse(b.spawnAtEntry, $"{CapturedAt}: {Sandbox} spawnAtEntry");
                Assert.IsTrue(b.buildAsh, $"{CapturedAt}: {Sandbox} buildAsh");
                Assert.IsFalse(b.buildRavines, $"{CapturedAt}: {Sandbox} buildRavines");
                Assert.IsFalse(b.buildTerrace, $"{CapturedAt}: {Sandbox} buildTerrace");
                Assert.IsFalse(b.buildCanyon, $"{CapturedAt}: {Sandbox} buildCanyon");
                Assert.IsFalse(b.buildStationLoop, $"{CapturedAt}: {Sandbox} buildStationLoop");
                Assert.IsFalse(b.buildStationHill, $"{CapturedAt}: {Sandbox} buildStationHill");
                Assert.IsFalse(b.buildLabComplex, $"{CapturedAt}: {Sandbox} buildLabComplex");
                Assert.IsFalse(b.buildLabPlan, $"{CapturedAt}: {Sandbox} buildLabPlan");
                Assert.IsFalse(b.buildClimate, $"{CapturedAt}: {Sandbox} buildClimate");
                Assert.AreEqual(LabStyle.Bastion, b.labStyle, $"{CapturedAt}: {Sandbox} labStyle");
                Assert.AreEqual(LabEntrance.Rock, b.labEntrance, $"{CapturedAt}: {Sandbox} labEntrance");
                Assert.AreEqual(150f, b.minLabDistance, 1e-4f, $"{CapturedAt}: {Sandbox} minLabDistance");
                Assert.AreEqual(1, b.speciesPool.Count, $"{CapturedAt}: {Sandbox} speciesPool");
                AssertDefaults(b);
            });
        }

        [Test]
        public void Fragment_Profile_IsFrozen()
        {
            ReadProfile(Fragment, b =>
            {
                // B0 — ТОЛЬКО сид 9 (решение 01.11). Сид 4 — исторический, не базлайн.
                Assert.AreEqual(9L, b.seed, $"{CapturedAt}: {Fragment} seed (B0 = 9)");
                Assert.IsFalse(b.randomSeed, $"{CapturedAt}: {Fragment} randomSeed");
                Assert.AreEqual(400f, b.mapDiameter, 1e-4f, $"{CapturedAt}: {Fragment} mapDiameter");
                Assert.AreEqual(100f, b.chunkSize, 1e-4f, $"{CapturedAt}: {Fragment} chunkSize");
                Assert.AreEqual(24, b.terrainQuads, $"{CapturedAt}: {Fragment} terrainQuads");
                Assert.AreEqual(0, b.treeCount, $"{CapturedAt}: {Fragment} treeCount");
                Assert.IsFalse(b.patchyFlora, $"{CapturedAt}: {Fragment} patchyFlora");
                Assert.IsTrue(b.buildOnAwake, $"{CapturedAt}: {Fragment} buildOnAwake");
                Assert.IsNull(b.config, $"{CapturedAt}: {Fragment} назначенный config — это новый базлайн");
                Assert.IsTrue(b.spawnAtEntry, $"{CapturedAt}: {Fragment} spawnAtEntry");
                Assert.IsTrue(b.buildAsh, $"{CapturedAt}: {Fragment} buildAsh");
                Assert.IsTrue(b.buildRavines, $"{CapturedAt}: {Fragment} buildRavines");
                Assert.IsFalse(b.buildTerrace, $"{CapturedAt}: {Fragment} buildTerrace");
                Assert.IsFalse(b.buildCanyon, $"{CapturedAt}: {Fragment} buildCanyon");
                Assert.IsFalse(b.buildStationLoop, $"{CapturedAt}: {Fragment} buildStationLoop");
                Assert.IsFalse(b.buildStationHill, $"{CapturedAt}: {Fragment} buildStationHill");
                Assert.IsTrue(b.buildLabComplex, $"{CapturedAt}: {Fragment} buildLabComplex");
                Assert.IsTrue(b.buildLabPlan, $"{CapturedAt}: {Fragment} buildLabPlan");
                Assert.AreEqual(1, b.labPlanVersion, $"{CapturedAt}: {Fragment} labPlanVersion");
                Assert.IsTrue(b.buildClimate, $"{CapturedAt}: {Fragment} buildClimate");
                Assert.AreEqual(LabStyle.InSlope, b.labStyle, $"{CapturedAt}: {Fragment} labStyle");
                Assert.AreEqual(LabEntrance.Underground, b.labEntrance, $"{CapturedAt}: {Fragment} labEntrance");
                Assert.AreEqual(150f, b.minLabDistance, 1e-4f, $"{CapturedAt}: {Fragment} minLabDistance");
                Assert.AreEqual(0, b.speciesPool.Count, $"{CapturedAt}: {Fragment} speciesPool");
                AssertDefaults(b);
            });
        }

        [Test]
        public void Pavilion_Profile_IsFrozen()
        {
            ReadProfile(Pavilion, b =>
            {
                // B-lite: Ø2000, сид 1337, quads 48, пятна, 1500 деревьев.
                Assert.AreEqual(1337L, b.seed, $"{CapturedAt}: {Pavilion} seed");
                Assert.IsFalse(b.randomSeed, $"{CapturedAt}: {Pavilion} randomSeed");
                Assert.AreEqual(2000f, b.mapDiameter, 1e-4f, $"{CapturedAt}: {Pavilion} mapDiameter");
                Assert.AreEqual(250f, b.chunkSize, 1e-4f, $"{CapturedAt}: {Pavilion} chunkSize");
                Assert.AreEqual(48, b.terrainQuads, $"{CapturedAt}: {Pavilion} terrainQuads");
                Assert.AreEqual(1500, b.treeCount, $"{CapturedAt}: {Pavilion} treeCount");
                Assert.IsTrue(b.patchyFlora, $"{CapturedAt}: {Pavilion} patchyFlora");
                Assert.IsTrue(b.buildOnAwake, $"{CapturedAt}: {Pavilion} buildOnAwake");
                Assert.IsNull(b.config, $"{CapturedAt}: {Pavilion} назначенный config — это новый базлайн");
                Assert.IsTrue(b.spawnAtEntry, $"{CapturedAt}: {Pavilion} spawnAtEntry");
                Assert.IsTrue(b.buildAsh, $"{CapturedAt}: {Pavilion} buildAsh");
                Assert.IsFalse(b.buildRavines, $"{CapturedAt}: {Pavilion} buildRavines");
                Assert.IsTrue(b.buildTerrace, $"{CapturedAt}: {Pavilion} buildTerrace");
                // 13.11, П-3: павильон — первая зона вечного цикла; каньон заморожен (тег-архив), пул — четыре зверя.
                Assert.IsFalse(b.buildCanyon, $"{FirstZoneAt}: {Pavilion} buildCanyon");
                Assert.IsTrue(b.buildStationLoop, $"{CapturedAt}: {Pavilion} buildStationLoop");
                Assert.IsTrue(b.buildStationHill, $"{CapturedAt}: {Pavilion} buildStationHill");
                Assert.IsTrue(b.buildLabComplex, $"{CapturedAt}: {Pavilion} buildLabComplex");
                Assert.IsTrue(b.buildLabPlan, $"{CapturedAt}: {Pavilion} buildLabPlan");
                Assert.AreEqual(1, b.labPlanVersion, $"{CapturedAt}: {Pavilion} labPlanVersion");
                Assert.IsTrue(b.buildClimate, $"{CapturedAt}: {Pavilion} buildClimate");
                Assert.AreEqual(LabStyle.Bastion, b.labStyle, $"{CapturedAt}: {Pavilion} labStyle");
                Assert.AreEqual(LabEntrance.Rock, b.labEntrance, $"{CapturedAt}: {Pavilion} labEntrance");
                Assert.AreEqual(150f, b.minLabDistance, 1e-4f, $"{CapturedAt}: {Pavilion} minLabDistance");
                Assert.AreEqual(4, b.speciesPool.Count, $"{FirstZoneAt}: {Pavilion} speciesPool");
                AssertDefaults(b);
            });
        }

        static void ReadProfile(string path, System.Action<ForestBuilder> check)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var builders = Object.FindObjectsByType<ForestBuilder>(FindObjectsInactive.Include)
                    .Where(b => b.gameObject.scene == scene).ToArray();
                Assert.AreEqual(1, builders.Length, $"{path}: ровно один ForestBuilder — это профиль мира");
                check(builders[0]);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        /// <summary>В этих сценах config == null → действуют дефолты. Проверяем, что они не «молчат».</summary>
        static void AssertDefaults(ForestBuilder b)
        {
            // Старые сцены не получают поле → 0. Это НЕ версия 0: действует v1.
            Assert.AreEqual(0, b.fingerprintVersion, $"{b.gameObject.scene.path}: fingerprintVersion (0 = «не настроено»)");
            Assert.AreEqual(WorldFingerprint.Version, b.FingerprintVersion,
                $"{b.gameObject.scene.path}: 0 читается как действующая v1");
            Assert.AreEqual(0, b.worldVersion, $"{b.gameObject.scene.path}: worldVersion (0 = «не настроено»)");
            Assert.AreEqual(1, b.WorldVersion, $"{b.gameObject.scene.path}: 0 = не настроено → v1");
            // G7 шаг 1: treeVersion — v2-поле. У v1-сцен его нет, и 0 не значит «дерево v0».
            Assert.AreEqual(0, b.treeVersion, $"{b.gameObject.scene.path}: treeVersion (0 = «не настроено»)");
            Assert.AreEqual(0, b.TreeVersion, $"{b.gameObject.scene.path}: у v1 дерева нет");

            var cfg = b.config != null ? b.config : ScriptableObject.CreateInstance<DomeGenConfigSO>();
            try
            {
                Assert.AreEqual(1, cfg.version, "действующий конфиг: поле v1");
                Assert.AreEqual(6f, cfg.amplitude, 1e-6f, "действующий конфиг: amplitude");
                Assert.AreEqual(0.02f, cfg.baseFrequency, 1e-6f, "действующий конфиг: baseFrequency");
                Assert.AreEqual(5, cfg.octaves, "действующий конфиг: octaves");
                Assert.AreEqual(5, cfg.ridgedOctaves, "действующий конфиг: ridgedOctaves");
                Assert.AreEqual(4, cfg.warpOctaves, "действующий конфиг: warpOctaves");
                Assert.AreEqual(6f, cfg.warpStrength, 1e-6f, "действующий конфиг: warpStrength");
                Assert.AreEqual(35f, cfg.maxSlopeDegrees, 1e-6f, "действующий конфиг: maxSlopeDegrees");
                Assert.AreEqual(-1.5f, cfg.waterLevel, 1e-6f, "действующий конфиг: waterLevel");
                Assert.AreEqual(0.35f, cfg.navVoxelSize, 1e-6f, "действующий конфиг: navVoxelSize");
                Assert.AreEqual(112f, cfg.navTileSize, 1e-6f, "действующий конфиг: navTileSize");
            }
            finally
            {
                if (b.config == null) Object.DestroyImmediate(cfg);
            }
        }
    }
}
