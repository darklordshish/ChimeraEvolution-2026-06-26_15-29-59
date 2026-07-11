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
    Stagger stagger;
    Renderer[] bodyRenderers;
    bool hidden;
    float revealUntil;

    public bool Hidden => hidden; // психика змеи читает: погремушка видима вместе с телом (плюс мигание гремка)

    // раскрыть на seconds (психика зовёт на время боя — чтобы камуфляж не мигал в паузах между ударами)
    public void Reveal(float seconds) => revealUntil = Mathf.Max(revealUntil, Time.time + seconds);

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        TryGetComponent(out telegraph);
        TryGetComponent(out stagger);
        var list = new List<Renderer>();
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            if (!(r is MeshRenderer || r is SkinnedMeshRenderer)) continue; // след/линии не прячем — запах = зацепка
            string n = r.gameObject.name;
            // каналы чувств НЕ трогаем — ими рулят свои системы: термо-контур (HeatGhost, сквозь стены!)
            // гаснет сам по холоднокровности, аура запаха (Sphere) — по нюху, ПОГРЕМУШКА (RattleMesh) —
            // психикой змеи (гремок мигает ей даже у невидимой). Камуфляж прячет только ТЕЛО.
            if (n == "HeatGhost" || n == "Sphere" || n == "RattleMesh") continue;
            list.Add(r);
        }
        bodyRenderers = list.ToArray();
    }

    void OnDisable() => SetHidden(false); // сняли компонент/выключили — вернуть видимость

    void Update()
    {
        Vector3 v = controller.velocity; v.y = 0f;
        bool revealed = (telegraph != null && telegraph.IsShowing) || Time.time < revealUntil
                        || (stagger != null && stagger.IsStaggered); // телеграф / память боя / боль (стаггер, обхват) = раскрыт
        SetHidden(v.sqrMagnitude < moveThreshold * moveThreshold && !revealed);
    }

    void SetHidden(bool h)
    {
        if (h == hidden) return;
        hidden = h;
        foreach (var r in bodyRenderers) if (r != null) r.enabled = !h;
    }
}
