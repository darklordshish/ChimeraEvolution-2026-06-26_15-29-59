using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>БОСС-ХИМЕРА — спавнер-тюнинг в сцене (правило проекта: настраиваемое — объектом, не бутстрапом).
///     Шасси — ВСЕГДА Человек: сценарный закон «босс — химера человека с кем-то». Доноры берутся у тела игрока —
/// босс буквально «такой же, как ты, только зашёл дальше». Состав набирается конструктором по рецепту, а
/// боссовость навешивается отдельным модулем (`Bossness`) — сборщика «под босса» нет.
///     Рецептов два (геймдизайнер, 11.09): ОБОРОТЕНЬ — чистый зверолюд, человек плюс весь набор ОДНОГО вида и
/// химерные слоты боссовости сверху, «с приколами»; СЛУЧАЙНАЯ ХИМЕРА — каша. Удалённый 11.09 вервольф-вид
/// теперь просто один из исходов оборотня — тот, где выпал волк.</summary>
public class BossChimeraSpawner : MonoBehaviour
{
    public enum Recipe
    {
        [InspectorName("Случайная химера")] RandomBeasts,
        [InspectorName("Оборотень")] Werebeast, // значение 1 — как у прежнего «Вервольфа»: сохранённая сцена не сбивается
    }

    [SerializeField] Recipe recipe = Recipe.RandomBeasts;
    [Tooltip("Рецепт «Случайная»: попыток поставить чужой орган в родной слот")]
    [SerializeField, Min(0)] int randomOrgans = 5;
    [Tooltip("Рецепт «Оборотень»: чей набор. Пусто — случайный вид из доноров игрока")]
    [SerializeField] string wereSpecies = "";
    [SerializeField] Bossness.Settings bossness = new();

    [Header("Автоспавн по родству")]
    [SerializeField] bool autoSpawn;
    [SerializeField, Range(0, 100)] int affinityThreshold = 75; // родство игрока ко ВСЕМ донорам
    [SerializeField] float respawnDelay = 120f;
    [SerializeField] float spawnDistance = 18f;

    CreatureBody current;
    float nextSpawn;

    public CreatureBody Current => current;
    public bool AutoSpawn { get => autoSpawn; set => autoSpawn = value; }
    float SpawnDistance => spawnDistance > 0f ? spawnDistance : 18f;

    void Update()
    {
        if (!autoSpawn || current != null || Time.time < nextSpawn) return;
        var pb = CreatureBody.PlayerBody;
        if (pb == null || !pb.AllDonorsAffinityAtLeast(affinityThreshold)) return;
        SpawnNearPlayer(recipe);
    }

    public CreatureBody SpawnNearPlayer(Recipe r)
    {
        var pb = CreatureBody.PlayerBody;
        Vector3 origin = pb != null ? pb.transform.position : transform.position;
        Vector2 dir = Random.insideUnitCircle.normalized;
        if (dir == Vector2.zero) dir = Vector2.right;
        Vector3 pos = origin + new Vector3(dir.x, 0f, dir.y) * SpawnDistance;
        if (NavMesh.SamplePosition(pos, out var hit, 10f, NavMesh.AllAreas)) pos = hit.position;
        return Spawn(r, pos);
    }

    /// <param name="ground">точка на земле; центр носителя поднимается на полкапсулы с учётом размера босса</param>
    public CreatureBody Spawn(Recipe r, Vector3 ground)
    {
        var pb = CreatureBody.PlayerBody;
        if (pb == null || pb.Chassis == null || pb.Donors == null || pb.Donors.Length == 0)
        {
            Debug.LogWarning("BossChimeraSpawner: нет тела игрока с донорами — не из кого собрать босса");
            return null;
        }
        var human = pb.Chassis;
        if (human.speciesName != "Человек")
            Debug.LogWarning($"BossChimeraSpawner: шасси игрока «{human.speciesName}», а босс по сценарию — химера ЧЕЛОВЕКА");

        string were = r == Recipe.Werebeast ? PickWereSpecies(pb.Donors, human) : null;
        if (r == Recipe.Werebeast && were == null)
        {
            Debug.LogWarning("BossChimeraSpawner: среди доноров игрока нет зверя — оборотню не из кого");
            return null;
        }
        // награда — за ТИП босса: оборотень любого вида считается одним типом, имя объекта говорит, кто выпал
        string typeId = r == Recipe.Werebeast ? "Босс-оборотень" : "Босс-химера";
        string name = r == Recipe.Werebeast ? $"Оборотень ({were})" : typeId;
        float scale = bossness != null && bossness.sizeScale > 0f ? bossness.sizeScale : 1f;
        Bossness module = null;

        var body = ChimeraFactory.Spawn(human, pb.Donors, ground + Vector3.up * (0.5f * scale), name,
            beforeBody: go => module = Bossness.AttachBefore(go, bossness),
            compose: b =>
            {
                if (r == Recipe.Werebeast) ChimeraFactory.InstallAllFrom(b, were);
                else ChimeraFactory.InstallRandomBeasts(b, randomOrgans);
                module.Extend(b);
            });
        if (body == null) return null;

        module.Finish(body, typeId);
        if (body.TryGetComponent<Health>(out var hp))
            hp.onDeath.AddListener(() => { if (current == body) { current = null; nextSpawn = Time.time + respawnDelay; } });
        current = body;

        var dom = body.MostKin(out var tier);
        Debug.Log($"{name}: доминанта {(dom != null ? $"{dom.speciesName} ({tier})" : "нет — истинная химера")} · " +
                  $"HP {(hp != null ? hp.Max : 0)} · {ChimeraFactory.Describe(body)}", body);
        return body;
    }

    // вид оборотня: задан в инспекторе — он, иначе случайный зверь из доноров (шасси-человек в оборотни не годится)
    string PickWereSpecies(SpeciesSO[] donors, SpeciesSO chassis)
    {
        if (!string.IsNullOrEmpty(wereSpecies)) return wereSpecies;
        var pool = new List<SpeciesSO>();
        foreach (var d in donors) if (d != null && d != chassis) pool.Add(d);
        return pool.Count > 0 ? pool[Random.Range(0, pool.Count)].speciesName : null;
    }
}
