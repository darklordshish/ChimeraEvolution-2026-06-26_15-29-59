/// <summary>
/// Разрешённый удар руки: форма совместима с полями LimbStrikeData
/// (паёк + конус + цена/ритм), чтобы хук s4b кормил PlayerAttack 1:1.
/// power идёт отдельным множителем damageMult — не печём в int, иначе двойное
/// округление у доставки. Лес, слайс s4.
/// </summary>
public struct HandStrike
{
    public int damage;
    public float damageMult;
    public float knockForce;
    public int bleedStacks;
    public float range;
    public float halfAngle;
    public float staminaCost;
    public float cooldown;
    public float windupTime;
}
