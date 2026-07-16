using UnityEngine;

/// <summary>
/// Доставка «таран»: замах (телеграф Charge) → закоммиченный рывок ВПЕРЁД по земле (без дуги прыжка) →
/// удар копытами по цели в hitRadius: урон + Knockback (резист у Massive — внутри Knockback) + Stagger.
/// Закоммичен как прыжок: мягкий срыв (стаггер) игнорит, жёсткий (нокбэк) рвёт. Дефолты — лось.
/// </summary>
public class ChargeAbility : WindupAbility
{
    [Header("Таран")]
    [SerializeField] float minRange = 4f;
    [SerializeField] float maxRange = 12f;
    [SerializeField] float chargeSpeed = 16f;
    [SerializeField] float duration = 0.7f;
    [SerializeField] int damage = 20;
    [SerializeField] float hitRadius = 1.8f;
    [SerializeField] float knockForce = 12f;   // отлёт цели (Knockback сам резистит Massive)
    [SerializeField] float staggerTime = 0.5f; // сбив цели при попадании
    [SerializeField] float stompRadius = 4f;   // топот-приземление: AOE в конце тарана (разгоняет скопления)
    [SerializeField] float stompStagger = 0.4f;
    [SerializeField] float stompForce = 13f;   // радиальный толчок топота у эпицентра — «землетрясение» (спад к краю; Massive резистит)

    public float MinRange => minRange; // психика читает окно дистанций тарана
    public float MaxRange => maxRange;

    protected override float GizmoRange => maxRange; // хитбокс — дальность разбега тарана
    protected override float GizmoHalfAngle => 30f;

    bool charging, hit;
    float chargeEnd;
    Vector3 dir;

    protected override Color TelegraphColor => TelegraphColors.Charge;

    protected override AbilityRun OnTick()
    {
        if (!charging)
        {
            if (Time.time < windupEnd) { SettleInPlace(); return AbilityRun.Running; }
            charging = true; hit = false;
            telegraph.Clear();
            chargeEnd = Time.time + duration;
            dir = DirToTarget();               // направление берём в последний кадр замаха
        }

        controller.Move(dir * chargeSpeed * Time.deltaTime);                                // рывок вперёд
        if (!controller.isGrounded) controller.Move(Vector3.up * gravity * Time.deltaTime); // прижать к земле

        if (!hit && targetHealth != null && DistToTarget() <= hitRadius)
        {
            hit = true;
            var h = new Hit(ownHealth, transform.position);
            h.Apply(targetHealth, HitEffect.Damage(Mathf.RoundToInt(damage * DamageMult)));
            if (targetHealth.TryGetComponent<Knockback>(out var kb)) kb.Push(dir * knockForce); // Massive-цель Push проигнорит
            if (targetHealth.TryGetComponent<Stagger>(out var st)) st.Hitstun(staggerTime);
        }
        if (Time.time < chargeEnd) return AbilityRun.Running;

        Stomp(); // топот на приземлении тарана: землетрясение вокруг
        charging = false;
        return AbilityRun.Done;
    }

    // топот-приземление: сбивает всех со Stagger в радиусе (кроме себя) — разгоняет скопления, «землетрясение»
    void Stomp()
    {
        foreach (var col in Physics.OverlapSphere(transform.position, stompRadius, ~0, QueryTriggerInteraction.Ignore))
        {
            var st = col.GetComponentInParent<Stagger>();
            if (st == null || st.gameObject == gameObject) continue;
            st.Hitstun(stompStagger);
            if (st.TryGetComponent<Knockback>(out var kb)) // радиальный толчок = видимое землетрясение (Massive резистит)
            {
                Vector3 away = st.transform.position - transform.position; away.y = 0f;
                float d = away.magnitude;
                if (d > 0.0001f) kb.Push(away / d * stompForce * Mathf.Clamp01(1f - d / stompRadius * 0.6f)); // ближе к эпицентру — сильнее
            }
        }
    }

    // таран закоммичен: стаггер (мягкий срыв) не рвёт; нокбэк (hard) рвёт
    public override void Abort(bool hard)
    {
        if (charging && !hard) return;
        charging = false;
        base.Abort(hard);
    }
}
