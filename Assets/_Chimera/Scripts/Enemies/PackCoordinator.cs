using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Координатор стаи. Делает из россыпи волков стаю:
///  • СЛОТЫ ОКРУЖЕНИЯ — раздаёт каждому равномерный угол вокруг игрока, чтобы заходили с флангов/тыла;
///  • ЖЕТОНЫ АТАКИ — одновременно лезут в атаку лишь несколько (maxAttackers), остальные кружат и ждут окна;
///  • ЖЕТОН ЗАХВАТА — держать игрока может только один за раз (иначе death spiral).
/// Авто-создаётся при первом обращении — отдельный объект в сцену ставить не нужно.
/// </summary>
public class PackCoordinator : MonoBehaviour
{
    [SerializeField] int maxAttackers = 4;    // сколько волков грызут одновременно (захват — сверх этого, отдельной ролью)
    [SerializeField] float standoff = 4.5f;   // радиус кольца, на котором ждут не-атакующие

    [Header("Мораль стаи")]
    [SerializeField] int routKillsMin = 3;    // после стольких смертей подряд (случайно из диапазона) ломается мораль
    [SerializeField] int routKillsMax = 5;
    [SerializeField] float routDuration = 4f; // сколько секунд бегут, прежде чем вернуться в режим поиска
    [SerializeField] float routRadius = 12f;  // радиус паники вокруг места гибели — бегут только ближние участники боя

    static PackCoordinator instance;
    public static PackCoordinator Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<PackCoordinator>();
                if (instance == null)
                    instance = new GameObject("PackCoordinator").AddComponent<PackCoordinator>();
            }
            return instance;
        }
    }

    readonly List<WolfAI> wolves = new();
    readonly HashSet<WolfAI> attackers = new();
    WolfAI grabber;
    Transform player;
    Health playerHealth;
    float fearlessUntil;

    public int AttackerCount => attackers.Count;
    public int MaxAttackers => maxAttackers;
    public bool GrabActive => grabber != null;

    // волк завыл: слышат только ближние (в радиусе) — они и сбегаются в стаю. Глобального алерта на всю карту нет.
    public void Howl(Vector3 origin, float radius, Vector3 playerPos)
    {
        float r2 = radius * radius;
        foreach (var w in wolves)
            if (w != null && (w.transform.position - origin).sqrMagnitude <= r2)
                w.Hear(playerPos);
    }

    // вой ВОЖАКА слышен по всей карте: ВСЕ волки узнают, где игрок, и сходятся (в отличие от локального Howl волка)
    public void AlertAll(Vector3 playerPos)
    {
        foreach (var w in wolves)
            if (w != null) w.Hear(playerPos);
    }

    // мораль: страх/бегство — ЛИЧНОЕ у каждого волка (WolfAI). Пул задаёт лишь параметры; ярость вожака гасит страх.
    public bool Fearless => Time.time < fearlessUntil;
    public int RollPanicThreshold() => Random.Range(routKillsMin, routKillsMax + 1); // личный порог храбрости волка
    public float RoutDuration => routDuration;

    public bool AnyRouting()
    {
        foreach (var w in wolves) if (w != null && w.Routing) return true;
        return false;
    }

    // смерть волка пугает ТОЛЬКО ближних участников боя (у места гибели) — каждому +1 к его личному страху
    public void ReportKill(Vector3 deathPos)
    {
        if (Fearless) return;
        float r2 = routRadius * routRadius;
        foreach (var w in wolves)
            if (w != null && w.Engaged && (w.transform.position - deathPos).sqrMagnitude <= r2)
                w.AddFear();
    }

    // приказ вожака (вой): бесстрашие на duration — гасит текущее бегство и обнуляет страх у всех
    public void Rally(float duration)
    {
        fearlessUntil = Time.time + duration;
        foreach (var w in wolves) if (w != null) w.CalmRout();
    }

    Transform Player
    {
        get
        {
            if (player == null)
            {
                var pc = FindAnyObjectByType<PlayerController>();
                if (pc != null) player = pc.transform;
            }
            return player;
        }
    }

    void Update()
    {
        // «в бою», если хоть один волк сейчас преследует игрока — это гейтит реген вне боя
        Transform p = Player;
        if (p == null) return;
        if (playerHealth == null) playerHealth = p.GetComponent<Health>();
        if (playerHealth != null) playerHealth.InCombat = AnyEngaged();
    }

    // есть ли хоть один волк, который сейчас держит игрока в поле зрения (агро на тебя)
    public bool AnyEngaged()
    {
        foreach (var w in wolves)
            if (w != null && w.Engaged) return true;
        return false;
    }

    public void Register(WolfAI w) { if (!wolves.Contains(w)) wolves.Add(w); }

    public void Unregister(WolfAI w)
    {
        wolves.Remove(w);
        attackers.Remove(w);
        if (grabber == w) grabber = null;
    }

    public bool TryAcquireAttack(WolfAI w)
    {
        if (attackers.Contains(w)) return true;
        int cap = Fearless ? Mathf.Max(maxAttackers, wolves.Count) : maxAttackers; // ярость: наваливается вся стая
        if (attackers.Count >= cap) return false;
        attackers.Add(w);
        return true;
    }

    public void ReleaseAttack(WolfAI w) => attackers.Remove(w);

    public bool TryAcquireGrab(WolfAI w)
    {
        if (grabber == null || grabber == w) { grabber = w; return true; }
        return false;
    }

    public void ReleaseGrab(WolfAI w) { if (grabber == w) grabber = null; }

    // Точка на кольце окружения для данного волка (слот = его индекс в стае).
    public Vector3 SlotPoint(WolfAI w)
    {
        Transform p = Player;
        if (p == null) return w.transform.position;
        int n = Mathf.Max(1, wolves.Count);
        int i = wolves.IndexOf(w); if (i < 0) i = 0;
        Vector3 dir = Quaternion.Euler(0f, 360f / n * i, 0f) * Vector3.forward;
        return p.position + dir * standoff;
    }
}
