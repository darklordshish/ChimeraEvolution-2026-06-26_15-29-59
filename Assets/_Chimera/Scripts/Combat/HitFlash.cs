using UnityEngine;

/// <summary>
/// Вспышка цели при получении урона (через Health.onDamaged): на миг подменяет _BaseColor.
/// Через MaterialPropertyBlock — общий материал не трогается, мигает только этот объект.
/// </summary>
[RequireComponent(typeof(Health))]
public class HitFlash : MonoBehaviour
{
    [SerializeField] Color flashColor = Color.white;
    [SerializeField] float flashTime = 0.08f;

    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    Renderer[] renderers;
    Color[] baseColors;
    MaterialPropertyBlock mpb;
    float timer;

    void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
        baseColors = new Color[renderers.Length];
        mpb = new MaterialPropertyBlock();

        for (int i = 0; i < renderers.Length; i++)
        {
            var m = renderers[i].sharedMaterial;
            baseColors[i] = (m != null && m.HasProperty(BaseColor)) ? m.GetColor(BaseColor) : Color.gray;
        }

        GetComponent<Health>().onDamaged.AddListener(() => timer = flashTime);
    }

    void Update()
    {
        if (timer <= 0f) return;
        timer -= Time.deltaTime;
        bool on = timer > 0f;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            renderers[i].GetPropertyBlock(mpb);
            mpb.SetColor(BaseColor, on ? flashColor : baseColors[i]);
            renderers[i].SetPropertyBlock(mpb);
        }
    }
}
