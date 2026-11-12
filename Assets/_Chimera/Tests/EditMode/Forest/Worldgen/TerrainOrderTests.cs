using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// S3 §2.1: порядок слоёв в одном месте — <see cref="DomeTerrain"/>.
    /// <para>
    /// Главное различие веток видно без сцены: в v1 landform (P5-lite) применяется
    /// ПОСЛЕ площадки (историческое поведение, которым собраны A/C/B0/Pavilion),
    /// в v2 — ДО холма и площадки («широкое → местное»). Это и есть «площадка последняя».
    /// </para>
    /// <para>
    /// Синтетика: вода выключена, холм и рельеф заданы руками. Ни площадки коллайдеров,
    /// ни чанков — проверяется только порядок, а не мир.
    /// </para>
    /// </summary>
    public class TerrainOrderTests
    {
        DomeGenConfigSO cfg;

        [TearDown]
        public void TearDown()
        {
            if (cfg != null) Object.DestroyImmediate(cfg);
            cfg = null;
        }

        /// <summary>Пол с холмом H=10 и рельефом H=5 в центре, площадка = platformY.</summary>
        DomeTerrain Make(int worldVersion, float platformY)
        {
            cfg = ScriptableObject.CreateInstance<DomeGenConfigSO>();
            cfg.seed = 1337;
            cfg.randomSeed = false;
            cfg.mapDiameter = 2000f;
            cfg.chunkSize = 250f;
            cfg.worldVersion = worldVersion;

            var relief = new DomeRelief.State();
            relief.hills.Add(new DomeRelief.Hill
            {
                center = Vector2.zero,
                radius = 100f,
                height = 5f,
            });

            return new DomeTerrain
            {
                cfg = cfg,
                hydro = new DomeHydro.State { lakeLevel = -1000f, lakeRadius = 0f },
                worldVersion = cfg.WorldVersion,
                terrace = default,
                canyon = default,
                ravines = new DomeRavines.Ravine[0],
                relief = relief,
                hill = new DomeHill.State
                {
                    center = Vector2.zero,
                    radius = 40f,
                    height = 10f,
                    blend = 0f,
                    valid = true,
                },
                padEnabled = true,
                padRect = false,
                labCenter = Vector2.zero,
                platformY = platformY,
            };
        }

        [Test]
        public void V1_KeepsLegacyOrder_LandformAfterPad()
        {
            var t = Make(1, 0f);
            float h0 = t.Hydro0(0f, 0f);
            Assert.IsFalse(t.TargetOrder, "v1 — исторический порядок");
            Assert.AreEqual(h0 + 10f, t.Shape0(0f, 0f), 1e-3f,
                "Shape0 v1 — поле + холм, без landform");
            // Площадка даёт platformY=0, и только ПОСЛЕ неё ложится рельеф +5.
            Assert.AreEqual(5f, t.SampleGround(0f, 0f), 1e-3f,
                "v1: P5-lite применяется после площадки (замороженное поведение)");
        }

        [Test]
        public void V2_PadIsLast_AndReliefBeforeHill()
        {
            var t = Make(2, 0f);
            float h0 = t.Hydro0(0f, 0f);
            Assert.IsTrue(t.TargetOrder, "v2 — целевой порядок S3");
            Assert.AreEqual(h0 + 15f, t.Shape0(0f, 0f), 1e-3f,
                "Shape0 v2 = H0 + landform(5) + hill(10)");
            Assert.AreEqual(0f, t.SampleGround(0f, 0f), 1e-3f,
                "площадка — последний blend: SampleGround(центр) = platformY");
            Assert.AreNotEqual(t.ShapeGround(0f, 0f), t.SampleGround(0f, 0f),
                "площадка не входит в ShapeGround");
        }

        [Test]
        public void ShapeGround_IsShape0_NoErosionYet()
        {
            var t = Make(2, 0f);
            for (float x = -300f; x <= 300f; x += 150f)
                for (float z = -300f; z <= 300f; z += 150f)
                    Assert.AreEqual(t.Shape0(x, z), t.ShapeGround(x, z), 1e-4f,
                        "слот эрозии пуст: ShapeGround = Shape0 (= PreErosion)");
        }

        [Test]
        public void Pad_IsAfterShape_AtEveryPoint()
        {
            var t = Make(2, 0f);
            // Вне площадки (далеко от центра) pad — тождество, обе высоты совпадают.
            float x = 600f, z = 600f;
            Assert.AreEqual(t.ShapeGround(x, z), t.SampleGround(x, z), 1e-4f,
                "вне площадки форма и пол совпадают");
            // Внутри площадки — строго platformY, что бы ни говорила форма.
            Assert.AreEqual(0f, t.SampleGround(5f, 5f), 1e-4f, "внутри R20 — плоская плита");
        }

        [Test]
        public void PlatformY_IsReadFromShape_NotFromHydro()
        {
            // «platformY = Hydro0» даёт яму на высоту плато; правильный вход — ShapeGround.
            var t = Make(2, 0f);
            float shape = t.ShapeGround(0f, 0f);
            float hydro = t.Hydro0(0f, 0f);
            Assert.Greater(shape - hydro, 10f,
                "форма подняла центр (landform+hill); Hydro0 этого не знает");
        }
    }
}
