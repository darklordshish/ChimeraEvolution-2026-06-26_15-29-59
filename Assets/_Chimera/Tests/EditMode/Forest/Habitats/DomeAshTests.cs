using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// Ясень: место в кольце 100–400м от лабы, полого, на суше, детерминировано;
    /// гнездо — «Сова»/Nest с радиусами под крону; крона объёмна. Слайс s10g.
    /// </summary>
    public class DomeAshTests
    {
        static DomeGenConfigSO Config(long seed)
        {
            var cfg = ScriptableObject.CreateInstance<DomeGenConfigSO>();
            cfg.seed = seed;
            cfg.randomSeed = false;
            cfg.mapDiameter = 2000f;
            return cfg;
        }

        [Test]
        public void Spot_RingAroundLab_OnDryFlat_AndDeterministic()
        {
            var cfg = Config(1337);
            try
            {
                var hydro = DomeHydro.Build(cfg, 128);
                var a = DomeAshSite.FindSpot(cfg, 4242, Vector2.zero, hydro, 96);
                var b = DomeAshSite.FindSpot(cfg, 4242, Vector2.zero, hydro, 96);
                Assert.AreEqual(a.pos.x, b.pos.x, 1e-6f, "бит-в-бит");
                float d = a.pos.magnitude;
                Assert.GreaterOrEqual(d, 100f, "дальше 100м от лабы");
                Assert.LessOrEqual(d, 400f, "ближе 400м (виден и слышен)");
                Assert.Less(DomeSlope.SlopeAt(cfg, a.pos.x, a.pos.y), 15f, "полого");
                float dx = a.pos.x - hydro.lakeCenter.x, dz = a.pos.y - hydro.lakeCenter.y;
                Assert.Greater(dx * dx + dz * dz, hydro.lakeRadius * hydro.lakeRadius, "не в озере");
            }
            finally
            {
                Object.DestroyImmediate(cfg);
            }
        }

        [Test]
        public void Nest_IsOwl_AndValid()
        {
            var go = new GameObject("~OwlProbe");
            try
            {
                var site = go.AddComponent<LairSite>();
                DomeAshSite.ConfigureNest(site, new Vector3(10f, 30f, 5f));
                Assert.AreEqual("Сова", site.speciesName, "вид");
                Assert.AreEqual(LairSite.LairAreaType.Nest, site.areaType, "тип");
                Assert.AreEqual(new Vector3(10f, 30f, 5f), site.transform.position, "в кроне");
                Assert.IsEmpty(LairRules.CheckSite(site.speciesName, site.homeRadius, site.spawnRadius,
                    site.fearRadius, site.tier, site.capacity, site.respawnSeconds), "валидно");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void AshCrown_HasVolume()
        {
            var mesh = FloraMeshKit.AshCrown(12f);
            try
            {
                Assert.AreEqual(7 * 20 * 3, mesh.vertexCount, "7 икосаэдров × 20 граней × 3");
                Assert.Greater(mesh.bounds.size.x, 20f, "крона широкая (дом совы виден)");
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }
    }
}
