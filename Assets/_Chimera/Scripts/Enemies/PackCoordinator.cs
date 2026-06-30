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

    public int AttackerCount => attackers.Count;
    public int MaxAttackers => maxAttackers;
    public bool GrabActive => grabber != null;

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
        if (attackers.Count >= maxAttackers) return false;
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
