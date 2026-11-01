using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Вытоптанные дорожки по коридорам B-lite: тонкая лента над землёй,
/// чтобы структура читалась без роста леса. Не коллайдер и не NavMesh.
/// Лента режется радиусом арены (в центр арены дорожка не заходит), пятном
/// станции и входом; поперёк профиля она повторяет землю, а не висит плоскостью.
/// </summary>
public static class DomeSpacePaths
{
    /// <summary>Сколько колонок поперёк ленты: одна пара рёбер «клюёт» на склоне.</summary>
    const int Columns = 5;
    /// <summary>Малый подъём: лента садится на нарисованный чанк, а не на аналитику.</summary>
    const float Lift = 0.12f;

    public static Mesh Build(IReadOnlyList<ForestSpace.Corridor> corridors,
        Func<float, float, float> ground, float width = 6f,
        IReadOnlyList<ForestSpace.Clearing> clearings = null,
        Func<float, float, bool> exclude = null,
        Func<float, float, float> surface = null)
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
            int steps = Mathf.Max(2, Mathf.CeilToInt(len / 2f));
            Vector2 dir = ab / len;
            Vector2 side = new Vector2(-dir.y, dir.x);
            for (int i = 0; i < steps; i++)
            {
                Vector2 p0 = Vector2.Lerp(cor.a, cor.b, i / (float)steps);
                Vector2 p1 = Vector2.Lerp(cor.a, cor.b, (i + 1) / (float)steps);
                // Ряд режется, если попал в арену, пятно станции или у входа:
                // так дорожка не идёт через центр боя и не ложится на бункер.
                if (!RowActive(p0, clearings, exclude) || !RowActive(p1, clearings, exclude))
                    continue;
                int baseIndex = verts.Count;
                for (int r = 0; r < 2; r++)
                {
                    Vector2 p = r == 0 ? p0 : p1;
                    for (int col = 0; col < Columns; col++)
                    {
                        float s = -1f + 2f * col / (Columns - 1);
                        float x = p.x + side.x * half * s;
                        float z = p.y + side.y * half * s;
                        // surface — высота нарисованного чанка (без «укусов»); без неё — аналитика.
                        float y = surface != null ? surface(x, z) : ground(x, z);
                        verts.Add(new Vector3(x, y + Lift, z));
                    }
                }
                for (int col = 0; col < Columns - 1; col++)
                {
                    int a = baseIndex + col;
                    int b = a + 1;
                    int c2 = baseIndex + Columns + col;
                    int d = c2 + 1;
                    tris.Add(a); tris.Add(b); tris.Add(c2);
                    tris.Add(c2); tris.Add(b); tris.Add(d);
                }
            }
        }
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static bool RowActive(Vector2 p, IReadOnlyList<ForestSpace.Clearing> clearings,
        Func<float, float, bool> exclude)
    {
        if (ForestSpace.Contains(clearings, p.x, p.y, 0f)) return false;
        if (exclude != null && exclude(p.x, p.y)) return false;
        return true;
    }
}
