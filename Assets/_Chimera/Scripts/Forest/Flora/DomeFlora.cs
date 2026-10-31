using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Флора купола (s10i + срез «деревья и подлесок» 25.10): детерминированный
/// rejection sampling с гейтами (крутизна/вода/пояса) + посадка китом на shared-материалах.
/// Без коллайдеров (навигация — отдельным бейком).
/// Срез 25.10: порода по весам (широколист / ель-сосна / зонтик / сухостой / берёза),
/// подлесок пятнами, вырубка гасит ТОЛЬКО деревья. Соли scatter 111/133/144/155/166
/// не сдвинуты — те же точки на тех же сидах; новые решения — новые соли 701…709.
/// </summary>
public static class DomeFlora
{
    public struct Pt
    {
        public Vector3 pos;
        public float scale;
        public float rotY;
    }

    /// <summary>Счётчики сборки для теста бюджета (И2): ни один не растёт сверх запроса.</summary>
    public struct BuildCounts
    {
        public int trees;
        public int treeColliders;
        public int coverPods;
        public List<Vector3> treeColliderPositions;
        public int bushes;
        public int grass;
        public int ferns;
        public int rocks;
        public float crownArea;
        public float crownCover;
    }

    /// <summary>
    /// Скаттер: sample(x,z) отдаёт (высота, годен). Чистая логика — тесты на синтетике.
    /// </summary>
    public static List<Pt> Scatter(long seed, int salt, int count, float diameter,
        float minDist, Func<float, float, (float h, bool ok)> sample)
    {
        var pts = new List<Pt>();
        if (count <= 0 || diameter <= 0f || minDist <= 0f) return pts;
        var grid = new Dictionary<(int, int), List<Vector2>>();
        int attempts = 0;
        int maxAttempts = count * 25 + 200;
        int i = 0;
        while (pts.Count < count && attempts < maxAttempts)
        {
            attempts++;
            float x = (SeededHash.ToFloat01(SeededHash.Hash(seed + salt, i, 1)) * 2f - 1f) * diameter / 2;
            float z = (SeededHash.ToFloat01(SeededHash.Hash(seed + salt, i, 2)) * 2f - 1f) * diameter / 2;
            i++;
            if (new Vector2(x, z).magnitude > diameter / 2) continue;
            var (h, ok) = sample(x, z);
            if (!ok) continue;
            int cx = Mathf.FloorToInt(x / minDist);
            int cz = Mathf.FloorToInt(z / minDist);
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
            pts.Add(new Pt
            {
                pos = new Vector3(x, h, z),
                scale = 0.8f + SeededHash.ToFloat01(SeededHash.Hash(seed + salt, i, 3)) * 0.5f,
                rotY = SeededHash.ToFloat01(SeededHash.Hash(seed + salt, i, 4)) * 360f,
            });
        }
        return pts;
    }

    /// <summary>
    /// Порода дерева по весам (срез 25.10) вместо k%3, где треть леса была сухостоем.
    /// Широколист 40–45 %, ель/сосна 20–25 %, зонтик 10–15 %, сухостой 8–12 %,
    /// остаток — берёза (~12 %). Высота мягко перекашивает внутри полос: выше — больше
    /// хвои, ниже — больше широколиста (старое «ели выше/суше» живо, но полосы держит).
    /// Детерминировано хэшем (seed, 701, k); позиции точек от породы не зависят.
    /// </summary>
    public static FloraKind KindFor(long seed, int k, float heightAboveWater)
    {
        float roll = SeededHash.ToFloat01(SeededHash.Hash(seed, 701, k));
        float t = Mathf.Clamp01((heightAboveWater - 1f) / 4f);
        float broadleaf = Mathf.Lerp(0.45f, 0.40f, t);
        float conifer = Mathf.Lerp(0.20f, 0.25f, t);
        const float umbrella = 0.13f;
        const float dead = 0.10f;
        if (roll < broadleaf) return FloraKind.PikeTree;
        roll -= broadleaf;
        if (roll < conifer) return FloraKind.SpruceTree;
        roll -= conifer;
        if (roll < umbrella) return FloraKind.UmbrellaTree;
        roll -= umbrella;
        if (roll < dead) return FloraKind.DeadHook;
        return FloraKind.BirchTree;
    }

    static float CrownRadius(FloraKind kind)
    {
        switch (kind)
        {
            case FloraKind.PikeTree: return 4.0f;
            case FloraKind.UmbrellaTree: return 4.2f;
            case FloraKind.SpruceTree: return 3.6f;
            case FloraKind.DeadHook: return 2.4f;
            default: return 3.2f;
        }
    }

    static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
    static readonly List<Material> mats = new List<Material>();

    static Mesh Kit(string key, Func<Mesh> build)
    {
        if (!meshes.TryGetValue(key, out var mesh)) meshes[key] = mesh = build();
        return mesh;
    }

    static Material Flat(Color color, bool doubleSided)
    {
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = color;
        mat.enableInstancing = true;
        if (doubleSided) mat.doubleSidedGI = true;
        mats.Add(mat);
        return mat;
    }

    static GameObject Part(Transform root, Mesh mesh, Material mat, Pt p)
    {
        return Part(root, mesh, mat, p.pos, Quaternion.Euler(0f, p.rotY, 0f), p.scale);
    }

    static GameObject Part(Transform root, Mesh mesh, Material mat, Vector3 pos, Quaternion rot, float scale)
    {
        var go = new GameObject("DomeFlora");
        go.transform.SetParent(root, false);
        go.transform.position = pos;
        go.transform.rotation = rot;
        go.transform.localScale = Vector3.one * scale;
        var filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = mat;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go;
    }

    /// <summary>
    /// Дерево: ствол утоплен комлем на 0.3 м, крона стоит на своей высоте от земли.
    /// Наклон (2–6°, ~25 %) общий для ствола и кроны — крона едет по оси ствола,
    /// а не остаётся висеть в центре.
    /// </summary>
    static void Tree(Transform root, Pt p, Quaternion rot, Mesh trunkMesh, Material trunkMat,
        Mesh crownMesh, Material leafMat, float crownLift, bool collider = false)
    {
        var trunk = Part(root, trunkMesh, trunkMat, p.pos - rot * Vector3.up * 0.3f, rot, p.scale);
        if (collider)
        {
            var cap = trunk.AddComponent<CapsuleCollider>();
            cap.center = new Vector3(0f, 7f, 0f);
            cap.height = 14f;
            cap.radius = 0.45f;
        }
        // крона висит по оси ствола, чтобы при наклоне не отрывалась
        Part(root, crownMesh, leafMat, p.pos + rot * Vector3.up * (crownLift * p.scale), rot, p.scale);
    }
    /// <summary>
    /// Дерево 12–18 м: разброс scatter (0.8–1.3) сжат в 0.90–1.12; номинальные высоты
    /// подобраны так, чтобы и комель (−0.3 м), и верхний край не выводили за коридор.
    /// Кусты/трава сохраняют свой масштаб.
    /// </summary>
    static Pt TreeScale(Pt p)
    {
        p.scale = Mathf.Lerp(0.90f, 1.12f, Mathf.InverseLerp(0.8f, 1.3f, p.scale));
        return p;
    }

    /// <summary>Наклон 2–6° у ~25 % деревьев; остальные стоят ровно. Детерминировано.</summary>
    static Quaternion TreeRotation(long seed, int k, float rotY)
    {
        if (SeededHash.ToFloat01(SeededHash.Hash(seed, 702, k)) >= 0.25f)
            return Quaternion.Euler(0f, rotY, 0f);
        float ang = 2f + SeededHash.ToFloat01(SeededHash.Hash(seed, 702, k + 10000)) * 4f;
        float a = SeededHash.ToFloat01(SeededHash.Hash(seed, 702, k + 20000)) * Mathf.PI * 2f;
        return Quaternion.Euler(Mathf.Cos(a) * ang, rotY, Mathf.Sin(a) * ang);
    }

    static Material BarkFor(long seed, int k, FloraKind kind,
        Material brown, Material grey, Material birch)
    {
        if (kind == FloraKind.BirchTree) return birch;
        return SeededHash.ToFloat01(SeededHash.Hash(seed, 703, k)) < 0.6f ? brown : grey;
    }

    static Material LeafFor(long seed, int k, FloraKind kind,
        Material mid, Material dark, Material cold)
    {
        if (kind == FloraKind.SpruceTree) return cold;
        if (kind == FloraKind.BirchTree) return dark;
        if (kind == FloraKind.UmbrellaTree) return mid;
        // Широколист — половина на половину: тёмная листва держит контраст с землёй.
        return SeededHash.ToFloat01(SeededHash.Hash(seed, 704, k)) < 0.5f ? mid : dark;
    }

    /// <summary>
    /// Пятна подлеска (20–30 м): 10 центров по сиду (радиус 0.10–0.40 диаметра),
    /// центры не в вырубке и не на воде/крутизне. Между пятнами подлеска нет;
    /// вырубка R12/R14 — исключение (там он сеется, деревья — нет).
    /// </summary>
    static List<(Vector2 c, float r)> UndergrowthPatches(long seed, float d,
        Func<float, float, bool> exclude, Func<float, float, bool> undergrowthExclude,
        Func<float, float, (float h, bool ok)> ground)
    {
        var list = new List<(Vector2 c, float r)>();
        const int want = 10;
        for (int i = 0; i < want; i++)
        {
            for (int attempt = 0; attempt < 16; attempt++)
            {
                int salt = i * 37 + attempt;
                float ang = (i + 0.5f) / want * Mathf.PI * 2f
                    + (SeededHash.ToFloat01(SeededHash.Hash(seed, 706, salt)) - 0.5f) * 0.5f;
                float rad = d * (0.10f + SeededHash.ToFloat01(SeededHash.Hash(seed, 706, salt + 5000)) * 0.30f);
                float pr = 24f + SeededHash.ToFloat01(SeededHash.Hash(seed, 706, salt + 10000)) * 6f;
                float cx = Mathf.Cos(ang) * rad, cz = Mathf.Sin(ang) * rad;
                if (new Vector2(cx, cz).magnitude + pr > d * 0.5f - 8f) continue;
                if (exclude != null && exclude(cx, cz)) continue;
                if (undergrowthExclude != null && undergrowthExclude(cx, cz)) continue;
                var (_, ok) = ground(cx, cz);
                if (!ok) continue;
                list.Add((new Vector2(cx, cz), pr));
                break;
            }
        }
        return list;
    }

    static bool InPatchOrClearing(List<(Vector2 c, float r)> patches,
        Func<float, float, bool> exclude, Func<float, float, bool> undergrowthExclude,
        float x, float z)
    {
        // Пятно комплекса — не подлесок: под 12-см плитой трава торчала бы в зале.
        if (undergrowthExclude != null && undergrowthExclude(x, z)) return false;
        if (exclude != null && exclude(x, z)) return true; // R12/R14: подлесок живёт
        for (int i = 0; i < patches.Count; i++)
        {
            float dx = x - patches[i].c.x, dz = z - patches[i].c.y;
            if (dx * dx + dz * dz <= patches[i].r * patches[i].r) return true;
        }
        return false;
    }

    static bool NearTree(List<Pt> trees, float x, float z, float radius)
    {
        float r2 = radius * radius;
        for (int i = 0; i < trees.Count; i++)
        {
            float dx = trees[i].pos.x - x, dz = trees[i].pos.z - z;
            if (dx * dx + dz * dz <= r2) return true;
        }
        return false;
    }

    /// <summary>
    /// Сборка флоры: counts для кадра (не лимиты s10d — те для Indirect-рантайма).
    /// groundOverride — пол мира (в рантайме с flatten-площадкой лаборатории);
    /// без него — вырезанное гидрологией поле превью. exclude — вырубка (Entry R12,
    /// пятно комплекса, R14 у LabDoor): гасит ТОЛЬКО деревья и скалы, подлесок
    /// сеется в R12/R14 и пятнах. undergrowthExclude — отдельно пятно комплекса:
    /// под 12-см плитой трава торчала бы в зале, поэтому туда подлесок не сеется.
    /// Возвращает число деревьев (496 на Ø400/сид 4).
    /// </summary>
    public static int Build(GameObject root, DomeGenConfigSO cfg, DomeHydro.State hydro, long seed,
        Func<float, float, float> groundOverride = null, Func<float, float, bool> exclude = null,
        Func<float, float, bool> undergrowthExclude = null, Func<float, float, bool> colliderNear = null,
        IReadOnlyList<ForestSpace.CoverPod> coverPods = null,
        Func<float, float, bool> edgeNear = null)
    {
        return Build(root, cfg, hydro, seed, groundOverride, exclude, undergrowthExclude, out _, colliderNear, coverPods, edgeNear);
    }

    /// <summary>Та же сборка + счётчики бюджетов (И2) наружу.</summary>
    public static int Build(GameObject root, DomeGenConfigSO cfg, DomeHydro.State hydro, long seed,
        Func<float, float, float> groundOverride, Func<float, float, bool> exclude,
        Func<float, float, bool> undergrowthExclude, out BuildCounts counts,
        Func<float, float, bool> colliderNear = null,
        IReadOnlyList<ForestSpace.CoverPod> coverPods = null,
        Func<float, float, bool> edgeNear = null)
    {
        if (root == null) throw new ArgumentNullException(nameof(root));
        if (cfg == null) throw new ArgumentNullException(nameof(cfg));
        if (hydro == null) throw new ArgumentNullException(nameof(hydro));
        counts = default;
        counts.treeColliderPositions = new List<Vector3>();
        float d = cfg.mapDiameter;
        float lakeY = hydro.lakeLevel;
        Func<float, float, float> baseGround = groundOverride
            ?? ((x, z) => DomeHydro.SampleHydro(cfg, hydro, x, z));

        // Гейты без вырубки: вода, крутизна, озеро. Подлесок живёт и в вырубке.
        Func<float, float, (float, bool)> groundRaw = (x, z) =>
        {
            float h = baseGround(x, z);
            if (h < lakeY) return (h, false);
            if (DomeSlope.SlopeAtS(cfg, x, z, baseGround) > cfg.maxSlopeDegrees) return (h, false);
            float dx = x - hydro.lakeCenter.x, dz = z - hydro.lakeCenter.y;
            if (dx * dx + dz * dz < hydro.lakeRadius * hydro.lakeRadius) return (h, false);
            return (h, true);
        };
        // Деревья и скалы — с вырубкой.
        Func<float, float, (float, bool)> ground = (x, z) =>
        {
            if (exclude != null && exclude(x, z)) return (baseGround(x, z), false);
            return groundRaw(x, z);
        };

        // 6 общих материалов деревьев (3 коры + 3 кроны) + трава/скала. Не по экземпляру.
        var leafMid = Flat(DomePalette.Crown, false);                    // 0.24/0.38/0.20
        var leafDark = Flat(new Color(0.18f, 0.30f, 0.15f), false);
        var leafCold = Flat(new Color(0.16f, 0.30f, 0.22f), false);      // ель/сосна
        var barkBrown = Flat(DomePalette.Bark, false);                   // 0.35/0.25/0.18
        var barkGrey = Flat(new Color(0.31f, 0.28f, 0.24f), false);
        var barkBirch = Flat(new Color(0.62f, 0.60f, 0.54f), false);
        var grassMat = Flat(new Color(0.55f, 0.60f, 0.30f), true);
        var rockMatF = Flat(DomePalette.Rock, false);

        // Деревья: точки те же (соль 111, 700 запрос), меняются порода/посадка/цвет.
        int treeTarget = cfg.treeCount > 0 ? cfg.treeCount : 700;

        // Пятна леса/поляны: низкочастотный fBm поверх общего пола.
        if (cfg.patchyFlora)
        {
            var patchBase = ground;
            float threshold = Mathf.Lerp(-0.35f, 0.15f, cfg.forestFraction);
            ground = (x, z) =>
            {
                var g = patchBase(x, z);
                if (!g.Item2) return g;
                float n = ValueNoise2D.Fbm(seed + 9111L, x * 0.0025f, z * 0.0025f, 3);
                return (g.Item1, n > threshold);
            };
        }

        if (edgeNear != null)
        {
            var baseTreeGround = ground;
            ground = (x, z) =>
            {
                var g = baseTreeGround(x, z);
                if (!g.Item2 || edgeNear(x, z)) return g;
                float r = SeededHash.ToFloat01(
                    SeededHash.Hash(seed, 9801 + (int)(x * 7f), (int)(z * 7f)));
                return (g.Item1, r < 0.34f);
            };
        }
        var trees = Scatter(seed, 111, treeTarget, d, 10f, ground);
        int k = 0, deadIndex = 0, colliderCount = 0;
        foreach (var p0 in trees)
        {
            var p = TreeScale(p0);
            var kind = KindFor(seed, k, p0.pos.y - lakeY);
            float crown = CrownRadius(kind) * p.scale;
            counts.crownArea += Mathf.PI * crown * crown;
            var rot = TreeRotation(seed, k, p.rotY);
            var trunkMat = BarkFor(seed, k, kind, barkBrown, barkGrey, barkBirch);
            var leafMat = LeafFor(seed, k, kind, leafMid, leafDark, leafCold);
            bool fallen = false;
            bool wantCollider = cfg.patchyFlora && colliderCount < 500 &&
                ((colliderNear != null && colliderNear(p.pos.x, p.pos.z)) || k % 4 == 0);

            if (kind == FloraKind.PikeTree)
            {
                // Широколист: ствол 15.5 м, крона шире и ниже (r 4.0, lift 8.6).
                Tree(root.transform, p, rot,
                    Kit("dtrunk0.52/0.28/15.5", () => FloraMeshKit.Trunk(0.52f, 0.28f, 15.5f)),
                    trunkMat,
                    Kit("dbroad4.0x6.9", () => FloraMeshKit.BroadleafCrown(4.0f, 6.9f)),
                    leafMat, 8.6f, wantCollider);
            }
            else if (kind == FloraKind.UmbrellaTree)
            {
                // Зонтик: ниже ствол, крона уже и выше (r 4.2, lift 4.0).
                Tree(root.transform, p, rot,
                    Kit("dtrunk0.60/0.38/7.5", () => FloraMeshKit.Trunk(0.60f, 0.38f, 7.5f)),
                    trunkMat,
                    Kit("dbroad4.2x9.6", () => FloraMeshKit.BroadleafCrown(4.2f, 9.6f)),
                    leafMat, 4.0f, wantCollider);
            }
            else if (kind == FloraKind.SpruceTree)
            {
                // Два хвойных силуэта: ель (3 яруса) и сосна (5 ярусов).
                if (SeededHash.ToFloat01(SeededHash.Hash(seed, 709, k)) < 0.5f)
                {
                    Tree(root.transform, p, rot,
                        Kit("dtrunk0.48/0.14/15.4", () => FloraMeshKit.Trunk(0.48f, 0.14f, 15.4f)),
                        trunkMat,
                        Kit("dspruce3.6x12.6", () => FloraMeshKit.SpruceCrown(3.6f, 12.6f)),
                        leafMat, 2.5f, wantCollider);
                }
                else
                {
                    Tree(root.transform, p, rot,
                        Kit("dtrunk0.42/0.12/15.0", () => FloraMeshKit.Trunk(0.42f, 0.12f, 15.0f)),
                        trunkMat,
                        Kit("dpine3.3x12.5", () => FloraMeshKit.PineCrown(3.3f, 12.5f)),
                        leafMat, 2.6f, wantCollider);
                }
            }
            else if (kind == FloraKind.BirchTree)
            {
                // Берёза: тонкий ствол, светлая кора, мелкая редкая крона.
                Tree(root.transform, p, rot,
                    Kit("dtrunk0.18/0.09/15.0", () => FloraMeshKit.Trunk(0.18f, 0.09f, 15.0f)),
                    trunkMat,
                    Kit("dbirch2.2x4.8", () => FloraMeshKit.BirchCrown(2.2f, 4.8f)),
                    leafMat, 10.8f, wantCollider);
            }
            else // DeadHook
            {
                // Бюджет сухостоя делится: до 26 поваленных стволов, остальные — сломанные.
                Quaternion logRot = Quaternion.Euler(0f, p.rotY, 0f) * Quaternion.Euler(90f, 0f, 0f);
                Vector3 dir = logRot * Vector3.up;
                float len = 10f * p.scale;
                // Лежащий ствол не должен влезать в вырубку/пятно комплекса концом:
                // точка дерева уже снаружи, но 10-метровый ствол может дотянуться.
                bool logClear = exclude == null
                    || (!exclude(p.pos.x + dir.x * len * 0.5f, p.pos.z + dir.z * len * 0.5f)
                        && !exclude(p.pos.x - dir.x * len * 0.5f, p.pos.z - dir.z * len * 0.5f));
                if (deadIndex < 26 && logClear)
                {
                    fallen = true;
                    Vector3 center = p.pos - dir * (len * 0.5f);
                    center.y = p.pos.y + 0.35f * p.scale - 0.2f; // утоплен на 0.2 м, без коллайдера
                    Part(root.transform,
                        Kit("dlog10", () => FloraMeshKit.Trunk(0.35f, 0.12f, 10f)),
                        barkGrey, center, logRot, p.scale);
                }
                else
                {
                    Part(root.transform,
                        Kit("dsnag0.40/0.10/14.5/2.6", () => FloraMeshKit.Snag(0.40f, 0.10f, 14.5f, 2.6f)),
                        barkGrey, p.pos - rot * Vector3.up * 0.3f, rot, p.scale);
                }
                deadIndex++;
            }

            // Корневые заломы у 30–40 % стоящих деревьев: 5–6 рёбер, 1.5–2.5 м.
            if (!fallen && SeededHash.ToFloat01(SeededHash.Hash(seed, 705, k)) < 0.35f)
            {
                float spread = 1.5f + Mathf.Floor(SeededHash.ToFloat01(SeededHash.Hash(seed, 705, k + 10000)) * 3f) * 0.5f;
                int ribs = SeededHash.ToFloat01(SeededHash.Hash(seed, 705, k + 20000)) < 0.5f ? 5 : 6;
                Part(root.transform,
                    Kit($"droot{spread}/{ribs}", () => FloraMeshKit.AshRoots(spread, ribs)),
                    trunkMat, p.pos - Vector3.up * 0.08f, Quaternion.Euler(0f, p.rotY, 0f), p.scale);
            }
            if (wantCollider && kind != FloraKind.DeadHook)
            {
                colliderCount++;
                counts.treeColliderPositions.Add(p.pos);
            }
            k++;
        }
        counts.treeColliders = colliderCount;
        counts.trees = trees.Count;
        counts.crownCover = d > 0f
            ? counts.crownArea / (Mathf.PI * (d * 0.5f) * (d * 0.5f))
            : 0f;

        // Подлесок: на СТЕНДЕ (Ø≤600) — пятна 20–30 м + вырубка, между пятнами пусто;
        // на большом мире (Ø2000 A/C) — прежний равномерный путь, чтобы не «сломать A/C»
        // десятком пятен на 2 км. Бюджеты не растут ни там, ни там.
        bool patchy = d <= 600f;
        var patches = patchy
            ? UndergrowthPatches(seed, d, exclude, undergrowthExclude, groundRaw)
            : new List<(Vector2 c, float r)>();
        Func<float, float, (float, bool)> underGround = patchy
            ? (Func<float, float, (float, bool)>)((x, z) =>
            {
                var (h, ok) = groundRaw(x, z);
                if (!ok) return (h, false);
                if (!InPatchOrClearing(patches, exclude, undergrowthExclude, x, z)) return (h, false);
                return (h, true);
            })
            : ground; // старый путь: равномерно, вырубка гасит и подлесок

        int bi = 0;
        foreach (var p in Scatter(seed, 133, 400, d, 4f, underGround))
        {
            int lobes = 3 + Mathf.Clamp(
                (int)(SeededHash.ToFloat01(SeededHash.Hash(seed, 708, bi)) * 5f), 0, 4);
            Part(root.transform,
                Kit($"dbushNest{lobes}", () => FloraMeshKit.BushNest(0.7f, lobes)),
                leafDark, p);
            bi++;
        }
        counts.bushes = bi;

        int gi = 0;
        var tuft = Kit("dtuft", () => FloraMeshKit.GrassTuft(0.12f, 0.7f));
        foreach (var p in Scatter(seed, 144, 2000, d, 1.5f, underGround))
        {
            Part(root.transform, tuft, grassMat, p);
            gi++;
        }
        counts.grass = gi;

        // Папоротник — у воды и у комлей деревьев; у воды живёт и вне пятен.
        int fi = 0;
        var fern = Kit("dfern", () => FloraMeshKit.Fern(0.8f, 0.5f, 0.08f));
        foreach (var p in Scatter(seed, 155, 250, d, 2f, (x, z) =>
        {
            var (h, ok) = patchy ? groundRaw(x, z) : ground(x, z);
            if (!ok) return (h, false);
            if (patchy && undergrowthExclude != null && undergrowthExclude(x, z)) return (h, false);
            bool nearWater = h <= lakeY + 2f;
            if (patchy && !nearWater && !InPatchOrClearing(patches, exclude, undergrowthExclude, x, z))
                return (h, false);
            if (!nearWater && !NearTree(trees, x, z, 5f)) return (h, false);
            return (h, true);
        }))
        {
            Part(root.transform, fern, grassMat, p);
            fi++;
        }
        counts.ferns = fi;

        int ri = 0;
        foreach (var p in Scatter(seed, 166, 120, d, 6f, (x, z) =>
        {
            float h = baseGround(x, z);
            if (h < lakeY || DomeSlope.SlopeAtS(cfg, x, z, baseGround) < 20f) return (h, false);
            return (h, true);
        }))
        {
            Part(root.transform, Kit("dslab", () => FloraMeshKit.Slab(0.9f, 0.5f, 0.7f)), rockMatF, p);
            ri++;
        }
        counts.rocks = ri;
        int pi = 0;
        if (coverPods != null)
        {
            foreach (var pod in coverPods)
            {
                float h = baseGround(pod.center.x, pod.center.y);
                if (h < lakeY + 0.5f) continue;
                bool log = SeededHash.ToFloat01(SeededHash.Hash(seed, 9701, pi)) < 0.45f;
                if (log)
                {
                    var rot = Quaternion.Euler(0f, SeededHash.ToFloat01(SeededHash.Hash(seed, 9702, pi)) * 360f, 90f);
                    Part(root.transform, Kit("dcoverLog", () => FloraMeshKit.Trunk(0.35f, 0.15f, 4.5f)),
                        barkGrey, new Vector3(pod.center.x, h + 0.35f, pod.center.y), rot, 1f);
                }
                else
                {
                    Part(root.transform, Kit("dcoverRock", () => FloraMeshKit.Slab(2.4f, 1.2f, 1.8f)),
                        rockMatF, new Vector3(pod.center.x, h - 0.2f, pod.center.y),
                        Quaternion.Euler(0f, SeededHash.ToFloat01(SeededHash.Hash(seed, 9703, pi)) * 360f, 0f), 1f);
                }
                pi++;
            }
        }
        counts.coverPods = pi;

        Debug.Log($"[Forest] флора купола: деревья {counts.trees}, кусты {counts.bushes}, " +
                  $"трава {counts.grass}, папоротники {counts.ferns}, скалы {counts.rocks}");
        return counts.trees;
    }

    /// <summary>Редакторный WipePreview: немедленный снос кэшей (прежнее поведение).</summary>
    public static void ClearMats() => DestroyCaches(true);

    /// <summary>Рантайм: в Play Destroy (отложенный), в редакторе — DestroyImmediate.</summary>
    public static IReadOnlyList<Material> Materials => mats;
    public static void ReleaseCaches() => DestroyCaches(!Application.isPlaying);

    static void DestroyCaches(bool immediate)
    {
        foreach (var mat in mats)
            if (mat != null) Destroy(mat, immediate);
        mats.Clear();
        foreach (var mesh in meshes.Values)
            if (mesh != null) Destroy(mesh, immediate);
        meshes.Clear();
    }

    static void Destroy(UnityEngine.Object obj, bool immediate)
    {
        if (immediate) UnityEngine.Object.DestroyImmediate(obj);
        else UnityEngine.Object.Destroy(obj);
    }
}
