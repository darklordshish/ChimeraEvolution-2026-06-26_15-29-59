using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Спавнит волков ПРЯМОУГОЛЬНИКОМ ПО КАРТЕ (как змеиный/лосиный спавнеры — единый паттерн) и
/// поддерживает число. Логова-кластера больше нет: волки рождаются рассеянно и сбиваются в стаи
/// САМИ (вои/след/точки сбора). Призыв воем босса (SpawnAt) по-прежнему кольцом вокруг него.
/// </summary>
public class WolfSpawner : MonoBehaviour
{
    [SerializeField] GameObject wolfPrefab;
    [SerializeField] int maxAlive = 30;
    [SerializeField] float mapHalfExtent = 95f;         // полукрай зоны спавна (под арену 200)
    [SerializeField] float spawnInterval = 2f;          // как часто досыпать до maxAlive
    [SerializeField] float minDistanceFromPlayer = 12f; // не спавнить на глазах у игрока

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
        // ступенчатое ослабление (паттерн змеиного спавнера): вне глаз игрока → лишь бы на навмеше
        for (int pass = 0; pass < 2; pass++)
            for (int attempt = 0; attempt < 40; attempt++)
            {
                Vector3 p = transform.position + new Vector3(Random.Range(-mapHalfExtent, mapHalfExtent), 0f,
                                                             Random.Range(-mapHalfExtent, mapHalfExtent));
                if (!NavMesh.SamplePosition(p, out var hit, 8f, NavMesh.AllAreas)) continue; // на навмеш, не в стену
                if (pass == 0 && player != null && Vector3.Distance(hit.position, player.position) < minDistanceFromPlayer) continue;
                return hit.position;
            }
        Debug.LogWarning("WolfSpawner: не нашёл точку на NavMesh — проверь позицию спавнера/mapHalfExtent.");
        return transform.position;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position, new Vector3(mapHalfExtent * 2f, 1f, mapHalfExtent * 2f));
    }
}
