using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Меши воды павильона — один источник для превью и рантайма.
/// Озеро — капированный диск; река — лента, посаженная на вырезанное поле
/// (+0.25 м), поэтому вода непрерывна и повторяет русло.
/// Лес, срез A.
/// </summary>
public static class DomeWaterMesh
{
    public static Mesh LakeDisc(DomeHydro.State s)
    {
        if (s == null) throw new System.ArgumentNullException(nameof(s));
        const int segs = 48;
        var verts = new Vector3[segs * 3];
        var center = new Vector3(s.lakeCenter.x, s.lakeLevel, s.lakeCenter.y);
        float r = s.lakeRadius * 1.02f;
        for (int i = 0; i < segs; i++)
        {
            float a0 = i / (float)segs * Mathf.PI * 2f;
            float a1 = (i + 1) / (float)segs * Mathf.PI * 2f;
            verts[i * 3] = center;
            verts[i * 3 + 1] = center + new Vector3(Mathf.Cos(a1) * r, 0f, Mathf.Sin(a1) * r);
            verts[i * 3 + 2] = center + new Vector3(Mathf.Cos(a0) * r, 0f, Mathf.Sin(a0) * r);
        }
        return FacetMesh("LakeDisc", verts);
    }

    /// <summary>
    /// Лента реки: сегменты дробятся ×4 с посадкой каждой точки на вырезанное поле
    /// (+0.25), поэтому вода непрерывна и повторяет русло, а не режет бугры напрямик.
    /// </summary>
    public static Mesh RiverRibbon(DomeGenConfigSO cfg, DomeHydro.State s)
    {
        if (cfg == null) throw new System.ArgumentNullException(nameof(cfg));
        if (s == null) throw new System.ArgumentNullException(nameof(s));
        var pts = s.riverPts;
        const int sub = 4;
        var verts = new List<Vector3>((pts.Count - 1) * sub * 6);
        float w = s.riverHalfWidth;
        for (int i = 0; i < pts.Count - 1; i++)
        {
            Vector2 d = (pts[i + 1] - pts[i]).normalized;
            Vector2 p = new Vector2(-d.y, d.x) * w;
            Vector3 prevL = WaterEdge(cfg, s, pts[i].x - p.x, pts[i].y - p.y);
            Vector3 prevR = WaterEdge(cfg, s, pts[i].x + p.x, pts[i].y + p.y);
            for (int k = 1; k <= sub; k++)
            {
                float t = k / (float)sub;
                float px = Mathf.Lerp(pts[i].x, pts[i + 1].x, t);
                float pz = Mathf.Lerp(pts[i].y, pts[i + 1].y, t);
                Vector2 dd = (pts[i + 1] - pts[i]).normalized;
                Vector2 pp = new Vector2(-dd.y, dd.x) * w;
                Vector3 l = WaterEdge(cfg, s, px - pp.x, pz - pp.y);
                Vector3 r = WaterEdge(cfg, s, px + pp.x, pz + pp.y);
                verts.Add(prevL); verts.Add(prevR); verts.Add(r);
                verts.Add(prevL); verts.Add(r); verts.Add(l);
                prevL = l; prevR = r;
            }
        }
        return FacetMesh("RiverRibbon", verts.ToArray());
    }

    static Vector3 WaterEdge(DomeGenConfigSO cfg, DomeHydro.State s, float x, float z)
    {
        return new Vector3(x, DomeHydro.SampleHydro(cfg, s, x, z) + 0.25f, z);
    }

    static Mesh FacetMesh(string name, Vector3[] verts)
    {
        var tris = new int[verts.Length];
        for (int i = 0; i < tris.Length; i++) tris[i] = i;
        var mesh = new Mesh { name = name };
        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
