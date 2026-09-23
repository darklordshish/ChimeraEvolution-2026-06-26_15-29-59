using System;
using UnityEngine;

/// <summary>
/// Свод купола (s10a): полусфера радиуса Rv с центром под землёй (апекс Rv/2 над полом),
/// кромка зарыта за скальным кольцом. Обход — КАК У ЧАНКОВ (тест поймал: обратный смотрит
/// наружу): грани смотрят внутрь. Один меш, один Unlit-шейдер неба, туману не подвержен.
/// </summary>
public static class DomeVault
{
    public static float ApexFactor = 0.5f;
    public static float RimTheta = Mathf.PI * 0.5f + 0.15f;

    public static float CenterY(float vaultRadius)
    {
        return -(vaultRadius - vaultRadius * ApexFactor);
    }

    public static Mesh BuildVaultMesh(float vaultRadius, int lonSegs, int latSegs)
    {
        if (vaultRadius <= 0f) throw new ArgumentOutOfRangeException(nameof(vaultRadius));
        lonSegs = Mathf.Clamp(lonSegs, 16, 256);
        latSegs = Mathf.Clamp(latSegs, 4, 64);
        float cy = CenterY(vaultRadius);
        var verts = new Vector3[lonSegs * latSegs * 6];
        int v = 0;
        for (int j = 0; j < latSegs; j++)
            for (int i = 0; i < lonSegs; i++)
            {
                float a0 = i / (float)lonSegs * Mathf.PI * 2f;
                float a1 = (i + 1) / (float)lonSegs * Mathf.PI * 2f;
                float t0 = j / (float)latSegs * RimTheta;
                float t1 = (j + 1) / (float)latSegs * RimTheta;
                Vector3 p00 = Point(vaultRadius, cy, a0, t0);
                Vector3 p10 = Point(vaultRadius, cy, a1, t0);
                Vector3 p01 = Point(vaultRadius, cy, a0, t1);
                Vector3 p11 = Point(vaultRadius, cy, a1, t1);
                verts[v++] = p00;
                verts[v++] = p11;
                verts[v++] = p10;
                verts[v++] = p00;
                verts[v++] = p01;
                verts[v++] = p11;
            }
        var tris = new int[verts.Length];
        for (int k = 0; k < tris.Length; k++) tris[k] = k;
        var mesh = new Mesh { name = "DomeVault" };
        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static Vector3 Point(float radius, float cy, float angle, float theta)
    {
        float r = radius * Mathf.Sin(theta);
        return new Vector3(Mathf.Cos(angle) * r, cy + radius * Mathf.Cos(theta), Mathf.Sin(angle) * r);
    }
}
