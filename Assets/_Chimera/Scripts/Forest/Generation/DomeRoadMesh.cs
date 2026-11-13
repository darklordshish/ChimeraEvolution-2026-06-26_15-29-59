using System.Collections.Generic;
using UnityEngine;

/// <summary>S4 P6: лента дороги по земле, 4 м (spur 3 м), подъём 0,10 м; без коллайдера.</summary>
public static class DomeRoadMesh
{
    public static Mesh Build(ForestRoadNetwork net, System.Func<float, float, float> surface)
    {
        var mesh = new Mesh { name = "ForestRoads" };
        if (net == null || net.roads == null || surface == null) return mesh;
        const int columns = 3;
        var verts = new List<Vector3>();
        var tris = new List<int>();
        foreach (var road in net.roads)
        {
            if (road.points == null || road.points.Count < 2) continue;
            float half = road.width * 0.5f;
            for (int i = 1; i < road.points.Count; i++)
            {
                Vector2 a = road.points[i - 1], b = road.points[i];
                Vector2 dir = (b - a);
                if (dir.sqrMagnitude < 1e-4f) continue;
                dir.Normalize();
                Vector2 side = new Vector2(-dir.y, dir.x);
                int baseIndex = verts.Count;
                for (int r = 0; r < 2; r++)
                {
                    Vector2 p = r == 0 ? a : b;
                    for (int c = 0; c < columns; c++)
                    {
                        float t = -1f + 2f * c / (columns - 1f);
                        float x = p.x + side.x * half * t;
                        float z = p.y + side.y * half * t;
                        verts.Add(new Vector3(x, surface(x, z) + 0.10f, z));
                    }
                }
                for (int c = 0; c < columns - 1; c++)
                {
                    int i0 = baseIndex + c;
                    int i1 = i0 + 1;
                    int i2 = baseIndex + columns + c;
                    int i3 = i2 + 1;
                    tris.Add(i0); tris.Add(i1); tris.Add(i2);
                    tris.Add(i2); tris.Add(i1); tris.Add(i3);
                }
            }
        }
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
