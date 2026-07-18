using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ТАРАН лосиных ног (орган «Лосиные ноги», enablesCharge): рывок игрока становится ГОРЯЩИМ —
/// врезаешься в кого проходишь → урон + Knockback (резист Massive внутри). Пассивный наездник на
/// рывке PlayerController (не отдельная кнопка): «основа боя лося — копыта», локомоция от данных.
/// Урон — раз на рывок (память целей чистится на новом рывке). Кровотечения НЕТ (таран тупой; кровь — рога).
/// </summary>
public class PlayerCharge : MonoBehaviour
{
    [Header("Таран (на рывке)")]
    [SerializeField] int damage = 22;
    [SerializeField] float force = 13f;   // отлёт (Massive резистит)
    [SerializeField] float radius = 1.3f; // ширина тарана
    [SerializeField] float reach = 1.0f;  // вынос центра вперёд
    [SerializeField] float shake = 0.3f;

    public bool ChargeEnabled { get; set; } // включается органом «Лосиные ноги»

    PlayerController move;
    Health ownHealth;
    CameraFollow cam;
    readonly HashSet<Health> goredThisDash = new();
    bool wasDashing;

    void Start()
    {
        move = GetComponent<PlayerController>();
        ownHealth = GetComponent<Health>();
        cam = FindAnyObjectByType<CameraFollow>();
    }

    void Update()
    {
        if (!ChargeEnabled || move == null) return;

        bool dashing = move.IsDashing;
        if (dashing && !wasDashing) goredThisDash.Clear(); // новый рывок — цель-память с нуля
        wasDashing = dashing;
        if (!dashing) return;

        Vector3 center = transform.position + transform.forward * reach + Vector3.up * 0.5f;
        var hit = new Hit(ownHealth, transform.position);
        foreach (var col in Physics.OverlapSphere(center, radius, ~0, QueryTriggerInteraction.Ignore))
        {
            var hp = col.GetComponentInParent<Health>();
            if (hp == null || hp.transform == transform || !goredThisDash.Add(hp)) continue;
            hit.Apply(hp, HitEffect.Damage(damage));   // вспышка + стаггер
            hit.Apply(hp, HitEffect.Knockback(force)); // сносим с дороги
            if (cam != null) cam.Shake(0.14f, shake);
        }
    }

    void OnDrawGizmos()
    {
        if (!ChargeEnabled) return;
        Gizmos.color = TelegraphColors.Charge;
        Gizmos.DrawWireSphere(transform.position + transform.forward * reach + Vector3.up * 0.5f, radius);
    }
}
