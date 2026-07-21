using UnityEngine;

/// <summary>
/// Укус — вторая атака (слот «Пасть»). Отдельная кнопка (Q / правый триггер):
/// короткая дистанция, мощный единичный удар + вампиризм. Активен, только если слот «Пасть» надет
/// (CreatureBody выставляет BiteEnabled).
/// </summary>
public class PlayerBite : MonoBehaviour, IAbility
{
    [Header("Укус")]
    [SerializeField] int damage = 14;
    [SerializeField] float range = 1.2f;   // короче когтя
    [SerializeField] float radius = 0.9f;
    [SerializeField] float cooldown = 0.7f;
    [SerializeField] int lifeSteal = 6;    // лечение за результативный укус
    [SerializeField] float shake = 0.2f;
    [SerializeField, Range(0f, 1f)] float regenDebuff = 0.5f; // укус сбивает реген цели (×0.5)
    [SerializeField] float regenDebuffTime = 3f;

    public bool BiteEnabled { get; set; }  // включается слотом «Пасть»

    int organDamage;  // урон из данных органа Пасти (0 = орган молчит → сериализованный дефолт выше)
    int venomStacks;  // яд из данных органа (змеиные клыки)
    int bleedStacks;  // кровь из данных органа (волчьи клыки)

    public void SetDamage(int v) => organDamage = v;
    public void SetVenom(int stacks) => venomStacks = stacks;
    public void SetBleed(int stacks) => bleedStacks = stacks;

    float nextTime;
    CameraFollow cam;
    Health ownHealth;

    void Start()
    {
        cam = FindAnyObjectByType<CameraFollow>();
        ownHealth = GetComponent<Health>();
    }

    // водитель зовёт по вводу; активен только с надетой Пастью; кулдаун проверяем сами
    public bool TryUse()
    {
        if (!BiteEnabled || Time.time < nextTime) return false;
        nextTime = Time.time + cooldown;
        DoBite();
        return true;
    }

    void DoBite()
    {
        // призрака раскрывает попадание (Hit.Apply), не замах
        var hit = new Hit(ownHealth, transform.position);
        // единый паёк укуса (см. MeleeBlow): урон + сбив регена + вампиризм + яд/кровь по данным органа
        var blow = new MeleeBlow
        {
            Damage = organDamage > 0 ? organDamage : damage,
            LifeSteal = lifeSteal, VenomStacks = venomStacks, BleedStacks = bleedStacks,
            RegenDebuffFactor = regenDebuff, RegenDebuffTime = regenDebuffTime,
        };
        var targets = TargetScan.Healths(BiteCenter(), radius, transform);
        foreach (var hp in targets) blow.Deliver(hit, hp); // эрозия по кину — внутри Hit.Apply

        if (targets.Count > 0)
        {
            if (cam != null) cam.Shake(0.12f, shake);
            Hitstop.Do(0.05f);
        }
    }

    Vector3 BiteCenter() => transform.position + transform.forward * range + Vector3.up * 0.3f; // пасть/грудь (корень игрока — центр капсулы)

    void OnDrawGizmos()
    {
        if (!BiteEnabled) return;
        Gizmos.color = TelegraphColors.Bite;
        Gizmos.DrawWireSphere(BiteCenter(), radius);
    }
}
