using UnityEngine;

/// <summary>
/// КРОВОТЕЧЕНИЕ — накопительный статус-эффект (эталон — Venom). Стаки набегают от порезов (`HitEffect.Bleed`,
/// сигнатура волчьих клыков), выцветают за stackDuration без нового пореза. Перевалил ПОРОГ (bleedThreshold) →
/// кровопотеря: урон = % от МАКС HP за тик (масштаб-независимо — и на игроке, и на лосе, и на боссе одинаково
/// больно; «чем больше режут — тем сильнее истекаешь»). Держи кровотечение свежими порезами — иначе стаки спадут.
/// До-создаётся на цели при первом порезе (параметры — свойство КЛЫКОВ, не жертвы; дефолты ок).
/// </summary>
public class Bleed : MonoBehaviour
{
    [SerializeField] int bleedThreshold = 5;              // стаков, после которых начинается кровопотеря
    [SerializeField] int maxStacks = 8;
    [SerializeField] float stackDuration = 3f;            // сколько живёт стак без нового пореза
    [SerializeField, Range(0f, 1f)] float pctPerTick = 0.04f; // урон = доля МАКС HP за тик
    [SerializeField] float tickInterval = 0.6f;

    int stacks;
    float expireAt, nextTick;
    Health health;
    Health source; // кто порезал: смерть от кровопотери — на счету источника (родство убийце)

    public int Stacks => Time.time < expireAt ? stacks : 0;

    void Awake() => TryGetComponent(out health);

    BleedResist resist; // КРОВЕУПОРНОСТЬ (сердце лося): не блокирует порез, а укорачивает жизнь стака

    public void AddStack()
    {
        if (resist == null) TryGetComponent(out resist); // ленивая привязка (маркер вешает тело в Recompute)
        stacks = Mathf.Min(maxStacks, Stacks + 1);
        // у кровеупорного рана затягивается быстро: порез проходит, но кровь НЕ КОПИТСЯ до порога кровопотери
        expireAt = Time.time + stackDuration * (resist != null ? resist.DurationMult : 1f);
    }

    public void SetSource(Health s) => source = s;

    void Update()
    {
        if (Stacks < bleedThreshold || health == null) return; // кровопотеря — только за порогом
        if (Time.time >= nextTick)
        {
            nextTick = Time.time + tickInterval;
            if (source != null) health.LastAttacker = source;               // смерть от кровопотери — убийство источника
            health.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(health.Max * pctPerTick)), true); // % HP, минует i-frames
        }
    }
}
