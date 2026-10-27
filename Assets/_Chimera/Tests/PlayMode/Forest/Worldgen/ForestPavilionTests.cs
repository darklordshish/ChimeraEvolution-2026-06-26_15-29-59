using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    /// <summary>
    /// Task 1 большого павильона: терраса и каньон входят в SampleGround,
    /// при выключенных флагах мир остаётся прежним.
    /// </summary>
    public class ForestPavilionTests
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

        ForestBuilder Build(bool geography)
        {
            host = new GameObject("~ForestPavilionProbe");
            host.SetActive(false);
            builder = host.AddComponent<ForestBuilder>();
            builder.buildOnAwake = false;
            builder.randomSeed = false;
            builder.seed = 1337;
            builder.mapDiameter = 2000f;
            builder.chunkSize = 250f;
            builder.terrainQuads = 16;
            builder.buildAsh = false;
            builder.buildRavines = false;
            builder.buildLabComplex = false;
            builder.buildTerrace = geography;
            builder.buildCanyon = geography;
            builder.spawnAtEntry = true;
            builder.speciesPool = new List<SpeciesSO>();
            host.SetActive(true);
            builder.Build();
            return builder;
        }

        [UnityTest]
        public IEnumerator Geography_ChangesSampleGround_AndCanyonIsValid()
        {
            var b = Build(true);
            yield return null;

            Assert.IsTrue(b.Terrace.valid, "терраса найдена на Ø2000/1337");
            Assert.IsTrue(b.Canyon.valid, "каньон найден на Ø2000/1337");
            Assert.IsNotNull(b.Canyon.axis, "ось каньона есть");

            var mid = b.Canyon.axis[b.Canyon.axis.Count / 2];
            Assert.Less(b.SampleGround(mid.x, mid.y), b.RawGround(mid.x, mid.y),
                "каньон опускает пол относительно сырого поля");
            Assert.Greater(b.SampleGround(mid.x, mid.y), b.Hydro.lakeLevel,
                "дно каньона выше воды");
        }

        [UnityTest]
        public IEnumerator Geography_Off_IsNoOp()
        {
            var b = Build(false);
            yield return null;

            Assert.IsFalse(b.Terrace.valid, "без флага террасы нет");
            Assert.IsFalse(b.Canyon.valid, "без флага каньона нет");
            Assert.AreEqual(b.RawGround(300f, 300f), b.SampleGround(300f, 300f), 1e-4f,
                "без географии SampleGround не меняет поле вне площадки");
        }
    }
}
