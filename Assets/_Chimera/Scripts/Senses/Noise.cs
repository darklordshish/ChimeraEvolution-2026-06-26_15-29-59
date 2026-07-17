using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ШУМ существа — эмиттер оси звука (лось, срез B). САМ меряет скорость своего тела каждый кадр:
/// бежишь — громко, крадёшься — тихо, замер — беззвучен. В психики/контроллеры ничего не проводится —
/// любое движущееся тело шумит естественно (дизайн-принцип воронки манка: источник звука + спад
/// с расстоянием, всё происходит само). Spike() — разовый всплеск (атака/приземление/гремок) поверх
/// шума движения. Слушатель опрашивает статикой Hear: СЛЫШИМАЯ ДАЛЬНОСТЬ = громкость × радиус слуха —
/// бегущего слышно через полкарты, крадущегося — в упор. Вешает CreatureBody всем телам.
/// </summary>
public class Noise : MonoBehaviour
{
    [SerializeField] float loudSpeed = 8f; // скорость «полной громкости» (бег/рывок); медленнее — тише линейно
    [SerializeField] float smoothing = 6f; // сглаживание громкости (шаги не мигают нулями между кадрами)

    static readonly List<Noise> all = new();

    Vector3 lastPos;
    float loudness;          // громкость движения 0..1 (сглаженная)
    float spike, spikeUntil; // разовый всплеск поверх движения (гаснет сам)

    public float Loudness => Mathf.Max(loudness, Time.time < spikeUntil ? spike : 0f);

    void OnEnable() { all.Add(this); lastPos = transform.position; }
    void OnDisable() => all.Remove(this);

    /// <summary>Разовый всплеск шума (атака, приземление, гремок): громкость 0..1 на duration секунд.</summary>
    public void Spike(float strength, float duration)
    {
        spike = Mathf.Clamp01(strength);
        spikeUntil = Time.time + duration;
    }

    void Update()
    {
        Vector3 d = transform.position - lastPos; d.y = 0f; // вертикаль (гравитация/климб) не шумит
        lastPos = transform.position;
        float target = Time.deltaTime > 0f ? Mathf.Clamp01(d.magnitude / Time.deltaTime / loudSpeed) : 0f;
        loudness = Mathf.Lerp(loudness, target, 1f - Mathf.Exp(-smoothing * Time.deltaTime));
    }

    /// <summary>Самый громкий СЛЫШИМЫЙ источник для уха в точке ear с радиусом слуха range (свой — исключён;
    /// dev-призрак беззвучен). Слышимая дальность источника = его громкость × range слушателя.</summary>
    public static bool Hear(Vector3 ear, float range, Transform self, out Vector3 pos, out float strength)
    {
        pos = Vector3.zero; strength = 0f;
        foreach (var n in all)
        {
            if (n == null || n.transform == self) continue;
            if (Perception.PlayerGhost && n.GetComponent<PlayerController>() != null) continue; // призрак не слышен (как и не виден)
            float loud = n.Loudness;
            if (loud <= 0.01f) continue;
            float audible = loud * range;
            float dist = Vector3.Distance(ear, n.transform.position);
            if (dist > audible) continue;
            float s = loud * (1f - dist / Mathf.Max(audible, 0.01f)); // сила восприятия: громче и ближе — сильнее
            if (s > strength) { strength = s; pos = n.transform.position; }
        }
        return strength > 0f;
    }
}
