using UnityEngine;

/// <summary>«Где пол под ногами» — подключаемый поставщик для защиты игрока от провала (`PlayerController`).
/// Лес может подставить поле высот вместо луча: <see cref="GroundProbe.Provider"/> или компонент на игроке.</summary>
public interface IGroundProbe
{
    /// <summary>Самая высокая стоячая поверхность под точкой <paramref name="from"/> (она сама уже чуть выше опоры).
    /// false — пола нет вовсе (дыра в мир).</summary>
    bool TryFloor(Vector3 from, out float y);
}

/// <summary>Точка регистрации поставщика пола. null — берётся компонент на игроке, затем луч по умолчанию.</summary>
public static class GroundProbe
{
    public static IGroundProbe Provider;
}

/// <summary>Поставщик по умолчанию: луч вниз до <see cref="Range"/> м. Пропускает свою капсулу, живое (`Health`),
/// триггеры и всё, у чего нормаль не смотрит вверх (стены, стволы: normal.y &lt; 0,5).</summary>
public class RayGroundProbe : IGroundProbe
{
    public const float DefaultRange = 250f;
    readonly Transform owner;
    public float Range = DefaultRange;

    public RayGroundProbe(Transform owner) { this.owner = owner; }

    public bool TryFloor(Vector3 from, out float y)
    {
        y = 0f;
        int mask = ~LayerMask.GetMask("Ignore Raycast");
        var hits = Physics.RaycastAll(from, Vector3.down, Range, mask, QueryTriggerInteraction.Ignore);
        bool found = false;
        float best = float.NegativeInfinity;
        for (int i = 0; i < hits.Length; i++)
        {
            var col = hits[i].collider;
            if (col == null || col.isTrigger) continue;
            if (owner != null && (col.transform == owner || col.transform.IsChildOf(owner))) continue;
            if (col.GetComponentInParent<Health>() != null) continue;
            if (hits[i].normal.y < 0.5f) continue;
            if (hits[i].point.y > best) { best = hits[i].point.y; found = true; }
        }
        if (found) y = best;
        return found;
    }
}
