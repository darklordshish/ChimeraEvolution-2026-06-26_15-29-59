using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Движение от третьего лица + независимый прицел (twin-stick) + рывок с i-frames.
/// Двигаешься WASD/левый стик (относительно камеры), целишься мышью/правым стиком,
/// рывок на Space/A — короткий бросок в сторону движения с неуязвимостью.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Движение")]
    [SerializeField] float moveSpeed = 6f;
    [SerializeField] float aimRotationSpeed = 1080f; // доворот к прицелу (большой = почти мгновенно)
    [SerializeField] float gravity = -20f;

    [Header("Рывок")]
    [SerializeField] float dashSpeed = 20f;
    [SerializeField] float dashDuration = 0.18f;
    [SerializeField] float dashCooldown = 0.7f;

    CharacterController controller;
    Health health;
    InputAction moveAction, lookAction, dashAction;
    Camera cam;
    Vector3 velocity;       // только Y (гравитация)

    float dashTimer, dashReadyAt;
    Vector3 dashDir;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        health = GetComponent<Health>();

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
        dashAction.AddBinding("<Gamepad>/buttonSouth");
    }

    void Start() => cam = Camera.main;

    void OnEnable() { moveAction.Enable(); lookAction.Enable(); dashAction.Enable(); }
    void OnDisable() { moveAction.Disable(); lookAction.Disable(); dashAction.Disable(); }

    void Update()
    {
        Vector3 camF = cam != null ? cam.transform.forward : Vector3.forward;
        Vector3 camR = cam != null ? cam.transform.right : Vector3.right;
        camF.y = 0f; camR.y = 0f; camF.Normalize(); camR.Normalize();

        // движение — относительно камеры (не зависит от взгляда)
        Vector2 mv = moveAction.ReadValue<Vector2>();
        Vector3 move = camF * mv.y + camR * mv.x;

        // прицел — отдельно (правый стик, иначе мышь); работает и в рывке
        Vector3 aim = AimDirection(camF, camR);
        if (aim.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(aim), aimRotationSpeed * Time.deltaTime);

        // старт рывка
        if (dashAction.WasPressedThisFrame() && Time.time >= dashReadyAt && dashTimer <= 0f)
        {
            dashTimer = dashDuration;
            dashReadyAt = Time.time + dashCooldown;
            dashDir = move.sqrMagnitude > 0.01f ? move.normalized : transform.forward; // куда рулишь, иначе вперёд
        }

        // горизонтальная скорость: рывок или обычный ход
        Vector3 horizontal;
        if (dashTimer > 0f)
        {
            dashTimer -= Time.deltaTime;
            horizontal = dashDir * dashSpeed;
        }
        else
        {
            horizontal = move * moveSpeed;
        }

        if (health != null) health.Invulnerable = dashTimer > 0f; // i-frames на время рывка

        // гравитация + общий Move
        if (controller.isGrounded && velocity.y < 0f) velocity.y = -2f;
        velocity.y += gravity * Time.deltaTime;
        Vector3 motion = horizontal;
        motion.y = velocity.y;
        controller.Move(motion * Time.deltaTime);
    }

    Vector3 AimDirection(Vector3 camF, Vector3 camR)
    {
        // 1) геймпад — правый стик
        Vector2 look = lookAction.ReadValue<Vector2>();
        if (look.sqrMagnitude > 0.04f)
        {
            Vector3 d = camF * look.y + camR * look.x;
            d.y = 0f;
            return d;
        }

        // 2) мышь — луч в горизонтальную плоскость на высоте игрока
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
