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
    MaterialPropertyBlock mpb;
    bool active;
    Color activeColor;   // цель: плоский цвет ИЛИ target градиента
    float activeT = -1f; // ≥0 → градиент по t (родной→target); <0 → плоский цвет

    void Awake()
    {
        var list = new List<Renderer>();
        foreach (var r in GetComponentsInChildren<Renderer>())
            if (r is MeshRenderer || r is SkinnedMeshRenderer) list.Add(r); // не красим след/линии
        renderers = list.ToArray();

        baseColors = new Color[renderers.Length];
        mpb = new MaterialPropertyBlock();
        for (int i = 0; i < renderers.Length; i++)
        {
            var m = renderers[i].sharedMaterial;
            baseColors[i] = (m != null && m.HasProperty(BaseColor)) ? m.GetColor(BaseColor) : Color.gray;
        }
    }

    /// <summary>Плоский цвет телеграфа (вкл/выкл).</summary>
    public void Set(bool on, Color color)
    {
        active = on; activeColor = color; activeT = -1f;
        Apply();
    }

    /// <summary>Градиент телеграфа: каждый рендерер лерпит от СВОЕГО родного цвета к target по t (0=родной … 1=полный).
    /// Для СТАДИЙНЫХ приёмов (обхват): нарастание читается как переход родной→цвет-приёма, а не три разных цвета.</summary>
    public void SetGradient(Color target, float t)
    {
        active = true; activeColor = target; activeT = Mathf.Clamp01(t); // IsShowing=true — раскрытие для камуфляжа
        Apply();
    }

    /// <summary>Восстановить текущий телеграф — после того как HitFlash перебил вспышкой (иначе откат к родному съел бы телеграф).</summary>
    public void Reapply() { if (active) Apply(); }

    public void Clear() { active = false; Apply(); }

    // применить текущее состояние: выкл → родной цвет рендерера; градиент → лерп; плоский → цвет
    void Apply()
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            renderers[i].GetPropertyBlock(mpb);
            Color c = !active ? baseColors[i] : activeT >= 0f ? Color.Lerp(baseColors[i], activeColor, activeT) : activeColor;
            mpb.SetColor(BaseColor, c);
            renderers[i].SetPropertyBlock(mpb);
        }
    }

    public bool IsShowing => active; // «существо сейчас что-то телеграфирует» = раскрыто (сигнал для камуфляжа)
}
