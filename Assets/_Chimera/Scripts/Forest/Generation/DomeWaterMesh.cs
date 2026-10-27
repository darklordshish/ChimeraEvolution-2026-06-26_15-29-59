using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Меши воды павильона — один источник для превью и рантайма.
/// Озеро — капированный диск; река — лента, посаженная на вырезанное поле
/// (+0.25 м), поэтому вода непрерывна и повторяет русло.
/// <para>
/// Большой павильон (Task 2, 27.10): плёс под водопадом, полотно по стенке и ручей
/// по дну каньона. Все три читают ОДИН пол (<c>SampleGround</c>), а не сырое поле:
/// уровень плёса — <c>max(lakeLevel, дно) + 0.02</c>, ручей — <c>дно + 0.15</c>,
/// верх полотна — <c>дно липа + 0.15</c>. Поэтому вода не висит над рельефом.
/// </para>
/// <para>
/// Вода — только меш и материал: ни коллайдеров, ни физики, ни прозрачности.
/// Статики у класса нет; <see cref="Build"/> кладёт меши в переданный список, чтобы
/// <see cref="DomeWorld.Clear"/> снял их вместе с корнем мира.
/// </para>
/// Лес, срез A + большой павильон.
/// </summary>
public static class DomeWaterMesh
{
    /// <summary>Насколько поверхность плёса выше дна (и нижняя граница — уровень озера).</summary>
    public const float PoolLift = 0.02f;
    /// <summary>Верхний уровень воды над липом (верхний ручей подходит к полотну).</summary>
    public const float FallTopLift = 0.15f;
    /// <summary>Ручей над дном каньона.</summary>
    public const float StreamLift = 0.15f;
    /// <summary>Минимальный зазор ручья над дном, когда профиль выравнивается.</summary>
    public const float StreamMinLift = 0.02f;

    /// <summary>Ширина полотна водопада — рабочая цель спеки 8–12 м.</summary>
    public const float MinFallWidth = 8f;
    public const float MaxFallWidth = 12f;
    /// <summary>Ширина ручья — 3–6 м (ручей, а не второй каньон).</summary>
    public const float MinStreamWidth = 3f;
    public const float MaxStreamWidth = 6f;

    // ---- озеро и река (срез A, поведение не меняется) -----------------------

    public static Mesh LakeDisc(DomeHydro.State s)
    {
        if (s == null) throw new ArgumentNullException(nameof(s));
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
        if (cfg == null) throw new ArgumentNullException(nameof(cfg));
        if (s == null) throw new ArgumentNullException(nameof(s));
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

    // ---- каньон: плёс, полотно, ручей (Task 2, 27.10) ----------------------

    /// <summary>
    /// Уровень плёса. Спека Task 2 называет <c>lakeLevel + 0.02</c>; но каньон Task 1
    /// клампит дно к <c>lakeLevel + 0.45</c>, поэтому буквальный уровень озера утопил бы
    /// диск под дном. Берём максимум: уровень озера — нижняя граница, дно — реальная опора.
    /// </summary>
    public static float PoolLevel(DomeCanyon.State s, float lakeLevel,
        Func<float, float, float> ground)
    {
        if (ground == null || !s.valid) return lakeLevel + PoolLift;
        float bed = ground(s.pool.x, s.pool.y);
        return Mathf.Max(lakeLevel + PoolLift, bed + PoolLift);
    }

    /// <summary>Верх полотна: вода верхнего ручья на липе.</summary>
    public static float FallTopLevel(DomeCanyon.State s, Func<float, float, float> ground)
    {
        if (ground == null || !s.valid) return 0f;
        return ground(s.lip.x, s.lip.y) + FallTopLift;
    }

    /// <summary>Полная ширина полотна: 8–12 м, но не шире каньона с запасом.</summary>
    public static float FallWidth(DomeCanyon.State s)
    {
        return Mathf.Clamp(s.halfWidth * 1.2f, MinFallWidth, MaxFallWidth);
    }

    /// <summary>Полная ширина ручья: 3–6 м.</summary>
    public static float StreamWidth(DomeCanyon.State s)
    {
        return Mathf.Clamp(s.halfWidth * 0.5f, MinStreamWidth, MaxStreamWidth);
    }

    /// <summary>
    /// Плёс: плоский диск на уровне <paramref name="level"/> радиусом <c>poolRadius</c>.
    /// Один уровень по всей поверхности — «озеро одной плоскостью».
    /// </summary>
    public static Mesh PoolDisc(DomeCanyon.State s, float level)
    {
        if (!s.valid || s.poolRadius <= 0f) return EmptyMesh("PoolDisc");
        const int segs = 32;
        var verts = new Vector3[segs * 3];
        var center = new Vector3(s.pool.x, level, s.pool.y);
        float r = s.poolRadius;
        for (int i = 0; i < segs; i++)
        {
            float a0 = i / (float)segs * Mathf.PI * 2f;
            float a1 = (i + 1) / (float)segs * Mathf.PI * 2f;
            verts[i * 3] = center;
            verts[i * 3 + 1] = center + new Vector3(Mathf.Cos(a1) * r, 0f, Mathf.Sin(a1) * r);
            verts[i * 3 + 2] = center + new Vector3(Mathf.Cos(a0) * r, 0f, Mathf.Sin(a0) * r);
        }
        return FacetMesh("PoolDisc", verts);
    }

    /// <summary>
    /// Полотно водопада: лента от верхней кромки на липе до нижней у плёса.
    /// Двойная (видна с обеих сторон), плоские грани, без коллайдера и прозрачности.
    /// </summary>
    public static Mesh FallSheet(DomeCanyon.State s, float topY, float bottomY)
    {
        if (!s.valid || s.axis == null || s.axis.Count < 2) return EmptyMesh("FallSheet");

        Vector2 down2 = s.mouth - s.lip;
        if (down2.sqrMagnitude < 1e-8f) down2 = new Vector2(0f, -1f);
        down2.Normalize();
        Vector3 down = new Vector3(down2.x, 0f, down2.y);
        // side × up = down: нормаль полотна смотрит по течению (на зрителя у плёса).
        Vector3 side = new Vector3(down.z, 0f, -down.x);

        float half = FallWidth(s) * 0.5f;
        // Низ — над верхним краем плёса; верх — на липе. Горизонтальный вынос мал,
        // стенку задаёт рельеф, а не наклон полотна.
        Vector2 bottom2 = s.pool - down2 * (s.poolRadius * 0.85f);
        Vector3 topCenter = new Vector3(s.lip.x, topY, s.lip.y);
        Vector3 bottomCenter = new Vector3(bottom2.x, bottomY, bottom2.y);

        const int rows = 6;
        var verts = new List<Vector3>(rows * 12);
        Vector3 prevL = bottomCenter - side * half;
        Vector3 prevR = bottomCenter + side * half;
        for (int i = 1; i <= rows; i++)
        {
            float t = i / (float)rows;
            Vector3 c = Vector3.Lerp(bottomCenter, topCenter, t);
            Vector3 l = c - side * half;
            Vector3 r = c + side * half;
            AddQuad(verts, prevL, prevR, r, l);      // лицевая сторона
            AddQuad(verts, prevL, l, r, prevR);      // изнанка: полотно видно с обеих сторон
            prevL = l;
            prevR = r;
        }
        return FacetMesh("FallSheet", verts.ToArray());
    }

    /// <summary>
    /// Осевая линия ручья от плёса к устью (вниз по течению). Уровень каждой точки —
    /// <c>дна + 0.15</c>, выровненный так, чтобы не подниматься: ручей монотонно вниз.
    /// Отдельный метод нужен тесту и <see cref="StreamRibbon"/>; аллокаций на кадр нет —
    /// это сборка мира.
    /// </summary>
    public static List<Vector3> StreamProfile(DomeCanyon.State s,
        Func<float, float, float> ground)
    {
        var result = new List<Vector3>();
        if (!s.valid || ground == null || s.axis == null || s.axis.Count < 2) return result;

        Vector2 down2 = s.mouth - s.lip;
        if (down2.sqrMagnitude < 1e-8f) down2 = new Vector2(0f, -1f);
        down2.Normalize();

        // Начинаем у нижней кромки плёса, а не в его центре: иначе ручей ныряет в плёс.
        Vector2 start = s.pool + down2 * (s.poolRadius * 0.9f);
        float startProj = Vector2.Dot(start - s.lip, down2);

        // Ось от липа к устью; берём только точки, которые уже ниже старта.
        var path = DomeCanyon.StreamPath(s);
        var pts = new List<Vector2> { start };
        bool started = false;
        for (int i = 0; i < path.Count; i++)
        {
            float proj = Vector2.Dot(path[i] - s.lip, down2);
            if (!started && proj < startProj - 1e-3f) continue;
            started = true;
            if (Vector2.Distance(pts[pts.Count - 1], path[i]) < 1e-3f) continue;
            pts.Add(path[i]);
        }
        if (pts.Count < 2 || Vector2.Distance(pts[pts.Count - 1], s.mouth) > 1e-3f)
            pts.Add(s.mouth);

        const int sub = 4;
        float prevY = float.PositiveInfinity;
        for (int i = 0; i < pts.Count - 1; i++)
        {
            Vector2 a = pts[i];
            Vector2 b = pts[i + 1];
            if (Vector2.Distance(a, b) < 1e-5f) continue;
            for (int k = i == 0 ? 0 : 1; k <= sub; k++)
            {
                Vector2 p = Vector2.Lerp(a, b, k / (float)sub);
                float g = ground(p.x, p.y);
                float y = g + StreamLift;
                if (!float.IsPositiveInfinity(prevY)) y = Mathf.Min(y, prevY);
                // Вода не должна утонуть в поднявшемся дне: держим минимальный зазор.
                y = Mathf.Max(y, g + StreamMinLift);
                result.Add(new Vector3(p.x, y, p.y));
                prevY = y;
            }
        }
        return result;
    }

    /// <summary>Лента ручья по профилю <see cref="StreamProfile"/>.</summary>
    public static Mesh StreamRibbon(DomeCanyon.State s, Func<float, float, float> ground)
    {
        var profile = StreamProfile(s, ground);
        if (profile.Count < 2) return EmptyMesh("StreamRibbon");

        float half = StreamWidth(s) * 0.5f;
        var verts = new List<Vector3>(profile.Count * 6);
        Vector3 prevL = default;
        Vector3 prevR = default;
        for (int i = 0; i < profile.Count; i++)
        {
            Vector3 p = profile[i];
            Vector3 fwd = i < profile.Count - 1 ? profile[i + 1] - p : p - profile[i - 1];
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-8f) fwd = Vector3.forward;
            fwd.Normalize();
            // side × up = up-нормаль ленты (тот же обход, что у LakeDisc).
            Vector3 side = new Vector3(-fwd.z, 0f, fwd.x);
            Vector3 l = p - side * half;
            Vector3 r = p + side * half;
            if (i > 0) AddQuad(verts, prevL, prevR, r, l);
            prevL = l;
            prevR = r;
        }
        return FacetMesh("StreamRibbon", verts.ToArray());
    }

    // ---- сборка воды в сцену (один путь для DomeWorld и тестов) -------------

    /// <summary>
    /// Собрать всю воду под <paramref name="parent"/>: озеро и реку всегда (если есть
    /// гидрология), плёс/полотно/ручей — только при <c>canyon.valid</c>. Меши кладутся
    /// в <paramref name="track"/>, чтобы мир снял их вместе с корнем; коллайдеров нет.
    /// </summary>
    public static void Build(Transform parent, DomeGenConfigSO cfg, DomeHydro.State hydro,
        DomeCanyon.State canyon, Func<float, float, float> ground, Material waterMat,
        List<Mesh> track)
    {
        if (parent == null) throw new ArgumentNullException(nameof(parent));
        if (waterMat == null) throw new ArgumentNullException(nameof(waterMat));
        if (track == null) track = new List<Mesh>();

        if (hydro != null)
        {
            var lake = LakeDisc(hydro);
            track.Add(lake);
            Part("Lake", lake, waterMat, parent);
            if (cfg != null)
            {
                var river = RiverRibbon(cfg, hydro);
                track.Add(river);
                Part("River", river, waterMat, parent);
            }
        }

        if (!canyon.valid || ground == null) return;

        float lakeLevel = hydro != null ? hydro.lakeLevel : 0f;
        float poolLevel = PoolLevel(canyon, lakeLevel, ground);
        var pool = PoolDisc(canyon, poolLevel);
        track.Add(pool);
        Part("Pool", pool, waterMat, parent);

        var fall = FallSheet(canyon, FallTopLevel(canyon, ground), poolLevel);
        track.Add(fall);
        Part("Fall", fall, waterMat, parent);

        var stream = StreamRibbon(canyon, ground);
        track.Add(stream);
        Part("Stream", stream, waterMat, parent);
    }

    // ---- мелочи -------------------------------------------------------------

    static GameObject Part(string name, Mesh mesh, Material mat, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    static void AddQuad(List<Vector3> v, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        v.Add(a); v.Add(b); v.Add(c);
        v.Add(a); v.Add(c); v.Add(d);
    }

    static Mesh EmptyMesh(string name)
    {
        return new Mesh { name = name };
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
