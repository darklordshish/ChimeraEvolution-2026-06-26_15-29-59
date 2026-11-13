using UnityEngine;

/// <summary>
/// Вспышка цели при получении урона (через Health.onDamaged): на миг подменяет _BaseColor.
/// Через MaterialPropertyBlock — общий материал не трогается, мигает только этот объект.
/// </summary>
[RequireComponent(typeof(Health))]
public class HitFlash : MonoBehaviour
{
    [SerializeField] Color flashColor = Color.black; // чёрная читается лучше белой на светлых телах
    [SerializeField] float flashTime = 0.08f;

    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    Renderer[] renderers;
    Color[] baseColors;
    MaterialPropertyBlock mpb;
    Telegraph telegraph;
    float timer;

    void Awake() => Rebuild();

    /// <summary>Пере-собрать рендереры. ЗОВЁТ `CreatureBody` после каждой сборки морфа: части рождаются
    /// в рантайме, а ссылки из Awake к этому моменту мертвы — у волка массив был пуст навсегда (визуал
    /// префаба отключён), у змеи забит уничтоженными ссылками. Вспышки урона не было вообще, и ошибок
    /// в консоли тоже: гвард `!= null` гасил их молча.</summary>
    public void Rebuild()
    {
        var list = new System.Collections.Generic.List<Renderer>();
        foreach (var r in GetComponentsInChildren<Renderer>())
            if (r is MeshRenderer || r is SkinnedMeshRenderer) list.Add(r);   // след/линии не красим
        renderers = list.ToArray();
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

        // откат вспышки: восстановление — ЧЕРЕЗ Telegraph (он знает полный стек: вспышка приёма / градиент
        // обхвата / эмоц-рест-тинт / натуральный) — иначе откат к родному съедал бы телеграф и эмоцию
        if (!on)
        {
            if (telegraph == null) TryGetComponent(out telegraph);
            if (telegraph != null) { telegraph.Reapply(); return; }
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            renderers[i].GetPropertyBlock(mpb);
            mpb.SetColor(BaseColor, on ? flashColor : baseColors[i]);
            renderers[i].SetPropertyBlock(mpb);
        }
    }
}
