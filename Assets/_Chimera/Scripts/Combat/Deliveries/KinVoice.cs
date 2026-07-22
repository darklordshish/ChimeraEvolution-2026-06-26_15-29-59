using UnityEngine;

/// <summary>
/// ЕДИНЫЙ КИН-RALLY вокализов (спека идентичности §3, принцип пользователя «эффект в органе,
/// родство в комбинации»): КАКОЙ орган звучит — решает контроль по чужим (вой — стан, рёв — страх);
/// КОГО зовёшь — решает СОСТАВ тела, и rally-форма своя у ВИДА цели, одинаковая для любого голоса:
///  • волк (стайный) — дух по градации признания (+1/+2/+5), уверенное признание стирает страхи,
///    вектор на зовущего + ПРИЗЫВ В ЭСКОРТ (идут за тобой);
///  • лось (одиночка) — ДЕТОНАЦИЯ на месте (берсерк на ближайшую угрозу; зовущий-кин исключён);
///  • бесморальные (змея) — не за что зацепиться: no-op эмерджентно (спека §4).
/// Кросс-опыление из спеки бесплатно: лосиное тело + волчий вой → сзывает ЛОСЕЙ.
/// </summary>
public static class KinVoice
{
    /// <summary>Признать цель голосом `caster` и, если это кин (≥ слабого), СОЗВАТЬ её. Возвращает true =
    /// цель своя (контроль по ней не применять). Схлопывает дословный блок «вычислить кин-тир → если кин,
    /// RallyKin и continue», повторявшийся в КАЖДОМ голосе (вой/рёв).</summary>
    public static bool TryRallyKin(CreatureBody caster, Health target, Vector3 casterPos)
    {
        if (target == null || !target.TryGetComponent<CreatureBody>(out var tb)) return false;
        var tier = CreatureBody.Regard(caster, tb);
        if (tier == KinTier.None) return false;
        RallyKin(target, tier, casterPos);
        return true;
    }

    public static void RallyKin(Health target, KinTier tier, Vector3 casterPos)
    {
        if (target.TryGetComponent<WolfPsyche>(out var wolf))
        {
            wolf.Cheer(tier == KinTier.Strong ? 5f : tier == KinTier.Medium ? 2f : 1f);
            if (tier >= KinTier.Medium) wolf.CalmRout(); // уверенное признание — голос вожака, страхи стёрты
            wolf.Hear(casterPos);
            wolf.FollowKin();
        }
        else if (target.TryGetComponent<MoosePsyche>(out var moose))
        {
            moose.Provoke(); // детонация: одиночка не эскортится — взрывается там, где стоит
        }
    }
}
