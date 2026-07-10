using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Спавнит змей РАНДОМНО ПО ВСЕЙ КАРТЕ и поддерживает их число (досыпает по мере гибели).
/// Змея — скрытый засадный хищник: точки разбросаны по арене (не кластером у спавнера, в отличие
/// от волчьего логова) и не ближе minDistanceFromPlayer к игроку — засада не вылупляется на глазах.
/// Объект в сцене (тюнится в инспекторе); префаб — из «Chimera → Создать префаб Змеи».
/// </summary>
public class SnakeSpawner : MonoBehaviour
{
    [SerializeField] GameObject snakePrefab;
    [SerializeField] int maxAlive = 3;
    [SerializeField] float mapHalfExtent = 48f;          // полукрай зоны спавна от позиции спавнера (арена 100 → 48)
    [SerializeField] float spawnInterval = 6f;           // как часто досыпать до maxAlive
    [SerializeField] float minDistanceFromPlayer = 15f;  // появляться вне поля зрения игрока
    [SerializeField] float snakeSpacing = 20f;           // разлёт между змеями — засады не кучкуются
    [SerializeField] float wallClearance = 1.8f;         // зазор до кромки навмеша: змея длинная — впритык к стене торчит сквозь неё

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
        alive.RemoveAll(s => s == null);

        if (Time.time >= nextSpawn)
        {
            nextSpawn = Time.time + spawnInterval;
            if (alive.Count < maxAlive) TrySpawn();
        }
    }

    void TrySpawn()
    {
        if (snakePrefab == null) return;
        // случайный разворот: затаившиеся змеи смотрят кто куда, а не строем
        alive.Add(Instantiate(snakePrefab, PickSpawnPoint(), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f)));
    }

    public void SpawnBurst(int n) { for (int i = 0; i < n; i++) TrySpawn(); } // dev: разовый наброс сверх лимита

    Vector3 PickSpawnPoint()
    {
        // ступенчатое ослабление: (0) далеко от игрока И от других змей → (1) далеко от игрока →
        // (2) лишь бы на навмеше. Фолбэк в позицию спавнера — только совсем безнадёжно (и с криком).
        for (int pass = 0; pass < 3; pass++)
            for (int attempt = 0; attempt < 40; attempt++)
            {
                Vector3 p = transform.position + new Vector3(Random.Range(-mapHalfExtent, mapHalfExtent), 0f,
                                                             Random.Range(-mapHalfExtent, mapHalfExtent));
                if (!NavMesh.SamplePosition(p, out var hit, 8f, NavMesh.AllAreas)) continue; // на навмеш, не в стену
                if (pass <= 1 && NavMesh.FindClosestEdge(hit.position, out var edge, NavMesh.AllAreas)
                              && edge.distance < wallClearance) continue; // не впритык к стене (длинное тело торчит сквозь)
                if (pass <= 1 && player != null && Vector3.Distance(hit.position, player.position) < minDistanceFromPlayer) continue;
                if (pass == 0 && TooCloseToOtherSnake(hit.position)) continue;
                return hit.position;
            }
        Debug.LogWarning("SnakeSpawner: не нашёл точку на NavMesh по всей зоне — проверь позицию спавнера/масштаб mapHalfExtent.");
        return transform.position;
    }

    bool TooCloseToOtherSnake(Vector3 p)
    {
        foreach (var s in alive)
            if (s != null && (s.transform.position - p).sqrMagnitude < snakeSpacing * snakeSpacing) return true;
        return false;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.8f, 0.3f);
        Gizmos.DrawWireCube(transform.position, new Vector3(mapHalfExtent * 2f, 1f, mapHalfExtent * 2f));
    }
}
