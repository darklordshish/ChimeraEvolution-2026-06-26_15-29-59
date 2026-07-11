using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Пинок на E (или B на геймпаде): отталкивает врагов перед игроком + лёгкий урон
/// (через который идут вспышка и стаггер). Инструмент создания пространства под кайтинг.
/// Фича ЧЕЛОВЕЧЕСКИХ ног (CreatureBody выставляет KickEnabled) — с волчьими ногами пропадает,
/// и захват снимается только рывком. Потом: копыто лося и т.п. на том же флаге.
/// </summary>
public class PlayerKick : MonoBehaviour, IAbility
{
    [Header("Пинок")]
    [SerializeField] int damage = 4;
    [SerializeField] float range = 1.8f;    // вынос центра вперёд
    [SerializeField] float radius = 1.6f;   // широкий — толкаем клин стаи
    [SerializeField] float force = 12f;     // сила отлёта
    [SerializeField] float cooldown = 1.0f;
    [SerializeField] float shake = 0.4f;

    // включается органом «Ноги» (человеческие). Дефолт true — без данных тела пинок работает как раньше.
    public bool KickEnabled { get; set; } = true;

    float nextTime;
    CameraFollow cam;
    Health ownHealth;
    readonly HashSet<Health> hitThisKick = new();

    void Start()
    {
        cam = FindAnyObjectByType<CameraFollow>();
        ownHealth = GetComponent<Health>();
    }

    // водитель зовёт по вводу; активен только с человеческими ногами; кулдаун проверяем сами
    public bool TryUse()
    {
        if (!KickEnabled || Time.time < nextTime) return false;
        nextTime = Time.time + cooldown;
        DoKick();
        return true;
    }

    void DoKick()
    {
        hitThisKick.Clear();
        bool any = false;
        var hit = new Hit(ownHealth, transform.position); // источник нужен раскрытию призрака (вампиризма у пинка всё равно нет)

        Collider[] cols = Physics.OverlapSphere(KickCenter(), radius, ~0, QueryTriggerInteraction.Ignore);
        foreach (var col in cols)
        {
            var hp = col.GetComponentInParent<Health>();
            if (hp == null || hp.transform == transform || !hitThisKick.Add(hp)) continue;

            hit.Apply(hp, HitEffect.Damage(damage));   // вспышка + стаггер через onDamaged
            hit.Apply(hp, HitEffect.Knockback(force)); // отталкивание от игрока
            any = true;
        }

        if (any && cam != null) cam.Shake(0.16f, shake);
    }

    Vector3 KickCenter() => transform.position + transform.forward * range + Vector3.up * 0.5f;

    void OnDrawGizmos()
    {
        if (!KickEnabled) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(KickCenter(), radius);
    }
}
