using UnityEngine;

/// <summary>УДАР КОНЕЧНОСТЬЮ — доставка NPC.
///
/// ЗАЧЕМ. Органы в слоте «Руки» несут удар (`Копыто`, `Коготь`), и у игрока он работает через `PlayerAttack`. У NPC
/// `PlayerAttack` нет — без этой доставки лось не бил бы копытом, а волк когтем: не по замыслу, а потому что бить
/// было нечем.
///
/// ПОЧЕМУ ОБОБЩЁННО, А НЕ «HoofAbility». Слот «Руки» держит РОЛЬ, а не анатомию: у волка там оружие (коготь), у лося
/// опора-оружие (копыто), у человека манипулятор (кисть), у будущей совы — когти на задних, пока крылья работают
/// ходовой. Доставка описывает роль «бью тем, что на конечности», и потому годится всем.
///
/// ВСЕ ЧИСЛА — ИЗ ЗАПИСИ ОРГАНА (<see cref="LimbStrikeData"/>): та же запись и тот же конус, что у удара игрока.
/// Нет записи — удара нет.</summary>
public class LimbStrikeAbility : WindupAbility, IOrganAbility
{
    LimbStrikeData data; // раскрытая запись конечности; null — удара нет

    public System.Type DataType => typeof(LimbStrikeData);
    public void Configure(AbilityData d) => data = d as LimbStrikeData;
    public bool Available => data != null;
    protected override bool Ready => data != null;
    protected override float WindupTime => data.windupTime;

    public override float WindowMax => Range;               // окно арсенала: вплотную — до досягаемости записи
    public float Range => data != null ? data.range : 0f; // психика читает досягаемость; нет удара — 0, в зону не заманит

    protected override float GizmoRange => Range;
    protected override float GizmoHalfAngle => data != null ? data.halfAngle : 0f;

    protected override Color TelegraphColor => TelegraphColors.Kick; // тот же цвет, что пинок игрока

    protected override AbilityRun OnTick()
    {
        float dist = DistToTarget();
        bool inCone = Vector3.Angle(transform.forward, DirToTarget()) <= data.halfAngle;

        // ФАЗА ЗАМАХА: цель ушла — передумал, замах сорван (см. WindupAbility.InWindup)
        if (Time.time < windupEnd)
        {
            if (!(dist <= data.range && inCone)) return AbilityRun.Cancelled;
            SettleInPlace();
            return AbilityRun.Running;
        }

        // КАДР УДАРА: приём закоммичен и бьёт туда, куда заведён. Ушёл вовремя — промах, а не отмена
        if (dist <= data.range && inCone && targetHealth != null)
        {
            var blow = new MeleeBlow { Damage = data.damage, KnockForce = data.knockForce, BleedStacks = data.bleedStacks };
            blow.Deliver(new Hit(ownHealth, transform.position), targetHealth, DamageMult);
        }
        return AbilityRun.Done;
    }
}
