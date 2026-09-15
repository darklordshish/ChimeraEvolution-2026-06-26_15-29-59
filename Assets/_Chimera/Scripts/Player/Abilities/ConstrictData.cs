using System;
using UnityEngine;

/// <summary>
/// ЗАХВАТ — запись грэпл-органа: Хвост змеи (удушение до партера), Пасть волка и Цепкая пасть ежа (плоский пин ст.1).
/// Одна запись кормит машину `Constrict` у NPC (психика решает «когда» и «куда», машина держит) и драйвер игрока
/// `PlayerConstrict` (он оборачивает ту же машину). Блоки «Стадии»…«Дыхалка» читает машина у всех; блок «Игрок» —
/// только драйвер игрока: подбор в упор, волочение, своё замедление.
///
/// КАП СТАДИИ — вторая ось экспрессии (`Organ.nativeChassis`): дома орган держит на полную `maxStage`, на чужом
/// шасси — не выше `foreignMaxStage` (<see cref="OnForeignChassis"/>). При двух грэпл-органах берётся сильнейший
/// (супремум). Massive-жертва — на стадию слабее (правило машины).
///
/// СТАДИИ (спеки: единый захват 2026-07-17, хвост-эталон 2026-07-19):
///  ст.1 — слабый хват: жертва дерётся (её урон откатывает сжатие) и вырывается по таймеру (гонка);
///  ст.2 — защёлк: стан, сама не выйдет;
///  ст.3 — партер: хват до предела + удушение; жертва только зовёт своих.
/// </summary>
[Serializable]
public class ConstrictData : AbilityData
{
    public override Type NpcCarrier => typeof(Constrict);
    public override Type PlayerCarrier => typeof(PlayerConstrict);

    [Header("Стадии — сила захвата органа")]
    [Tooltip("Родная сила захвата, 1–3: Хвост змеи 3 (партер с удушением), челюсти волка и ежа 1 (плоский пин).")]
    [Range(1, 3), MustBePositive] public int maxStage = 3;

    [Tooltip("Кап стадии на ЧУЖОМ шасси (орган надет не на своё `nativeChassis`). Партер — только дома: хвост на человеке держит до защёлка.")]
    [Range(1, 3), MustBePositive] public int foreignMaxStage = 2;

    [Header("Сжатие — машина")]
    [Tooltip("Рост сжатия в секунду.")]
    [MustBePositive] public float tightenRate = 1f;

    [Tooltip("Сжатие, с которого ст.2 (защёлк). Ниже порог — сильнее захват.")]
    [LowerIsBetter, MustBePositive] public float stage2At = 1.5f;

    [Tooltip("Сжатие, с которого ст.3 (партер + удушение).")]
    [LowerIsBetter, MustBePositive] public float stage3At = 3.5f;

    [Header("Жертва-игрок — машина")]
    [Tooltip("Насколько 1 HP урона от жертвы-игрока откатывает сжатие. Контр-игра игрока: бей держащего — ослабишь хват.")]
    [LowerIsBetter, Min(0f)] public float loosenPerDamage = 0.12f;

    [Tooltip("Урон удушения игрока за тик на ст.3. Минует i-frames: рывком из удушения не спрятаться.")]
    [Expressed, Min(0)] public int chokeDamage = 4;

    [Tooltip("Интервал удушения игрока, с.")]
    [LowerIsBetter, MustBePositive] public float chokeInterval = 0.5f;

    [Tooltip("Множитель хода и рывка жертвы-игрока на ст.1. Меньше — сильнее слоу.")]
    [LowerIsBetter, Range(0f, 1f)] public float grabSlow1 = 0.35f;

    [Tooltip("То же на ст.2.")]
    [LowerIsBetter, Range(0f, 1f)] public float grabSlow2 = 0.15f;

    [Tooltip("То же на ст.3: 0 — полный корень.")]
    [LowerIsBetter, Range(0f, 1f)] public float grabSlow3 = 0f;

    [Header("Жертва-NPC — машина")]
    [Tooltip("Откат сжатия за 1 HP урона от NPC-жертвы. Слабее, чем у игрока: одиночка гонку не выигрывает.")]
    [LowerIsBetter, Min(0f)] public float npcLoosenPerDamage = 0.04f;

    [Tooltip("Урон удушения NPC за тик на ст.3. Баланс «одна змея душит волка».")]
    [Expressed, Min(0)] public int npcChokeDamage = 6;

    [Tooltip("Интервал удушения NPC, с.")]
    [LowerIsBetter, MustBePositive] public float npcChokeInterval = 0.6f;

    [Tooltip("Ст.1: NPC-жертва вырывается через случайное время из [min, max], с. Окно позже защёлка — успей дожать. Жертва-игрок таймером не вырывается: у него свой срыв рывком.")]
    [Min(0f)] public float escapeMin = 2.6f;

    [Tooltip("Верх окна вырывания NPC-жертвы, с.")]
    [MustBePositive] public float escapeMax = 4f;

    [Header("Срыв спасателем — машина")]
    [Tooltip("Внешний СЫРОЙ удар по держащему не меньше порога сбивает стадию (3→2→1→сорван). Сырой — броня держащего не «помогает держать». При 5 тик яда (3) хват не сбивает, укус волка — сбивает. Урон самой жертвы срывом не считается — это её контр-игра через откат сжатия.")]
    [Min(0)] public int breakRawThreshold = 5;

    [Header("Клыки — машина")]
    [Tooltip("Разовая кровь жертве на входе в хват: зубы сомкнулись. Дальше удержание — чистый контроль, урона не даёт (тикающий урон превратил бы захват в казнь).")]
    [Min(0)] public int grabBleedStacks = 0;

    [Header("Дыхалка — машина")]
    [Tooltip("Расход бака в секунду, пока держишь. Скромный и без надбавки за стадию: змея на партере обязана додушить волка, уволочь и остаться с запасом на переваривание.")]
    [LowerIsBetter, MustBePositive] public float holdDrain = 3f;

    [Tooltip("Как часто выдохшийся теряет стадию, с (иначе спад за один кадр). Жертве это честный выход «на измор».")]
    [MustBePositive] public float wearInterval = 1.5f;

    [Header("Игрок — драйвер PlayerConstrict")]
    [Tooltip("Дальность подбора цели в упор, м.")]
    [MustBePositive] public float grabRange = 2.2f;

    [Tooltip("Жертва ушла дальше — хватка соскользнула, м.")]
    [MustBePositive] public float holdRange = 3.2f;

    [Tooltip("Перезарядка после отпускания, с.")]
    [LowerIsBetter, Min(0f)] public float cooldown = 2.5f;

    [Tooltip("Твой множитель хода на ст.1: держишь отдельной частью тела.")]
    [Range(0f, 1f)] public float selfSlow1 = 0.8f;

    [Tooltip("Твой множитель хода со ст.2 (ноша). На партере слоу не растёт — растёт хват.")]
    [Range(0f, 1f)] public float selfSlow2 = 0.6f;

    [Tooltip("Отлёт жертвы, когда она вырвалась или хват сорвали.")]
    [Min(0f)] public float escapeKnock = 6f;

    [Tooltip("Со ст.2: на каком выносе от тебя едет ноша, м (за спиной или перед собой — тумблер C).")]
    [MustBePositive] public float dragOffset = 1.1f;

    /// <summary>Орган надет не на своё родное шасси: захват держит не выше <see cref="foreignMaxStage"/>.</summary>
    public override void OnForeignChassis() => maxStage = Mathf.Min(maxStage, foreignMaxStage);
}
