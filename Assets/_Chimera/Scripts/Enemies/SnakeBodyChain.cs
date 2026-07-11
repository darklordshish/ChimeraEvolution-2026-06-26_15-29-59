using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Цепочка тела змеи: сегменты (шарики + погремушка) тянутся по ПУТИ головы — буфер точек, каждый
/// сегмент сидит на своей дистанции вдоль пути (на поворотах тело изгибается S-ом само). Двигается
/// только корень (CharacterController/NavMesh); сегменты — визуал + свои коллайдеры (тело плотное
/// по всей длине: игрок и волки в него врезаются). Собственный CC игнорирует коллайдеры сегментов.
/// Заполняет генератор префаба (Chimera → Создать префаб Змеи).
/// </summary>
public class SnakeBodyChain : MonoBehaviour
{
    [SerializeField] Transform[] segments;      // от шеи к погремушке (генератор заполняет)
    [SerializeField] float spacing = 0.45f;     // дистанция между сегментами вдоль пути
    [SerializeField] float height = 0.3f;       // высота центров сегментов над путём (путь пишется по земле)
    [SerializeField] float sampleStep = 0.08f;  // шаг записи пути головы
    [SerializeField] int maxSamples = 256;

    readonly List<Vector3> path = new(); // [0] — новейшая точка
    Vector3 lastSample;

    void Awake()
    {
        // сегменты не должны становиться препятствием для СВОЕГО CharacterController
        if (TryGetComponent<CharacterController>(out var cc))
            foreach (var col in GetComponentsInChildren<Collider>())
                if (col != cc) Physics.IgnoreCollision(cc, col);

        // затравка пути: прямая линия назад — на спавне тело лежит вытянутым, а не комом
        lastSample = transform.position;
        path.Add(lastSample);
        float total = spacing * ((segments != null ? segments.Length : 0) + 1);
        for (float d = sampleStep; d <= total; d += sampleStep)
            path.Add(transform.position - transform.forward * d);
    }

    void LateUpdate()
    {
        // пишем путь головы (корня)
        if ((transform.position - lastSample).sqrMagnitude >= sampleStep * sampleStep)
        {
            lastSample = transform.position;
            path.Insert(0, lastSample);
            if (path.Count > maxSamples) path.RemoveAt(path.Count - 1);
        }

        if (segments == null) return;
        for (int i = 0; i < segments.Length; i++)
        {
            if (segments[i] == null) continue;
            Vector3 p = PointAlongPath((i + 1) * spacing, out Vector3 toHead);
            segments[i].position = p + Vector3.up * height;
            if (toHead.sqrMagnitude > 0.0001f) segments[i].rotation = Quaternion.LookRotation(toHead); // погремушка ложится вдоль пути
        }
    }

    // точка на пути в distance позади головы + направление «к голове» в этой точке
    Vector3 PointAlongPath(float distance, out Vector3 dirToHead)
    {
        Vector3 prev = transform.position;
        dirToHead = transform.forward;
        float remaining = distance;
        for (int i = 0; i < path.Count; i++)
        {
            Vector3 pt = path[i];
            float seg = Vector3.Distance(prev, pt);
            if (seg > 0.0001f && seg >= remaining)
            {
                Vector3 pos = Vector3.Lerp(prev, pt, remaining / seg);
                dirToHead = prev - pos;
                return pos;
            }
            remaining -= seg;
            if (seg > 0.0001f) dirToHead = prev - pt;
            prev = pt;
        }
        return prev; // путь короче нужного — хвост у последней записанной точки
    }
}
