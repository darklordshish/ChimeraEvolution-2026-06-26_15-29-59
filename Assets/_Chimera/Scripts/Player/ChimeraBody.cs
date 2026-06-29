using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Первый срез конструктора: один слот «Руки» — человек ↔ волчий коготь.
/// Установка гейтится родством с видом; хоткей 1 ставит/снимает. С когтем меняется удар + тинт тела.
/// Дальше нарастим: пул, остальные слоты, шкалу мозга, нормальный UI.
/// </summary>
[RequireComponent(typeof(PlayerAttack))]
public class ChimeraBody : MonoBehaviour
{
    [SerializeField] string species = "Волк";
    [SerializeField] int clawCost = 3;     // сколько родства-волк нужно, чтобы поставить коготь

    [Header("Рука — человек")]
    [SerializeField] int humanDamage = 10;
    [SerializeField] float humanRange = 1.6f;

    [Header("Рука — волчий коготь")]
    [SerializeField] int clawDamage = 18;
    [SerializeField] float clawRange = 1.5f;
    [SerializeField] Color clawTint = new Color(0.55f, 0.42f, 0.4f);

    public bool ClawEquipped { get; private set; }

    PlayerAttack attack;
    InputAction installAction;
    Renderer[] renderers;
    Color[] baseColors;
    MaterialPropertyBlock mpb;
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    void Awake()
    {
        attack = GetComponent<PlayerAttack>();

        installAction = new InputAction("InstallClaw", InputActionType.Button);
        installAction.AddBinding("<Keyboard>/1");

        renderers = GetComponentsInChildren<Renderer>();
        baseColors = new Color[renderers.Length];
        mpb = new MaterialPropertyBlock();
        for (int i = 0; i < renderers.Length; i++)
        {
            var m = renderers[i].sharedMaterial;
            baseColors[i] = (m != null && m.HasProperty(BaseColor)) ? m.GetColor(BaseColor) : Color.gray;
        }
    }

    void OnEnable() => installAction.Enable();
    void OnDisable() => installAction.Disable();

    void Start() => ApplyArm(); // на старте — человеческая рука

    void Update()
    {
        if (installAction.WasPressedThisFrame()) Toggle();
    }

    void Toggle()
    {
        if (!ClawEquipped)
        {
            int have = AffinityTracker.Get(species);
            if (have < clawCost)
            {
                Debug.Log($"Мало родства [{species}]: {have}/{clawCost}");
                return;
            }
            ClawEquipped = true;
            Debug.Log("Поставлен волчий коготь");
        }
        else
        {
            ClawEquipped = false;
            Debug.Log("Снят коготь — человеческая рука");
        }
        ApplyArm();
    }

    void ApplyArm()
    {
        if (ClawEquipped) attack.SetMelee(clawDamage, clawRange);
        else attack.SetMelee(humanDamage, humanRange);
        SetTint(ClawEquipped);
    }

    void SetTint(bool on)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            renderers[i].GetPropertyBlock(mpb);
            mpb.SetColor(BaseColor, on ? clawTint : baseColors[i]);
            renderers[i].SetPropertyBlock(mpb);
        }
    }
}
