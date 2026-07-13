using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Вой — СТРАШНЫЙ звук хищника (Alt / правый шифтер), два кольца: БЛИЖНИЕ оглохли (стан ≥1с — окно
/// действий), ДАЛЬНИЕ в испуге разбегаются (страх; ярость не боится). Фича волчьей Пасти
/// (CreatureBody выставляет HowlEnabled). Урона нет. Активное УДЕРЖАНИЕ захвата воем не рвётся —
/// только пинок/рывок (но обхват ЗМЕИ вой рвёт на ст.1–2 — её собственный стан).
/// </summary>
public class PlayerHowl : MonoBehaviour, IAbility
{
    [Header("Вой")]
    [SerializeField] float radius = 7f;
    [SerializeField] float stunDuration = 1f;   // СТАН (контроль ≥1с): вырубает ближних на окно действий
    [SerializeField] float fearRadius = 14f;    // дальнее кольцо (radius..fearRadius): испуг — разбегаются
    [SerializeField] float fearDuration = 2.5f;
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
        hitThisHowl.Clear(); // призрака раскрывает ЗАДЕТЫЙ воем (стан через Hit.Apply / испуг ниже), не вой в пустоту
        var hit = new Hit(ownHealth, transform.position);
        Collider[] cols = Physics.OverlapSphere(transform.position, fearRadius, ~0, QueryTriggerInteraction.Ignore);
        foreach (var col in cols)
        {
            var hp = col.GetComponentInParent<Health>();
            if (hp == null || hp.transform == transform || !hitThisHowl.Add(hp)) continue;

            float d = Vector3.Distance(hp.transform.position, transform.position);
            if (d <= radius) hit.Apply(hp, HitEffect.Stun(stunDuration)); // ближние ОГЛОХЛИ
            else if (hp.TryGetComponent<WolfPsyche>(out var w))
            {
                w.Frighten(fearDuration); // дальние в ИСПУГЕ разбегаются (змея — холодный расчёт, не боится)
                Perception.BreakGhost();  // напугал — воздействие: призрак раскрыт
            }
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
