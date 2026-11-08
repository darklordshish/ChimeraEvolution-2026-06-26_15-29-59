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
    /// <summary>
    /// Адаптивный чанк: базовая сетка res×res, в полосе refine — 4×4
    /// на клетку. Переходы — веер от центра клетки по общим кромкам,
    /// T-щелей нет.
    /// </summary>
    public static Mesh BuildAdaptiveChunkMesh(DomeGenConfigSO cfg, int cx, int cz,
        int baseQuads, int fineFactor, Func<float, float, bool> refine,
        Func<float, float, float> sampler, bool slopeBands = false, long seed = 0,
        Func<float, float, bool> skipAt = null)
    {
        if (cfg == null) throw new ArgumentNullException(nameof(cfg));
        if (sampler == null) throw new ArgumentNullException(nameof(sampler));
        int res = Mathf.Clamp(baseQuads, 4, 96);
        int ff = Mathf.Clamp(fineFactor, 2, 4);
        float half = cfg.mapDiameter * 0.5f;
        float x0 = -half + cx * cfg.chunkSize;
        float z0 = -half + cz * cfg.chunkSize;
        float coarse = cfg.chunkSize / res;
        float fine = coarse / ff;
        int fineRes = res * ff;

        var fineCell = new bool[res, res];
        for (int iz = 0; iz < res; iz++)
            for (int ix = 0; ix < res; ix++)
            {
                float x = x0 + (ix + 0.5f) * coarse;
                float z = z0 + (iz + 0.5f) * coarse;
                fineCell[ix, iz] = refine != null && refine(x, z);
            }

        var map = new Dictionary<long, int>();
        var verts = new List<Vector3>();
        var tris = new List<int>();
        long Key(int fx, int fz) => (long)fx * 100003 + fz;
        int Get(int fx, int fz)
        {
            long key = Key(fx, fz);
            if (map.TryGetValue(key, out int i)) return i;
            float x = x0 + fx * fine, z = z0 + fz * fine;
            i = verts.Count;
            verts.Add(new Vector3(x, sampler(x, z), z));
            map[key] = i;
            return i;
        }
        int Center(int ix, int iz) => Get(ix * ff + ff / 2, iz * ff + ff / 2);
        void Tri(int a, int b, int c) { tris.Add(a); tris.Add(b); tris.Add(c); }

        for (int iz = 0; iz < res; iz++)
            for (int ix = 0; ix < res; ix++)
            {
                if (fineCell[ix, iz])
                {
                    for (int a = 0; a < ff; a++)
                        for (int b = 0; b < ff; b++)
                        {
                            int fx = ix * ff + a, fz = iz * ff + b;
                            int v00 = Get(fx, fz), v10 = Get(fx + 1, fz);
                            int v01 = Get(fx, fz + 1), v11 = Get(fx + 1, fz + 1);
                            Tri(v00, v11, v10); Tri(v00, v01, v11);
                        }
                    continue;
                }

                bool south = iz > 0 && fineCell[ix, iz - 1];
                bool north = iz + 1 < res && fineCell[ix, iz + 1];
                bool west = ix > 0 && fineCell[ix - 1, iz];
                bool east = ix + 1 < res && fineCell[ix + 1, iz];
                var ring = new List<Vector2Int>();
                void Side(int ax, int az, int bx, int bz, bool fine)
                {
                    int n = fine ? ff : 1;
                    for (int k = 0; k <= n; k++)
                    {
                        if (ring.Count == res * 4 + 1 && k == 0) continue;
                        ring.Add(new Vector2Int(
                            Mathf.RoundToInt(Mathf.Lerp(ax, bx, k / (float)n)),
                            Mathf.RoundToInt(Mathf.Lerp(az, bz, k / (float)n))));
                    }
                }
                int L = ix * ff, R = (ix + 1) * ff;
                int B = iz * ff, T = (iz + 1) * ff;
                Side(L, B, R, B, south);
                Side(R, B, R, T, east);
                Side(R, T, L, T, north);
                Side(L, T, L, B, west);
                // remove consecutive duplicates
                for (int i = ring.Count - 1; i > 0; i--)
                    if (ring[i] == ring[i - 1]) ring.RemoveAt(i);
                int c = Center(ix, iz);
                for (int i = 0; i < ring.Count; i++)
                {
                    Vector2Int a = ring[i], b = ring[(i + 1) % ring.Count];
                    Tri(c, Get(a.x, a.y), Get(b.x, b.y));
                }
            }

        var mesh = new Mesh { name = $"DomeChunk_{cx}_{cz}" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        if (slopeBands)
        {
            var bands = new List<int>[5];
            for (int b = 0; b < bands.Length; b++) bands[b] = new List<int>();
            for (int i = 0; i < tris.Count; i += 3)
            {
                Vector3 a = verts[tris[i]], b = verts[tris[i + 1]], c = verts[tris[i + 2]];
                Vector3 n = Vector3.Cross(b - a, c - a).normalized;
                float slope = Mathf.Acos(Mathf.Clamp(Mathf.Abs(n.y), 0f, 1f)) * Mathf.Rad2Deg;
                Vector3 mid = (a + b + c) / 3f;
                int band = DomeSlopeBands.Index(slope, mid.x, mid.z, seed, cfg.WorldVersion);
                bands[band].Add(tris[i]); bands[band].Add(tris[i + 1]); bands[band].Add(tris[i + 2]);
            }
            mesh.subMeshCount = 5;
            for (int b = 0; b < 5; b++) mesh.SetTriangles(bands[b], b, false);
        }
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    public static Mesh BuildChunkMesh(DomeGenConfigSO cfg, int cx, int cz, int quadsPerSide)
    {
        if (cfg == null) throw new ArgumentNullException(nameof(cfg));
        return BuildChunkMesh(cfg, cx, cz, quadsPerSide,
            (x, z) => DomeHeightField.SampleHeight(cfg, x, z));
    }

    /// <summary>Тот же чанк, но высоты — произвольным семплером (гидрология с вырезом).</summary>
    public static Mesh BuildChunkMesh(DomeGenConfigSO cfg, int cx, int cz, int quadsPerSide,
        Func<float, float, float> sampler, bool slopeBands = false, long seed = 0,
        Func<float, float, bool> skipAt = null)
    {
        if (skipAt != null)
            return BuildChunkMeshClipped(cfg, cx, cz, quadsPerSide, sampler,
                slopeBands, seed, skipAt);
        if (cfg == null) throw new ArgumentNullException(nameof(cfg));
        if (sampler == null) throw new ArgumentNullException(nameof(sampler));
        int res = Mathf.Clamp(quadsPerSide, 4, 192);
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
                int band = DomeSlopeBands.Index(slope, mid.x, mid.z, seed, cfg.WorldVersion);
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

    /// <summary>
    /// Тот же чанк с вырезом footprint. Ячейка делится 4×4 только у кромки:
    /// внутри выреза треугольников нет, снаружи — обычная сетка. Граница
    /// ступенчатая не грубее 1.3 м; юбка префаба её закрывает.
    /// </summary>
    static Mesh BuildChunkMeshClipped(DomeGenConfigSO cfg, int cx, int cz,
        int quadsPerSide, Func<float, float, float> sampler, bool slopeBands,
        long seed, Func<float, float, bool> skipAt)
    {
        int res = Mathf.Clamp(quadsPerSide, 4, 192);
        float half = cfg.mapDiameter * 0.5f;
        float x0 = -half + cx * cfg.chunkSize;
        float z0 = -half + cz * cfg.chunkSize;
        float step = cfg.chunkSize / res;
        var verts = new List<Vector3>();
        var tris = new List<int>();
        Vector3 P(float x, float z) => new Vector3(x, sampler(x, z), z);
        void Quad(float ax, float az, float bx, float bz)
        {
            Vector3 p00 = P(ax, az), p10 = P(bx, az);
            Vector3 p01 = P(ax, bz), p11 = P(bx, bz);
            int i = verts.Count;
            verts.Add(p00); verts.Add(p11); verts.Add(p10);
            verts.Add(p00); verts.Add(p01); verts.Add(p11);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            tris.Add(i + 3); tris.Add(i + 4); tris.Add(i + 5);
        }
        const int sub = 4;
        float fine = step / sub;
        for (int iz = 0; iz < res; iz++)
            for (int ix = 0; ix < res; ix++)
            {
                float ax = x0 + ix * step, bx = ax + step;
                float az = z0 + iz * step, bz = az + step;
                bool c00 = skipAt(ax, az), c10 = skipAt(bx, az);
                bool c01 = skipAt(ax, bz), c11 = skipAt(bx, bz);
                bool center = skipAt((ax + bx) * 0.5f, (az + bz) * 0.5f);
                bool all = c00 && c10 && c01 && c11 && center;
                bool any = c00 || c10 || c01 || c11 || center;
                if (all) continue;
                if (!any) { Quad(ax, az, bx, bz); continue; }

                for (int sz = 0; sz < sub; sz++)
                    for (int sx = 0; sx < sub; sx++)
                    {
                        float sxa = ax + sx * fine, sxb = sxa + fine;
                        float sza = az + sz * fine, szb = sza + fine;
                        bool s00 = skipAt(sxa, sza), s10 = skipAt(sxb, sza);
                        bool s01 = skipAt(sxa, szb), s11 = skipAt(sxb, szb);
                        bool sc = skipAt((sxa + sxb) * 0.5f, (sza + szb) * 0.5f);
                        if (s00 || s10 || s01 || s11 || sc) continue;
                        Quad(sxa, sza, sxb, szb);
                    }
            }

        var mesh = new Mesh { name = $"DomeChunk_{cx}_{cz}" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        if (slopeBands)
        {
            var bands = new List<int>[5];
            for (int b = 0; b < bands.Length; b++) bands[b] = new List<int>();
            for (int i = 0; i < tris.Count; i += 3)
            {
                Vector3 a = verts[tris[i]], b = verts[tris[i + 1]], c = verts[tris[i + 2]];
                Vector3 n = Vector3.Cross(b - a, c - a).normalized;
                float slope = Mathf.Acos(Mathf.Clamp(Mathf.Abs(n.y), 0f, 1f)) * Mathf.Rad2Deg;
                Vector3 mid = (a + b + c) / 3f;
                int band = DomeSlopeBands.Index(slope, mid.x, mid.z, seed, cfg.WorldVersion);
                bands[band].Add(tris[i]); bands[band].Add(tris[i + 1]); bands[band].Add(tris[i + 2]);
            }
            mesh.subMeshCount = 5;
            for (int b = 0; b < 5; b++) mesh.SetTriangles(bands[b], b, false);
        }
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
