using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ШУМ существа — эмиттер оси звука (лось, срез B). САМ меряет скорость своего тела каждый кадр:
/// бежишь — громко, крадёшься — тихо, замер — беззвучен. В психики/контроллеры ничего не проводится —
/// любое движущееся тело шумит естественно (дизайн-принцип воронки манка: источник звука + спад
/// с расстоянием, всё происходит само). Spike() — разовый всплеск (атака/приземление/гремок) поверх
/// шума движения. Слушатель опрашивает статикой Hear: СЛЫШИМАЯ ДАЛЬНОСТЬ = громкость × радиус слуха —
/// бегущего слышно через полкарты, крадущегося — в упор. Вешает CreatureBody всем телам.
/// </summary>
public class Noise : MonoBehaviour
{
    [SerializeField] float loudSpeed = 8f; // скорость «полной громкости» (бег/рывок); медленнее — тише линейно
    [SerializeField] float smoothing = 6f; // сглаживание громкости (шаги не мигают нулями между кадрами)
    [SerializeField] float bulk = 1f;      // ГАБАРИТ: крупная туша шумит громче на той же скорости (лось ~2.5 — психика ставит)

    static readonly List<Noise> all = new();

    Vector3 lastPos;
    float loudness;          // громкость движения 0..1 (сглаженная)
    float spike, spikeUntil; // разовый всплеск поверх движения (гаснет сам)

    public float Loudness => Mathf.Max(loudness, Time.time < spikeUntil ? spike : 0f);

    [SerializeField] float painLoud = 0.75f;     // ВОПЛЬ БОЛИ: раненый вскрикивает — драка слышна даже в тишине
    [SerializeField] float painTime = 0.4f;

    Health health;

    void Awake()
    {
        // боль озвучивает САМ эмиттер, а не каждая психика поштучно: новый вид кричит без единой проводки
        if (TryGetComponent(out health)) health.onDamaged.AddListener(OnPain);
    }

    void OnDestroy() { if (health != null) health.onDamaged.RemoveListener(OnPain); }

    void OnPain() => Spike(painLoud, painTime);

    void OnEnable() { all.Add(this); lastPos = transform.position; }
    void OnDisable() => all.Remove(this);

    /// <summary>Габарит тела: множитель громкости движения (туша топает громче). Ставит психика/тело.</summary>
    public void SetBulk(float k) => bulk = Mathf.Max(0.1f, k);

    /// <summary>Разовый всплеск шума (атака, приземление, гремок): громкость 0..1 на duration секунд.</summary>
    public void Spike(float strength, float duration) => Spike(strength, duration, TelegraphColors.Unknown);

    /// <summary>Всплеск С ТОНОМ события — цветом приёма из общей легенды. ВОЛНА ЕСТЬ ТЕЛЕГРАФ НА РАССТОЯНИИ
    /// (идея пользователя): вой сиреневый, укус красный. Распознать тон, как и замах, даёт Чутьё —
    /// без него волна безымянно-светлая.</summary>
    public void Spike(float strength, float duration, Color tone)
    {
        spike = Mathf.Clamp01(strength);
        spikeUntil = Time.time + duration;
        Tone = tone;
        SpikeAt = Time.time;
    }

    public Color Tone { get; private set; } = TelegraphColors.Unknown; // тон последнего события (задел: цвет приёма)

    /// <summary>Когда случился последний ВСПЛЕСК. Визуализатор пускает волну ровно в этот момент —
    /// иначе гремок мигает погремушкой, а волна ждёт своего такта, и звук с картинкой разъезжаются.</summary>
    public float SpikeAt { get; private set; } = -999f;

    /// <summary>Все живые источники — визуализатор волн обходит их сам (шуметь может ЛЮБОЕ тело, не только приёмом).</summary>
    public static IReadOnlyList<Noise> All => all;

    void Update()
    {
        Vector3 d = transform.position - lastPos; d.y = 0f; // вертикаль (гравитация/климб) не шумит
        lastPos = transform.position;
        float target = Time.deltaTime > 0f ? Mathf.Clamp01(d.magnitude / Time.deltaTime / loudSpeed * bulk) : 0f;
        loudness = Mathf.Lerp(loudness, target, 1f - Mathf.Exp(-smoothing * Time.deltaTime));
    }

    /// <summary>Самый громкий СЛЫШИМЫЙ источник для уха в точке ear с радиусом слуха range (свой — исключён;
    /// dev-призрак беззвучен). Слышимая дальность источника = его громкость × range слушателя.
    /// source — КТО шумит: слушатель сам решает семантику (странный звук → любопытство, свой — не новость).</summary>
    public static bool Hear(Vector3 ear, float range, Transform self, out Vector3 pos, out float strength, out Noise source)
    {
        pos = Vector3.zero; strength = 0f; source = null;
        foreach (var n in all)
        {
            if (n == null || n.transform == self) continue;
            // призрак не слышен (как и не виден); dev-тишина глушит игрока отдельно — для диагностики каналов
            if ((Perception.PlayerGhost || Perception.DevSilent) && n.GetComponent<PlayerController>() != null) continue;
            float loud = n.Loudness;
            if (loud <= 0.01f) continue;
            float audible = loud * range;
            float dist = Vector3.Distance(ear, n.transform.position);
            if (dist > audible) continue;
            float s = loud * (1f - dist / Mathf.Max(audible, 0.01f)); // сила восприятия: громче и ближе — сильнее
            if (s > strength) { strength = s; pos = n.transform.position; source = n; }
        }
        return strength > 0f;
    }
}
