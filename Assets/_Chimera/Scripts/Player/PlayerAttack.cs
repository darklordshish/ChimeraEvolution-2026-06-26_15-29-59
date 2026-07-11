using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Простой мечевой удар: по кнопке бьём сферой-хитбоксом перед игроком,
/// наносим урон всем найденным Health (каждому — один раз за замах).
/// При попадании — сочность: хитстоп + тряска камеры.
/// </summary>
public class PlayerAttack : MonoBehaviour, IAbility
{
    [Header("Удар")]
    [SerializeField] int damage = 10;
    [SerializeField] float range = 1.6f;   // как далеко вперёд центр хитбокса
    [SerializeField] float radius = 1.0f;  // радиус хитбокса
    [SerializeField] float cooldown = 0.45f;
    [SerializeField] LayerMask hitMask = ~0; // пока бьём по всему

    [Header("Сочность")]
    [SerializeField] float hitstopDuration = 0.06f;
    [SerializeField] float shakeMagnitude = 0.25f;

    float nextTime;
    readonly HashSet<Health> hitThisSwing = new();
    CameraFollow cam;
    Health ownHealth;
    int lifeSteal;

    void Start()
    {
        cam = FindAnyObjectByType<CameraFollow>();
        ownHealth = GetComponent<Health>();
    }

    // водитель (PlayerInputDriver) зовёт по вводу; кулдаун проверяем сами
    public bool TryUse()
    {
        if (Time.time < nextTime) return false;
        nextTime = Time.time + cooldown;
        DoAttack();
        return true;
    }

    void DoAttack()
    {
        hitThisSwing.Clear(); // призрака раскрывает ПОПАДАНИЕ (Hit.Apply), не замах — холостой взмах безопасен
        var hit = new Hit(ownHealth, transform.position);
        Collider[] hits = Physics.OverlapSphere(AttackCenter(), radius, hitMask, QueryTriggerInteraction.Ignore);
        foreach (var col in hits)
        {
            var hp = col.GetComponentInParent<Health>();
            if (hp == null || hp.transform == transform || !hitThisSwing.Add(hp)) continue;
            hit.Apply(hp, HitEffect.Damage(damage));
            if (lifeSteal > 0) hit.Apply(hp, HitEffect.LifeSteal(lifeSteal)); // вампиризм = сумма по целям (слот «Пасть»)
        }

        if (hitThisSwing.Count > 0) // попали хотя бы по одному — сочность раз за замах
        {
            if (hitstopDuration > 0f) Hitstop.Do(hitstopDuration); // 0 = выключить глобальный фриз
            if (cam != null) cam.Shake(0.12f, shakeMagnitude);
        }
    }

    // конструктор меняет параметры удара при смене органа в слоте «Руки»
    public void SetMelee(int newDamage, float newRange)
    {
        damage = newDamage;
        range = newRange;
    }

    // слот «Сердце»: скорость атак (кулдаун)
    public void SetCooldown(float newCooldown) => cooldown = newCooldown;

    // слот «Пасть»: вампиризм (лечение при попадании)
    public void SetLifeSteal(int v) => lifeSteal = v;

    Vector3 AttackCenter() => transform.position + transform.forward * range + Vector3.up * 0.5f;

    // красная сфера — зона удара. Всегда видна; включи тумблер Gizmos в Game view, чтобы видеть и в игре
    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(AttackCenter(), radius);
    }
}
