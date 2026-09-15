using UnityEngine;

/// <summary>
/// БОЕВОЙ КЛИЧ — голос ЧЕЛОВЕКА (Alt, третий в аккорде после воя и рёва). Не контроль и не зов: единственный голос,
/// направленный НА СЕБЯ. Кричишь — рвёшь себе горло (стак крови) и входишь в ярость, тем более злую, чем больше на тебе крови.
/// Держится, пока идёт кровотечение.
///
/// ПОЧЕМУ ИМЕННО ЧЕЛОВЕКУ. Волк воет — зовёт стаю, лось ревёт — пугает; у человека нет ни стаи, ни массы. Его ход — заплатить
/// собой. Это же закрывает слот `Рот`, у которого прежде не было фич, — человек перестаёт быть «видом с дырой» в конструкторе.
///
/// ЦЕНА НАСТОЯЩАЯ. Стак крови от крика — такой же, как от волчьих клыков: копится к порогу кровопотери, а ярость вдобавок
/// поднимает входящий урон. Кричать выгодно, когда тебя уже рвут, — и ровно тогда это опаснее всего.
/// ВСЕ ЧИСЛА — ИЗ ЗАПИСИ ОРГАНА (<see cref="ScreamData"/>).
/// </summary>
public class PlayerScream : MonoBehaviour, IAbility, IOrganAbility
{
    [SerializeField, NotOrganData("ощущение: тряска камеры игрока")] float shake = 0.2f;

    ScreamData data; // раскрытая запись клича; null — Рта с кличем нет

    public System.Type DataType => typeof(ScreamData);
    public void Configure(AbilityData d) => data = d as ScreamData;
    public bool Available => data != null;

    float nextTime;
    bool screaming;      // клич отзвучал, ярость держится — пока идёт кровь
    Rage rage;
    Bleed bleed;
    CameraFollow cam;
    Noise noiseSrc;      // крик СЛЫШНО: ось звука, зверьё пойдёт проверять

    void Start()
    {
        TryGetComponent(out rage);
        cam = FindAnyObjectByType<CameraFollow>();
    }

    public bool TryUse()
    {
        if (data == null || Time.time < nextTime) return false;
        nextTime = Time.time + data.cooldown;
        DoScream();
        return true;
    }

    void DoScream()
    {
        // КРОВЬ ДО-СОЗДАЁМ САМИ: у нетронутого игрока компонента ещё нет (его вешают клыки при первом порезе).
        // Свой стак ставим без источника — смерть от собственного крика ничьим убийством не станет
        if (bleed == null && !TryGetComponent(out bleed)) bleed = gameObject.AddComponent<Bleed>();
        bleed.AddStack();

        if (noiseSrc == null) TryGetComponent(out noiseSrc);
        if (noiseSrc != null) noiseSrc.Spike(1f, 1f, TelegraphColors.RageTint); // тон крика — цвет ярости

        screaming = true;
        Sustain();
        if (cam != null) cam.Shake(0.2f, shake);
    }

    /// <summary>Ярость живёт РОВНО СТОЛЬКО, СКОЛЬКО ИДЁТ КРОВЬ, и растёт вместе с ней: добили до пятого стака — клич злее,
    /// кровь спала — ярость гаснет сама. Поэтому продлеваем покадрово, а не выдаём фиксированный срок.</summary>
    void Sustain()
    {
        if (data == null) { screaming = false; return; } // орган сняли посреди клича
        if (rage == null && !TryGetComponent(out rage)) return;
        int stacks = bleed != null ? bleed.Stacks : 0;
        if (stacks <= 0) { screaming = false; return; }
        rage.Enrage(0.25f, Mathf.Min(data.maxBoost, 1f + data.boostPerStack * stacks)); // короткими продлениями
    }

    void Update()
    {
        if (screaming) Sustain();
    }
}
