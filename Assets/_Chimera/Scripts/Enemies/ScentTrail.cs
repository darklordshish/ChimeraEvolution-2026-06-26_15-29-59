using UnityEngine;

/// <summary>
/// Запаховый след существа. Видим, только когда у игрока активно волчье Чутьё (Perception.WolfScent).
/// Самодостаточен: создаёт свой TrailRenderer-объект, ассетов не требует. Авто-добавляется волком.
/// </summary>
public class ScentTrail : MonoBehaviour
{
    [SerializeField] float linger = 2f;       // как долго тянется след (сек)
    [SerializeField] float startWidth = 0.5f;
    [SerializeField] Color tint = new Color(0.55f, 0.9f, 0.6f, 0.5f); // бледная дымка

    TrailRenderer trail;

    void Awake()
    {
        var go = new GameObject("ScentTrail");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 0.2f, 0f);

        trail = go.AddComponent<TrailRenderer>();
        trail.time = linger;
        trail.startWidth = startWidth;
        trail.endWidth = 0.04f;
        trail.minVertexDistance = 0.12f;
        trail.numCapVertices = 3;
        trail.alignment = LineAlignment.View; // билборд к камере — читается как дымка
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;

        var shader = Shader.Find("Sprites/Default"); // прозрачный, поддерживает градиент через вершинный цвет
        if (shader != null) trail.material = new Material(shader);
        trail.startColor = tint;
        trail.endColor = new Color(tint.r, tint.g, tint.b, 0f);

        trail.emitting = false; // по умолчанию не виден
    }

    void Update()
    {
        bool on = Perception.WolfScent;
        if (trail.emitting != on)
        {
            if (on) trail.Clear(); // не тянуть линию из старой позиции при включении
            trail.emitting = on;
        }
    }
}
