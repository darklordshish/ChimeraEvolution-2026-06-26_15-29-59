using UnityEngine;

/// <summary>
/// Визуал запаха существа: тянущийся след (от движения) + аура у тела (видно неподвижного).
/// Видим, когда у игрока активно волчье Чутьё (Perception.WolfScent). СВОЙ след (isOwn) дополнительно
/// гейтится тогглом Perception.ShowOwnScent и без ауры (она была бы на самом игроке — мешает).
/// Configure(color, isOwn) задаёт цвет/принадлежность (зелёный чужой / тёплый свой). Самодостаточен.
/// </summary>
public class ScentTrail : MonoBehaviour
{
    [SerializeField] float linger = 3.5f;
    [SerializeField] float startWidth = 0.65f;
    [SerializeField] Color tint = new Color(0.5f, 0.95f, 0.6f, 0.65f); // дефолт — зелёный (чужой)
    [SerializeField] float auraSize = 1.6f;
    [SerializeField] bool isOwn;

    [SerializeField, Range(0f, 2f)] float strength = 1f; // сила запаха: 1 = потеющий зверь; змея ~0.35; ЛОСЬ ~1.6 (туша пахнет ЗА себя и за стадо)

    TrailRenderer trail;
    Renderer aura;

    void Awake()
    {
        var shader = Shader.Find("Sprites/Default");

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
        trail.emitting = false;

        var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        if (s.TryGetComponent<Collider>(out var col)) col.enabled = false;
        s.transform.SetParent(transform, false);
        s.transform.localPosition = new Vector3(0f, 0.3f, 0f);
        s.transform.localScale = Vector3.one * auraSize;
        aura = s.GetComponent<Renderer>();
        aura.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        aura.receiveShadows = false;
        if (shader != null) aura.sharedMaterial = new Material(shader);
        aura.enabled = false;

        ApplyColor();
    }

    // вызывается после AddComponent: цвет следа + свой/чужой
    public void Configure(Color color, bool own)
    {
        tint = color;
        isOwn = own;
        ApplyColor();
    }

    // сила запаха существа: короче/тоньше след и бледнее аура (змея ~0.35) … длиннее/шире (лось ~1.6:
    // туша пахнет сильнее потеющего человека — шкала растянута за единицу, LerpUnclamped)
    public void SetStrength(float k)
    {
        strength = Mathf.Clamp(k, 0f, 2f);
        if (trail != null)
        {
            trail.time = linger * Mathf.LerpUnclamped(0.4f, 1f, strength);
            trail.startWidth = startWidth * Mathf.LerpUnclamped(0.45f, 1f, strength);
        }
        if (aura != null) aura.transform.localScale = Vector3.one * auraSize * Mathf.LerpUnclamped(0.55f, 1f, strength);
        ApplyColor();
    }

    void ApplyColor()
    {
        float a = Mathf.Min(1f, Mathf.Lerp(0.3f, 1f, strength)); // слабый запах — бледнее (ярче единицы не бывает)
        if (trail != null)
        {
            trail.startColor = new Color(tint.r, tint.g, tint.b, tint.a * a);
            trail.endColor = new Color(tint.r, tint.g, tint.b, 0f);
        }
        if (aura != null && aura.sharedMaterial != null)
            aura.sharedMaterial.color = new Color(tint.r, tint.g, tint.b, 0.22f * a);
    }

    void Update()
    {
        bool show = Perception.WolfScent && (!isOwn || Perception.ShowOwnScent);
        if (aura != null) aura.enabled = show && !isOwn; // свою ауру не рисуем (она была бы на игроке)
        if (trail.emitting != show)
        {
            if (show) trail.Clear();
            trail.emitting = show;
        }
    }
}
