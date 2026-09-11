using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// ТЕСТБЕД (#4b-1): спавнит химеру со СЛУЧАЙНЫМ составом — от доминанты одного вида до истинной химеры, не кина
/// никому, — красит по составу и показывает идентичность (MostKin, ридаут в дев-панели). Шасси — случайный вид.
/// Рождается общим путём `ChimeraFactory`, тем же, что босс: собственного сборщика у тестбеда больше нет.
/// Пул видов — из инспектора, а если пуст — шасси и доноры тела игрока (все пять видов). Спавн — кнопкой в дев-панели.
/// </summary>
public class TestChimeraSpawner : MonoBehaviour
{
    [SerializeField] SpeciesSO[] species;    // пул шасси/доноров; пусто → берётся у тела игрока
    [SerializeField] float spawnRadius = 8f; // на каком расстоянии от игрока (или спавнера, если игрока нет)
    [SerializeField] int maxAugments = 8;    // потолок случайных аугументов (0..N → спаннинг спектра)

    SpeciesSO[] Pool()
    {
        if (species != null && species.Length > 0) return species;
        var pb = CreatureBody.PlayerBody;
        if (pb == null || pb.Chassis == null) return null;
        var list = new List<SpeciesSO> { pb.Chassis };
        if (pb.Donors != null) foreach (var d in pb.Donors) if (d != null && !list.Contains(d)) list.Add(d);
        return list.ToArray();
    }

    public CreatureBody SpawnRandom()
    {
        var pool = Pool();
        if (pool == null || pool.Length == 0) { Debug.LogWarning("TestChimeraSpawner: пула видов нет — ни в инспекторе, ни у тела игрока"); return null; }

        var pb = CreatureBody.PlayerBody;
        Vector3 origin = pb != null ? pb.transform.position : transform.position;
        Vector2 dir = Random.insideUnitCircle.normalized;
        if (dir == Vector2.zero) dir = Vector2.right;
        Vector3 pos = origin + new Vector3(dir.x, 0f, dir.y) * Mathf.Max(4f, spawnRadius);
        if (NavMesh.SamplePosition(pos, out var hit, 10f, NavMesh.AllAreas)) pos = hit.position;

        var chassis = pool[Random.Range(0, pool.Length)];
        int n = Random.Range(0, maxAugments + 1);
        return ChimeraFactory.Spawn(chassis, pool, pos + Vector3.up * 0.5f, "TestChimera", compose: body =>
        {
            // СПАННИНГ: случайное число аугументов в случайные слоты — от «почти чистого» до каши.
            // Реролл даёт и доминантных (видовой модуль психики), и истинных химер (химера-альфа)
            for (int i = 0; i < n; i++)
            {
                int slot = Random.Range(0, body.SlotCount);
                var vars = body.GetVariants(slot);
                if (vars.Count > 0) body.Install(slot, Random.Range(0, vars.Count));
            }
        });
    }
}
