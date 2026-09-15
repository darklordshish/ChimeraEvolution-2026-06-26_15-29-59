using UnityEngine;

/// <summary>
/// Вой — СТРАШНЫЙ звук хищника (Alt / правый шифтер), два кольца: БЛИЖНИЕ оглохли (стан ≥1 с — окно действий), ДАЛЬНИЕ в испуге
/// разбегаются (страх; ярость не боится). Урона нет. Активное УДЕРЖАНИЕ захвата воем не рвётся — только пинок/рывок (но обхват
/// ЗМЕИ вой рвёт на ст.1–2 — её собственный стан).
/// ВСЕ ЧИСЛА — ИЗ ЗАПИСИ ОРГАНА (<see cref="HowlData"/>), вместе с законом голоса: радиус × мощь не ниже базы, стан — порогом мощи.
/// </summary>
public class PlayerHowl : MonoBehaviour, IAbility, IOrganAbility
{
    [SerializeField, NotOrganData("ощущение: тряска камеры игрока")] float shake = 0.3f;

    HowlData data; // раскрытая запись воя; null — Пасть не воет

    public System.Type DataType => typeof(HowlData);
    public void Configure(AbilityData d) => data = d as HowlData;
    public bool Available => data != null;

    float nextTime;
    CameraFollow cam;
    Health ownHealth;
    CreatureBody body;       // состав зовущего решает, кого вой созывает (KinVoice)
    PlayerBellow bellowMate; // вторая глотка (аккорд): стан расширяется до большего из радиусов голосов
    Noise noiseSrc;          // источник звука (вешает тело): вой игрока звучит в мире (ось Noise) — лось услышит

    void Start()
    {
        cam = FindAnyObjectByType<CameraFollow>();
        ownHealth = GetComponent<Health>();
        TryGetComponent(out body);
    }

    // водитель зовёт по вводу; доступность и перезарядка — из записи органа
    public bool TryUse()
    {
        if (data == null || Time.time < nextTime) return false;
        nextTime = Time.time + data.cooldown;
        DoHowl();
        return true;
    }

    void DoHowl()
    {
        if (ownHealth == null) ownHealth = GetComponent<Health>(); // вой до нашего Start
        if (body == null) TryGetComponent(out body);
        if (noiseSrc == null) TryGetComponent(out noiseSrc);
        if (noiseSrc != null) noiseSrc.Spike(1f, 0.8f, TelegraphColors.Howl); // вой ЗВУЧИТ (Noise): в призраке Hear сам глушит (беззвучен)
        // призрака раскрывает ЗАДЕТЫЙ воем (стан через Hit.Apply / испуг ниже), не вой в пустоту
        var hit = new Hit(ownHealth, transform.position);

        // КОЛЬЦА — из записи по закону голоса: дальнее = Reach, ближнее (стан) = половина.
        // АККОРД двух глоток (правило супремума): вторая глотка (рёв) расширяет стан до большего радиуса
        if (bellowMate == null) TryGetComponent(out bellowMate);
        float fearR = data.Reach, stunR = fearR * 0.5f;
        if (bellowMate != null && bellowMate.Available) stunR = Mathf.Max(stunR, bellowMate.FearRadius);
        float moraleHit = data.fearMoraleHit * Mathf.Max(1f, data.power); // вес страха растёт с мощью органа

        foreach (var hp in TargetScan.Healths(transform.position, fearR, transform))
        {
            // K3: ПРИЗНАНИЕ ПЕРЕВОРАЧИВАЕТ ЗНАК ГОЛОСА (спека §3). Кин-цель (моя идентичность к её виду ≥ слабого) вместо контроля
            // получает RALLY (волк — дух+эскорт, лось — детонация; вой при лосином ките сзывает ЛОСЕЙ). Чужим — стан/страх ниже
            if (KinVoice.TryRallyKin(body, hp, transform.position)) continue; // свой созван, не контролим

            float d = Vector3.Distance(hp.transform.position, transform.position);
            if (data.Stuns && d <= stunR) hit.Apply(hp, HitEffect.Stun(data.stunDuration)); // ближние ЧУЖИЕ оглохли (если мощь доросла)
            else if (hp.TryGetComponent<WolfPsyche>(out var w))
            {
                w.Frighten(moraleHit); // удар по морали дальнего кольца
                Perception.BreakGhost(); // напугал — воздействие: призрак раскрыт
            }
        }
        if (cam != null) cam.Shake(0.15f, shake); // визуальный сигнал воя (VFX/звук — потом)
    }

    void OnDrawGizmos()
    {
        if (data == null) return;
        Gizmos.color = TelegraphColors.Howl;
        Gizmos.DrawWireSphere(transform.position, data.Reach * 0.5f);
    }
}
