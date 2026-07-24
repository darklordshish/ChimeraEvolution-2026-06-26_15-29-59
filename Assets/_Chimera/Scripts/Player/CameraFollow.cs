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

            Vector3 head = target.position + Vector3.up * headHeight + target.forward * headForward;
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
        transform.position = followPos + Quaternion.Euler(0f, yaw, 0f) * offset + shake;
        transform.LookAt(target.position + Vector3.up * lookHeight);
    }

    Vector3 ShakeOffset()
    {
        if (shakeTimer <= 0f) return Vector3.zero;
        shakeTimer -= Time.unscaledDeltaTime;
        float k = Mathf.Clamp01(shakeTimer / shakeDuration);
        return Random.insideUnitSphere * (shakeMag * k);
    }
}
