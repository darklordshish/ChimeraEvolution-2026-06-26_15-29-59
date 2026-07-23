using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Спавнит ЕЖЕЙ рандомно по карте и поддерживает их число. Ёж — одиночка, но не такой редкий, как лось:
/// он не туша-событие, а колючая помеха, на которую натыкаешься по дороге. Объект в сцене; префаб —
/// «Chimera → Создать префаб Ежа».
///
/// ДОЛГ: это ПЯТЫЙ спавнер с почти одинаковым телом (волк/змея/лось/вервольф/ёж). Ступенчатый подбор
/// точки, поддержание числа, разлёт — общие; различаются только числа и гизмо. Просится общий базовый
/// класс, но вытаскивать его посреди слайса вида — значит менять поведение четырёх существующих
/// спавнеров без плейтеста. Вынесено в отдельную задачу.
/// </summary>
public class HedgehogSpawner : MonoBehaviour
{
    [SerializeField] GameObject hedgehogPrefab;
    [SerializeField] int maxAlive = 5;
    [SerializeField] float mapHalfExtent = 72f;          // полукрай зоны спавна (под арену 150)
    [SerializeField] float spawnInterval = 8f;
    [SerializeField] float minDistanceFromPlayer = 15f;  // не вылупляться на глазах
    [SerializeField] float spacing = 14f;                // одиночки, но плотнее лосей

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
        alive.RemoveAll(h => h == null);

        if (Time.time >= nextSpawn)
        {
            nextSpawn = Time.time + spawnInterval;
            if (alive.Count < maxAlive) TrySpawn();
        }
    }

    void TrySpawn()
    {
        if (hedgehogPrefab == null) return;
        alive.Add(Instantiate(hedgehogPrefab, PickSpawnPoint(), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f)));
    }

    public void SpawnBurst(int n) { for (int i = 0; i < n; i++) TrySpawn(); } // dev: разовый наброс сверх лимита

    Vector3 PickSpawnPoint()
    {
        // ступенчатое ослабление (общий паттерн спавнеров): строгие условия → мягче → лишь бы на навмеше
        for (int pass = 0; pass < 3; pass++)
            for (int attempt = 0; attempt < 40; attempt++)
            {
                Vector3 p = transform.position + new Vector3(Random.Range(-mapHalfExtent, mapHalfExtent), 0f,
                                                             Random.Range(-mapHalfExtent, mapHalfExtent));
                if (!NavMesh.SamplePosition(p, out var hit, 8f, NavMesh.AllAreas)) continue;
                if (pass <= 1 && player != null && Vector3.Distance(hit.position, player.position) < minDistanceFromPlayer) continue;
                if (pass == 0 && TooCloseToOther(hit.position)) continue;
                return hit.position;
            }
        Debug.LogWarning("HedgehogSpawner: не нашёл точку на NavMesh — проверь позицию спавнера/mapHalfExtent.");
        return transform.position;
    }

    bool TooCloseToOther(Vector3 p)
    {
        foreach (var h in alive)
            if (h != null && (h.transform.position - p).sqrMagnitude < spacing * spacing) return true;
        return false;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.68f, 0.6f, 0.5f); // = Ёж.tint
        Gizmos.DrawWireCube(transform.position, new Vector3(mapHalfExtent * 2f, 1f, mapHalfExtent * 2f));
    }
}
