using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// G7 шаг 4: каньон — ветвь `wet`/`dry`/`absent`, а не «повезло/не повезло».
    /// `absent` — valid = true, `invalid` — только дефект входов/сборки.
    /// </summary>
    public class CanyonGateTests
    {
        static DomeGenConfigSO Config(long seed)
        {
            var cfg = ScriptableObject.CreateInstance<DomeGenConfigSO>();
            cfg.seed = seed;
            cfg.randomSeed = false;
            cfg.mapDiameter = 2000f;
            cfg.chunkSize = 250f;
            cfg.version = 1;
            return cfg;
        }

        [Test]
        public void TwentySeeds_PoliciesAreNamed_And1337IsNotAbsent()
        {
            var counts = new Dictionary<CanyonPolicy, int>();
            var cfg = Config(1337);
            try
            {
                for (int seed = 1; seed <= 20; seed++)
                {
                    cfg.seed = seed;
                    var hydro = DomeHydro.Build(cfg, 96);
                    var entry = new Vector2(-300f, 0f);
                    var lab = new Vector2(300f, 0f);
                    var s = DomeCanyon.Build(cfg, seed, hydro, entry, lab,
                        DomeRockRing.InnerRadius(cfg));
                    Assert.IsTrue(s.policy == CanyonPolicy.Wet || s.policy == CanyonPolicy.Dry
                        || s.policy == CanyonPolicy.Absent,
                        $"сид {seed}: политика не названа ({s.policy})");
                    Assert.IsTrue(s.valid, $"сид {seed}: политика {s.policy} должна быть valid");
                    if (seed == 1337)
                        Assert.AreNotEqual(CanyonPolicy.Absent, s.policy,
                            "канонический 1337 не absent");
                    counts.TryGetValue(s.policy, out int n);
                    counts[s.policy] = n + 1;
                }
                Assert.GreaterOrEqual(counts.Count, 2, "20 сидов не дали хотя бы двух политик");
                Assert.Pass($"wet={Count(counts, CanyonPolicy.Wet)} " +
                    $"dry={Count(counts, CanyonPolicy.Dry)} " +
                    $"absent={Count(counts, CanyonPolicy.Absent)}");
            }
            finally
            {
                Object.DestroyImmediate(cfg);
            }
        }

        static int Count(Dictionary<CanyonPolicy, int> d, CanyonPolicy p)
        {
            d.TryGetValue(p, out int n);
            return n;
        }

        [Test]
        public void MaskBlocksCanyon_ReturnsAbsent_NotInvalid()
        {
            var cfg = Config(1337);
            try
            {
                var hydro = DomeHydro.Build(cfg, 96);
                var masks = new DomeMasks();
                masks.Add("station", DomeFootprint.Circle(Vector2.zero, 5000f));
                var s = DomeCanyon.Build(cfg, 1337, hydro, new Vector2(-300f, 0f),
                    new Vector2(300f, 0f), DomeRockRing.InnerRadius(cfg), default, masks);
                Assert.IsTrue(s.valid, "absent — законная ветвь, а не invalid");
                Assert.AreEqual(CanyonPolicy.Absent, s.policy);
                Assert.IsTrue(s.attempts != null && s.attempts.Count > 0,
                    "причина отказа попытки потеряна");
            }
            finally
            {
                Object.DestroyImmediate(cfg);
            }
        }

        [Test]
        public void DryAndAbsent_AreNotInvalid()
        {
            var dry = new DomeCanyon.State
            {
                valid = true, policy = CanyonPolicy.Dry,
                axis = new List<Vector2>
                {
                    new Vector2(0f, 0f), new Vector2(100f, 0f),
                },
                halfWidth = 8f, depthMouth = 8f, depthHead = 10f,
            };
            var absent = new DomeCanyon.State { valid = true, policy = CanyonPolicy.Absent };
            Assert.AreEqual(10f, DomeCanyon.Apply(10f, absent, 20f, 0f, 0f), 1e-4f);
            Assert.Less(DomeCanyon.Apply(20f, dry, 50f, 0f, 0f), 20f,
                "dry — сухое дно, но вырезка есть");
        }
    }
}
