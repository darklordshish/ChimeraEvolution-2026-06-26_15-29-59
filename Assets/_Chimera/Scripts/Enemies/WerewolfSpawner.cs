using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Призывает босса-суперхимеру (пока вервольф), когда родство со ВСЕМИ донор-видами достигает порога —
/// кульминация всего леса, не одной ветки волка. Пока босс жив — нового не плодит; после смерти через
/// (долгую) паузу появляется новый, пока родство держится ≥ порога. Положи на объект в сцене + назначь префаб.
/// </summary>
public class WerewolfSpawner : MonoBehaviour
{
    [SerializeField] GameObject werewolfPrefab;
    [SerializeField] int triggerAffinity = 75;   // порог по КАЖДОМУ донор-виду (все виды освоены → апекс приходит)
    [SerializeField] float spawnDistance = 16f;  // на каком расстоянии от игрока появляется
    [SerializeField] float respawnDelay = 45f;   // ДОЛГАЯ пауза: апекс-босс редкий, не спамим тушу за тушей
    [SerializeField] bool autoSpawn;             // авто-призыв по родству. ВЫКЛЮЧЕН по умолчанию: родство на отладке
                                                 // крутят постоянно, а босс ломает естественный порядок сцены —
                                                 // включать осознанно (тумблер в Dev-панели), а не ловить внезапно

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
        var pb = CreatureBody.PlayerBody; // родство теперь локальное — читаем тело игрока
        if (pb == null || !pb.AllDonorsAffinityAtLeast(triggerAffinity)) return; // ВСЕ виды освоены до порога

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
