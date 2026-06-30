using UnityEngine;

/// <summary>
/// Запаховый след существа: тянущийся след (от движения) + слабая АУРА вокруг тела
/// (видно даже неподвижного волка). Видим, только когда у игрока активно волчье Чутьё
/// (Perception.WolfScent). Самодостаточен: создаёт свои объекты, ассетов не требует.
/// </summary>
public class ScentTrail : MonoBehaviour
{
    [SerializeField] float linger = 3.5f;      // как долго тянется след (сек)
    [SerializeField] float startWidth = 0.65f;
    [SerializeField] Color tint = new Color(0.5f, 0.95f, 0.6f, 0.65f); // дымка
    [SerializeField] float auraSize = 1.6f;    // диаметр ауры запаха вокруг тела

    TrailRenderer trail;
    Renderer aura;

    void Awake()
    {
        var shader = Shader.Find("Sprites/Default"); // прозрачный, без освещения, поддерживает вершинный цвет

        // тянущийся след (от движения)
        var t = new GameObject("ScentTrail");
        t.transform.SetParent(transform, false);
        t.transform.localPosition = new Vector3(0f, 0.2f, 0f);
        trail = t.AddComponent<TrailRenderer>();
        trail.time = linger;
        trail.startWidth = startWidth;
        trail.endWidth = 0.05f;
        trail.minVertexDistance = 0.12f;
        trail.numCapVertices = 3;
        trail.alignment = LineAlignment.View;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
        if (shader != null) trail.material = new Material(shader);
        trail.startColor = tint;
        trail.endColor = new Color(tint.r, tint.g, tint.b, 0f);
        trail.emitting = false;

        // аура вокруг тела — чтобы неподвижный волк тоже «пах»
        var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        if (s.TryGetComponent<Collider>(out var col)) col.enabled = false; // без физики
        s.transform.SetParent(transform, false);
        s.transform.localPosition = new Vector3(0f, 0.3f, 0f);
        s.transform.localScale = Vector3.one * auraSize;
        aura = s.GetComponent<Renderer>();
        aura.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        aura.receiveShadows = false;
        if (shader != null)
        {
            var am = new Material(shader);
            am.color = new Color(tint.r, tint.g, tint.b, 0.22f);
            aura.sharedMaterial = am;
        }
        aura.enabled = false;
    }

    void Update()
    {
        bool on = Perception.WolfScent;
        if (aura != null) aura.enabled = on;
        if (trail.emitting != on)
        {
            if (on) trail.Clear(); // не тянуть линию из старой позиции при включении
            trail.emitting = on;
        }
    }
}
