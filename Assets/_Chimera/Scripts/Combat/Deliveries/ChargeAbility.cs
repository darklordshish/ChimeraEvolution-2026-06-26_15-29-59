using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Доставка «таран»: замах (телеграф Charge) → закоммиченный рывок ВПЕРЁД по земле (без дуги прыжка) →
/// удар копытами по цели в hitRadius: урон + Knockback (резист у Massive — внутри Knockback) + Stagger.
/// ПРОПАШКА (массивный таранящий): по пути раздвигает попавшихся — кин толчком, не-кин толчком+уроном разгона.
/// Закоммичен как прыжок: мягкий срыв (стаггер) игнорит, жёсткий (нокбэк) рвёт. Дефолты — лось.
/// ВСЕ ЧИСЛА — ИЗ ЗАПИСИ ОРГАНА «Лосиные ноги» (<see cref="ChargeData"/>, спека «данные в органах»): нет записи — тарана нет.
/// Довод о длине разбега и прочие обоснования чисел живут у полей записи, рядом с ручками.
/// </summary>
public class ChargeAbility : WindupAbility, IOrganAbility
{
    ChargeData data; // раскрытая запись тарана; null — тарана нет

    public System.Type DataType => typeof(ChargeData);
    public void Configure(AbilityData d) => data = d as ChargeData;
    public bool Available => data != null;
    protected override bool Ready => data != null;
    protected override float WindupTime => data.windupTime;
    protected override float StaminaCost => data != null ? data.staminaCost : 0f;      // цена тарана — запись ног

    // психика читает окно дистанций тарана; нет тарана — пустое окно [0, 0]
    public float MinRange => data != null ? data.minRange : 0f;
    public float MaxRange => data != null ? data.maxRange : 0f;
    public override float WindowMin => MinRange;            // окно арсенала: с минимальной дистанции записи
    public override float WindowMax => MaxRange;

    protected override float GizmoRange => MaxRange; // хитбокс — дальность разбега тарана
    protected override float GizmoHalfAngle => 30f;

    bool charging, hit;
    float chargeEnd;
    Vector3 dir, chargeStart; // старт разбега — от него меряем разгон (метры → бонус урона)
    readonly HashSet<Health> plowedThisCharge = new(); // пропашка: каждого задеваем раз за таран
    CreatureBody ownBody; // для кин-чека Regard (лениво)

    protected override Color TelegraphColor => TelegraphColors.Charge;

    protected override AbilityRun OnTick()
    {
        if (!charging)
        {
            if (Time.time < windupEnd) { SettleInPlace(); return AbilityRun.Running; }
            charging = true; hit = false; plowedThisCharge.Clear();
            telegraph.Clear();
            chargeEnd = Time.time + data.duration;
            dir = DirToTarget();               // направление берём в последний кадр замаха
            chargeStart = transform.position;  // отсюда меряем разгон
        }

        controller.Move(dir * data.chargeSpeed * Time.deltaTime);                                // рывок вперёд
        if (!controller.isGrounded) controller.Move(Vector3.up * gravity * Time.deltaTime); // прижать к земле

        // ПРОПАШКА: массивная туша по пути раздвигает попавшихся — ВКЛЮЧАЯ главную цель (её штатный удар в конце
        // ЧАСТО МАЖЕТ: лось коммитит разбег и стартует ближе своего разгона → проскакивает мимо увернувшейся цели,
        // урона не было вовсе). Дедуплится с финальным ударом ниже (не задвоит, когда тот всё же попадает). Кин —
        // только толчок; не-кин — толчок + урон ТОЙ ЖЕ формулы разгона. Кин-чек через Regard (идентичность, не тип-B)
        if (GetComponent<Massive>() != null)
        {
            if (ownBody == null) TryGetComponent(out ownBody);
            foreach (var hp in TargetScan.Healths(transform.position, data.plowRadius, transform))
            {
                if (!plowedThisCharge.Add(hp)) continue; // каждого задеваем раз за таран (цель — тоже, её удар часто мажет)
                var vb = hp.GetComponent<CreatureBody>();
                bool kin = ownBody != null && vb != null && CreatureBody.Regard(ownBody, vb) != KinTier.None;
                if (!kin) // не-кин: урон разгона (формула главного удара), БЕЗ стаггера — это снос, не стан
                {
                    float run = (transform.position - chargeStart).magnitude;
                    int dmg = Mathf.RoundToInt((data.damage + data.damagePerMeter * run) * DamageMult);
                    new MeleeBlow { Damage = dmg }.Deliver(new Hit(ownHealth, transform.position), hp);
                }
                Vector3 away = hp.transform.position - transform.position; away.y = 0f;
                if (hp.TryGetComponent<Knockback>(out var kb)) kb.Push((away.sqrMagnitude > 0.0001f ? away.normalized : dir) * data.plowForce); // несильный снос (Massive-цель проигнорит)
            }
        }

        // финальный удар — только если пропашка цель ещё не задела (дедуп: `Add` вернёт false, если уже плужена).
        // Так цель гарантированно получает урон (обычно от пропашки), а полный отброс/стаггер — когда доехал в упор
        if (!hit && targetHealth != null && DistToTarget() <= data.hitRadius && plowedThisCharge.Add(targetHealth))
        {
            hit = true;
            float run = (transform.position - chargeStart).magnitude; // метры разбега = импульс туши
            // единый паёк (см. MeleeBlow): урон-с-разгоном (мощь уже учтена) + сбив. Откидывание ОСТАЁТСЯ
            // направленным (импульс ВДОЛЬ линии тарана, не от центра) — фирменный снос, числа не трогаем
            int dmg = Mathf.RoundToInt((data.damage + data.damagePerMeter * run) * DamageMult);
            var blow = new MeleeBlow { Damage = dmg, StaggerTime = data.staggerTime };
            blow.Deliver(new Hit(ownHealth, transform.position), targetHealth);
            // ТАРАН-ПО-МАССЕ (§5 спеки #2A): массивная туша (лось) сносит в полную силу, лёгкое тело
            // (игрок на человечьем шасси) — лишь слабый толчок. Приём читает СВОЁ тело
            float appliedKnock = GetComponent<Massive>() != null ? data.knockForce : data.knockForce * data.lightChargeMult;
            if (targetHealth.TryGetComponent<Knockback>(out var kb)) kb.Push(dir * appliedKnock); // Massive-ЦЕЛЬ Push всё равно проигнорит
        }
        if (Time.time < chargeEnd) return AbilityRun.Running;

        Stomp(); // топот на приземлении тарана: землетрясение вокруг
        charging = false;
        return AbilityRun.Done;
    }

    // топот-приземление: сбивает всех со Stagger в радиусе (кроме себя) — разгоняет скопления, «землетрясение»
    void Stomp()
    {
        foreach (var col in Physics.OverlapSphere(transform.position, data.stompRadius, ~0, QueryTriggerInteraction.Ignore))
        {
            var st = col.GetComponentInParent<Stagger>();
            if (st == null || st.gameObject == gameObject) continue;
            st.Hitstun(data.stompStagger);
            if (st.TryGetComponent<Knockback>(out var kb)) // радиальный толчок = видимое землетрясение (Massive резистит)
            {
                Vector3 away = st.transform.position - transform.position; away.y = 0f;
                float d = away.magnitude;
                if (d > 0.0001f) kb.Push(away / d * data.stompForce * Mathf.Clamp01(1f - d / data.stompRadius * 0.6f)); // ближе к эпицентру — сильнее
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
