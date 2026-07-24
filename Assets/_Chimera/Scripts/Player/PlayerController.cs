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
    [SerializeField, Range(0.2f, 1f)] float sneakMult = 0.4f; // ТИХИЙ ШАГ (Ctrl): медленнее = тише (шум меряется скоростью — Noise)

    [Header("Спринт (стамина)")]
    [SerializeField] float sprintMult = 1.6f;   // во сколько раз быстрее обычного хода
    // СПРИНТ — НАГРУЗКА, а не рывок усилия (то же различие, что у погони NPC). Через TrySpend он
    // откладывал реген каждый кадр: бак пустел за считаные секунды, и РЫВОК ПЕРЕСТАВАЛ РАБОТАТЬ НА БЕГУ —
    // выглядело как баг. Теперь слив идёт параллельно регену и лишь превышает его: бежать дорого, но
    // предсказуемо, и запас на рывок остаётся
    [SerializeField] float sprintDrain = 30f;   // расход бака в секунду, пока бежишь (реген игрока 21/с)

    // UNITY-ГОЧА (ловили дважды): НОВОЕ сериализованное поле у компонента, УЖЕ лежащего в сцене, приходит
    // НУЛЁМ — инициализатор из кода к нему не применяется. Здесь молчание было бы особенно коварным:
    // sprintMult 0 ОСТАНАВЛИВАЕТ игрока при беге, а dashCost 0 делает рывок бесплатным (фича «работает»,
    // но ничего не стоит — и это не заметить). Читаем 0 как «не настроено»
    float SprintMult => sprintMult > 0f ? sprintMult : 1.6f;
    float SprintDrain => sprintDrain > 0f ? sprintDrain : 30f;
    float DashCost => dashCost > 0f ? dashCost : 25f;

    [Header("Рывок")]
    [SerializeField] float dashSpeed = 20f;
    [SerializeField] float dashDuration = 0.18f;
    // КУЛДАУН ЖИВЁТ РЯДОМ СО СТАМИНОЙ (решение ревью): он держит РИТМ (не два рывка подряд),
    // бак держит ОБЩИЙ ЛИМИТ. Раз лимит появился, кулдаун укорочен — иначе душили бы вдвоём
    [SerializeField] float dashCooldown = 0.45f;
    [SerializeField] float dashCost = 25f;      // цена рывка из бака
    [SerializeField] int dashRipDamage = 6;       // урон волку, когда срываемся рывком с захвата
    [SerializeField] int dashRipSelfDamage = 5;   // и себе — вырываться из пасти больно (минует i-frames рывка)

    [Header("Вид / прицел")]
    [SerializeField] float lookSensitivity = 0.1f;  // мышь, поворот в FPS
    [SerializeField] float gamepadYawSpeed = 180f;  // правый стик, поворот в FPS

    public bool FirstPerson { get; private set; }

    CharacterController controller;
    Knockback knockback;
    Health health;
    InputAction moveAction, lookAction, dashAction, toggleViewAction, sneakAction, sprintAction;
    Stamina stamina;   // дыхалка: гейтит рывок и спринт, на нуле — отдышка (замедление)
    Slow slow;         // замедление от игл ежа (до-создаётся эффектом): режет ход, не рывок
    Satiety satiety;   // истощение (пустая шкала голода) слабит ход
    Vector3 velocity;
    float dashTimer, dashReadyAt, groundY, legDashOverride; // legDashOverride: длинный рывок лосиных ног (0 = дефолт dashDuration)
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

        // ТИХИЙ ШАГ переехал с Shift на Ctrl: Shift занял спринт — «жму Shift = трачу» читается само,
        // и это привычнее по другим играм
        sneakAction = new InputAction("Sneak", InputActionType.Button); // ТИХИЙ ШАГ: держать
        sneakAction.AddBinding("<Keyboard>/leftCtrl");
        sneakAction.AddBinding("<Gamepad>/leftShoulder");

        sprintAction = new InputAction("Sprint", InputActionType.Button); // СПРИНТ: держать, жжёт стамину
        sprintAction.AddBinding("<Keyboard>/leftShift");
        sprintAction.AddBinding("<Gamepad>/leftStickPress");
    }

    void Start()
    {
        groundY = transform.position.y;
        SetFirstPerson(false);
        if (constrict == null) TryGetComponent(out constrict); // мог до-создаться в CreatureBody.Awake
    }

    void OnEnable() { moveAction.Enable(); lookAction.Enable(); dashAction.Enable(); toggleViewAction.Enable(); sneakAction.Enable(); sprintAction.Enable(); }
    void OnDisable() { moveAction.Disable(); lookAction.Disable(); dashAction.Disable(); toggleViewAction.Disable(); sneakAction.Disable(); sprintAction.Disable(); }

    void Update()
    {
        // КОНСТРУКТОР ОТКРЫТ (пауза сборки) — игрок не управляется: мышь принадлежит UI. Без этого тело
        // крутится от Mouse.delta прямо под открытым меню (единое управление рулит телом в обоих видах),
        // а перетаскивание звёзд молчит — указатель «прилип» к персонажу. Владение курсором — у конструктора
        if (ConstructorUI.IsOpen) return;

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

        // рывок: кулдаун держит ритм, СТАМИНА — общий лимит. Порядок в условии важен — TrySpend не должен
        // списывать бак, когда рывок и так не состоится по кулдауну
        if (stamina == null) TryGetComponent(out stamina); // бак до-создаёт тело в Recompute → привязка ленивая
        if (dashAction.WasPressedThisFrame() && Time.time >= dashReadyAt && dashTimer <= 0f
            && (stamina == null || stamina.TrySpend(DashCost)))
        {
            dashTimer = legDashOverride > 0f ? legDashOverride : dashDuration; // лосиные ноги — длиннее (таран прёт дальше)
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
        // СПРИНТ жжёт бак, пока держишь Shift и реально бежишь. На отдышке не включается — там своё
        // замедление, и бежать быстрее выдохшийся просто не может
        float sprint = 1f;
        if (sprintAction.IsPressed() && sneak >= 1f && dashTimer <= 0f && move.sqrMagnitude > 0.01f
            && stamina != null && !stamina.Exhausted)
        {
            stamina.Drain(SprintDrain * Time.deltaTime);
            sprint = SprintMult;
        }
        // ОТДЫШКА: выжал бак досуха — ползёшь, пока не отдышался. Это и есть цена перерасхода —
        // наказывает открытостью, а не запретом кнопки
        float winded = stamina != null ? stamina.MoveMult : 1f;
        // ЗАМЕДЛЕНИЕ (иглы ежа) — тот же множитель, что у NPC (там в NavLocomotion): режет ХОД, но не рывок —
        // увяз в булавках, зато рвануть ещё можешь (окно на разрыв дистанции честно оставлено)
        if (slow == null) TryGetComponent(out slow); // до-создаётся эффектом при первом попадании
        float mired = slow != null ? slow.MoveMult : 1f;
        if (satiety == null) TryGetComponent(out satiety); // тело заводит шкалу в Awake
        float starve = satiety != null ? satiety.Vigor : 1f; // истощён (пустая шкала голода) — ползёшь
        Vector3 horizontal = dashTimer > 0f ? dashDir * dashSpeed * grip : move * moveSpeed * grip * hold * sneak * sprint * winded * mired * starve;
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
        // свою голову от ПЕРВОГО лица не рендерим (нос/куб лезут в камеру) — классика FPS; в 3-м лице возвращаем.
        // Лицо (глаза/брови/борода из PlayerModel) прячется вместе с головой
        foreach (var r in GetComponentsInChildren<Renderer>())
            if (IsOwnFace(r.name)) r.enabled = !on;
    }

    // ЧТО ПРЯЧЕМ ОТ ПЕРВОГО ЛИЦА: всю голову целиком. Список синхронизирован с моделью игрока — у неё
    // голова нарезана по слотам органов, и без челюсти с ушами они висели бы в камере отдельно от лица
    static bool IsOwnFace(string n) =>
        n == "Head" || n == "Nose" || n == "Jaw" || n == "Teeth"
        || n == "EyeL" || n == "EyeR" || n == "EarL" || n == "EarR"
        || n == "BrowL" || n == "BrowR" || n == "Beard";

    // конструктор меняет мобильность при смене органа в слоте «Ноги»
    public void SetLegs(float newMoveSpeed, float newDashSpeed, float newDashDuration = 0f)
    {
        moveSpeed = newMoveSpeed;
        dashSpeed = newDashSpeed;
        legDashOverride = newDashDuration; // 0 → рывок на дефолтной длине (короткий); >0 → длинный (лосиные)
    }

    // слот «Чутьё»: быстрее откат рывка
    public void SetDashCooldown(float newCooldown) => dashCooldown = newCooldown;

    // захват волком: режем скорость, пока висит; рывок/пинок снимают
    public void ApplyGrab(IGrabber g, float slow) { grabber = g; grabSlow = Mathf.Clamp01(slow); } // 0 = полный корень (3-я стадия)
    public void ReleaseGrab(IGrabber g) { if (ReferenceEquals(grabber, g)) { grabber = null; grabSlow = 1f; } }

}
