using UnityEngine;

/// <summary>
/// ПИНОК на E (или B на геймпаде): отталкивает врагов перед игроком + лёгкий урон (через который идут вспышка и сбив).
/// Инструмент создания пространства под кайтинг. Фича ЧЕЛОВЕЧЬИХ ног: с волчьими пропадает, и захват снимается только рывком.
/// ВСЕ ЧИСЛА — ИЗ ЗАПИСИ ОРГАНА (<see cref="KickData"/>). Двойника у NPC нет, поэтому форма — сфера на выносе — как была.
/// </summary>
public class PlayerKick : MonoBehaviour, IAbility, IOrganAbility
{
    [SerializeField, NotOrganData("ощущение: тряска камеры игрока")] float shake = 0.4f;

    KickData data; // раскрытая запись пинка; null — ноги не человечьи

    public System.Type DataType => typeof(KickData);
    public void Configure(AbilityData d) => data = d as KickData;
    public bool Available => data != null;

    float nextTime;
    CameraFollow cam;
    Health ownHealth;

    void Start()
    {
        cam = FindAnyObjectByType<CameraFollow>();
        ownHealth = GetComponent<Health>();
    }

    // водитель зовёт по вводу; перезарядка — из записи
    public bool TryUse()
    {
        if (data == null || Time.time < nextTime) return false;
        nextTime = Time.time + data.cooldown;
        DoKick();
        return true;
    }

    void DoKick()
    {
        if (ownHealth == null) ownHealth = GetComponent<Health>(); // пинок до нашего Start
        var hit = new Hit(ownHealth, transform.position); // источник нужен раскрытию призрака
        var blow = new MeleeBlow { Damage = data.damage, KnockForce = data.knockForce }; // единый паёк (см. MeleeBlow)
        var targets = TargetScan.Healths(KickCenter(), data.radius, transform);
        foreach (var hp in targets) blow.Deliver(hit, hp); // лёгкий урон (вспышка+сбив) + отталкивание; эрозия — внутри Hit.Apply

        if (targets.Count > 0 && cam != null) cam.Shake(0.16f, shake);
    }

    Vector3 KickCenter() => transform.position + transform.forward * data.reach + Vector3.up * -0.2f; // нога ниже пояса (корень — центр капсулы)

    void OnDrawGizmos()
    {
        if (data == null) return;
        Gizmos.color = TelegraphColors.Kick;
        Gizmos.DrawWireSphere(KickCenter(), data.radius);
    }
}
