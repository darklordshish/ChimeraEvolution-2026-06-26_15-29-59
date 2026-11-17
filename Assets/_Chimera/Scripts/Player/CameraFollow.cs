using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Камера с тремя режимами (круг в PlayerController, клавиша V):
/// 3-е лицо издали и близко — follow за целью; 1-е лицо — в голове, питч мышью/стиком. Тряска работает во всех.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [Header("Третье лицо")]
    [SerializeField] Transform target;
    [SerializeField] Vector3 offset = new Vector3(0f, 6f, -7f);
    [SerializeField] Vector3 nearOffset = NearDefault;   // близкий режим: тело видно крупно
    [SerializeField] float followLerp = 10f;
    [SerializeField] float lookHeight = 1.2f;
    [SerializeField] bool rotateBehind = true;   // камера висит ЗА СПИНОЙ тела (мышь крутит тело — камера следом)
    [SerializeField] bool rigidYaw = true;       // БЕЗЫНЕРЦИОННО: жёстко за телом, без запаздывания (удобно тестить/целиться)
    [SerializeField] float yawFollowSpeed = 240f; // град/с доворота при ВЫКЛЮЧЕННОМ rigidYaw (мягкое запаздывание)

    [Header("Стены (3-е лицо)")]
    [SerializeField] float wallRadius = 0.2f;     // радиус шара-пробы между головой и камерой
    [SerializeField] float wallRecover = 6f;      // м/с: как быстро камера выезжает обратно после преграды
    [SerializeField] float wallPad = 0.05f;       // зазор перед преградой
    /// <summary>Новое поле у камеры, уже лежащей в сцене, приходит нулём: ноль читаем как «дефолт».</summary>
    float WallRadius => wallRadius > 0f ? wallRadius : 0.2f;
    float WallRecover => wallRecover > 0f ? wallRecover : 6f;
    float clip = 1f;      // доля пути от головы до желаемой позиции, до которой камера пускает преграда
    static readonly RaycastHit[] wallHits = new RaycastHit[16];

    [Header("Первое лицо")]
    [SerializeField] float headHeight = 1.4f;
    [SerializeField] float headForward = 0.25f;
    [SerializeField] float pitchSensitivity = 0.1f;
    [SerializeField] float minPitch = -60f;
    [SerializeField] float maxPitch = 70f;

    public static readonly Vector3 NearDefault = new Vector3(0f, 2.2f, -3.2f);
    /// <summary>Новое поле у камеры, уже лежащей в сцене, приходит нулём: ноль читаем как «не настроено», иначе камера окажется в теле.</summary>
    public static Vector3 NearOr(Vector3 v) => v == Vector3.zero ? NearDefault : v;
    Vector3 CurrentOffset => player != null && player.View == PlayerController.ViewMode.Near ? NearOr(nearOffset) : offset;

    Vector3 followPos;
    float shakeTimer, shakeDuration, shakeMag, pitch;
    float yaw; // угол камеры вокруг цели (0 = исходный офсет); догоняет направление ДВИЖЕНИЯ игрока
    PlayerController player;
    BodySize sizeOf;   // размер тела цели: вешает тело при сборке, позже камеры — берём лениво
    BodySize size => sizeOf != null ? sizeOf : (target != null ? sizeOf = target.GetComponent<BodySize>() : null);

    void Awake()
    {
        followPos = transform.position;
        // FPS вплотную к стене: голова на +0.25 вперёд, до стены остаётся ~0.25 — штатный near clip 0.3
        // протыкает геометрию (видно сквозь). Поджимаем плоскость отсечения.
        if (TryGetComponent<Camera>(out var cam)) cam.nearClipPlane = Mathf.Min(cam.nearClipPlane, 0.05f);
    }

    void Start()
    {
        if (target == null) return;
        player = target.GetComponent<PlayerController>();
        followPos = target.position; // followPos теперь сглаженная ПОЗИЦИЯ ЦЕЛИ (не камеры) — стартуем с неё
    }

    public void Shake(float duration = 0.12f, float magnitude = 0.3f)
    {
        shakeDuration = duration;
        shakeMag = magnitude;
        shakeTimer = duration;
    }

    void LateUpdate()
    {
        if (target == null) return;
        if (ConstructorUI.IsOpen) return; // конструктор открыт (пауза): камера замирает, не берёт pitch с мыши — ввод у UI

        Vector3 shake = ShakeOffset();

        // первое лицо
        if (player != null && player.FirstPerson)
        {
            float dy = 0f;
            if (Mouse.current != null) dy -= Mouse.current.delta.ReadValue().y * pitchSensitivity;
            var gp = Gamepad.current;
            if (gp != null) dy -= gp.rightStick.ReadValue().y * 100f * Time.deltaTime;
            pitch = Mathf.Clamp(pitch + dy, minPitch, maxPitch);

            // глаза — с ростом тела (`BodySize`): человек-лось 3 м смотрит сверху, Хеджхалк — от земли
            float s = size != null ? size.Scale : 1f;
            Vector3 head = target.position + Vector3.up * (size != null ? size.FromGround(headHeight) : headHeight) + target.forward * headForward * s;
            transform.position = head + shake;
            transform.rotation = Quaternion.Euler(pitch, target.eulerAngles.y, 0f);
            return;
        }

        // третье лицо «ЗА ПЛЕЧОМ»: мышь крутит ТЕЛО (PlayerController), камера за его спиной.
        // Сглаживаем только ХОД (позицию цели); поворот — жёсткий (rigidYaw) или мягкий — иначе
        // разворот мышью «плавал» дугой и целиться было тяжело
        if (rotateBehind)
            yaw = rigidYaw ? target.eulerAngles.y
                           : Mathf.MoveTowardsAngle(yaw, target.eulerAngles.y, yawFollowSpeed * Time.deltaTime);

        followPos = Vector3.Lerp(followPos, target.position, followLerp * Time.deltaTime);
        float k = size != null ? size.Scale : 1f;   // камера за плечом отходит вместе с ростом
        Vector3 look = target.position + Vector3.up * (size != null ? size.FromGround(lookHeight) : lookHeight);
        Vector3 want = followPos + Quaternion.Euler(0f, yaw, 0f) * CurrentOffset * k;
        transform.position = ClipToWalls(look, want) + shake;
        transform.LookAt(look);
    }

    /// <summary>Камера не лезет в стволы: шар от точки взгляда к желаемой позиции; преграда — окружение (не тело
    /// игрока, не NPC, не триггер). Втянулась мгновенно, выезжает плавно.</summary>
    Vector3 ClipToWalls(Vector3 look, Vector3 want)
    {
        Vector3 d = want - look;
        float dist = d.magnitude;
        if (dist < 0.01f) return want;
        float wanted = 1f;
        int n = Physics.SphereCastNonAlloc(look, WallRadius, d / dist, wallHits, dist, ~0, QueryTriggerInteraction.Ignore);
        float best = dist;
        for (int i = 0; i < n; i++)
        {
            var h = wallHits[i];
            if (h.distance <= 0f || h.distance >= best) continue; // 0 — шар стартует внутри коллайдера: его не обходим
            if (!IsEnvironment(h.collider)) continue;
            best = h.distance;
        }
        if (best < dist) wanted = Mathf.Max(0f, (best - wallPad) / dist);
        clip = StepClip(clip, wanted, Time.unscaledDeltaTime, WallRecover / dist);
        return look + d * clip;
    }

    /// <summary>Доля пути: уменьшается сразу, растёт со скоростью <paramref name="recoverPerSec"/> (доля/с).</summary>
    public static float StepClip(float current, float wanted, float dt, float recoverPerSec)
        => wanted < current ? wanted : Mathf.MoveTowards(current, wanted, recoverPerSec * dt);

    bool IsEnvironment(Collider c)
    {
        if (c.isTrigger) return false;
        if (target != null && c.transform.IsChildOf(target)) return false;
        if (c.GetComponentInParent<CreatureBody>() != null) return false;
        if (c.GetComponentInParent<Health>() != null) return false;
        return true;
    }

    Vector3 ShakeOffset()
    {
        if (shakeTimer <= 0f) return Vector3.zero;
        shakeTimer -= Time.unscaledDeltaTime;
        float k = Mathf.Clamp01(shakeTimer / shakeDuration);
        return Random.insideUnitSphere * (shakeMag * k);
    }
}
