using UnityEngine;

/// <summary>
/// Доставка «удар рогами»: короткий замах (телеграф) → удар по близкой цели: урон + Knockback (резист Massive внутри
/// Knockback.Push) + Bleed (протыкание, стаки). Наказывает липнущих вплотную — кого таран не достаёт (dist < разбег).
/// Замах прерывается стаггером (базовый Abort, в отличие от закоммиченного тарана).
/// ВСЕ ЧИСЛА — ИЗ ЗАПИСИ ОРГАНА «Рога» (<see cref="AntlerData"/>, спека «данные в органах»): нет записи — рогов нет.
/// </summary>
public class AntlerAbility : WindupAbility, IOrganAbility
{
    AntlerData data; // раскрытая запись рогов; null — рогов нет

    public System.Type DataType => typeof(AntlerData);
    public void Configure(AbilityData d) => data = d as AntlerData;
    public bool Available => data != null;
    protected override bool Ready => data != null;
    protected override float WindupTime => data.windupTime;

    // психика читает дистанцию удара; нет рогов — 0: в зону атаки не заманит
    public override float WindowMax => Range;               // окно арсенала: вплотную — до досягаемости записи
    public float Range => data != null ? data.range : 0f;
    public float HalfAngle => data != null ? data.halfAngle : 0f;

    protected override float GizmoRange => Range; // хитбокс — ближний радиус рогов
    protected override float GizmoHalfAngle => HalfAngle;

    protected override Color TelegraphColor => TelegraphColors.Antler; // рога — свой цвет (протыкание ≠ таран)

    protected override AbilityRun OnTick()
    {
        float dist = DistToTarget();
        bool inCone = Vector3.Angle(transform.forward, DirToTarget()) <= data.halfAngle;

        // ФАЗА ЗАМАХА: цель вышла из зоны или из конуса — лось ПЕРЕДУМАЛ, замах сорван.
        if (Time.time < windupEnd)
        {
            if (!(dist <= data.range && inCone)) return AbilityRun.Cancelled;
            SettleInPlace();
            return AbilityRun.Running;
        }

        // КАДР УДАРА: приём закоммичен и бьёт ТУДА, КУДА БЫЛ ЗАВЕДЁН. Ушёл вовремя — рога проходят мимо,
        // и это ПРОМАХ, а не отмена. Разница не косметическая: отмена возвращает лося в готовность драться
        // и уворот ничего игроку не даёт, а промах оставляет зверя раскрытым — ровно та награда за тайминг,
        // ради которой уворот и существует. Прежняя проверка стояла ДО этой развилки и била по обеим фазам
        if (dist <= data.range && inCone && targetHealth != null)
        {
            // единый паёк рогов (см. MeleeBlow) — тот же удар льёт и игрок; мощь NPC масштабирует урон
            var blow = new MeleeBlow { Damage = data.damage, KnockForce = data.knockForce, BleedStacks = data.bleedStacks };
            blow.Deliver(new Hit(ownHealth, transform.position), targetHealth, DamageMult);
        }
        return AbilityRun.Done;
    }
}
