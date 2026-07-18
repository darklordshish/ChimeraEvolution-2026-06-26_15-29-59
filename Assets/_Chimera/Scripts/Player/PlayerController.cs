using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Движение + прицел с тогглом вида (V).
/// 3-е лицо: twin-stick (движение относительно камеры, прицел мышью/правым стиком).
/// 1-е лицо: mouse-look (поворот мышью, движение от лица). Плюс рывок с i-frames.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInputDriver))]
public class PlayerController : MonoBehaviour
{
    [Header("Движение")]
    [SerializeField] float moveSpeed = 6f;
    [SerializeField] float gravity = -20f;
    [SerializeField, Range(0.2f, 1f)] float sneakMult = 0.4f; // ТИХИЙ ШАГ (Shift): медленнее = тише (шум меряется скоростью — Noise)

    [Header("Рывок")]
    [SerializeField] float dashSpeed = 20f;
    [SerializeField] float dashDuration = 0.18f;
    [SerializeField] float dashCooldown = 0.7f;
    [SerializeField] int dashRipDamage = 6;       // урон волку, когда срываемся рывком с захвата
    [SerializeField] int dashRipSelfDamage = 5;   // и себе — вырываться из пасти больно (минует i-frames рывка)

    [Header("Вид / прицел")]
    [SerializeField] float lookSensitivity = 0.1f;  // мышь, поворот в FPS
    [SerializeField] float gamepadYawSpeed = 180f;  // правый стик, поворот в FPS

    public bool FirstPerson { get; private set; }

    CharacterController controller;
    Knockback knockback;
    Health health;
    InputAction moveAction, lookAction, dashAction, toggleViewAction, sneakAction;
    Vector3 velocity;
    float dashTimer, dashReadyAt, groundY;
    Vector3 dashDir;
    IGrabber grabber;        // волк/змея, держащий игрока в захвате
    float grabSlow = 1f;     // множитель скорости И рывка, пока в захвате (1 = свободно; 0 = корень на 3-й стадии обхвата)
    PlayerConstrict constrict; // СВОЙ обхват (хвост): пока держишь жертву — сам замедлен (SelfSlow)
    bool grabImmune; // чёрный ход: будущая способность даёт иммунитет к захвату
    // dev-призрак даёт иммунитет к захватам автоматически (и теряет его вместе с призраком при атаке):
    // волк/змея уже проверяют GrabImmune в своих захватах — распускаются сами
    public bool GrabImmune { get => grabImmune || Perception.PlayerGhost; set => grabImmune = value; }
    public bool IsGrabbed => grabber != null; // тебя держат (гейт для своего обхвата и т.п.)
    public bool IsDashing => dashTimer > 0f;  // ТАРАН лосиных ног ездит на рывке (PlayerCharge гейтит уроном)

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        health = GetComponent<Health>();
        if (!TryGetComponent(out knockback)) knockback = gameObject.AddComponent<Knockback>(); // отлёт от ударов (рога/таран/топот/пинок)
        TryGetComponent(out constrict);

        // запах игрока: эмиттер следа (его тропят враги) + визуал своего следа. В Awake (не Start!) —
        // чтобы след существовал к первому Recompute тела: оно красит след ЦВЕТОМ СОСТАВА (видовой отпечаток)
        if (!TryGetComponent<ScentEmitter>(out _)) gameObject.AddComponent<ScentEmitter>();
        if (!TryGetComponent<ScentTrail>(out _)) gameObject.AddComponent<ScentTrail>().Configure(new Color(1f, 0.45f, 0.3f), true);

        moveAction = new InputAction("Move", InputActionType.Value);
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
            .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
        moveAction.AddBinding("<Gamepad>/leftStick");

        lookAction = new InputAction("Look", InputActionType.Value);
        lookAction.AddBinding("<Gamepad>/rightStick");

        dashAction = new InputAction("Dash", InputActionType.Button);
        dashAction.AddBinding("<Keyboard>/space");
        dashAction.AddBinding("<Mouse>/rightButton"); // ПКМ освободился после переноса пинка на E
        dashAction.AddBinding("<Gamepad>/buttonSouth");

        toggleViewAction = new InputAction("ToggleView", InputActionType.Button);
        toggleViewAction.AddBinding("<Keyboard>/v");
        toggleViewAction.AddBinding("<Gamepad>/buttonNorth");

        sneakAction = new InputAction("Sneak", InputActionType.Button); // ТИХИЙ ШАГ: держать
        sneakAction.AddBinding("<Keyboard>/leftShift");
        sneakAction.AddBinding("<Gamepad>/leftShoulder");
    }

    void Start()
    {
        groundY = transform.position.y;
        SetFirstPerson(false);
        if (constrict == null) TryGetComponent(out constrict); // мог до-создаться в CreatureBody.Awake
    }

    void OnEnable() { moveAction.Enable(); lookAction.Enable(); dashAction.Enable(); toggleViewAction.Enable(); sneakAction.Enable(); }
    void OnDisable() { moveAction.Disable(); lookAction.Disable(); dashAction.Disable(); toggleViewAction.Disable(); sneakAction.Disable(); }

    void Update()
    {
        if (toggleViewAction.WasPressedThisFrame()) SetFirstPerson(!FirstPerson);

        Vector2 mv = moveAction.ReadValue<Vector2>();

        // ЕДИНОЕ управление обоих видов (решение пользователя): мышь/правый стик крутят ТЕЛО,
        // WASD — относительно тела. Виды отличаются только камерой (в голове / за плечом)
        float yaw = 0f;
        if (Mouse.current != null) yaw += Mouse.current.delta.ReadValue().x * lookSensitivity;
        yaw += lookAction.ReadValue<Vector2>().x * gamepadYawSpeed * Time.deltaTime;
        transform.Rotate(0f, yaw, 0f);

        Vector3 f = transform.forward; f.y = 0f; f.Normalize();
        Vector3 r = transform.right;   r.y = 0f; r.Normalize();
        Vector3 move = f * mv.y + r * mv.x;

        // рывок
        if (dashAction.WasPressedThisFrame() && Time.time >= dashReadyAt && dashTimer <= 0f)
        {
            dashTimer = dashDuration;
            dashReadyAt = Time.time + dashCooldown;
            dashDir = move.sqrMagnitude > 0.01f ? move.normalized : transform.forward;
            // срываемся рывком: держащий решает, отпустит ли (змея на поздних стадиях обхвата — нет). Иммун — всегда свободен.
            if (grabber != null && (GrabImmune || grabber.BreakFree(dashRipDamage)))
            {
                grabber = null; grabSlow = 1f;
                if (!GrabImmune && health != null) health.TakeDamage(dashRipSelfDamage, true); // рвал себя из захвата — больно (минует i-frames)
            }
        }

        // захват режет И перемещение, И рывок (чем туже, тем короче рывок; на 3-й стадии — корень). Иммун снимает всё.
        // СВОЙ обхват (хвост) замедляет только ход — рывок остаётся полным (и сам рвёт хватку дистанцией).
        float grip = GrabImmune ? 1f : grabSlow;
        float hold = constrict != null ? constrict.SelfSlow : 1f;
        float sneak = sneakAction.IsPressed() ? sneakMult : 1f; // тихий шаг: скорость ↓ → шум ↓ (Noise сам заметит)
        Vector3 horizontal = dashTimer > 0f ? dashDir * dashSpeed * grip : move * moveSpeed * grip * hold * sneak;
        if (knockback != null && knockback.IsActive) horizontal = Vector3.zero; // пока откидывает — не рулим (толкает Knockback)
        if (dashTimer > 0f) dashTimer -= Time.deltaTime;
        if (health != null) health.Invulnerable = dashTimer > 0f;

        // гравитация + общий Move
        if (controller.isGrounded && velocity.y < 0f) velocity.y = -2f;
        velocity.y += gravity * Time.deltaTime;
        Vector3 motion = horizontal;
        motion.y = velocity.y;
        controller.Move(motion * Time.deltaTime);

        if (transform.position.y < groundY - 3f) // провалились сквозь пол (стая продавила) — возвращаем
        {
            var p = transform.position; p.y = groundY; transform.position = p;
            velocity.y = 0f;
        }
    }

    void SetFirstPerson(bool on)
    {
        FirstPerson = on;
        Cursor.lockState = CursorLockMode.Locked; // мышь — руль тела в ОБОИХ видах, курсор не нужен (Esc отпускает)
        Cursor.visible = false;
        // свою голову от ПЕРВОГО лица не рендерим (нос/куб лезут в камеру) — классика FPS; в 3-м лице возвращаем
        foreach (var r in GetComponentsInChildren<Renderer>())
            if (r.name == "Head" || r.name == "Nose") r.enabled = !on;
    }

    // конструктор меняет мобильность при смене органа в слоте «Ноги»
    public void SetLegs(float newMoveSpeed, float newDashSpeed)
    {
        moveSpeed = newMoveSpeed;
        dashSpeed = newDashSpeed;
    }

    // слот «Чутьё»: быстрее откат рывка
    public void SetDashCooldown(float newCooldown) => dashCooldown = newCooldown;

    // захват волком: режем скорость, пока висит; рывок/пинок снимают
    public void ApplyGrab(IGrabber g, float slow) { grabber = g; grabSlow = Mathf.Clamp01(slow); } // 0 = полный корень (3-я стадия)
    public void ReleaseGrab(IGrabber g) { if (ReferenceEquals(grabber, g)) { grabber = null; grabSlow = 1f; } }

}
