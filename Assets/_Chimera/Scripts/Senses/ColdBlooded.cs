using UnityEngine;

/// <summary>
/// ХОЛОДНОКРОВНОСТЬ — маркер: существо не излучает тепло, поэтому НЕВИДИМО для термозрения
/// (`Perception.SeesThermal`). Даёт орган Сердце (флаг `Organ.coldBlooded`); `CreatureBody` вешает/снимает.
/// Присутствие компонента = холоднокровен. Симметрия: украл змеиное сердце → сам невидим для термо-врагов.
/// </summary>
public class ColdBlooded : MonoBehaviour { }
