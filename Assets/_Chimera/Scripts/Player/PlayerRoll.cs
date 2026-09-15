using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ПЕРЕКАТ ежиных ног: рывок игрока идёт «в клубке» — кого прокатил насквозь, тот РЕЖЕТСЯ (урон + кровь), а сам ты
/// неуязвим (i-frames рывка = броня клубка). Третий профиль рывка после лосиного ТАРАНА и волчьей СКОРОСТИ (§3-бис спеки
/// ежа). Пассивный наездник на рывке PlayerController, как `PlayerCharge`. Урон — раз на рывок.
///
/// ВСЕ ЧИСЛА — ИЗ ЗАПИСИ ОРГАНА (<see cref="RollData"/>): та же запись и та же геометрия (сфера впереди на свой радиус),
/// что у проката шара ежа (`CurlDefense`).
///
/// КРОСС-СЛОТ СЕТ (§0-бис спеки, «дожд-ролл с иглами»): колется, только когда надеты И «Ежиные ноги» (форма-кувырок,
/// запись переката), И иглы в Шкуре (жало — тело вешает `Thorns`). Одни ноги — защитный уворот без урона: грань не
/// активна, а i-frames рывка живут в PlayerController и остаются. У ежа-NPC иглы есть всегда.
/// </summary>
public class PlayerRoll : MonoBehaviour, IOrganAbility
{
    [SerializeField, NotOrganData("ощущение: тряска камеры игрока")] float shake = 0.18f;

    RollData data; // раскрытая запись переката; null — ноги не ежиные

    public System.Type DataType => typeof(RollData);
    public void Configure(AbilityData d) => data = d as RollData;
    public bool Available => data != null;

    /// <summary>Колется ли перекат сейчас: запись ног есть И надеты иглы (кросс-слот сет).</summary>
    public bool Active => data != null && TryGetComponent<Thorns>(out _);

    PlayerController move;
    Health ownHealth;
    CameraFollow cam;
    readonly HashSet<Health> hitThisDash = new();
    bool wasDashing;

    void Start()
    {
        move = GetComponent<PlayerController>();
        ownHealth = GetComponent<Health>();
        cam = FindAnyObjectByType<CameraFollow>();
    }

    void Update()
    {
        if (!Active || move == null) return;

        bool dashing = move.IsDashing;
        if (dashing && !wasDashing) hitThisDash.Clear(); // новый рывок — цель-память с нуля
        wasDashing = dashing;
        if (!dashing) return;

        var hit = new Hit(ownHealth, transform.position);
        var blow = new MeleeBlow { Damage = data.damage, KnockForce = data.knockForce, BleedStacks = data.bleedStacks }; // паёк игл
        foreach (var hp in TargetScan.Healths(transform.position + transform.forward * data.radius, data.radius, transform))
        {
            if (!hitThisDash.Add(hp)) continue; // раз за рывок (память живёт весь дэш)
            blow.Deliver(hit, hp); // урон + кровотечение + толчок; эрозия по кину — внутри Hit.Apply
            if (cam != null) cam.Shake(0.12f, shake);
        }
    }

    void OnDrawGizmos()
    {
        if (!Active) return;
        Gizmos.color = TelegraphColors.Roll;
        Gizmos.DrawWireSphere(transform.position + transform.forward * data.radius, data.radius);
    }
}
