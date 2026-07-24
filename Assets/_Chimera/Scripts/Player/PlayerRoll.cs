using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ПЕРЕКАТ ежиных ног (орган «Ежиные ноги», enablesRoll): рывок игрока идёт «в клубке» —
/// кого прокатил насквозь, тот РЕЖЕТСЯ (урон + `Bleed`), а сам ты неуязвим (i-frames рывка = броня клубка).
/// Третий профиль рывка после лосиного ТАРАНА и волчьей СКОРОСТИ (§3-бис спеки ежа): «иду сквозь
/// невредимым, и кто тронул — режется». Пассивный наездник на рывке PlayerController, как `PlayerCharge`.
///
/// Отличие от тарана — паёк ИГЛАМИ, а не копытами: кровотечение есть (иглы протыкают), а откидывание
/// слабое (катишься сквозь, не сносишь — «снося» это таран). Урон — раз на рывок (память чистится на новом).
///
/// КРОСС-СЛОТ СЕТ (§0-бис спеки, «дожд-ролл с иглами»): колючесть включает ТЕЛО только когда надеты
/// И «Ежиные ноги» (форма-кувырок), И «Иглы» в Шкуре (жало) — гейт в CreatureBody.Recompute
/// (`rollOn && thornsOn`). Одни ноги = защитный уворот без урона: этот компонент просто не активен,
/// а i-frames рывка живут в PlayerController и остаются. Числа переката — свои (иглы работают как гейт).
/// </summary>
public class PlayerRoll : MonoBehaviour
{
    [Header("Перекат (на рывке)")]
    [SerializeField] int damage = 12;
    [SerializeField] int bleedStacks = 1;  // иглы протыкают — кровь тому, кого прокатил
    [SerializeField] float force = 4f;      // лёгкий толчок вбок (катишься сквозь, не сносишь — это не таран)
    [SerializeField] float radius = 1.2f;   // ширина клубка
    [SerializeField] float reach = 0.9f;    // вынос центра вперёд
    [SerializeField] float shake = 0.18f;

    public bool RollEnabled { get; set; } // включается органом «Ежиные ноги»

    PlayerController move;
    Health ownHealth;
    CameraFollow cam;
    readonly HashSet<Health> hitThisDash = new();
    bool wasDashing;

    void Start()
    {
        move = GetComponent<PlayerController>();
        ownHealth = GetComponent<Health>();
        cam = FindAnyObjectByType<CameraFollow>();
    }

    void Update()
    {
        if (!RollEnabled || move == null) return;

        bool dashing = move.IsDashing;
        if (dashing && !wasDashing) hitThisDash.Clear(); // новый рывок — цель-память с нуля
        wasDashing = dashing;
        if (!dashing) return;

        Vector3 center = transform.position + transform.forward * reach + Vector3.up * 0.3f;
        var hit = new Hit(ownHealth, transform.position);
        var blow = new MeleeBlow { Damage = damage, KnockForce = force, BleedStacks = bleedStacks }; // паёк игл: урон + кровь + лёгкий толчок
        foreach (var hp in TargetScan.Healths(center, radius, transform))
        {
            if (!hitThisDash.Add(hp)) continue; // раз за рывок (память живёт весь дэш)
            blow.Deliver(hit, hp); // урон + кровотечение + толчок; эрозия по кину — внутри Hit.Apply
            if (cam != null) cam.Shake(0.12f, shake);
        }
    }

    void OnDrawGizmos()
    {
        if (!RollEnabled) return;
        Gizmos.color = TelegraphColors.Roll;
        Gizmos.DrawWireSphere(transform.position + transform.forward * reach + Vector3.up * 0.3f, radius);
    }
}
