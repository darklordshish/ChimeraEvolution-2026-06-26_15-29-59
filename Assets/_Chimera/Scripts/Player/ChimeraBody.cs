using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Конструктор (срез) — data-driven список аугументов. 6 слотов MVP (хоткеи 1–6): человек ↔ волчий орган.
/// Установка гейтится родством; эффекты суммируются от базы и применяются к компонентам;
/// тинт тела растёт по числу звериных слотов («шкала мозга»). Дальше: пул мутагена (стоимость/бюджет), UI.
/// </summary>
[RequireComponent(typeof(PlayerAttack))]
[RequireComponent(typeof(PlayerController))]
[RequireComponent(typeof(Health))]
public class ChimeraBody : MonoBehaviour
{
    [SerializeField] string species = "Волк";
    [SerializeField] Color wolfTint = new Color(0.5f, 0.38f, 0.36f); // цвет при полном озверении

    [Header("База (человек)")]
    [SerializeField] int baseDamage = 10;
    [SerializeField] float baseRange = 1.6f;
    [SerializeField] float baseAtkCooldown = 0.45f;
    [SerializeField] float baseMoveSpeed = 6f;
    [SerializeField] float baseDashSpeed = 20f;
    [SerializeField] float baseDashCooldown = 0.7f;
    [SerializeField] int baseMaxHp = 100;

    class Augment
    {
        public string name, key;
        public int cost;
        public int dDamage, dMaxHp, dLifeSteal;
        public float dRange, dAtkCd, dMove, dDash, dDashCd, dReduce;
        public bool enablesBite; // включает отдельную атаку-укус (слот «Пасть»)
        public InputAction action;
        public bool equipped;
    }

    Augment[] augments;
    PlayerAttack attack;
    PlayerController move;
    Health health;
    PlayerBite bite;
    Renderer[] renderers;
    Color[] baseColors;
    MaterialPropertyBlock mpb;
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    public int MaxSlots => augments != null ? augments.Length : 0;

    public int BeastSlots
    {
        get { int n = 0; if (augments != null) foreach (var a in augments) if (a.equipped) n++; return n; }
    }

    public string BuildSummary
    {
        get
        {
            if (augments == null) return "—";
            var parts = new List<string>();
            foreach (var a in augments) if (a.equipped) parts.Add(a.name);
            return parts.Count == 0 ? "— (человек)" : string.Join(", ", parts);
        }
    }

    void Awake()
    {
        attack = GetComponent<PlayerAttack>();
        move = GetComponent<PlayerController>();
        health = GetComponent<Health>();
        bite = GetComponent<PlayerBite>();

        augments = new[]
        {
            new Augment { name = "Когти",  key = "1", cost = 3, dDamage = 8, dRange = -0.1f },
            new Augment { name = "Ноги",   key = "2", cost = 5, dMove = 3f, dDash = 10f },
            new Augment { name = "Сердце", key = "3", cost = 8, dAtkCd = -0.15f, dMaxHp = 50 },
            new Augment { name = "Чутьё",  key = "4", cost = 4, dDashCd = -0.25f },
            new Augment { name = "Пасть",  key = "5", cost = 6, enablesBite = true }, // даёт укус на Left Shift
            new Augment { name = "Шкура",  key = "6", cost = 7, dReduce = 0.3f },
        };
        foreach (var a in augments)
        {
            a.action = new InputAction(a.name, InputActionType.Button);
            a.action.AddBinding($"<Keyboard>/{a.key}");
        }

        renderers = GetComponentsInChildren<Renderer>();
        baseColors = new Color[renderers.Length];
        mpb = new MaterialPropertyBlock();
        for (int i = 0; i < renderers.Length; i++)
        {
            var m = renderers[i].sharedMaterial;
            baseColors[i] = (m != null && m.HasProperty(BaseColor)) ? m.GetColor(BaseColor) : Color.gray;
        }
    }

    void OnEnable() { foreach (var a in augments) a.action.Enable(); }
    void OnDisable() { foreach (var a in augments) a.action.Disable(); }

    void Start() => Recompute(); // выставить базу (человек)

    void Update()
    {
        foreach (var a in augments)
            if (a.action.WasPressedThisFrame()) Toggle(a);
    }

    void Toggle(Augment a)
    {
        if (!a.equipped)
        {
            int have = AffinityTracker.Get(species);
            if (have < a.cost) { Debug.Log($"Мало родства [{species}] для «{a.name}»: {have}/{a.cost}"); return; }
        }
        a.equipped = !a.equipped;
        Debug.Log((a.equipped ? "Поставлен орган: " : "Снят орган: ") + a.name);
        Recompute();
    }

    void Recompute()
    {
        int dmg = baseDamage, maxHp = baseMaxHp, life = 0, beast = 0;
        float rng = baseRange, atkCd = baseAtkCooldown, mv = baseMoveSpeed, dash = baseDashSpeed, dashCd = baseDashCooldown, reduce = 0f;

        bool biteOn = false;
        foreach (var a in augments)
        {
            if (!a.equipped) continue;
            dmg += a.dDamage; maxHp += a.dMaxHp; life += a.dLifeSteal;
            rng += a.dRange; atkCd += a.dAtkCd; mv += a.dMove; dash += a.dDash; dashCd += a.dDashCd; reduce += a.dReduce;
            if (a.enablesBite) biteOn = true;
            beast++;
        }

        if (bite != null) bite.BiteEnabled = biteOn;

        attack.SetMelee(dmg, Mathf.Max(0.5f, rng));
        attack.SetCooldown(Mathf.Max(0.05f, atkCd));
        attack.SetLifeSteal(life);
        move.SetLegs(mv, dash);
        move.SetDashCooldown(Mathf.Max(0.05f, dashCd));
        health.SetMaxHealth(maxHp);
        health.DamageReduction = Mathf.Clamp01(reduce);

        UpdateTint(beast);
    }

    void UpdateTint(int beast)
    {
        float k = MaxSlots > 0 ? (float)beast / MaxSlots : 0f;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            renderers[i].GetPropertyBlock(mpb);
            mpb.SetColor(BaseColor, Color.Lerp(baseColors[i], wolfTint, k));
            renderers[i].SetPropertyBlock(mpb);
        }
    }
}
