using System;
using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// G7 шаг 2: откат станции — это названная ветка, а не дефолт в центр.
    /// <see cref="DomeLabSite.Reserve"/> даёт провизорный регион, <see cref="DomeLabSite.Confirm"/>
    /// подтверждает место на ShapeGround внутри него; нет места — `found = false`
    /// без запасной точки и без `LabCenter = zero`.
    /// </summary>
    public class TreeRollbackTests
    {
        DomeGenConfigSO cfg;

        [TearDown]
        public void TearDown()
        {
            if (cfg != null) UnityEngine.Object.DestroyImmediate(cfg);
            cfg = null;
        }

        DomeGenConfigSO MakeCfg()
        {
            cfg = ScriptableObject.CreateInstance<DomeGenConfigSO>();
            cfg.seed = 1337;
            cfg.randomSeed = false;
            cfg.mapDiameter = 2000f;
            cfg.chunkSize = 250f;
            cfg.version = 1;
            cfg.worldVersion = 2;
            return cfg;
        }

        static DomeHydro.State DryHydro() => new DomeHydro.State { lakeLevel = -100f };

        [Test]
        public void Reserve_IsAddressRegion_NotPlate()
        {
            var c = MakeCfg();
            var hydro = DryHydro();
            var entry = new Vector2(-400f, 0f);
            var reserve = DomeLabSite.Reserve(c, 1337L, entry, hydro,
                LabStyle.Auto, 150f, LabEntrance.Auto);
            try
            {
                Assert.IsTrue(reserve.found, "резерв существует");
                Assert.IsFalse(reserve.fallback, "v2: резерв не fallback");
                Assert.AreNotEqual(LabStyle.Bastion, reserve.style,
                    "v2: Auto/Bastion не разрешается в Bastion");
                Assert.That(reserve.entrance, Is.EqualTo(LabEntrance.Rock)
                    .Or.EqualTo(LabEntrance.Underground).Or.EqualTo(LabEntrance.Buried));
                Assert.That(Vector2.Distance(entry, reserve.center), Is.GreaterThanOrEqualTo(150f),
                    "резерв не ближе 150 м к Entry (1A — по LabDoor)");
                Assert.That(Vector2.Distance(entry,
                        new Vector2(reserve.door.x, reserve.door.z)),
                    Is.GreaterThanOrEqualTo(150f), "LabDoor ≥150 м от Entry");
                Assert.IsTrue(DomeLabSite.InReserve(reserve, reserve.center),
                    "центр резерва лежит в самом резерве");
                // Резерв не задаёт platformY и геометрию: у Spot нет поля платформы,
                // а высота резерва — лишь диагностический groundY из Hydro0.
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(c);
            }
        }

        [Test]
        public void NoPlace_ReturnsFalse_WithoutCenterFallback()
        {
            var c = MakeCfg();
            var hydro = DryHydro();
            var wetHydro = new DomeHydro.State { lakeLevel = 100f };
            var entry = new Vector2(-400f, 0f);
            var reserve = DomeLabSite.Reserve(c, 1337L, entry, hydro,
                LabStyle.Auto, 150f, LabEntrance.Auto);
            try
            {
                Assert.IsTrue(reserve.found);
                // Поле везде ниже воды — годного пятна в резерве нет.
                var bad = DomeLabSite.Confirm(c, wetHydro, (x, z) => 0f, entry, reserve,
                    LabStyle.Auto, LabEntrance.Auto);
                Assert.IsFalse(bad.found, "станции нет — found = false");
                Assert.IsFalse(bad.fallback, "v2: no fallback");
                Assert.AreNotEqual(Vector2.zero, bad.center,
                    "нет скрытого дефолта LabCenter = zero");
                Assert.That(Vector2.Distance(bad.center, reserve.center), Is.LessThan(0.01f),
                    "адрес отката — резерв, а не центр мира");
                StringAssert.Contains("station=none", bad.note, "причина названа");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(c);
            }
        }

        [Test]
        public void GoodGround_ConfirmsInsideReserve_WithoutFallback()
        {
            var c = MakeCfg();
            var hydro = DryHydro();
            var entry = new Vector2(-400f, 0f);
            var reserve = DomeLabSite.Reserve(c, 1337L, entry, hydro,
                LabStyle.Auto, 150f, LabEntrance.Auto);
            try
            {
                var ok = DomeLabSite.Confirm(c, hydro, (x, z) => 10f, entry, reserve,
                    LabStyle.Auto, LabEntrance.Auto);
                Assert.IsTrue(ok.found, "ровное сухое плечо подтверждается");
                Assert.IsFalse(ok.fallback, "подтверждение — не fallback");
                Assert.IsTrue(DomeLabSite.InReserve(reserve, ok.center),
                    "подтверждённая станция — только внутри резерва");
                Assert.That(Vector2.Distance(entry,
                        new Vector2(ok.door.x, ok.door.z)),
                    Is.GreaterThanOrEqualTo(150f), "LabDoor не ближе 150 м к Entry");
                Assert.That(Vector2.Distance(new Vector2(ok.door.x, ok.door.z),
                        new Vector2(reserve.door.x, reserve.door.z)),
                    Is.LessThan(30f), "дверь остаётся у 1A-якоря, а не уезжает по форме");
                Assert.AreNotEqual(LabStyle.Bastion, ok.style,
                    "v2: стиль — не Bastion");
                Assert.That(ok.entrance, Is.EqualTo(LabEntrance.Rock)
                    .Or.EqualTo(LabEntrance.Underground).Or.EqualTo(LabEntrance.Buried));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(c);
            }
        }
    }
}
