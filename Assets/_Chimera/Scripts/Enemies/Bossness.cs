using System.Collections.Generic;
using UnityEngine;

/// <summary>БОССОВОСТЬ — отдельный модуль поверх обычной химеры (слайс #4b-4). Босс собирается ТЕМ ЖЕ
/// конструктором, что все (`ChimeraFactory`), а модуль тело не строит — он его РАСШИРЯЕТ:
///   • химерными слотами сверх нормы, в которые чужие органы ставятся тем же `Install`, что у игрока;
///   • раскрытием генов органов (экспрессия) до потолка игрока;
///   • чертами туши (масса, ярость), размером и наградой за первое убийство;
///   • АТАВИЗМАМИ ВЕРВОЛЬФА — вампиризмом, временными HP и приказом вожака. Жили в общих системах без носителя;
///     геймдизайнер 11.09: «механики обдумаем, когда будут виды». По умолчанию выключены.
/// То, что органом не выражается (множители HP и урона), живёт здесь же, а тело и доставки опрашивают модуль,
/// как статус — поэтому бонус переживает любой `Recompute`, а не стирается следующей прививкой.
///     Сценарный закон (геймдизайнер, 11.09): босс — всегда химера ЧЕЛОВЕКА с кем-то. Шасси выбирает спавнер.</summary>
public class Bossness : MonoBehaviour
{
    [System.Serializable]
    public class Settings
    {
        [Tooltip("Расширение конструктором: химерных слотов сверх нормы, в каждый — чужой орган. 2 — потолок, при котором " +
                 "оборотень-волк остаётся волком; 3 размывают его в истинную химеру всегда (счёт 11.09, сторож — BossSlotsIdentityTests)")]
        [Min(0)] public int chimeraSlots = 2;
        [Tooltip("Раскрытие генов органов. 2 = потолок игрока на 100 родства; 0 = как у рядовой химеры")]
        [Min(0f)] public float expression = 2f;
        [Tooltip("Множитель HP — то, что органом не выражается")]
        [Min(0.1f)] public float hpMult = 2f;
        [Tooltip("Множитель урона всех приёмов")]
        [Min(0.1f)] public float damageMult = 1.25f;
        [Tooltip("Размер — читается боссом издали")]
        [Min(0.1f)] public float sizeScale = 1.3f;
        [Tooltip("Масса: не отбрасывается, обхват по нему на стадию слабее")]
        public bool massive = true;
        [Tooltip("Вечная ярость: быстрее и больнее бьёт, но и сам получает больше")]
        public bool eternalRage;
        [Tooltip("Награда за ПЕРВОЕ убийство этого типа босса: расширение пула")]
        [Min(0)] public int poolReward = 4;
        [Tooltip("Награда за первое убийство: химерный слот игроку")]
        public bool grantsChimeraSlot = true;
        [Tooltip("Вампиризм: HP за каждое попадание любым приёмом. 0 — выключен (атавизм вервольфа, механика не решена)")]
        [Min(0)] public int lifeSteal;
        [Tooltip("Временные HP: на сколько вампиризм может поднять свыше максимума. 0 — не выше максимума")]
        [Min(0)] public int overheal;
    }

    [SerializeField] Settings settings = new();

    // 0-гоча сериализации: ноль в старом объекте читаем как «не настроено», а не как «убить множителем»
    public float HpMult => settings.hpMult > 0f ? settings.hpMult : 1f;
    public float DamageMult => settings.damageMult > 0f ? settings.damageMult : 1f;
    public int LifeSteal => settings.lifeSteal; // доставки опрашивают, как множитель урона
    public string TypeId { get; private set; } = "Суперхимера";

    /// <summary>ШАГ 1 — до доставок и тела: сам модуль и черты, которые доставки кэшируют в Awake (ярость).
    /// Настройки КОПИРУЮТСЯ: спавнер держит их в инспекторе, и общий объект у всех боссов значил бы, что правка
    /// в Play задним числом меняет уже живых.</summary>
    public static Bossness AttachBefore(GameObject go, Settings source)
    {
        var b = go.AddComponent<Bossness>();
        b.settings = source != null ? JsonUtility.FromJson<Settings>(JsonUtility.ToJson(source)) : new Settings();
        if (b.settings.massive && !go.TryGetComponent<Massive>(out _)) go.AddComponent<Massive>();
        if (b.settings.eternalRage && !go.TryGetComponent<Rage>(out _)) go.AddComponent<Rage>();
        return b;
    }

    /// <summary>ШАГ 2 — внутри сборки, после рецепта состава: расширение конструктором и раскрытие генов.</summary>
    public void Extend(CreatureBody body)
    {
        ChimeraFactory.GrantAndFillChimeraSlots(body, settings.chimeraSlots);
        if (settings.expression > 0f) body.SetExpression(settings.expression);
    }

    /// <summary>ШАГ 3 — после сборки и психики: размер, ярость, награда.</summary>
    public void Finish(CreatureBody body, string typeId)
    {
        TypeId = string.IsNullOrEmpty(typeId) ? TypeId : typeId;
        // ШТУЧНЫЙ: разброс особи тело вешает каждому NPC, а решение Ф6 — босс, как игрок, без разброса. Без этого
        // два одинаковых босса расходились по HP, урону и скорости на ±15%
        if (TryGetComponent<SpawnVariance>(out var variance)) { variance.MakeUnique(); body.Refeed(); }
        if (!Mathf.Approximately(settings.sizeScale, 1f)) transform.localScale = Vector3.one * settings.sizeScale;
        if (settings.eternalRage && TryGetComponent<Rage>(out var rage)) rage.Enrage(float.PositiveInfinity);
        if (!TryGetComponent<SuperBossReward>(out var reward)) reward = gameObject.AddComponent<SuperBossReward>();
        reward.Configure(TypeId, settings.poolReward, settings.grantsChimeraSlot);
        if (settings.overheal > 0 && TryGetComponent<Health>(out var hp)) hp.OverhealCap = settings.overheal;
    }

    /// <summary>ПРИКАЗ ВОЖАКА — задел механики босса, вызывать пока некому. Своих рядом узнаёт по составу, тем же
    /// голосом кина, что у игрока (`KinVoice`): сильное признание даёт +5 духа и снимает бегство. Жил в
    /// `PackCoordinator.Rally` вместе с окном «наваливается вся стая» и после удаления вервольфа не вызывался.</summary>
    public int OrderKin(float radius)
    {
        if (!TryGetComponent<CreatureBody>(out var body)) return 0;
        var heard = new HashSet<Health>(); // у существа несколько коллайдеров — приказ слышат один раз
        foreach (var col in Physics.OverlapSphere(transform.position, radius, ~0, QueryTriggerInteraction.Ignore))
        {
            var hp = col.GetComponentInParent<Health>();
            if (hp == null || hp.gameObject == gameObject || !heard.Add(hp)) continue;
            KinVoice.TryRallyKin(body, hp, transform.position);
        }
        return heard.Count;
    }

    public float SizeScale => settings.sizeScale > 0f ? settings.sizeScale : 1f;
}
