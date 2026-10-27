using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// Сторож террасы большого павильона (план 27.10, Task 1):
    /// сид → то же пятно, Apply — тождество вне пятна, valid=false — тождество,
    /// пятно не пересекает вход, лабораторию, кольцо и воду.
    /// </summary>
    public class DomeTerraceTests
    {
        static readonly Vector2 Lab = new Vector2(89f, 67f);
        static readonly Vector2 Entry = new Vector2(-87f, 50f);
        const float Rin = 120f; // Ø400: DomeRockRing.InnerRadius = 200 − 80

        static DomeGenConfigSO MakeCfg(long seed)
        {
            var cfg = ScriptableObject.CreateInstance<DomeGenConfigSO>();
            cfg.mapDiameter = 400f;
            cfg.chunkSize = 100f;
            cfg.seed = seed;
            return cfg;
        }

        [Test]
        public void Build_SameSeed_IsDeterministic()
        {
            var cfg = MakeCfg(9);
            var hydro = DomeHydro.Build(cfg, 96);
            var a = DomeTerrace.Build(cfg, 9, Lab, Entry, hydro, Rin);
            var b = DomeTerrace.Build(cfg, 9, Lab, Entry, hydro, Rin);

            Assert.IsTrue(a.valid, "на сиде 9 пятно террасы находится");
            Assert.AreEqual(a.center, b.center, "тот же центр");
            Assert.AreEqual(a.radius, b.radius, 1e-4f, "тот же радиус");
            Assert.AreEqual(a.height, b.height, 1e-4f, "та же высота");
            Assert.AreEqual(a.blend, b.blend, 1e-4f, "тот же блендинг");
            Assert.AreEqual(a.valid, b.valid, "та же годность");
            Object.DestroyImmediate(cfg);
        }

        [Test]
        public void Build_OtherSeed_AlsoDeterministic()
        {
            var cfg = MakeCfg(4);
            var hydro = DomeHydro.Build(cfg, 96);
            var a = DomeTerrace.Build(cfg, 4, Lab, Entry, hydro, Rin);
            var b = DomeTerrace.Build(cfg, 4, Lab, Entry, hydro, Rin);

            Assert.IsTrue(a.valid, "на сиде 4 пятно террасы находится");
            Assert.AreEqual(a.center, b.center, "тот же центр");
            Assert.AreEqual(a.radius, b.radius, 1e-4f, "тот же радиус");
            Object.DestroyImmediate(cfg);
        }

        [Test]
        public void Apply_OutsideFootprint_IsIdentity_InsideHigher()
        {
            var cfg = MakeCfg(9);
            var hydro = DomeHydro.Build(cfg, 96);
            var s = DomeTerrace.Build(cfg, 9, Lab, Entry, hydro, Rin);
            Assert.IsTrue(s.valid, "пятно построено");

            // Центр ровной части: raw + height.
            float rawCenter = DomeHydro.SampleHydro(cfg, hydro, s.center.x, s.center.y);
            float liftedCenter = DomeTerrace.Apply(rawCenter, s, s.center.x, s.center.y);
            Assert.Greater(liftedCenter, rawCenter, "внутри пятна поле выше сырого");
            Assert.AreEqual(rawCenter + s.height, liftedCenter, 1e-3f,
                "ровная часть поднята ровно на height");

            // Середина борта: подъём есть, но меньше полного.
            Vector2 mid = s.center + new Vector2(s.radius + s.blend * 0.5f, 0f);
            float rawMid = DomeHydro.SampleHydro(cfg, hydro, mid.x, mid.y);
            float liftedMid = DomeTerrace.Apply(rawMid, s, mid.x, mid.y);
            Assert.Greater(liftedMid, rawMid, "борт тоже поднимает");
            Assert.Less(liftedMid, rawMid + s.height, "борт поднимает меньше ровной части");

            // Снаружи полного пятна — тождество, даже если поле ниже.
            Vector2 far = s.center + new Vector2(s.radius + s.blend + 25f, 0f);
            float rawFar = DomeHydro.SampleHydro(cfg, hydro, far.x, far.y);
            Assert.AreEqual(rawFar, DomeTerrace.Apply(rawFar, s, far.x, far.y), 1e-4f,
                "вне пятна высота не меняется");
            Object.DestroyImmediate(cfg);
        }

        [Test]
        public void Apply_InvalidState_IsIdentity()
        {
            var none = default(DomeTerrace.State);
            Assert.AreEqual(10f, DomeTerrace.Apply(10f, none, 0f, 0f), 1e-6f,
                "непостроенное пятно не меняет сушу");
            Assert.AreEqual(-2.5f, DomeTerrace.Apply(-2.5f, none, 120f, -80f), 1e-6f,
                "непостроенное пятно не поднимает низину");

            var off = new DomeTerrace.State
            {
                center = Vector2.zero,
                radius = 50f,
                height = 7f,
                blend = 10f,
                valid = false,
            };
            Assert.AreEqual(3f, DomeTerrace.Apply(3f, off, 0f, 0f), 1e-6f,
                "valid=false — тождество даже в центре");
            Assert.IsFalse(DomeTerrace.Contains(off, 0f, 0f), "выключенное пятно ничего не содержит");
        }

        [Test]
        public void Build_Spot_AvoidsEntryLabRingAndWater()
        {
            var cfg = MakeCfg(9);
            var hydro = DomeHydro.Build(cfg, 96);
            var s = DomeTerrace.Build(cfg, 9, Lab, Entry, hydro, Rin);
            Assert.IsTrue(s.valid, "пятно построено");

            float reach = s.radius + s.blend;
            Assert.Greater(Vector2.Distance(s.center, Entry), reach,
                "пятно не пересекает вход");
            Assert.Greater(Vector2.Distance(s.center, Lab), reach,
                "пятно не пересекает лабораторию");
            Assert.LessOrEqual(s.center.magnitude + reach, Rin,
                "пятно не заходит в скальное кольцо");
            if (hydro.lakeRadius > 0f)
                Assert.Greater(Vector2.Distance(s.center, hydro.lakeCenter),
                    reach + hydro.lakeRadius, "пятно не в озере");
            for (int i = 0; i < hydro.riverPts.Count; i++)
                Assert.Greater(Vector2.Distance(s.center, hydro.riverPts[i]),
                    reach + hydro.riverHalfWidth, "пятно не на русле");

            Assert.IsTrue(DomeTerrace.Contains(s, s.center.x, s.center.y), "центр внутри пятна");
            Assert.IsFalse(DomeTerrace.Contains(s, Entry.x, Entry.y), "вход вне пятна");
            Assert.IsFalse(DomeTerrace.Contains(s, Lab.x, Lab.y), "лаборатория вне пятна");
            Assert.IsFalse(DomeTerrace.Contains(s, s.center.x, s.center.y + reach + 50f),
                "далеко снаружи — не пятно");
            Object.DestroyImmediate(cfg);
        }
    }
}
