using Chimera.Laws;
using UnityEngine;

/// <summary>
/// ИГЛЫ — пассивная ОТВЕТКА на контактный урон: ударил в упор — порезался сам (+кровотечение).
/// Живёт на Шкуре ежа и тем же компонентом придёт игроку как аугумент: «трогаешь — режешься».
///
/// ТОЛЬКО В УПОР (`reach`) — и это не украшение, а суть: яд, кровотечение и будущие снаряды приходят
/// издалека, за них иглы не мстят. Иначе ответка била бы по стрелку через всю арену.
///
/// ЭМЕРДЖЕНТНОЕ СЛЕДСТВИЕ (ради него всё и затевалось): в `Constrict` урон ОТ САМОЙ жертвы не считается
/// спасением-извне, а расшатывает хват (`grip -= dmg * npcLoosenPerDamage`). Значит змея, схватившая
/// ежа, срывается об иглы сама собой — анти-контроль выпадает даром, без единой строчки про захваты.
/// </summary>
[RequireComponent(typeof(Health))]
public class Thorns : MonoBehaviour
{
    // числа (доля ответа 0.55, «в упор» 2.5 м, стаки крови, минимум) — `LawsConfig.thorns`
    static ThornsParams P => LawsConfig.Current.thorns;

    Health health;
    bool reflecting; // ГВАРД ПЕТЛИ: два колючих зверя иначе закололи бы друг друга насмерть рекурсией

    void Awake()
    {
        health = GetComponent<Health>();
        health.onDamaged.AddListener(Reflect);
    }

    void OnDestroy()
    {
        if (health != null) health.onDamaged.RemoveListener(Reflect);
    }

    void Reflect()
    {
        if (reflecting || !isActiveAndEnabled) return; // выключены (ёж «на спине» в истощении) — иглы прижаты, хватают безболезненно

        var attacker = health.LastAttacker;
        if (attacker == null || ReferenceEquals(attacker, health)) return;
        if ((attacker.transform.position - transform.position).sqrMagnitude > P.Reach * P.Reach) return;

        // считаем от СЫРОГО урона: моя броня не должна влиять на то, насколько больно об меня уколоться
        int back = StatusLaw.ThornsReturn(health.LastRawDamage, P.ReturnShare, P.MinReturn);

        reflecting = true;
        var hit = new Hit(health, transform.position);
        hit.Apply(attacker, HitEffect.Damage(back));
        for (int i = 0; i < P.BleedStacks; i++) hit.Apply(attacker, HitEffect.Bleed());
        reflecting = false;
    }
}
