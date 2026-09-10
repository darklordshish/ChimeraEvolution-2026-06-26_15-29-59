using UnityEngine;

/// <summary>УДАР КОНЕЧНОСТЬЮ — доставка для NPC, которой до сих пор не было.
///
/// ЗАЧЕМ. Органы в слоте «Руки» несут урон (`Копыто` 22, `Коготь` 18), и у ИГРОКА он работает:
/// `CreatureBody` кладёт его в `PlayerAttack.SetMelee`. Но `PlayerAttack` — компонент игрока, у NPC
/// его нет, и урон конечности утекал в переменную, которую некому прочитать. Лось не бил копытом,
/// волк не бил когтем — не по замыслу, а потому что бить было нечем.
///
/// ПОЧЕМУ ОБОБЩЁННО, А НЕ «HoofAbility». Слот «Руки» держит РОЛЬ, а не анатомию: у волка там оружие
/// (коготь), у лося опора-оружие (копыто), у человека манипулятор (кисть), у будущей совы — когти
/// на задних, пока крылья работают ходовой. Доставка описывает роль «бью тем, что на конечности»,
/// и потому годится всем; частная `HoofAbility` потребовала бы близнеца на каждый вид.
///
/// УРОН ПРИХОДИТ ИЗ ТЕЛА, А НЕ ЗАДАЁТСЯ ЗДЕСЬ. `damage = 0` значит «спроси у органа»: психика зовёт
/// `SetDamage` из `OnBodyStats`. Иначе число жило бы и в органе, и в префабе — и разошлось бы молча,
/// как это уже было с длительностью тарана.</summary>
public class LimbStrikeAbility : WindupAbility
{
    [Header("Удар конечностью")]
    [SerializeField] float range = 2.0f;
    [SerializeField] float halfAngle = 60f;   // шире рогов: конечностью машут, а не целятся корпусом
    [SerializeField] int damage;              // 0 = урон берётся у органа через SetDamage
    [SerializeField] float knockForce = 4f;   // толчок, а не отлёт: сшибает с ног таран, не копыто
    [SerializeField] int bleedStacks;         // рассечение — по виду: копыто нет, коготь да

    // Ноль читается как «не настроено» — рецепт проекта против гочи с новым [SerializeField]
    // у компонента, уже лежащего в префабе (иначе конус 0° и промах КАЖДЫЙ раз, молча)
    float Cone => halfAngle > 0f ? halfAngle : 60f;
    float Reach => range > 0f ? range : 2.0f;

    int bodyDamage;
    /// <summary>Урон от органа в слоте конечности. Зовётся психикой из `OnBodyStats`.</summary>
    public void SetDamage(int value) => bodyDamage = value;

    int Damage => damage > 0 ? damage : (bodyDamage > 0 ? bodyDamage : 10);

    public float Range => Reach;

    protected override float GizmoRange => Reach;
    protected override float GizmoHalfAngle => Cone;

    protected override Color TelegraphColor => TelegraphColors.Kick; // тот же цвет, что пинок игрока

    protected override AbilityRun OnTick()
    {
        float dist = DistToTarget();
        bool inCone = Vector3.Angle(transform.forward, DirToTarget()) <= Cone;

        // ФАЗА ЗАМАХА: цель ушла — передумал, замах сорван (см. WindupAbility.InWindup)
        if (Time.time < windupEnd)
        {
            if (!(dist <= Reach && inCone)) return AbilityRun.Cancelled;
            SettleInPlace();
            return AbilityRun.Running;
        }

        // КАДР УДАРА: приём закоммичен и бьёт туда, куда заведён. Ушёл вовремя — промах, а не отмена
        if (dist <= Reach && inCone && targetHealth != null)
        {
            var blow = new MeleeBlow { Damage = Damage, KnockForce = knockForce, BleedStacks = bleedStacks };
            blow.Deliver(new Hit(ownHealth, transform.position), targetHealth, DamageMult);
        }
        return AbilityRun.Done;
    }
}
