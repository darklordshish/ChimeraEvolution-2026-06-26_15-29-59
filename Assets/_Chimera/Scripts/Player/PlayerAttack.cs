using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Простой мечевой удар: по кнопке бьём сферой-хитбоксом перед игроком,
/// наносим урон всем найденным Health (каждому — один раз за замах).
/// </summary>
public class PlayerAttack : MonoBehaviour
{
    [Header("Удар")]
    [SerializeField] int damage = 10;
    [SerializeField] float range = 1.6f;   // как далеко вперёд центр хитбокса
    [SerializeField] float radius = 1.0f;  // радиус хитбокса
    [SerializeField] float cooldown = 0.45f;
    [SerializeField] LayerMask hitMask = ~0; // пока бьём по всему

    InputAction attackAction;
    float nextTime;
    readonly HashSet<Health> hitThisSwing = new();

    void Awake()
    {
        // ЛКМ / X на геймпаде / J на клавиатуре
        attackAction = new InputAction("Attack", InputActionType.Button);
        attackAction.AddBinding("<Mouse>/leftButton");
        attackAction.AddBinding("<Gamepad>/buttonWest");
        attackAction.AddBinding("<Keyboard>/j");
    }

    void OnEnable() => attackAction.Enable();
    void OnDisable() => attackAction.Disable();

    void Update()
    {
        if (attackAction.WasPressedThisFrame() && Time.time >= nextTime)
        {
            nextTime = Time.time + cooldown;
            DoAttack();
        }
    }

    void DoAttack()
    {
        hitThisSwing.Clear();
        Collider[] hits = Physics.OverlapSphere(AttackCenter(), radius, hitMask, QueryTriggerInteraction.Ignore);
        foreach (var col in hits)
        {
            var hp = col.GetComponentInParent<Health>();
            if (hp != null && hp.transform != transform && hitThisSwing.Add(hp))
                hp.TakeDamage(damage);
        }
    }

    Vector3 AttackCenter() => transform.position + transform.forward * range + Vector3.up * 0.5f;

    // красная сфера в сцене, когда объект выделен — видно дальность удара
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(AttackCenter(), radius);
    }
}
