using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Вода павильона, уровень данных (s10b): Priority-Flood заливка впадин (Barnes 2014) +
/// сток D8 + аккумуляция + carve рек с монотонным спуском + маска озёр.
/// Чистые функции на float[,] — без сцены, тестируются в EditMode.
/// Плоскости воды/шейдеры — следующим заходом; здесь только поле и русла.
/// </summary>
public static class DomeWater
{
    public static readonly int[] Dx = { 1, 1, 0, -1, -1, -1, 0, 1 };
    public static readonly int[] Dz = { 0, 1, 1, 1, 0, -1, -1, -1 };

    /// <summary>
    /// Заливка впадин от границ через очередь с приоритетом: каждая клетка получает
    /// epsilon-спуск к границе. filled >= original везде; filled == original вне впадин.
    /// </summary>
    public static float[,] PriorityFlood(float[,] h, float eps)
    {
        int w = h.GetLength(0);
        int z = h.GetLength(1);
        var filled = (float[,])h.Clone();
        var closed = new bool[w, z];
        var queue = new MinHeap();
        for (int ix = 0; ix < w; ix++)
            for (int iz = 0; iz < z; iz++)
            {
                if (ix == 0 || iz == 0 || ix == w - 1 || iz == z - 1)
                {
                    queue.Push(new Vector2Int(ix, iz), h[ix, iz]);
                    closed[ix, iz] = true;
                }
            }
        while (queue.Count > 0)
        {
            var cell = queue.Pop();
            for (int d = 0; d < 8; d++)
            {
                int nx = cell.x + Dx[d];
                int nz = cell.y + Dz[d];
                if (nx < 0 || nz < 0 || nx >= w || nz >= z || closed[nx, nz]) continue;
                closed[nx, nz] = true;
                filled[nx, nz] = Math.Max(h[nx, nz], filled[cell.x, cell.y] + eps);
                queue.Push(new Vector2Int(nx, nz), filled[nx, nz]);
            }
        }
        return filled;
    }

    /// <summary>Направление стока D8 по залитой сетке: индекс 0..7 или -1 (устье/граница).</summary>
    public static int[,] FlowDir(float[,] filled)
    {
        int w = filled.GetLength(0);
        int z = filled.GetLength(1);
        var dir = new int[w, z];
        for (int ix = 0; ix < w; ix++)
            for (int iz = 0; iz < z; iz++)
            {
                if (ix == 0 || iz == 0 || ix == w - 1 || iz == z - 1) { dir[ix, iz] = -1; continue; }
                float best = 0f;
                int bestD = -1;
                for (int d = 0; d < 8; d++)
                {
                    int nx = ix + Dx[d];
                    int nz = iz + Dz[d];
                    float dist = (d % 2 == 0) ? 1f : 1.41421356f;
                    float drop = (filled[ix, iz] - filled[nx, nz]) / dist;
                    if (drop > best) { best = drop; bestD = d; }
                }
                dir[ix, iz] = bestD;
            }
        return dir;
    }

    /// <summary>Аккумуляция стока: через клетку течёт 1 (себя) + всё сверху. Порядок — по убыванию высоты.</summary>
    public static float[,] Accumulation(float[,] filled, int[,] dir)
    {
        int w = filled.GetLength(0);
        int z = filled.GetLength(1);
        var acc = new float[w, z];
        var order = new List<Vector2Int>(w * z);
        for (int ix = 0; ix < w; ix++)
            for (int iz = 0; iz < z; iz++)
            {
                acc[ix, iz] = 1f;
                order.Add(new Vector2Int(ix, iz));
            }
        order.Sort((a, b) => filled[b.x, b.y].CompareTo(filled[a.x, a.y]));
        foreach (var cell in order)
        {
            int d = dir[cell.x, cell.y];
            if (d < 0) continue;
            acc[cell.x + Dx[d], cell.y + Dz[d]] += acc[cell.x, cell.y];
        }
        return acc;
    }

    /// <summary>Трек реки от истока по D8 до границы/устья. Защита от петель — cap по числу клеток.</summary>
    public static List<Vector2Int> TraceRiver(int[,] dir, int sx, int sz)
    {
        int w = dir.GetLength(0);
        int z = dir.GetLength(1);
        var path = new List<Vector2Int>(w + z);
        var seen = new HashSet<Vector2Int>();
        var cur = new Vector2Int(sx, sz);
        while (cur.x >= 0 && cur.y >= 0 && cur.x < w && cur.y < z && seen.Add(cur) && path.Count < w * z)
        {
            path.Add(cur);
            int d = dir[cur.x, cur.y];
            if (d < 0) break;
            cur = new Vector2Int(cur.x + Dx[d], cur.y + Dz[d]);
        }
        return path;
    }

    /// <summary>
    /// Вырезание русла: клетки трека углубляются на depth, соседи — на bankDepth (берега),
    /// высоты вдоль трека монотонно падают (река не течёт в гору, проверка — в тестах).
    /// </summary>
    public static float[,] CarveRiver(float[,] h, List<Vector2Int> path, float depth, float bankDepth, float eps)
    {
        int w = h.GetLength(0);
        int z = h.GetLength(1);
        var out_ = (float[,])h.Clone();
        float ceiling = float.PositiveInfinity;
        foreach (var cell in path)
        {
            float carved = Math.Min(out_[cell.x, cell.y] - depth, ceiling);
            out_[cell.x, cell.y] = carved;
            ceiling = carved - eps;
            for (int d = 0; d < 8; d++)
            {
                int nx = cell.x + Dx[d];
                int nz = cell.y + Dz[d];
                if (nx < 0 || nz < 0 || nx >= w || nz >= z) continue;
                out_[nx, nz] = Math.Min(out_[nx, nz], out_[cell.x, cell.y] + bankDepth);
            }
        }
        return out_;
    }

    /// <summary>Маска озёр: где заливка подняла поле выше исходного (впадины ниже spill-point).</summary>
    public static bool[,] LakeMask(float[,] orig, float[,] filled, float eps)
    {
        int w = orig.GetLength(0);
        int z = orig.GetLength(1);
        var mask = new bool[w, z];
        for (int ix = 0; ix < w; ix++)
            for (int iz = 0; iz < z; iz++)
                mask[ix, iz] = filled[ix, iz] - orig[ix, iz] > eps;
        return mask;
    }

    /// <summary>Минимальная бинарная куча (cell, приоритет) — PriorityQueue нет в профиле .NET Unity.</summary>
    sealed class MinHeap
    {
        readonly List<Vector2Int> cells = new List<Vector2Int>();
        readonly List<float> keys = new List<float>();

        public int Count => cells.Count;

        public void Push(Vector2Int cell, float key)
        {
            cells.Add(cell);
            keys.Add(key);
            int i = cells.Count - 1;
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (keys[parent] <= keys[i]) break;
                Swap(i, parent);
                i = parent;
            }
        }

        public Vector2Int Pop()
        {
            var top = cells[0];
            int last = cells.Count - 1;
            cells[0] = cells[last];
            keys[0] = keys[last];
            cells.RemoveAt(last);
            keys.RemoveAt(last);
            int i = 0;
            while (true)
            {
                int left = 2 * i + 1;
                int right = left + 1;
                int best = i;
                if (left < cells.Count && keys[left] < keys[best]) best = left;
                if (right < cells.Count && keys[right] < keys[best]) best = right;
                if (best == i) break;
                Swap(i, best);
                i = best;
            }
            return top;
        }

        void Swap(int a, int b)
        {
            var c = cells[a]; cells[a] = cells[b]; cells[b] = c;
            float k = keys[a]; keys[a] = keys[b]; keys[b] = k;
        }
    }
}
