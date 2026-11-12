using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// S4: тепловая эрозия на снимке Shape0, маска авторских форм, детерминизм,
    /// сохранение массы и гидро-кеш по ключу (seed, worldVersion, erosionVersion, resolution).
    /// </summary>
    public class ErosionFieldTests
    {
        DomeGenConfigSO cfg;
        string cacheDir;

        [TearDown]
        public void TearDown()
        {
            if (cfg != null) UnityEngine.Object.DestroyImmediate(cfg);
            cfg = null;
            DomeErosion.CacheDir = null;
            if (!string.IsNullOrEmpty(cacheDir) && Directory.Exists(cacheDir))
                Directory.Delete(cacheDir, true);
            cacheDir = null;
        }

        DomeGenConfigSO Config(bool thermal, bool hydro = false)
        {
            cfg = ScriptableObject.CreateInstance<DomeGenConfigSO>();
            cfg.mapDiameter = 200f;
            cfg.chunkSize = 100f;
            cfg.worldVersion = 2;
            cfg.erosionResolution = 64;
            cfg.buildThermalErosion = thermal;
            cfg.buildHydraulicErosion = hydro;
            cfg.thermalIterations = 10;
            cfg.thermalTalusDegrees = 30f;
            cfg.hydroDrops = 2000;
            cfg.hydroSteps = 12;
            cfg.hydroRadius = 2;
            return cfg;
        }

        static float Ground(float x, float z) =>
            20f + 0.65f * x + 0.2f * Mathf.Sin(x * 0.13f) * Mathf.Cos(z * 0.11f);

        static float ShareOver(float[] h, int n, float step, float threshold)
        {
            int over = 0, total = 0;
            for (int z = 0; z < n; z++)
                for (int x = 0; x < n - 1; x++)
                {
                    float dx = (h[z * n + x + 1] - h[z * n + x]) / step;
                    float slope = Mathf.Atan(Mathf.Abs(dx)) * Mathf.Rad2Deg;
                    if (slope > threshold) over++;
                    total++;
                }
            return total > 0 ? (float)over / total : 0f;
        }

        [Test]
        public void Off_IsIdentity()
        {
            var c = Config(false);
            var st = DomeErosion.Build(c, Ground, null, 1337L);
            foreach (float d in st.delta) Assert.AreEqual(0f, d, 1e-6f,
                "оба флага выключены — дельта нулевая");
        }

        [Test]
        public void Thermal_IsDeterministic()
        {
            var c = Config(true);
            var a = DomeErosion.Build(c, Ground, null, 1337L);
            var b = DomeErosion.Build(c, Ground, null, 1337L);
            Assert.AreEqual(a.deltaSum, b.deltaSum, "тот же сид/параметры — та же дельта");
            for (int i = 0; i < a.delta.Length; i++)
                Assert.AreEqual(a.delta[i], b.delta[i], 1e-6f, $"ячейка {i}");
        }

        [Test]
        public void Thermal_ConservesMass()
        {
            var c = Config(true);
            var st = DomeErosion.Build(c, Ground, null, 1337L);
            double sum = 0, abs = 0;
            foreach (float d in st.delta) { sum += d; abs += Math.Abs(d); }
            Assert.Less(Math.Abs(sum), Math.Max(1e-3, abs * 0.02),
                "тепловая эрозия не теряет материал больше 2%");
        }

        [Test]
        public void ProtectedMask_IsZero()
        {
            var c = Config(true);
            var st = DomeErosion.Build(c, Ground, (x, z) => x < 0f, 1337L);
            for (int z = 0; z < st.resolution; z++)
                for (int x = 0; x < st.resolution; x++)
                    if (st.mask[z * st.resolution + x])
                        Assert.AreEqual(0f, st.delta[z * st.resolution + x], 1e-6f,
                            "в маске дельта обязана быть 0");
        }

        [Test]
        public void Thermal_ReducesOverTalusShare()
        {
            var c = Config(true);
            int n = c.erosionResolution;
            float step = c.mapDiameter / n;
            var before = new float[n * n];
            for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++)
                    before[z * n + x] = Ground(
                        -c.mapDiameter * 0.5f + (x + 0.5f) * step,
                        -c.mapDiameter * 0.5f + (z + 0.5f) * step);
            var st = DomeErosion.Build(c, Ground, null, 1337L);
            var after = new float[n * n];
            for (int i = 0; i < after.Length; i++) after[i] = before[i] + st.delta[i];
            float s0 = ShareOver(before, n, step, 32f);
            float s1 = ShareOver(after, n, step, 32f);
            Assert.LessOrEqual(s1, s0 + 1e-4f,
                $"доля >talus+2° не выросла: {s0:P2} → {s1:P2}");
            float md=0f; foreach(var d in st.delta) md=Mathf.Max(md,Mathf.Abs(d)); Assert.Pass($"share>32°: {s0:P2} → {s1:P2}; max|delta|={md:F3}");
        }


        [Test]
        public void MissingBake_FallsBackToThermal()
        {
            cacheDir = Path.Combine(Path.GetTempPath(), "chimera-erosion-file-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(cacheDir);
            string asFile = Path.Combine(cacheDir, "not-a-dir");
            File.WriteAllText(asFile, "x");
            DomeErosion.CacheDir = asFile; // путь к файлу: запись кеша невозможна
            var c = Config(true, true);
            var st = DomeErosion.Build(c, Ground, null, 1337L);
            Assert.IsFalse(st.hydraulic, "гидро не должно считаться выполненным");
            Assert.IsTrue(st.hydraulicStatus.StartsWith("missing"),
                "fallback обязан быть явным: " + st.hydraulicStatus);
            double sum = 0;
            foreach (float d in st.delta) sum += Math.Abs(d);
            Assert.Greater(sum, 1e-4, "тепловая эрозия должна остаться");
        }
        [Test]
        public void Hydraulic_CacheKey_IsComplete()
        {
            string a = DomeErosion.Key(1337, 2, 3, 512);
            Assert.AreNotEqual(a, DomeErosion.Key(1338, 2, 3, 512), "seed");
            Assert.AreNotEqual(a, DomeErosion.Key(1337, 3, 3, 512), "worldVersion");
            Assert.AreNotEqual(a, DomeErosion.Key(1337, 2, 4, 512), "erosionVersion");
            Assert.AreNotEqual(a, DomeErosion.Key(1337, 2, 3, 256), "resolution");
        }

        [Test]
        public void Hydraulic_Bake_AndCacheHit_AreEqual()
        {
            cacheDir = Path.Combine(Path.GetTempPath(), "chimera-erosion-" + Guid.NewGuid().ToString("N"));
            DomeErosion.CacheDir = cacheDir;
            var c = Config(true, true);
            var fresh = DomeErosion.Build(c, Ground, null, 1337L);
            Assert.IsTrue(fresh.hydraulic, "гидро не запечено: " + fresh.hydraulicStatus);
            var hit = DomeErosion.Build(c, Ground, null, 1337L);
            Assert.AreEqual(fresh.deltaSum, hit.deltaSum, "кеш и свежее запекание — одна дельта");
            Assert.AreEqual("cache", hit.hydraulicStatus, "второй раз — попадание в кеш");
            Assert.Pass($"{fresh.hydraulicStatus}/{hit.hydraulicStatus} sum={fresh.deltaSum:x16}");
        }
    }
}
