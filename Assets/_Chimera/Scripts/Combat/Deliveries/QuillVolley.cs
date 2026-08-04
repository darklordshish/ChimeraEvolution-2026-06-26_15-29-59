using UnityEngine;

/// <summary>
/// ЗАЛП ИГЛАМИ — первая ДАЛЬНЯЯ доставка (наследник `WindupAbility`). Замах-телеграф → веер снарядов
/// вперёд, в конус на цель. Число попавших = глубина замедления цели (кит ежа: осыпал → добыча увязла →
/// подошёл → схватил). Само-балансится дистанцией/углом: близко-в-лоб много игл, издали-боком мало.
///
/// БОЕЗАПАС = ОТКАТ (решение ревью), не ресурс: ритм держит психика (кулдаун между залпами), «патроны»
/// без носителя — лишняя бухгалтерия. Каждая игла — `Quill` с общим `MeleeBlow`-пайком.
/// </summary>
public class QuillVolley : WindupAbility
{
    [Header("Залп")]
    [SerializeField] float minRange = 6f;      // ближе — не стреляет (переходит в ближний бой)
    [SerializeField] float maxRange = 16f;     // дальше — не достаёт
    [SerializeField] int quills = 6;           // игл в пучке
    [SerializeField] float spreadAngle = 9f;   // полу-угол разлёта — УЗКИЙ (дробовик-пучок): вблизи все в цель, вдаль расходятся
    [SerializeField] float speed = 22f;
    [SerializeField] float hitRadius = 0.35f;  // толщина иглы (SphereCast)

    [Header("Паёк иглы")]
    [SerializeField] int damagePerQuill = 3;
    [SerializeField] int bleedPerQuill = 1;   // протыкание
    [SerializeField] int slowPerQuill = 1;    // замедление — глубина копится числом попаданий

    [SerializeField] float blindAimError = 1.6f; // радиус промаха прицела при стрельбе ПО НЮХУ (цель в камуфляже)
    float BlindAimError => blindAimError > 0f ? blindAimError : 1.6f; // 0-гоча: новое поле на префабе приходит нулём
    bool blind;                                // этот залп — вслепую (ставит психика перед TryUse)

    public float MinRange => minRange;         // психика читает окно дистанций
    public float MaxRange => maxRange;

    /// <summary>СТРЕЛЬБА ПО НЮХУ: цель чуется, но не видна (камуфляж) — бьём ПРИМЕРНО туда, прицел уводит на
    /// BlindAimError. Камуфляж не отменяет залп, а сбивает точность; задел попал → урон раскрывает цель
    /// (Health.onDamaged → Camouflage.Reveal) → дальше уже прицельно. Ставит психика перед каждым TryUse.</summary>
    public void SetBlind(bool on) => blind = on;

    protected override float GizmoRange => maxRange;
    protected override float GizmoHalfAngle => spreadAngle;

    protected override Color TelegraphColor => TelegraphColors.Volley;

    protected override AbilityRun OnTick()
    {
        if (Time.time < windupEnd) { SettleInPlace(); return AbilityRun.Running; } // замах: стоим, целимся

        // ВЫСТРЕЛ: целимся В 3D (не расплющивая Y) — иначе по змее на стене-насесте иглы летят
        // горизонтально мимо. Вынос origin — по ГОРИЗОНТАЛИ (за свой коллайдер), прицел уже в точку тела
        Vector3 aim = target.position + Vector3.up * 0.4f;                 // тело цели, не пол под ней
        if (blind) aim += Random.insideUnitSphere * BlindAimError;          // по нюху — «примерно туда» (камуфляж сбивает прицел, не отменяет залп)
        Vector3 flatFwd = aim - transform.position; flatFwd.y = 0f;
        flatFwd = flatFwd.sqrMagnitude > 0.001f ? flatFwd.normalized : transform.forward;
        Vector3 origin = transform.position + Vector3.up * 0.5f + flatFwd * 0.9f;
        Vector3 baseDir = (aim - origin).sqrMagnitude > 0.001f ? (aim - origin).normalized : flatFwd;
        var blow = new MeleeBlow { Damage = damagePerQuill, BleedStacks = bleedPerQuill, SlowStacks = slowPerQuill };

        Quaternion aimRot = Quaternion.LookRotation(baseDir);
        float coneR = Mathf.Tan(spreadAngle * Mathf.Deg2Rad); // радиус конуса на единичной дальности
        for (int i = 0; i < quills; i++)
        {
            // КОНУС (дробовик): случайная точка в диске вокруг прицела → иглы кучно в 3D, а не плоским веером
            Vector2 d = Random.insideUnitCircle * coneR;
            Vector3 dir = aimRot * new Vector3(d.x, d.y, 1f).normalized;
            Quill.Spawn(origin, dir, speed, maxRange, hitRadius, ownHealth, blow, DamageMult);
        }
        return AbilityRun.Done;
    }
}
