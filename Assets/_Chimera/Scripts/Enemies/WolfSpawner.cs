using System.Collections.Generic;
using UnityEngine;

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

    Vector3 PickSpawnPoint()
    {
        for (int attempt = 0; attempt < 10; attempt++)
        {
            Vector2 c = Random.insideUnitCircle * spawnRadius;
            Vector3 p = transform.position + new Vector3(c.x, 0f, c.y);
            if (player == null || Vector3.Distance(p, player.position) >= minDistanceFromPlayer)
                return new Vector3(p.x, transform.position.y, p.z);
        }
        return transform.position;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, spawnRadius);
    }
}
