using System;
using UnityEngine;

/// <summary>
/// ЗАЛП ИГЛАМИ — запись органа «Игломёт» (придаток ежа; у человека — химерный слот). Одна запись кормит залп ежа
/// (`QuillVolley`: замах → пучок игл в цель) и залп игрока (`PlayerQuillVolley`: мгновенно, туда, куда смотрит камера).
/// У игрока урон, скорость и дальность игл растут с мощью органа — это модификатор грани, а не число записи.
/// </summary>
[Serializable]
public class VolleyData : AbilityData
{
    public override Type NpcCarrier => typeof(QuillVolley);
    public override Type PlayerCarrier => typeof(PlayerQuillVolley);

    [Header("Паёк иглы")]
    [Tooltip("Урон одной иглы. У игрока × мощь органа (родство с ежом), у NPC как есть.")]
    [Expressed, MustBePositive] public int damagePerQuill = 4;

    [Tooltip("Стаков кровотечения за иглу: протыкание.")]
    [Min(0)] public int bleedPerQuill = 1;

    [Tooltip("Стаков замедления за иглу: глубина копится числом попаданий (кит ежа: осыпал → добыча увязла → подошёл → схватил).")]
    [Min(0)] public int slowPerQuill = 1;

    [Header("Пучок")]
    [Tooltip("Игл в пучке.")]
    [MustBePositive] public int quills = 6;

    [Tooltip("Полуугол разлёта, градусы. УЗКИЙ — дробовик-пучок: вблизи все в цель, вдаль расходятся сами.")]
    [MustBePositive] public float spreadAngle = 9f;

    [Tooltip("Скорость иглы, м/с. У игрока × мощь органа.")]
    [MustBePositive] public float speed = 22f;

    [Tooltip("Толщина иглы, м: радиус SphereCast по шагу полёта.")]
    [MustBePositive] public float hitRadius = 0.35f;

    [Header("Дистанция и ритм")]
    [Tooltip("Ближе этого ёж не стреляет, а переходит в ближний бой, м. Читает NPC.")]
    [MustBePositive] public float minRange = 6f;

    [Tooltip("Дальность полёта иглы, м: дальше не долетает. У игрока × мощь органа.")]
    [MustBePositive] public float maxRange = 15f;

    [Tooltip("Замах NPC перед залпом, с: телеграф. У игрока замаха нет.")]
    [LowerIsBetter, MustBePositive] public float windupTime = 0.5f;

    [Tooltip("Перезарядка залпа, с — у игрока и у NPC (доставка ждёт её сама). Число родного вида органа (спека 16.09).")]
    [LowerIsBetter, MustBePositive] public float cooldown = 0.8f;

    [Tooltip("СТРЕЛЬБА ПО НЮХУ: радиус промаха прицела, когда цель чуется, но не видна (камуфляж), м. Читает NPC.")]
    [MustBePositive] public float blindAimError = 1.6f;
}
