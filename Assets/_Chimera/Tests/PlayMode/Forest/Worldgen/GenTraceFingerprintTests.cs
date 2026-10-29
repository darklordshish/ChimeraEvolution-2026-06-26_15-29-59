using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    public class GenTraceFingerprintTests
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

        ForestBuilder Make(long seed)
        {
            host = new GameObject($"~GenTraceFingerprint_{seed}");
            host.SetActive(false);
            builder = host.AddComponent<ForestBuilder>();
            builder.buildOnAwake = false;
            builder.randomSeed = false;
            builder.seed = seed;
            builder.mapDiameter = 300f;
            builder.terrainQuads = 6;
            builder.buildAsh = false;
            builder.speciesPool = new System.Collections.Generic.List<SpeciesSO>();
            host.SetActive(true);
            return builder;
        }

        [UnityTest]
        public IEnumerator SameSeed_SameFingerprint_AndTraceIsVisible()
        {
            var b = Make(1337L);
            b.Build();
            yield return null;

            Assert.IsNotNull(b.Trace, "GenTrace не создан");
            Assert.Greater(b.Trace.Entries.Count, 0, "GenTrace пуст");
            Assert.AreNotEqual(0UL, b.Fingerprint, "Fingerprint не посчитан");
            ulong first = b.Fingerprint;

            b.Clear();
            b.Build();
            yield return null;
            Assert.AreEqual(first, b.Fingerprint, "тот же сид дал другой отпечаток");
        }

        [UnityTest]
        public IEnumerator DifferentSeed_DifferentFingerprint()
        {
            var b = Make(1337L);
            b.Build();
            yield return null;
            ulong a = b.Fingerprint;

            b.Clear();
            b.seed = 1338L;
            b.Build();
            yield return null;
            Assert.AreNotEqual(a, b.Fingerprint, "разные сиды дали один отпечаток");
        }
    }
}
