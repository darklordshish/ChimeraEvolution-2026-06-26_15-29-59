using System.Collections.Generic;
using UnityEngine;

/// <summary>РАЗМЕР ТЕЛА (спеки `2026-10-20-shkala-morfov.md`, `2026-10-20-razmer-hedzhhalka.md`): масштаб = масштаб вида ×
/// рост химеры по массе (`GlobalLayer.Growth`). Как делают с вариантами размера: масштабируется ВИЗУАЛ целиком (контейнер
/// морфа, его начало — на земле, у низа капсулы), а корень с физикой остаётся 1 — `CharacterController` меряет в мировых
/// метрах, и унаследованный масштаб ломал бы его. Капсулу пересчитываем тем же множителем, НИЗ остаётся на месте.
/// Форма в инструментах сверки (витрина, силуэты, матрица) — в масштабе шасси: размер применяет только игра и `GameSizes`.
/// Дальности и скорости приёмов размер не трогает — это баланс, они в органах.</summary>
[DisallowMultipleComponent]
public class BodySize : MonoBehaviour
{
    public const string Morph = "Morph";   // контейнер сборки `MorphBuilder` (его имя — контракт сборки)

    public float Scale { get; private set; } = 1f;

    bool cached;
    float h0, r0, step0;
    Vector3 c0;

    /// <summary>Масштаб тела по составу: масштаб вида шасси × рост по массе.</summary>
    public static float Of(SpeciesSO chassis, IReadOnlyList<Organ> worn) =>
        chassis == null ? 1f : chassis.FormScale * GlobalLayer.Growth(chassis, worn);

    /// <summary>Поставить размер собранному телу: контейнер морфа — равномерно от его начала (земли), капсула — от низа.</summary>
    public void Apply(float s)
    {
        if (!(s > 0f)) s = 1f;
        var cc = GetComponent<CharacterController>();
        if (!cached && cc != null) { h0 = cc.height; r0 = cc.radius; c0 = cc.center; step0 = cc.stepOffset; cached = true; }
        var morph = transform.Find(Morph);
        if (morph != null) morph.localScale = Vector3.one * s;   // РАВНОМЕРНО: неравномерный масштаб родителя плющит детали
        if (cc != null && cached)
        {
            float bottom = c0.y - h0 * 0.5f;
            cc.radius = r0 * s;
            cc.height = h0 * s;
            cc.center = new Vector3(c0.x * s, bottom + h0 * s * 0.5f, c0.z * s);
            cc.stepOffset = Mathf.Min(step0 * Mathf.Min(1f, s), cc.height);   // ступенька не выше роста; большим — прежняя
        }
        Scale = s;
    }

    /// <summary>Высота над корнем, заданная для тела масштаба 1, — для тела этого размера (от земли, у низа капсулы).</summary>
    public float FromGround(float y)
    {
        if (!cached) return y;
        float bottom = c0.y - h0 * 0.5f;
        return bottom + (y - bottom) * Scale;
    }
}
