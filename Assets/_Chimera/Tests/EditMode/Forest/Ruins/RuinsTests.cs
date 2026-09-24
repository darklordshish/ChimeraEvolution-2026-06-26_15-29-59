using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// Бункер: габарит кита, детерминизм комплекса, counts, steep-gate витрины.
    /// Слайс s9.
    /// </summary>
    public class RuinsTests
    {
        [Test]
        public void Kit_PartsHaveVolume()
        {
            var parts = BunkerKit.Complex(1337);
            Assert.GreaterOrEqual(parts.Count, 20, "комплекс: портал+стены+крыша+вент+дверь+мох+веха+8 обломков");
            int colliders = 0;
            foreach (var p in parts) if (p.collider) colliders++;
            Assert.Greater(colliders, 10, "блокеров хватает (пилоны/стены/дверь/обломки)");
        }

        [Test]
        public void Complex_Deterministic()
        {
            var a = BunkerKit.Complex(1337);
            var b = BunkerKit.Complex(1337);
            Assert.AreEqual(a.Count, b.Count);
            Assert.AreEqual(a[0].pos, b[0].pos, "тот же сид — та же раскладка");
            var c = BunkerKit.Complex(777);
            bool differs = a.Count != c.Count;
            for (int i = 0; i < Mathf.Min(a.Count, c.Count) && !differs; i++)
                if (a[i].pos != c[i].pos) differs = true;
            Assert.IsTrue(differs, "разные сиды — разные обломки");
        }

        [Test]
        public void ShowcaseConfig_SlopeGate_Clean()
        {
            var cfg = ScriptableObject.CreateInstance<WorldGenConfigSO>();
            cfg.seed = 1337;
            cfg.amplitude = 7f;
            cfg.baseFrequency = 0.03f;
            var issues = WorldRules.CheckGrid(cfg, 32, 2f);
            Assert.IsEmpty(issues, "витрина 120м/amp7: крутых за лимитом нет — " + string.Join("; ", issues));
        }

        [Test]
        public void Vent_MarksUnderground()
        {
            var parts = BunkerKit.Complex(5);
            bool hasVent = false, hasVoid = false;
            foreach (var p in parts)
            {
                if (p.mat == BunkerKit.PartMat.Void) hasVoid = true;
                if (p.pos.y > 1f && p.mat == BunkerKit.PartMat.Dark) hasVent = true;
            }
            Assert.IsTrue(hasVoid, "тёмная ниша входа есть");
            Assert.IsTrue(hasVent, "вент-колпак выше роста есть");
        }
    }
}
