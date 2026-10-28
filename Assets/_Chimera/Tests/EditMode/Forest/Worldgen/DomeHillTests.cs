using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>Холм станции: lift-слой, тождество вне пятна, детерминизм.</summary>
    public class DomeHillTests
    {
        static DomeGenConfigSO Config()
        {
            var cfg = ScriptableObject.CreateInstance<DomeGenConfigSO>();
            cfg.mapDiameter = 2000f;
            return cfg;
        }

        [Test]
        public void Build_IsDeterministic_AndHasHeight()
        {
            var cfg = Config();
            try
            {
                var a = DomeHill.Build(cfg, 1337, new Vector2(391f, 287f));
                var b = DomeHill.Build(cfg, 1337, new Vector2(391f, 287f));
                Assert.IsTrue(a.valid);
                Assert.AreEqual(a.radius, b.radius, 1e-5f);
                Assert.AreEqual(a.height, b.height, 1e-5f);
                Assert.Greater(a.height, 0f);
            }
            finally { Object.DestroyImmediate(cfg); }
        }

        [Test]
        public void Apply_RaisesCenter_AndIsIdentityOutside()
        {
            var s = new DomeHill.State
            {
                valid = true,
                center = Vector2.zero,
                radius = 40f,
                height = 8f,
                blend = 10f,
            };
            Assert.AreEqual(18f, DomeHill.Apply(10f, s, 0f, 0f), 1e-5f, "центр поднят");
            Assert.AreEqual(10f, DomeHill.Apply(10f, s, 60f, 0f), 1e-5f, "вне пятна — тождество");
            float edge = DomeHill.Apply(10f, s, 45f, 0f);
            Assert.Greater(edge, 10f);
            Assert.Less(edge, 18f);
        }

        [Test]
        public void Contains_RespectsBlend()
        {
            var s = new DomeHill.State
            {
                valid = true, center = Vector2.zero, radius = 40f, height = 8f, blend = 10f,
            };
            Assert.IsTrue(DomeHill.Contains(s, 49f, 0f));
            Assert.IsFalse(DomeHill.Contains(s, 51f, 0f));
        }
    }
}
