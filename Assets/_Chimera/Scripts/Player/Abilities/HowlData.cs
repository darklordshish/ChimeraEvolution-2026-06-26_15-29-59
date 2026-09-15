using System;
using UnityEngine;

/// <summary>
/// ВОЙ — запись волчьей Пасти. Один голос, два поведения: у игрока (`PlayerHowl`) — стан ближних и страх дальних, у волка-NPC
/// — зов стаи (психика читает запись у тела: у NPC носителя нет, решает психика). Закон голоса живёт ЗДЕСЬ, рядом с числами:
/// радиус = база × мощь органа, но не ниже базы (взрослый волк воет как волк), а стан открывается порогом мощи.
/// </summary>
[Serializable]
public class HowlData : AbilityData
{
    public override Type NpcCarrier => null;               // вой волка — решение психики (зов стаи), она читает запись у тела
    public override Type PlayerCarrier => typeof(PlayerHowl);

    [Tooltip("База радиуса воя, м: дальнее кольцо (страх у игрока, зов стаи у волка). Итог × мощь органа, не ниже базы.")]
    [MustBePositive] public float radius = 14f;

    [Tooltip("ПОРОГ-ФИЧА: мощь органа ≥ этого — вой ещё и СТАНИТ ближнее кольцо (половина радиуса). 0 — стана нет вовсе. Не «сколько», а ЧТО ВООБЩЕ открывается: рядовой волк (Э 0.45) только зовёт стаю, босс (Э 2) и игрок на 100 родства глушат.")]
    [Min(0f)] public float stunAt = 2f;

    [Tooltip("Длительность стана ближних, с (контроль ≥1 с — окно действий). Читает игрок.")]
    [MustBePositive] public float stunDuration = 1f;

    [Tooltip("Удар по морали дальнего кольца. У игрока × мощь органа: до −4 на сотке родства. Читает игрок.")]
    [MustBePositive] public float fearMoraleHit = 2f;

    [Tooltip("Перезарядка воя, с — у игрока и у волка (психика ждёт её по записи). Держит один вклад воя в шкале духа стаи. Число родного вида органа (спека 16.09).")]
    [LowerIsBetter, MustBePositive] public float cooldown = 10f;

    [Tooltip("Сколько держится вспышка-сигнал воя у NPC, с: видно, что волк зовёт стаю.")]
    [MustBePositive] public float cueTime = 0.4f;

    /// <summary>Итоговый радиус голоса: база × мощь органа, норму вниз не штрафуем.</summary>
    public float Reach => radius * Mathf.Max(1f, power);

    /// <summary>Открыт ли стан: порог задан и мощь органа до него доросла.</summary>
    public bool Stuns => stunAt > 0f && power >= stunAt;
}
