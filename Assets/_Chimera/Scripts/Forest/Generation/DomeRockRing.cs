using System;
using UnityEngine;

/// <summary>
/// Скальное кольцо — граница мира павильона (s10a): меш-лента Rin=R−80 … Rout=R+140,
/// высота = подъём от высоты поля на внутренней кромке к пику с ridged-модуляцией
/// по углу + спад наружу (там уже свод купола, не глаз). Фасетка тем же языком, что
/// чанки, но обход ОБРАТНЫЙ ((p00,p10,p11)+(p00,p11,p01)): при параметризации
/// (угол → радиус) прямой порядок смотрит вниз (проверено выводом нормалей).
/// </summary>
public static class DomeRockRing
{
    const long RingSalt = 0x4A5E6D7CL;

    public static float InnerRadius(DomeGenConfigSO cfg)
    {
        return cfg.mapDiameter * 0.5f - 80f;
    }

    public static float OuterRadius(DomeGenConfigSO cfg)
    {
        return cfg.mapDiameter * 0.5f + 140f;
    }

    public static float RidgeAt(DomeGenConfigSO cfg, float angle)
    {
        float n = ValueNoise2D.Fbm(cfg.seed + RingSalt,
            Mathf.Cos(angle) * 3f + 7f, Mathf.Sin(angle) * 3f - 7f, 4);
        float r = 1f - Mathf.Abs(n);
        return r * r;
    }

    public static float HeightAt(DomeGenConfigSO cfg, float angle, float t)
    {
        float rin = InnerRadius(cfg);
        float hIn = DomeHeightField.SampleHeight(cfg, Mathf.Cos(angle) * rin, Mathf.Sin(angle) * rin);
        float peak = cfg.ringPeakHeight * (0.65f + 0.35f * RidgeAt(cfg, angle));
        float rise = Hermite(Mathf.Clamp01(t / 0.5f));
        float h = Mathf.Lerp(hIn, peak, rise);
        float fall = Hermite(Mathf.Clamp01((t - 0.5f) / 0.5f));
        return Mathf.Lerp(h, cfg.ringPeakHeight * 0.15f, fall);
    }

    /// <summary>
    /// Ручной Hermite вместо Mathf.SmoothStep: в этом окружении SmoothStep ведёт себя
    /// не по документации (замер 19.09: SmoothStep(0, 0.5, 0.5) = 0.25, а не 1),
    /// поэтому кривые подъёма/спада считаем явно и тестируемо.
    /// </summary>
    static float Hermite(float u)
    {
        return u * u * (3f - 2f * u);
    }

    public static Mesh BuildRingMesh(DomeGenConfigSO cfg, int around, int radial)
    {
        if (cfg == null) throw new ArgumentNullException(nameof(cfg));
        around = Mathf.Clamp(around, 16, 512);
        radial = Mathf.Clamp(radial, 2, 16);
        float rin = InnerRadius(cfg);
        float rout = OuterRadius(cfg);
        var verts = new Vector3[around * radial * 6];
        int v = 0;
        for (int j = 0; j < radial; j++)
            for (int i = 0; i < around; i++)
            {
                float a0 = (i / (float)around) * Mathf.PI * 2f;
                float a1 = ((i + 1) / (float)around) * Mathf.PI * 2f;
                float t0 = j / (float)radial;
                float t1 = (j + 1) / (float)radial;
                Vector3 p00 = Point(cfg, rin, rout, a0, t0);
                Vector3 p10 = Point(cfg, rin, rout, a1, t0);
                Vector3 p01 = Point(cfg, rin, rout, a0, t1);
                Vector3 p11 = Point(cfg, rin, rout, a1, t1);
                verts[v++] = p00;
                verts[v++] = p10;
                verts[v++] = p11;
                verts[v++] = p00;
                verts[v++] = p11;
                verts[v++] = p01;
            }
        var tris = new int[verts.Length];
        for (int k = 0; k < tris.Length; k++) tris[k] = k;
        var mesh = new Mesh { name = "DomeRockRing" };
        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static Vector3 Point(DomeGenConfigSO cfg, float rin, float rout, float angle, float t)
    {
        float r = Mathf.Lerp(rin, rout, t);
        return new Vector3(Mathf.Cos(angle) * r, HeightAt(cfg, angle, t), Mathf.Sin(angle) * r);
    }
}
