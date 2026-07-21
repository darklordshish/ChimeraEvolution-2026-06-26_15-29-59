using UnityEngine;

/// <summary>
/// Вой — СТРАШНЫЙ звук хищника (Alt / правый шифтер), два кольца: БЛИЖНИЕ оглохли (стан ≥1с — окно
/// действий), ДАЛЬНИЕ в испуге разбегаются (страх; ярость не боится). Фича волчьей Пасти
/// (CreatureBody выставляет HowlEnabled). Урона нет. Активное УДЕРЖАНИЕ захвата воем не рвётся —
/// только пинок/рывок (но обхват ЗМЕИ вой рвёт на ст.1–2 — её собственный стан).
/// </summary>
public class PlayerHowl : MonoBehaviour, IAbility
{
    [Header("Вой")]
    [SerializeField] float radius = 7f;
    [SerializeField] float stunDuration = 1f;   // СТАН (контроль ≥1с): вырубает ближних на окно действий
    [SerializeField] float fearRadius = 14f;    // дальнее кольцо (radius..fearRadius): испуг — удар по морали
    [SerializeField] float fearMoraleHit = 2f;  // −вклад шкалы морали; × бонус органов (родство): до −4 на сотке (почти вожак)
    [SerializeField] float cooldown = 8f;
    [SerializeField] float shake = 0.3f;

    public bool HowlEnabled { get; set; } // включается волчьей Пастью (CreatureBody)
    public bool StunUnlocked { get; set; } // ПОРОГ-ФИЧА: стан открыт, только если мощь доросла до порога Пасти
                                           // (тело считает, см. CreatureBody.HowlStuns). Ниже порога — голос без глушения

    /// <summary>ГОЛОС — от данных: тело отдаёт радиус (органная база × мощь-превосходство). Стан = половина.</summary>
    public void SetReach(float reach)
    {
        if (reach <= 0.01f) return; // Пасть без голоса — остаёмся на сериализованных дефолтах
        fearRadius = reach;
        radius = reach * 0.5f;
    }

    float nextTime;
    CameraFollow cam;
    Health ownHealth;
    CreatureBody body; // бонус органов (родство) масштабирует вес воя по морали
    PlayerBellow bellowMate; // вторая глотка (аккорд): стан расширяется до большего из радиусов голосов
    Noise noiseSrc; // источник звука (вешает тело): вой игрока звучит в мире (ось Noise) — лось услышит

    void Start()
    {
        cam = FindAnyObjectByType<CameraFollow>();
        ownHealth = GetComponent<Health>();
        TryGetComponent(out body);
    }

    // водитель зовёт по вводу; активен только с волчьей Пастью; кулдаун проверяем сами
    public bool TryUse()
    {
        if (!HowlEnabled || Time.time < nextTime) return false;
        nextTime = Time.time + cooldown;
        DoHowl();
        return true;
    }

    void DoHowl()
    {
        if (noiseSrc == null) TryGetComponent(out noiseSrc);
        if (noiseSrc != null) noiseSrc.Spike(1f, 0.8f); // вой ЗВУЧИТ (Noise): в призраке Hear сам глушит (беззвучен)
        // призрака раскрывает ЗАДЕТЫЙ воем (стан через Hit.Apply / испуг ниже), не вой в пустоту
        var hit = new Hit(ownHealth, transform.position);

        // радиусы уже ТЕЛЕСНЫЕ: тело отдало орган × мощь через SetReach (на сотке стан 7→14, страх 14→28).
        // АККОРД двух глоток (правило супремума): вторая глотка (рёв) расширяет стан до большего радиуса
        if (bellowMate == null) TryGetComponent(out bellowMate);
        float stunR = radius, fearR = fearRadius;
        if (bellowMate != null && bellowMate.BellowEnabled) stunR = Mathf.Max(stunR, bellowMate.FearRadius);

        foreach (var hp in TargetScan.Healths(transform.position, fearR, transform))
        {
            // K3: ПРИЗНАНИЕ ПЕРЕВОРАЧИВАЕТ ЗНАК ГОЛОСА (спека идентичности §3). Кин-цель (моя идентичность
            // к ЕЁ виду ≥ слабого) вместо контроля получает RALLY: дух по градации признания (слабое +1,
            // среднее +2 + стирание страхов, сильное +5 — голос вожака) и точку сбора. Чужим — как раньше
            var kinTier = KinTier.None;
            if (body != null && hp.TryGetComponent<CreatureBody>(out var targetBody) && targetBody.Chassis != null)
                kinTier = body.Tier(targetBody.Chassis);

            if (kinTier != KinTier.None)
            {
                // ЕДИНЫЙ кин-rally («эффект в органе, родство в комбинации»): волк — дух+эскорт,
                // лось — детонация; вой при лосином ките сзывает ЛОСЕЙ — кросс-опыление спеки
                KinVoice.RallyKin(hp, kinTier, transform.position);
                continue; // своих не контролим; бесморальные — no-op эмерджентно (спека §4)
            }

            float d = Vector3.Distance(hp.transform.position, transform.position);
            if (StunUnlocked && d <= stunR) hit.Apply(hp, HitEffect.Stun(stunDuration)); // ближние ЧУЖИЕ оглохли (если мощь доросла)
            else if (hp.TryGetComponent<WolfPsyche>(out var w))
            {
                // удар по морали дальнего кольца: вес растёт с родством (бонус органов ×1..×2 → −2..−4)
                w.Frighten(fearMoraleHit * (body != null ? body.BonusMult : 1f));
                Perception.BreakGhost();  // напугал — воздействие: призрак раскрыт
            }
        }
        if (cam != null) cam.Shake(0.15f, shake); // визуальный сигнал воя (VFX/звук — потом)
    }

    void OnDrawGizmos()
    {
        if (!HowlEnabled) return;
        Gizmos.color = TelegraphColors.Howl;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
