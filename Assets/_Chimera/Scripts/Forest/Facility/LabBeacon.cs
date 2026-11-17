using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// МАЯК ЛАБОРАТОРИИ (решение геймдизайнера 17.11, проба «давай попробуем фонарь на мачте»): мачта над дверью
/// лаборатории выше крон, на ней фонарь, который туман не гасит, — ориентир «куда идти» из любого угла купола.
/// Ставит и настраивает его <see cref="ForestBuilder"/> (поля «Маяк» в инспекторе); здесь — только тело маяка.
///
/// Как фонарь виден издалека:
///  • шейдер без тумана (`Resources/Лес/Маяк.shader`, `Chimera/Beacon`) — туман его не съедает ни ночью, ни в мгле;
///  • угловой размер не меньше <see cref="Settings.minAngleDeg"/>: перед рендером каждой камеры фонарь раздувается
///    так, чтобы с её места он был не меньше этого угла (у входа, в 900 м, шар в 1,5 м — меньше пикселя);
///  • точечный свет у фонаря — подсветка мачты и крыши вблизи, на дальность не работает.
/// </summary>
public class LabBeacon : MonoBehaviour
{
    [System.Serializable]
    public struct Settings
    {
        public float height;      // от земли у двери до фонаря, м
        public Color color;       // цвет фонаря (без яркости)
        public float intensity;   // яркость: множитель цвета, >1 — пересвет
        public float lampSize;    // диаметр фонаря вблизи, м
        public float minAngleDeg; // меньше этого угла фонарь не становится ни с какого расстояния
        public float glowRange;   // радиус точечного света, м (0 — без света)
    }

    public const string ShaderResource = "Лес/Маяк";
    public const string ShaderName = "Chimera/Beacon";

    public Transform Lamp { get; private set; }
    public Transform Mast { get; private set; }
    public Renderer LampRenderer { get; private set; }
    public Light Glow { get; private set; }
    public Settings Current { get; private set; }

    Material lampMat, mastMat;
    Vector3 basePos;

    /// <summary>Поставить маяк: <paramref name="ground"/> — земля у основания мачты (мировые координаты).</summary>
    public static LabBeacon Build(Transform parent, Vector3 ground, Settings s)
    {
        var go = new GameObject("LabBeacon");
        go.transform.SetParent(parent, true);
        go.transform.position = ground;
        var b = go.AddComponent<LabBeacon>();
        b.basePos = ground;
        b.Create();
        b.Apply(s);
        return b;
    }

    void Create()
    {
        var mast = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        mast.name = "Мачта";
        Kill(mast.GetComponent<Collider>()); // мачта не мешает выходу босса и навмешу
        mast.transform.SetParent(transform, false);
        mastMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mastMat.color = new Color(0.22f, 0.22f, 0.24f);
        mastMat.SetFloat("_Smoothness", 0.1f);
        mast.GetComponent<Renderer>().sharedMaterial = mastMat;
        mast.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        Mast = mast.transform;

        var lamp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        lamp.name = "Фонарь";
        Kill(lamp.GetComponent<Collider>());
        lamp.transform.SetParent(transform, false);
        var shader = Resources.Load<Shader>(ShaderResource);
        if (shader == null) shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            Debug.LogError("[Forest] маяк: нет шейдера " + ShaderName + " (Resources/" + ShaderResource + ")");
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }
        lampMat = new Material(shader);
        LampRenderer = lamp.GetComponent<Renderer>();
        LampRenderer.sharedMaterial = lampMat;
        LampRenderer.shadowCastingMode = ShadowCastingMode.Off;
        LampRenderer.receiveShadows = false;
        Lamp = lamp.transform;

        Glow = lamp.AddComponent<Light>();
        Glow.type = LightType.Point;
        Glow.shadows = LightShadows.None;
    }

    /// <summary>Применить настройки (вызывается при сборке и при правке полей у ForestBuilder в Play).</summary>
    public void Apply(Settings s)
    {
        Current = s;
        float h = Mathf.Max(1f, s.height);
        // Мачта — цилиндр Unity высотой 2 по Y: масштаб Y = половина высоты.
        Mast.localPosition = new Vector3(0f, h * 0.5f, 0f);
        Mast.localScale = new Vector3(0.35f, h * 0.5f, 0.35f);
        Lamp.localPosition = new Vector3(0f, h, 0f);
        Lamp.localScale = Vector3.one * Mathf.Max(0.1f, s.lampSize);
        lampMat.SetColor("_Color", s.color * Mathf.Max(0f, s.intensity));
        Glow.enabled = s.glowRange > 0f;
        Glow.range = Mathf.Max(0f, s.glowRange);
        Glow.color = s.color;
        Glow.intensity = Mathf.Max(0f, s.intensity) * 2f;
    }

    /// <summary>Мировая высота фонаря над землёй у основания.</summary>
    public float LampHeightAboveGround => Lamp != null ? Lamp.position.y - basePos.y : 0f;

    void OnEnable() => RenderPipelineManager.beginCameraRendering += Fit;
    void OnDisable() => RenderPipelineManager.beginCameraRendering -= Fit;

    void Fit(ScriptableRenderContext ctx, Camera cam) => FitFor(cam.transform.position);

    /// <summary>Размер фонаря для взгляда из точки <paramref name="eye"/>: не меньше заданного угла.</summary>
    public void FitFor(Vector3 eye)
    {
        if (Lamp == null) return;
        float d = Vector3.Distance(eye, Lamp.position);
        float byAngle = 2f * d * Mathf.Tan(Current.minAngleDeg * 0.5f * Mathf.Deg2Rad);
        Lamp.localScale = Vector3.one * Mathf.Max(Mathf.Max(0.1f, Current.lampSize), byAngle);
    }

    static void Kill(Object o)
    {
        if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o);
    }

    void OnDestroy()
    {
        if (lampMat != null) Kill(lampMat);
        if (mastMat != null) Kill(mastMat);
    }
}
