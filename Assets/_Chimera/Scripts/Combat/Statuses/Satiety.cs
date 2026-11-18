using Chimera.Laws;
using UnityEngine;

/// <summary>
/// СЫТОСТЬ↔ГОЛОД — ШКАЛА-ДВИЖИТЕЛЬ (0 истощён … 1 сыт). Одна ось, три ступени: СЫТ (реген HP+стамины,
/// спокоен) → ГОЛОДЕН (реген нет, лезет на рожон, ищет еду — M3) → ИСТОЩЁН (слабеет: медленнее двигается).
/// Поел — полная, тает со временем. Источник еды разный, статус один: хищник наполняет УБИЙСТВОМ
/// (`CreatureBody.CreditKiller`), травоядный — ОБГЛАДЫВАНИЕМ (кормёжка, M2).
///
/// МЕТАБОЛИЗМ ЗАВИСИТ ОТ ОДНОРОДНОСТИ (идея пользователя — цена химеризации): чистый вид держит сытость
/// долго, ХИМЕРА сгорает быстро (тело нестабильно, никто не кин → жрать чаще). Тело задаёт множитель
/// распада по идентичности (`SetMetabolism`); NPC однородны → медленный распад, «утекало на глазах»
/// только у смешанного игрока.
///
/// Дыхалку одну восстанавливать смысла нет (вне боя не чувствуется), а в связке с HP работает. Тело =
/// шкала (физиология), психика = поведение (змея прячется на насест по IsSated; M3 — голод-агрессия).
/// </summary>
[RequireComponent(typeof(Health))]
public class Satiety : MonoBehaviour
{
    // ЧИСЛА — `LawsConfig.hunger`: реген HP и стамины пока сыт; ЖИЗНЬ ШКАЛЫ по ТИРАМ ОДНОРОДНОСТИ (жёсткие рамки, решение
    // пользователя; сглаживание — потом): полная идентичность — 3 мин, средняя — 2, слабая — 1.5, химера — 30 с;
    // пороги СЫТ 0.5 / ГОЛОДЕН 0.25 / ИСТОЩЁН 0.08; вигор истощённого 0.65 (ход И урон). Шаги — `HungerLaw`.
    static HungerParams P => LawsConfig.Current.hunger;

    float fullness;
    float decayPerSec; // /с — задаёт тело по однородности (SetMetabolism); дефолт — как у чистого
    Health health;
    Stamina stamina;
    float hpAcc;

    public float Fullness => fullness;
    public float SatedAt => P.SatedThreshold;    // пороги наружу — HUD ставит по ним засечки стадий
    public float HungryAt => P.HungryThreshold;
    public float StarveAt => P.StarveThreshold;
    public bool IsSated => HungerLaw.IsSated(fullness, P);
    public bool IsHungry => HungerLaw.IsHungry(fullness, P); // M3: голоден — смелее, ищет еду
    public bool IsStarving => HungerLaw.IsStarving(fullness, P);
    public float Vigor => HungerLaw.Vigor(fullness, P); // ИСТОЩЕНИЕ слабит ВСЁ: ход (локомоции) и урон (DamageMult)

    void Awake()
    {
        health = GetComponent<Health>();
        TryGetComponent(out stamina);
        fullness = P.StartFullness;
        decayPerSec = HungerLaw.DefaultDecayPerSec(P); // до сборки — как у чистого (NPC однородны и так)
    }

    /// <summary>Метаболизм по ОДНОРОДНОСТИ тела (макс. идентичность 0..1): ступенчато по тирам — чистый
    /// держит сытость дольше, химера сгорает. Тело зовёт из Recompute (меняется со сборкой/родством).</summary>
    public void SetMetabolism(float homogeneity) => decayPerSec = HungerLaw.DecayPerSec(homogeneity, P);

    /// <summary>Поел (доля насыщения 0..1): убийство/обгладывание наполняет шкалу.</summary>
    public void Feed(float amount) => fullness = HungerLaw.Feed(fullness, amount);

    void Update()
    {
        fullness = HungerLaw.Decay(fullness, decayPerSec, Time.deltaTime); // голод растёт (у химеры быстрее — см. тиры)
        if (!IsSated) { hpAcc = 0f; return; }

        // пока СЫТ — доливаем HP и стамину (уроном не сбивается: восстановление, не трапеза; HP — сквозь подавление регена)
        if (stamina == null) TryGetComponent(out stamina);
        stamina?.Recover(P.StaminaRegen * Time.deltaTime);

        if (health != null && health.Current < health.Max)
        {
            // яд и укус Пасти глушат и еду (`Health.RegenFactor`)
            int h = HungerLaw.RegenTick(ref hpAcc, P.HpRegen * health.RegenFactor, Time.deltaTime);
            if (h > 0) health.Heal(h);
        }
    }
}
