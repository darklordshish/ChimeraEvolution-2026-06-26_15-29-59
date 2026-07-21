using UnityEngine;

/// <summary>
/// РЁВ игрока (Глотка лося, K2 идентичности) — второй вокализ, КОНТРАСТ волчьему вою (решение
/// пользователя): вой ведёт стаю ЗА собой, рёв — ДЕТОНАЦИЯ НА МЕСТЕ. Кин-лоси (моя идентичность
/// к Лосю ≥ слабого) впадают в берсерк там, где стоят, и бьют ближайшую угрозу (кин-игрок из их
/// угроз исключён → приведи волков и рявкни — цепь размолотит стаю). ЧУЖИМ — удар по морали (−2,
/// рёв туши). Своих (кинов ЛЮБОГО вида) не контролим. Кулдаун свой; звучит в мире (Noise).
/// </summary>
public class PlayerBellow : MonoBehaviour, IAbility
{
    [Header("Рёв (Глотка лося)")]
    [SerializeField] float fearRadius = 12f;   // удар по морали чужих (−2 — голос туши)
    [SerializeField] float rallyRadius = 30f;  // кин-лоси в этом радиусе детонируют берсерком (цепь понесёт дальше)
    [SerializeField] float cooldown = 10f;
    [SerializeField] float shake = 0.35f;

    public bool BellowEnabled { get; set; } // включается Глоткой лося (CreatureBody)
    public float FearRadius => fearRadius;  // для аккорда с воем: стан берёт БОЛЬШИЙ из радиусов двух голосов

    float nextTime;
    CameraFollow cam;
    CreatureBody body;
    Noise noiseSrc;

    void Start()
    {
        cam = FindAnyObjectByType<CameraFollow>();
        TryGetComponent(out body);
    }

    public bool TryUse()
    {
        if (!BellowEnabled || Time.time < nextTime) return false;
        nextTime = Time.time + cooldown;
        DoBellow();
        return true;
    }

    void DoBellow()
    {
        if (noiseSrc == null) TryGetComponent(out noiseSrc);
        if (noiseSrc != null) noiseSrc.Spike(1f, 1f); // рёв ЗВУЧИТ: уши слышат (лоси насторожатся и без кина)
        if (cam != null) cam.Shake(0.2f, shake);

        foreach (var hp in TargetScan.Healths(transform.position, rallyRadius, transform))
        {
            // признание вида цели решает знак голоса (как у воя): кинов не контролим
            var kinTier = KinTier.None;
            if (body != null && hp.TryGetComponent<CreatureBody>(out var targetBody) && targetBody.Chassis != null)
                kinTier = body.Tier(targetBody.Chassis);

            if (kinTier != KinTier.None)
            {
                // ЕДИНЫЙ кин-rally («эффект в органе, родство в комбинации»): форма зова — у ВИДА цели
                // (лось — детонация на месте, волк — дух+эскорт), голос-орган решает лишь контроль чужих
                KinVoice.RallyKin(hp, kinTier, transform.position);
                continue; // своих не пугаем
            }

            // ЧУЖИЕ в ближнем кольце: рёв туши давит дух (−2)
            if ((hp.transform.position - transform.position).sqrMagnitude <= fearRadius * fearRadius
                && hp.TryGetComponent<Morale>(out var morale))
            {
                morale.Add(-2f);
                Perception.BreakGhost(); // напугал — воздействие: призрак раскрыт
            }
        }
    }

    void OnDrawGizmos()
    {
        if (!BellowEnabled) return;
        Gizmos.color = TelegraphColors.Howl;
        Gizmos.DrawWireSphere(transform.position, fearRadius);
    }
}
