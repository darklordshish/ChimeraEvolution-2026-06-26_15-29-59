using UnityEngine;

/// <summary>
/// УДАР КОНЕЧНОСТЬЮ игрока (левая кнопка, слот «Руки»): бьём конусом перед собой, каждому найденному — раз за замах.
/// При попадании — сочность: хитстоп + тряска камеры.
///
/// ВСЕ ЧИСЛА — ИЗ ЗАПИСИ ОРГАНА (<see cref="LimbStrikeData"/>): та же запись и тот же конус, что у удара конечностью NPC
/// (`LimbStrikeAbility`) — волчьим когтем игрок бьёт как волк. До 15.09 урон и досягаемость приходили суммой органов через
/// `SetMelee`, а бил игрок сферой. У грани своё — ощущение и ТЕМП АТАК: он принадлежит Сердцу, тело задаёт его модификатором.
/// </summary>
public class PlayerAttack : MonoBehaviour, IAbility, IOrganAbility
{
    [Header("Сочность")]
    [SerializeField, NotOrganData("ощущение: хитстоп удара игрока")] float hitstopDuration = 0.06f;
    [SerializeField, NotOrganData("ощущение: тряска камеры игрока")] float shakeMagnitude = 0.25f;

    // СКАН С ЗАПАСОМ: сфера ловит коллайдеры по поверхности, конус меряет центры — допуск скана, не число удара
    const float ScanPad = 1.5f;

    LimbStrikeData data; // раскрытая запись конечности; null — бить нечем

    public System.Type DataType => typeof(LimbStrikeData);
    public void Configure(AbilityData d) => data = d as LimbStrikeData;
    public bool Available => data != null;

    // ТЕМП АТАК — свойство тела (Сердце), а не удара: тело задаёт его в Recompute. Начальное значение — лишь до первой сборки
    float tempo = 0.45f;
    public void SetTempo(float seconds) => tempo = seconds;

    float nextTime;
    CameraFollow cam;
    Health ownHealth;
    HandsBridge hands; // s4b: мост рук (лениво — грань могут до-создать позже)

    void Start()
    {
        cam = FindAnyObjectByType<CameraFollow>();
        ownHealth = GetComponent<Health>();
    }

    // водитель (PlayerInputDriver) зовёт по вводу; темп проверяем сами
    public bool TryUse()
    {
        if (data == null || Time.time < nextTime) return false;
        nextTime = Time.time + tempo;
        DoAttack();
        return true;
    }

    void DoAttack()
    {
        if (ownHealth == null) ownHealth = GetComponent<Health>(); // удар до нашего Start
        if (hands == null) hands = GetComponent<HandsBridge>(); // мост могут повесить позже — берём лениво
        // призрака раскрывает ПОПАДАНИЕ (Hit.Apply), не замах — холостой взмах безопасен
        var hit = new Hit(ownHealth, transform.position);
        // s4b: мост с предметом переопределяет ПАЁК+КОНУС (темп не трогаем); без моста — путь записи
        bool manual = hands != null && hands.HasHands;
        HandStrike strike = manual ? hands.ResolveStrike() : default;
        if (manual) hands.SpendSwing();
        var blow = new MeleeBlow { // единый паёк
            Damage = manual ? strike.damage : data.damage,
            KnockForce = manual ? strike.knockForce : data.knockForce,
            BleedStacks = manual ? strike.bleedStacks : data.bleedStacks };
        float range = manual ? strike.range : data.range;
        float halfAngle = manual ? strike.halfAngle : data.halfAngle;
        int struck = 0;
        foreach (var hp in TargetScan.Healths(transform.position, range + ScanPad, transform))
        {
            if (!InCone(hp.transform.position, range, halfAngle)) continue;
            blow.Deliver(hit, hp, manual ? strike.damageMult : 1f); // урон; эрозия по кину — внутри Hit.Apply
            struck++;
        }

        if (struck > 0) // попали хотя бы по одному — сочность раз за замах
        {
            if (hitstopDuration > 0f) Hitstop.Do(hitstopDuration); // 0 = выключить глобальный фриз
            if (cam != null) cam.Shake(0.12f, shakeMagnitude);
        }
    }

    bool InCone(Vector3 point, float range, float halfAngle)
    {
        Vector3 to = point - transform.position; to.y = 0f;
        if (to.sqrMagnitude > range * range) return false;
        return to.sqrMagnitude < 0.0001f || Vector3.Angle(transform.forward, to) <= halfAngle;
    }

    void OnDrawGizmos()
    {
        if (data == null) return;
        Vector3 o = transform.position, f = transform.forward;
        Gizmos.color = TelegraphColors.Sword;
        Gizmos.DrawLine(o, o + Quaternion.AngleAxis(-data.halfAngle, Vector3.up) * f * data.range);
        Gizmos.DrawLine(o, o + Quaternion.AngleAxis(data.halfAngle, Vector3.up) * f * data.range);
    }
}
