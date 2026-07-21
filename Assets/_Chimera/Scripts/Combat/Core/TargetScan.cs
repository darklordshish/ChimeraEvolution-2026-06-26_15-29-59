using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ОБЩИЙ СКАН ЦЕЛЕЙ (C1-аудит: обвязка «сфера → Health по родителю коллайдера → без себя, без дублей»
/// повторялась в каждом приёме). Возвращает ПЕРЕИСПОЛЬЗУЕМЫЙ список — читать сразу, не кэшировать
/// (следующий вызов его перепишет; ноль аллокаций на скан, кроме самих коллайдеров Physics).
/// </summary>
public static class TargetScan
{
    static readonly List<Health> found = new();

    /// <summary>Все живые цели в сфере: Health каждого — один раз, себя (self) не считаем.</summary>
    public static List<Health> Healths(Vector3 center, float radius, Transform self)
    {
        found.Clear();
        foreach (var col in Physics.OverlapSphere(center, radius, ~0, QueryTriggerInteraction.Ignore))
        {
            var hp = col.GetComponentInParent<Health>();
            if (hp == null || hp.transform == self || found.Contains(hp)) continue; // Contains: целей единицы, O(n²) не страшен
            found.Add(hp);
        }
        return found;
    }
}
