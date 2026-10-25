using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Камера с двумя режимами (тоггл в PlayerController, клавиша V):
/// 3-е лицо — follow за целью; 1-е лицо — в голове, питч мышью/стиком. Тряска работает в обоих.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [Header("Третье лицо")]
    [SerializeField] Transform target;
    [SerializeField] Vector3 offset = new Vector3(0f, 6f, -7f);
    [SerializeField] float followLerp = 10f;
    [SerializeField] float lookHeight = 1.2f;
    [SerializeField] bool rotateBehind = true;   // камера висит ЗА СПИНОЙ тела (мышь крутит тело — камера следом)
    [SerializeField] bool rigidYaw = true;       // БЕЗЫНЕРЦИОННО: жёстко за телом, без запаздывания (удобно тестить/целиться)
    [SerializeField] float yawFollowSpeed = 240f; // град/с доворота при ВЫКЛЮЧЕННОМ rigidYaw (мягкое запаздывание)

    [Header("Третье лицо: коллизия (0 = не настроено, берётся дефолт)")]
    // Обёртки-свойства ниже читают 0 как «поле не настроено»: у компонентов, уже лежащих
    // в старых сценах, новое [SerializeField] приходит нулём, а не инициализатором.
    [Tooltip("Радиус сферы проверки стены/крыши. 0 = 0.28 м.")]
    [SerializeField] float wallProbeRadius = 0f;
    [Tooltip("Запас перед препятствием: камера встаёт на столько раньше контакта. 0 = 0.12 м.")]
    [SerializeField] float wallProbePadding = 0f;
    [Tooltip("Что считается препятствием. 0 = всё, кроме слоя Ignore Raycast.")]
    [SerializeField] LayerMask wallProbeMask = 0;

    const float DefaultWallProbeRadius = 0.28f;
    const float DefaultWallProbePadding = 0.12f;

    [Header("Первое лицо")]
    [SerializeField] float headHeight = 1.4f;
    [SerializeField] float headForward = 0.25f;
    [SerializeField] float pitchSensitivity = 0.1f;
    [SerializeField] float minPitch = -60f;
    [SerializeField] float maxPitch = 70f;

    Vector3 followPos;
    float shakeTimer, shakeDuration, shakeMag, pitch;
    float yaw; // угол камеры вокруг цели (0 = исходный офсет); догоняет направление ДВИЖЕНИЯ игрока
    PlayerController player;
    BodySize sizeOf;   // размер тела цели: вешает тело при сборке, позже камеры — берём лениво
    RaycastHit[] probeHits; // несериализуемое: после перекомпиляции на Play приходит null — заводим лениво
    static int defaultProbeMask = int.MinValue;
    BodySize size => sizeOf != null ? sizeOf : (target != null ? sizeOf = target.GetComponent<BodySize>() : null);

    float ProbeRadius => wallProbeRadius > 0f ? wallProbeRadius : DefaultWallProbeRadius;
    float ProbePadding => wallProbePadding > 0f ? wallProbePadding : DefaultWallProbePadding;
    int ProbeMask => wallProbeMask.value != 0 ? wallProbeMask.value : DefaultProbeMask;

    /// <summary>Всё, кроме встроенного слоя Ignore Raycast (2). Считается один раз на домен.</summary>
    static int DefaultProbeMask
    {
        get
        {
            if (defaultProbeMask == int.MinValue)
                defaultProbeMask = ~LayerMask.GetMask("Ignore Raycast");
            return defaultProbeMask;
        }
    }

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
        Vector3 aim = target.position + Vector3.up * (size != null ? size.FromGround(lookHeight) : lookHeight);
        Vector3 desired = followPos + Quaternion.Euler(0f, yaw, 0f) * offset * k;
        // Стена/крыша: камера не заходит за препятствие, встаёт перед ним с запасом.
        // Нет препятствия — позиция ровно прежняя (важно для A/C и старых сцен).
        transform.position = ResolveWall(aim, desired) + shake;
        transform.LookAt(aim);
    }

    /// <summary>
    /// Третье лицо с коллизией: сфера летит от точки взгляда (голова/пояс — то же, куда смотрит
    /// камера) к ЖЕЛАЕМОЙ позиции камеры. Свои коллайдеры (цель и игрок с детьми тела) не считаем:
    /// CharacterController игрока всегда перекрывает начало луча.
    /// </summary>
    Vector3 ResolveWall(Vector3 aim, Vector3 desired)
    {
        Vector3 delta = desired - aim;
        float distance = delta.magnitude;
        if (distance < 1e-4f) return desired;

        Vector3 dir = delta / distance;
        if (probeHits == null) probeHits = new RaycastHit[8]; // перекомпиляция на Play обнуляет поле
        int count = Physics.SphereCastNonAlloc(aim, ProbeRadius, dir, probeHits, distance,
            ProbeMask, QueryTriggerInteraction.Ignore);

        float nearest = distance;
        for (int i = 0; i < count; i++)
        {
            var hit = probeHits[i];
            if (hit.collider == null || IsOwnCollider(hit.collider)) continue;
            if (hit.distance < nearest) nearest = hit.distance;
        }

        if (nearest >= distance) return desired;
        return aim + dir * Mathf.Max(0f, nearest - ProbePadding);
    }

    bool IsOwnCollider(Collider c)
    {
        Transform t = c.transform;
        if (target != null && (t == target || t.IsChildOf(target))) return true;
        if (player != null && (t == player.transform || t.IsChildOf(player.transform))) return true;
        return false;
    }

    Vector3 ShakeOffset()
    {
        if (shakeTimer <= 0f) return Vector3.zero;
        shakeTimer -= Time.unscaledDeltaTime;
        float k = Mathf.Clamp01(shakeTimer / shakeDuration);
        return Random.insideUnitSphere * (shakeMag * k);
    }
}
