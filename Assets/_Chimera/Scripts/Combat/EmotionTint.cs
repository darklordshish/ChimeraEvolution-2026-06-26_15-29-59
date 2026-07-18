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

        // морда = «настроение»: СИЛЬНЕЙШЕЕ из живого ГРАДУСНИКА ШКАЛЫ МОРАЛИ (−кап синяя … 0 натур … +кап
        // бордовая) и MOOD-градиента психики (лесенка лося). Так у волка ведёт градусник, у лося (морали ~0)
        // — лесенка; когда рёв/вой качнёт мораль лося, синева/бордо перебьёт mood, если сильнее.
        float mn = moraleRef != null ? moraleRef.Normalized : 0f;
        float moraleT = Mathf.Abs(mn) * statusStrength;

        Color c; float t;
        if (moraleT >= moodT) { c = mn >= 0f ? TelegraphColors.RageTint : TelegraphColors.FearTint; t = moraleT; }
        else { c = moodColor; t = moodT; }

        // БЕРСЕРК/вечная ярость → полный красный, но ТОЛЬКО когда своего градусника нет (лось-берсерк,
        // вервольф): у волков шкала ведёт сама — не сбиваем плавность в полный
        if (rage != null && rage.IsEnraged && moraleT < 0.05f) { c = TelegraphColors.RageTint; t = statusStrength; }
        // одиночки БЕЗ шкалы морали (будущие) — словарный Fear
        else if (moraleRef == null && fear != null && fear.IsRouting) { c = TelegraphColors.FearTint; t = statusStrength; }

        if (t == lastT && c == lastColor) return; // рест трогаем только на изменение
        lastColor = c; lastT = t;
        telegraph.SetRest(c, t);
    }
}
