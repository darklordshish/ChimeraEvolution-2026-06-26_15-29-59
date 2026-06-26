using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Движение игрока от третьего лица на CharacterController.
/// Ввод — новый Input System, биндинги создаются в коде (вешать сразу, без настройки в инспекторе).
/// Движение относительно камеры; персонаж поворачивается лицом по направлению хода.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Движение")]
    [SerializeField] float moveSpeed = 6f;
    [SerializeField] float rotationSpeed = 720f; // град/сек — скорость доворота к направлению хода
    [SerializeField] float gravity = -20f;

    CharacterController controller;
    InputAction moveAction;
    Transform cam;
    Vector3 velocity; // храним только Y (гравитация)

    void Awake()
    {
        controller = GetComponent<CharacterController>();

        // WASD + стрелки + левый стик геймпада
        moveAction = new InputAction("Move", InputActionType.Value);
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
            .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
        moveAction.AddBinding("<Gamepad>/leftStick");

        var mainCam = Camera.main;
        if (mainCam != null) cam = mainCam.transform;
    }

    void OnEnable() => moveAction.Enable();
    void OnDisable() => moveAction.Disable();

    void Update()
    {
        Vector2 input = moveAction.ReadValue<Vector2>();

        // оси камеры, спроецированные на горизонталь
        Vector3 forward = cam != null ? cam.forward : Vector3.forward;
        Vector3 right = cam != null ? cam.right : Vector3.right;
        forward.y = 0f; right.y = 0f;
        forward.Normalize(); right.Normalize();

        Vector3 move = forward * input.y + right * input.x; // нормализованное направление хода

        // доворот лицом к направлению
        if (move.sqrMagnitude > 0.01f)
        {
            Quaternion target = Quaternion.LookRotation(move);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, rotationSpeed * Time.deltaTime);
        }

        // гравитация
        if (controller.isGrounded && velocity.y < 0f) velocity.y = -2f;
        velocity.y += gravity * Time.deltaTime;

        // одно общее перемещение: горизонталь + вертикаль
        Vector3 motion = move * moveSpeed;
        motion.y = velocity.y;
        controller.Move(motion * Time.deltaTime);
    }
}
