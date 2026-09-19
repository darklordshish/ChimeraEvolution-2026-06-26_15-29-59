using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Детерминированный scatter: rejection sampling по гриду (ячейка = minDist, проверка 3×3).
/// Потоки — SeededHash + счётчик, без Random. Фильтры: уклон > лимита, ниже waterLevel.
/// Может вернуть меньше count (теснота) — не висит. Лес, слайс s8.
/// </summary>
public static class FloraScatter
{
    public struct FloraPoint
    {
        public Vector3 pos;
        public float scale;
        public float rotY;
    }

    public static List<FloraPoint> Scatter(long seed, int salt, int count, float areaHalf,
        float minDist, WorldGenConfigSO world)
    {
        var pts = new List<FloraPoint>();
        if (world == null || count <= 0 || areaHalf <= 0f || minDist <= 0f) return pts;
        float cell = minDist;
        var grid = new Dictionary<(int, int), List<Vector2>>();
        int attempts = 0;
        int maxAttempts = count * 25 + 200;
        int i = 0;
        while (pts.Count < count && attempts < maxAttempts)
        {
            attempts++;
            float x = (SeededHash.ToFloat01(SeededHash.Hash(seed + salt, i, 1)) * 2f - 1f) * areaHalf;
            float z = (SeededHash.ToFloat01(SeededHash.Hash(seed + salt, i, 2)) * 2f - 1f) * areaHalf;
            i++;
            float y = WorldHeightField.SampleHeight(world, x, z);
            if (y < world.waterLevel) continue; // пруды чистые
            if (SlopeAt(world, x, z) > world.maxSlopeDegrees) continue; // на крутизне не растёт
            int cx = Mathf.FloorToInt(x / cell);
            int cz = Mathf.FloorToInt(z / cell);
            bool clash = false;
            for (int ax = cx - 1; ax <= cx + 1 && !clash; ax++)
                for (int az = cz - 1; az <= cz + 1 && !clash; az++)
                {
                    if (!grid.TryGetValue((ax, az), out var cellPts)) continue;
                    foreach (var q in cellPts)
                    {
                        float dx = q.x - x, dz = q.y - z;
                        if (dx * dx + dz * dz < minDist * minDist) { clash = true; break; }
                    }
                }
            if (clash) continue;
            if (!grid.TryGetValue((cx, cz), out var list)) grid[(cx, cz)] = list = new List<Vector2>();
            list.Add(new Vector2(x, z));
            pts.Add(new FloraPoint
            {
                pos = new Vector3(x, y, z),
                scale = 0.8f + SeededHash.ToFloat01(SeededHash.Hash(seed + salt, i, 3)) * 0.5f,
                rotY = SeededHash.ToFloat01(SeededHash.Hash(seed + salt, i, 4)) * 360f
            });
        }
        return pts;
    }

    static float SlopeAt(WorldGenConfigSO world, float x, float z)
    {
        const float e = 0.5f;
        float gx = (WorldHeightField.SampleHeight(world, x + e, z) - WorldHeightField.SampleHeight(world, x - e, z)) / (2f * e);
        float gz = (WorldHeightField.SampleHeight(world, x, z + e) - WorldHeightField.SampleHeight(world, x, z - e)) / (2f * e);
        return Mathf.Atan(Mathf.Sqrt(gx * gx + gz * gz)) * Mathf.Rad2Deg;
    }
}
