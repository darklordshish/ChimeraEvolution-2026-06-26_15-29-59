using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Раскидывает КОРМОВЫЕ КУСТЫ (`Forage`) по проходам лабиринта на старте — еда для травоядных (лось).
/// Точки берёт на НАВМЕШЕ (проходимо, не в стене), не ближе `spacing` друг к другу (кусты не слипаются).
/// Куст — низкий зелёный примитив с `Forage`. Объект в сцене; создаётся ПОСЛЕ бейка навмеша (ArenaWalls
/// строит его в Awake, мы — в Start).
/// </summary>
public class ForageSpawner : MonoBehaviour
{
    [SerializeField] int count = 18;              // сколько кустов по карте
    [SerializeField] float mapHalfExtent = 46f;   // полукрай зоны раскидки (под арену 100)
    [SerializeField] float spacing = 8f;          // кусты не ближе этого друг к другу
    [SerializeField] float bushSize = 1.3f;       // общий размах куста
    [SerializeField] int blobs = 6;               // сфер-«ветвей» в клумбе (больше = пышнее)

    readonly List<Vector3> placed = new();

    void Start()
    {
        for (int i = 0; i < count; i++)
        {
            if (!TryPickPoint(out var p)) continue;
            placed.Add(p);

            // КЛУМБА: корень-пустышка + несколько сфер-«ветвей» внахлёст — пышный округлый куст, не грустный кубик
            var bush = new GameObject("Forage");
            bush.transform.SetParent(transform, false);
            bush.transform.position = p; // низ клумбы на земле
            for (int b = 0; b < blobs; b++)
            {
                var leaf = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.Destroy(leaf.GetComponent<Collider>()); // куст не мешает ходьбе/навмешу — только визуал
                leaf.transform.SetParent(bush.transform, false);
                Vector2 off = Random.insideUnitCircle * 0.5f * bushSize; // разброс вширь
                float h = Random.Range(0.25f, 1.0f) * bushSize;          // разная высота — округлая крона
                leaf.transform.localPosition = new Vector3(off.x, h, off.y);
                float s = Random.Range(0.7f, 1.15f) * bushSize;
                leaf.transform.localScale = new Vector3(s, s * 0.9f, s);  // чуть приплюснуто — кустистее
            }
            bush.AddComponent<Forage>(); // масштабит/красит всю клумбу разом
        }
        if (placed.Count == 0) Debug.LogWarning("ForageSpawner: не нашёл точек на NavMesh — проверь позицию/размах.");
    }

    bool TryPickPoint(out Vector3 point)
    {
        for (int attempt = 0; attempt < 50; attempt++)
        {
            Vector3 c = transform.position + new Vector3(Random.Range(-mapHalfExtent, mapHalfExtent), 0f,
                                                         Random.Range(-mapHalfExtent, mapHalfExtent));
            if (!NavMesh.SamplePosition(c, out var hit, 6f, NavMesh.AllAreas)) continue;
            if (TooClose(hit.position)) continue;
            point = hit.position;
            return true;
        }
        point = Vector3.zero;
        return false;
    }

    bool TooClose(Vector3 p)
    {
        foreach (var q in placed) if ((q - p).sqrMagnitude < spacing * spacing) return true;
        return false;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.30f, 0.52f, 0.22f);
        Gizmos.DrawWireCube(transform.position, new Vector3(mapHalfExtent * 2f, 1f, mapHalfExtent * 2f));
    }
}
