using UnityEngine;

/// <summary>
/// РЁВ игрока (Глотка лося, K2 идентичности) — второй вокализ, КОНТРАСТ волчьему вою (решение пользователя): вой ведёт стаю ЗА
/// собой, рёв — ДЕТОНАЦИЯ НА МЕСТЕ. Кин-лоси (моя идентичность к Лосю ≥ слабого) впадают в берсерк там, где стоят, и бьют
/// ближайшую угрозу (кин-игрок из их угроз исключён → приведи волков и рявкни — цепь размолотит стаю). ЧУЖИМ — удар по морали.
/// Своих (кинов ЛЮБОГО вида) не контролим. Звучит в мире (Noise).
/// ВСЕ ЧИСЛА — ИЗ ЗАПИСИ ОРГАНА (<see cref="BellowData"/>): та же запись, по которой ревёт лось.
/// </summary>
public class PlayerBellow : MonoBehaviour, IAbility, IOrganAbility
{
    [SerializeField, NotOrganData("ощущение: тряска камеры игрока")] float shake = 0.35f;

    BellowData data; // раскрытая запись рёва; null — Глотки нет

    public System.Type DataType => typeof(BellowData);
    public void Configure(AbilityData d) => data = d as BellowData;
    public bool Available => data != null;
    public float FearRadius => data != null ? data.fearRadius : 0f; // для аккорда с воем: стан берёт БОЛЬШИЙ из радиусов голосов

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
        if (data == null || Time.time < nextTime) return false;
        nextTime = Time.time + data.cooldown;
        DoBellow();
        return true;
    }

    void DoBellow()
    {
        if (body == null) TryGetComponent(out body);
        if (noiseSrc == null) TryGetComponent(out noiseSrc);
        if (noiseSrc != null) noiseSrc.Spike(1f, 1f, TelegraphColors.Howl); // рёв ЗВУЧИТ: уши слышат (лоси насторожатся и без кина)
        if (cam != null) cam.Shake(0.2f, shake);

        foreach (var hp in TargetScan.Healths(transform.position, data.rallyRadius, transform))
        {
            // признание вида цели решает знак голоса (как у воя): кинов не контролим, а созываем
            if (KinVoice.TryRallyKin(body, hp, transform.position)) continue; // свой созван, не пугаем

            // ЧУЖИЕ в радиусе ужаса: рёв туши давит дух
            if ((hp.transform.position - transform.position).sqrMagnitude <= data.fearRadius * data.fearRadius
                && hp.TryGetComponent<Morale>(out var morale))
            {
                morale.Add(-data.fearMoraleHit);
                Perception.BreakGhost(); // напугал — воздействие: призрак раскрыт
            }
        }
    }

    void OnDrawGizmos()
    {
        if (data == null) return;
        Gizmos.color = TelegraphColors.Howl;
        Gizmos.DrawWireSphere(transform.position, data.fearRadius);
    }
}
