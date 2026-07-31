using UnityEngine;

/// <summary>ЭВОЛЮЦИЯ NPC — тюнинг-объект в сцене (правило проекта: настраиваемое — объектом, не бутстрап).
/// Рантайм-реестр всех видов (его в проекте не было) + параметры химеризации. NPC-тела находят его в Awake:
/// встают на платформу «тело=данные» (donors=все + стартовое родство + цвет по составу). Родство — ЕДИНАЯ ось:
/// родство к виду >0 → его ветка доступна (грант/скидка). startAffinity: 1=отладка (всё приоткрыто), 0=демка.</summary>
public class EvolutionConfig : MonoBehaviour
{
    [SerializeField] SpeciesSO[] allSpecies;               // ВСЕ виды (Человек/Волк/Змея/Лось/Ёж) — назначить в инспекторе
    [SerializeField] bool evolveNpc = true;                // рубильник всей фичи
    [SerializeField, Range(0, 100)] int startAffinity = 1; // стартовое родство ко всем НЕ-своим видам
    [SerializeField] float chimerizeChancePerAffinity = 0.01f; // шанс гранта = родство × это (высокая динамика; тюним)

    public SpeciesSO[] AllSpecies => allSpecies;
    public bool EvolveNpc => evolveNpc;
    public int StartAffinity => startAffinity;
    public float ChimerizeChancePerAffinity => chimerizeChancePerAffinity > 0f ? chimerizeChancePerAffinity : 0.01f;

    static EvolutionConfig instance;
    public static EvolutionConfig Instance => instance != null ? instance : (instance = FindAnyObjectByType<EvolutionConfig>());
}
