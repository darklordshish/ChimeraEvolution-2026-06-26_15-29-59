using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    /// <summary>
    /// Исторический профиль сида 4 — «подтопленный» вариант B0.1 из `Docs/les/СИДЫ.md`.
    /// В базлайн S0 он НЕ входит (решение 01.11: B0 — только сид 9), но профиль
    /// должен продолжать собираться: это отдельный тест, а не B0.
    /// </summary>
    public class ForestFragmentHistoricalTests
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

        [UnityTest]
        public IEnumerator Seed4_HistoricalWetStand_StillBuilds()
        {
            host = new GameObject("~ForestSeed4Historical");
            host.SetActive(false);
            builder = host.AddComponent<ForestBuilder>();
            builder.buildOnAwake = false;
            builder.randomSeed = false;
            builder.seed = 4L;
            builder.mapDiameter = 400f;
            builder.chunkSize = 100f;
            builder.terrainQuads = 24;
            builder.buildAsh = false;
            builder.spawnAtEntry = true;
            builder.speciesPool = new List<SpeciesSO>();
            host.SetActive(true);

            var b = builder;
            b.Build();
            yield return null;

            Assert.AreEqual(12, b.Chunks, "сид 4 (исторический): Ø400/chunk 100 — 12 чанков");
            Assert.That(b.Trees, Is.InRange(450, 700),
                "сид 4 (исторический): бюджет деревьев прежнего стенда не раздут");
            Assert.IsNull(b.NestSite, "сид 4 (исторический): без ясеня совиного гнезда нет");
            Assert.AreEqual(6, b.Lairs.Count,
                "сид 4 (исторический): 2 устья + 4 гнезда — факт, не базлайн");
            Assert.AreNotEqual(Vector3.zero, b.Entry, "сид 4 (исторический): вход на месте");
        }
    }
}
