using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Спавнит лосей РАНДОМНО ПО КАРТЕ и поддерживает их число (досыпает по мере гибели — медленно:
/// туша редкая). Лоси — одиночки: разлёт mooseSpacing держит их поодаль друг от друга, но в пределах
/// слышимости рёва (цепная ярость ~26м срабатывает, когда выпас сводит их ближе). Не вплотную к стенам
/// (крупная туша) и не на глазах игрока. Объект в сцене; префаб — «Chimera → Создать префаб Лося».
/// </summary>
public class MooseSpawner : MonoBehaviour
{
    [SerializeField] GameObject moosePrefab;
    [SerializeField] int maxAlive = 3;
    [SerializeField] float mapHalfExtent = 72f;          // полукрай зоны спавна (под арену 150)
    [SerializeField] float spawnInterval = 12f;          // досып медленный — лось не расходник
    [SerializeField] float minDistanceFromPlayer = 18f;  // появляться вне поля зрения
    [SerializeField] float mooseSpacing = 22f;           // одиночки: поодаль друг от друга (но цепь рёва ~26м достаёт)
    [SerializeField] float wallClearance = 2.5f;         // крупной туше не место впритык к стене

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
        alive.RemoveAll(m => m == null);

        if (Time.time >= nextSpawn)
        {
            nextSpawn = Time.time + spawnInterval;
            if (alive.Count < maxAlive) TrySpawn();
        }
    }

    void TrySpawn()
    {
        if (moosePrefab == null) return;
        alive.Add(Instantiate(moosePrefab, PickSpawnPoint(), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f)));
    }

    public void SpawnBurst(int n) { for (int i = 0; i < n; i++) TrySpawn(); } // dev: разовый наброс сверх лимита

    Vector3 PickSpawnPoint()
    {
        // ступенчатое ослабление (паттерн змеиного спавнера): строгие условия → мягче → лишь бы на навмеше
        for (int pass = 0; pass < 3; pass++)
            for (int attempt = 0; attempt < 40; attempt++)
            {
                Vector3 p = transform.position + new Vector3(Random.Range(-mapHalfExtent, mapHalfExtent), 0f,
                                                             Random.Range(-mapHalfExtent, mapHalfExtent));
                if (!NavMesh.SamplePosition(p, out var hit, 8f, NavMesh.AllAreas)) continue;
                if (pass <= 1 && NavMesh.FindClosestEdge(hit.position, out var edge, NavMesh.AllAreas)
                              && edge.distance < wallClearance) continue;
                if (pass <= 1 && player != null && Vector3.Distance(hit.position, player.position) < minDistanceFromPlayer) continue;
                if (pass == 0 && TooCloseToOtherMoose(hit.position)) continue;
                return hit.position;
            }
        Debug.LogWarning("MooseSpawner: не нашёл точку на NavMesh — проверь позицию спавнера/mapHalfExtent.");
        return transform.position;
    }

    bool TooCloseToOtherMoose(Vector3 p)
    {
        foreach (var m in alive)
            if (m != null && (m.transform.position - p).sqrMagnitude < mooseSpacing * mooseSpacing) return true;
        return false;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.55f, 0.4f, 0.2f); // бурый — лосиный
        Gizmos.DrawWireCube(transform.position, new Vector3(mapHalfExtent * 2f, 1f, mapHalfExtent * 2f));
    }
}
