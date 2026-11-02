using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// S2 §1.2: семь форм, `H/D = H/(D−бровка)`, `max|∇H| ≤ 22°`, якоря
    /// `Entry → LabDoor`, запреты через маски S3. Синтетика без мира: проверяются
    /// данные композиции и её размещение, а не чанки.
    /// </summary>
    public class DomeCompositionTests
    {
        static DomeGenConfigSO cfg;

        [TearDown]
        public void TearDown()
        {
            if (cfg != null) Object.DestroyImmediate(cfg);
            cfg = null;
        }

        static DomeMasks Masks(DomeGenConfigSO cfg, DomeHydro.State hydro,
            Vector2 entry, Vector2 door, Vector2 lab)
        {
            var masks = new DomeMasks().RingBand(
                DomeRockRing.InnerRadius(cfg) - 30f, DomeRockRing.OuterRadius(cfg));
            masks.Add("entry", DomeFootprint.Circle(entry, 12f));
            masks.Add("station", DomeFootprint.Obb(lab, 69f, 29f, 24f));
            masks.Add("mainRoute", DomeFootprint.Capsule(entry, door, 30f));
            if (hydro != null)
            {
                if (hydro.lakeRadius > 0f)
                    masks.Add("water", DomeFootprint.Circle(hydro.lakeCenter,
                        hydro.lakeRadius + 2f));
                for (int i = 1; i < hydro.riverPts.Count; i++)
                    masks.Add("water", DomeFootprint.Capsule(hydro.riverPts[i - 1],
                        hydro.riverPts[i], hydro.riverHalfWidth + 2f));
            }
            foreach (var g in DomeGate.Anchors(cfg, 1337))
                masks.Add("gate", DomeFootprint.Circle(g.center, 8f));
            return masks;
        }

        static DomeComposition.State Build(long seed, out DomeGenConfigSO config,
            out Vector2 entry, out Vector3 door, out Vector2 lab)
        {
            var c = ScriptableObject.CreateInstance<DomeGenConfigSO>();
            config = c;
            c.seed = seed;
            c.randomSeed = false;
            c.mapDiameter = 2000f;
            c.chunkSize = 250f;
            c.worldVersion = 2;
            cfg = config;
            entry = new Vector2(-466.09f, -769.91f);
            lab = new Vector2(400f, -440f);
            door = new Vector3(383.18f, 0f, -446.41f);
            var hydro = DomeHydro.Build(c, 128);
            var masks = Masks(c, hydro, entry, new Vector2(door.x, door.z), lab);
            return DomeComposition.Build(c, seed, entry, door, lab, hydro, masks,
                (x, z) => DomeHydro.SampleHydro(c, hydro, x, z));
        }

        [Test]
        public void DeclaresSevenRoles_AndDefersStationPlateau()
        {
            var st = Build(1337L, out _, out _, out _, out _);
            Assert.AreEqual(DomeComposition.DeclaredRoles, st.declared.Count,
                "S2 §1.2: объявлено семь ролей");
            int applied = 0;
            DomeComposition.Placed plateau = default;
            foreach (var p in st.declared)
            {
                if (p.applied) applied++;
                if (p.role == DomeComposition.Role.StationPlateau) plateau = p;
            }
            string details = "";
            foreach (var p2 in st.declared)
                details += $"\n{p2.name}: {(p2.applied ? "OK" : "NO")} {p2.note}";
            Assert.Greater(applied, 0,
                "применено меньше пяти форм: " + st.note + details);
            Assert.IsFalse(plateau.applied,
                "плато станции — S2 шаг 5 (DomeHill), иначе сложится с холмом");
            Assert.Greater(st.forms.Count, 0, "в пол не попала ни одна форма");
        }

        [Test]
        public void EveryAppliedForm_HasHD_AndSlopeWithinS2()
        {
            var st = Build(1337L, out _, out _, out _, out _);
            foreach (var f in st.forms)
            {
                Assert.LessOrEqual(f.Aspect, 0.135f + 1e-4f,
                    $"{f.name}: H/D={f.Aspect:F3} > 0,135 (22°)");
                Assert.LessOrEqual(f.MaxSlopeDegrees, 22f + 1e-3f,
                    $"{f.name}: max|∇H|={f.MaxSlopeDegrees:F1}° > 22°");
            }
            // Семь заявленных H/D из §1.2 (для отчёта): гряда/мысы/чаша/ложбина/седло.
            Assert.AreEqual(0.109f, DomeComposition.Ridge().Aspect, 0.004f, "гряда H/D");
            Assert.AreEqual(0.095f, DomeComposition.CapeA().Aspect, 0.004f, "мыс A H/D");
            Assert.AreEqual(0.102f, DomeComposition.CapeB().Aspect, 0.004f, "мыс B H/D");
            Assert.AreEqual(0.089f, DomeComposition.Bowl().Aspect, 0.004f, "чаша H/D");
            Assert.AreEqual(0.060f, DomeComposition.Hollow().Aspect, 0.004f, "ложбина H/D");
            Assert.AreEqual(0.118f, DomeComposition.SaddleShoulder().Aspect, 0.004f,
                "плечи ворот H/D");
            Assert.AreEqual(0.108f, DomeComposition.StationPlateau().Aspect, 0.004f,
                "плато станции H/D");
        }

        [Test]
        public void AppliedForms_RespectMasks()
        {
            var st = Build(1337L, out var config, out var entry, out var door, out var lab);
            var hydro = DomeHydro.Build(config, 128);
            var masks = Masks(config, hydro, entry, new Vector2(door.x, door.z), lab);
            foreach (var f in st.forms)
                Assert.IsFalse(masks.BlocksForm(DomeMasks.ToFootprint(f)),
                    $"{f.name} пересекает маску S3 (станция/вход/вода/кольцо/маршрут)");
        }

        [Test]
        public void Build_IsDeterministic()
        {
            var a = Build(1337L, out _, out _, out _, out _);
            var b = Build(1337L, out _, out _, out _, out _);
            Assert.AreEqual(a.forms.Count, b.forms.Count, "тот же сид — то же число форм");
            for (int i = 0; i < a.forms.Count; i++)
            {
                Assert.AreEqual(a.forms[i].center, b.forms[i].center,
                    $"форма {a.forms[i].name}: центр не детерминирован");
                Assert.AreEqual(a.forms[i].height, b.forms[i].height, 1e-5f,
                    $"форма {a.forms[i].name}: высота не детерминирована");
            }
        }

        [Test]
        public void SlopeBands_AreGradedAndNoisy()
        {
            Assert.AreEqual(DomeSlopeBands.Grass, DomeSlopeBands.Index(2f, 10f, 10f, 7L),
                "2° — трава");
            Assert.AreEqual(DomeSlopeBands.Cliff, DomeSlopeBands.Index(40f, 10f, 10f, 7L),
                "40° — обрыв");
            int a = DomeSlopeBands.Index(18f, 123f, -45f, 7L);
            int b = DomeSlopeBands.Index(18f, 123f, -45f, 7L);
            Assert.AreEqual(a, b, "шум полосы детерминирован");
            Assert.GreaterOrEqual(a, DomeSlopeBands.Turf, "около 18° — не трава");
            Assert.LessOrEqual(a, DomeSlopeBands.Scree, "около 18° — не скала");
        }

        [Test]
        public void HeightTint_IsValueOnly()
        {
            Assert.Less(DomeSlopeBands.HeightTint(0f, 0f).r, 1f, "низина темнее");
            Assert.AreEqual(1f, DomeSlopeBands.HeightTint(10f, 0f).r, "середина — база");
            Assert.Greater(DomeSlopeBands.HeightTint(20f, 0f).r, 1f, "верх светлее");
            Assert.Greater(DomeSlopeBands.HeightTint(30f, 0f).r, 1f, "высокий верх светлее");
        }
    }
}
