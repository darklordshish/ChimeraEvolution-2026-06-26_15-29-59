using UnityEngine;

/// <summary>
/// ТЕСТБЕД (#4b-1): спавнит сферу-химеру со СЛУЧАЙНЫМ-спаннинг составом (от доминанты одного вида до истинной
/// химеры, не кин никому), красит по составу (CompositionTint), показывает идентичность (MostKin, ридаут в
/// дев-панели). ОЖИВЛЯЕТ её (#4b-2): CC/Health/базовый укус → ходячий/дерущийся NPC, затем PsycheDispatch
/// вешает психику (пока всегда химера-альфа). Положи на объект в сцене, назначь species (Человек/Волк/Змея/Лось/Ёж).
/// Спавн — кнопкой в дев-панели (Chimera Dev).
/// </summary>
public class TestChimeraSpawner : MonoBehaviour
{
    [SerializeField] SpeciesSO[] species;    // пул шасси/доноров: назначить 5 видов в инспекторе
    [SerializeField] float spawnRadius = 8f; // где вокруг спавнера появляется
    [SerializeField] int maxAugments = 8;    // потолок случайных аугументов (0..N → спаннинг спектра)

    public CreatureBody SpawnRandom()
    {
        if (species == null || species.Length == 0) { Debug.LogWarning("TestChimeraSpawner: пул species пуст — назначь виды в инспекторе"); return null; }

        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "TestChimera";
        Vector2 c = Random.insideUnitCircle * spawnRadius;
        go.transform.position = transform.position + new Vector3(c.x, 0.5f, c.y);
        // ОЖИВЛЕНИЕ (#4b-2): сфера → ходячий/дерущийся NPC. Порядок важен — CC/Health/укус ДО CreatureBody,
        // чтобы его Awake их нашёл, а Recompute задал HP (health.SetMaxHealth гейтится наличием Health)
        if (go.TryGetComponent<Collider>(out var col)) Destroy(col); // сферный коллайдер долой
        var cc = go.AddComponent<CharacterController>();             // и коллайдер, и мотор для NavLocomotion.Move
        cc.height = 1f; cc.radius = 0.5f; cc.center = Vector3.zero;  // центр капсулы = центр меш-сферы, иначе pivot садится на землю и меш тонет наполовину
        go.AddComponent<Health>();      // тело задаст Max в Recompute (applyVitals)
        go.AddComponent<BiteAbility>(); // базовая атака (гарантия хотя бы одной); Awake создаст и Telegraph

        var body = go.AddComponent<CreatureBody>();
        var chassis = species[Random.Range(0, species.Length)];
        body.Configure(chassis, species, tintFromComposition: true); // все виды — потенциальные доноры; красим по составу
        body.ExpandPool(9999); // ТЕСТБЕД: снять экономику пула. Холодная химера родится с аффинити 0 → без скидки
                               // звериные органы НЕ влезают в пул → все Install'ы молча отваливаются → тело чистое (всё Strong).
                               // Тестбед про СОСТАВ, не про грайнд — даём бесконечный пул, чтобы состав реально размывался.

        // СПАННИНГ: случайное число аугументов в случайные слоты (0..maxAugments) — от «почти чистого» до каши.
        // Реролл даёт и доминантных (получат видовой модуль), и истинных химер (получат химеру-альфу — #4b-2)
        int n = Random.Range(0, maxAugments + 1);
        for (int i = 0; i < n; i++)
        {
            int slot = Random.Range(0, body.SlotCount);
            var vars = body.GetVariants(slot);
            if (vars.Count > 0) body.Install(slot, Random.Range(0, vars.Count)); // случайный вариант (вкл. чужие виды)
        }

        PsycheDispatch.Attach(body); // по идентичности вешает психику (пока всегда альфа) + пере-раздаёт статы

        // СФЕРУ ДОЛОЙ — ПОКАЗЫВАЕМ НАСТОЯЩЕЕ ТЕЛО. Морф строился и раньше (Configure → Recompute →
        // MorphBuilder), но шар-заглушка висел поверх и всё загораживал: тестбед про СОСТАВ показывал
        // состав только цветом. Сносим меш, оставляя объект — на нём висят CC, Health и психика
        // ...НО ТОЛЬКО ЕСЛИ МОРФ ЧТО-ТО ПОСТРОИЛ. У змеиного шасси ВСЕ места `codeDriven` (тело ведёт
        // SnakeBodyChain, которого на тестбеде нет) — морф пуст, и без шара химера была бы НЕВИДИМОЙ.
        // Шар остаётся честным признаком «это шасси ещё не переведено на морф», а не украшением
        if (go.GetComponentsInChildren<Renderer>().Length > 1)
        {
            if (go.TryGetComponent<MeshRenderer>(out var mr)) Destroy(mr);
            if (go.TryGetComponent<MeshFilter>(out var mf)) Destroy(mf);
        }
        // КАПСУЛУ ПОД РАЗМЕР ТЕЛА ЗДЕСЬ НЕ ПОДГОНЯЕМ, и это осознанно: `MorphBuilder` сдвигает сборку
        // к НИЗУ CharacterController (высоты в данных заданы от земли). Поменяй CC после сборки — тело
        // уедет относительно новой капсулы. Правильный порядок — знать габарит ДО морфа, а он берётся
        // из собранного тела: курица и яйцо. Решается вместе с боссом, где размер тела и так понадобится
        return body;
    }
}
