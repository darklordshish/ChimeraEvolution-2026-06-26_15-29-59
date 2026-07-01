using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Локомоция по NavMesh для ИИ: направление к точке с учётом стен (троттлинг пересчёта пути)
/// и случайная точка блуждания. Само перемещение (CharacterController) остаётся у существа —
/// здесь только «куда идти». Извлечено из дублей WolfAI/WerewolfBoss (Фаза 0).
/// </summary>
public class NavLocomotion : MonoBehaviour
{
    [SerializeField] float pathInterval = 0.2f;   // как часто пересчитывать путь
    [SerializeField] float pathDirectRange = 5f;  // ближе — идём напрямую, без пафайндинга

    NavMeshPath navPath;
    float nextPathTime;
    Vector3 cachedNavDir;
    Vector3 wanderTarget;
    float nextWanderTime;

    void Awake() => navPath = new NavMeshPath();

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
