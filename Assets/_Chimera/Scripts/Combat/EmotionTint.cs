using UnityEngine;

/// <summary>
/// ЭМОЦ-ИНДИКАЦИЯ (лось, срез C; идеи пользователя): моральное состояние красит ГОЛОВУ существа, пока
/// действует — через головной рест-слой Telegraph. Пространственное разделение каналов: ТЕЛО = приёмы
/// (вспышки телеграфа), ГОЛОВА = эмоции — «морда налилась кровью / побелела» читается даже в гуще боя.
/// ЯРОСТЬ (Rage.IsEnraged) — бордовая морда, ПАНИКА (Fear.IsRouting) — синяя.
/// SetMood — ручка психики для ГРАДИЕНТНЫХ состояний (лесенка предупреждений лося: натуральный→ярость
/// по ступеням); статус-эмоции сильнее mood. Вешает ТЕЛО всем: у холоднокровных Rage/Fear не заводятся —
/// эмоций нет, тинт молчит сам (та же фильтрация, что везде). Плейсхолдер под боди-ленгвидж (риги).
/// </summary>
public class EmotionTint : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] float statusStrength = 0.9f; // сила статус-эмоции: голова красится почти в чистый цвет (лицо — сигнал)

    Telegraph telegraph;
    Rage rage;
    Fear fear;
    Morale moraleRef; // стайные: паника — по шкале морали
    Color moodColor;  // градиентное «настроение» от психики (лесенка лося)
    float moodT;
    Color lastColor;
    float lastT = -1f;

    /// <summary>Психика задаёт ГРАДИЕНТНОЕ настроение (лесенка: t растёт по ступеням). Статус-эмоции сильнее.</summary>
    public void SetMood(Color color, float t) { moodColor = color; moodT = Mathf.Clamp01(t); }

    void Update()
    {
        // статусы могут до-создаться позже (Fear — при первом источнике) — ленивая привязка
        if (telegraph == null && !TryGetComponent(out telegraph)) return;
        if (rage == null) TryGetComponent(out rage);
        if (fear == null) TryGetComponent(out fear);
        if (moraleRef == null) TryGetComponent(out moraleRef);

        Color c; float t;
        if (moraleRef != null)
        {
            // СТАЙНЫЙ: морда = живой градусник ШКАЛЫ МОРАЛИ (идея пользователя): −кап чисто-синяя ↔
            // 0 натуральная ↔ +кап чисто-бордовая. Статус-оверрайды не нужны — дух виден напрямую
            float n = moraleRef.Normalized;
            c = n >= 0f ? TelegraphColors.RageTint : TelegraphColors.FearTint;
            t = Mathf.Abs(n) * statusStrength;
        }
        else if (rage != null && rage.IsEnraged) { c = TelegraphColors.RageTint; t = statusStrength; } // берсерк/вожак
        else if (fear != null && fear.IsRouting) { c = TelegraphColors.FearTint; t = statusStrength; } // одиночки
        else { c = moodColor; t = moodT; } // градиент-настроение психики (лесенка лося)

        if (t == lastT && c == lastColor) return; // рест трогаем только на изменение
        lastColor = c; lastT = t;
        telegraph.SetRest(c, t);
    }
}
