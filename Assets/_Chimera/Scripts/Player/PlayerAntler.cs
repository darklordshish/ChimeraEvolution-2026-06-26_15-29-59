using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// РОГА игрока (орган «Рога» лося в химерном слоте — придаток, как змеиный хвост-обхват): фронтальный
/// удар-протыкание перед игроком — урон + Knockback немассивных (резист Massive внутри) + КРОВОТЕЧЕНИЕ (стаки).
/// Спека лося: «рога — откидывание немассивных + Bleed». Диспатчится на E вместе с пинком («пинок повесим
/// на откидывание рогами»): человечьи ноги — пинок, рога — свайп; есть оба — бьют оба. Включается органом.
/// </summary>
public class PlayerAntler : MonoBehaviour, IAbility
{
    [Header("Рога")]
    [SerializeField] int damage = 12;
    [SerializeField] float range = 1.9f;   // вынос центра вперёд (длиннее пинка — рога тянутся)
    [SerializeField] float radius = 1.3f;  // уже пинка — фронтальный тычок, не широкий клин
    [SerializeField] float force = 9f;     // отлёт (Massive резистит)
    [SerializeField] int bleedStacks = 2;  // протыкание — кровь стаками
    [SerializeField] float cooldown = 1.2f;
    [SerializeField] float shake = 0.35f;

    public bool AntlerEnabled { get; set; } // включается органом «Рога» (химерный слот)

    float nextTime;
    CameraFollow cam;
    Health ownHealth;
    readonly HashSet<Health> hitThis = new();

    void Start()
    {
        cam = FindAnyObjectByType<CameraFollow>();
        ownHealth = GetComponent<Health>();
    }

    public bool TryUse()
    {
        if (!AntlerEnabled || Time.time < nextTime) return false;
        nextTime = Time.time + cooldown;
        DoGore();
        return true;
    }

    void DoGore()
    {
        hitThis.Clear();
        bool any = false;
        var hit = new Hit(ownHealth, transform.position);

        foreach (var col in Physics.OverlapSphere(Center(), radius, ~0, QueryTriggerInteraction.Ignore))
        {
            var hp = col.GetComponentInParent<Health>();
            if (hp == null || hp.transform == transform || !hitThis.Add(hp)) continue;

            hit.Apply(hp, HitEffect.Damage(damage));       // вспышка + стаггер через onDamaged
            hit.Apply(hp, HitEffect.Knockback(force));     // откидывание (Massive резистит)
            for (int i = 0; i < bleedStacks; i++) hit.Apply(hp, HitEffect.Bleed()); // протыкание — кровь
            any = true;
        }

        if (any && cam != null) cam.Shake(0.16f, shake);
    }

    Vector3 Center() => transform.position + transform.forward * range + Vector3.up * 0.5f;

    void OnDrawGizmos()
    {
        if (!AntlerEnabled) return;
        Gizmos.color = TelegraphColors.Antler;
        Gizmos.DrawWireSphere(Center(), radius);
    }
}
