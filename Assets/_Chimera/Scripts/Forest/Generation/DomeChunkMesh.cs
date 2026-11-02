using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Меш одного чанка павильона (s10a): фасетка quads×quads квадов из DomeHeightField.
/// Координаты мировые (чанковый оффсет + локалка) — соседние чанки семплят то же поле,
/// общий бордер сходится 1:1 по построению (сторожит DomeChunkTests).
/// Обход — как у TerrainApplier (p00,p11,p10)+(p00,p01,p11), нормали — честные
/// гранные (RecalculateNormals по дублированным вершинам): лоу-поли язык мира.
/// </summary>
public static class DomeChunkMesh
{
    public static Mesh BuildChunkMesh(DomeGenConfigSO cfg, int cx, int cz, int quadsPerSide)
    {
        if (cfg == null) throw new ArgumentNullException(nameof(cfg));
        return BuildChunkMesh(cfg, cx, cz, quadsPerSide,
            (x, z) => DomeHeightField.SampleHeight(cfg, x, z));
    }

    /// <summary>Тот же чанк, но высоты — произвольным семплером (гидрология с вырезом).</summary>
    public static Mesh BuildChunkMesh(DomeGenConfigSO cfg, int cx, int cz, int quadsPerSide,
        Func<float, float, float> sampler, bool slopeBands = false, long seed = 0)
    {
        if (cfg == null) throw new ArgumentNullException(nameof(cfg));
        if (sampler == null) throw new ArgumentNullException(nameof(sampler));
        int res = Mathf.Clamp(quadsPerSide, 4, 128);
        float half = cfg.mapDiameter * 0.5f;
        float x0 = -half + cx * cfg.chunkSize;
        float z0 = -half + cz * cfg.chunkSize;
        float step = cfg.chunkSize / res;
        // Высоты вершин считаются ОДИН раз: четыре угла на квад давали 9216
        // SampleGround на чанк (52 чанка — 12 с); сетка (res+1)² — 2401.
        var heights = new float[(res + 1) * (res + 1)];
        // Строки сетки независимы; SampleGround — чистая функция. Это 125k сэмплов
        // на мир (52 чанка), параллель по строкам снимает ~2 с.
        System.Threading.Tasks.Parallel.For(0, res + 1, iz =>
        {
            for (int ix = 0; ix <= res; ix++)
                heights[iz * (res + 1) + ix] = sampler(x0 + ix * step, z0 + iz * step);
        });
        Vector3 P(int ix, int iz) =>
            new Vector3(x0 + ix * step, heights[iz * (res + 1) + ix], z0 + iz * step);
        var verts = new Vector3[res * res * 6];
        int v = 0;
        for (int iz = 0; iz < res; iz++)
            for (int ix = 0; ix < res; ix++)
            {
                Vector3 p00 = P(ix, iz);
                Vector3 p10 = P(ix + 1, iz);
                Vector3 p01 = P(ix, iz + 1);
                Vector3 p11 = P(ix + 1, iz + 1);
                verts[v++] = p00;
                verts[v++] = p11;
                verts[v++] = p10;
                verts[v++] = p00;
                verts[v++] = p01;
                verts[v++] = p11;
            }
        var tris = new int[verts.Length];
        for (int i = 0; i < tris.Length; i++) tris[i] = i;
        var mesh = new Mesh { name = $"DomeChunk_{cx}_{cz}" };
        mesh.vertices = verts;
        mesh.triangles = tris;
        if (slopeBands)
        {
            // S2 §4: 5 полос склона; шум ±2° уже в DomeSlopeBands.
            var bands = new List<int>[5];
            for (int b = 0; b < bands.Length; b++) bands[b] = new List<int>();
            for (int i = 0; i < tris.Length; i += 3)
            {
                Vector3 a = verts[tris[i]], b = verts[tris[i + 1]], c = verts[tris[i + 2]];
                // Уклон грани из её же нормали: поле уже в вершинах, четыре повторных
                // SampleGround на каждый треугольник давали 33 с на 52 чанка.
                Vector3 n = Vector3.Cross(b - a, c - a).normalized;
                float slope = Mathf.Acos(Mathf.Clamp(Mathf.Abs(n.y), 0f, 1f)) * Mathf.Rad2Deg;
                Vector3 mid = (a + b + c) / 3f;
                int band = DomeSlopeBands.Index(slope, mid.x, mid.z, seed);
                bands[band].Add(tris[i]);
                bands[band].Add(tris[i + 1]);
                bands[band].Add(tris[i + 2]);
            }
            mesh.subMeshCount = 5;
            for (int b = 0; b < 5; b++) mesh.SetTriangles(bands[b], b, false);
        }
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

}
