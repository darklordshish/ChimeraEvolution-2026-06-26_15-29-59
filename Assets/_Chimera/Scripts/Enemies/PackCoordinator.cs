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
    [SerializeField] int maxAttackers = 4;    // грызут одновременно (захват — сверх, отдельной ролью): 4 грызут + 1 держит = 5 в упоре
    [SerializeField] float standoff = 5.5f;   // радиус КОЛЬЦА ОЖИДАНИЯ (battle-circle: слотовые ждут здесь)
    [SerializeField] int ringSlots = 10;      // фикс. слотов на кольце ожидания — волк резервирует БЛИЖАЙШИЙ свободный
    [SerializeField] float looseRadius = 9f;  // РЫХЛАЯ СТАЯ: кому не хватило слота — держатся поодаль на этом радиусе (не жмутся)

    [Header("Мораль стаи")]
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

    readonly List<WolfPsyche> wolves = new();
    readonly HashSet<WolfPsyche> attackers = new();
    readonly Dictionary<WolfPsyche, float> looseAngle = new(); // персональный угол РЫХЛОЙ стаи (золотой угол, стабилен на всю жизнь)
    int looseCounter;
    WolfPsyche[] ring;   // слоты кольца ожидания: ring[i] = владелец слота i (или null)
    WolfPsyche grabber;

    void EnsureRing() { if (ring == null || ring.Length != Mathf.Max(1, ringSlots)) ring = new WolfPsyche[Mathf.Max(1, ringSlots)]; }
    Transform player;
    Health playerHealth;
    float fearlessUntil;

    public int AttackerCount => attackers.Count;
    public int MaxAttackers => maxAttackers;
    public bool GrabActive => grabber != null;

    // волк завыл: слышат только ближние (в радиусе) — сбегаются в стаю и ЗАВОДЯТСЯ (ярость-пульс).
    [SerializeField] float packHowlGap = 7f; // стая не воет ХОРОМ: один голос раз в столько секунд (фон морали ≈ +1)
    float lastHowlAt = -999f;

    // волк просит право голоса: занято — молчит (перекличка, не сирена). Вой вервольфа-вожака вне очереди
    public bool TryClaimHowl()
    {
        if (Time.time < lastHowlAt + packHowlGap) return false;
        lastHowlAt = Time.time;
        return true;
    }

    public void Howl(Vector3 origin, float radius, Vector3 playerPos)
    {
        float r2 = radius * radius;
        foreach (var w in wolves)
            if (w != null && (w.transform.position - origin).sqrMagnitude <= r2)
            {
                w.Hear(playerPos);
                w.Cheer(1f); // вой = +1 к шкале духа; ярость больше не дарится напрямую — придёт коммитом шкалы (M2)
            }
    }

    // вой ВОЖАКА — тоже ЛОКАЛЕН (на арене 200 «вся карта» была мега-навалом всех 30+): ближние узнают
    // точку сбора и сходятся, дальние живут своей жизнью — лес не схлопывается в одну кучу
    public void AlertAround(Vector3 origin, float radius, Vector3 target)
    {
        float r2 = radius * radius;
        foreach (var w in wolves)
            if (w != null && (w.transform.position - origin).sqrMagnitude <= r2) w.Hear(target);
    }

    // мораль: страх/бегство — ЛИЧНОЕ у каждого волка (WolfPsyche). Пул задаёт лишь параметры; ярость вожака гасит страх.
    public bool Fearless => Time.time < fearlessUntil;
    // пороги храбрости и длительность паники живут теперь в Morale/Personality (шкала стаков, спека 2026-07-17)

    public bool AnyRouting()
    {
        foreach (var w in wolves) if (w != null && w.Routing) return true;
        return false;
    }

    // смерть волка пугает ТОЛЬКО ближних участников боя (у места гибели) — каждому +1 к его личному страху
    public void ReportKill(Vector3 deathPos)
    {
        // гейт Fearless не нужен: приказ вожака (+5) перевешивает −1 смерти АРИФМЕТИЧЕСКИ (шкала сама решает)
        float r2 = routRadius * routRadius;
        foreach (var w in wolves)
            if (w != null && w.Engaged && (w.transform.position - deathPos).sqrMagnitude <= r2)
                w.AddFear();
    }

    // приказ вожака (вой): ЛОКАЛЬНО — +5 духа ближним (мораль над любым порогом → коммит → ярость сама)
    // + стирает страхи; приказное окно (кап атакующих снят) остаётся глобальным флагом координатора
    public void Rally(Vector3 origin, float radius, float duration)
    {
        fearlessUntil = Time.time + duration;
        float r2 = radius * radius;
        foreach (var w in wolves)
            if (w != null && (w.transform.position - origin).sqrMagnitude <= r2) { w.CalmRout(); w.Cheer(5f); }
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
        if (playerHealth != null && AnyEngaged()) playerHealth.MarkInCombat(); // хоть один волк агрится → в бою
    }

    // есть ли хоть один волк, который сейчас держит игрока в поле зрения (агро на тебя)
    public bool AnyEngaged()
    {
        foreach (var w in wolves)
            if (w != null && w.Engaged) return true;
        return false;
    }

    public void Register(WolfPsyche w)
    {
        if (wolves.Contains(w)) return;
        wolves.Add(w);
        looseAngle[w] = looseCounter++ * 137.508f; // ЗОЛОТОЙ УГОЛ рыхлой стаи: ровный разброс при любом числе
    }

    public void Unregister(WolfPsyche w)
    {
        wolves.Remove(w);
        attackers.Remove(w);
        looseAngle.Remove(w);
        ReleaseRingSlot(w);
        if (grabber == w) grabber = null;
    }

    void ReleaseRingSlot(WolfPsyche w) { if (ring != null) for (int i = 0; i < ring.Length; i++) if (ring[i] == w) ring[i] = null; }

    public bool TryAcquireAttack(WolfPsyche w)
    {
        if (attackers.Contains(w)) return true;
        int cap = Fearless ? Mathf.Max(maxAttackers, wolves.Count) : maxAttackers; // ярость: наваливается вся стая
        if (attackers.Count >= cap) return false;
        attackers.Add(w); // first-come среди готовых в зоне; «ближайший-гейт» пробовали — голодание жетонов (кусал один)
        ReleaseRingSlot(w); // ушёл в упор → слот на кольце свободен (ротация: ближайший из рыхлой стаи займёт)
        return true;
    }

    public void ReleaseAttack(WolfPsyche w) => attackers.Remove(w);

    public bool TryAcquireGrab(WolfPsyche w)
    {
        if (grabber == null || grabber == w) { grabber = w; ReleaseRingSlot(w); return true; } // держит в упоре → слот свободен
        return false;
    }

    public void ReleaseGrab(WolfPsyche w) { if (grabber == w) grabber = null; }

    // волк покинул строй (потерял игрока/бежит) → освободить его слот кольца, чтобы занял другой
    public void LeaveRing(WolfPsyche w) => ReleaseRingSlot(w);

    // BATTLE-CIRCLE: куда идёт НЕ-атакующий волк. Владеет слотом кольца → его точка; иначе занимает БЛИЖАЙШИЙ
    // свободный слот (резервация — никто не пересекает толпу, не дерётся за место); нет свободных → РЫХЛАЯ СТАЯ
    // поодаль (личный золотой угол). Слоты кольца разнесены шире радиуса расталкивания → сел → сепарация 0 → не дрожит.
    public Vector3 StandoffPoint(WolfPsyche w)
    {
        Transform p = Player;
        if (p == null) return w.transform.position;
        EnsureRing();
        int slot = System.Array.IndexOf(ring, w);
        if (slot < 0) slot = ClaimRingSlot(w); // ещё не на кольце — займём ближайший свободный слот
        if (slot >= 0) return p.position + RingDir(slot) * standoff; // ждём на своём слоте кольца
        float a = looseAngle.TryGetValue(w, out float ang) ? ang : 0f; // кольцо занято → рыхлая стая снаружи
        return p.position + Quaternion.Euler(0f, a, 0f) * Vector3.forward * looseRadius;
    }

    // ближайший к волку СВОБОДНЫЙ слот кольца (резервируем за ним); -1 если все заняты
    int ClaimRingSlot(WolfPsyche w)
    {
        Transform p = Player; if (p == null) return -1;
        int best = -1; float bestD = float.MaxValue;
        for (int i = 0; i < ring.Length; i++)
        {
            if (ring[i] != null) continue;
            float d = (p.position + RingDir(i) * standoff - w.transform.position).sqrMagnitude;
            if (d < bestD) { bestD = d; best = i; }
        }
        if (best >= 0) ring[best] = w;
        return best;
    }

    Vector3 RingDir(int i) => Quaternion.Euler(0f, 360f / Mathf.Max(1, ring.Length) * i, 0f) * Vector3.forward;
}
