using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    /// <summary>
    /// P0 шага 6: каньон проверяется на РЕАЛЬНОМ v2-мире (терраса, станция, маски),
    /// а не на синтетической паре entry/lab. 1337 обязан дать wet/dry, не absent.
    /// </summary>
    public class CanyonGateTests
    {
        GameObject host;
        ForestBuilder builder;

        [UnityTest]
        public IEnumerator Seed1337_Canyon_IsWetOrDry_WithRealV2()
        {
            host = new GameObject("~CanyonGateV2");
            host.SetActive(false);
            builder = ForestProfiles.PavilionV2(host, 1337L, erosion: false);
            host.SetActive(true);
            builder.Build();
            yield return null;

            Assert.IsTrue(builder.Canyon.valid, "каньон v2 не собран");
            Assert.AreNotEqual(CanyonPolicy.Absent, builder.Canyon.policy,
                "1337: каньон не может быть absent");
            Assert.IsTrue(builder.Canyon.policy == CanyonPolicy.Wet ||
                          builder.Canyon.policy == CanyonPolicy.Dry,
                "каньон v2 — wet/dry, не " + builder.Canyon.policy);
            Assert.IsTrue(builder.Canyon.attempts != null &&
                          builder.Canyon.attempts.Count > 0,
                "причины попыток каньона потеряны");
            Assert.IsTrue(builder.HasStation, "станция должна быть собрана");
            Assert.IsTrue(builder.Terrace.valid && builder.Terrace.radius > 0f,
                "терраса должна быть собрана");
        }

        [TearDown]
        public void TearDown()
        {
            if (builder != null) builder.Clear();
            if (host != null) Object.Destroy(host);
            host = null;
            builder = null;
        }
    }
}
