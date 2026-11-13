using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    /// <summary>
    /// П-3 леса (13.11): `ForestPavilion` — первая зона вечного цикла (решение геймдизайнера 13.11). Грузится НАСТОЯЩАЯ
    /// сцена, как в игре: мир строится без архивных ассетов (каньон и акведук заморожены, ассеты в теге
    /// `archive/les-zamorozka-2026-11-12`), объекты вечного цикла на месте, игрок стоит на навмеше, логово рожает,
    /// небо идёт по игровым часам. Любой `Debug.LogError` при загрузке валит тест сам (Unity Test Framework).
    /// Мир Ø2000 строится секунды — прогон ручной, не гейт.
    /// </summary>
    public class ForestPavilionFirstZoneTests
    {
        const string SceneName = "ForestPavilion";
        ForestBuilder builder;

        [UnitySetUp]
        public IEnumerator Load()
        {
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;
            builder = Object.FindAnyObjectByType<ForestBuilder>();
            Assert.IsNotNull(builder, "в павильоне нет ForestBuilder");
        }

        // Выгрузка, как у сторожа арены: сцена грузится Single, без выгрузки её игрок достался бы следующим тестам.
        [UnityTearDown]
        public IEnumerator Unload()
        {
            Time.timeScale = 1f;
            ForestClimate.ResetStatic();
            var scene = SceneManager.GetSceneByName(SceneName);
            if (!scene.IsValid() || !scene.isLoaded) yield break;
            var empty = SceneManager.CreateScene("Пусто-после-павильона");
            SceneManager.SetActiveScene(empty);
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator World_Builds_WithoutArchivedAssets()
        {
            Assert.IsNotNull(builder.BuiltRoot, "мир павильона собран в Awake");
            Assert.IsFalse(builder.buildCanyon, "каньон заморожен — в первой зоне выключен");
            Assert.IsFalse(builder.buildAqueduct, "акведук заморожен — в первой зоне выключен");
            Assert.IsNull(GameObject.Find("CanyonAuthor"), "архивный меш каньона в мир не попал");
            Assert.Greater(builder.Chunks, 0, "рельеф собран");
            Assert.Greater(builder.Trees, 0, "лес посажен");
            yield return null;
        }

        [UnityTest]
        public IEnumerator EndlessLoop_Objects_ArePresent()
        {
            Assert.IsNotNull(Object.FindAnyObjectByType<WorldClock>(), "игровые часы — один носитель времени");
            Assert.IsNotNull(Object.FindAnyObjectByType<BossChimeraSpawner>(), "босс выходит из лаборатории");
            Assert.IsNotNull(Object.FindAnyObjectByType<PackCoordinator>(), "стая волков");
            Assert.IsNotNull(Object.FindAnyObjectByType<ForageSpawner>(), "кусты — корм лося");
            Assert.IsNotNull(Object.FindAnyObjectByType<IndividualityConfig>(), "разброс особей");
            Assert.IsNotNull(Object.FindAnyObjectByType<DebugHud>(), "дев-панель и итог смерти");
            Assert.IsNotNull(Object.FindAnyObjectByType<NoiseWaves>(), "волны звука");
            Assert.AreEqual(4, builder.speciesPool.Count, "пул — четыре зверя леса");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Player_StandsOnNavMesh()
        {
            var pc = Object.FindAnyObjectByType<PlayerController>();
            Assert.IsNotNull(pc, "в павильоне есть игрок");
            Assert.IsNotNull(pc.GetComponent<PlayerDeath>(), "смерть без перезагрузки — на игроке");
            yield return null;
            Assert.IsTrue(NavMesh.SamplePosition(pc.transform.position, out _, 3f, NavMesh.AllAreas),
                "игрок появился у прохода, а не в скале или в воздухе");
        }

        [UnityTest]
        public IEnumerator SnakeLair_Births_ChainedSnake()
        {
            Assert.IsNotNull(builder.Spawner, "спавнер логов есть");
            Assert.Greater(builder.Registry.Count, 0, "логова зарегистрированы");
            builder.Spawner.Tick(120f); // очередь логов: несколько циклов рождения разом
            yield return null;
            Assert.Greater(builder.Spawner.SpawnedCount, 0, "логово родило");
            var born = builder.Spawner.lastSpawn;
            Assert.IsNotNull(born, "рождённое тело есть");
            if (born.Chassis != null && born.Chassis.speciesName == "Змея")
                Assert.IsNotNull(born.GetComponent<SnakeBodyChain>(), "змея из логова — с цепью звеньев");
        }

        /// <summary>Спека «логово по законам» §5 п. 6: у каждого из четырёх зверей есть дом, и дом рожает свой вид.</summary>
        [UnityTest]
        public IEnumerator EveryAnimal_HasLair_ThatBirthsItsKind()
        {
            Assert.IsTrue(builder.buildAnimalLairs, "логова зверей включены в профиле первой зоны");
            foreach (var species in new[] { "Волк", "Змея", "Лось", "Ёж" })
            {
                LairSite site = null;
                foreach (var l in builder.Lairs) if (l != null && l.speciesName == species) { site = l; break; }
                Assert.IsNotNull(site, $"у вида «{species}» есть логово");
                Assert.IsTrue(NavMesh.SamplePosition(site.transform.position, out _, 4f, NavMesh.AllAreas),
                    $"логово «{species}» на проходимой земле");
                site.pendingSpawns = 1;
                Assert.IsTrue(builder.Spawner.DrainOnce(site), $"логово «{species}» рожает");
                Assert.AreEqual(species, builder.Spawner.lastSpawn.Chassis.speciesName, $"логово «{species}» рожает свой вид");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator BossExit_IsLabDoor_OnNavMesh()
        {
            Assert.AreSame(builder, BossChimeraSpawner.ExitProvider, "выход босса отдаёт мир павильона");
            Assert.IsTrue(builder.TryGetExit(out Vector3 exit, out Quaternion rot), "адрес выхода есть после сборки");
            Assert.IsNotNull(builder.LabComplex, "лаборатория-комплекс построена");
            Vector3 entry = builder.Entry;
            Assert.Greater(Vector2.Distance(new Vector2(exit.x, exit.z), new Vector2(entry.x, entry.z)), 20f,
                "босс выходит из лаборатории, а не из входа в павильон");
            Assert.IsTrue(NavMesh.SamplePosition(exit, out _, 3f, NavMesh.AllAreas),
                "у двери лаборатории есть навмеш — босс сможет идти");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Sky_FollowsWorldClock()
        {
            var clock = Object.FindAnyObjectByType<WorldClock>();
            var climate = Object.FindAnyObjectByType<ForestClimate>();
            Assert.IsNotNull(climate, "климат павильона включён");
            clock.Advance(5f / clock.HoursPerSecond);
            yield return null;
            yield return null;
            float expected = clock.HourOfDay / 24f * DayNightCycle.DayLengthMinutes;
            Assert.AreEqual(expected, climate.timeMinutes, 0.1f, "небо павильона идёт по игровым часам");
        }
    }
}
