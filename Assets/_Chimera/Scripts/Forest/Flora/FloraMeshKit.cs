using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Процедурный кит флоры гранями (FBX нет): ствол-брусок, крона из икосаэдров,
/// плита-бокс, шип-конус, куст-гроздь, трава — кросс-квады. Неиндексированные
/// треугольники + плоские нормали (тот же язык, что поле и ригблоки).
/// Порядок обхода — наружу: нормаль Unity = cross(B−A, C−A) (эмпирика TerrainApplier),
/// сторожит тест outward-нормалей. Лес, слайс s8.
/// </summary>
public static class FloraMeshKit
{
    static void Tri(List<Vector3> v, List<Vector3> n, List<int> t, Vector3 a, Vector3 b, Vector3 c)
    {
        int i = v.Count;
        v.Add(a); v.Add(b); v.Add(c);
        Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
        n.Add(normal); n.Add(normal); n.Add(normal);
        t.Add(i); t.Add(i + 1); t.Add(i + 2);
    }

    static Mesh Bake(List<Vector3> v, List<Vector3> n, List<int> t, string name)
    {
        var mesh = new Mesh { name = name };
        mesh.SetVertices(v);
        mesh.SetNormals(n);
        mesh.SetTriangles(t, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>Сужающийся брусок-ствол: низ r0, верх r1, высота h. ~20 тр.</summary>
    public static Mesh Trunk(float r0, float r1, float h)
    {
        var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
        Vector3[] lo = { new Vector3(-r0, 0, -r0), new Vector3(r0, 0, -r0), new Vector3(r0, 0, r0), new Vector3(-r0, 0, r0) };
        Vector3[] hi = { new Vector3(-r1, h, -r1), new Vector3(r1, h, -r1), new Vector3(r1, h, r1), new Vector3(-r1, h, r1) };
        for (int k = 0; k < 4; k++)
        {
            int n2 = (k + 1) % 4;
            Tri(v, n, t, lo[k], hi[n2], lo[n2]);
            Tri(v, n, t, lo[k], hi[k], hi[n2]);
        }
        Tri(v, n, t, hi[0], hi[2], hi[1]);
        Tri(v, n, t, hi[0], hi[3], hi[2]);
        return Bake(v, n, t, "Trunk");
    }

    // Вершины икосаэдра (t — золотое сечение), грани ниже — стандартным списком.
    static Vector3[] IcoVerts(float r)
    {
        float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
        return new[]
        {
            new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
            new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
            new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1)
        };
    }

    static readonly int[] IcoFaces =
    {
        0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
        1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
        3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
        4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
    };

    static void Ico(List<Vector3> v, List<Vector3> n, List<int> t, float r, Vector3 center, float squashY)
    {
        var p = IcoVerts(1f);
        float norm = Mathf.Sqrt(1f + Mathf.Pow((1f + Mathf.Sqrt(5f)) * 0.5f, 2f));
        for (int f = 0; f < IcoFaces.Length; f += 3)
        {
            Vector3 a = Shape(p[IcoFaces[f]], r, center, squashY, norm);
            Vector3 b = Shape(p[IcoFaces[f + 1]], r, center, squashY, norm);
            Vector3 c = Shape(p[IcoFaces[f + 2]], r, center, squashY, norm);
            Tri(v, n, t, a, b, c);
        }
    }

    static Vector3 Shape(Vector3 p, float r, Vector3 center, float squashY, float norm)
    {
        return new Vector3(center.x + p.x / norm * r, center.y + p.y / norm * r * squashY, center.z + p.z / norm * r);
    }

    /// <summary>Одиночный икосаэдр (примитив гроздей; outward проверяется тестом на нём,
    /// а не на кластере — внутренние стороны грозди смотрят друг на друга законно).</summary>
    public static Mesh Icosahedron(float r)
    {
        var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
        Ico(v, n, t, r, Vector3.zero, 1f);
        return Bake(v, n, t, "Icosahedron");
    }

    /// <summary>Крона: 3 сплюснутых икосаэдра гроздью. 60 тр.</summary>
    public static Mesh Crown(float r)
    {
        var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
        Ico(v, n, t, r, new Vector3(0, r * 1.2f, 0), 0.7f);
        Ico(v, n, t, r * 0.7f, new Vector3(r * 0.5f, r * 0.6f, 0), 0.7f);
        Ico(v, n, t, r * 0.7f, new Vector3(-r * 0.5f, r * 0.6f, 0), 0.7f);
        return Bake(v, n, t, "Crown");
    }

    /// <summary>Крона ясеня-исполина: 7 широких сплюснутых икосаэдров (герой s10g).
    /// Гроздь — outward только объёмом (как Crown). 140 тр.</summary>
    public static Mesh AshCrown(float r)
    {
        var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
        Ico(v, n, t, r, new Vector3(0, r * 0.9f, 0), 0.55f);
        for (int k = 0; k < 6; k++)
        {
            float a = k / 6f * Mathf.PI * 2f;
            Ico(v, n, t, r * 0.55f,
                new Vector3(Mathf.Cos(a) * r * 0.85f, r * 0.45f, Mathf.Sin(a) * r * 0.85f), 0.55f);
        }
        return Bake(v, n, t, "AshCrown");
    }

    /// <summary>Плита: бокс sx·sy·sz. 12 тр.</summary>
    public static Mesh Slab(float sx, float sy, float sz)
    {
        var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
        Vector3[] c =
        {
            new Vector3(-sx, 0, -sz), new Vector3(sx, 0, -sz), new Vector3(sx, 0, sz), new Vector3(-sx, 0, sz),
            new Vector3(-sx, sy, -sz), new Vector3(sx, sy, -sz), new Vector3(sx, sy, sz), new Vector3(-sx, sy, sz)
        };
        Tri(v, n, t, c[4], c[6], c[5]); Tri(v, n, t, c[4], c[7], c[6]); // верх
        Tri(v, n, t, c[0], c[1], c[2]); Tri(v, n, t, c[0], c[2], c[3]); // низ
        Tri(v, n, t, c[0], c[5], c[1]); Tri(v, n, t, c[0], c[4], c[5]); // бок -z
        Tri(v, n, t, c[2], c[7], c[3]); Tri(v, n, t, c[2], c[6], c[7]); // бок +z
        Tri(v, n, t, c[1], c[6], c[2]); Tri(v, n, t, c[1], c[5], c[6]); // бок +x
        Tri(v, n, t, c[3], c[4], c[0]); Tri(v, n, t, c[3], c[7], c[4]); // бок -x
        return Bake(v, n, t, "Slab");
    }

    /// <summary>Шип: восьмигранный конус. 16 тр.</summary>
    public static Mesh Spike(float r, float h)
    {
        var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
        Vector3 tip = new Vector3(0, h, 0);
        Vector3[] ring = new Vector3[8];
        for (int k = 0; k < 8; k++)
        {
            float a = k / 8f * Mathf.PI * 2f;
            ring[k] = new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
        }
        for (int k = 0; k < 8; k++) Tri(v, n, t, tip, ring[(k + 1) % 8], ring[k]);
        for (int k = 1; k < 7; k++) Tri(v, n, t, ring[0], ring[k], ring[k + 1]);
        return Bake(v, n, t, "Spike");
    }

    /// <summary>Куст: 3 мелких икосаэдра гроздью у земли. 60 тр.</summary>
    public static Mesh Bush(float r)
    {
        var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
        Ico(v, n, t, r, new Vector3(0, r * 0.7f, 0), 0.8f);
        Ico(v, n, t, r * 0.7f, new Vector3(r * 0.7f, r * 0.4f, r * 0.3f), 0.8f);
        Ico(v, n, t, r * 0.7f, new Vector3(-r * 0.6f, r * 0.4f, -r * 0.4f), 0.8f);
        return Bake(v, n, t, "Bush");
    }

    static void ConeTier(List<Vector3> v, List<Vector3> n, List<int> t, float y0, float h, float r)
    {
        Vector3 tip = new Vector3(0, y0 + h, 0);
        Vector3[] ring = new Vector3[8];
        for (int k = 0; k < 8; k++)
        {
            float a = k / 8f * Mathf.PI * 2f;
            ring[k] = new Vector3(Mathf.Cos(a) * r, y0, Mathf.Sin(a) * r);
        }
        for (int k = 0; k < 8; k++) Tri(v, n, t, tip, ring[(k + 1) % 8], ring[k]);
    }

    /// <summary>Ель: 3 яруса восьмигранных конусов (ствол — отдельно Trunk).
    /// Низ открыт (прячет верхний ярус), бока — наружу (тест). 24 тр.</summary>
    public static Mesh SpruceCrown(float r, float h)
    {
        var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
        ConeTier(v, n, t, h * 0.30f, h * 0.34f, r);
        ConeTier(v, n, t, h * 0.55f, h * 0.30f, r * 0.72f);
        ConeTier(v, n, t, h * 0.76f, h * 0.26f, r * 0.45f);
        return Bake(v, n, t, "SpruceCrown");
    }

    static void FrondQuad(List<Vector3> v, List<Vector3> n, List<int> t,
        Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        Tri(v, n, t, a, b, c);
        Tri(v, n, t, a, c, d);
    }

    /// <summary>Папоротник: 6 поникающих вай по 2 квада. Односторонний (лист двусторонний);
    /// outward не меряем (как грозди), только объём. 24 тр.</summary>
    public static Mesh Fern(float len, float h, float w)
    {
        var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
        for (int k = 0; k < 6; k++)
        {
            float a = (k + 0.5f) / 6f * Mathf.PI * 2f;
            Vector3 dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            Vector3 perp = new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a)) * w;
            Vector3 b0 = new Vector3(0, 0.05f, 0);
            Vector3 m0 = dir * (len * 0.55f) + new Vector3(0, h * 0.75f, 0);
            Vector3 t0 = dir * len + new Vector3(0, h * 0.3f, 0);
            FrondQuad(v, n, t, b0 - perp, b0 + perp, m0 + perp, m0 - perp);
            FrondQuad(v, n, t, m0 - perp * 0.6f, m0 + perp * 0.6f, t0 + perp * 0.3f, t0 - perp * 0.3f);
        }
        return Bake(v, n, t, "Fern");
    }

    /// <summary>Травинка: 2 кросс-квада (двусторонний материал). 4 тр.</summary>
    public static Mesh GrassBlade(float w, float h)
    {
        var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
        Tri(v, n, t, new Vector3(-w, 0, 0), new Vector3(w, 0, 0), new Vector3(w, h, 0));
        Tri(v, n, t, new Vector3(-w, 0, 0), new Vector3(w, h, 0), new Vector3(-w, h, 0));
        Tri(v, n, t, new Vector3(0, 0, -w), new Vector3(0, 0, w), new Vector3(0, h, w));
        Tri(v, n, t, new Vector3(0, 0, -w), new Vector3(0, h, w), new Vector3(0, h, -w));
        return Bake(v, n, t, "GrassBlade");
    }
}
