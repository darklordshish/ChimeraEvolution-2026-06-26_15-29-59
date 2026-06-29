using UnityEngine;

/// <summary>
/// Простой враг-волк без NavMesh (на плоскости без препятствий хватает «беги к игроку»).
/// Бежит к цели, у радиуса атаки кусает по кулдауну. NavMesh подключим, когда появится геометрия уровня.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class WolfAI : MonoBehaviour
{
    [Header("Погоня")]
    [SerializeField] float moveSpeed = 4f;
    [SerializeField] float rotationSpeed = 540f;
    [SerializeField] float gravity = -20f;
    [SerializeField] float sightRange = 25f; // дальше игрока не замечает

    [Header("Атака")]
    [SerializeField] float attackRange = 1.8f;
    [SerializeField] int attackDamage = 8;
    [SerializeField] float attackCooldown = 1.2f;

    CharacterController controller;
    Transform target;
    Health targetHealth;
    float nextAttackTime;
    float verticalVel;

    void Awake() => controller = GetComponent<CharacterController>();

    void Start()
    {
        var player = FindAnyObjectByType<PlayerController>();
        if (player != null)
        {
            target = player.transform;
            targetHealth = player.GetComponent<Health>();
        }
    }

    void Update()
    {
        if (target == null) { Settle(Vector3.zero); return; }

        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;
        float dist = toTarget.magnitude;

        if (dist > sightRange) { Settle(Vector3.zero); return; }

        Vector3 dir = dist > 0.001f ? toTarget / dist : Vector3.zero;

        // поворот мордой к цели
        if (dir.sqrMagnitude > 0.001f)
        {
            Quaternion look = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, rotationSpeed * Time.deltaTime);
        }

        // подходим, пока не в радиусе атаки; в радиусе — кусаем
        Vector3 horizontal = dist > attackRange ? dir * moveSpeed : Vector3.zero;
        if (dist <= attackRange) TryBite();

        Settle(horizontal);
    }

    void TryBite()
    {
        if (Time.time < nextAttackTime || targetHealth == null) return;
        nextAttackTime = Time.time + attackCooldown;
        targetHealth.TakeDamage(attackDamage);
    }

    // применяет горизонтальное движение + гравитацию одним Move
    void Settle(Vector3 horizontal)
    {
        if (controller.isGrounded && verticalVel < 0f) verticalVel = -2f;
        verticalVel += gravity * Time.deltaTime;
        Vector3 motion = horizontal;
        motion.y = verticalVel;
        controller.Move(motion * Time.deltaTime);
    }

    // жёлтая сфера — радиус укуса. Всегда видна (см. тумблер Gizmos в Game view)
    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.5f, attackRange);
    }
}
