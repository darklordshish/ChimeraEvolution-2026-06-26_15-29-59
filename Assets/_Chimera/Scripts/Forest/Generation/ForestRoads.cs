using System.Collections.Generic;
using UnityEngine;

/// <summary>Дорога P6: лента по земле между адресами POI; поле не меняет (S4).</summary>
public struct ForestRoad
{
    public List<Vector2> points;
    public float width;
    public bool primary;
    public bool ford;
}

/// <summary>Сеть дорог: MST от базы + 1–2 петли (S4 §5.2).</summary>
public sealed class ForestRoadNetwork
{
    public List<ForestRoad> roads = new List<ForestRoad>();
    public float length;
    public int loops;
    public string note;
}

/// <summary>
/// S4 P6: POI v0 (база, ворота, открытые/общие зоны) → least-cost по сетке 20 м →
/// MST с корнем базы + 1–2 петли. Дорога не идёт через воду/кольцо/станцию/авторские
/// формы; `DomeSpacePaths` не переопределяется. Первый срез — лента по земле.
/// </summary>
public static class ForestRoads
{
    const float Cell = 4f;
    const int MaxGrid = 512;
    /// <summary>Продольный уклон дороги: S4 §5.2 — длительно ≤12°, коротко ≤18°/20 м.
    /// Первый срез держит 12° на шаге сетки (без выемки/насыпи); 18° здесь не нужен,
    /// пока least-cost находит обход — иначе G4 p95 не сойдётся.</summary>
    const float MaxGrade = 12f;

    sealed class RoadGrid
    {
        public int n;
        public float origin;
        public bool[] blocked;
        public float[] height;
    }

    public static ForestRoadNetwork Build(DomeGenConfigSO cfg, IReadOnlyList<ForestPoi> pois,
        System.Func<float, float, float> ground, DomeHydro.State hydro, DomeMasks masks,
        long seed)
    {
        var net = new ForestRoadNetwork();
        if (cfg == null || pois == null || ground == null) return net;

        var nodes = new List<ForestPoi>();
        foreach (var p in pois)
        {
            if (p.kind != ForestPoiKind.Base && p.kind != ForestPoiKind.Gate &&
                p.kind != ForestPoiKind.Zone) continue;
            bool dup = false;
            foreach (var q in nodes)
                if (Vector2.Distance(q.pos, p.pos) < 8f) { dup = true; break; }
            if (!dup) nodes.Add(p);
        }
        int root = -1;
        for (int i = 0; i < nodes.Count; i++)
            if (nodes[i].kind == ForestPoiKind.Base) { root = i; break; }
        if (root < 0) { net.note = "roads=blocked: нет базы (LabDoor)"; return net; }
        if (nodes.Count < 2) { net.note = "roads: одна база, некуда вести"; return net; }

        var grid = BuildGrid(cfg, ground, hydro, masks, nodes[root].pos);
        int n = nodes.Count;
        var cost = new float[n, n];
        var path = new List<Vector2>[n, n];
        // Пары независимы: Path читает общую сетку и пишет только свои ячейки.
        System.Threading.Tasks.Parallel.For(0, n, i =>
        {
            for (int j = i + 1; j < n; j++)
            {
                var p = Path(nodes[i].pos, nodes[j].pos, ground, grid, out float c);
                cost[i, j] = cost[j, i] = c;
                path[i, j] = path[j, i] = p;
            }
        });

        var inTree = new bool[n];
        var best = new float[n];
        var parent = new int[n];
        for (int i = 0; i < n; i++) { best[i] = float.PositiveInfinity; parent[i] = -1; }
        best[root] = 0f;
        for (int k = 0; k < n; k++)
        {
            int u = -1;
            for (int i = 0; i < n; i++)
                if (!inTree[i] && (u < 0 || best[i] < best[u])) u = i;
            if (u < 0 || float.IsPositiveInfinity(best[u])) break;
            inTree[u] = true;
            if (parent[u] >= 0) Add(net, path[parent[u], u], 4f, true);
            for (int v = 0; v < n; v++)
                if (!inTree[v] && cost[u, v] < best[v]) { best[v] = cost[u, v]; parent[v] = u; }
        }

        // Петля — только самая короткая и только если сеть остаётся в бюджете 4 км.
        int bi = -1, bj = -1;
        float bestLoop = float.MaxValue;
        for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
            {
                if (parent[j] == i || parent[i] == j) continue;
                if (float.IsPositiveInfinity(cost[i, j])) continue;
                float len = PathLength(path[i, j]);
                if (len >= bestLoop) continue;
                bestLoop = len; bi = i; bj = j;
            }
        if (bi >= 0 && net.length + bestLoop <= 4000f)
        {
            Add(net, path[bi, bj], 4f, false);
            net.loops = 1;
        }

        AddLairSpurs(net, pois);
        net.note = $"POI {n}, корень {root}, петель {net.loops}, spur {SpurCount}; " +
                   (nodes.Count < 3 ? "мало primary POI — отчёт" : "v0");
        return net;
    }

    static int SpurCount;

    static void AddLairSpurs(ForestRoadNetwork net, IReadOnlyList<ForestPoi> pois)
    {
        SpurCount = 0;
        foreach (var p in pois)
        {
            if (p.kind != ForestPoiKind.Lair) continue;
            float best = float.MaxValue; Vector2 hit = p.pos;
            foreach (var road in net.roads)
                for (int i = 1; i < road.points.Count; i++)
                {
                    Vector2 a = road.points[i - 1], b = road.points[i];
                    Vector2 ab = b - a; float l2 = ab.sqrMagnitude;
                    float t = l2 < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p.pos - a, ab) / l2);
                    Vector2 q = a + ab * t;
                    float d = Vector2.Distance(p.pos, q);
                    if (d < best) { best = d; hit = q; }
                }
            if (best > 8f && best < 80f)
            {
                net.roads.Add(new ForestRoad { points = new List<Vector2> { p.pos, hit }, width = 3f });
                net.length += best;
                SpurCount++;
            }
        }
    }

    static void Add(ForestRoadNetwork net, List<Vector2> pts, float width, bool primary)
    {
        if (pts == null || pts.Count < 2) return;
        net.roads.Add(new ForestRoad { points = pts, width = width, primary = primary });
        for (int i = 1; i < pts.Count; i++) net.length += Vector2.Distance(pts[i - 1], pts[i]);
    }

    static RoadGrid BuildGrid(DomeGenConfigSO cfg, System.Func<float, float, float> ground,
        DomeHydro.State hydro, DomeMasks masks, Vector2 basePos)
    {
        float d = Mathf.Max(100f, cfg.mapDiameter);
        int n = Mathf.Clamp(Mathf.CeilToInt(d / Cell), 32, MaxGrid);
        var g = new RoadGrid
        {
            n = n,
            origin = -0.5f * (n - 1) * Cell,
            blocked = new bool[n * n],
            height = new float[n * n],
        };
        // Клетки независимы: блокировки и высоты читают мир, но не пишут в него.
        // Параллель по строкам снимает 5.4 с BuildGrid, порядок и числа те же.
        System.Threading.Tasks.Parallel.For(0, n, z =>
        {
            for (int x = 0; x < n; x++)
            {
                float wx = g.origin + x * Cell;
                float wz = g.origin + z * Cell;
                bool blocked = Blocked(wx, wz, hydro, masks, basePos);
                g.blocked[z * n + x] = blocked;
                // Высоту blocked-клетки не семплим: A* в неё не заходит, а SampleGround
                // в v2 дорогой (эрозия/композиция/пады).
                if (!blocked) g.height[z * n + x] = ground(wx, wz);
            }
        });
        return g;
    }

    static bool Blocked(float x, float z, DomeHydro.State hydro, DomeMasks masks, Vector2 basePos)
    {
        Vector2 p = new Vector2(x, z);
        if (masks != null)
        {
            if (masks.OverlapsRing(DomeFootprint.Circle(p, 3f))) return true;
            // S4 §5.1: дорога не идёт через воду/террасу/каньон/овраги/landform.
            if (masks.ContainsPoint(p, "water", "terrace", "canyon", "ravine", "landform")) return true;
            // База — LabDoor у станции: 30 м вокруг неё дорога имеет право быть.
            if (Vector2.Distance(p, basePos) > 30f && masks.ContainsPoint(p, "station"))
                return true;
        }
        if (hydro != null)
        {
            if (Vector2.Distance(p, hydro.lakeCenter) < hydro.lakeRadius + 2f) return true;
            foreach (var rp in hydro.riverPts)
                if (Vector2.Distance(p, rp) < hydro.riverHalfWidth + 1f) return true;
        }
        return false;
    }

    static List<Vector2> Path(Vector2 a, Vector2 b, System.Func<float, float, float> ground,
        RoadGrid grid, out float cost)
    {
        var p = GridPath(a, b, grid, out cost);
        if (p != null)
        {
            var sm = Smooth(p, grid, ground, out cost);
            // Прямая короче и законна — берём её, а не зигзаг A*.
            if (LineClear(a, b, grid, ground))
            {
                float straight = Vector2.Distance(a, b);
                if (straight < cost) { cost = straight; return new List<Vector2> { a, b }; }
            }
            return sm;
        }
        var line = TryLine(a, b, ground, out cost);
        if (line != null) return line;
        cost = float.PositiveInfinity;
        return null;
    }

    static List<Vector2> Smooth(List<Vector2> pts, RoadGrid g,
        System.Func<float, float, float> ground, out float cost)
    {
        cost = 0f;
        if (pts == null || pts.Count < 3) { cost = Length(pts); return pts; }
        var outp = new List<Vector2> { pts[0] };
        int i = 0;
        while (i < pts.Count - 1)
        {
            int far = i + 1;
            // Сглаживание смотрит вперёд не дальше 64 клеток (256 м): полный O(n²)
            // по 250-точечной ленте стоил 1.5 с на 28 пар, а выигрыш дальше — тот же.
            int limit = Mathf.Min(pts.Count - 1, i + 64);
            for (int j = limit; j > i + 1; j--)
                if (LineClear(pts[i], pts[j], g, ground)) { far = j; break; }
            outp.Add(pts[far]);
            i = far;
        }
        cost = Length(outp);
        return outp;
    }

    static float PathLength(List<Vector2> pts) => Length(pts);

    static float Length(List<Vector2> pts)
    {
        float s = 0f;
        for (int i = 1; i < pts.Count; i++) s += Vector2.Distance(pts[i - 1], pts[i]);
        return s;
    }

    static bool LineClear(Vector2 a, Vector2 b, RoadGrid g,
        System.Func<float, float, float> ground)
    {
        float len = Vector2.Distance(a, b);
        int steps = Mathf.Max(2, Mathf.CeilToInt(len / Cell));
        float prev = ground(a.x, a.y);
        for (int i = 0; i <= steps; i++)
        {
            Vector2 p = Vector2.Lerp(a, b, i / (float)steps);
            int cx = Mathf.RoundToInt((p.x - g.origin) / Cell);
            int cz = Mathf.RoundToInt((p.y - g.origin) / Cell);
            if (cx < 0 || cz < 0 || cx >= g.n || cz >= g.n) return false;
            if (g.blocked[cz * g.n + cx]) return false;
            float h = ground(p.x, p.y);
            if (i > 0)
            {
                float grade = Mathf.Atan2(Mathf.Abs(h - prev), len / steps) * Mathf.Rad2Deg;
                if (grade > MaxGrade) return false;
            }
            prev = h;
        }
        return true;
    }

    static List<Vector2> GridPath(Vector2 a, Vector2 b, RoadGrid g, out float cost)
    {
        cost = float.PositiveInfinity;
        int s = NearestOpen(a, g);
        int t = NearestOpen(b, g);
        if (s < 0 || t < 0) return null;
        int n = g.n;
        var dist = new float[n * n];
        var prev = new int[n * n];
        var closed = new bool[n * n];
        for (int i = 0; i < dist.Length; i++) { dist[i] = float.PositiveInfinity; prev[i] = -1; }
        var heap = new MinHeap();
        dist[s] = 0f;
        heap.Push(0f, s);
        int[] dx = { 1, -1, 0, 0, 1, 1, -1, -1 };
        int[] dz = { 0, 0, 1, -1, 1, -1, 1, -1 };
        while (heap.Count > 0)
        {
            heap.Pop(out float _, out int cur);
            if (closed[cur]) continue;
            closed[cur] = true;
            if (cur == t) break;
            int cx = cur % n, cz = cur / n;
            float hc = g.height[cur];
            for (int k = 0; k < 8; k++)
            {
                int nx = cx + dx[k], nz = cz + dz[k];
                if (nx < 0 || nz < 0 || nx >= n || nz >= n) continue;
                int ni = nz * n + nx;
                if (closed[ni] || g.blocked[ni]) continue;
                float hn = g.height[ni];
                float step = Cell * (k < 4 ? 1f : 1.41421356f);
                float slope = Mathf.Atan2(Mathf.Abs(hn - hc), step) * Mathf.Rad2Deg;
                if (slope > MaxGrade) continue;
                // 12° — жёсткий допуск S4, но к самому потолку путь дороже: least-cost
                // предпочитает пологий обход, а не держится у границы.
                float costStep = step * (1f + 0.25f * Mathf.Pow(slope / MaxGrade, 4f));
                float nd = dist[cur] + costStep;
                if (nd >= dist[ni]) continue;
                dist[ni] = nd; prev[ni] = cur;
                float hx = Mathf.Abs(g.origin + nx * Cell - b.x);
                float hz = Mathf.Abs(g.origin + nz * Cell - b.y);
                // Октильная нижняя оценка: без неё A* почти Dijkstra и обходит 250k
                // клеток на каждую из 28 пар POI — 13.7 с на сборку.
                float h = Mathf.Max(hx, hz) + 0.41421356f * Mathf.Min(hx, hz);
                heap.Push(nd + h, ni);
            }
        }
        if (!closed[t]) return null;
        var rev = new List<Vector2>();
        for (int i = t; i >= 0; i = prev[i])
        {
            rev.Add(new Vector2(g.origin + (i % n) * Cell, g.origin + (i / n) * Cell));
            if (i == s) break;
        }
        rev.Reverse();
        rev.Insert(0, a); rev.Add(b);
        cost = dist[t];
        return rev;
    }

    static int NearestOpen(Vector2 p, RoadGrid g)
    {
        int n = g.n;
        int cx = Mathf.Clamp(Mathf.RoundToInt((p.x - g.origin) / Cell), 0, n - 1);
        int cz = Mathf.Clamp(Mathf.RoundToInt((p.y - g.origin) / Cell), 0, n - 1);
        // База стоит у станции: ближние 25 м разрешены, даже если клетка в маске.
        for (int r = 0; r <= 12; r++)
            for (int dz = -r; dz <= r; dz++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != r) continue;
                    int x = cx + dx, z = cz + dz;
                    if (x < 0 || z < 0 || x >= n || z >= n) continue;
                    int i = z * n + x;
                    if (!g.blocked[i]) return i;
                }
        return -1;
    }

    static List<Vector2> TryLine(Vector2 a, Vector2 b,
        System.Func<float, float, float> ground, out float cost)
    {
        cost = float.PositiveInfinity;
        float len = Vector2.Distance(a, b);
        if (len < 1f) return null;
        int steps = Mathf.Max(2, Mathf.CeilToInt(len / 10f));
        var pts = new List<Vector2>(steps + 1);
        float sum = 0f, prev = ground(a.x, a.y);
        for (int i = 0; i <= steps; i++)
        {
            Vector2 p = Vector2.Lerp(a, b, i / (float)steps);
            float h = ground(p.x, p.y);
            if (i > 0)
            {
                float grade = Mathf.Atan2(Mathf.Abs(h - prev), len / steps) * Mathf.Rad2Deg;
                if (grade > MaxGrade) return null;
                sum += (len / steps) * (1f + 0.25f * Mathf.Pow(grade / MaxGrade, 4f));
            }
            pts.Add(p); prev = h;
        }
        cost = sum;
        return pts;
    }

    /// <summary>Точка в полосе дороги (для вырубки леса).</summary>
    public static bool Near(ForestRoadNetwork net, float x, float z, float margin = 1.5f)
    {
        if (net == null || net.roads == null) return false;
        Vector2 p = new Vector2(x, z);
        foreach (var road in net.roads)
        {
            if (road.points == null) continue;
            float r = road.width * 0.5f + margin;
            for (int i = 1; i < road.points.Count; i++)
                if (DomeFootprint.DistanceToSegment(p, road.points[i - 1], road.points[i]) < r)
                    return true;
        }
        return false;
    }

    sealed class MinHeap
    {
        readonly List<(float f, int i)> data = new List<(float f, int i)>();
        public int Count => data.Count;
        public void Push(float f, int i)
        {
            data.Add((f, i));
            int c = data.Count - 1;
            while (c > 0)
            {
                int p = (c - 1) / 2;
                if (data[p].f <= data[c].f) break;
                (data[p], data[c]) = (data[c], data[p]);
                c = p;
            }
        }
        public void Pop(out float f, out int i)
        {
            f = data[0].f; i = data[0].i;
            int last = data.Count - 1;
            data[0] = data[last]; data.RemoveAt(last);
            int p = 0;
            while (true)
            {
                int l = p * 2 + 1, r = l + 1, m = p;
                if (l < data.Count && data[l].f < data[m].f) m = l;
                if (r < data.Count && data[r].f < data[m].f) m = r;
                if (m == p) break;
                (data[p], data[m]) = (data[m], data[p]);
                p = m;
            }
        }
    }
}
