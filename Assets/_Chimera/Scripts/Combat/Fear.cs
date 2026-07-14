using UnityEngine;

/// <summary>
/// СТРАХ — накопительный статус-эффект (эталон — Venom). Величина копится от источников: гибель собрата рядом,
/// вой-испуг игрока, удар из невидимости; позже — чуждость-химеры (S4). Перевалила личный ПОРОГ ХРАБРОСТИ →
/// бегство (rout) на routDuration → величина сброшена → без новых источников затухает. Богаче длительностного:
/// стая ломается ВРАЗНОБОЙ (у каждого свой порог), храбрые держатся. ИММУНИТЕТ: холоднокровные (не боятся —
/// рационально отступают сами) и яростные (ярость перебивает страх). До-создаётся на цели при первом источнике.
/// Психика читает `IsRouting` (бежать); `SetThreshold` — личность; `Calm` — вой вожака гасит.
/// </summary>
public class Fear : MonoBehaviour
{
    [SerializeField] float decayPerSec = 0.5f;   // затухание накопленной величины без новых источников
    [SerializeField] float routDuration = 2.5f;  // сколько секунд бежит после срыва

    float magnitude, routUntil, threshold = 3f;
    Rage rage;
    ColdBlooded cold;

    void Awake() { TryGetComponent(out rage); TryGetComponent(out cold); }

    public bool IsRouting => Time.time < routUntil && !(rage != null && rage.IsEnraged); // ярость перебивает бегство
    public void SetThreshold(float t) => threshold = Mathf.Max(0.01f, t);                // личный порог храбрости (личность)
    public void Calm() { magnitude = 0f; routUntil = 0f; }                               // вой вожака гасит страх и бегство

    public void Add(float amount)
    {
        if (cold != null || (rage != null && rage.IsEnraged)) return; // холоднокровный/яростный не боится
        magnitude += amount;
        if (magnitude >= threshold) { routUntil = Time.time + routDuration; magnitude = 0f; }
    }

    void Update()
    {
        if (magnitude > 0f) magnitude = Mathf.Max(0f, magnitude - decayPerSec * Time.deltaTime);
    }
}
