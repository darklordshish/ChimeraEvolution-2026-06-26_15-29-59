using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Видимая крыша холма над станцией: сетка по <see cref="DomeHill.State"/>,
/// с вырезом под северную расщелину. Это не второй пол — <see cref="DomeWorld.SampleGround"/>
/// остаётся единственным источником для чанков/NavMesh; меш закрывает плоскую крышу.
/// </summary>
public static class DomeHillMesh
{
    public static Mesh Build(DomeHill.State s, Func<float, float, float> ground,
        int res = 32, float holeRadius = 6f)
    {
        if (!s.valid || ground == null) return null;
        res = Mathf.Clamp(res, 8, 64);
        float extent = s.radius + s.blend;
        float step = 2f * extent / res;
        var verts = new List<Vector3>(res * res * 6);
        var tris = new List<int>(res * res * 6);
        for (int iz = 0; iz < res; iz++)
            for (int ix = 0; ix < res; ix++)
            {
                float x0 = s.center.x - extent + ix * step;
                float z0 = s.center.y - extent + iz * step;
                float x1 = x0 + step, z1 = z0 + step;
                if (InHole(s, x0, z0, holeRadius) || InHole(s, x1, z0, holeRadius) ||
                    InHole(s, x0, z1, holeRadius) || InHole(s, x1, z1, holeRadius))
                    continue;
                int b = verts.Count;
                verts.Add(P(ground, s, x0, z0));
                verts.Add(P(ground, s, x1, z0));
                verts.Add(P(ground, s, x0, z1));
                verts.Add(P(ground, s, x1, z1));
                tris.Add(b); tris.Add(b + 2); tris.Add(b + 1);
                tris.Add(b + 1); tris.Add(b + 2); tris.Add(b + 3);
            }
        if (verts.Count == 0) return null;
        var mesh = new Mesh { name = "StationHill" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static Vector3 P(Func<float, float, float> ground, DomeHill.State s, float x, float z)
    {
        return new Vector3(x, ground(x, z) + DomeHill.Apply(0f, s, x, z) + 0.15f, z);
    }

    static bool InHole(DomeHill.State s, float x, float z, float r)
    {
        if (s.notchRadius <= 0f) return false;
        float dx = x - s.notch.x, dz = z - s.notch.y;
        return dx * dx + dz * dz < r * r;
    }
}
