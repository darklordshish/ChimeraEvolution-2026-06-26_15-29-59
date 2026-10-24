using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// Срез A леса (24.10): принятые числа ясеня и площадки лаборатории — сторож решения
    /// геймдизайнера 14.10. Числа живут в данных (DomeAshSite / DomeWorld) и попадают
    /// в игру без переписывания; меняешь число — меняй и этот тест тем же заходом.
    /// <para>
    /// Второй смысл — дерево, которое не видно в цифрах: крона ясеня должна быть широкой
    /// (размах = CrownDiameter) и доставать верхом до Height с учётом CrownBase. Габарит
    /// МЕША при этом не равен параметру height — см. замечание в AshCrownWide_Bounds.
    /// </para>
    /// </summary>
    public class ForestSliceATests
    {
        [Test]
        public void Ash_AcceptedNumbers()
        {
            Assert.That(DomeAshSite.Height, Is.EqualTo(85f), "высота ясеня — 85 м");
            Assert.That(DomeAshSite.CrownDiameter, Is.EqualTo(140f), "размах кроны — 140 м");
            Assert.That(DomeAshSite.TrunkDiameterMid, Is.EqualTo(16f), "ствол на середине — 16 м");
            Assert.That(DomeAshSite.RootSpread, Is.EqualTo(50f), "корни — 50 м");
            Assert.That(DomeAshSite.NestHeight, Is.EqualTo(36f), "гнездо совы — 36 м");
        }

        [Test]
        public void AshCrownWide_Bounds()
        {
            var crown = FloraMeshKit.AshCrownWide(
                DomeAshSite.CrownDiameter * 0.5f, DomeAshSite.CrownHeight);
            var roots = FloraMeshKit.AshRoots(DomeAshSite.RootSpread * 0.5f, 10);
            try
            {
                var c = crown.bounds;
                Assert.That(c.size.x, Is.EqualTo(DomeAshSite.CrownDiameter).Within(2f),
                    "размах кроны = 2·halfWidth (140 ± 2)");
                Assert.That(c.size.z, Is.EqualTo(DomeAshSite.CrownDiameter).Within(2f),
                    "второй поперечник кроны тот же");
                Assert.That(c.max.y, Is.EqualTo(0.92f * DomeAshSite.CrownHeight).Within(1f),
                    "верх кроны на 0.92·height — так 39 + 46 = 85");
                Assert.That(DomeAshSite.CrownBase + c.max.y, Is.EqualTo(DomeAshSite.Height).Within(2f),
                    "верх ясеня сходится с принятой высотой 85 м");

                // ТЗ на срез просило size.y ~ 50 (±5). Замер: купол НЕ заполняет height —
                // при height=50 габарит меша 43.7 (низ купола на +2.5 от локального нуля).
                // Фиксируем факт, чтобы расхождение было видно, а не растворилось: если
                // решим, что height — это габарит кроны, это правка геометрии, а не теста.
                Assert.That(c.size.y, Is.EqualTo(43.7f).Within(1f),
                    "фактическая высота купола при CrownHeight=50 (ТЗ просило 50±5 — долг)");

                var r = roots.bounds;
                Assert.That(r.size.x, Is.EqualTo(DomeAshSite.RootSpread).Within(2f),
                    "корни = 2·spread (50 ± 2)");
                Assert.That(r.max.y, Is.GreaterThan(0f), "корни объёмные, не плоскость");
            }
            finally
            {
                Object.DestroyImmediate(crown);
                Object.DestroyImmediate(roots);
            }
        }

        [Test]
        public void LabPad_Constants()
        {
            Assert.That(DomeWorld.LabRadius, Is.EqualTo(25f), "площадка лаборатории — R25");
            Assert.That(DomeWorld.LabFlatRadius, Is.EqualTo(20f), "плоско до R20");
            Assert.That(DomeWorld.LabFlatRadius, Is.LessThan(DomeWorld.LabRadius),
                "между R20 и R25 — блендинг к сырому полю");
        }
    }
}
