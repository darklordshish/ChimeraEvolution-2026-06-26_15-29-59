using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Вой — АоЕ микро-стан вокруг игрока (Alt / правый шифтер). Фича волчьей Пасти
/// (CreatureBody выставляет HowlEnabled): с полным волчьим лоадаутом играешь управляемого вервольфа.
/// Урона нет — чистый контроль: прерывает замахи стаи, даёт окно. Активное УДЕРЖАНИЕ захвата
/// воем не рвётся (как и ударом) — только пинок/рывок.
/// </summary>
public class PlayerHowl : MonoBehaviour, IAbility
{
    [Header("Вой")]
    [SerializeField] float radius = 7f;
    [SerializeField] float stunDuration = 1f;   // СТАН (контроль ≥1с): вырубает стаю на окно действий
    [SerializeField] float cooldown = 8f;
    [SerializeField] float shake = 0.3f;

    public bool HowlEnabled { get; set; } // включается волчьей Пастью (CreatureBody)

    float nextTime;
    CameraFollow cam;
    Health ownHealth;
    readonly HashSet<Health> hitThisHowl = new();

    void Start()
    {
        cam = FindAnyObjectByType<CameraFollow>();
        ownHealth = GetComponent<Health>();
    }

    // водитель зовёт по вводу; активен только с волчьей Пастью; кулдаун проверяем сами
    public bool TryUse()
    {
        if (!HowlEnabled || Time.time < nextTime) return false;
        nextTime = Time.time + cooldown;
        DoHowl();
        return true;
    }

    void DoHowl()
    {
        Perception.BreakGhost(); // dev-призрак: вой раскрывает
        hitThisHowl.Clear();
        var hit = new Hit(ownHealth, transform.position);
        Collider[] cols = Physics.OverlapSphere(transform.position, radius, ~0, QueryTriggerInteraction.Ignore);
        foreach (var col in cols)
        {
            var hp = col.GetComponentInParent<Health>();
            if (hp == null || hp.transform == transform || !hitThisHowl.Add(hp)) continue;
            hit.Apply(hp, HitEffect.Stun(stunDuration));
        }
        if (cam != null) cam.Shake(0.15f, shake); // визуальный сигнал воя (VFX/звук — потом)
    }

    void OnDrawGizmos()
    {
        if (!HowlEnabled) return;
        Gizmos.color = new Color(0.6f, 0.5f, 1f); // цвет воя из легенды
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
