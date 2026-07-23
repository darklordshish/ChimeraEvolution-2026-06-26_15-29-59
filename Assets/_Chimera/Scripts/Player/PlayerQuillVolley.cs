using UnityEngine;

/// <summary>
/// ЗАЛП ИГЛАМИ игрока (слот «Руки», орган «Иглы-руки») — первый ДАЛЬНИЙ бой в руках игрока. Веер снарядов
/// туда, куда СМОТРИШЬ (направление КАМЕРЫ, не тела): целится и вверх по стене, перекрестье = центр экрана.
/// Мгновенный (свои приёмы игрок не телеграфит), кулдаун свой. Реюз снаряда `Quill` и пайка `MeleeBlow`.
///
/// Иглы-руки ВЫТЕСНЯЮТ мечевой удар в слоте Рук (melee↔ranged выбор): взял метатели — бьёшь издали, а не
/// когтем в упор. До-создаётся телом игроку, включается флагом органа (`VolleyEnabled`).
/// </summary>
public class PlayerQuillVolley : MonoBehaviour, IAbility
{
    [Header("Залп")]
    [SerializeField] int quills = 6;
    [SerializeField] float spreadAngle = 9f;   // полу-угол — УЗКИЙ (дробовик-пучок): кучно, вблизи все в цель
    [SerializeField] float speed = 26f;
    [SerializeField] float range = 18f;
    [SerializeField] float hitRadius = 0.35f;
    [SerializeField] float cooldown = 0.8f;

    [Header("Паёк иглы")]
    [SerializeField] int damagePerQuill = 4;
    [SerializeField] int bleedPerQuill = 1;
    [SerializeField] int slowPerQuill = 1;

    public bool VolleyEnabled { get; set; } // включает орган-придаток «Игломёт»
    float power = 1f;                        // мощь от родства с ежом (тело задаёт в Recompute); 1 = свежий графт
    public void SetPower(float mult) => power = Mathf.Max(1f, mult);

    float nextTime;
    Health ownHealth;
    CameraFollow camShake;

    void Start()
    {
        ownHealth = GetComponent<Health>();
        camShake = FindAnyObjectByType<CameraFollow>();
    }

    // водитель зовёт по вводу; активен только с надетыми Иглами-руками; кулдаун свой
    public bool TryUse()
    {
        if (!VolleyEnabled || Time.time < nextTime) return false;
        nextTime = Time.time + cooldown;

        // ПРИЦЕЛ = взгляд КАМЕРЫ (с наклоном вверх/вниз) — по перекрестью; тело даёт только точку вылета
        var cam = Camera.main;
        Vector3 aimDir = cam != null ? cam.transform.forward : transform.forward;
        Vector3 flatFwd = aimDir; flatFwd.y = 0f;
        flatFwd = flatFwd.sqrMagnitude > 0.001f ? flatFwd.normalized : transform.forward;
        Vector3 origin = transform.position + Vector3.up * 1.2f + flatFwd * 0.6f; // от груди, вперёд за свой коллайдер

        // МОЩЬ от родства с ежом (BonusMultiplier): чем выше — тем быстрее и дальше летит игла + больнее колет.
        // Дальний бой растёт с мастерством, как и всё остальное (ось экспрессии)
        int dmg = Mathf.Max(1, Mathf.RoundToInt(damagePerQuill * power));
        var blow = new MeleeBlow { Damage = dmg, BleedStacks = bleedPerQuill, SlowStacks = slowPerQuill };
        Quaternion aimRot = Quaternion.LookRotation(aimDir);
        float coneR = Mathf.Tan(spreadAngle * Mathf.Deg2Rad);
        for (int i = 0; i < quills; i++)
        {
            // КОНУС (дробовик): случайная точка в диске вокруг прицела — кучно в 3D
            Vector2 d = Random.insideUnitCircle * coneR;
            Vector3 dir = aimRot * new Vector3(d.x, d.y, 1f).normalized;
            Quill.Spawn(origin, dir, speed * power, range * power, hitRadius, ownHealth, blow, 1f);
        }
        if (camShake != null) camShake.Shake(0.08f, 0.12f); // лёгкая отдача
        return true;
    }
}
