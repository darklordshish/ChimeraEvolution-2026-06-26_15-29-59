using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ТАРАН игрока (орган «Лосиные ноги»): рывок игрока становится ГОРЯЩИМ — врезаешься в кого проходишь → урон + отлёт
/// (резист Massive внутри) + сбив. Пассивный наездник на рывке PlayerController (не отдельная кнопка). Урон — раз на
/// рывок (память целей чистится на новом рывке). Кровотечения нет: таран тупой, кровь — у рогов.
/// ВСЕ ЧИСЛА — ИЗ ЗАПИСИ ОРГАНА (<see cref="ChargeData"/>), блок «Удар»: та же запись, что у тарана лося. Разгон
/// считается так же — метры от начала рывка; разбег, топот и пропашка — формы NPC, у рывка игрока их нет.
/// </summary>
public class PlayerCharge : MonoBehaviour, IOrganAbility
{
    [SerializeField, NotOrganData("ощущение: тряска камеры игрока")] float shake = 0.3f;

    // СКАН С ЗАПАСОМ: сфера ловит коллайдеры по поверхности, радиус удара меряет центры — допуск скана
    const float ScanPad = 1.5f;

    ChargeData data; // раскрытая запись тарана; null — тарана нет

    public System.Type DataType => typeof(ChargeData);
    public void Configure(AbilityData d) => data = d as ChargeData;
    public bool Available => data != null;

    PlayerController move;
    Health ownHealth;
    CameraFollow cam;
    readonly HashSet<Health> goredThisDash = new();
    bool wasDashing;
    Vector3 dashStart; // отсюда меряем разгон — как chargeStart у тарана NPC

    void Start()
    {
        move = GetComponent<PlayerController>();
        ownHealth = GetComponent<Health>();
        cam = FindAnyObjectByType<CameraFollow>();
    }

    void Update()
    {
        if (data == null || move == null) return;

        bool dashing = move.IsDashing;
        if (dashing && !wasDashing) { goredThisDash.Clear(); dashStart = transform.position; } // новый рывок — память и разгон с нуля
        wasDashing = dashing;
        if (!dashing) return;

        var hit = new Hit(ownHealth, transform.position);
        float run = (transform.position - dashStart).magnitude; // метры рывка = импульс
        // ТАРАН-ПО-МАССЕ (§5 спеки #2A): снести с ног нужна масса — игрок на человечьем шасси толкает слабо
        float knock = GetComponent<Massive>() != null ? data.knockForce : data.knockForce * data.lightChargeMult;
        var blow = new MeleeBlow
        {
            Damage = Mathf.RoundToInt(data.damage + data.damagePerMeter * run), KnockForce = knock, StaggerTime = data.staggerTime,
        }; // единый паёк тарана (см. MeleeBlow)
        foreach (var hp in TargetScan.Healths(transform.position, data.hitRadius + ScanPad, transform))
        {
            Vector3 to = hp.transform.position - transform.position; to.y = 0f;
            if (to.sqrMagnitude > data.hitRadius * data.hitRadius) continue; // радиус удара — по центрам, как у NPC
            if (!goredThisDash.Add(hp)) continue;                           // раз за рывок (память живёт весь дэш)
            blow.Deliver(hit, hp); // урон (вспышка+стаггер) + снос с дороги; эрозия по кину — внутри Hit.Apply
            if (cam != null) cam.Shake(0.14f, shake);
        }
    }

    void OnDrawGizmos()
    {
        if (data == null) return;
        Gizmos.color = TelegraphColors.Charge;
        Gizmos.DrawWireSphere(transform.position, data.hitRadius);
    }
}
