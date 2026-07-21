using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ВОЛНЫ ЗВУКА — отображение СЛУХА игрока (слайс Z2, спека `2026-07-22-os-zvuka-vizualizaciya`).
///
/// Решения пользователя, на которых всё держится:
///  • ОТОБРАЖЕНИЕ СЕНСОРИКИ ПРИНАДЛЕЖИТ ОРГАНУ ЭТОЙ СЕНСОРИКИ — волны видит тот, у кого лосиное Ухо
///    (`Perception.KeenHearing`). Слух как канал (строка в сводке) работает и без органа; ВИДЕТЬ звук —
///    это уже острота уха, а не общая способность.
///  • Волна — СФЕРА, расходящаяся от источника, полупрозрачная, сквозь стены (звук идёт сквозь).
///  • Цвет — ОСВЕТЛЁННЫЙ ЦВЕТ ВИДА источника, не цвет приёма: слух отвечает «КТО шумит». Тот же язык,
///    что у нераспознанного замаха (тело светлеет своим цветом) — звук читается как «зверь оттуда».
///  • ШУМИТ — ЗНАЧИТ ИЗЛУЧАЕТ: волну пускает ЛЮБОЙ звук, включая шаги, а не только приёмы. Интенсивность
///    выражают ДВА инструмента: прозрачность и дальность расхождения (= слышимая дальность источника).
///
/// Само-бутстрап, как VitalsHud; переживает перезагрузку сцены (смерть игрока делает LoadScene).
/// </summary>
public class NoiseWaves : MonoBehaviour
{
    [Header("Волна")]
    [SerializeField] float life = 1.8f;         // сколько сфера расходится до полного затухания (дольше = мягче)
    [SerializeField] float minLoud = 0.12f;     // тише этого не излучаем (полный штиль не должен мигать)
    [SerializeField] float alpha = 0.30f;       // «почти прозрачные» — не спорят с миром
    [SerializeField, Range(0f, 1f)] float wash = 0.18f; // насколько ОСВЕТЛЯЕМ цвет вида: меньше — виды различимее
    [SerializeField] int maxWaves = 24;

    [Header("Ритм")]
    [SerializeField] float basePeriod = 1.1f;   // пауза между волнами у ТИХОГО источника
    [SerializeField] float loudPeriod = 0.5f;   // ...и у громкого: громкий пульсирует чаще, но не мельтешит

    static Material sharedMat;
    static readonly int ColorId = Shader.PropertyToID("_Color");

    class Wave
    {
        public Transform tr;
        public MeshRenderer mr;
        public float born, span;
        public Color tone;
    }

    readonly List<Wave> pool = new();
    readonly Dictionary<Noise, float> nextAt = new();    // когда источнику пускать следующую волну
    readonly Dictionary<Noise, float> seenSpike = new(); // последний ОТРИСОВАННЫЙ всплеск источника
    MaterialPropertyBlock mpb;
    Transform player;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (FindAnyObjectByType<NoiseWaves>() != null) return;
        var go = new GameObject("NoiseWaves");
        go.AddComponent<NoiseWaves>();
        DontDestroyOnLoad(go); // бутстрап-атрибут срабатывает раз за запуск, а смерть перезагружает сцену
    }

    void Update()
    {
        mpb ??= new MaterialPropertyBlock();
        Animate();

        // ВОЛНЫ — ФИЧА ОРГАНА: без острого уха звук слышен (сводка), но не виден
        if (!Perception.KeenHearing) return;

        if (player == null)
        {
            var pc = FindAnyObjectByType<PlayerController>();
            if (pc == null) return;
            player = pc.transform;
        }

        float ear = Perception.PlayerSenses != null ? Perception.PlayerSenses.Range(SenseKind.Hearing) : 0f;
        if (ear <= 0f) return;

        foreach (var n in Noise.All)
        {
            if (n == null || n.transform == player) continue; // свои шаги себе не рисуем
            float loud = n.Loudness;
            if (loud < minLoud) continue;

            // СЛЫШИМАЯ ДАЛЬНОСТЬ = громкость × радиус уха (та же формула, что у NPC): она же радиус волны —
            // сфера честно показывает, докуда звук достаёт
            float span = loud * ear;
            if ((n.transform.position - player.position).sqrMagnitude > span * span) continue;

            // ВСПЛЕСК ЗВУЧИТ СРАЗУ: гремок/вой/удар пускают волну в тот же кадр, а не ждут своего такта —
            // иначе погремушка мигает, а волна приходит позже, и звук с картинкой разъезжаются
            bool spiked = seenSpike.TryGetValue(n, out float last) ? n.SpikeAt > last : n.SpikeAt > 0f;
            seenSpike[n] = n.SpikeAt;

            if (!spiked && nextAt.TryGetValue(n, out float t) && Time.time < t) continue;
            nextAt[n] = Time.time + Mathf.Lerp(basePeriod, loudPeriod, loud); // громкий пульсирует чаще
            Spawn(n.transform.position, span, SpeciesTone(n.transform), loud);
        }
    }

    /// <summary>Цвет вида источника, слегка осветлённый — тот же язык, что у нераспознанного замаха: зверь
    /// узнаётся по своему цвету, поданному «не в фокусе». Осветляем СЛАБО (`wash`): сильная примесь белого
    /// сводит все виды к одинаковой пастели, и волк со змеёй перестают различаться. Нет тела — нейтральный.</summary>
    Color SpeciesTone(Transform t)
    {
        Color baseTone = TelegraphColors.Unknown;
        if (t.TryGetComponent<CreatureBody>(out var body) && body.Chassis != null) baseTone = body.Chassis.tint;
        return Color.Lerp(baseTone, Color.white, wash);
    }

    void Spawn(Vector3 pos, float span, Color tone, float loud)
    {
        var w = Free();
        if (w == null) return;
        w.tr.position = pos + Vector3.up * 0.8f; // от туловища, а не от пяток
        w.born = Time.time;
        w.span = span;
        w.tone = tone;
        w.tone.a = alpha * Mathf.Clamp01(loud); // ГРОМКОСТЬ → прозрачность (второй инструмент интенсивности)
        w.mr.enabled = true;
    }

    void Animate()
    {
        for (int i = 0; i < pool.Count; i++)
        {
            var w = pool[i];
            if (w.mr == null || !w.mr.enabled) continue;

            float t = (Time.time - w.born) / Mathf.Max(0.01f, life);
            if (t >= 1f) { w.mr.enabled = false; continue; }

            // МЯГКОЕ РАСПРОСТРАНЕНИЕ: радиус идёт с замедлением (ease-out) — фронт уходит быстро,
            // а у границы слышимости почти замирает, как настоящая волна
            float grow = 1f - (1f - t) * (1f - t);
            w.tr.localScale = Vector3.one * (w.span * 2f * grow);

            // яркость НАРАСТАЕТ и гаснет (а не падает с обрыва): волна проявляется, живёт, растворяется —
            // без этого десяток источников давал резкое мельтешение
            float fade = Mathf.Sin(t * Mathf.PI);
            Color c = w.tone;
            c.a = w.tone.a * fade * fade;
            mpb.SetColor(ColorId, c);
            w.mr.SetPropertyBlock(mpb);
        }
    }

    Wave Free()
    {
        foreach (var w in pool) if (w.mr != null && !w.mr.enabled) return w;
        if (pool.Count >= maxWaves) return null;

        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "NoiseWave";
        go.transform.SetParent(transform, false);
        Destroy(go.GetComponent<Collider>()); // чистый визуал, физике не мешает
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = Mat();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.enabled = false;

        var wave = new Wave { tr = go.transform, mr = mr };
        pool.Add(wave);
        return wave;
    }

    static Material Mat()
    {
        if (sharedMat == null)
        {
            var shader = Shader.Find("Chimera/NoiseWave");
            if (shader == null) shader = Shader.Find("Sprites/Default"); // фолбэк: видно, но не сквозь стены
            sharedMat = new Material(shader);
        }
        return sharedMat;
    }
}
