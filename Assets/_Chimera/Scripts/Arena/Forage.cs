using UnityEngine;

/// <summary>
/// КОРМОВОЙ КУСТ (ива/поросль — биология: лось броузер, обгладывает кусты) — еда травоядного. Голодный
/// лось идёт к необъеденному кусту и щиплет его: `Graze` отдаёт корм (наполняет `Satiety`), куст истощается
/// и со временем ОТРАСТАЕТ. Так лось живёт «от еды до еды», а не бесцельно бродит — и волки перехватывают
/// его на тропе. Само отражает состояние размером и цветом (пышный зелёный ↔ объеденный бурый огрызок).
/// </summary>
public class Forage : MonoBehaviour
{
    [SerializeField] float regenRate = 0.03f;               // /с — как быстро отрастает объеденный
    [SerializeField, Range(0f, 1f)] float minEdible = 0.15f; // ниже — огрызок, лось не трогает (ищет свежий)

    float amount = 1f; // 0 объеден … 1 пышный
    Renderer[] rends;  // вся листва клумбы (несколько сфер) — красим и масштабируем разом
    MaterialPropertyBlock mpb;
    Vector3 fullScale;

    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    static readonly Color Lush = new(0.30f, 0.52f, 0.22f);  // сочная зелень
    static readonly Color Bare = new(0.42f, 0.36f, 0.24f);  // объеденный бурый

    public bool IsEdible => amount > minEdible;

    void Awake()
    {
        rends = GetComponentsInChildren<Renderer>();
        mpb = new MaterialPropertyBlock();
        fullScale = transform.localScale;
        Refresh();
    }

    /// <summary>Щипок: съедаем до bite (не ниже нуля), возвращаем СКОЛЬКО реально съели — это и есть корм.</summary>
    public float Graze(float bite)
    {
        float g = Mathf.Clamp(Mathf.Min(bite, amount), 0f, 1f);
        amount -= g;
        Refresh();
        return g;
    }

    void Update()
    {
        if (amount >= 1f) return;
        amount = Mathf.Min(1f, amount + regenRate * Time.deltaTime); // отрастает
        Refresh();
    }

    // размер и цвет по объёму: пышная зелёная клумба → сжавшийся бурый огрызок (масштаб РАВНОМЕРНО — куст усыхает)
    void Refresh()
    {
        transform.localScale = fullScale * (0.35f + 0.65f * amount);
        if (rends == null) return;
        Color c = Color.Lerp(Bare, Lush, amount);
        mpb.SetColor(BaseColor, c);
        foreach (var r in rends) if (r != null) r.SetPropertyBlock(mpb);
    }
}
