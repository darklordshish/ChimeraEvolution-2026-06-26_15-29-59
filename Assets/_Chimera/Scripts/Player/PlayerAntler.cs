using UnityEngine;

/// <summary>
/// РОГА игрока (орган «Рога» лося в химерном слоте — придаток, как змеиный хвост-обхват): фронтальный удар-протыкание —
/// урон + отлёт немассивных (резист Massive внутри) + КРОВОТЕЧЕНИЕ. Диспатчится на R.
/// ВСЕ ЧИСЛА — ИЗ ЗАПИСИ ОРГАНА (<see cref="AntlerData"/>): та же запись и тот же конус, что у рогов лося (`AntlerAbility`).
/// До 15.09 игрок бил сферой перед собой; при общей записи двух геометрий быть не может. Замаха у игрока нет.
/// </summary>
public class PlayerAntler : MonoBehaviour, IAbility, IOrganAbility
{
    [SerializeField, NotOrganData("ощущение: тряска камеры игрока")] float shake = 0.35f;

    // СКАН С ЗАПАСОМ: сфера ловит коллайдеры по поверхности, конус меряет центры — допуск скана, не число рогов
    const float ScanPad = 1.5f;

    AntlerData data; // раскрытая запись рогов; null — рогов нет

    public System.Type DataType => typeof(AntlerData);
    public void Configure(AbilityData d) => data = d as AntlerData;
    public bool Available => data != null;

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
        if (data == null || Time.time < nextTime) return false;
        nextTime = Time.time + data.cooldown;
        DoGore();
        return true;
    }

    void DoGore()
    {
        if (ownHealth == null) ownHealth = GetComponent<Health>(); // удар до нашего Start
        var hit = new Hit(ownHealth, transform.position);
        // единый паёк рогов (см. MeleeBlow) из записи — тот же, что льёт лось
        var blow = new MeleeBlow { Damage = data.damage, KnockForce = data.knockForce, BleedStacks = data.bleedStacks };
        int gored = 0;
        foreach (var hp in TargetScan.Healths(transform.position, data.range + ScanPad, transform))
        {
            if (!InCone(hp.transform.position)) continue;
            blow.Deliver(hit, hp); // вспышка+стаггер, откидывание, кровь; эрозия по кину — внутри Hit.Apply
            gored++;
        }
        if (gored > 0 && cam != null) cam.Shake(0.16f, shake);
    }

    bool InCone(Vector3 point)
    {
        Vector3 to = point - transform.position; to.y = 0f;
        if (to.sqrMagnitude > data.range * data.range) return false;
        return to.sqrMagnitude < 0.0001f || Vector3.Angle(transform.forward, to) <= data.halfAngle;
    }

    void OnDrawGizmos()
    {
        if (data == null) return;
        Vector3 o = transform.position, f = transform.forward;
        Gizmos.color = TelegraphColors.Antler;
        Gizmos.DrawLine(o, o + Quaternion.AngleAxis(-data.halfAngle, Vector3.up) * f * data.range);
        Gizmos.DrawLine(o, o + Quaternion.AngleAxis(data.halfAngle, Vector3.up) * f * data.range);
    }
}
