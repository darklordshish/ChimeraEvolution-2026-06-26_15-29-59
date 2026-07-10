using UnityEngine;

/// <summary>
/// Роняет точки запаха в ScentField через равные интервалы — это след, по которому враги тропят.
/// Вешается на игрока (он — добыча). Визуал следа — отдельно (ScentTrail).
/// </summary>
public class ScentEmitter : MonoBehaviour
{
    [SerializeField] float dropInterval = 0.4f;

    float nextDrop;

    void Update()
    {
        if (Perception.PlayerGhost) return; // dev-призрак не пахнет
        if (Time.time < nextDrop) return;
        nextDrop = Time.time + dropInterval;
        ScentField.Instance.Drop(transform.position);
    }
}
