using System;
using System.IO;
using UnityEngine;

/// <summary>
/// S4: слой <c>[erosion]</c> в <see cref="DomeTerrain.ShapeGround"/>.
/// <para>
/// Порядок внутри слоя: <c>hydro(Shape0) → thermal(hydro result)</c>. Вход — снимок
/// <c>Shape0</c> (после hill, до POI и площадок), не <c>SampleGround</c>. Дельта
/// хранится сеткой и выходит наружу только через билинейную выборку.
/// </para>
/// <para>
/// Тепловая: 8 соседей, talus 30°, 6 итераций, перенос 0,4, кламп ±0,4 м.
/// Гидро: droplets на <c>Shape0</c>, дельта R16-квант 0,02 м, кламп ±1,5 м, кеш
/// по ключу <c>(seed, worldVersion, erosionVersion, resolution)</c>. Нет кеша/сбой —
/// <c>hydraulic=missing</c> и тепловая без гидро; подмены «на лету» нет.
/// </para>
/// </summary>
public static class DomeErosion
{
    public sealed class State
    {
        public int resolution;
        public float step;
        public float origin;
        public float[] delta;
        public bool[] mask;
        public int erosionVersion;
        public bool hydraulic;
        public string hydraulicStatus = "off";
        public float hydraulicSeconds;
        public ulong deltaSum;
        public float maxTransfer;

        public float SampleDelta(float x, float z)
        {
            if (delta == null || resolution <= 1) return 0f;
            float fx = (x - origin) / step;
            float fz = (z - origin) / step;
            int x0 = Mathf.FloorToInt(fx), z0 = Mathf.FloorToInt(fz);
            if (x0 < 0 || z0 < 0 || x0 >= resolution - 1 || z0 >= resolution - 1) return 0f;
            float u = fx - x0, v = fz - z0;
            int i00 = z0 * resolution + x0;
            int i10 = i00 + 1;
            int i01 = i00 + resolution;
            int i11 = i01 + 1;
            float a = Mathf.Lerp(delta[i00], delta[i10], u);
            float b = Mathf.Lerp(delta[i01], delta[i11], u);
            return Mathf.Lerp(a, b, v);
        }
    }

    /// <summary>Каталог кеша; тесты подменяют на временный.</summary>
    public static string CacheDir;

    static string Dir =>
        string.IsNullOrEmpty(CacheDir)
            ? Path.Combine(Application.persistentDataPath, "ErosionBake")
            : CacheDir;

    /// <summary>Полный ключ кеша — один и тот же для чтения и записи.</summary>
    public static string Key(long seed, int worldVersion, int erosionVersion, int resolution)
        => $"{seed}_{worldVersion}_{erosionVersion}_{resolution}";

    /// <summary>
    /// Собрать дельту эрозии на снимке <paramref name="preErosion"/>.
    /// <paramref name="protectedAt"/> — формы ДО эрозии (вода/формы/терраса/каньон/овраг/холм);
    /// вне домена (кольцо) маска тоже 1.
    /// </summary>
    public static State Build(DomeGenConfigSO cfg, Func<float, float, float> preErosion,
        Func<float, float, bool> protectedAt, long seed)
    {
        var st = new State();
        if (cfg == null || preErosion == null) return st;
        int n = Mathf.Clamp(cfg.erosionResolution, 64, 1024);
        float d = Mathf.Max(1f, cfg.mapDiameter);
        st.resolution = n;
        st.step = d / n;
        st.origin = -d * 0.5f;
        st.erosionVersion = cfg.erosionVersion;
        st.delta = new float[n * n];
        st.mask = new bool[n * n];

        var h0 = new float[n * n];
        for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                float wx = st.origin + (x + 0.5f) * st.step;
                float wz = st.origin + (z + 0.5f) * st.step;
                h0[z * n + x] = preErosion(wx, wz);
                st.mask[z * n + x] = protectedAt != null && protectedAt(wx, wz);
            }

        var h = (float[])h0.Clone();
        if (cfg.buildHydraulicErosion)
        {
            float t0 = Time.realtimeSinceStartup;
            string key = Key(seed, cfg.WorldVersion, cfg.erosionVersion, n);
            string file = Path.Combine(Dir, "ErosionBake-" + key + ".bin");
            float[] hydro;
            if (TryRead(file, seed, cfg.WorldVersion, cfg.erosionVersion, n, out hydro))
            {
                st.hydraulic = true;
                st.hydraulicStatus = "cache";
            }
            else
            {
                try
                {
                    hydro = HydroBake(cfg, h0, st.mask, n, st.step, seed);
                    if (TryWrite(file, seed, cfg.WorldVersion, cfg.erosionVersion, n,
                            cfg.hydroQuantum, hydro))
                    {
                        st.hydraulic = true;
                        st.hydraulicStatus = "baked";
                    }
                    else
                    {
                        hydro = null;
                        st.hydraulic = false;
                        st.hydraulicStatus = "missing: cache write";
                    }
                }
                catch (Exception e)
                {
                    hydro = null;
                    st.hydraulic = false;
                    st.hydraulicStatus = "missing: " + e.Message;
                }
            }
            if (hydro != null)
            {
                for (int i = 0; i < h.Length; i++)
                    if (!st.mask[i]) h[i] += hydro[i];
            }
            st.hydraulicSeconds = Time.realtimeSinceStartup - t0;
        }

        if (cfg.buildThermalErosion)
            st.maxTransfer = Thermal(h, st.mask, n, st.step, cfg);

        for (int i = 0; i < h.Length; i++)
        {
            float v = h[i] - h0[i];
            if (st.mask[i]) v = 0f;
            float clamp = cfg.buildHydraulicErosion ? cfg.hydroClamp : cfg.thermalClamp;
            v = Mathf.Clamp(v, -clamp, clamp);
            if (cfg.buildHydraulicErosion && cfg.hydroQuantum > 0f)
                v = Mathf.Round(v / cfg.hydroQuantum) * cfg.hydroQuantum;
            st.delta[i] = v;
        }
        st.deltaSum = Sum(st.delta);
        return st;
    }

    public static float Apply(State st, float h, float x, float z)
        => st == null ? h : h + st.SampleDelta(x, z);

    // ------------------------------------------------------------------
    // Тепловая: попарный перенос по 4 «полунаправлениям», масса сохраняется.
    // ------------------------------------------------------------------
    static float Thermal(float[] h, bool[] mask, int n, float step, DomeGenConfigSO cfg)
    {
        float talus = Mathf.Tan(Mathf.Clamp(cfg.thermalTalusDegrees, 1f, 60f) * Mathf.Deg2Rad);
        int iters = Mathf.Clamp(cfg.thermalIterations, 1, 32);
        float rate = Mathf.Clamp01(cfg.thermalRate);
        float maxTransfer = 0f;
        int[] dx = { 1, 1, 0, -1 };
        int[] dz = { 0, 1, 1, 1 };
        for (int it = 0; it < iters; it++)
        {
            maxTransfer = 0f;
            for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++)
                {
                    int a = z * n + x;
                    if (mask[a]) continue;
                    for (int k = 0; k < 4; k++)
                    {
                        int nx = x + dx[k], nz = z + dz[k];
                        if (nx < 0 || nz < 0 || nx >= n || nz >= n) continue;
                        int b = nz * n + nx;
                        if (mask[b]) continue;
                        float dh = h[a] - h[b];
                        float dist = step * (k == 0 || k == 2 ? 1f : 1.41421356f);
                        float excess = Mathf.Abs(dh) - talus * dist;
                        if (excess <= 0f) continue;
                        float move = rate * 0.5f * excess;
                        if (move > Mathf.Abs(dh) * 0.5f) move = Mathf.Abs(dh) * 0.5f;
                        if (move <= 0f) continue;
                        // Перенос всегда от высокого к низкому: полунаправление
                        // проверяет пару один раз, знак решает, кто отдаёт.
                        if (dh > 0f) { h[a] -= move; h[b] += move; }
                        else { h[b] -= move; h[a] += move; }
                        if (move > maxTransfer) maxTransfer = move;
                    }
                }
            if (maxTransfer < 1e-4f) break;
        }
        return maxTransfer;
    }

    // ------------------------------------------------------------------
    // Гидроэрозия droplets на Shape0. RNG — только SeededHash.
    // ------------------------------------------------------------------
    static float[] HydroBake(DomeGenConfigSO cfg, float[] h0, bool[] mask, int n, float step,
        long seed)
    {
        var h = (float[])h0.Clone();
        var delta = new float[n * n];
        int drops = Mathf.Clamp(cfg.hydroDrops, 0, 500000);
        int steps = Mathf.Clamp(cfg.hydroSteps, 1, 256);
        float inertia = Mathf.Clamp01(cfg.hydroInertia);
        float capacityFactor = Mathf.Max(0.01f, cfg.hydroCapacity);
        float erode = Mathf.Clamp01(cfg.hydroErode);
        float deposit = Mathf.Clamp01(cfg.hydroDeposit);
        float evaporate = Mathf.Clamp01(cfg.hydroEvaporate);
        int radius = Mathf.Clamp(cfg.hydroRadius, 0, 8);
        float quantum = Mathf.Max(0.001f, cfg.hydroQuantum);
        float clamp = Mathf.Max(0.01f, cfg.hydroClamp);

        for (int k = 0; k < drops; k++)
        {
            float u = SeededHash.ToFloat01(SeededHash.Hash(seed ^ 0x4D59, k, 1));
            float v = SeededHash.ToFloat01(SeededHash.Hash(seed ^ 0x4D59, k, 2));
            float px = st_origin(n, step) + u * (n - 1) * step;
            float pz = st_origin(n, step) + v * (n - 1) * step;
            float dirX = 0f, dirZ = 0f, speed = 1f, water = 1f, sediment = 0f;
            for (int s = 0; s < steps; s++)
            {
                int cx = Mathf.Clamp(Mathf.FloorToInt((px - st_origin(n, step)) / step), 0, n - 2);
                int cz = Mathf.Clamp(Mathf.FloorToInt((pz - st_origin(n, step)) / step), 0, n - 2);
                int i = cz * n + cx;
                if (mask[i]) break;
                float gx = (h[i + 1] - h[i]) / step;
                float gz = (h[i + n] - h[i]) / step;
                dirX = dirX * inertia - gx * (1f - inertia);
                dirZ = dirZ * inertia - gz * (1f - inertia);
                float len = Mathf.Sqrt(dirX * dirX + dirZ * dirZ);
                if (len < 1e-6f) break;
                dirX /= len; dirZ /= len;
                float nx = px + dirX * step, nz = pz + dirZ * step;
                if (nx < st_origin(n, step) + step || nz < st_origin(n, step) + step ||
                    nx > -st_origin(n, step) - step || nz > -st_origin(n, step) - step) break;
                int ix = Mathf.FloorToInt((nx - st_origin(n, step)) / step);
                int iz = Mathf.FloorToInt((nz - st_origin(n, step)) / step);
                if (ix < 0 || iz < 0 || ix >= n - 1 || iz >= n - 1) break;
                int j = iz * n + ix;
                if (mask[j]) break;
                float hOld = h[i], hNew = h[j];
                float dH = hNew - hOld;
                float capacity = Mathf.Max(-dH, 0.01f) * speed * water * capacityFactor;
                if (dH > capacity || sediment > capacity)
                {
                    float amount = Mathf.Min(capacity - dH, sediment);
                    if (amount > 0f)
                    {
                        h[i] += amount * deposit;
                        delta[i] += amount * deposit;
                        sediment -= amount;
                    }
                }
                else
                {
                    float amount = Mathf.Min((capacity - dH) * erode, -dH);
                    if (amount > 0f)
                    {
                        Brush(h, delta, mask, n, ix, iz, radius, amount);
                        sediment += amount;
                    }
                }
                speed = Mathf.Sqrt(Mathf.Max(0.01f, speed * speed + dH * -1f));
                water *= (1f - evaporate);
                px = nx; pz = nz;
                if (speed < 0.01f || water < 0.01f) break;
            }
        }

        for (int i = 0; i < delta.Length; i++)
        {
            float v = Mathf.Clamp(delta[i], -clamp, clamp);
            if (mask[i]) v = 0f;
            delta[i] = Mathf.Round(v / quantum) * quantum;
        }
        return delta;
    }

    static float st_origin(int n, float step) => -0.5f * (n - 1) * step;

    static void Brush(float[] h, float[] delta, bool[] mask, int n, int cx, int cz, int radius,
        float amount)
    {
        int count = 0;
        for (int z = Mathf.Max(0, cz - radius); z <= Mathf.Min(n - 1, cz + radius); z++)
            for (int x = Mathf.Max(0, cx - radius); x <= Mathf.Min(n - 1, cx + radius); x++)
                if (!mask[z * n + x]) count++;
        if (count == 0) return;
        float each = amount / count;
        for (int z = Mathf.Max(0, cz - radius); z <= Mathf.Min(n - 1, cz + radius); z++)
            for (int x = Mathf.Max(0, cx - radius); x <= Mathf.Min(n - 1, cx + radius); x++)
            {
                int i = z * n + x;
                if (mask[i]) continue;
                h[i] -= each;
                delta[i] -= each;
            }
    }

    static ulong Sum(float[] a)
    {
        ulong h = 14695981039346656037UL;
        for (int i = 0; i < a.Length; i++)
        {
            uint bits = BitConverter.ToUInt32(BitConverter.GetBytes(a[i]), 0);
            h ^= bits + 0x9E3779B97F4A7C15UL + (h << 6) + (h >> 2);
        }
        return h;
    }

    // ------------------------------------------------------------------
    // Кеш: заголовок (ключ/разрешение/квант) + float[] дельты.
    // Битый/чужой — «кеша нет», пересборка.
    // ------------------------------------------------------------------
    const uint Magic = 0x45345231u; // "E4R1"

    static bool TryRead(string file, long seed, int worldVersion, int erosionVersion, int n,
        out float[] delta)
    {
        delta = null;
        try
        {
            if (!File.Exists(file)) return false;
            using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
            using (var br = new BinaryReader(fs))
            {
                if (br.ReadUInt32() != Magic) return false;
                if (br.ReadInt64() != seed) return false;
                if (br.ReadInt32() != worldVersion) return false;
                if (br.ReadInt32() != erosionVersion) return false;
                if (br.ReadInt32() != n) return false;
                br.ReadSingle(); // quantum (формат не входит в ключ)
                int count = br.ReadInt32();
                if (count != n * n) return false;
                var d = new float[count];
                for (int i = 0; i < count; i++) d[i] = br.ReadSingle();
                if (br.ReadUInt64() != Sum(d)) return false;
                delta = d;
                return true;
            }
        }
        catch { return false; }
    }

    static bool TryWrite(string file, long seed, int worldVersion, int erosionVersion, int n,
        float quantum, float[] delta)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            using (var fs = new FileStream(file, FileMode.Create, FileAccess.Write))
            using (var bw = new BinaryWriter(fs))
            {
                bw.Write(Magic);
                bw.Write(seed);
                bw.Write(worldVersion);
                bw.Write(erosionVersion);
                bw.Write(n);
                bw.Write(quantum);
                bw.Write(delta.Length);
                foreach (float v in delta) bw.Write(v);
                bw.Write(Sum(delta));
            }
            return true;
        }
        catch { return false; }
    }
}
