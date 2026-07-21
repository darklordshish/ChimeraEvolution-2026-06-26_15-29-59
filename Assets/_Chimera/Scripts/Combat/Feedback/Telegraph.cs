using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Телеграф приёма: красит материалы ТЕЛА в цвет замаха и возвращает исходные.
/// Per-renderer _BaseColor через MaterialPropertyBlock (без инстансинга материалов).
/// Красит только Mesh/SkinnedMesh — TrailRenderer (запаховый след) не трогает.
/// Извлечено из дублей WolfPsyche/WerewolfPsyche (Фаза 0 рефактора существ).
/// </summary>
public class Telegraph : MonoBehaviour
{
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    Renderer[] renderers;
    Color[] baseColors;
    bool[] headPart;     // ГОЛОВНЫЕ части (по конвенции имён): эмоц-рест красит ТОЛЬКО их — «лицо» существа
    MaterialPropertyBlock mpb;
    bool active;
    bool activeIntent;   // текущая покраска = ЗАМАХ (намерение), а не статус → её распознаёт лишь Чутьё
    bool isPlayer;       // свои приёмы носитель знает без всякого органа — телеграф игрока не обезличиваем
    Color activeColor;   // цель: плоский цвет ИЛИ target градиента
    float activeT = -1f; // ≥0 → градиент по t (рест→target); <0 → плоский цвет
    Color restColor;     // ЭМОЦ-РЕСТ-СЛОЙ (только голова): «морда налилась кровью / побелела от страха»;
    float restT;         // 0 = чистый натуральный. Вспышки приёмов — ВСЕМ телом поверх, откат К РЕСТУ

    // конвенция имён головных частей (генераторы моделек + ручные кубы игрока следуют ей)
    static bool IsHeadName(string n) => n == "Head" || n == "Muzzle" || n == "Nose" || n == "EarL" || n == "EarR";

    void Awake()
    {
        isPlayer = GetComponent<PlayerController>() != null;

        var list = new List<Renderer>();
        foreach (var r in GetComponentsInChildren<Renderer>())
            if (r is MeshRenderer || r is SkinnedMeshRenderer) list.Add(r); // не красим след/линии
        renderers = list.ToArray();

        baseColors = new Color[renderers.Length];
        headPart = new bool[renderers.Length];
        mpb = new MaterialPropertyBlock();
        for (int i = 0; i < renderers.Length; i++)
        {
            var m = renderers[i].sharedMaterial;
            baseColors[i] = (m != null && m.HasProperty(BaseColor)) ? m.GetColor(BaseColor) : Color.gray;
            headPart[i] = IsHeadName(renderers[i].name);
        }
    }

    /// <summary>Плоский цвет телеграфа (вкл/выкл). `intent` — это ЗАМАХ ПРИЁМА, а не статус: такую покраску
    /// игрок без человеческого Чутья видит нерасспознанной («что-то готовит»), см. `Apply`.
    /// Статусы (стан, эмоции) метить намерением НЕ надо — они факты и видны всегда.</summary>
    public void Set(bool on, Color color, bool intent = false)
    {
        active = on; activeColor = color; activeT = -1f; activeIntent = intent;
        Apply();
    }

    /// <summary>Градиент телеграфа: каждый рендерер лерпит от СВОЕГО родного цвета к target по t (0=родной … 1=полный).
    /// Для СТАДИЙНЫХ приёмов (обхват): нарастание читается как переход родной→цвет-приёма, а не три разных цвета.</summary>
    public void SetGradient(Color target, float t, bool intent = true)
    {
        active = true; activeColor = target; activeT = Mathf.Clamp01(t); // IsShowing=true — раскрытие для камуфляжа
        activeIntent = intent;
        Apply();
    }

    /// <summary>РЕСТ-слой (эмоции) — красит ТОЛЬКО ГОЛОВУ (идея пользователя: тело = приёмы, голова = эмоции —
    /// каналы разнесены пространственно, эмоция читается даже в гуще боевых вспышек).
    /// Приоритет-стек: стан/вспышка приёма — всем телом поверх, откат к ресту (голова сохраняет эмоцию).</summary>
    public void SetRest(Color target, float t)
    {
        restColor = target; restT = Mathf.Clamp01(t);
        Apply(); // активная вспышка не затрётся: Apply уважает active
    }

    /// <summary>Восстановить текущее состояние (телеграф/рест) — после того как HitFlash перебил вспышкой.</summary>
    public void Reapply() => Apply();

    public void Clear() { active = false; Apply(); }

    // насколько тело СВЕТЛЕЕТ, когда намерение не распознано. Не отдельный цвет (чужеродная метка поверх
    // существа), а осветление ЕГО СОБСТВЕННОГО — «зверь подобрался», но чем именно замахнулся, не разобрать
    const float UnknownLift = 0.45f;

    // применить текущее состояние: выкл → рест (эмоция НА ГОЛОВЕ, тело натуральное); градиент → лерп от реста; плоский → цвет
    void Apply()
    {
        // РАСПОЗНАВАНИЕ НАМЕРЕНИЯ — фича человеческого Чутья: без него замах виден (окно реакции то же),
        // но безымянным. С Чутьём проступает цвет приёма: укус, захват, таран, вой.
        // Гейт только на НАМЕРЕНИИ: стан и эмоции — факты, у них свои каналы, их не обезличиваем
        bool veiled = activeIntent && !Perception.Insight && !isPlayer;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            renderers[i].GetPropertyBlock(mpb);
            Color rest = restT > 0f && headPart[i] ? Color.Lerp(baseColors[i], restColor, restT) : baseColors[i];
            // нераспознанное — светлеем ОТ СВОЕГО цвета (per-renderer): волк остаётся волком, просто «зажёгся»
            Color target = veiled ? Color.Lerp(rest, Color.white, UnknownLift) : activeColor;
            Color c = !active ? rest : activeT >= 0f ? Color.Lerp(rest, target, activeT) : target;
            mpb.SetColor(BaseColor, c);
            renderers[i].SetPropertyBlock(mpb);
        }
    }

    public bool IsShowing => active; // «существо сейчас что-то телеграфирует» = раскрыто (сигнал для камуфляжа)
}
