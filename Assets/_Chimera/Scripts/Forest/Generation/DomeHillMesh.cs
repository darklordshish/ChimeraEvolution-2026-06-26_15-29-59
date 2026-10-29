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
        int res = 32, float holeRadius = 6f, float halfX = 0f, float halfZ = 0f,
        Func<float, float, bool> cut = null, Func<float, float, float> cutGround = null,
        Func<float, float, float> cutWeight = null)
    {
        if (!s.valid || ground == null) return null;
        res = Mathf.Clamp(res, 8, 64);
        float ex = halfX > 0f ? halfX : s.radius + s.blend;
        float ez = halfZ > 0f ? halfZ : s.radius + s.blend;
        float stepX = 2f * ex / res;
        float stepZ = 2f * ez / res;
        var verts = new List<Vector3>(res * res * 6);
        var tris = new List<int>(res * res * 6);
        for (int iz = 0; iz < res; iz++)
            for (int ix = 0; ix < res; ix++)
            {
                float x0 = s.center.x - ex + ix * stepX;
                float z0 = s.center.y - ez + iz * stepZ;
                float x1 = x0 + stepX, z1 = z0 + stepZ;
                if (InHole(s, x0, z0, holeRadius) || InHole(s, x1, z0, holeRadius) ||
                    InHole(s, x0, z1, holeRadius) || InHole(s, x1, z1, holeRadius))
                    continue;
                int b = verts.Count;
                verts.Add(P(ground, s, x0, z0, cut, cutGround, cutWeight));
                verts.Add(P(ground, s, x1, z0, cut, cutGround, cutWeight));
                verts.Add(P(ground, s, x0, z1, cut, cutGround, cutWeight));
                verts.Add(P(ground, s, x1, z1, cut, cutGround, cutWeight));
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

    static Vector3 P(Func<float, float, float> ground, DomeHill.State s, float x, float z,
        Func<float, float, bool> cut, Func<float, float, float> cutGround,
        Func<float, float, float> cutWeight)
    {
        float hillY = ground(x, z) + DomeHill.Apply(0f, s, x, z) + 0.15f;
        if (cutWeight != null && cutGround != null)
        {
            float w = Mathf.Clamp01(cutWeight(x, z));
            if (w > 0f)
                return new Vector3(x, Mathf.Lerp(hillY, cutGround(x, z) + 0.15f, w), z);
        }
        if (cut != null && cutGround != null && cut(x, z))
            return new Vector3(x, cutGround(x, z) + 0.15f, z);
        return new Vector3(x, hillY, z);
    }

    static bool InHole(DomeHill.State s, float x, float z, float r)
    {
        if (s.notchRadius <= 0f) return false;
        float dx = x - s.notch.x, dz = z - s.notch.y;
        return dx * dx + dz * dz < r * r;
    }
}
