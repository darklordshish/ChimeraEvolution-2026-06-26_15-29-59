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
    [SerializeField] float hpRegen = 4f;       // HP/с пока сыт
    [SerializeField] float staminaRegen = 14f; // стамины/с пока сыт
    // ЖИЗНЬ ШКАЛЫ по ТИРАМ ОДНОРОДНОСТИ (жёсткие рамки, решение пользователя; сглаживание — потом):
    // полная идентичность — 3 мин, средняя — 2, слабая — 1.5, химера — 30 с. Полная→пустая за это время.
    [SerializeField] float pureMinutes = 3f;
    [SerializeField] float mediumMinutes = 2f;
    [SerializeField] float weakMinutes = 1.5f;
    [SerializeField] float chimeraMinutes = 0.5f;
    [SerializeField, Range(0f, 1f)] float startFullness = 0.4f;
    [SerializeField, Range(0f, 1f)] float satedThreshold = 0.5f;    // выше — СЫТ (реген, спокоен)
    [SerializeField, Range(0f, 1f)] float hungryThreshold = 0.25f;  // ниже — ГОЛОДЕН (M3: агрессия/поиск еды)
    [SerializeField, Range(0f, 1f)] float starveThreshold = 0.08f;  // ниже — ИСТОЩЁН (слабеет ВЕЗДЕ)
    [SerializeField, Range(0.3f, 1f)] float starveVigor = 0.65f;    // общий множитель истощённого: ход И урон

    float fullness;
    float decayPerSec; // /с — задаёт тело по однородности (SetMetabolism); дефолт — как у чистого
    Health health;
    Stamina stamina;
    float hpAcc;

    public float Fullness => fullness;
    public float SatedAt => satedThreshold;    // пороги наружу — HUD ставит по ним засечки стадий
    public float HungryAt => hungryThreshold;
    public float StarveAt => starveThreshold;
    public bool IsSated => fullness >= satedThreshold;
    public bool IsHungry => fullness <= hungryThreshold; // M3: голоден — смелее, ищет еду
    public bool IsStarving => fullness <= starveThreshold;
    public float Vigor => IsStarving ? starveVigor : 1f; // ИСТОЩЕНИЕ слабит ВСЁ: ход (локомоции) и урон (DamageMult)

    void Awake()
    {
        health = GetComponent<Health>();
        TryGetComponent(out stamina);
        fullness = startFullness;
        decayPerSec = 1f / (pureMinutes * 60f); // до сборки — как у чистого (NPC однородны и так)
    }

    /// <summary>Метаболизм по ОДНОРОДНОСТИ тела (макс. идентичность 0..1): ступенчато по тирам — чистый
    /// держит сытость дольше, химера сгорает. Тело зовёт из Recompute (меняется со сборкой/родством).</summary>
    public void SetMetabolism(float homogeneity)
    {
        float min = homogeneity >= 0.85f ? pureMinutes    // моно-сет / чистый вид
                  : homogeneity >= 0.70f ? mediumMinutes  // средняя примесь
                  : homogeneity >= 0.55f ? weakMinutes     // слабая идентичность
                  : chimeraMinutes;                        // мешанина — химера
        decayPerSec = 1f / (Mathf.Max(0.1f, min) * 60f);
    }

    /// <summary>Поел (доля насыщения 0..1): убийство/обгладывание наполняет шкалу.</summary>
    public void Feed(float amount) => fullness = Mathf.Clamp01(fullness + Mathf.Max(0f, amount));

    void Update()
    {
        fullness = Mathf.Max(0f, fullness - decayPerSec * Time.deltaTime); // голод растёт (у химеры быстрее — см. тиры)
        if (!IsSated) { hpAcc = 0f; return; }

        // пока СЫТ — доливаем HP и стамину (устойчиво, уроном не сбивается: восстановление, не трапеза)
        if (stamina == null) TryGetComponent(out stamina);
        stamina?.Recover(staminaRegen * Time.deltaTime);

        if (health != null && health.Current < health.Max)
        {
            hpAcc += hpRegen * Time.deltaTime;
            if (hpAcc >= 1f) { int h = (int)hpAcc; hpAcc -= h; health.Heal(h); }
        }
    }
}
