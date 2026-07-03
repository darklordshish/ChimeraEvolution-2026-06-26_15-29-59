using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// КАМУФЛЯЖ-В-НЕПОДВИЖНОСТИ (Чешуя змеи): прячет меши ТЕЛА, пока существо почти не движется
/// И не «раскрыто» — телеграф погашен (не в замахе/приёме/захвате/гремке). Двинулось или пошло
/// в приём → видно. След (TrailRenderer) НЕ прячем: запах остаётся зацепкой для нюха (RPS).
/// Вешает CreatureBody по флагу Шкуры. Симметрия: игрок со змеиной кожей тоже растворяется на месте.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class Camouflage : MonoBehaviour
{
    [SerializeField] float moveThreshold = 0.6f; // ниже этой горизонтальной скорости считаемся «неподвижным»

    CharacterController controller;
    Telegraph telegraph;
    Renderer[] bodyRenderers;
    bool hidden;
    float revealUntil;

    // раскрыть на seconds (психика зовёт на время боя — чтобы камуфляж не мигал в паузах между ударами)
    public void Reveal(float seconds) => revealUntil = Mathf.Max(revealUntil, Time.time + seconds);

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        TryGetComponent(out telegraph);
        var list = new List<Renderer>();
        foreach (var r in GetComponentsInChildren<Renderer>())
            if (r is MeshRenderer || r is SkinnedMeshRenderer) list.Add(r); // след/линии не прячем — запах = зацепка
        bodyRenderers = list.ToArray();
    }

    void OnDisable() => SetHidden(false); // сняли компонент/выключили — вернуть видимость

    void Update()
    {
        Vector3 v = controller.velocity; v.y = 0f;
        bool revealed = (telegraph != null && telegraph.IsShowing) || Time.time < revealUntil; // телеграф ИЛИ память боя = раскрыт
        SetHidden(v.sqrMagnitude < moveThreshold * moveThreshold && !revealed);
    }

    void SetHidden(bool h)
    {
        if (h == hidden) return;
        hidden = h;
        foreach (var r in bodyRenderers) if (r != null) r.enabled = !h;
    }
}
