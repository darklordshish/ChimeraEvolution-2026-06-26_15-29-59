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
        var hit = new Hit(ownHealth, transform.position);
        // единый паёк рогов (см. MeleeBlow) — тот же удар льёт NPC-лось; мощь впечена в числа (mult 1)
        var blow = new MeleeBlow { Damage = damage, KnockForce = force, BleedStacks = bleedStacks };
        var targets = TargetScan.Healths(Center(), radius, transform);
        foreach (var hp in targets) blow.Deliver(hit, hp); // вспышка+стаггер, откидывание, кровь; эрозия — внутри Hit.Apply

        if (targets.Count > 0 && cam != null) cam.Shake(0.16f, shake);
    }

    Vector3 Center() => transform.position + transform.forward * range + Vector3.up * 0.55f; // рога — уровень головы (корень — центр капсулы)

    void OnDrawGizmos()
    {
        if (!AntlerEnabled) return;
        Gizmos.color = TelegraphColors.Antler;
        Gizmos.DrawWireSphere(Center(), radius);
    }
}
