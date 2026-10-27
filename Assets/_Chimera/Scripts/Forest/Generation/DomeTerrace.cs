using UnityEngine;

/// <summary>
/// Терраса большого павильона: поднятое ровное пятно над сырым полем.
/// Чистый статический слой — без сцены, без Random, без состояния между вызовами.
/// Порядок географии спеки 27.10: терраса идёт ПОСЛЕ кольца и ДО каньона, поэтому
/// сам слой только поднимает поле и ничего не знает о каньоне и воде.
///
/// Числа — рабочие цели первого среза, не нормы: радиус от диаметра, высота и
/// блендинг локальными константами (DomeGenConfigSO не трогаем).
/// Соли НОВЫЕ: старые потоки (рельеф, овраги, флора) остаются на месте.
/// </summary>
public static class DomeTerrace
{
    /// <summary>
    /// Пятно террасы. valid=false — пятна нет, и Apply обязан быть тождеством
    /// (так же ведёт себя пустой список оврагов). height — прибавка в метрах,
    /// radius — ровная часть, blend — плавный борт шириной в метрах.
    /// </summary>
    public struct State
    {
        public Vector2 center;
        public float radius;
        public float height;
        public float blend;
        public bool valid;
    }

    // Новые соли этого слоя (овраги сидят на 7301..7307 — их не сдвигаем).
    const int SaltOffset = 7501; // сдвиг решётки от сида
    const int SaltRadius = 7502;
    const int SaltHeight = 7503;
    const int SaltBlend = 7504;

    // Рабочие цели: на Ø2000 радиус 80..130 м, борт 28..78 м.
    const float RadiusMinFraction = 0.040f;
    const float RadiusMaxFraction = 0.065f;
    const float BlendMinFraction = 0.35f;
    const float BlendMaxFraction = 0.60f;
    const float HeightMin = 5f;
    const float HeightMax = 9f;

    // Защищённые зоны: запас сверх пятна (метры).
    const float RingKeep = 30f;  // ровно там, где RingField начинает подъём кольца
    const float EntryKeep = 10f;
    const float LabKeep = 33f;   // полудиагональ станции 46×36 ≈ 29 + запас
    const float WaterKeep = 6f;
    const float DryKeep = 1.5f;

    const int Sizes = 3;      // три размера пятна на сид: годный выбирается по земле
    const int GridCells = 64; // решётка 65×65 на круг чаши — перебор, не случай
    const float DefaultDiameter = 2000f;

    /// <summary>
    /// Детерминированный выбор пятна. Не случайные броски (на малом стенде годная
    /// полоса узкая — 0,1% диска, и 128 попыток её не находят), а перебор решётки:
    /// три размера пятна от сида, точки решётки со сдвигом от сида. Тот же сид —
    /// та же решётка и то же пятно, без UnityEngine.Random.
    /// Кандидат обязан быть сухим, внутри чаши, не задевать вход, лабораторию,
    /// скальное кольцо, озеро и русло. Из годных берётся самое высокое место —
    /// терраса читается уровнем рельефа, а не случайным бугром.
    /// Если годного нет — valid=false (слой молчит, мир не ломается).
    /// </summary>
    public static State Build(DomeGenConfigSO cfg, long seed, Vector2 labCenter,
        Vector2 entry, DomeHydro.State hydro, float rin)
    {
        State none = default;
        if (cfg == null || hydro == null) return none;

        float d = cfg.mapDiameter > 0f ? cfg.mapDiameter : DefaultDiameter;
        // 0 у rin — «не настроено»: берём кольцо купола (как делает DomeWorld для оврагов).
        if (rin <= 0f) rin = DomeRockRing.InnerRadius(cfg);
        if (rin <= 1f) return none;

        float lakeLevel = hydro.lakeLevel;
        State best = none;
        float bestGround = float.NegativeInfinity;

        for (int s = 0; s < Sizes; s++)
        {
            float radius = Mathf.Lerp(d * RadiusMinFraction, d * RadiusMaxFraction,
                Hash01(seed, SaltRadius, s));
            float blend = radius * Mathf.Lerp(BlendMinFraction, BlendMaxFraction,
                Hash01(seed, SaltBlend, s));
            // На Ø400 — 5..9 м, на Ø2000 — 12..18 м: водопад должен быть выше
            // «холма» и читаться с игровой камеры.
            float scale = Mathf.InverseLerp(400f, 2000f, d);
            float hMin = Mathf.Lerp(HeightMin, 12f, scale);
            float hMax = Mathf.Lerp(HeightMax, 18f, scale);
            float height = Mathf.Lerp(hMin, hMax, Hash01(seed, SaltHeight, s));
            float reach = radius + blend;
            float maxCenter = rin - reach - RingKeep;
            if (maxCenter <= 0f) continue;

            // Сдвиг решётки меньше шага: разные сиды выбирают разные точки,
            // один и тот же сид повторяется до бита.
            float step = 2f * maxCenter / GridCells;
            float offX = (Hash01(seed, SaltOffset, s * 2) - 0.5f) * step;
            float offZ = (Hash01(seed, SaltOffset, s * 2 + 1) - 0.5f) * step;

            for (int ix = 0; ix <= GridCells; ix++)
            {
                float x = -maxCenter + ix * step + offX;
                for (int iz = 0; iz <= GridCells; iz++)
                {
                    float z = -maxCenter + iz * step + offZ;
                    if (x * x + z * z > maxCenter * maxCenter) continue;
                    var c = new Vector2(x, z);

                    // Защищённые зоны считаются по ПОЛНОМУ пятну (ровная часть + борт).
                    if (Vector2.Distance(c, entry) < reach + EntryKeep) continue;
                    if (Vector2.Distance(c, labCenter) < reach + LabKeep) continue;

                    // Вода: озеро и русло не должны попасть даже в борт.
                    if (hydro.lakeRadius > 0f &&
                        Vector2.Distance(c, hydro.lakeCenter) < reach + hydro.lakeRadius + WaterKeep)
                        continue;
                    if (NearRiver(hydro, c, reach + hydro.riverHalfWidth + WaterKeep)) continue;

                    // Само пятно — на суше: поднимать дно озера или пойму нельзя.
                    float ground = DomeHydro.SampleHydro(cfg, hydro, c.x, c.y);
                    if (ground < lakeLevel + DryKeep) continue;

                    if (ground <= bestGround) continue;
                    bestGround = ground;
                    best = new State
                    {
                        center = c,
                        radius = radius,
                        height = height,
                        blend = blend,
                        valid = true,
                    };
                }
            }
        }
        return best;
    }

    static float Hash01(long seed, int salt, int k) =>
        SeededHash.ToFloat01(SeededHash.Hash(seed, salt, k));

    /// <summary>
    /// Подъём поля: вне пятна — тождество, внутри ровной части — raw + height,
    /// на борту — плавный спад по Эрмиту (непрерывен на обеих кромках).
    /// valid=false — тоже тождество: выключенная терраса не меняет ни одного числа.
    /// </summary>
    public static float Apply(float raw, State s, float x, float z)
    {
        if (!s.valid) return raw;
        float dx = x - s.center.x;
        float dz = z - s.center.y;
        float d = Mathf.Sqrt(dx * dx + dz * dz);
        float blend = Mathf.Max(0f, s.blend);
        if (d >= s.radius + blend) return raw;
        if (d <= s.radius) return raw + s.height;
        if (blend <= 1e-4f) return raw;
        float k = 1f - (d - s.radius) / blend;
        k = k * k * (3f - 2f * k);
        return raw + s.height * k;
    }

    /// <summary>
    /// Точка внутри пятна, включая плавный борт, — для вырубки и запретов.
    /// margin расширяет пятно в метрах (отрицательный не сужает).
    /// </summary>
    public static bool Contains(State s, float x, float z, float margin = 0f)
    {
        if (!s.valid) return false;
        float reach = s.radius + Mathf.Max(0f, s.blend) + Mathf.Max(0f, margin);
        float dx = x - s.center.x;
        float dz = z - s.center.y;
        return dx * dx + dz * dz <= reach * reach;
    }

    static bool NearRiver(DomeHydro.State hydro, Vector2 c, float keep)
    {
        if (hydro.riverPts == null) return false;
        float k = keep * keep;
        for (int i = 0; i < hydro.riverPts.Count; i++)
        {
            Vector2 p = hydro.riverPts[i];
            float dx = p.x - c.x;
            float dz = p.y - c.y;
            if (dx * dx + dz * dz < k) return true;
        }
        return false;
    }
}
