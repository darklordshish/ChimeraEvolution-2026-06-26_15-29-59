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
    [SerializeField] float aimRotationSpeed = 1080f; // доворот к прицелу (3-е лицо)
    [SerializeField] float gravity = -20f;

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
    InputAction moveAction, lookAction, dashAction, toggleViewAction;
    Camera cam;
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
    }

    void Start()
    {
        cam = Camera.main;
        groundY = transform.position.y;
        SetFirstPerson(false);
        if (constrict == null) TryGetComponent(out constrict); // мог до-создаться в CreatureBody.Awake
    }

    void OnEnable() { moveAction.Enable(); lookAction.Enable(); dashAction.Enable(); toggleViewAction.Enable(); }
    void OnDisable() { moveAction.Disable(); lookAction.Disable(); dashAction.Disable(); toggleViewAction.Disable(); }

    void Update()
    {
        if (toggleViewAction.WasPressedThisFrame()) SetFirstPerson(!FirstPerson);

        Vector3 camF = cam != null ? cam.transform.forward : Vector3.forward;
        Vector3 camR = cam != null ? cam.transform.right : Vector3.right;
        camF.y = 0f; camR.y = 0f; camF.Normalize(); camR.Normalize();

        Vector2 mv = moveAction.ReadValue<Vector2>();

        // поворот
        if (FirstPerson)
        {
            float yaw = 0f;
            if (Mouse.current != null) yaw += Mouse.current.delta.ReadValue().x * lookSensitivity;
            yaw += lookAction.ReadValue<Vector2>().x * gamepadYawSpeed * Time.deltaTime;
            transform.Rotate(0f, yaw, 0f);
        }
        else
        {
            Vector3 aim = AimDirection(camF, camR);
            if (aim.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(aim), aimRotationSpeed * Time.deltaTime);
        }

        // движение: FPS — относительно тела, 3-е лицо — относительно камеры
        Vector3 move;
        if (FirstPerson)
        {
            Vector3 f = transform.forward; f.y = 0f; f.Normalize();
            Vector3 r = transform.right;   r.y = 0f; r.Normalize();
            move = f * mv.y + r * mv.x;
        }
        else move = camF * mv.y + camR * mv.x;

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
        Vector3 horizontal = dashTimer > 0f ? dashDir * dashSpeed * grip : move * moveSpeed * grip * hold;
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
        Cursor.lockState = on ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !on;
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

    Vector3 AimDirection(Vector3 camF, Vector3 camR)
    {
        Vector2 look = lookAction.ReadValue<Vector2>();
        if (look.sqrMagnitude > 0.04f)
        {
            Vector3 d = camF * look.y + camR * look.x;
            d.y = 0f;
            return d;
        }

        if (cam != null && Mouse.current != null)
        {
            Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            Plane ground = new Plane(Vector3.up, new Vector3(0f, transform.position.y, 0f));
            if (ground.Raycast(ray, out float enter))
            {
                Vector3 d = ray.GetPoint(enter) - transform.position;
                d.y = 0f;
                return d;
            }
        }

        return Vector3.zero;
    }
}
