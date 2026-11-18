using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Chimera.Tests.PlayMode
{
    /// <summary>
    /// КАРТА КАК ИСТОЧНИК, шаг 2 (спека `2026-11-18-forest-karta-kak-istochnik` §5, §11): рельеф v2 — слой карты.
    /// `ForestMapRelief` хранит рецепт поля и считает высоту по СВОЕЙ копии `DomeTerrain`, собранной из рецепта. Сторожа:
    /// высота карты совпадает с полом мира побитно на всей сетке; JSON туда и обратно её не меняет; хеш карты держит
    /// ключ эрозии, а не сетку (ГеймБосс 18.11: float-разнобой машин не должен менять хеш); чанки v2 строятся по карте.
    /// </summary>
    public class ForestMapReliefTests
    {
        static GameObject host;
        static ForestBuilder builder;

        [OneTimeSetUp]
        public void BuildWorld()
        {
            host = new GameObject("~ForestMapReliefProbe");
            host.SetActive(false);
            builder = ForestProfiles.PavilionV2Zone(host, 1337L);
            builder.buildOnAwake = false;
            host.SetActive(true);
            builder.Build();
        }

        [OneTimeTearDown]
        public void TearDownWorld()
        {
            if (builder != null) builder.Clear();
            if (host != null) Object.Destroy(host);
            builder = null; host = null;
        }

        /// <summary>Число точек сетки шага `meshStep` (385×385 на Ø2000/48), где высоты расходятся хоть в бите.</summary>
        static int Mismatches(Func<float, float, float> a, Func<float, float, float> b, out string first)
        {
            float step = builder.Contract.meshStep;
            int n = Mathf.RoundToInt(builder.mapDiameter / step) + 1;
            float start = -builder.mapDiameter * 0.5f;
            int bad = 0; first = null;
            for (int iz = 0; iz < n; iz++)
                for (int ix = 0; ix < n; ix++)
                {
                    float x = start + ix * step, z = start + iz * step;
                    float ha = a(x, z), hb = b(x, z);
                    if (BitConverter.SingleToInt32Bits(ha) == BitConverter.SingleToInt32Bits(hb)) continue;
                    if (bad++ == 0) first = $"({x:0.##}, {z:0.##}): {ha:R} против {hb:R}";
                }
            return bad;
        }

        [UnityTest]
        public IEnumerator Relief_HeightEqualsWorldGround()
        {
            var r = builder.Map.relief;
            Assert.IsNotNull(r, "у карты v2 есть рельеф");
            int bad = Mismatches(r.Height, builder.SampleGround, out string first);
            Assert.AreEqual(0, bad, $"высота карты = пол мира побитно; первое расхождение {first}");
            yield break;
        }

        [UnityTest]
        public IEnumerator Relief_JsonRoundTrip_SameHeights()
        {
            var back = ForestMap.FromJson(builder.Map.ToJson());
            back.Restore();
            Assert.AreEqual(builder.Map.hash, ForestMap.ComputeHash(back), "хеш переживает JSON");
            int bad = Mismatches(back.relief.Height, builder.SampleGround, out string first);
            Assert.AreEqual(0, bad, $"рельеф из JSON = пол мира побитно; первое расхождение {first}");
            yield break;
        }

        [UnityTest]
        public IEnumerator Relief_HashIgnoresErosionGrid()
        {
            var copy = ForestMap.FromJson(builder.Map.ToJson());
            Assert.IsNotNull(copy.relief.erosion, "у профиля павильона v2 есть эрозия");
            Assert.Greater(copy.relief.erosion.delta.Length, 0, "сетка эрозии в JSON");
            copy.relief.erosion.delta[0] += 0.5f;
            Assert.AreEqual(builder.Map.hash, ForestMap.ComputeHash(copy), "сетка эрозии в хеш не входит");
            copy.relief.erosionKey += "_другой";
            Assert.AreNotEqual(builder.Map.hash, ForestMap.ComputeHash(copy), "ключ эрозии в хеш входит");
            yield break;
        }

        [UnityTest]
        public IEnumerator Relief_RefusesUnsupportedLayers()
        {
            var t = new DomeTerrain
            {
                cfg = ScriptableObject.CreateInstance<DomeGenConfigSO>(),
                hydro = new DomeHydro.State(),
                worldVersion = 2,
                composition = new DomeComposition.State(),
                hill = new DomeHill.State { valid = true, hole = (x, z) => false },
            };
            try { Assert.Throws<NotSupportedException>(() => ForestMapRelief.From(t, 1337L), "холм с дыркой — не молча"); }
            finally { Object.Destroy(t.cfg); }
            yield break;
        }

        [UnityTest]
        public IEnumerator Chunks_ReadMapRelief()
        {
            var world = builder.World;
            Assert.IsNotNull(world.MapRelief, "мир v2 снял рельеф карты до чанков");
            Assert.AreSame(world.MapRelief, builder.Map.relief, "карта держит тот же рельеф, по которому построены чанки");
            var root = builder.BuiltRoot.transform;
            int checkedChunks = 0, verts = 0, bad = 0;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
            {
                if (!mf.name.StartsWith("Chunk_")) continue;
                var mesh = mf.sharedMesh;
                if (mesh == null || mesh.vertexCount == 0) continue;
                if (checkedChunks++ % 9 != 0) continue; // каждый девятый чанк — по всей чаше
                foreach (var v in mesh.vertices)
                {
                    Vector3 p = root.InverseTransformPoint(mf.transform.TransformPoint(v));
                    float h = builder.Map.relief.Height(p.x, p.z);
                    if (Mathf.Abs(h - p.y) > 1e-3f) bad++;
                    verts++;
                }
            }
            Assert.Greater(verts, 1000, "вершины чанков найдены");
            Assert.AreEqual(0, bad, $"вершины чанков лежат на высоте карты ({verts} проверено)");
            yield break;
        }
    }
}
