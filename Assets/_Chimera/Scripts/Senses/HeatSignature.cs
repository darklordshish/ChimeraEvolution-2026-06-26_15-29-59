using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// «Тепловая подпись» существа — визуал термозрения ИГРОКА (Пит-орган): тёплый силуэт-дубль мешей
/// тела, видимый СКВОЗЬ стены (шейдер Chimera/ThermalGlow, ZTest Always). Горит, когда термо игрока
/// включено, существо в радиусе и НЕ холоднокровно (ColdBlooded опрашиваем каждый кадр — Сердце
/// меняется в рантайме). Вешается в Awake психик (как ScentTrail). Самодостаточен.
/// Enabled дублей пишем каждый кадр — побеждаем чужие переключатели рендереров (Camouflage).
/// </summary>
public class HeatSignature : MonoBehaviour
{
    static Material sharedMat; // один материал на всех (общий цвет тепла)

    readonly List<Renderer> ghosts = new();
    Transform player;

    void Awake()
    {
        // дубли по видимым мешам тела; выключенные пропускаем (аура запаха в этот момент выключена)
        foreach (var mf in GetComponentsInChildren<MeshFilter>())
        {
            var src = mf.GetComponent<MeshRenderer>();
            if (src == null || !src.enabled) continue;

            var go = new GameObject("HeatGhost");
            go.transform.SetParent(mf.transform, false);
            go.transform.localScale = Vector3.one * 1.02f; // чуть крупнее тела — читается как контур
            go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = Mat();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.enabled = false;
            ghosts.Add(r);
        }
    }

    void Start()
    {
        var pc = FindAnyObjectByType<PlayerController>();
        if (pc != null) player = pc.transform;
    }

    void Update()
    {
        bool show = Perception.ThermalOn
                    && player != null
                    && (transform.position - player.position).sqrMagnitude <=
                       Perception.ThermalRadius * Perception.ThermalRadius
                    && GetComponent<ColdBlooded>() == null; // холоднокровный тепла не излучает
        foreach (var g in ghosts) if (g != null) g.enabled = show;
    }

    static Material Mat()
    {
        if (sharedMat == null)
        {
            var shader = Shader.Find("Chimera/ThermalGlow");
            if (shader == null) shader = Shader.Find("Sprites/Default"); // фолбэк: видно, но не сквозь стены
            sharedMat = new Material(shader);
            if (sharedMat.HasProperty("_Color")) sharedMat.color = new Color(1f, 0.45f, 0.12f, 0.55f);
        }
        return sharedMat;
    }
}
