using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    /// <summary>
    /// Базлайн леса: A/C/B0 должны оставаться теми же по ключевым фактам,
    /// пока меняется только станция/павильон. Это не проверка красоты —
    /// числа берутся из уже принятых срезов, чтобы карта не «уехала» молча.
    /// </summary>
    public class ForestBaselineTests
    {
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

        ForestBuilder Make(float diameter, float chunk, int quads, long seed,
            bool ash, bool spawnAtEntry)
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
            builder.buildAsh = ash;
            builder.spawnAtEntry = spawnAtEntry;
            builder.speciesPool = new System.Collections.Generic.List<SpeciesSO>();
            host.SetActive(true);
            return builder;
        }

        [UnityTest]
        public IEnumerator A_Profile_KeepsFiftyTwoChunks_AndLairSet()
        {
            var b = Make(2000f, 250f, 16, 1337L, ash: true, spawnAtEntry: false);
            b.Build();
            yield return null;

            Assert.AreEqual(52, b.Chunks, "A: изменилось число чанков Ø2000");
            Assert.AreEqual(700, b.Trees, "A: изменилось число деревьев");
            Assert.IsNotNull(b.NestSite, "A: пропало совиное гнездо");
            Assert.AreEqual(10, b.Lairs.Count, "A: изменился набор логовищ (9 змей + сова)");
        }

        [UnityTest]
        public IEnumerator C_Profile_KeepsSpawner_AndOwlDoesNotSpawn()
        {
            var b = Make(2000f, 250f, 16, 1337L, ash: true, spawnAtEntry: false);
            b.Build();
            yield return null;

            Assert.IsNotNull(b.Spawner, "C: пропал ForestSpawner");
            Assert.AreEqual(10, b.Registry.Count, "C: изменился реестр логовищ");
            Assert.IsNotNull(b.NestSite, "C: пропало совиное гнездо");
            Assert.IsFalse(b.NestSite.spawnEnabled, "C: сова снова рожает без тела филина");
        }

        [UnityTest]
        public IEnumerator B0_Profile_KeepsTwelveChunks_AndNoLairs()
        {
            var b = Make(400f, 100f, 24, 4L, ash: false, spawnAtEntry: true);
            b.Build();
            yield return null;

            Assert.AreEqual(12, b.Chunks, "B0: изменилось число чанков Ø400");
            Assert.That(b.Trees, Is.InRange(450, 700), "B0: число деревьев вне стенда");
            Assert.IsNull(b.NestSite, "B0: появилось совиное гнездо, которого нет в профиле");
            // В B0 нет совиного гнезда и спавнера, но 2 устья + 4 гнезда
            // остаются адресами-логовами: это профиль мира, а не звери.
            Assert.AreEqual(6, b.Lairs.Count, "B0: изменился набор адресов-логовищ");
            Assert.AreNotEqual(Vector3.zero, b.Entry, "B0: пропал вход");
        }
    }
}
