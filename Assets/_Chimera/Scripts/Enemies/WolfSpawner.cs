using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Спавнит волков вокруг себя и поддерживает их количество (досыпает по мере гибели).
/// Спавнит из префаба; не ближе minDistanceFromPlayer к игроку.
/// </summary>
public class WolfSpawner : MonoBehaviour
{
    [SerializeField] GameObject wolfPrefab;
    [SerializeField] int maxAlive = 5;
    [SerializeField] float spawnRadius = 12f;
    [SerializeField] float spawnInterval = 2f;          // как часто досыпать до maxAlive
    [SerializeField] float minDistanceFromPlayer = 6f;  // не спавнить вплотную к игроку

    readonly List<GameObject> alive = new();
    Transform player;
    float nextSpawn;

    void Start()
    {
        var pc = FindAnyObjectByType<PlayerController>();
        if (pc != null) player = pc.transform;

        for (int i = 0; i < maxAlive; i++) TrySpawn(); // первичный наброс
    }

    void Update()
    {
        alive.RemoveAll(w => w == null); // убираем уже погибших

        if (Time.time >= nextSpawn)
        {
            nextSpawn = Time.time + spawnInterval;
            if (alive.Count < maxAlive) TrySpawn();
        }
    }

    void TrySpawn()
    {
        if (wolfPrefab == null) return;
        var wolf = Instantiate(wolfPrefab, PickSpawnPoint(), Quaternion.identity);
        alive.Add(wolf);
    }

    public void SpawnBurst(int n) { for (int i = 0; i < n; i++) TrySpawn(); } // dev: разовый наброс сверх лимита

    // призыв стаи вокруг точки (вой босса): на навмеш, кольцом радиусом 3..7
    public void SpawnAt(Vector3 center, int n)
    {
        if (wolfPrefab == null) return;
        for (int i = 0; i < n; i++)
        {
            Vector3 p = center;
            for (int a = 0; a < 12; a++)
            {
                Vector2 c = Random.insideUnitCircle.normalized * Random.Range(3f, 7f);
                if (NavMesh.SamplePosition(center + new Vector3(c.x, 0f, c.y), out var hit, 4f, NavMesh.AllAreas)) { p = hit.position; break; }
            }
            alive.Add(Instantiate(wolfPrefab, p, Quaternion.identity));
        }
    }

    Vector3 PickSpawnPoint()
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            Vector2 c = Random.insideUnitCircle * spawnRadius;
            Vector3 p = transform.position + new Vector3(c.x, 0f, c.y);
            if (!NavMesh.SamplePosition(p, out var hit, 4f, NavMesh.AllAreas)) continue; // спавним на навмеш, не в стену
            if (player == null || Vector3.Distance(hit.position, player.position) >= minDistanceFromPlayer)
                return hit.position;
        }
        return transform.position;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, spawnRadius);
    }
}
