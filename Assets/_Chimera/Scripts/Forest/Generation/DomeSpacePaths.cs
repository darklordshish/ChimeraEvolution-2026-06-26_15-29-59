using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Вытоптанные дорожки по коридорам B-lite: тонкая лента над землёй,
/// чтобы структура читалась без роста леса. Не коллайдер и не NavMesh.
/// </summary>
public static class DomeSpacePaths
{
    public static Mesh Build(IReadOnlyList<ForestSpace.Corridor> corridors,
        Func<float, float, float> ground, float width = 6f)
    {
        var mesh = new Mesh { name = "DomeSpacePaths" };
        if (corridors == null || corridors.Count == 0 || ground == null) return mesh;

        var verts = new List<Vector3>();
        var tris = new List<int>();
        float half = width * 0.5f;
        for (int c = 0; c < corridors.Count; c++)
        {
            var cor = corridors[c];
            Vector2 ab = cor.b - cor.a;
            float len = ab.magnitude;
            if (len < 1f) continue;
            int steps = Mathf.Max(2, Mathf.CeilToInt(len / 4f));
            Vector2 dir = ab / len;
            Vector2 side = new Vector2(-dir.y, dir.x);
            int baseIndex = verts.Count;
            for (int i = 0; i <= steps; i++)
            {
                Vector2 p = Vector2.Lerp(cor.a, cor.b, i / (float)steps);
                for (int s = -1; s <= 1; s += 2)
                {
                    float x = p.x + side.x * half * s;
                    float z = p.y + side.y * half * s;
                    verts.Add(new Vector3(x, ground(x, z) + 0.18f, z));
                }
            }
            for (int i = 0; i < steps; i++)
            {
                int a = baseIndex + i * 2;
                tris.Add(a); tris.Add(a + 1); tris.Add(a + 2);
                tris.Add(a + 2); tris.Add(a + 1); tris.Add(a + 3);
            }
        }
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}