using UnityEngine;

/// <summary>
/// КЛУБОК ежа (слайс C) — оборонительная стойка последнего рубежа: свернулся → БРОНЯ↑, залп молчит,
/// и это ЖЖЁТ СТАМИНУ (держать шар — усилие, `Stamina.Drain` — нагрузка, не рывок). Кончилась дыхалка →
/// разворачивается (окно «на спине» приедет слайсом C3; пока просто встаёт).
///
/// Клубок — про БРОНЮ и ЦЕНУ, не про ответку: иглы (`Thorns`) и так всегда наказывают удар в упор,
/// клубок их НЕ дублирует. Грабо-иммунитета намеренно НЕТ (решение пользователя): урон от самой жертвы
/// расшатывает `Constrict`-хват, а иглы возвращают урон+кровь — кто вцепился в ежа, гибнет сам.
///
/// РЕЖИМ, не приём: психика (лестница отчаяния, слайс D) решает КОГДА свернуться; машина держит МЕХАНИКУ.
/// </summary>
[RequireComponent(typeof(Health))]
public class CurlDefense : MonoBehaviour
{
    [SerializeField, Range(0f, 0.9f)] float curlArmor = 0.6f; // броня в клубке (берём МАКСИМУМ с базовой, не суммируем)
    [SerializeField] float staminaDrain = 14f;                // жжём бак в секунду, пока свёрнуты

    public bool Curled { get; private set; }

    Health health;
    Stamina stamina;
    Telegraph telegraph;
    float baseArmor;

    void Awake()
    {
        health = GetComponent<Health>();
        TryGetComponent(out stamina);
        TryGetComponent(out telegraph);
    }

    /// <summary>Свернуться: поднять броню (не ниже базовой), зажечь телеграф клубка. Идемпотентно.</summary>
    public void Curl()
    {
        if (Curled) return;
        Curled = true;
        baseArmor = health.DamageReduction;
        health.DamageReduction = Mathf.Max(baseArmor, curlArmor);
        if (telegraph != null) telegraph.Set(true, TelegraphColors.Curl); // факт-статус (не намерение) — виден всем без Чутья
    }

    /// <summary>Развернуться: вернуть базовую броню и рест-вид. Идемпотентно.</summary>
    public void Uncurl()
    {
        if (!Curled) return;
        Curled = false;
        health.DamageReduction = baseArmor;
        if (telegraph != null) telegraph.Set(false, TelegraphColors.Curl);
    }

    /// <summary>Психика зовёт каждый кадр, пока держит клубок: жжём стамину. Выдохся → разворот (C3 заменит
    /// на падение «на спину»). Возвращает false, когда бак сух и клубок сам распустился — сигнал психике.</summary>
    public bool Hold()
    {
        if (!Curled) return false;
        if (stamina != null)
        {
            stamina.Drain(staminaDrain * Time.deltaTime);
            if (stamina.Exhausted) { Uncurl(); return false; }
        }
        return true;
    }
}
