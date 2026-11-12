using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chimera.Tests.PlayMode
{
    /// <summary>
    /// G7 шаг 3: на 0–150 м есть ближняя форма в ФАКТИЧЕСКОМ ShapeGround
    /// (не только в метаданных): локальный подъём ≥3 м в окне 40–60 м,
    /// два подхода, грань 22–28° (класс M13), 0 стволов на бровке.
    /// </summary>
    public class G7NearFieldTests
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

        static string FormsDebug(IReadOnlyList<DomeForm> forms)
        {
            string s = "";
            foreach (var f in forms) s += $"{f.name}@{f.center} h={f.height:F1} | ";
            return s;
        }

        [UnityTest]
        public IEnumerator NearForm_IsInField_Readable_AndOffAxis()
        {
            host = new GameObject("~G7NearField");
            host.SetActive(false);
            builder = ForestProfiles.PavilionV2(host, 1337L, erosion: false);
            host.SetActive(true);
            builder.Build();
            yield return null;

            Assert.IsNotNull(builder.BuiltRoot, "v2-мир не собран");
            Assert.IsNotEmpty(builder.Scheme, "схема не выбрана");
            Assert.IsNotEmpty(builder.Near, "ветвь near не выбрана");
            Assert.AreEqual(builder.Scheme, builder.Plan.parameters.scheme, "план не назвал схему");
            Assert.AreEqual(builder.Near, builder.Plan.parameters.near, "план не назвал near");
            StringAssert.Contains("scheme=", builder.Trace.Text);

            var nearForms = builder.NearForms;
            Assert.GreaterOrEqual(nearForms.Count, 2, "ближняя ветвь — меньше 2 форм");
            Assert.LessOrEqual(nearForms.Count, 3, "ближняя ветвь — больше 3 форм");
            foreach (var f in nearForms)
                CollectionAssert.Contains(builder.CompositionForms, f,
                    f.name + " не входит в CompositionForms/ShapeGround");

            Vector2 entry = new Vector2(builder.Entry.x, builder.Entry.z);
            Vector2 door = new Vector2(builder.LabDoor.x, builder.LabDoor.z);
            Vector2 u = (door - entry).normalized;
            Vector2 v = new Vector2(-u.y, u.x);

            // Левая пара низких форм; линия через неё — фактический ShapeGround.
            Vector2 left = (nearForms[0].center + nearForms[1].center) * 0.5f;
            float lateral = Vector2.Dot(left - entry, v);
            float u0 = Vector2.Dot(left - entry, u) - 55f;
            float u1 = Vector2.Dot(left - entry, u) + 5f;
            float min = float.MaxValue, max = float.MinValue, maxSlope = 0f;
            float nearMin = float.MaxValue, nearMax = float.MinValue, nearFace = 0f;
            System.Func<float, float, float> nearAt = (x, z) =>
            {
                float h = 0f;
                foreach (var f in nearForms) h = f.Apply(h, x, z);
                return h;
            };
            for (int i = 0; i <= 60; i++)
            {
                float t = Mathf.Lerp(u0, u1, i / 60f);
                Vector2 p = entry + u * t + v * lateral;
                float h = builder.ShapeGround(p.x, p.y);
                if (h < min) min = h;
                if (h > max) max = h;
                float slope = SlopeClasses.SlopeAt(builder.Terrain.cfg, p.x, p.y,
                    builder.ShapeGround, SlopeClasses.ClassEpsilon);
                if (slope > maxSlope) maxSlope = slope;
                float nh = nearAt(p.x, p.y);
                if (nh < nearMin) nearMin = nh;
                if (nh > nearMax) nearMax = nh;
                for (int k = -4; k <= 4; k++)
                {
                    Vector2 q = p + v * (k * 2f);
                    float ns = SlopeClasses.SlopeAt(builder.Terrain.cfg, q.x, q.y,
                        nearAt, SlopeClasses.ClassEpsilon);
                    if (ns > nearFace) nearFace = ns;
                }
            }
            Assert.GreaterOrEqual(nearMax - nearMin, 3f,
                $"подъём именно near {nearMax - nearMin:F2} м < 3 м в окне 60 м; " +
                FormsDebug(nearForms));
            Assert.That(nearFace, Is.InRange(22f, 28f),
                $"грань near {nearFace:F1}° вне 22–28° (M13); " + FormsDebug(nearForms));
            Assert.GreaterOrEqual(max - min, 3f,
                $"локальный подъём {max - min:F2} м < 3 м в окне 60 м");

            // Ось не пережата, два подхода — по обе стороны, без стволов.
            float maxAxis = 0f;
            for (int i = 0; i <= 30; i++)
            {
                float t = Mathf.Lerp(40f, 150f, i / 30f);
                Vector2 p = entry + u * t;
                maxAxis = Mathf.Max(maxAxis, SlopeClasses.SlopeAt(builder.Terrain.cfg,
                    p.x, p.y, builder.ShapeGround, SlopeClasses.ClassEpsilon));
            }
            Assert.LessOrEqual(maxAxis, 22.01f,
                $"осевая пережата ближней формой: {maxAxis:F1}°");

            var laterals = new List<float>();
            foreach (var f in nearForms)
            {
                float side = Mathf.Abs(Vector2.Dot(f.center - entry, v));
                if (!laterals.Exists(x => Mathf.Abs(x - side) < 6f)) laterals.Add(side);
                Assert.IsFalse(builder.Masks.ContainsPoint(f.center, "water"),
                    f.name + ": форма в воде");
                Assert.IsFalse(builder.Masks.ContainsPoint(f.center, "stationReserve"),
                    f.name + ": форма в stationReserve");
                Assert.IsFalse(builder.Masks.OverlapsRing(
                        DomeFootprint.Circle(f.center, f.diameter * 0.5f)),
                    $"{f.name}: форма в кольце center={f.center} r={f.diameter * 0.5f:F1} " +
                    $"ringInner={builder.Masks.RingInner:F1}");
                foreach (var t in builder.TreeColliderPositions)
                    if (Vector2.Distance(new Vector2(t.x, t.z), f.center) <
                        f.diameter * 0.5f + 8f)
                        Assert.Fail($"{f.name}: ствол на бровке ({t.x:F0};{t.z:F0})");
            }

            // Два подхода к левой гряде: со стороны оси и с внешней стороны.
            float sideSign = Mathf.Sign(Vector2.Dot(left - entry, v));
            float uMid = Vector2.Dot(left - entry, u);
            var passPoints = new[]
            {
                entry + u * (uMid - 45f) + v * (80f * sideSign),
                entry + u * (uMid + 45f) + v * (80f * sideSign),
            };
            foreach (var p in passPoints)
            {
                Assert.LessOrEqual(SlopeClasses.SlopeAt(builder.Terrain.cfg, p.x, p.y,
                        builder.ShapeGround, SlopeClasses.ClassEpsilon), 20.01f,
                    $"подход ({p.x:F0};{p.y:F0}) круче 20°");
                Assert.IsFalse(builder.Masks.ContainsPoint(p, "water"), "подход в воде");
            }

            Assert.Pass($"scheme={builder.Scheme}, near={builder.Near}, " +
                $"nearForms={nearForms.Count}, rise={max - min:F2} м, " +
                $"face={maxSlope:F1}°, axis={maxAxis:F1}°");
        }
    }
}
