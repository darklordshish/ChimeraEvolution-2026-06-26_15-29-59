using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Движение от третьего лица + НЕЗАВИСИМЫЙ прицел (twin-stick).
/// Двигаешься на WASD/левый стик (относительно камеры), а смотришь/бьёшь туда, куда целишь:
/// мышь (луч в пол) или правый стик геймпада. Можно отступать лицом к врагу.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Движение")]
    [SerializeField] float moveSpeed = 6f;
    [SerializeField] float aimRotationSpeed = 1080f; // доворот к прицелу (большой = почти мгновенно)
    [SerializeField] float gravity = -20f;

    CharacterController controller;
    InputAction moveAction;
    InputAction lookAction;   // правый стик геймпада
    Camera cam;
    Vector3 velocity;         // храним только Y (гравитация)

    void Awake()
    {
        controller = GetComponent<CharacterController>();

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
    }

    void Start() => cam = Camera.main;

    void OnEnable() { moveAction.Enable(); lookAction.Enable(); }
    void OnDisable() { moveAction.Disable(); lookAction.Disable(); }

    void Update()
    {
        Vector3 camF = cam != null ? cam.transform.forward : Vector3.forward;
        Vector3 camR = cam != null ? cam.transform.right : Vector3.right;
        camF.y = 0f; camR.y = 0f; camF.Normalize(); camR.Normalize();

        // движение — относительно камеры (НЕ зависит от взгляда)
        Vector2 mv = moveAction.ReadValue<Vector2>();
        Vector3 move = camF * mv.y + camR * mv.x;

        // прицел — отдельно: правый стик, иначе мышь
        Vector3 aim = AimDirection(camF, camR);
        if (aim.sqrMagnitude > 0.001f)
        {
            Quaternion look = Quaternion.LookRotation(aim);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, aimRotationSpeed * Time.deltaTime);
        }

        // гравитация + общее перемещение
        if (controller.isGrounded && velocity.y < 0f) velocity.y = -2f;
        velocity.y += gravity * Time.deltaTime;
        Vector3 motion = move * moveSpeed;
        motion.y = velocity.y;
        controller.Move(motion * Time.deltaTime);
    }

    Vector3 AimDirection(Vector3 camF, Vector3 camR)
    {
        // 1) геймпад — правый стик (относительно камеры)
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

        return Vector3.zero; // нет ввода — сохраняем текущий поворот
    }
}
