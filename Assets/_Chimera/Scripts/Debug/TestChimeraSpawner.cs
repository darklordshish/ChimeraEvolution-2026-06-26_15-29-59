using UnityEngine;

/// <summary>
/// ТЕСТБЕД (#4b-1): спавнит сферу-химеру со СЛУЧАЙНЫМ-спаннинг составом (от доминанты одного вида до истинной
/// химеры, не кин никому), красит по составу (CompositionTint), показывает идентичность (MostKin, ридаут в
/// дев-панели). ПСИХИКИ НЕТ — стоит и демонстрирует состав. Носитель, против которого проверяем следующие
/// слайсы #4b (диспатч/поведение). Положи на объект в сцене, назначь species (Человек/Волк/Змея/Лось/Ёж).
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
        if (go.TryGetComponent<Collider>(out var col)) Destroy(col); // без физ-коллайдера-заглушки

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
        return body;
    }
}
