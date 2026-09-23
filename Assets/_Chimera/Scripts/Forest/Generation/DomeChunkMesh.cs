using System;
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
        Func<float, float, float> sampler)
    {
        if (cfg == null) throw new ArgumentNullException(nameof(cfg));
        if (sampler == null) throw new ArgumentNullException(nameof(sampler));
        int res = Mathf.Clamp(quadsPerSide, 4, 128);
        float half = cfg.mapDiameter * 0.5f;
        float x0 = -half + cx * cfg.chunkSize;
        float z0 = -half + cz * cfg.chunkSize;
        float step = cfg.chunkSize / res;
        var verts = new Vector3[res * res * 6];
        int v = 0;
        for (int iz = 0; iz < res; iz++)
            for (int ix = 0; ix < res; ix++)
            {
                Vector3 p00 = Point(sampler, x0 + ix * step, z0 + iz * step);
                Vector3 p10 = Point(sampler, x0 + (ix + 1) * step, z0 + iz * step);
                Vector3 p01 = Point(sampler, x0 + ix * step, z0 + (iz + 1) * step);
                Vector3 p11 = Point(sampler, x0 + (ix + 1) * step, z0 + (iz + 1) * step);
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
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static Vector3 Point(Func<float, float, float> sampler, float x, float z)
    {
        return new Vector3(x, sampler(x, z), z);
    }
}
