using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Призывает босса-вервольфа, когда родство с видом достигает порога — кульминация ветки волка.
/// Пока босс жив — нового не плодит; после его смерти через паузу появляется новый (так, пока родство ≥ порога).
/// Положи на пустой объект в сцене и назначь префаб вервольфа в инспекторе.
/// </summary>
public class WerewolfSpawner : MonoBehaviour
{
    [SerializeField] GameObject werewolfPrefab;
    [SerializeField] string species = "Волк";
    [SerializeField] int triggerAffinity = 80;
    [SerializeField] float spawnDistance = 16f;  // на каком расстоянии от игрока появляется
    [SerializeField] float respawnDelay = 5f;    // пауза после смерти босса до следующего
    [SerializeField] bool autoSpawn = true;      // авто-призыв по родству; выключается в Dev-панели (тесты без босса)

    public bool AutoSpawn { get => autoSpawn; set => autoSpawn = value; }

    bool warned;
    float nextSpawnTime;
    Transform player;

    void Start()
    {
        var pc = FindAnyObjectByType<PlayerController>();
        if (pc != null) player = pc.transform;
    }

    void Update()
    {
        if (!autoSpawn) return;
        if (AffinityTracker.Get(species) < triggerAffinity) return;

        if (werewolfPrefab == null)
        {
            if (!warned) { Debug.LogWarning("WerewolfSpawner: родство достигло порога, но поле Werewolf Prefab ПУСТОЕ — назначь префаб!"); warned = true; }
            return;
        }

        if (FindAnyObjectByType<WerewolfPsyche>() != null) // босс жив — следующего не плодим, держим паузу свежей
        {
            nextSpawnTime = Time.time + respawnDelay;
            return;
        }

        if (Time.time < nextSpawnTime) return; // пауза-передышка после смерти босса

        Instantiate(werewolfPrefab, PickSpawnPos(), Quaternion.identity);
    }

    // точка на навмеше в кольце spawnDistance вокруг игрока (не вплотную)
    Vector3 PickSpawnPos()
    {
        Vector3 center = player != null ? player.position : transform.position;
        for (int i = 0; i < 16; i++)
        {
            Vector2 c = Random.insideUnitCircle.normalized * spawnDistance;
            if (NavMesh.SamplePosition(center + new Vector3(c.x, 0f, c.y), out var hit, 6f, NavMesh.AllAreas))
                return hit.position;
        }
        return center;
    }
}
