using UnityEngine;

/// <summary>
/// СТРАХ — накопительный статус-эффект для ОДИНОЧЕК (эталон — Venom). СТАЙНЫЕ (волк) мигрировали на
/// шкалу `Morale` (страх↔ярость, стаки ±1×10с — спека 2026-07-17); Hit.Apply сам выбирает: есть Morale →
/// вклад шкалы, нет — этот компонент (будущие одиночные травоядные). Величина копится от источников,
/// перевалила личный ПОРОГ ХРАБРОСТИ → бегство (rout) → сброс → затухание. ИММУНИТЕТ: холоднокровные
/// (не боятся — рационально отступают сами) и яростные. До-создаётся на цели при первом источнике.
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
