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
    [SerializeField] int quills = 5;           // игл в веере
    [SerializeField] float spreadAngle = 22f;  // полу-угол разлёта веера
    [SerializeField] float speed = 22f;
    [SerializeField] float hitRadius = 0.35f;  // толщина иглы (SphereCast)

    [Header("Паёк иглы")]
    [SerializeField] int damagePerQuill = 3;
    [SerializeField] int bleedPerQuill = 1;   // протыкание
    [SerializeField] int slowPerQuill = 1;    // замедление — глубина копится числом попаданий

    public float MinRange => minRange;         // психика читает окно дистанций
    public float MaxRange => maxRange;

    protected override float GizmoRange => maxRange;
    protected override float GizmoHalfAngle => spreadAngle;

    protected override Color TelegraphColor => TelegraphColors.Volley;

    protected override AbilityRun OnTick()
    {
        if (Time.time < windupEnd) { SettleInPlace(); return AbilityRun.Running; } // замах: стоим, целимся

        // ВЫСТРЕЛ: целимся В 3D (не расплющивая Y) — иначе по змее на стене-насесте иглы летят
        // горизонтально мимо. Вынос origin — по ГОРИЗОНТАЛИ (за свой коллайдер), прицел уже в точку тела
        Vector3 aim = target.position + Vector3.up * 0.4f;                 // тело цели, не пол под ней
        Vector3 flatFwd = aim - transform.position; flatFwd.y = 0f;
        flatFwd = flatFwd.sqrMagnitude > 0.001f ? flatFwd.normalized : transform.forward;
        Vector3 origin = transform.position + Vector3.up * 0.5f + flatFwd * 0.9f;
        Vector3 baseDir = (aim - origin).sqrMagnitude > 0.001f ? (aim - origin).normalized : flatFwd;
        var blow = new MeleeBlow { Damage = damagePerQuill, BleedStacks = bleedPerQuill, SlowStacks = slowPerQuill };

        for (int i = 0; i < quills; i++)
        {
            float t = quills == 1 ? 0f : (i / (float)(quills - 1)) * 2f - 1f; // −1..+1 по вееру
            // веер вокруг ВЕРТИКАЛИ: разлёт влево-вправо, наклон на цель по высоте сохраняется
            Vector3 dir = Quaternion.AngleAxis(t * spreadAngle, Vector3.up) * baseDir;
            Quill.Spawn(origin, dir, speed, maxRange, hitRadius, ownHealth, blow, DamageMult);
        }
        return AbilityRun.Done;
    }
}
