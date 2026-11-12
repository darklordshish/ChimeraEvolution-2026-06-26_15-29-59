using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Авторский меш каньона: дно, борта, полка, торцы и юбка.
/// Terrain вырезается по тому же footprint; юбка закрывает шов.
/// </summary>
public static class DomeCanyonAuthorMesh
{
    public const float Reach = 2.5f; // rim beyond halfWidth

    public static void Build(Transform parent, DomeCanyon.State s,
        System.Func<float, float, float> ground, Material rock, Material water, Material foam,
        List<Mesh> track = null)
    {
        if (parent == null || ground == null || rock == null) return;
        if (!s.valid || s.axis == null || s.axis.Count < 2) return;

        var rows = new List<Vector2>();
        for (int i = 1; i < s.axis.Count; i++)
        {
            Vector2 a = s.axis[i - 1], b = s.axis[i];
            int n = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / 2f));
            for (int k = i == 1 ? 0 : 1; k <= n; k++)
                rows.Add(Vector2.Lerp(a, b, k / (float)n));
        }
        if (rows.Count < 2) return;

        float reach = s.halfWidth + Reach;
        var verts = new List<Vector3>(rows.Count * 7);
        for (int i = 0; i < rows.Count; i++)
        {
            Vector2 p = rows[i];
            Vector2 fwd = i < rows.Count - 1 ? rows[i + 1] - p : p - rows[i - 1];
            if (fwd.sqrMagnitude < 1e-6f) fwd = Vector2.up;
            Vector2 perp = new Vector2(-fwd.y, fwd.x).normalized;
            Vector2 L = p + perp * reach, R = p - perp * reach;
            float rim = Mathf.Min(ground(L.x, L.y), ground(R.x, R.y));
            float depth = Mathf.Lerp(s.depthMouth, s.depthHead, i / (float)(rows.Count - 1));
            float floor = rim - depth;
            for (int c = 0; c < 7; c++)
            {
                float t = c / 6f;
                float lat = Mathf.Lerp(-reach, reach, t);
                Vector2 q = p + perp * lat;
                float y;
                if (c == 0 || c == 6) y = ground(q.x, q.y);
                else if (c == 1 || c == 5) y = floor + depth * 0.55f;
                else if (c == 2 || c == 4) y = floor + depth * 0.22f;
                else y = floor;
                verts.Add(new Vector3(q.x, y, q.y));
            }
        }
        var tris = new List<int>((rows.Count - 1) * 6 * 6);
        for (int i = 0; i < rows.Count - 1; i++)
            for (int c = 0; c < 6; c++)
            {
                int a = i * 7 + c, b = a + 1, d = a + 7, e = d + 1;
                tris.Add(a); tris.Add(b); tris.Add(d);
                tris.Add(d); tris.Add(b); tris.Add(e);
            }
        // Торцы.
        for (int end = 0; end < 2; end++)
        {
            int baseRow = end == 0 ? 0 : (rows.Count - 1) * 7;
            for (int c = 0; c < 6; c++)
            {
                int a = baseRow + c, b = a + 1;
                int a2 = baseRow + (end == 0 ? 0 : 0) + c;
                tris.Add(a2); tris.Add(b); tris.Add(a2 + 7 > verts.Count - 1 ? a : a2 + 7);
            }
        }
        var mesh = new Mesh { name = "CanyonAuthor" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        Part(parent, "CanyonAuthor", mesh, rock, track);

        // Юбка: вертикальная лента по внешнему контуру вниз, закрывает вырез.
        for (int side = -1; side <= 1; side += 2)
        {
            var sv = new List<Vector3>(rows.Count * 2);
            for (int i = 0; i < rows.Count; i++)
            {
                Vector2 p = rows[i];
                Vector2 fwd = i < rows.Count - 1 ? rows[i + 1] - p : p - rows[i - 1];
                if (fwd.sqrMagnitude < 1e-6f) fwd = Vector2.up;
                Vector2 perp = new Vector2(-fwd.y, fwd.x).normalized * side;
                Vector2 q = p + perp * reach;
                float top = ground(q.x, q.y);
                sv.Add(new Vector3(q.x, top, q.y));
                sv.Add(new Vector3(q.x, top - 3f, q.y));
            }
            var st = new List<int>((rows.Count - 1) * 6);
            for (int i = 0; i < rows.Count - 1; i++)
            {
                int a = i * 2, b = a + 1, c = a + 2, d = c + 1;
                st.Add(a); st.Add(b); st.Add(c);
                st.Add(c); st.Add(b); st.Add(d);
            }
            var sm = new Mesh { name = side < 0 ? "CanyonSkirtW" : "CanyonSkirtE" };
            sm.SetVertices(sv);
            sm.SetTriangles(st, 0);
            sm.RecalculateNormals();
            Part(parent, sm.name, sm, rock, track);
        }

        if (s.policy != CanyonPolicy.Wet || water == null) return;
        BuildWater(parent, s, ground, water, foam, track);
    }

    static void BuildWater(Transform parent, DomeCanyon.State s,
        System.Func<float, float, float> ground, Material water, Material foam, List<Mesh> track)
    {
        float top = DomeWaterMesh.FallTopLevel(s, ground) + 0.12f;
        float bottom = DomeWaterMesh.PoolLevel(s, 0f, ground) + 0.20f;
        Vector2 d2 = s.mouth - s.lip;
        if (d2.sqrMagnitude < 1e-8f) d2 = new Vector2(0f, -1f);
        d2.Normalize();
        Vector3 down = new Vector3(d2.x, 0f, d2.y);
        Vector3 side = new Vector3(down.z, 0f, -down.x);
        float half = DomeWaterMesh.FallWidth(s) * 0.5f;
        Vector3 topC = new Vector3(s.lip.x, top, s.lip.y);
        Vector3 botC = new Vector3(s.pool.x, bottom, s.pool.y);
        const int rows = 8;
        var v = new List<Vector3>(rows * 24);
        for (int i = 0; i < rows; i++)
        {
            Vector3 c0 = Vector3.Lerp(botC, topC, i / (float)rows);
            Vector3 c1 = Vector3.Lerp(botC, topC, (i + 1) / (float)rows);
            Vector3 f0 = c0 - side * half, f1 = c1 - side * half;
            Vector3 g0 = c0 + side * half, g1 = c1 + side * half;
            Vector3 b0 = f0 + down * 0.55f, b1 = f1 + down * 0.55f;
            Vector3 h0 = g0 + down * 0.55f, h1 = g1 + down * 0.55f;
            AddQuad(v, f0, g0, g1, f1); AddQuad(v, b0, b1, h1, h0);
            AddQuad(v, f0, f1, b1, b0); AddQuad(v, g0, h0, h1, g1);
        }
        var fm = new Mesh { name = "CanyonWater" };
        fm.SetVertices(v); fm.SetTriangles(All(v.Count), 0); fm.RecalculateNormals();
        Part(parent, fm.name, fm, water, track);
        if (foam != null)
        {
            var fv = new List<Vector3>(12);
            Vector3 c = botC + Vector3.up * 0.22f;
            Vector3 x = new Vector3(2f, 0f, 0f), z = new Vector3(0f, 0f, 2f);
            AddQuad(fv, c - x - z, c + x - z, c + x + z, c - x + z);
            AddQuad(fv, c - x - z, c - x + z, c + x + z, c + x - z);
            var fmm = new Mesh { name = "CanyonFoam" };
            fmm.SetVertices(fv); fmm.SetTriangles(All(fv.Count), 0); fmm.RecalculateNormals();
            Part(parent, fmm.name, fmm, foam, track);
        }
    }

    static int[] All(int n) { var a = new int[n]; for (int i = 0; i < n; i++) a[i] = i; return a; }
    static void AddQuad(List<Vector3> v, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    { v.Add(a); v.Add(b); v.Add(c); v.Add(a); v.Add(c); v.Add(d); }
    static void Part(Transform parent, string name, Mesh mesh, Material mat, List<Mesh> track)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        track?.Add(mesh);
    }
}
