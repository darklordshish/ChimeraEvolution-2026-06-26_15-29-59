using System;

/// <summary>
/// Предмет в руках: данные. Форма удара — та же, что LimbStrikeData
/// (damage/knock/bleed/range/halfAngle/stamina/cooldown/windup), чтобы будущий хук s4b
/// мог кормить PlayerAttack без пересчёта. Plain-класс (не SO): SO-обёртка — только ради
/// ассетов в инспекторе, позже. Что такое засада — флаг снаружи (чувства/стелс).
/// Лес, слайс s4.
/// </summary>
[Serializable]
public class HandItemDef
{
    public string id = "bare";
    public HandItemKind kind = HandItemKind.Bare;
    public int tier;

    public int damage;
    public float knockForce;
    public int bleedStacks;
    public float range;
    public float halfAngle;
    public float staminaCost;
    public float cooldown;
    public float windupTime;

    public float backstabMult = 1f;

    public int durabilityMax;
    public float spoilSeconds;
    public float fuelSeconds;

    public bool twoHanded;
    public float lightRadius;

    /// <summary>Голые руки: форма как запись удара конечностью.</summary>
    public static HandItemDef Bare()
    {
        return new HandItemDef
        {
            id = "bare",
            kind = HandItemKind.Bare,
            tier = 0,
            damage = 10,
            range = 1.6f,
            halfAngle = 60f,
            cooldown = 0.45f
        };
    }
}
