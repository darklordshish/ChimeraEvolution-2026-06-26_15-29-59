using System;
using UnityEngine;

/// <summary>
/// Укус игрока — вторая атака (слот «Пасть»), кнопка Q / правый триггер. ВСЕ ЧИСЛА — ИЗ ЗАПИСИ ОРГАНА
/// (<see cref="BiteData"/>): та же запись, что кормит укус NPC (`BiteAbility`), — волчьей Пастью игрок кусает как волк.
/// Нет записи — нет укуса. Грань держит только то, что принадлежит управлению игрока: кнопку и ощущение (тряска).
///
/// ГЕОМЕТРИЯ — ТОТ ЖЕ КОНУС, что у NPC: досягаемость и полуугол из записи, расстояние между центрами на плоскости.
/// До 15.09 игрок кусал сферой перед собой; при общей записи двух геометрий быть не может.
/// Замаха у игрока нет — нажатие и есть решение; `windupTime` записи читает только NPC (телеграф для уворота).
/// </summary>
public class PlayerBite : MonoBehaviour, IAbility, IOrganAbility
{
    [SerializeField, NotOrganData("ощущение: тряска камеры игрока")] float shake = 0.2f;

    // СКАН С ЗАПАСОМ: сфера ловит коллайдеры по поверхности, а конус меряет центры — берём шире досягаемости
    // и отбираем по той же метрике, что у BiteAbility (DistToTarget/Angle). Запас — не число укуса, а допуск скана
    const float ScanPad = 1.5f;

    BiteData data; // раскрытая запись надетой Пасти; null — укуса нет

    public Type DataType => typeof(BiteData);
    public void Configure(AbilityData d) => data = d as BiteData;
    public bool Available => data != null;

    float nextTime;
    CameraFollow cam;
    Health ownHealth;

    void Start()
    {
        cam = FindAnyObjectByType<CameraFollow>();
        ownHealth = GetComponent<Health>();
    }

    // водитель зовёт по вводу; перезарядка — из записи органа
    public bool TryUse()
    {
        if (data == null || Time.time < nextTime) return false;
        nextTime = Time.time + data.cooldown;
        DoBite();
        return true;
    }

    void DoBite()
    {
        if (ownHealth == null) ownHealth = GetComponent<Health>(); // укус до нашего Start (сборка тела в тот же кадр)
        // призрака раскрывает попадание (Hit.Apply), не замах
        var hit = new Hit(ownHealth, transform.position);
        // единый паёк укуса (см. MeleeBlow) из записи органа. Вампиризма у приёмов нет — он у модуля боссовости
        var blow = new MeleeBlow
        {
            Damage = data.damage,
            VenomStacks = data.venomStacks, BleedStacks = data.bleedStacks,
            RegenDebuffFactor = data.regenDebuff, RegenDebuffTime = data.regenDebuffTime,
        };
        int bitten = 0;
        foreach (var hp in TargetScan.Healths(transform.position, data.range + ScanPad, transform))
        {
            if (!InCone(hp.transform.position)) continue;
            blow.Deliver(hit, hp); // эрозия по кину — внутри Hit.Apply
            bitten++;
        }

        if (bitten > 0)
        {
            if (cam != null) cam.Shake(0.12f, shake);
            Hitstop.Do(0.05f);
        }
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
        Gizmos.color = TelegraphColors.Bite;
        Gizmos.DrawLine(o, o + Quaternion.AngleAxis(-data.halfAngle, Vector3.up) * f * data.range);
        Gizmos.DrawLine(o, o + Quaternion.AngleAxis(data.halfAngle, Vector3.up) * f * data.range);
    }
}
