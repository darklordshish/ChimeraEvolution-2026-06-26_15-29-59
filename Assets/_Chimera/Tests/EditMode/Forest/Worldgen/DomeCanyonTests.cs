using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// Task 1 большого павильона (27.10): каньон — чистый слой над полем.
    /// Детерминизм, габариты спеки, тождество вне пятна, дно над водой, Contains в метрах.
    /// Стенд-состояния собираются руками: Apply/Contains не зависят от сида.
    /// </summary>
    public class DomeCanyonTests
    {
        static DomeGenConfigSO Config(long seed)
        {
            var cfg = ScriptableObject.CreateInstance<DomeGenConfigSO>();
            cfg.seed = seed;
            cfg.randomSeed = false;
            cfg.mapDiameter = 2000f;
            cfg.chunkSize = 250f;
            return cfg;
        }

        /// <summary>Ось устье(0,0) → исток(100,0), полуширина 5: удобные метры.</summary>
        static DomeCanyon.State Straight()
        {
            return new DomeCanyon.State
            {
                valid = true,
                mouth = new Vector2(0f, 0f),
                lip = new Vector2(100f, 0f),
                axis = new List<Vector2>
                {
                    new Vector2(0f, 0f),
                    new Vector2(50f, 0f),
                    new Vector2(100f, 0f),
                },
                halfWidth = 5f,
                depthMouth = 8f,
                depthHead = 12f,
                fallHeight = 14f,
                pool = new Vector2(90f, 0f),
                poolRadius = 7f,
            };
        }

        static float PolyLength(DomeCanyon.State s)
        {
            float len = 0f;
            for (int i = 1; i < s.axis.Count; i++)
                len += Vector2.Distance(s.axis[i - 1], s.axis[i]);
            return len;
        }

        [Test]
        public void Apply_OutsideFootprint_IsIdentity()
        {
            var s = Straight();
            Assert.AreEqual(20f, DomeCanyon.Apply(20f, s, 50f, 100f, 0f), 1e-4f,
                "вне пятна каньон не меняет поле");
            Assert.AreEqual(20f, DomeCanyon.Apply(20f, s, -500f, 0f, 0f), 1e-4f,
                "за торцом устья — тождество");
        }

        [Test]
        public void Apply_CarvesFloor_NotBelowWater()
        {
            var s = Straight();
            float carved = DomeCanyon.Apply(30f, s, 50f, 0f, 10f);
            Assert.Less(carved, 30f, "центр каньона ниже сырого поля");
            Assert.GreaterOrEqual(carved, 10f + 0.45f - 1e-4f,
                "дно не ниже уровня воды + 0.45");

            // Сырое поле ниже воды: пол поднимается до lake + 0.45, а не уходит под воду.
            float low = DomeCanyon.Apply(10f, s, 50f, 0f, 10f);
            Assert.GreaterOrEqual(low, 10f + 0.45f - 1e-4f, "дно всё равно над водой");
            Assert.GreaterOrEqual(low, 10f - 1e-4f, "пол не ниже сырого поля при клампе");
        }

        [Test]
        public void Apply_Shoulder_IsContinuous()
        {
            var s = Straight();
            float center = DomeCanyon.Apply(30f, s, 50f, 0f, 0f);
            float halfway = DomeCanyon.Apply(30f, s, 50f, 5f + 4f * 0.5f, 0f);
            float justInsideEdge = DomeCanyon.Apply(30f, s, 50f, 4.999f, 0f);
            float outside = DomeCanyon.Apply(30f, s, 50f, 10f, 0f);

            Assert.AreEqual(center, justInsideEdge, 1e-3f, "ядро вырезано на полную глубину");
            Assert.Greater(halfway, center, "плечо мягче ядра");
            Assert.Less(halfway, 30f, "плечо всё ещё ниже поля");
            Assert.AreEqual(30f, outside, 1e-4f, "за плечом — снова сырое поле");
        }

        [Test]
        public void Contains_MarginIsMeters_EdgeIsOutside()
        {
            var s = Straight();
            Assert.IsTrue(DomeCanyon.Contains(s, 50f, 0f), "центр внутри");
            Assert.IsTrue(DomeCanyon.Contains(s, 50f, 4.9f), "чуть внутри кромки");
            Assert.IsFalse(DomeCanyon.Contains(s, 50f, 5.0001f, 0f), "снаружи кромки");
            Assert.IsTrue(DomeCanyon.Contains(s, 50f, 6.5f, 2f), "margin расширяет в метрах");
            Assert.IsFalse(DomeCanyon.Contains(s, 50f, 7f, 2f),
                "точка в 2 м от кромки при margin = 2 — снаружи");
            Assert.IsFalse(DomeCanyon.Contains(s, 500f, 0f), "далеко — снаружи");
        }

        [Test]
        public void Invalid_IsIdentity()
        {
            var s = new DomeCanyon.State { valid = false };
            Assert.AreEqual(7.5f, DomeCanyon.Apply(7.5f, s, 0f, 0f, 0f), 1e-6f,
                "невалидное состояние не трогает поле");
            Assert.IsFalse(DomeCanyon.Contains(s, 0f, 0f), "невалидное состояние пусто");
            Assert.AreEqual(0, DomeCanyon.StreamPath(s).Count, "невалидная ось пуста");
        }

        [Test]
        public void StreamPath_GoesDownstream_FromLipToMouth()
        {
            var s = Straight();
            var path = DomeCanyon.StreamPath(s);
            Assert.AreEqual(s.axis.Count, path.Count, "все точки оси");
            Assert.AreEqual(s.lip, path[0], "первая — исток");
            Assert.AreEqual(s.mouth, path[path.Count - 1], "последняя — устье");
        }

        [Test]
        public void Build_IsDeterministic_AndSane()
        {
            var cfg = Config(1337);
            try
            {
                var hydro = DomeHydro.Build(cfg, 96);
                float rin = DomeRockRing.InnerRadius(cfg);
                // Дальние зоны: проверяем геометрию каньона, а не борьбу за место.
                Vector2 entry = new Vector2(-700f, 300f);
                Vector2 lab = new Vector2(600f, -400f);

                var a = DomeCanyon.Build(cfg, 1337, hydro, entry, lab, rin);
                var b = DomeCanyon.Build(cfg, 1337, hydro, entry, lab, rin);

                Assert.IsTrue(a.valid, "на сиде 1337 каньон строится");
                Assert.AreEqual(a.valid, b.valid, "детерминизм по валидности");
                Assert.AreEqual(a.mouth.x, b.mouth.x, 1e-6f, "устье X детерминировано");
                Assert.AreEqual(a.mouth.y, b.mouth.y, 1e-6f, "устье Z детерминировано");
                Assert.AreEqual(a.lip.x, b.lip.x, 1e-6f, "исток X детерминирован");
                Assert.AreEqual(a.lip.y, b.lip.y, 1e-6f, "исток Z детерминирован");
                Assert.AreEqual(a.axis.Count, b.axis.Count, "число узлов оси");
                for (int i = 0; i < a.axis.Count; i++)
                {
                    Assert.AreEqual(a.axis[i].x, b.axis[i].x, 1e-6f, $"узел {i} X");
                    Assert.AreEqual(a.axis[i].y, b.axis[i].y, 1e-6f, $"узел {i} Z");
                }

                float len = PolyLength(a);
                Assert.GreaterOrEqual(len, DomeCanyon.MinLength - 0.01f, "ось не короче 150 м");
                Assert.LessOrEqual(len, DomeCanyon.MaxLength + 0.01f, "ось не длиннее 250 м");

                Assert.GreaterOrEqual(a.halfWidth, DomeCanyon.MinHalfWidth - 1e-4f);
                Assert.LessOrEqual(a.halfWidth, DomeCanyon.MaxHalfWidth + 1e-4f);
                Assert.GreaterOrEqual(a.depthHead, DomeCanyon.MinDepth - 1e-4f);
                Assert.LessOrEqual(a.depthHead, DomeCanyon.MaxDepth + 1e-4f);
                Assert.GreaterOrEqual(a.depthMouth, DomeCanyon.MinDepth - 1e-4f);
                Assert.LessOrEqual(a.depthMouth, DomeCanyon.MaxDepth + 1e-4f);
                Assert.GreaterOrEqual(a.fallHeight, DomeCanyon.MinFall - 1e-4f);
                Assert.LessOrEqual(a.fallHeight, DomeCanyon.MaxFall + 1e-4f);
                Assert.Greater(a.poolRadius, 0f, "плёс имеет радиус");

                // Устье у озера, исток дальше от озера.
                float mouthFromLake = Vector2.Distance(a.mouth, hydro.lakeCenter);
                float lipFromLake = Vector2.Distance(a.lip, hydro.lakeCenter);
                Assert.GreaterOrEqual(mouthFromLake,
                    hydro.lakeRadius + a.halfWidth + DomeCanyon.LakeGap - 1e-3f,
                    "устье не в озере");
                Assert.LessOrEqual(mouthFromLake,
                    hydro.lakeRadius + a.halfWidth + DomeCanyon.LakeGap + 25f,
                    "устье рядом с озером");
                Assert.Greater(lipFromLake, mouthFromLake, "исток дальше от озера, чем устье");

                // Защитные зоны: кольцо, озеро, русло, вход, лаборатория.
                for (int i = 0; i < a.axis.Count; i++)
                {
                    Assert.LessOrEqual(a.axis[i].magnitude, rin - DomeCanyon.RingMargin + 1e-3f,
                        $"узел {i} не в скальном кольце");
                    Assert.GreaterOrEqual(
                        Vector2.Distance(a.axis[i], hydro.lakeCenter),
                        hydro.lakeRadius + a.halfWidth + DomeCanyon.LakeGap - 1e-3f,
                        $"узел {i} не в чаше озера");
                }

                for (int i = 0; i < hydro.riverPts.Count; i++)
                {
                    Assert.IsFalse(DomeCanyon.Contains(a, hydro.riverPts[i].x, hydro.riverPts[i].y,
                            hydro.riverHalfWidth + DomeCanyon.RiverClearance),
                        $"точка русла {i} не в каньоне");
                }

                Assert.IsFalse(DomeCanyon.Contains(a, entry.x, entry.y,
                        DomeCanyon.EntryClearance), "каньон не накрывает вход");
                Assert.IsFalse(DomeCanyon.Contains(a, lab.x, lab.y,
                        DomeCanyon.LabClearance), "каньон не накрывает лабораторию");
            }
            finally
            {
                Object.DestroyImmediate(cfg);
            }
        }

        [Test]
        public void Build_NullInputs_Invalid()
        {
            var none = DomeCanyon.Build(null, 1, null, Vector2.zero, Vector2.zero, 0f);
            Assert.IsFalse(none.valid, "без cfg/hydro каньона нет");
            Assert.AreEqual(3f, DomeCanyon.Apply(3f, none, 0f, 0f, 0f), 1e-6f,
                "невалидный результат — тождество");
        }
    }
}
