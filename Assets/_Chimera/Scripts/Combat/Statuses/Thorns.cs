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
    [SerializeField, Range(0f, 1f)] float returnShare = 0.55f; // доля СЫРОГО урона обратно. 0.35 не тянуло матч
                                                               // против змеи (укус 24 > ежиный 14): теперь её
                                                               // же крупный укус возвращается ~13 — большой удар
                                                               // об иглы наказывается сильнее, ёж выигрывает размен
    [SerializeField] float reach = 2.5f;                       // «в упор»: дальше иглы не достают
    [SerializeField] int bleedStacks = 1;                      // протыкание — стаки кровотечения атакующему
    [SerializeField] int minReturn = 1;                        // даже слабый тычок должен колоть

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
        if (reflecting) return;

        var attacker = health.LastAttacker;
        if (attacker == null || ReferenceEquals(attacker, health)) return;
        if ((attacker.transform.position - transform.position).sqrMagnitude > reach * reach) return;

        // считаем от СЫРОГО урона: моя броня не должна влиять на то, насколько больно об меня уколоться
        int back = Mathf.Max(minReturn, Mathf.RoundToInt(health.LastRawDamage * returnShare));

        reflecting = true;
        var hit = new Hit(health, transform.position);
        hit.Apply(attacker, HitEffect.Damage(back));
        for (int i = 0; i < bleedStacks; i++) hit.Apply(attacker, HitEffect.Bleed());
        reflecting = false;
    }
}
