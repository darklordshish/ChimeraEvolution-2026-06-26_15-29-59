using UnityEngine;

/// <summary>
/// Положенная ловушка: ждёт чужую живую Health в радиусе и бьёт пайком от имени
/// владельца (атрибуция — как яд/снаряды: родство/эволюция работают заочно).
/// Одна жертва за тик, при uses 0 — самоуничтожение. Владелец пропал — инертна.
/// Установка игроком (кнопка/инвентарь) — не здесь. Лес, слайс s4c.
/// </summary>
public class PlacedTrap : MonoBehaviour
{
    public TrapDef def = new TrapDef();
    public int UsesLeft { get; private set; }

    Health owner;

    /// <summary>Вооружить: чья (атрибуция) и чем (паёк). Без владельца не работает.</summary>
    public void Arm(Health ownerHealth, TrapDef trapDef)
    {
        owner = ownerHealth;
        if (trapDef != null) def = trapDef;
        UsesLeft = def != null ? def.uses : 0;
    }

    void Update()
    {
        if (def == null || UsesLeft <= 0) return;
        if (owner == null) return;
        foreach (var hp in TargetScan.Healths(transform.position, def.radius,
                     owner.transform))
        {
            if (hp == null || hp.Current <= 0f) continue; // трупы не триггерят
            var hit = new Hit(owner, transform.position);
            var blow = new MeleeBlow
            {
                Damage = def.damage,
                BleedStacks = def.bleedStacks,
                SlowStacks = def.slowStacks
            };
            blow.Deliver(hit, hp);
            UsesLeft--;
            if (UsesLeft <= 0) Destroy(gameObject);
            break;
        }
    }
}
