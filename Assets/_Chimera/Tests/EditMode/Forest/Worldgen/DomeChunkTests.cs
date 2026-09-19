using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>
    /// Чанк павильона: фасетка quads²×6, высоты в ±amplitude, соседние чанки
    /// сходятся по общему бордеру 1:1 (то же поле, те же координаты). Слайс s10a.
    /// </summary>
    public class DomeChunkTests
    {
        static DomeGenConfigSO Config(long seed)
        {
            var cfg = ScriptableObject.CreateInstance<DomeGenConfigSO>();
            cfg.seed = seed;
            cfg.randomSeed = false;
            cfg.mapDiameter = 500f;
            cfg.chunkSize = 250f;
            return cfg;
        }

        [Test]
        public void Chunk_HasFacetedVertexCount_AndFiniteHeights()
        {
            var cfg = Config(1337);
            var mesh = DomeChunkMesh.BuildChunkMesh(cfg, 0, 0, 8);
            try
            {
                Assert.AreEqual(8 * 8 * 6, mesh.vertexCount, "фасетка: 6 вершин на квад");
                foreach (var v in mesh.vertices)
                {
                    Assert.IsTrue(Mathf.Abs(v.y) <= cfg.amplitude + 1e-4f, "высота в ±amplitude");
                    Assert.IsFalse(float.IsNaN(v.y) || float.IsInfinity(v.y), "конечные высоты");
                }
                Assert.AreEqual(cfg.chunkSize, mesh.bounds.size.x, 1e-3f, "чанк покрывает свой квадрат");
            }
            finally
            {
                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(cfg);
            }
        }

        [Test]
        public void NeighbourChunks_MeetSeamlessly()
        {
            var cfg = Config(1337);
            var left = DomeChunkMesh.BuildChunkMesh(cfg, 0, 0, 8);
            var right = DomeChunkMesh.BuildChunkMesh(cfg, 1, 0, 8);
            try
            {
                float borderX = -cfg.mapDiameter * 0.5f + cfg.chunkSize;
                var a = EdgeVerts(left.vertices, borderX);
                var b = EdgeVerts(right.vertices, borderX);
                Assert.AreEqual(a.Count, b.Count, "шов: поровну вершин с обеих сторон");
                Assert.Greater(a.Count, 0, "шов обязан содержать вершины");
                // Углы встречаются с разных сторон разное число раз (p00/p11 дублируются,
                // p10/p01 — нет), поэтому сравниваем МНОЖЕСТВА точек шва, а не вершины.
                var setA = SeamSet(a);
                var setB = SeamSet(b);
                Assert.AreEqual(setA.Count, setB.Count, "шов: поровну уникальных точек");
                foreach (var key in setA.Keys)
                {
                    Assert.IsTrue(setB.ContainsKey(key), $"шов: точка {key} без пары");
                    Assert.AreEqual(setA[key], setB[key], 1e-4f, $"шов: высота точки {key} с обеих сторон");
                }
            }
            finally
            {
                Object.DestroyImmediate(left);
                Object.DestroyImmediate(right);
                Object.DestroyImmediate(cfg);
            }
        }

        static List<Vector3> EdgeVerts(Vector3[] verts, float borderX)
        {
            var out_ = new List<Vector3>();
            foreach (var v in verts)
                if (Mathf.Abs(v.x - borderX) < 1e-4f)
                    out_.Add(v);
            return out_;
        }

        static Dictionary<Vector2Int, float> SeamSet(List<Vector3> verts)
        {
            var set = new Dictionary<Vector2Int, float>();
            foreach (var v in verts)
            {
                var key = new Vector2Int(Mathf.RoundToInt(v.x * 1e4f), Mathf.RoundToInt(v.z * 1e4f));
                if (set.TryGetValue(key, out float y))
                    Assert.AreEqual(y, v.y, 1e-4f, "дубли одной точки обязаны совпадать по высоте");
                else
                    set[key] = v.y;
            }
            return set;
        }
    }
}
