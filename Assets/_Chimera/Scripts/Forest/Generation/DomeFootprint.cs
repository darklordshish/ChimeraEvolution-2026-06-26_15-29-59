using UnityEngine;

/// <summary>Тип габарита (S3 §3.1): запрещено доказывать «свободно» расстоянием до центра.</summary>
public enum DomeFootKind
{
    Circle = 0,
    Capsule = 1,
    Obb = 2,
    Polygon = 3,
}

/// <summary>
/// Габарит места или формы: круг, капсула, повёрнутый прямоугольник, полигон.
/// Пересечение — единственное доказательство «свободно»; центровые расстояния
/// (<c>Vector2.Distance &lt; K</c>) этим классом заменяются.
/// <para>
/// Кадр OBB: локальная <c>+Z</c> смотрит вдоль <see cref="yawDeg"/> (совпадает с
/// кадром плиты станции: <c>world = center + (lx·c + lz·s, −lx·s + lz·c)</c>).
/// </para>
/// </summary>
public struct DomeFootprint
{
    public DomeFootKind kind;
    public Vector2 a;
    public Vector2 b;
    public float radius;
    public float halfWidth;
    public float halfDepth;
    public float yawDeg;
    public Vector2[] points;

    public static DomeFootprint Circle(Vector2 center, float radius) => new DomeFootprint
    {
        kind = DomeFootKind.Circle,
        a = center,
        b = center,
        radius = Mathf.Max(0f, radius),
    };

    public static DomeFootprint Capsule(Vector2 from, Vector2 to, float radius) => new DomeFootprint
    {
        kind = DomeFootKind.Capsule,
        a = from,
        b = to,
        radius = Mathf.Max(0f, radius),
    };

    public static DomeFootprint Obb(Vector2 center, float yawDeg, float halfWidth, float halfDepth) =>
        new DomeFootprint
        {
            kind = DomeFootKind.Obb,
            a = center,
            b = center,
            halfWidth = Mathf.Max(0f, halfWidth),
            halfDepth = Mathf.Max(0f, halfDepth),
            yawDeg = yawDeg,
        };

    public static DomeFootprint Polygon(Vector2[] vertices) => new DomeFootprint
    {
        kind = DomeFootKind.Polygon,
        a = Centroid(vertices),
        points = vertices,
        radius = 0f,
    };

    public bool Contains(Vector2 p)
    {
        switch (kind)
        {
            case DomeFootKind.Circle:
                return (p - a).sqrMagnitude <= radius * radius;
            case DomeFootKind.Capsule:
                return DistanceToSegment(p, a, b) <= radius;
            case DomeFootKind.Obb:
            {
                Local(p, out float lx, out float lz);
                return Mathf.Abs(lx) <= halfWidth && Mathf.Abs(lz) <= halfDepth;
            }
            case DomeFootKind.Polygon:
                return InPolygon(p);
            default:
                return false;
        }
    }

    public Rect Aabb
    {
        get
        {
            switch (kind)
            {
                case DomeFootKind.Circle:
                    return new Rect(a.x - radius, a.y - radius, radius * 2f, radius * 2f);
                case DomeFootKind.Capsule:
                {
                    float minX = Mathf.Min(a.x, b.x) - radius;
                    float minY = Mathf.Min(a.y, b.y) - radius;
                    float maxX = Mathf.Max(a.x, b.x) + radius;
                    float maxY = Mathf.Max(a.y, b.y) + radius;
                    return Rect.MinMaxRect(minX, minY, maxX, maxY);
                }
                case DomeFootKind.Obb:
                {
                    Corners(out var c0, out var c1, out var c2, out var c3);
                    float minX = Mathf.Min(Mathf.Min(c0.x, c1.x), Mathf.Min(c2.x, c3.x));
                    float minY = Mathf.Min(Mathf.Min(c0.y, c1.y), Mathf.Min(c2.y, c3.y));
                    float maxX = Mathf.Max(Mathf.Max(c0.x, c1.x), Mathf.Max(c2.x, c3.x));
                    float maxY = Mathf.Max(Mathf.Max(c0.y, c1.y), Mathf.Max(c2.y, c3.y));
                    return Rect.MinMaxRect(minX, minY, maxX, maxY);
                }
                default:
                {
                    if (points == null || points.Length == 0) return new Rect(a.x, a.y, 0f, 0f);
                    float minX = float.MaxValue, minY = float.MaxValue;
                    float maxX = float.MinValue, maxY = float.MinValue;
                    foreach (var v in points)
                    {
                        minX = Mathf.Min(minX, v.x); minY = Mathf.Min(minY, v.y);
                        maxX = Mathf.Max(maxX, v.x); maxY = Mathf.Max(maxY, v.y);
                    }
                    return Rect.MinMaxRect(minX, minY, maxX, maxY);
                }
            }
        }
    }

    /// <summary>
    /// Диапазон расстояний от начала координат до габарита (для полосы кольца).
    /// Минимум — ТОЧНОЕ расстояние до тела (для OBB — до рамки, для полигона — до ребра),
    /// а не минимум вершин: иначе зона у кольца проскакивала бы между вершинами.
    /// Максимум — по крайним точкам (норма выпукла, максимум на вершине).
    /// </summary>
    public void DistanceRangeFromOrigin(out float min, out float max)
    {
        switch (kind)
        {
            case DomeFootKind.Circle:
                max = a.magnitude + radius;
                min = Mathf.Max(0f, a.magnitude - radius);
                return;
            case DomeFootKind.Capsule:
                min = Mathf.Max(0f, DistanceToSegment(Vector2.zero, a, b) - radius);
                max = Mathf.Max(a.magnitude, b.magnitude) + radius;
                return;
            case DomeFootKind.Obb:
            {
                Corners(out var c0, out var c1, out var c2, out var c3);
                max = Mathf.Max(Mathf.Max(c0.magnitude, c1.magnitude),
                    Mathf.Max(c2.magnitude, c3.magnitude));
                min = Contains(Vector2.zero) ? 0f : LocalDistanceToOrigin();
                return;
            }
            default:
            {
                if (points == null || points.Length == 0)
                {
                    min = max = a.magnitude;
                    return;
                }
                max = float.MinValue;
                foreach (var v in points) max = Mathf.Max(max, v.magnitude);
                min = InPolygon(Vector2.zero) ? 0f : DistanceToEdges(Vector2.zero);
                return;
            }
        }
    }

    /// <summary>Точное расстояние от начала координат до повёрнутого прямоугольника.</summary>
    float LocalDistanceToOrigin()
    {
        Local(Vector2.zero, out float lx, out float lz);
        float qx = Mathf.Max(Mathf.Abs(lx) - halfWidth, 0f);
        float qz = Mathf.Max(Mathf.Abs(lz) - halfDepth, 0f);
        return Mathf.Sqrt(qx * qx + qz * qz);
    }

    float DistanceToEdges(Vector2 p)
    {
        float min = float.MaxValue;
        int n = points.Length;
        for (int i = 0, j = n - 1; i < n; j = i++)
            min = Mathf.Min(min, DistanceToSegment(p, points[j], points[i]));
        return min;
    }

    void Local(Vector2 p, out float lx, out float lz)
    {
        float yaw = yawDeg * Mathf.Deg2Rad;
        float c = Mathf.Cos(yaw), s = Mathf.Sin(yaw);
        float dx = p.x - a.x, dz = p.y - a.y;
        lx = dx * c - dz * s;
        lz = dx * s + dz * c;
    }

    void Corners(out Vector2 c0, out Vector2 c1, out Vector2 c2, out Vector2 c3)
    {
        float yaw = yawDeg * Mathf.Deg2Rad;
        float c = Mathf.Cos(yaw), s = Mathf.Sin(yaw);
        Vector2 w = a;
        c0 = new Vector2(w.x - halfWidth * c - halfDepth * s, w.y + halfWidth * s - halfDepth * c);
        c1 = new Vector2(w.x + halfWidth * c - halfDepth * s, w.y - halfWidth * s - halfDepth * c);
        c2 = new Vector2(w.x + halfWidth * c + halfDepth * s, w.y - halfWidth * s + halfDepth * c);
        c3 = new Vector2(w.x - halfWidth * c + halfDepth * s, w.y + halfWidth * s + halfDepth * c);
    }

    bool InPolygon(Vector2 p)
    {
        if (points == null) return false;
        bool inside = false;
        int n = points.Length;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            Vector2 pi = points[i], pj = points[j];
            if ((pi.y > p.y) != (pj.y > p.y) &&
                p.x < (pj.x - pi.x) * (p.y - pi.y) / (pj.y - pi.y) + pi.x)
                inside = !inside;
        }
        return inside;
    }

    static Vector2 Centroid(Vector2[] pts)
    {
        if (pts == null || pts.Length == 0) return Vector2.zero;
        Vector2 s = Vector2.zero;
        foreach (var p in pts) s += p;
        return s / pts.Length;
    }

    public static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float len2 = ab.sqrMagnitude;
        if (len2 < 1e-8f) return (p - a).magnitude;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
        return (p - (a + ab * t)).magnitude;
    }
}

/// <summary>Пересечение габаритов (S3 §3.1). Полигон — растром; остальное — точной геометрией.</summary>
public static class DomeFootprints
{
    public static bool Overlaps(DomeFootprint x, DomeFootprint y)
    {
        if (x.kind == DomeFootKind.Polygon || y.kind == DomeFootKind.Polygon)
            return RasterOverlap(x, y);
        if (x.kind == DomeFootKind.Circle && y.kind == DomeFootKind.Circle)
            return (x.a - y.a).magnitude <= x.radius + y.radius;
        if (x.kind == DomeFootKind.Circle && y.kind == DomeFootKind.Capsule)
            return DomeFootprint.DistanceToSegment(x.a, y.a, y.b) <= x.radius + y.radius;
        if (x.kind == DomeFootKind.Capsule && y.kind == DomeFootKind.Circle)
            return DomeFootprint.DistanceToSegment(y.a, x.a, x.b) <= x.radius + y.radius;
        if (x.kind == DomeFootKind.Capsule && y.kind == DomeFootKind.Capsule)
            return SegmentDistance(x.a, x.b, y.a, y.b) <= x.radius + y.radius;
        if (x.kind == DomeFootKind.Circle && y.kind == DomeFootKind.Obb)
            return CircleObb(x.a, x.radius, y);
        if (x.kind == DomeFootKind.Obb && y.kind == DomeFootKind.Circle)
            return CircleObb(y.a, y.radius, x);
        if (x.kind == DomeFootKind.Capsule && y.kind == DomeFootKind.Obb)
            return SegmentObb(x.a, x.b, x.radius, y);
        if (x.kind == DomeFootKind.Obb && y.kind == DomeFootKind.Capsule)
            return SegmentObb(y.a, y.b, y.radius, x);
        if (x.kind == DomeFootKind.Obb && y.kind == DomeFootKind.Obb)
            return ObbObb(x, y);
        return false;
    }

    static bool CircleObb(Vector2 center, float radius, DomeFootprint obb)
    {
        float yaw = obb.yawDeg * Mathf.Deg2Rad;
        float c = Mathf.Cos(yaw), s = Mathf.Sin(yaw);
        float dx = center.x - obb.a.x, dz = center.y - obb.a.y;
        float lx = dx * c - dz * s;
        float lz = dx * s + dz * c;
        float qx = Mathf.Max(Mathf.Abs(lx) - obb.halfWidth, 0f);
        float qz = Mathf.Max(Mathf.Abs(lz) - obb.halfDepth, 0f);
        return qx * qx + qz * qz <= radius * radius;
    }

    static bool SegmentObb(Vector2 a, Vector2 b, float radius, DomeFootprint obb)
    {
        // Отрезок внутри OBB — пересечение; иначе ближайшая точка отрезка к рамке.
        float yaw = obb.yawDeg * Mathf.Deg2Rad;
        float c = Mathf.Cos(yaw), s = Mathf.Sin(yaw);
        Vector2 L(Vector2 p)
        {
            float dx = p.x - obb.a.x, dz = p.y - obb.a.y;
            return new Vector2(dx * c - dz * s, dx * s + dz * c);
        }
        Vector2 la = L(a), lb = L(b);
        if (Mathf.Abs(la.x) <= obb.halfWidth && Mathf.Abs(la.y) <= obb.halfDepth) return true;
        if (Mathf.Abs(lb.x) <= obb.halfWidth && Mathf.Abs(lb.y) <= obb.halfDepth) return true;
        // Расстояние до рамки по 4 сторонам.
        const int steps = 16;
        for (int i = 0; i <= steps; i++)
        {
            Vector2 p = Vector2.Lerp(la, lb, i / (float)steps);
            float qx = Mathf.Max(Mathf.Abs(p.x) - obb.halfWidth, 0f);
            float qz = Mathf.Max(Mathf.Abs(p.y) - obb.halfDepth, 0f);
            if (qx * qx + qz * qz <= radius * radius) return true;
        }
        return false;
    }

    static bool ObbObb(DomeFootprint x, DomeFootprint y)
    {
        // SAT по 4 осям (2 на каждый прямоугольник).
        if (Separated(x, y)) return false;
        if (Separated(y, x)) return false;
        return true;
    }

    static bool Separated(DomeFootprint x, DomeFootprint y)
    {
        float yaw = x.yawDeg * Mathf.Deg2Rad;
        float c = Mathf.Cos(yaw), s = Mathf.Sin(yaw);
        for (int k = 0; k < 2; k++)
        {
            Vector2 axis = k == 0 ? new Vector2(c, -s) : new Vector2(s, c);
            Range(x, axis, out float x0, out float x1);
            Range(y, axis, out float y0, out float y1);
            if (x1 < y0 || y1 < x0) return true;
        }
        return false;
    }

    static void Range(DomeFootprint f, Vector2 axis, out float min, out float max)
    {
        if (f.kind == DomeFootKind.Obb)
        {
            float yaw = f.yawDeg * Mathf.Deg2Rad;
            float c = Mathf.Cos(yaw), s = Mathf.Sin(yaw);
            Vector2 ax = new Vector2(c, -s), az = new Vector2(s, c);
            float e = Mathf.Abs(Vector2.Dot(ax, axis)) * f.halfWidth +
                      Mathf.Abs(Vector2.Dot(az, axis)) * f.halfDepth;
            float center = Vector2.Dot(f.a, axis);
            min = center - e;
            max = center + e;
            return;
        }
        if (f.kind == DomeFootKind.Circle)
        {
            float center = Vector2.Dot(f.a, axis);
            min = center - f.radius;
            max = center + f.radius;
            return;
        }
        // Капсула/полигон для OBB-SAT не нужны: вызывается только из ObbObb.
        f.DistanceRangeFromOrigin(out min, out max);
    }

    static bool RasterOverlap(DomeFootprint x, DomeFootprint y)
    {
        Rect a = x.Aabb, b = y.Aabb;
        float x0 = Mathf.Max(a.xMin, b.xMin), x1 = Mathf.Min(a.xMax, b.xMax);
        float y0 = Mathf.Max(a.yMin, b.yMin), y1 = Mathf.Min(a.yMax, b.yMax);
        if (x1 < x0 || y1 < y0) return false;
        float step = Mathf.Clamp(Mathf.Min(x1 - x0, y1 - y0) / 6f, 0.5f, 2f);
        for (float pz = y0; pz <= y1 + step * 0.5f; pz += step)
            for (float px = x0; px <= x1 + step * 0.5f; px += step)
            {
                var p = new Vector2(px, pz);
                if (x.Contains(p) && y.Contains(p)) return true;
            }
        return false;
    }

    static float SegmentDistance(Vector2 a0, Vector2 a1, Vector2 b0, Vector2 b1)
    {
        // Пересечение отрезков (в т.ч. вырожденных) — ноль.
        if (SegmentsCross(a0, a1, b0, b1)) return 0f;
        return Mathf.Min(
            Mathf.Min(DomeFootprint.DistanceToSegment(a0, b0, b1),
                DomeFootprint.DistanceToSegment(a1, b0, b1)),
            Mathf.Min(DomeFootprint.DistanceToSegment(b0, a0, a1),
                DomeFootprint.DistanceToSegment(b1, a0, a1)));
    }

    static bool SegmentsCross(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4)
    {
        float d1 = Cross(p4 - p3, p1 - p3);
        float d2 = Cross(p4 - p3, p2 - p3);
        float d3 = Cross(p2 - p1, p3 - p1);
        float d4 = Cross(p2 - p1, p4 - p1);
        return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) &&
               ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));
    }

    static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
}
