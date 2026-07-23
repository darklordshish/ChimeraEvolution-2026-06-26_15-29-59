using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Локомоция по NavMesh для ИИ: КУДА идти (направление с учётом стен, троттлинг пути, блуждание)
/// и КАК ехать (Move: гравитация + сглаживание скорости). Извлечено из дублей психик.
///
/// АНТИ-ТРЯСКА (важное): психики задают скорость бинарно — «дальше дистанции удара → полный ход,
/// иначе → стоп». На самой границе это полный газ/полный тормоз каждый кадр — существо дрожит.
/// Лечим в ОДНОМ месте, чтобы не расползалось по видам:
///  • Arrive(): плавное торможение у цели вместо обрыва в ноль (шаблон уже был у тени-загона лося);
///  • сглаживание скорости в Move — резкие смены гасятся разгоном/торможением, а не рывком;
///  • дедзона: микро-скорости считаем нулём (иначе расталкивание вечно двигает стоящего).
/// </summary>
public class NavLocomotion : MonoBehaviour
{
    [SerializeField] float pathInterval = 0.2f;   // как часто пересчитывать путь
    [SerializeField] float pathDirectRange = 5f;  // ближе — идём напрямую, без пафайндинга

    [Header("Плавность хода (анти-тряска)")]
    [SerializeField] float accel = 18f;           // как быстро скорость догоняет заданную (м/с²); меньше = вальяжнее
    [SerializeField] float stopDeadzone = 0.35f;  // скорость ниже — считаем «стоим» (гасит дрожь расталкивания)
    [SerializeField] float gravity = -20f;

    NavMeshPath navPath;
    float nextPathTime;
    Vector3 cachedNavDir;
    Vector3 wanderTarget;
    float nextWanderTime;

    CharacterController controller;
    Vector3 smoothed;   // сглаженная горизонтальная скорость (между кадрами)
    float verticalVel;
    Slow slow;          // замедление (иглы ежа): ЕДИНЫЙ хук на всех NPC — психики трогать не надо

    void Awake()
    {
        navPath = new NavMeshPath();
        TryGetComponent(out controller);
    }

    /// <summary>ПЕРЕМЕЩЕНИЕ: гравитация + сглаживание горизонтальной скорости. Психика говорит «хочу
    /// вот такую скорость», а рывок/дрожь гасятся здесь — одинаково для всех видов.</summary>
    public void Move(Vector3 desiredHorizontal)
    {
        if (controller == null) return;
        // ЗАМЕДЛЕНИЕ — здесь, в единой точке хода: снаряд-иглы тянут вниз ЛЮБОГО NPC без правок его психики
        if (slow == null) TryGetComponent(out slow); // до-создаётся эффектом на первом попадании — привязка ленивая
        if (slow != null) desiredHorizontal *= slow.MoveMult;
        if (desiredHorizontal.sqrMagnitude < stopDeadzone * stopDeadzone) desiredHorizontal = Vector3.zero; // дедзона
        smoothed = Vector3.MoveTowards(smoothed, desiredHorizontal, accel * Time.deltaTime);

        if (controller.isGrounded && verticalVel < 0f) verticalVel = -2f;
        verticalVel += gravity * Time.deltaTime;
        Vector3 motion = smoothed; motion.y = verticalVel;
        controller.Move(motion * Time.deltaTime);
    }

    /// <summary>Сбросить накопленную скорость (телепорт/подъём на стену/конец переноски).</summary>
    public void ResetMotion() { smoothed = Vector3.zero; verticalVel = 0f; }

    /// <summary>ПЛАВНЫЙ ПОДХОД: полная скорость вдалеке, мягкое торможение в радиусе прибытия, ноль в цели.
    /// Замена бинарному «дальше порога — газ, иначе стоп», от которого существо дрожит на границе.</summary>
    public Vector3 Arrive(Vector3 dest, float speed, float arriveRadius = 1.5f, float stopAt = 0f)
    {
        Vector3 flat = dest - transform.position; flat.y = 0f;
        float d = flat.magnitude - stopAt;                    // stopAt — «дистанция удержания» (не лезем вплотную)
        if (d <= 0.01f) return Vector3.zero;
        float k = Mathf.Clamp01(d / Mathf.Max(arriveRadius, 0.01f)); // ближе к цели — медленнее
        return DirTo(dest) * speed * k;
    }

    /// <summary>Направление к точке с учётом стен (к следующему углу пути NavMesh).</summary>
    public Vector3 DirTo(Vector3 dest)
    {
        if (navPath == null) navPath = new NavMeshPath(); // страховка от domain-reload в Play
        Vector3 flat = dest - transform.position; flat.y = 0f;
        if (flat.sqrMagnitude < pathDirectRange * pathDirectRange)
            return flat.sqrMagnitude > 0.01f ? flat.normalized : transform.forward;

        if (Time.time >= nextPathTime)
        {
            nextPathTime = Time.time + pathInterval;
            cachedNavDir = Compute(dest);
        }
        return cachedNavDir;
    }

    Vector3 Compute(Vector3 dest)
    {
        if (NavMesh.CalculatePath(transform.position, dest, NavMesh.AllAreas, navPath) && navPath.corners.Length > 1)
        {
            Vector3 d = navPath.corners[1] - transform.position; d.y = 0f;
            if (d.sqrMagnitude > 0.01f) return d.normalized;
        }
        Vector3 fb = dest - transform.position; fb.y = 0f;
        return fb.sqrMagnitude > 0.01f ? fb.normalized : transform.forward;
    }

    /// <summary>Случайная точка на навмеше для блуждания (меняется по таймеру/при достижении).</summary>
    public Vector3 Wander(float radius)
    {
        if (Time.time >= nextWanderTime || (wanderTarget - transform.position).sqrMagnitude < 4f)
        {
            nextWanderTime = Time.time + Random.Range(2f, 5f);
            Vector2 c = Random.insideUnitCircle * radius;
            Vector3 cand = transform.position + new Vector3(c.x, 0f, c.y);
            wanderTarget = NavMesh.SamplePosition(cand, out var hit, 4f, NavMesh.AllAreas) ? hit.position : transform.position;
        }
        return wanderTarget;
    }
}
