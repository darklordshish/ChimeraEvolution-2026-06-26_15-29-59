using System;
using UnityEngine;

/// <summary>
/// Мох-звёзды свода — ГЕОМЕТРИЯ (s10c, вердикт художника: не emissive-маска, без bloom).
/// Октаэдры на сфере Фибоначчи (только верх), запечены в один меш, 2 ступени яркости
/// вертекс-колором. Ночь/день — uniform _Level материала (ставит риг).
/// </summary>
public static class DomeStars
{
    public static Mesh BuildStarMesh(long seed, int count, Vector3 center, float radius, float starSize)
    {
        if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
        var verts = new Vector3[count * 8 * 3];
        var colors = new Color[verts.Length];
        int v = 0;
        float golden = Mathf.PI * (3f - Mathf.Sqrt(5f));
        for (int i = 0; i < count; i++)
        {
            // Полоса y ∈ [0.055, 1]: верх свода, гарантированная терминация.
            float y = 1f - (i + 0.5f) / count * 0.95f;
            float r = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            float a = golden * i;
            var starCenter = center + new Vector3(Mathf.Cos(a) * r * radius, y * radius, Mathf.Sin(a) * r * radius);
            float bright = SeededHash.ToFloat01(SeededHash.Hash(seed, i, 7)) > 0.5f ? 1f : 0.55f;
            float tint = 0.85f + 0.15f * SeededHash.ToFloat01(SeededHash.Hash(seed, i, 77));
            var col = new Color(bright * tint, bright * tint, bright);
            float s = starSize * (0.7f + 0.6f * SeededHash.ToFloat01(SeededHash.Hash(seed, i, 777)));
            Vector3 up = (starCenter - center).normalized;
            Vector3 t1 = Math.Abs(up.y) > 0.9f ? new Vector3(1f, 0f, 0f) : Vector3.Cross(up, new Vector3(0f, 1f, 0f)).normalized;
            Vector3 t2 = Vector3.Cross(up, t1);
            Vector3 top = starCenter + up * s;
            Vector3 bottom = starCenter - up * s;
            Vector3 e1 = starCenter + t1 * s;
            Vector3 e2 = starCenter + t2 * s;
            Vector3 e3 = starCenter - t1 * s;
            Vector3 e4 = starCenter - t2 * s;
            // 8 граней октаэдра (верхняя пирамида + нижняя).
            Vector3[] tris = { top, e1, e2, top, e2, e3, top, e3, e4, top, e4, e1,
                               bottom, e2, e1, bottom, e3, e2, bottom, e4, e3, bottom, e1, e4 };
            foreach (var p in tris) { verts[v] = p; colors[v] = col; v++; }
        }
        var indices = new int[verts.Length];
        for (int k = 0; k < indices.Length; k++) indices[k] = k;
        var mesh = new Mesh { name = "DomeStars" };
        mesh.vertices = verts;
        mesh.colors = colors;
        mesh.triangles = indices;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
