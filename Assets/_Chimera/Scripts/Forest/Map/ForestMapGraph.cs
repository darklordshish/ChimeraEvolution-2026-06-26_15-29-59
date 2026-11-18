using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ГРАФ ПУТЕЙ КАРТЫ (спека `2026-11-18-forest-karta-kak-istochnik` §9, шаг 6; схема — ГеймБосс 18.11). Строит лес, ИЗ
/// СЛОЁВ КАРТЫ (растр, зоны, логова, водопои, вход, дверь лаборатории) — при 5б он не меняется. Симулятор граф только читает.
///
/// Вершина: id, x, z, вид, зона. Ребро несёт ФАКТЫ, а не веса видов: длина пути в метрах, наибольший уклон, переправа
/// (да/нет и длина), преобладающая поверхность. Сколько ребро стоит волку или змее, считает закон тела у механиков.
///
/// Как строится ребро: кратчайший ход по клеткам растра (8 соседей, длина — метры, стена и «вне кольца» непроходимы,
/// вода — только если сухого пути нет, и тогда это переправа) между вершиной и её <see cref="Neighbours"/> ближайшими
/// по прямой соседями. Сначала ищется сухой путь: в игре в озере не плавают (навмеш обходит воду), и ребро «через озеро»
/// спрятало бы сухой обход от закона тела (найдено 18.11: логово–водопой переплывало озеро 151 м).
/// Путь по растру, а не по прямой: вода и рельеф делают прямую ложной (спека симулятора §8 п. 3).
/// </summary>
[Serializable]
public sealed class ForestMapGraph
{
    public enum VertexKind { Zone, Entry, Lair, Waterhole, Lab, Forage }

    [Serializable]
    public sealed class Vertex
    {
        public string id;
        public float x, z;
        public VertexKind kind;
        /// <summary>Индекс зоны карты, внутри которой вершина; −1 — вне зон.</summary>
        public int zone = -1;
    }

    [Serializable]
    public sealed class Edge
    {
        public string a, b;
        public float length;
        public float maxSlope;
        public bool crossesWater;
        public float waterLength;
        public ForestMapRaster.Surface surface;
    }

    /// <summary>Сколько ближайших соседей связывает каждая вершина.</summary>
    public const int Neighbours = 4;
    /// <summary>Как далеко (в клетках) искать проходимую клетку для вершины, стоящей на стене или у кольца.</summary>
    public const int SnapCells = 6;

    public string mapHash;
    public string rasterKey;
    public List<Vertex> vertices = new List<Vertex>();
    public List<Edge> edges = new List<Edge>();
    /// <summary>
    /// Ход ребра по растру (прореженный, каждые <see cref="PathEvery"/> клеток) — параллельно `edges`, для картинки:
    /// ребро рисуется ломаной по фактическому пути, а не прямой (ГеймБосс 18.11). Не сериализуется — в схему не входит.
    /// </summary>
    [NonSerialized] public List<List<Vector2>> paths = new List<List<Vector2>>();
    public const int PathEvery = 4;

    public static ForestMapGraph Build(ForestMap map, ForestMapRaster r)
    {
        if (map == null) throw new ArgumentNullException(nameof(map));
        if (r == null) throw new ArgumentNullException(nameof(r));
        var g = new ForestMapGraph { mapHash = map.hash, rasterKey = r.key };

        for (int i = 0; i < map.zones.Count; i++) g.Add(map, $"zone:{i}", map.zones[i].center, VertexKind.Zone);
        g.Add(map, "entry", new Vector2(map.points.entry.x, map.points.entry.z), VertexKind.Entry);
        for (int i = 0; i < map.lairs.Count; i++)
            g.Add(map, $"lair:{i}", new Vector2(map.lairs[i].pos.x, map.lairs[i].pos.z), VertexKind.Lair);
        for (int i = 0; i < map.waterholes.Count; i++)
            g.Add(map, $"water:{i}", map.waterholes[i].center, VertexKind.Waterhole);
        if (map.lab.has) g.Add(map, "lab", new Vector2(map.lab.door.x, map.lab.door.z), VertexKind.Lab);
        if (map.forage != null)
            for (int i = 0; i < map.forage.Count; i++) g.Add(map, $"forage:{i}", map.forage[i].pos, VertexKind.Forage);

        // Пары: каждая вершина — с Neighbours ближайшими по прямой; пары без повторов, в порядке вершин.
        var pairs = new SortedSet<long>();
        int vn = g.vertices.Count;
        for (int i = 0; i < vn; i++)
        {
            var order = new List<int>();
            for (int j = 0; j < vn; j++) if (j != i) order.Add(j);
            var pi = g.P(i);
            order.Sort((p, q) => Vector2.Distance(pi, g.P(p)).CompareTo(Vector2.Distance(pi, g.P(q))));
            for (int k = 0; k < Mathf.Min(Neighbours, order.Count); k++)
            {
                int a = Mathf.Min(i, order[k]), b = Mathf.Max(i, order[k]);
                pairs.Add((long)a * 100000 + b);
            }
        }
        var paths = new RasterPaths(r);
        foreach (long key in pairs)
        {
            int a = (int)(key / 100000), b = (int)(key % 100000);
            var e = paths.Edge(g.P(a), g.P(b));
            if (e == null) continue; // не дойти по растру — ребра нет: это факт ландшафта
            e.a = g.vertices[a].id;
            e.b = g.vertices[b].id;
            g.edges.Add(e);
            g.paths.Add(paths.LastPath);
        }

        // Мосты между кучками: соседство по прямой даёт кучки (логова у кольца связаны только друг с другом), а связность —
        // факт растра (найдено 18.11, сид 17). Пары вершин из разных кучек — по возрастанию прямой; ребро ставится, если
        // по растру есть путь (как Краскал). Кучки, до которых по растру не дойти, так и остаются отдельными.
        var parent = new int[vn];
        for (int i = 0; i < vn; i++) parent[i] = i;
        int Find(int x) { while (parent[x] != x) x = parent[x] = parent[parent[x]]; return x; }
        var index = new Dictionary<string, int>();
        for (int i = 0; i < vn; i++) index[g.vertices[i].id] = i;
        foreach (var e in g.edges) parent[Find(index[e.a])] = Find(index[e.b]);
        var bridges = new List<(float d, int a, int b)>();
        for (int i = 0; i < vn; i++)
            for (int j = i + 1; j < vn; j++)
                if (Find(i) != Find(j)) bridges.Add((Vector2.Distance(g.P(i), g.P(j)), i, j));
        bridges.Sort((x, y) => x.d != y.d ? x.d.CompareTo(y.d) : x.a != y.a ? x.a.CompareTo(y.a) : x.b.CompareTo(y.b));
        var failed = new HashSet<long>(); // пары корней кучек, между которыми пути нет
        foreach (var (d, a, b) in bridges)
        {
            int ra = Find(a), rb = Find(b);
            if (ra == rb) continue;
            long rootKey = (long)Mathf.Min(ra, rb) * 100000 + Mathf.Max(ra, rb);
            if (failed.Contains(rootKey)) continue;
            var e = paths.Edge(g.P(a), g.P(b));
            if (e == null) { failed.Add(rootKey); continue; }
            e.a = g.vertices[a].id;
            e.b = g.vertices[b].id;
            g.edges.Add(e);
            g.paths.Add(paths.LastPath);
            parent[ra] = rb;
        }
        return g;
    }

    Vector2 P(int i) => new Vector2(vertices[i].x, vertices[i].z);

    void Add(ForestMap map, string id, Vector2 p, VertexKind kind)
    {
        int zone = -1;
        for (int i = 0; i < map.zones.Count; i++)
            if (Vector2.Distance(p, map.zones[i].center) <= map.zones[i].radius) { zone = i; break; }
        vertices.Add(new Vertex { id = id, x = p.x, z = p.y, kind = kind, zone = zone });
    }

    /// <summary>Компоненты связности: сколько вершин достижимо из вершины <paramref name="from"/> по рёбрам.</summary>
    public HashSet<string> Reachable(string from)
    {
        var adj = new Dictionary<string, List<string>>();
        foreach (var e in edges)
        {
            if (!adj.TryGetValue(e.a, out var la)) adj[e.a] = la = new List<string>();
            if (!adj.TryGetValue(e.b, out var lb)) adj[e.b] = lb = new List<string>();
            la.Add(e.b); lb.Add(e.a);
        }
        var seen = new HashSet<string> { from };
        var stack = new Stack<string>();
        stack.Push(from);
        while (stack.Count > 0)
            if (adj.TryGetValue(stack.Pop(), out var next))
                foreach (var v in next) if (seen.Add(v)) stack.Push(v);
        return seen;
    }

    /// <summary>
    /// Истина связности — заливка растра (вода проходима) от вершины <paramref name="from"/>: id вершин, до которых по
    /// растру можно дойти. Граф обязан давать то же (сторож `Graph_ReachabilityEqualsRaster_Seed17`).
    /// </summary>
    public static HashSet<string> RasterReachable(ForestMap map, ForestMapRaster r, ForestMapGraph g, string from)
    {
        var result = new HashSet<string>();
        Vertex src = g.vertices.Find(v => v.id == from);
        if (src == null) return result;
        int s = SnapCell(r, new Vector2(src.x, src.z), dry: false);
        if (s < 0) return result;
        var seen = new bool[r.n * r.n];
        var q = new Queue<int>();
        seen[s] = true; q.Enqueue(s);
        while (q.Count > 0)
        {
            int c = q.Dequeue(), cx = c % r.n, cz = c / r.n;
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int x = cx + dx, z = cz + dz;
                    if (x < 0 || z < 0 || x >= r.n || z >= r.n) continue;
                    int i = z * r.n + x;
                    if (seen[i] || !r.Passable(i)) continue;
                    seen[i] = true; q.Enqueue(i);
                }
        }
        foreach (var v in g.vertices)
        {
            int c = SnapCell(r, new Vector2(v.x, v.z), dry: false);
            if (c >= 0 && seen[c]) result.Add(v.id);
        }
        return result;
    }

    /// <summary>Ближайшая проходимая клетка к точке (в пределах <see cref="SnapCells"/>); −1 — нет.</summary>
    static int SnapCell(ForestMapRaster r, Vector2 p, bool dry)
    {
        if (!r.Cell(p, out int ix, out int iz)) return -1;
        for (int rad = 0; rad <= SnapCells; rad++)
            for (int dz = -rad; dz <= rad; dz++)
                for (int dx = -rad; dx <= rad; dx++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != rad) continue;
                    int x = ix + dx, z = iz + dz;
                    if (x < 0 || z < 0 || x >= r.n || z >= r.n) continue;
                    int i = r.Index(x, z);
                    if (r.Passable(i) && !(dry && r.cls[i] == ForestMapRaster.Cls.Water)) return i;
                }
        return -1;
    }

    public string ToJson() => JsonUtility.ToJson(this, true);

    /// <summary>A* по клеткам растра (8 соседей, длина — метры) с фактами пути.</summary>
    sealed class RasterPaths
    {
        readonly ForestMapRaster r;
        readonly float[] g;
        readonly int[] from;
        readonly int[] stamp;
        int run;

        public RasterPaths(ForestMapRaster raster)
        {
            r = raster;
            int count = r.n * r.n;
            g = new float[count];
            from = new int[count];
            stamp = new int[count];
        }

        bool Open(int i, bool dry) => r.Passable(i) && !(dry && r.cls[i] == ForestMapRaster.Cls.Water);

        int Snap(Vector2 p, bool dry) => SnapCell(r, p, dry);

        /// <summary>Сухой путь, а если его нет — через воду; null — не дойти вовсе.</summary>
        public Edge Edge(Vector2 pa, Vector2 pb) => Search(pa, pb, dry: true) ?? Search(pa, pb, dry: false);

        Edge Search(Vector2 pa, Vector2 pb, bool dry)
        {
            int s = Snap(pa, dry), t = Snap(pb, dry);
            if (s < 0 || t < 0) return null;
            run++;
            int n = r.n;
            float diag = r.step * 1.41421356f;
            var open = new SortedSet<(float f, int i)>();
            g[s] = 0f; from[s] = -1; stamp[s] = run;
            open.Add((H(s, t), s));
            var closed = new HashSet<int>();
            while (open.Count > 0)
            {
                var top = open.Min;
                open.Remove(top);
                int c = top.i;
                if (c == t) return Facts(s, t);
                if (!closed.Add(c)) continue;
                int cx = c % n, cz = c / n;
                for (int dz = -1; dz <= 1; dz++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dz == 0) continue;
                        int x = cx + dx, z = cz + dz;
                        if (x < 0 || z < 0 || x >= n || z >= n) continue;
                        int i = z * n + x;
                        if (!Open(i, dry) || closed.Contains(i)) continue;
                        float ng = g[c] + (dx != 0 && dz != 0 ? diag : r.step);
                        if (stamp[i] == run && ng >= g[i]) continue;
                        if (stamp[i] == run) open.Remove((g[i] + H(i, t), i));
                        g[i] = ng; from[i] = c; stamp[i] = run;
                        open.Add((ng + H(i, t), i));
                    }
            }
            return null;
        }

        float H(int i, int t)
        {
            int n = r.n;
            float dx = (i % n - t % n) * r.step, dz = (i / n - t / n) * r.step;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>Прореженный ход последнего найденного пути (от s к t).</summary>
        public List<Vector2> LastPath { get; private set; }

        Edge Facts(int s, int t)
        {
            var e = new Edge();
            var cells = new List<int>();
            for (int c = t; c >= 0; c = c == s ? -1 : from[c]) cells.Add(c);
            cells.Reverse();
            var path = new List<Vector2>();
            for (int k = 0; k < cells.Count; k++)
                if (k % PathEvery == 0 || k == cells.Count - 1) path.Add(r.Center(cells[k] % r.n, cells[k] / r.n));
            LastPath = path;
            var bySurface = new float[Enum.GetValues(typeof(ForestMapRaster.Surface)).Length];
            int n = r.n;
            for (int c = t; c != s && c >= 0; c = from[c])
            {
                int p = from[c];
                if (p < 0) break;
                float len = (c % n != p % n && c / n != p / n) ? r.step * 1.41421356f : r.step;
                e.length += len;
                e.maxSlope = Mathf.Max(e.maxSlope, r.slopeDeg[c]);
                if (r.cls[c] == ForestMapRaster.Cls.Water) { e.crossesWater = true; e.waterLength += len; }
                bySurface[(int)r.surface[c]] += len;
            }
            int best = 0;
            for (int k = 1; k < bySurface.Length; k++) if (bySurface[k] > bySurface[best]) best = k;
            e.surface = (ForestMapRaster.Surface)best;
            return e;
        }
    }
}
