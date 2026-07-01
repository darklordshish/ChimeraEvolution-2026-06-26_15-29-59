using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Телеграф приёма: красит материалы ТЕЛА в цвет замаха и возвращает исходные.
/// Per-renderer _BaseColor через MaterialPropertyBlock (без инстансинга материалов).
/// Красит только Mesh/SkinnedMesh — TrailRenderer (запаховый след) не трогает.
/// Извлечено из дублей WolfAI/WerewolfBoss (Фаза 0 рефактора существ).
/// </summary>
public class Telegraph : MonoBehaviour
{
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    Renderer[] renderers;
    Color[] baseColors;
    MaterialPropertyBlock mpb;
    bool active;
    Color activeColor;

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

    /// <summary>Включить/выключить телеграф заданного цвета. Идемпотентно — лишней работы нет.</summary>
    public void Set(bool on, Color color)
    {
        if (on == active && (!on || color == activeColor)) return;
        active = on;
        activeColor = color;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            renderers[i].GetPropertyBlock(mpb);
            mpb.SetColor(BaseColor, on ? color : baseColors[i]);
            renderers[i].SetPropertyBlock(mpb);
        }
    }

    public void Clear() => Set(false, activeColor);
}
