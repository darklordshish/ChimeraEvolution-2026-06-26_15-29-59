using UnityEngine;

/// <summary>
/// ЗАЛП ИГЛАМИ — первая ДАЛЬНЯЯ доставка (наследник `WindupAbility`). Замах-телеграф → веер снарядов
/// вперёд, в конус на цель. Число попавших = глубина замедления цели (кит ежа: осыпал → добыча увязла →
/// подошёл → схватил). Само-балансится дистанцией/углом: близко-в-лоб много игл, издали-боком мало.
///
/// БОЕЗАПАС = ОТКАТ (решение ревью), не ресурс: ритм держит психика (кулдаун между залпами), «патроны»
/// без носителя — лишняя бухгалтерия. Каждая игла — `Quill` с общим `MeleeBlow`-пайком.
/// ВСЕ ЧИСЛА — ИЗ ЗАПИСИ ОРГАНА «Игломёт» (<see cref="VolleyData"/>, спека «данные в органах»): нет записи — залпа нет.
/// </summary>
public class QuillVolley : WindupAbility, IOrganAbility
{
    VolleyData data; // раскрытая запись залпа; null — залпа нет

    public System.Type DataType => typeof(VolleyData);
    public void Configure(AbilityData d) => data = d as VolleyData;
    public bool Available => data != null;
    protected override bool Ready => data != null;
    protected override float WindupTime => data.windupTime;

    bool blind; // этот залп — вслепую (ставит психика перед TryUse)

    // психика читает окно дистанций; нет залпа — пустое окно [0, 0]
    public float MinRange => data != null ? data.minRange : 0f;
    public float MaxRange => data != null ? data.maxRange : 0f;

    /// <summary>СТРЕЛЬБА ПО НЮХУ: цель чуется, но не видна (камуфляж) — бьём ПРИМЕРНО туда, прицел уводит на
    /// blindAimError записи. Камуфляж не отменяет залп, а сбивает точность; задел попал → урон раскрывает цель
    /// (Health.onDamaged → Camouflage.Reveal) → дальше уже прицельно. Ставит психика перед каждым TryUse.</summary>
    public void SetBlind(bool on) => blind = on;

    protected override float GizmoRange => MaxRange;
    protected override float GizmoHalfAngle => data != null ? data.spreadAngle : 0f;

    protected override Color TelegraphColor => TelegraphColors.Volley;

    protected override AbilityRun OnTick()
    {
        if (Time.time < windupEnd) { SettleInPlace(); return AbilityRun.Running; } // замах: стоим, целимся

        // ВЫСТРЕЛ: целимся В 3D (не расплющивая Y) — иначе по змее на стене-насесте иглы летят
        // горизонтально мимо. Вынос origin — по ГОРИЗОНТАЛИ (за свой коллайдер), прицел уже в точку тела
        Vector3 aim = target.position + Vector3.up * 0.4f;                 // тело цели, не пол под ней
        if (blind) aim += Random.insideUnitSphere * data.blindAimError;          // по нюху — «примерно туда» (камуфляж сбивает прицел, не отменяет залп)
        Vector3 flatFwd = aim - transform.position; flatFwd.y = 0f;
        flatFwd = flatFwd.sqrMagnitude > 0.001f ? flatFwd.normalized : transform.forward;
        Vector3 origin = transform.position + Vector3.up * 0.5f + flatFwd * 0.9f;
        Vector3 baseDir = (aim - origin).sqrMagnitude > 0.001f ? (aim - origin).normalized : flatFwd;
        var blow = new MeleeBlow { Damage = data.damagePerQuill, BleedStacks = data.bleedPerQuill, SlowStacks = data.slowPerQuill };

        Quaternion aimRot = Quaternion.LookRotation(baseDir);
        float coneR = Mathf.Tan(data.spreadAngle * Mathf.Deg2Rad); // радиус конуса на единичной дальности
        for (int i = 0; i < data.quills; i++)
        {
            // КОНУС (дробовик): случайная точка в диске вокруг прицела → иглы кучно в 3D, а не плоским веером
            Vector2 d = Random.insideUnitCircle * coneR;
            Vector3 dir = aimRot * new Vector3(d.x, d.y, 1f).normalized;
            Quill.Spawn(origin, dir, data.speed, data.maxRange, data.hitRadius, ownHealth, blow, DamageMult);
        }
        return AbilityRun.Done;
    }
}
