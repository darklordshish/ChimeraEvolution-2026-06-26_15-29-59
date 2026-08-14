using UnityEngine;

/// <summary>
/// БОЕВОЙ КЛИЧ — голос ЧЕЛОВЕКА (Alt, третий в аккорде после воя и рёва). Не контроль и не зов: единственный
/// голос, направленный НА СЕБЯ. Кричишь — рвёшь себе горло (стак крови) и входишь в ярость, тем более злую,
/// чем больше на тебе крови. Держится, пока идёт кровотечение.
///
/// ПОЧЕМУ ИМЕННО ЧЕЛОВЕКУ. Волк воет — зовёт стаю, лось ревёт — пугает; у человека нет ни стаи, ни массы.
/// Его ход — заплатить собой: единственный вид, чей голос ухудшает собственное состояние ради силы. Это же
/// закрывает слот `Рот`, который у человека стоял пустым (`enablesBite = false`) — теперь функционален
/// каждый его слот, и человек перестаёт быть «видом с дырой» в конструкторе.
///
/// ЦЕНА НАСТОЯЩАЯ. Стак крови от крика — такой же, как от волчьих клыков: копится к порогу кровопотери,
/// а ярость вдобавок поднимает входящий урон. Кричать выгодно, когда тебя уже рвут, — и ровно тогда это
/// опаснее всего. Штука решает не «нажать ли», а «сколько ещё терпеть до крика».
/// </summary>
public class PlayerScream : MonoBehaviour, IAbility
{
    [Header("Клич")]
    [SerializeField] float cooldown = 12f;
    [SerializeField] float boostPerStack = 0.12f; // +12% к ярости за каждый стак крови на себе
    [SerializeField] float maxBoost = 2f;         // потолок усиления (кровопотеря сама себя ограничивает)
    [SerializeField] float shake = 0.2f;

    public bool ScreamEnabled { get; set; }       // включает орган `Рот` человека (CreatureBody)

    float nextTime;
    bool screaming;      // клич отзвучал, ярость держится — пока идёт кровь
    Rage rage;
    Bleed bleed;
    Health ownHealth;
    CameraFollow cam;
    Noise noiseSrc;      // крик СЛЫШНО: ось звука, зверьё пойдёт проверять

    void Start()
    {
        TryGetComponent(out rage);
        TryGetComponent(out ownHealth);
        cam = FindAnyObjectByType<CameraFollow>();
    }

    public bool TryUse()
    {
        if (!ScreamEnabled || Time.time < nextTime) return false;
        nextTime = Time.time + cooldown;
        DoScream();
        return true;
    }

    void DoScream()
    {
        // КРОВЬ ДО-СОЗДАЁМ САМИ: у нетронутого игрока компонента ещё нет (его вешают клыки при первом
        // порезе). Свой стак ставим без источника — смерть от собственного крика ничьим убийством не станет
        if (bleed == null && !TryGetComponent(out bleed)) bleed = gameObject.AddComponent<Bleed>();
        bleed.AddStack();

        if (noiseSrc == null) TryGetComponent(out noiseSrc);
        if (noiseSrc != null) noiseSrc.Spike(1f, 1f, TelegraphColors.RageTint); // тон крика — цвет ярости

        screaming = true;
        Sustain();
        if (cam != null) cam.Shake(0.2f, shake);
    }

    /// <summary>Ярость живёт РОВНО СТОЛЬКО, СКОЛЬКО ИДЁТ КРОВЬ, и растёт вместе с ней: добили до пятого
    /// стака — клич злее, кровь спала — ярость гаснет сама. Поэтому продлеваем покадрово, а не выдаём
    /// фиксированный срок: иначе «до конца кровотечения» стало бы «на восемь секунд», и связь потерялась.</summary>
    void Sustain()
    {
        if (rage == null && !TryGetComponent(out rage)) return;
        int stacks = bleed != null ? bleed.Stacks : 0;
        if (stacks <= 0) { screaming = false; return; }
        rage.Enrage(0.25f, Mathf.Min(maxBoost, 1f + boostPerStack * stacks)); // короткими продлениями
    }

    void Update()
    {
        if (screaming) Sustain();
    }
}
