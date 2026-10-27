using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// Task 2 большого павильона (27.10): вода — геометрия над ОДНИМ полом.
    /// Плёс не ниже уровня озера и не висит над дном, полотно соединяет лип и плёс,
    /// ручей идёт вниз по дну, у воды нет коллайдеров, без каньона — прежние озеро и река.
    /// Стенд-состояния собираются руками: формы не зависят от сида; отдельный тест
    /// проверяет тот же инвариант на настоящем поле Ø2000/1337.
    /// </summary>
    public class DomeWaterShapeTests
    {
        // ---- стенд ----------------------------------------------------------

        static DomeGenConfigSO Config(long seed = 1337L)
        {
            var cfg = ScriptableObject.CreateInstance<DomeGenConfigSO>();
            cfg.seed = seed;
            cfg.randomSeed = false;
            cfg.mapDiameter = 2000f;
            cfg.chunkSize = 250f;
            return cfg;
        }

        static DomeHydro.State Hydro()
        {
            return new DomeHydro.State
            {
                lakeCenter = new Vector2(-160f, 0f),
                lakeRadius = 40f,
                lakeLevel = 0f,
                riverPts = new List<Vector2>
                {
                    new Vector2(-160f, 0f),
                    new Vector2(-100f, 0f),
                },
                riverBed = new List<float> { 0f, 0f },
                riverHalfWidth = 2.5f,
            };
        }

        /// <summary>Ось устье(0,0) → исток(100,0); плёс у липа, как в Task 1.</summary>
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
                halfWidth = 9f,
                depthMouth = 8f,
                depthHead = 14f,
                fallHeight = 14f,
                pool = new Vector2(90f, 0f),
                poolRadius = 7f,
            };
        }

        static float Flat(float x, float z) => 0f;

        /// <summary>Исток выше устья: вода обязана идти к устью вниз.</summary>
        static float Slope(float x, float z) => Mathf.Max(0f, x) * 0.04f;

        /// <summary>Дно плёса ниже берегов на 1 м: вода не должна выйти за берег.</summary>
        static Func<float, float, float> Bowl(DomeCanyon.State s)
        {
            return (x, z) =>
            {
                float d = Vector2.Distance(new Vector2(x, z), s.pool);
                return d <= s.poolRadius ? 0f : 1f;
            };
        }

        // ---- плёс -----------------------------------------------------------

        [Test]
        public void PoolLevel_NotBelowLake_AndNotBuriedInBed()
        {
            var s = Straight();
            float lake = -1f;

            // Дно ниже озера — уровень озера становится опорой (lake + 0.02).
            Func<float, float, float> underWater = (x, z) => lake - 1f;
            float lakeBound = DomeWaterMesh.PoolLevel(s, lake, underWater);
            Assert.AreEqual(lake + DomeWaterMesh.PoolLift, lakeBound, 1e-4f,
                "дно ниже озера — плёс на уровне озера + 0.02");

            // Дно выше озера (каньон Task 1 клампит его к lake + 0.45): плёс не тонет.
            float bedLevel = DomeWaterMesh.PoolLevel(s, lake, Slope);
            Assert.GreaterOrEqual(bedLevel, lake + DomeWaterMesh.PoolLift - 1e-4f,
                "плёс не ниже уровня озера");
            Assert.AreEqual(Slope(s.pool.x, s.pool.y) + DomeWaterMesh.PoolLift, bedLevel, 1e-4f,
                "плёс стоит над дном, а не под ним");
        }

        [Test]
        public void PoolDisc_IsFlat_AndBelowLip()
        {
            var s = Straight();
            float level = DomeWaterMesh.PoolLevel(s, 0f, Flat);
            var mesh = DomeWaterMesh.PoolDisc(s, level);
            var v = mesh.vertices;

            Assert.Greater(v.Length, 0, "плёс построен");
            for (int i = 0; i < v.Length; i++)
                Assert.AreEqual(level, v[i].y, 1e-4f, "плёс — одна плоскость");

            float top = DomeWaterMesh.FallTopLevel(s, Flat);
            Assert.Less(level, top, "плёс ниже липа — вода падает вниз, а не вверх");

            // Диск не больше своего радиуса и не меньше его: плёс — это poolRadius.
            float maxR = 0f;
            for (int i = 0; i < v.Length; i++)
                maxR = Mathf.Max(maxR, Vector2.Distance(new Vector2(v[i].x, v[i].z), s.pool));
            Assert.AreEqual(s.poolRadius, maxR, 1e-3f, "радиус плёса из состояния каньона");
        }

        [Test]
        public void PoolSurface_DoesNotRiseAboveBanks()
        {
            var s = Straight();
            Func<float, float, float> bowl = Bowl(s);
            float level = DomeWaterMesh.PoolLevel(s, 0f, bowl);
            var mesh = DomeWaterMesh.PoolDisc(s, level);

            foreach (var v in mesh.vertices)
            {
                float ground = bowl(v.x, v.z);
                Assert.LessOrEqual(v.y, ground + 0.03f,
                    $"вода не выше берега/дна в точке ({v.x:F1}; {v.z:F1})");
            }
        }

        // ---- полотно --------------------------------------------------------

        [Test]
        public void FallSheet_TopIsLip_BottomIsPool()
        {
            var s = Straight();
            float bottom = DomeWaterMesh.PoolLevel(s, 0f, Slope);
            float top = DomeWaterMesh.FallTopLevel(s, Slope);
            var mesh = DomeWaterMesh.FallSheet(s, top, bottom);
            var v = mesh.vertices;

            Assert.Greater(v.Length, 0, "полотно построено");
            float min = float.PositiveInfinity, max = float.NegativeInfinity;
            foreach (var p in v)
            {
                min = Mathf.Min(min, p.y);
                max = Mathf.Max(max, p.y);
            }
            Assert.AreEqual(bottom, min, 1e-3f, "низ полотна — уровень плёса");
            Assert.AreEqual(top, max, 1e-3f, "верх полотна — уровень липа");
            Assert.Less(bottom, top, "плёс ниже липа");

            // Ширина полотна — из полуширины каньона, в рабочих 8–12 м.
            float width = DomeWaterMesh.FallWidth(s);
            Assert.GreaterOrEqual(width, DomeWaterMesh.MinFallWidth - 1e-4f);
            Assert.LessOrEqual(width, DomeWaterMesh.MaxFallWidth + 1e-4f);
            float rowWidth = 0f;
            foreach (var p in v)
                if (Mathf.Abs(p.y - bottom) < 1e-3f)
                    rowWidth = Mathf.Max(rowWidth, Vector3.Distance(
                        p, new Vector3(s.pool.x, bottom, s.pool.y)));
            Assert.Greater(rowWidth, 0f, "у полотна есть ширина");
        }

        [Test]
        public void FallSheet_DoesNotHangAboveLipOrBelowPool()
        {
            var s = Straight();
            float bottom = DomeWaterMesh.PoolLevel(s, 0f, Slope);
            float top = DomeWaterMesh.FallTopLevel(s, Slope);
            var mesh = DomeWaterMesh.FallSheet(s, top, bottom);

            foreach (var p in mesh.vertices)
            {
                Assert.LessOrEqual(p.y, top + 1e-3f, "полотно не выше липа");
                Assert.GreaterOrEqual(p.y, bottom - 1e-3f, "полотно не ниже плёса");
            }
        }

        // ---- ручей ----------------------------------------------------------

        [Test]
        public void Stream_GoesDownhill_AndFollowsGround()
        {
            var s = Straight();
            var profile = DomeWaterMesh.StreamProfile(s, Slope);

            Assert.Greater(profile.Count, 2, "профиль ручья построен");
            Assert.Less(profile[profile.Count - 1].x, profile[0].x,
                "ручей идёт от плёса к устью");

            float prev = float.PositiveInfinity;
            foreach (var p in profile)
            {
                float ground = Slope(p.x, p.z);
                float lift = p.y - ground;
                Assert.LessOrEqual(lift, DomeWaterMesh.StreamLift + 1e-3f,
                    $"ручей не висит над дном (x={p.x:F1})");
                Assert.GreaterOrEqual(lift, DomeWaterMesh.StreamMinLift - 1e-3f,
                    $"ручей не тонет в дне (x={p.x:F1})");
                Assert.LessOrEqual(p.y, prev + 1e-3f,
                    $"ручей не течёт вверх (x={p.x:F1})");
                prev = p.y;
            }
        }

        [Test]
        public void StreamRibbon_SitsOnGround()
        {
            var s = Straight();
            var mesh = DomeWaterMesh.StreamRibbon(s, Slope);
            var v = mesh.vertices;

            Assert.Greater(v.Length, 0, "лента ручья построена");
            foreach (var p in v)
            {
                float lift = p.y - Slope(p.x, p.z);
                Assert.LessOrEqual(lift, DomeWaterMesh.StreamLift + 1e-3f,
                    $"вершина ручья не висит (x={p.x:F1})");
                Assert.GreaterOrEqual(lift, DomeWaterMesh.StreamMinLift - 1e-3f,
                    $"вершина ручья не под дном (x={p.x:F1})");
            }
        }

        // ---- сборка и коллайдеры --------------------------------------------

        [Test]
        public void Build_WithCanyon_AddsWater_WithoutColliders()
        {
            var parent = new GameObject("~DomeWaterShape");
            var cfg = Config();
            var hydro = Hydro();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var track = new List<Mesh>();
            try
            {
                DomeWaterMesh.Build(parent.transform, cfg, hydro, Straight(), Slope, mat, track);

                var names = new HashSet<string>();
                foreach (Transform child in parent.transform) names.Add(child.name);
                CollectionAssert.IsSupersetOf(names,
                    new[] { "Lake", "River", "Pool", "Fall", "Stream" },
                    "с каньоном есть озеро, река, плёс, полотно и ручей");

                Assert.AreEqual(0, parent.GetComponentsInChildren<Collider>().Length,
                    "у воды нет коллайдеров: это меш, а не препятствие");
                Assert.AreEqual(names.Count, track.Count,
                    "каждый меш воды отдан миру на снос");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parent);
                foreach (var m in track) if (m != null) UnityEngine.Object.DestroyImmediate(m);
                UnityEngine.Object.DestroyImmediate(mat);
                UnityEngine.Object.DestroyImmediate(cfg);
            }
        }

        [Test]
        public void Build_InvalidCanyon_KeepsOnlyLakeAndRiver()
        {
            var parent = new GameObject("~DomeWaterShapeInvalid");
            var cfg = Config();
            var hydro = Hydro();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var track = new List<Mesh>();
            var none = new DomeCanyon.State { valid = false };
            try
            {
                DomeWaterMesh.Build(parent.transform, cfg, hydro, none, Slope, mat, track);

                var names = new HashSet<string>();
                foreach (Transform child in parent.transform) names.Add(child.name);
                CollectionAssert.AreEquivalent(new[] { "Lake", "River" }, names,
                    "без каньона поведение прежнее: только озеро и река");

                Assert.AreEqual(0, DomeWaterMesh.StreamProfile(none, Slope).Count,
                    "невалидный каньон не даёт ручья");
                Assert.AreEqual(0, DomeWaterMesh.PoolDisc(none, 0f).vertexCount,
                    "невалидный каньон не даёт плёса");
                Assert.AreEqual(0, DomeWaterMesh.FallSheet(none, 1f, 0f).vertexCount,
                    "невалидный каньон не даёт полотна");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parent);
                foreach (var m in track) if (m != null) UnityEngine.Object.DestroyImmediate(m);
                UnityEngine.Object.DestroyImmediate(mat);
                UnityEngine.Object.DestroyImmediate(cfg);
            }
        }

        // ---- настоящее поле -------------------------------------------------

        /// <summary>
        /// Тот же закон на настоящем поле Ø2000/1337: вода не висит, плёс ниже липа,
        /// ручей не поднимается. Пол — повторение <c>DomeWorld.SampleGround</c> без
        /// площадки лаборатории и оврагов (каньон защищён от пятна, они не влияют).
        /// </summary>
        [Test]
        public void RealField_WaterStaysOnGround_AndPoolBelowLip()
        {
            var cfg = Config();
            try
            {
                var hydro = DomeHydro.Build(cfg, 256);
                Vector2 dir = -DomeFacilityLayout.HangarSpot(cfg, 1337L);
                if (dir.sqrMagnitude < 1e-6f) dir = Vector2.down;
                dir.Normalize();
                float rin = DomeRockRing.InnerRadius(cfg);
                Vector2 entry = dir * Mathf.Max(0f, rin - DomeWorld.EntryInset);
                Vector2 lab = Vector2.zero;

                var terrace = DomeTerrace.Build(cfg, 1337L, lab, entry, hydro, rin);
                var canyon = DomeCanyon.Build(cfg, 1337L, hydro, entry, lab, rin);
                Assert.IsTrue(canyon.valid, "на Ø2000/1337 каньон строится");

                Func<float, float, float> ground = (x, z) =>
                {
                    float raw = DomeHydro.SampleHydro(cfg, hydro, x, z);
                    raw = DomeTerrace.Apply(raw, terrace, x, z);
                    return DomeCanyon.Apply(raw, canyon, x, z, hydro.lakeLevel);
                };

                float poolLevel = DomeWaterMesh.PoolLevel(canyon, hydro.lakeLevel, ground);
                float topLevel = DomeWaterMesh.FallTopLevel(canyon, ground);

                Assert.GreaterOrEqual(poolLevel, hydro.lakeLevel + DomeWaterMesh.PoolLift - 1e-3f,
                    "плёс не ниже уровня озера");
                Assert.GreaterOrEqual(poolLevel,
                    ground(canyon.pool.x, canyon.pool.y) + DomeWaterMesh.PoolLift - 1e-3f,
                    "плёс не утоплен в дно");
                Assert.Less(poolLevel, topLevel, "плёс ниже липа");

                var profile = DomeWaterMesh.StreamProfile(canyon, ground);
                Assert.Greater(profile.Count, 2, "ручей от плёса до устья");
                float prev = float.PositiveInfinity;
                foreach (var p in profile)
                {
                    float lift = p.y - ground(p.x, p.z);
                    Assert.LessOrEqual(lift, DomeWaterMesh.StreamLift + 0.02f,
                        $"ручей не висит (x={p.x:F0}; z={p.z:F0})");
                    Assert.GreaterOrEqual(lift, DomeWaterMesh.StreamMinLift - 0.02f,
                        $"ручей не под дном (x={p.x:F0}; z={p.z:F0})");
                    Assert.LessOrEqual(p.y, prev + 0.02f,
                        $"ручей не течёт вверх (x={p.x:F0}; z={p.z:F0})");
                    prev = p.y;
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cfg);
            }
        }
    }
}
