using UnityEngine;

/// <summary>
/// ЗАЛП ИГЛАМИ игрока (придаток «Игломёт» ежа, химерный слот) — первый ДАЛЬНИЙ бой игрока. Пучок снарядов туда,
/// куда СМОТРИШЬ (направление КАМЕРЫ, не тела): целится и вверх по стене, перекрестье = центр экрана.
/// Мгновенный (свои приёмы игрок не телеграфит). Реюз снаряда `Quill` и пайка `MeleeBlow`.
///
/// ВСЕ ЧИСЛА — ИЗ ЗАПИСИ ОРГАНА (<see cref="VolleyData"/>): та же запись, что у залпа ежа. У грани своё только одно —
/// МОЩЬ органа как модификатор индивида: чем больше родство с ежом, тем больнее, быстрее и дальше летит игла.
/// </summary>
public class PlayerQuillVolley : MonoBehaviour, IAbility, IOrganAbility
{
    VolleyData data; // раскрытая запись залпа; null — залпа нет

    public System.Type DataType => typeof(VolleyData);
    public void Configure(AbilityData d) => data = d as VolleyData;
    public bool Available => data != null;

    float nextTime;
    Health ownHealth;
    CameraFollow camShake;

    void Start()
    {
        ownHealth = GetComponent<Health>();
        camShake = FindAnyObjectByType<CameraFollow>();
    }

    // водитель зовёт по вводу; доступность — из записи органа, перезарядка — из неё же
    public bool TryUse()
    {
        if (data == null || Time.time < nextTime) return false;
        nextTime = Time.time + data.cooldown;
        if (ownHealth == null) ownHealth = GetComponent<Health>(); // залп до нашего Start

        // ПРИЦЕЛ = взгляд КАМЕРЫ (с наклоном вверх/вниз) — по перекрестью; тело даёт только точку вылета
        var cam = Camera.main;
        Vector3 aimDir = cam != null ? cam.transform.forward : transform.forward;
        Vector3 flatFwd = aimDir; flatFwd.y = 0f;
        flatFwd = flatFwd.sqrMagnitude > 0.001f ? flatFwd.normalized : transform.forward;
        Vector3 origin = transform.position + Vector3.up * 1.2f + flatFwd * 0.6f; // от груди, вперёд за свой коллайдер

        // УРОН УЖЕ РАСКРЫТ ЗАКОНОМ ОРГАНА в записи (спека 16.09: урон всех приёмов — по экспрессии), второй раз не множим.
        // Мощь органа здесь — модификатор индивида только для полёта: с мастерством иглы летят дальше и быстрее;
        // ниже ×1 не опускается — свежий графт стреляет базой записи
        float power = Mathf.Max(1f, data.power);
        int dmg = Mathf.Max(1, data.damagePerQuill);
        var blow = new MeleeBlow { Damage = dmg, BleedStacks = data.bleedPerQuill, SlowStacks = data.slowPerQuill };
        Quaternion aimRot = Quaternion.LookRotation(aimDir);
        float coneR = Mathf.Tan(data.spreadAngle * Mathf.Deg2Rad);
        for (int i = 0; i < data.quills; i++)
        {
            // КОНУС (дробовик): случайная точка в диске вокруг прицела — кучно в 3D
            Vector2 d = Random.insideUnitCircle * coneR;
            Vector3 dir = aimRot * new Vector3(d.x, d.y, 1f).normalized;
            Quill.Spawn(origin, dir, data.speed * power, data.maxRange * power, data.hitRadius, ownHealth, blow, 1f);
        }
        if (camShake != null) camShake.Shake(0.08f, 0.12f); // лёгкая отдача
        return true;
    }
}
